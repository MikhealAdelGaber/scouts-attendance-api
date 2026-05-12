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

    /// <summary>Points awarded when a member is marked Present or Excused for this event.</summary>
    public decimal  PointValue      { get; set; } = 100m;
    /// <summary>Points awarded when a member is marked Late for this event.</summary>
    public decimal  LatePointValue  { get; set; } = 50m;

    public Group  Group { get; set; } = null!;
    public Troop? Troop { get; set; }
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
