using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Attendance;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IAttendanceService
{
    Task<IEnumerable<AttendanceDto>> GetByEventAsync(Guid eventId);
    Task<AttendanceDto>              MarkAttendanceAsync(MarkAttendanceDto dto);
    Task<IEnumerable<AttendanceDto>> BulkMarkAsync(BulkAttendanceDto dto);
    Task<AttendanceDto?>             MarkByQrAsync(QrAttendanceDto dto);
    Task<AttendanceSummaryDto>       GetSummaryAsync(Guid eventId);
    Task<IEnumerable<AttendanceDto>> GetMemberHistoryAsync(Guid memberId);
}

public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public AttendanceService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<AttendanceDto>> GetByEventAsync(Guid eventId)
    {
        var records = await _uow.AttendanceRecords.Query()
            .Include(a => a.Event)
            .Include(a => a.Member).ThenInclude(m => m.Troop)
            .Include(a => a.AutoPoints)
            .Where(a => a.EventId == eventId && !a.IsDeleted)
            .ToListAsync();

        return records.Select(MapToDto);
    }

    public async Task<AttendanceDto> MarkAttendanceAsync(MarkAttendanceDto dto)
    {
        // Load member with excuses to check for active excuse
        var member = await _uow.Members.Query()
            .Include(m => m.Excuses)
            .FirstOrDefaultAsync(m => m.Id == dto.MemberId && !m.IsDeleted)
            ?? throw new KeyNotFoundException($"Member {dto.MemberId} not found");

        // Auto-promote Absent → Excused if the member has an active excuse
        var effectiveStatus = dto.Status;
        if (effectiveStatus == AttendanceStatus.Absent &&
            member.HasActiveExcuse(DateTime.UtcNow))
        {
            effectiveStatus = AttendanceStatus.Excused;
        }

        var existing = await _uow.AttendanceRecords.FindSingleAsync(
            a => a.EventId == dto.EventId && a.MemberId == dto.MemberId && !a.IsDeleted);

        if (existing != null)
        {
            existing.Status    = effectiveStatus;
            existing.Notes     = dto.Notes;
            existing.MarkedAt  = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            _uow.AttendanceRecords.Update(existing);
            await _uow.SaveChangesAsync();
            await UpdateAutoPointsAsync(existing);
            return await GetAttendanceDtoAsync(existing.Id);
        }

        var record = new AttendanceRecord
        {
            EventId  = dto.EventId,
            MemberId = dto.MemberId,
            Status   = effectiveStatus,
            Notes    = dto.Notes,
            MarkedBy = _currentUser.UserId,
            MarkedAt = DateTime.UtcNow
        };

        await _uow.AttendanceRecords.AddAsync(record);
        await _uow.SaveChangesAsync();
        await CreateAutoPointsAsync(record);
        return await GetAttendanceDtoAsync(record.Id);
    }

    public async Task<IEnumerable<AttendanceDto>> BulkMarkAsync(BulkAttendanceDto dto)
    {
        var results = new List<AttendanceDto>();
        foreach (var item in dto.Records)
        {
            var result = await MarkAttendanceAsync(new MarkAttendanceDto
            {
                EventId  = dto.EventId,
                MemberId = item.MemberId,
                Status   = item.Status,
                Notes    = item.Notes
            });
            results.Add(result);
        }
        return results;
    }

    public async Task<AttendanceDto?> MarkByQrAsync(QrAttendanceDto dto)
    {
        var member = await _uow.Members.FindSingleAsync(m => m.QrCode == dto.QrToken && !m.IsDeleted);
        if (member is null) return null;

        return await MarkAttendanceAsync(new MarkAttendanceDto
        {
            EventId  = dto.EventId,
            MemberId = member.Id,
            Status   = AttendanceStatus.Present
        });
    }

    public async Task<AttendanceSummaryDto> GetSummaryAsync(Guid eventId)
    {
        var ev      = await _uow.Events.GetByIdAsync(eventId);
        var records = await _uow.AttendanceRecords.FindAsync(a => a.EventId == eventId && !a.IsDeleted);

        var list = records.ToList();
        return new AttendanceSummaryDto
        {
            EventId        = eventId,
            EventName      = ev?.Name ?? string.Empty,
            TotalMembers   = list.Count,
            Present        = list.Count(r => r.Status == AttendanceStatus.Present),
            Late           = list.Count(r => r.Status == AttendanceStatus.Late),
            Absent         = list.Count(r => r.Status == AttendanceStatus.Absent),
            Excused        = list.Count(r => r.Status == AttendanceStatus.Excused),
            AttendanceRate = list.Count == 0 ? 0 :
                (double)list.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late) /
                list.Count * 100
        };
    }

    public async Task<IEnumerable<AttendanceDto>> GetMemberHistoryAsync(Guid memberId)
    {
        var records = await _uow.AttendanceRecords.Query()
            .Include(a => a.Event)
            .Include(a => a.Member).ThenInclude(m => m.Troop)
            .Include(a => a.AutoPoints)
            .Where(a => a.MemberId == memberId && !a.IsDeleted)
            .OrderByDescending(a => a.Event.EventDate)
            .ToListAsync();

        return records.Select(MapToDto);
    }

    // ─── Auto-points: uses Event-level PointValue / LatePointValue ───────────

    private async Task CreateAutoPointsAsync(AttendanceRecord record)
    {
        if (record.Status == AttendanceStatus.Absent) return;

        var ev = await _uow.Events.GetByIdAsync(record.EventId);
        if (ev is null) return;

        // Points come directly from the event — no global category lookup needed
        decimal pts = record.Status switch
        {
            AttendanceStatus.Present => ev.PointValue,
            AttendanceStatus.Late    => ev.LatePointValue,
            AttendanceStatus.Excused => ev.PointValue,    // full points for excused
            _                        => 0
        };

        if (pts <= 0) return;

        // Find or create the "Attendance" category scoped to this member's group
        // (used only as a label/bucket for the leaderboard breakdown — not for value)
        var member = await _uow.Members.GetByIdAsync(record.MemberId);
        if (member is null) return;

        var category = await GetOrSeedAttendanceCategoryAsync(member.GroupId);
        if (category is null) return;

        var mp = new MemberPoints
        {
            MemberId              = record.MemberId,
            MemberPointCategoryId = category.Id,
            Points                = pts,
            Date                  = DateTime.UtcNow,
            Note                  = $"Auto: {record.Status} — {ev.Name}",
            AddedBy               = _currentUser.UserId,
            AttendanceRecordId    = record.Id
        };

        await _uow.MemberPoints.AddAsync(mp);
        await _uow.SaveChangesAsync();
    }

    /// <summary>
    /// Returns the "Attendance" MemberPointCategory scoped to the given group,
    /// creating it if it does not yet exist so points are never silently dropped.
    /// </summary>
    private async Task<Domain.Entities.MemberPointCategory?> GetOrSeedAttendanceCategoryAsync(Guid groupId)
    {
        // Filter by GroupId to avoid "sequence contains more than one element" when
        // multiple groups each have their own "Attendance" category.
        var category = await _uow.MemberPointCategories.FindSingleAsync(
            c => c.Name == "Attendance" && !c.IsDeleted && c.GroupId == groupId);

        if (category is not null) return category;

        category = new Domain.Entities.MemberPointCategory
        {
            Name        = "Attendance",
            Description = "Auto-awarded for attending events",
            GroupId     = groupId
        };
        await _uow.MemberPointCategories.AddAsync(category);
        await _uow.SaveChangesAsync();
        return category;
    }

    private async Task UpdateAutoPointsAsync(AttendanceRecord record)
    {
        // Find ALL auto-point records linked to this attendance record (including previously
        // soft-deleted ones) and unlink them. Without this, EF's one-to-one Include throws
        // "sequence contains more than one element" on the second+ status change.
        var linked = await _uow.MemberPoints.Query()
            .Where(mp => mp.AttendanceRecordId == record.Id)
            .ToListAsync();

        foreach (var mp in linked)
        {
            mp.AttendanceRecordId = null;   // unlink FK so Include never finds stale rows
            mp.IsDeleted          = true;
            mp.UpdatedAt          = DateTime.UtcNow;
            _uow.MemberPoints.Update(mp);
        }

        if (linked.Count > 0)
            await _uow.SaveChangesAsync();

        await CreateAutoPointsAsync(record);
    }

    private async Task<AttendanceDto> GetAttendanceDtoAsync(Guid id)
    {
        var record = await _uow.AttendanceRecords.Query()
            .Include(a => a.Event)
            .Include(a => a.Member).ThenInclude(m => m.Troop)
            .Include(a => a.AutoPoints)
            .FirstOrDefaultAsync(a => a.Id == id);
        return record is null
            ? throw new InvalidOperationException($"Attendance record {id} not found")
            : MapToDto(record);
    }

    private static AttendanceDto MapToDto(AttendanceRecord a) => new()
    {
        Id            = a.Id,
        EventId       = a.EventId,
        EventName     = a.Event?.Name ?? string.Empty,
        MemberId      = a.MemberId,
        MemberName    = a.Member?.FullName ?? string.Empty,
        TroopName     = a.Member?.Troop?.Name ?? string.Empty,
        Status        = a.Status,
        StatusName    = a.Status.ToString(),
        Notes         = a.Notes,
        MarkedAt      = a.MarkedAt,
        // Guard: only use AutoPoints if it hasn't been soft-deleted (stale linked rows)
        PointsAwarded = (a.AutoPoints is { IsDeleted: false }) ? a.AutoPoints.Points : null
    };
}
