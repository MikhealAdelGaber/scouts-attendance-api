using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

public class TripBooking : BaseEntity
{
    public Guid          TripId        { get; set; }
    public Guid          MemberId      { get; set; }
    public BookingStatus BookingStatus { get; set; } = BookingStatus.Confirmed;
    public bool          IsSibling     { get; set; } = false;
    public decimal       AmountDue     { get; set; }
    public DateTime?     PaidAt        { get; set; }
    public string        Notes         { get; set; } = string.Empty;

    // Navigation
    public Trip?   Trip   { get; set; }
    public Member? Member { get; set; }
}
