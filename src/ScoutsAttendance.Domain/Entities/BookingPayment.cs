using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// Records a single payment made toward a booking.
/// Multiple payments can exist per booking when AllowInstallments is enabled on the trip.
/// </summary>
public class BookingPayment : BaseEntity
{
    public Guid     BookingId  { get; set; }
    public decimal  AmountPaid { get; set; }
    public DateTime PaidAt     { get; set; }
    public string   Notes      { get; set; } = string.Empty;

    // Navigation
    public TripBooking? Booking { get; set; }
}
