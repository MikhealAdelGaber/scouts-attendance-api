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
        // ── Determine group scope ─────────────────────────────────────────────
        // SystemAdmin → null (no filter, sees everything)
        // Everyone else → their own GroupId
        Guid? scopedGroupId = _currentUser.IsSystemAdmin ? null : _currentUser.GroupId;

        // ── 1. Troops (scoped) ────────────────────────────────────────────────
        IQueryable<Domain.Entities.Troop> troopsQuery = _uow.Troops.Query();
        if (!_currentUser.IsSystemAdmin)
        {
            if (_currentUser.HasTroopScope)
                // AttendanceOnly / troop-scoped user: only their single troop
                troopsQuery = troopsQuery.Where(t => t.Id == _currentUser.TroopId!.Value);
            else if (scopedGroupId.HasValue)
                // GroupLeader / etc.: all troops in their group
                troopsQuery = troopsQuery.Where(t => t.GroupId == scopedGroupId.Value);
        }
        var troops    = await troopsQuery.Include(t => t.Group).ToListAsync();
        var troopIds  = troops.Select(t => t.Id).ToList();

        // ── 2. Total Members — count by GroupId to include troop-less members ─
        // Using GroupId (not troopIds) so members who have no troop yet are
        // still counted.  SystemAdmin sees all members.
        var membersQuery = _uow.Members.Query();
        if (scopedGroupId.HasValue)
            membersQuery = membersQuery.Where(m => m.GroupId == scopedGroupId.Value);
        var totalMemberCount = await membersQuery.CountAsync();

        // ── 3. Members in visible troops (needed for per-troop attendance) ────
        var troopMembers = await _uow.Members.Query()
            .Where(m => m.TroopId != null && troopIds.Contains(m.TroopId.Value))
            .Select(m => new { m.Id, m.TroopId })
            .ToListAsync();

        var memberIds = troopMembers.Select(m => m.Id).ToList();

        // ── 4. Attendance records for troop-members only ──────────────────────
        var records = await _uow.AttendanceRecords.Query()
            .Where(r => memberIds.Contains(r.MemberId))
            .Select(r => new { r.MemberId, r.Status })
            .ToListAsync();

        // ── 5. Events — scoped to group ───────────────────────────────────────
        var eventsQuery = _uow.Events.Query();
        if (scopedGroupId.HasValue)
            eventsQuery = eventsQuery.Where(e => e.GroupId == scopedGroupId.Value);
        var totalEvents = await eventsQuery.CountAsync();

        // ── 6. In-memory grouping for per-troop breakdown ─────────────────────
        var membersByTroop = troopMembers
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
            TotalMembers          = totalMemberCount,
            TotalTroops           = troops.Count,
            TotalEvents           = totalEvents,
            OverallAttendanceRate = totalRecords > 0
                ? Math.Round((double)totalPresent / totalRecords * 100, 1)
                : 0,
            TroopBreakdown        = troopBreakdown
        };
    }
}
