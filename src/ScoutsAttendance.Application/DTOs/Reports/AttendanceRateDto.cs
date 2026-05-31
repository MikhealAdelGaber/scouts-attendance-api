namespace ScoutsAttendance.Application.DTOs.Reports;

public class AttendanceRateDto
{
    public Guid   MemberId    { get; set; }
    public string MemberName  { get; set; } = string.Empty;
    public string TroopName   { get; set; } = string.Empty;
    public int    TotalEvents { get; set; }
    public int    Present     { get; set; }
    public int    Late        { get; set; }
    public int    TooLate     { get; set; }
    public int    Excused     { get; set; }
    public int    Absent      { get; set; }

    /// <summary>
    /// Attendance rate as a percentage 0-100.
    /// Rate = (Present + Late + TooLate + Excused) / TotalEvents × 100.
    /// TooLate members physically attended — counted toward rate.
    /// Excused members count as "attended" because they have a valid excuse.
    /// Members with no attendance record default to Absent (not Present).
    /// </summary>
    public double Rate => TotalEvents == 0 ? 0.0
        : Math.Round((Present + Late + TooLate + Excused) * 100.0 / TotalEvents, 1);
}
