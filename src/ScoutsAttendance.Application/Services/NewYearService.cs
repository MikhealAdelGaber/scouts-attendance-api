using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScoutsAttendance.Application.DTOs.Admin;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

// ─── Interface ────────────────────────────────────────────────────────────────

public interface INewYearService
{
    /// <summary>Verify admin password only — no data changes. Returns (true,"") or (false, errorMessage).</summary>
    Task<(bool Ok, string Error)> VerifyPasswordAsync(string plainPassword);

    /// <summary>
    /// Execute the year-end reset:
    ///  1. Verify password + CONFIRM text
    ///  2. Snapshot every member's stats into YearlyArchive / YearlyMemberArchive
    ///  3. Delete all MemberPoints, MemberExcuses, PendingExcuses
    /// All in one DB transaction — rollback on any failure.
    /// </summary>
    Task<(bool Ok, string Error, NewYearResultDto? Result)> StartAsync(StartNewYearDto dto);

    Task<IEnumerable<YearlyArchiveSummaryDto>> GetArchivesAsync();
    Task<YearlyArchiveDetailDto?>              GetArchiveByIdAsync(Guid id);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public class NewYearService : INewYearService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<NewYearService> _log;

    public NewYearService(IUnitOfWork uow, ICurrentUserService currentUser,
                          ILogger<NewYearService> log)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _log         = log;
    }

    // ── Verify password ───────────────────────────────────────────────────────

    public async Task<(bool Ok, string Error)> VerifyPasswordAsync(string plainPassword)
    {
        var ok = await _uow.VerifyUserPasswordAsync(_currentUser.UserId, plainPassword);
        return ok ? (true, string.Empty) : (false, "Incorrect password.");
    }

    // ── Start new year ────────────────────────────────────────────────────────

    public async Task<(bool Ok, string Error, NewYearResultDto? Result)> StartAsync(StartNewYearDto dto)
    {
        // Gate 1: confirm text
        if (dto.ConfirmText != "CONFIRM")
            return (false, "Confirmation text must be exactly 'CONFIRM'.", null);

        // Gate 2: password
        if (!await _uow.VerifyUserPasswordAsync(_currentUser.UserId, dto.Password))
            return (false, "Incorrect password.", null);

        // ── Compute archive year label ────────────────────────────────────────
        var now = DateTime.UtcNow;
        var archiveYear = now.Month >= 9
            ? $"{now.Year}-{now.Year + 1}"
            : $"{now.Year - 1}-{now.Year}";

        _log.LogInformation("[NewYear] Starting year-end reset for {Year} by {User}",
            archiveYear, _currentUser.Username);

        // ── Snapshot: load all members ────────────────────────────────────────
        var members = await _uow.Members.Query()
            .Include(m => m.Group)
            .Include(m => m.Troop)
            .Where(m => !m.IsDeleted)
            .ToListAsync();

        // Aggregate points per member
        var pointsByMember = await _uow.MemberPoints.Query()
            .GroupBy(p => p.MemberId)
            .Select(g => new { g.Key, Total = g.Sum(p => p.Points) })
            .ToListAsync();
        var pointsMap = pointsByMember.ToDictionary(x => x.Key, x => x.Total);

        // Aggregate attendance per member
        var attList = await _uow.AttendanceRecords.Query()
            .GroupBy(a => a.MemberId)
            .Select(g => new
            {
                g.Key,
                Count    = g.Count(),
                Attended = g.Count(a => a.Status == AttendanceStatus.Present
                                     || a.Status == AttendanceStatus.Late
                                     || a.Status == AttendanceStatus.TooLate)
            })
            .ToListAsync();
        var attMap = attList.ToDictionary(x => x.Key, x => x);

        // Aggregate excuses per member
        var excuseList = await _uow.MemberExcuses.Query()
            .GroupBy(e => e.MemberId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        var excuseMap = excuseList.ToDictionary(x => x.Key, x => x.Count);

        // Latest exam score per member — load into memory first to avoid LINQ-to-SQL
        // translation issues with OrderByDescending().First() inside a GroupBy select.
        var allExamScores = await _uow.MemberExamScores.Query().ToListAsync();
        var examMap = allExamScores
            .GroupBy(e => e.MemberId)
            .ToDictionary(
                g => g.Key,
                g => (decimal?)g.OrderByDescending(e => e.Year).First().Score
            );

        // Projects per group and per member
        var projects = await _uow.Projects.Query()
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        // Total projects per group
        var projectsPerGroup = projects
            .GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Projects completed per member (score > 0) — global filter already excludes deleted
        var projectScoreList = await _uow.ProjectScores.Query()
            .Where(s => s.Score > 0)
            .GroupBy(s => s.MemberId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        var projCompletedMap = projectScoreList.ToDictionary(x => x.Key, x => x.Count);

        var totalGroups = members.Select(m => m.GroupId).Distinct().Count();

        // ── Transaction ───────────────────────────────────────────────────────
        int pointsDeleted = 0, excusesDeleted = 0, pendingDeleted = 0;
        int attDeleted = 0, eventsDeleted = 0, tripsDeleted = 0;
        int projectsDeleted = 0, troopsDeleted = 0;

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 1. Create YearlyArchive header
            var archive = new YearlyArchive
            {
                ArchiveYear  = archiveYear,
                ArchivedAt   = now,
                ArchivedBy   = _currentUser.Username,
                TotalMembers = members.Count,
                TotalGroups  = totalGroups
            };
            await _uow.YearlyArchives.AddAsync(archive);

            // 2. Create one YearlyMemberArchive per member (full snapshot)
            var memberArchives = members.Select(m =>
            {
                attMap.TryGetValue(m.Id, out var att);
                var groupProjects = projectsPerGroup.GetValueOrDefault(m.GroupId);
                return new YearlyMemberArchive
                {
                    YearlyArchiveId      = archive.Id,
                    MemberId             = m.Id,
                    MemberName           = m.FullName,
                    GroupId              = m.GroupId,
                    GroupName            = m.Group?.Name ?? string.Empty,
                    TroopId              = m.TroopId,
                    TroopName            = m.Troop?.Name,
                    TotalPointsAtYearEnd = pointsMap.GetValueOrDefault(m.Id),
                    TotalAttendanceCount = att?.Count    ?? 0,
                    TotalEventsAttended  = att?.Attended ?? 0,
                    TotalExcusesCount    = excuseMap.GetValueOrDefault(m.Id),
                    LatestExamScore      = examMap.GetValueOrDefault(m.Id),
                    TotalProjects        = groupProjects,
                    ProjectsCompleted    = projCompletedMap.GetValueOrDefault(m.Id),
                    AcademicGrade        = m.AcademicYear
                };
            }).ToList();

            await _uow.YearlyMemberArchives.AddRangeAsync(memberArchives);

            // Persist archive rows first (in the same transaction)
            await _uow.SaveChangesAsync();

            // 3. Delete MemberPoints (FK to AttendanceRecords — must go first)
            pointsDeleted   = await _uow.DeleteAllMemberPointsGlobalAsync();

            // 4. Delete AttendanceRecords (FK to Events)
            attDeleted      = await _uow.DeleteAllAttendanceRecordsGlobalAsync();

            // 5. Soft-delete Events
            eventsDeleted   = await _uow.SoftDeleteAllEventsGlobalAsync();

            // 6. Delete all Trip data (attendance → payments → bookings → trips)
            tripsDeleted    = await _uow.DeleteAllTripDataGlobalAsync();

            // 7. Delete project scores + soft-delete Projects
            projectsDeleted = await _uow.DeleteAllProjectDataGlobalAsync();

            // 8. Unassign members/users from troops + delete TroopPointCategories + soft-delete Troops
            troopsDeleted   = await _uow.DeleteAllTroopsGlobalAsync();

            // 9. Delete MemberExcuses
            excusesDeleted  = await _uow.DeleteAllMemberExcusesGlobalAsync();

            // 10. Delete PendingExcuses
            pendingDeleted  = await _uow.DeleteAllPendingExcusesGlobalAsync();
        });

        _log.LogInformation(
            "[NewYear] Completed. Members={Members}, Groups={Groups}, " +
            "Points={Points}, Att={Att}, Events={Events}, Trips={Trips}, " +
            "Projects={Projects}, Troops={Troops}, Excuses={Excuses}",
            members.Count, totalGroups, pointsDeleted, attDeleted, eventsDeleted,
            tripsDeleted, projectsDeleted, troopsDeleted, excusesDeleted);

        // Retrieve the archive Id we just inserted
        var saved = await _uow.YearlyArchives.Query()
            .OrderByDescending(a => a.ArchivedAt)
            .FirstOrDefaultAsync(a => a.ArchivedBy == _currentUser.Username
                                   && a.ArchiveYear == archiveYear);

        return (true, string.Empty, new NewYearResultDto
        {
            ArchiveId             = saved?.Id ?? Guid.Empty,
            ArchiveYear           = archiveYear,
            TotalMembers          = members.Count,
            TotalGroups           = totalGroups,
            ArchivedAt            = now,
            PointsDeleted         = pointsDeleted,
            ExcusesDeleted        = excusesDeleted,
            PendingExcusesDeleted = pendingDeleted,
            AttendanceDeleted     = attDeleted,
            EventsDeleted         = eventsDeleted,
            TripsDeleted          = tripsDeleted,
            ProjectsDeleted       = projectsDeleted,
            TroopsDeleted         = troopsDeleted
        });
    }

    // ── Archive list ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<YearlyArchiveSummaryDto>> GetArchivesAsync()
    {
        var list = await _uow.YearlyArchives.Query()
            .OrderByDescending(a => a.ArchivedAt)
            .ToListAsync();

        return list.Select(MapSummary);
    }

    // ── Archive detail ────────────────────────────────────────────────────────

    public async Task<YearlyArchiveDetailDto?> GetArchiveByIdAsync(Guid id)
    {
        var archive = await _uow.YearlyArchives.Query()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (archive is null) return null;

        var members = await _uow.YearlyMemberArchives.Query()
            .Where(m => m.YearlyArchiveId == id)
            .OrderBy(m => m.GroupName).ThenBy(m => m.MemberName)
            .ToListAsync();

        return new YearlyArchiveDetailDto
        {
            Id           = archive.Id,
            ArchiveYear  = archive.ArchiveYear,
            ArchivedAt   = archive.ArchivedAt,
            ArchivedBy   = archive.ArchivedBy,
            TotalMembers = archive.TotalMembers,
            TotalGroups  = archive.TotalGroups,
            Members      = members.Select(m => new YearlyMemberArchiveDto
            {
                Id                   = m.Id,
                MemberId             = m.MemberId,
                MemberName           = m.MemberName,
                GroupId              = m.GroupId,
                GroupName            = m.GroupName,
                TroopName            = m.TroopName,
                TotalPointsAtYearEnd = m.TotalPointsAtYearEnd,
                TotalAttendanceCount = m.TotalAttendanceCount,
                TotalEventsAttended  = m.TotalEventsAttended,
                TotalExcusesCount    = m.TotalExcusesCount,
                LatestExamScore      = m.LatestExamScore,
                TotalProjects        = m.TotalProjects,
                ProjectsCompleted    = m.ProjectsCompleted,
                AcademicGrade        = m.AcademicGrade
            }).ToList()
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static YearlyArchiveSummaryDto MapSummary(YearlyArchive a) => new()
    {
        Id           = a.Id,
        ArchiveYear  = a.ArchiveYear,
        ArchivedAt   = a.ArchivedAt,
        ArchivedBy   = a.ArchivedBy,
        TotalMembers = a.TotalMembers,
        TotalGroups  = a.TotalGroups
    };
}
