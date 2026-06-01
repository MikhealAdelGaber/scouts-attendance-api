using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// Per-member stats snapshot captured during a year-end reset.
/// All values reflect the member's accumulated data in their group
/// immediately BEFORE points and excuses were wiped.
/// </summary>
public class YearlyMemberArchive : BaseEntity
{
    public Guid     YearlyArchiveId       { get; set; }

    public Guid     MemberId              { get; set; }
    public string   MemberName            { get; set; } = string.Empty;

    public Guid     GroupId               { get; set; }
    public string   GroupName             { get; set; } = string.Empty;

    public Guid?    TroopId               { get; set; }
    public string?  TroopName             { get; set; }

    public decimal  TotalPointsAtYearEnd  { get; set; }
    public int      TotalAttendanceCount  { get; set; }
    public int      TotalEventsAttended   { get; set; }
    public int      TotalExcusesCount     { get; set; }

    /// <summary>Latest exam score (0–100) or null if none recorded.</summary>
    public decimal? LatestExamScore       { get; set; }

    /// <summary>Number of projects in this member's group for the year.</summary>
    public int      TotalProjects         { get; set; }

    /// <summary>Number of projects where the member received a score &gt; 0.</summary>
    public int      ProjectsCompleted     { get; set; }

    public string?  AcademicGrade         { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public YearlyArchive YearlyArchive { get; set; } = null!;
}
