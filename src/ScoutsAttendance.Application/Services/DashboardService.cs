using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Dashboard;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync();
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        // ── 1. Determine which troops this user may see ───────────────────────
        IQueryable<Domain.Entities.Troop> troopsQuery = _uow.Troops.Query();

        if (!_currentUser.IsSystemAdmin)
        {
            // Scope to the user's troop first (most restrictive)
            if (_currentUser.HasTroopScope)
                troopsQuery = troopsQuery.Where(t => t.Id == _currentUser.TroopId!.Value);
            // Otherwise scope to their group (AttendanceOnly, GroupLeader, etc.)
            else if (_currentUser.GroupId.HasValue)
                troopsQuery = troopsQuery.Where(t => t.GroupId == _currentUser.GroupId.Value);
        }
        // SystemAdmin → no filter

        // Apply Include after all Where filters to keep the type as IQueryable<Troop>
        var troops = await troopsQuery.Include(t => t.Group).ToListAsync();
        var troopIds = troops.Select(t => t.Id).ToList();

        // ── 2. Members in visible troops ─────────────────────────────────────
        var members = await _uow.Members.Query()
            .Where(m => m.TroopId != null && troopIds.Contains(m.TroopId.Value))
            .Select(m => new { m.Id, m.TroopId })
            .ToListAsync();

        var memberIds = members.Select(m => m.Id).ToList();

        // ── 3. Attendance records for those members ───────────────────────────
        var records = await _uow.AttendanceRecords.Query()
            .Where(r => memberIds.Contains(r.MemberId))
            .Select(r => new { r.MemberId, r.Status })
            .ToListAsync();

        // ── 4. Total events (regardless of troop, consistent with current dashboard) ─
        var totalEvents = await _uow.Events.Query().CountAsync();

        // ── 5. In-memory grouping ─────────────────────────────────────────────
        var membersByTroop = members
            .GroupBy(m => m.TroopId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToHashSet());

        var recordsByMember = records
            .GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var troopBreakdown = troops.Select(t =>
        {
            var troopMemberIds = membersByTroop.GetValueOrDefault(t.Id, []);

            var troopRecords = troopMemberIds
                .SelectMany(mId => recordsByMember.GetValueOrDefault(mId, []))
                .ToList();

            // Present + Late both count as "attended"
            int presentCount = troopRecords.Count(r =>
                r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
            int absentCount  = troopRecords.Count(r => r.Status == AttendanceStatus.Absent);
            int total        = troopRecords.Count;

            return new TroopAttendanceStatsDto
            {
                TroopId                = t.Id,
                TroopName              = t.Name,
                GroupName              = t.Group?.Name ?? string.Empty,
                MemberCount            = troopMemberIds.Count,
                TotalAttendanceRecords = total,
                PresentCount           = presentCount,
                AbsentCount            = absentCount,
                AttendanceRate         = total > 0 ? Math.Round((double)presentCount / total * 100, 1) : 0,
                AbsenceRate            = total > 0 ? Math.Round((double)absentCount  / total * 100, 1) : 0
            };
        })
        .OrderByDescending(t => t.AttendanceRate)
        .ToList();

        int totalPresent = troopBreakdown.Sum(t => t.PresentCount);
        int totalRecords = troopBreakdown.Sum(t => t.TotalAttendanceRecords);

        return new DashboardStatsDto
        {
            TotalMembers          = members.Count,
            TotalTroops           = troops.Count,
            TotalEvents           = totalEvents,
            OverallAttendanceRate = totalRecords > 0
                ? Math.Round((double)totalPresent / totalRecords * 100, 1)
                : 0,
            TroopBreakdown        = troopBreakdown
        };
    }
}
