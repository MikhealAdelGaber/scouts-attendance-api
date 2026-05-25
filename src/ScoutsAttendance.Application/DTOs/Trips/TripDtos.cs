using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.Trips;

// ─── Trip ─────────────────────────────────────────────────────────────────────

public class TripDto
{
    public Guid      Id             { get; set; }
    public string    Name           { get; set; } = string.Empty;
    public string    Description    { get; set; } = string.Empty;
    public DateTime  TripDate       { get; set; }
    public string    Location       { get; set; } = string.Empty;
    public decimal   Price          { get; set; }
    public decimal   SiblingPrice   { get; set; }
    public int?      MaxCapacity    { get; set; }
    public Guid      GroupId        { get; set; }
    public bool      HasPoints      { get; set; }
    public int?      PointValue     { get; set; }
    public TripStatus Status        { get; set; }
    public string    StatusName     { get; set; } = string.Empty;
    public string    CreatedBy      { get; set; } = string.Empty;
    public DateTime  CreatedAt      { get; set; }

    // Computed
    public int     ConfirmedCount   { get; set; }
    public int     WaitingCount     { get; set; }
    public decimal TotalCollected   { get; set; }
}

public class CreateTripDto
{
    [Required, MaxLength(200)] public string Name        { get; set; } = string.Empty;
    public string   Description   { get; set; } = string.Empty;
    [Required] public DateTime TripDate     { get; set; }
    public string   Location      { get; set; } = string.Empty;
    [Range(0, double.MaxValue)] public decimal Price        { get; set; }
    [Range(0, double.MaxValue)] public decimal SiblingPrice { get; set; }
    public int?     MaxCapacity   { get; set; }
    public bool     HasPoints     { get; set; } = false;
    public int?     PointValue    { get; set; }
}

public class UpdateTripDto
{
    [Required, MaxLength(200)] public string Name        { get; set; } = string.Empty;
    public string   Description   { get; set; } = string.Empty;
    [Required] public DateTime TripDate     { get; set; }
    public string   Location      { get; set; } = string.Empty;
    [Range(0, double.MaxValue)] public decimal Price        { get; set; }
    [Range(0, double.MaxValue)] public decimal SiblingPrice { get; set; }
    public int?     MaxCapacity   { get; set; }
    public bool     HasPoints     { get; set; }
    public int?     PointValue    { get; set; }
    public TripStatus Status      { get; set; }
}

// ─── Booking ──────────────────────────────────────────────────────────────────

public class TripBookingDto
{
    public Guid          Id            { get; set; }
    public Guid          TripId        { get; set; }
    public Guid          MemberId      { get; set; }
    public string        MemberName    { get; set; } = string.Empty;
    public int           MemberCustomId { get; set; }
    public BookingStatus BookingStatus { get; set; }
    public string        StatusName    { get; set; } = string.Empty;
    public bool          IsSibling     { get; set; }
    public decimal       AmountDue     { get; set; }
    public DateTime?     PaidAt        { get; set; }
    public string        Notes         { get; set; } = string.Empty;
    public DateTime      CreatedAt     { get; set; }
}

public class CreateBookingDto
{
    [Required] public Guid MemberId  { get; set; }
    public bool   IsSibling  { get; set; } = false;
    public string Notes      { get; set; } = string.Empty;
    /// <summary>True = add to waiting list even if trip is open (for pre-seeding WL).</summary>
    public bool   ForceWaiting { get; set; } = false;
}

// ─── Attendance ───────────────────────────────────────────────────────────────

public class TripAttendanceDto
{
    public Guid   TripId    { get; set; }
    public Guid   MemberId  { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public int    MemberCustomId { get; set; }
    public int    Status    { get; set; }    // 0=Present, 1=Absent, 2=Late, 3=Excused
    public string Notes     { get; set; } = string.Empty;
}

public class SaveTripAttendanceDto
{
    public List<TripAttendanceEntryDto> Records { get; set; } = new();
}

public class TripAttendanceEntryDto
{
    public Guid   MemberId { get; set; }
    public int    Status   { get; set; }
    public string Notes    { get; set; } = string.Empty;
}
