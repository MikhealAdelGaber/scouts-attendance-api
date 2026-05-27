using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class BookingPayment : BaseEntity
{
    public Guid      BookingId         { get; set; }
    public int       InstallmentNumber { get; set; }
    public decimal   AmountDue         { get; set; }
    public decimal   AmountPaid        { get; set; }
    public DateTime? PaidAt            { get; set; }
    public string    Notes             { get; set; } = string.Empty;

    // Navigation
    public TripBooking? Booking { get; set; }
}
