using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class TripAttendanceRecord : BaseEntity
{
    public Guid   TripId   { get; set; }
    public Guid   MemberId { get; set; }
    /// <summary>Present / Absent / Late / Excused — stored as int (same as AttendanceStatus enum).</summary>
    public int    Status   { get; set; } = 1; // 1 = Absent by default
    public string Notes    { get; set; } = string.Empty;

    // Navigation
    public Trip?   Trip   { get; set; }
    public Member? Member { get; set; }
}
