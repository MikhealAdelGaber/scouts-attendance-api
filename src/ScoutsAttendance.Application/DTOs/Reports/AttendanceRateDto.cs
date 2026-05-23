namespace ScoutsAttendance.Application.DTOs.Reports;

public class AttendanceRateDto
{
    public Guid   MemberId    { get; set; }
    public string MemberName  { get; set; } = string.Empty;
    public string TroopName   { get; set; } = string.Empty;
    public int    TotalEvents { get; set; }
    public int    Present     { get; set; }
    public int    Late        { get; set; }
    public int    Excused     { get; set; }
    public int    Absent      { get; set; }

    /// <summary>
    /// Attendance rate as a percentage 0-100.
    /// Rate = (Present + Late) / TotalEvents * 100.
    /// Returns 100 when TotalEvents == 0 (no events in range for that member).
    /// </summary>
    public double Rate => TotalEvents == 0 ? 100.0
        : Math.Round((Present + Late) * 100.0 / TotalEvents, 1);
}
