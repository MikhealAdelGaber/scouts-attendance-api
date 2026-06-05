using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Attendance;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IAttendanceService
{
    Task<IEnumerable<AttendanceDto>>          GetByEventAsync(Guid eventId);
    Task<IEnumerable<EventMemberStatusDto>>   GetEventMemberStatusesAsync(Guid eventId);
    Task<AttendanceDto>                       MarkAttendanceAsync(MarkAttendanceDto dto);
    Task<IEnumerable<AttendanceDto>>          BulkMarkAsync(BulkAttendanceDto dto);
    Task<AttendanceDto?>                      MarkByQrAsync(QrAttendanceDto dto);
    Task<AttendanceSummaryDto>                GetSummaryAsync(Guid eventId);
    Task<IEnumerable<AttendanceDto>>          GetMemberHistoryAsync(Guid memberId);
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

    // ─── Queries ─────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Returns ALL members applicable to the event (filtered by troop if the event is
    /// troop-scoped) together with their effective attendance status.
    ///
    /// Members that already have an attendance record → their saved status is used.
    /// Members with no record yet → status is auto-derived:
    ///   • HasActiveExcuse covering EventDate  → Excused
    ///   • Otherwise                           → Absent
    /// </summary>
    public async Task<IEnumerable<EventMemberStatusDto>> GetEventMemberStatusesAsync(Guid eventId)
    {
        var ev = await _uow.Events.Query()
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        var eventDate = ev.EventDate.Date;

        // Load members scoped to this event (troop or whole group)
        var membersQuery = _uow.Members.Query()
            .Include(m => m.Troop)
            .Include(m => m.Excuses)
            .Where(m => m.GroupId == ev.GroupId && !m.IsDeleted);

        if (ev.TroopId.HasValue)
            membersQuery = membersQuery.Where(m => m.TroopId == ev.TroopId.Value);

        var members = await membersQuery.ToListAsync();

        // Load existing records for this event (including soft-deleted guard via !IsDeleted)
        var existing = await _uow.AttendanceRecords.Query()
            .Include(a => a.AutoPoints)
            .Where(a => a.EventId == eventId && !a.IsDeleted)
            .ToListAsync();

        var existingMap = existing.ToDictionary(a => a.MemberId);

        return members.Select(m =>
        {
            if (existingMap.TryGetValue(m.Id, out var rec))
            {
                return new EventMemberStatusDto
                {
                    MemberId          = m.Id,
                    MemberName        = m.FullName,
                    CustomId          = m.CustomId,
                    Gender            = (int)m.Gender,
                    TroopId           = m.TroopId,
                    TroopName         = m.Troop?.Name ?? string.Empty,
                    ProfileImageUrl   = m.ProfileImageUrl,
                    HasActiveExcuse   = m.HasActiveExcuse(eventDate),
                    Status            = rec.Status,
                    HasExistingRecord = true,
                    RecordId          = rec.Id,
                    Notes             = rec.Notes,
                    PointsAwarded     = (rec.AutoPoints is { IsDeleted: false })
                                        ? rec.AutoPoints.Points : null
                };
            }
            else
            {
                // No record yet: derive default status from excuse coverage on event date
                var hasExcuse     = m.HasActiveExcuse(eventDate);
                var defaultStatus = hasExcuse ? AttendanceStatus.Excused : AttendanceStatus.Absent;

                return new EventMemberStatusDto
                {
                    MemberId          = m.Id,
                    MemberName        = m.FullName,
                    CustomId          = m.CustomId,
                    Gender            = (int)m.Gender,
                    TroopId           = m.TroopId,
                    TroopName         = m.Troop?.Name ?? string.Empty,
                    ProfileImageUrl   = m.ProfileImageUrl,
                    HasActiveExcuse   = hasExcuse,
                    Status            = defaultStatus,
                    HasExistingRecord = false,
                    RecordId          = null,
                    Notes             = null,
                    PointsAwarded     = null
                };
            }
        });
    }

    // ─── Mutations ───────────────────────────────────────────────────────────

    public async Task<AttendanceDto> MarkAttendanceAsync(MarkAttendanceDto dto)
    {
        // Load the event first — we need EventDate for excuse date comparison
        var ev = await _uow.Events.GetByIdAsync(dto.EventId)
            ?? throw new KeyNotFoundException($"Event {dto.EventId} not found");

        // Load member with excuses to check for active excuse on the EVENT date
        var member = await _uow.Members.Query()
            .Include(m => m.Excuses)
            .FirstOrDefaultAsync(m => m.Id == dto.MemberId && !m.IsDeleted)
            ?? throw new KeyNotFoundException($"Member {dto.MemberId} not found");

        // ── SECURITY: Group membership check ─────────────────────────────────
        // Reject if the member belongs to a different group than the event.
        // This prevents cross-group attendance and illegitimate points.
        if (member.GroupId != ev.GroupId)
            throw new InvalidOperationException(
                $"Member \"{member.FullName}\" does not belong to this group. Attendance not recorded.");

        // Also reject if the event is scoped to a specific troop and the member
        // is not in that troop (they may be in the same group but a different troop).
        if (ev.TroopId.HasValue && member.TroopId != ev.TroopId.Value)
            throw new InvalidOperationException(
                $"Member \"{member.FullName}\" is not in the troop assigned to this event. Attendance not recorded.");

        // Auto-promote Absent → Excused if the member has an active excuse
        // covering the EVENT's scheduled date (not today's date).
        var effectiveStatus = dto.Status;
        if (effectiveStatus == AttendanceStatus.Absent &&
            member.HasActiveExcuse(ev.EventDate))
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
            await UpdateAutoPointsAsync(existing, ev);
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
        await CreateAutoPointsAsync(record, ev);
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
        // Load the event to know its group/troop scope and event date
        var ev = await _uow.Events.GetByIdAsync(eventId);
        if (ev is null)
            return new AttendanceSummaryDto { EventId = eventId };

        var eventDate = ev.EventDate.Date;

        // ALL members in scope — same scoping as GetEventMemberStatusesAsync
        var membersQuery = _uow.Members.Query()
            .Include(m => m.Excuses)
            .Where(m => m.GroupId == ev.GroupId && !m.IsDeleted);
        if (ev.TroopId.HasValue)
            membersQuery = membersQuery.Where(m => m.TroopId == ev.TroopId.Value);
        var members = await membersQuery.ToListAsync();

        // Existing attendance records keyed by MemberId
        var records    = await _uow.AttendanceRecords.FindAsync(
                             a => a.EventId == eventId && !a.IsDeleted);
        var recordMap  = records.ToDictionary(r => r.MemberId, r => r.Status);

        // Tally — members with no record get an auto-derived status
        // (Excused if an active excuse covers the event date, Absent otherwise)
        int present = 0, late = 0, tooLate = 0, absent = 0, excused = 0;
        foreach (var m in members)
        {
            if (recordMap.TryGetValue(m.Id, out var status))
            {
                switch (status)
                {
                    case AttendanceStatus.Present: present++;  break;
                    case AttendanceStatus.Late:    late++;     break;
                    case AttendanceStatus.TooLate: tooLate++; break;
                    case AttendanceStatus.Excused: excused++; break;
                    default:                       absent++;   break;
                }
            }
            else
            {
                // No record yet → auto-derive from active excuse coverage
                if (m.HasActiveExcuse(eventDate)) excused++;
                else                              absent++;
            }
        }

        int total = members.Count;
        // Rate = (Present + Late + TooLate + Excused) / TotalMembers × 100
        // TooLate members did physically attend — counted toward rate
        double rate = total == 0 ? 0
            : Math.Round((present + late + tooLate + excused) * 100.0 / total, 1);

        return new AttendanceSummaryDto
        {
            EventId        = eventId,
            EventName      = ev.Name,
            TotalMembers   = total,
            Present        = present,
            Late           = late,
            TooLate        = tooLate,
            Absent         = absent,
            Excused        = excused,
            AttendanceRate = rate
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

    // ─── Auto-points ─────────────────────────────────────────────────────────

    /// <summary>
    /// Awards (or deducts) points for an attendance record based on the event's
    /// per-status point configuration.  Negative points (e.g. AbsentPoints = -10)
    /// are stored as-is and are correctly summed in the leaderboard.
    /// A zero-point status produces no MemberPoints row (no clutter).
    /// </summary>
    private async Task CreateAutoPointsAsync(AttendanceRecord record, Domain.Entities.Event ev)
    {
        // Excused earns the same points as Present — the member had a valid excuse.
        decimal pts = record.Status switch
        {
            AttendanceStatus.Present => ev.PresentPoints,
            AttendanceStatus.Late    => ev.LatePoints,
            AttendanceStatus.TooLate => ev.TooLatePoints,
            AttendanceStatus.Excused => ev.PresentPoints,   // same as Present
            AttendanceStatus.Absent  => ev.AbsentPoints,
            _                        => 0
        };

        // Skip if exactly 0 — no need to clutter the points history
        if (pts == 0) return;

        var member = await _uow.Members.GetByIdAsync(record.MemberId);
        if (member is null) return;

        var category = await GetOrSeedAttendanceCategoryAsync(member.GroupId);
        if (category is null) return;

        var mp = new MemberPoints
        {
            MemberId              = record.MemberId,
            MemberPointCategoryId = category.Id,
            Points                = pts,   // can be negative for Absent
            Date                  = DateTime.UtcNow,
            Note                  = $"Auto: {record.Status} — {ev.Name}",
            AddedBy               = _currentUser.UserId,
            AttendanceRecordId    = record.Id
        };

        await _uow.MemberPoints.AddAsync(mp);
        await _uow.SaveChangesAsync();
    }

    private async Task UpdateAutoPointsAsync(AttendanceRecord record, Domain.Entities.Event ev)
    {
        // Unlink and soft-delete ALL previously linked auto-point rows so that
        // the one-to-one Include never hits "sequence contains more than one element".
        var linked = await _uow.MemberPoints.Query()
            .Where(mp => mp.AttendanceRecordId == record.Id)
            .ToListAsync();

        foreach (var mp in linked)
        {
            mp.AttendanceRecordId = null;
            mp.IsDeleted          = true;
            mp.UpdatedAt          = DateTime.UtcNow;
            _uow.MemberPoints.Update(mp);
        }

        if (linked.Count > 0)
            await _uow.SaveChangesAsync();

        await CreateAutoPointsAsync(record, ev);
    }

    /// <summary>
    /// Returns the "Attendance" MemberPointCategory scoped to the given group,
    /// creating it if it does not yet exist so points are never silently dropped.
    /// </summary>
    private async Task<Domain.Entities.MemberPointCategory?> GetOrSeedAttendanceCategoryAsync(Guid groupId)
    {
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

    // ─── Helpers ─────────────────────────────────────────────────────────────

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
        PointsAwarded = (a.AutoPoints is { IsDeleted: false }) ? a.AutoPoints.Points : null
    };
}
