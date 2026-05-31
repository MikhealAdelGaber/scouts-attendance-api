using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class Event : BaseEntity
{
    public string   Name        { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public DateTime EventDate   { get; set; }
    public Guid     GroupId     { get; set; }
    public Guid?    TroopId     { get; set; }
    public bool     IsActive    { get; set; } = true;
    public Guid     CreatedBy   { get; set; }

    /// <summary>Points awarded when a member is marked Present for this event.</summary>
    public decimal  PresentPoints   { get; set; } = 100m;
    /// <summary>Points awarded when a member is marked Late for this event.</summary>
    public decimal  LatePoints      { get; set; } = 50m;
    /// <summary>Points awarded when a member is marked Excused for this event (same as Present).</summary>
    public decimal  ExcusedPoints   { get; set; } = 100m;
    /// <summary>Points applied when a member is marked Absent (can be negative).</summary>
    public decimal  AbsentPoints    { get; set; } = -10m;
    /// <summary>Points awarded when a member is marked Too Late (default 0).</summary>
    public decimal  TooLatePoints   { get; set; } = 0m;

    public Group  Group { get; set; } = null!;
    public Troop? Troop { get; set; }
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
