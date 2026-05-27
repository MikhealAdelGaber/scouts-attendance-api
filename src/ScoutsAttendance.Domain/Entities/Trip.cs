using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

public class Trip : BaseEntity
{
    public string   Name          { get; set; } = string.Empty;
    public string   Description   { get; set; } = string.Empty;
    public DateTime TripDate      { get; set; }
    public string   Location      { get; set; } = string.Empty;
    public decimal  Price         { get; set; }
    public decimal  SiblingPrice  { get; set; }
    public int?     MaxCapacity   { get; set; }          // null = unlimited
    public Guid     GroupId       { get; set; }
    public bool     HasPoints     { get; set; } = false;
    public int?     PointValue    { get; set; }
    public TripStatus Status           { get; set; } = TripStatus.Open;
    public bool     AllowInstallments  { get; set; } = false;
    public int?     NumberOfInstallments { get; set; }
    public string   CreatedBy          { get; set; } = string.Empty;

    // Navigation
    public Group?  Group    { get; set; }
    public ICollection<TripBooking> Bookings { get; set; } = new List<TripBooking>();
    public ICollection<TripAttendanceRecord> AttendanceRecords { get; set; } = new List<TripAttendanceRecord>();
}
