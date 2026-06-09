namespace ScoutsAttendance.Application.DTOs.Reports;

public class AttendanceRateDto
{
    public Guid   MemberId    { get; set; }
    public string MemberName  { get; set; } = string.Empty;
    public string TroopName   { get; set; } = string.Empty;
    public string? AcademicGrade  { get; set; }
    public bool    HasNeckerchief { get; set; }

    // ── Attendance ────────────────────────────────────────────────────────────
    public int    TotalEvents { get; set; }
    public int    Present     { get; set; }
    public int    Late        { get; set; }
    public int    TooLate     { get; set; }
    public int    Excused     { get; set; }
    public int    Absent      { get; set; }

    /// <summary>
    /// Attendance rate as a percentage 0-100.
    /// Rate = (Present + Late + TooLate + Excused) / TotalEvents × 100.
    /// </summary>
    public double Rate => TotalEvents == 0 ? 0.0
        : Math.Round((Present + Late + TooLate + Excused) * 100.0 / TotalEvents, 1);

    // ── Exam score ────────────────────────────────────────────────────────────
    /// <summary>Latest exam score (0–100) or null if none recorded.</summary>
    public decimal? LatestExamScore { get; set; }

    // ── Projects ──────────────────────────────────────────────────────────────
    public int      TotalProjects     { get; set; }
    public int      ProjectsCompleted { get; set; }

    /// <summary>Project completion % (0-100) or null if no projects exist.</summary>
    public double? ProjectRate => TotalProjects == 0 ? null
        : Math.Round(ProjectsCompleted * 100.0 / TotalProjects, 1);

    // ── Points ────────────────────────────────────────────────────────────────
    public decimal TotalPoints { get; set; }
}
