using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Trips;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface ITripService
{
    // ── Trips ─────────────────────────────────────────────────────────────────
    Task<IEnumerable<TripDto>>    GetAllAsync(Guid? groupId);
    Task<TripDto?>                GetByIdAsync(Guid id);
    Task<TripDto>                 CreateAsync(CreateTripDto dto, Guid groupId, string createdBy);
    Task<TripDto?>                UpdateAsync(Guid id, UpdateTripDto dto, Guid? callerGroupId, bool isAdmin);
    Task<bool>                    DeleteAsync(Guid id, Guid? callerGroupId, bool isAdmin);

    // ── Bookings ──────────────────────────────────────────────────────────────
    Task<IEnumerable<TripBookingDto>> GetBookingsAsync(Guid tripId);
    Task<TripBookingDto>              BookMemberAsync(Guid tripId, CreateBookingDto dto);
    Task<TripBookingDto?>             CancelBookingAsync(Guid bookingId);
    Task<TripBookingDto?>             MarkPaidAsync(Guid bookingId);

    // ── Attendance ────────────────────────────────────────────────────────────
    Task<IEnumerable<TripAttendanceDto>> GetAttendanceAsync(Guid tripId);
    Task                                 SaveAttendanceAsync(Guid tripId, SaveTripAttendanceDto dto);

    // ── Flexible payments ─────────────────────────────────────────────────────
    Task<IEnumerable<BookingPaymentDto>> GetPaymentsAsync(Guid bookingId);
    Task<BookingPaymentDto>              AddPaymentAsync(Guid tripId, Guid bookingId, AddPaymentDto dto);
    Task<bool>                           DeletePaymentAsync(Guid tripId, Guid paymentId);
}

public class TripService : ITripService
{
    private readonly IUnitOfWork _uow;

    public TripService(IUnitOfWork uow) => _uow = uow;

    // ─────────────────────────────────────────────────────────────────────────
    // Trips
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<TripDto>> GetAllAsync(Guid? groupId)
    {
        var q = _uow.Trips.Query()
            .Include(t => t.Bookings).ThenInclude(b => b.Payments)
            .Where(t => !t.IsDeleted);

        if (groupId.HasValue)
            q = q.Where(t => t.GroupId == groupId.Value);

        var trips = await q.OrderByDescending(t => t.TripDate).ToListAsync();
        return trips.Select(MapTrip);
    }

    public async Task<TripDto?> GetByIdAsync(Guid id)
    {
        var trip = await _uow.Trips.Query()
            .Include(t => t.Bookings).ThenInclude(b => b.Member)
            .Include(t => t.Bookings).ThenInclude(b => b.Payments)
            .Include(t => t.Group)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        return trip is null ? null : MapTrip(trip);
    }

    public async Task<TripDto> CreateAsync(CreateTripDto dto, Guid groupId, string createdBy)
    {
        var trip = new Trip
        {
            Name              = dto.Name.Trim(),
            Description       = (dto.Description ?? string.Empty).Trim(),
            TripDate          = DateTime.SpecifyKind(dto.TripDate, DateTimeKind.Utc),
            Location          = dto.Location.Trim(),
            Price             = dto.Price,
            SiblingPrice      = dto.SiblingPrice,
            MaxCapacity       = dto.MaxCapacity,
            GroupId           = groupId,
            HasPoints         = dto.HasPoints,
            PointValue        = dto.HasPoints ? dto.PointValue : null,
            Status            = TripStatus.Open,
            AllowInstallments = dto.AllowInstallments,
            CreatedBy         = createdBy
        };

        await _uow.Trips.AddAsync(trip);
        await _uow.SaveChangesAsync();
        return (await GetByIdAsync(trip.Id))!;
    }

    public async Task<TripDto?> UpdateAsync(Guid id, UpdateTripDto dto, Guid? callerGroupId, bool isAdmin)
    {
        var trip = await _uow.Trips.GetByIdAsync(id);
        if (trip is null || trip.IsDeleted) return null;
        if (!isAdmin && trip.GroupId != callerGroupId) return null;

        trip.Name             = dto.Name.Trim();
        trip.Description      = (dto.Description ?? string.Empty).Trim();
        trip.TripDate         = DateTime.SpecifyKind(dto.TripDate, DateTimeKind.Utc);
        trip.Location         = dto.Location.Trim();
        trip.Price            = dto.Price;
        trip.SiblingPrice     = dto.SiblingPrice;
        trip.MaxCapacity      = dto.MaxCapacity;
        trip.HasPoints        = dto.HasPoints;
        trip.PointValue       = dto.HasPoints ? dto.PointValue : null;
        trip.Status           = dto.Status;
        trip.AllowInstallments = dto.AllowInstallments;
        // SystemAdmin can re-assign to a different group via the DTO
        if (dto.GroupId.HasValue && dto.GroupId.Value != Guid.Empty)
            trip.GroupId  = dto.GroupId.Value;
        trip.UpdatedAt    = DateTime.UtcNow;

        _uow.Trips.Update(trip);
        await _uow.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? callerGroupId, bool isAdmin)
    {
        var trip = await _uow.Trips.GetByIdAsync(id);
        if (trip is null) return false;
        if (!isAdmin && trip.GroupId != callerGroupId) return false;

        _uow.Trips.SoftDelete(trip);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bookings
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<TripBookingDto>> GetBookingsAsync(Guid tripId)
    {
        var trip = await _uow.Trips.GetByIdAsync(tripId);

        var bookings = await _uow.TripBookings.Query()
            .Include(b => b.Member).ThenInclude(m => m!.Troop)
            .Include(b => b.Payments)
            .Where(b => b.TripId == tripId && !b.IsDeleted)
            .OrderBy(b => b.BookingStatus)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync();

        bool allowInstallments = trip?.AllowInstallments ?? false;
        return bookings.Select(b => MapBooking(b, allowInstallments));
    }

    public async Task<TripBookingDto> BookMemberAsync(Guid tripId, CreateBookingDto dto)
    {
        var trip = await _uow.Trips.Query()
            .Include(t => t.Bookings.Where(b => !b.IsDeleted))
                .ThenInclude(b => b.Member).ThenInclude(m => m!.Troop)
            .FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted)
            ?? throw new InvalidOperationException("Trip not found.");

        // Check already booked
        var existing = trip.Bookings.FirstOrDefault(b => b.MemberId == dto.MemberId);
        if (existing is not null)
            throw new InvalidOperationException("Member is already booked on this trip.");

        int confirmedCount = trip.Bookings.Count(b => b.BookingStatus == BookingStatus.Confirmed);
        bool isFull = trip.MaxCapacity.HasValue && confirmedCount >= trip.MaxCapacity.Value;

        BookingStatus status = isFull || dto.ForceWaiting
            ? BookingStatus.Waiting
            : BookingStatus.Confirmed;

        var booking = new TripBooking
        {
            TripId        = tripId,
            MemberId      = dto.MemberId,
            BookingStatus = status,
            IsSibling     = dto.IsSibling,
            AmountDue     = dto.IsSibling ? trip.SiblingPrice : trip.Price,
            Notes         = dto.Notes.Trim()
        };

        await _uow.TripBookings.AddAsync(booking);

        // Update trip status if now full
        if (status == BookingStatus.Confirmed && trip.MaxCapacity.HasValue &&
            confirmedCount + 1 >= trip.MaxCapacity.Value)
        {
            trip.Status    = TripStatus.Full;
            trip.UpdatedAt = DateTime.UtcNow;
            _uow.Trips.Update(trip);
        }

        await _uow.SaveChangesAsync();

        var saved = await _uow.TripBookings.Query()
            .Include(b => b.Member).ThenInclude(m => m!.Troop)
            .Include(b => b.Payments)
            .FirstAsync(b => b.Id == booking.Id);

        return MapBooking(saved, trip.AllowInstallments);
    }

    public async Task<TripBookingDto?> CancelBookingAsync(Guid bookingId)
    {
        var booking = await _uow.TripBookings.Query()
            .Include(b => b.Member).ThenInclude(m => m!.Troop)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted);

        if (booking is null) return null;

        var trip = await _uow.Trips.GetByIdAsync(booking.TripId);
        bool wasConfirmed = booking.BookingStatus == BookingStatus.Confirmed;
        _uow.TripBookings.SoftDelete(booking);
        await _uow.SaveChangesAsync();

        // Promote first waiting-list member if a confirmed slot freed up
        if (wasConfirmed)
        {
            var nextWaiting = await _uow.TripBookings.Query()
                .Where(b => b.TripId == booking.TripId &&
                            b.BookingStatus == BookingStatus.Waiting &&
                            !b.IsDeleted)
                .OrderBy(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextWaiting is not null)
            {
                nextWaiting.BookingStatus = BookingStatus.Confirmed;
                nextWaiting.UpdatedAt     = DateTime.UtcNow;
                _uow.TripBookings.Update(nextWaiting);

                // Trip back to Open if capacity allows
                if (trip is { Status: TripStatus.Full })
                {
                    trip.Status    = TripStatus.Open;
                    trip.UpdatedAt = DateTime.UtcNow;
                    _uow.Trips.Update(trip);
                }

                await _uow.SaveChangesAsync();
            }
        }

        return MapBooking(booking, trip?.AllowInstallments ?? false);
    }

    public async Task<TripBookingDto?> MarkPaidAsync(Guid bookingId)
    {
        var booking = await _uow.TripBookings.Query()
            .Include(b => b.Member).ThenInclude(m => m!.Troop)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId && !b.IsDeleted);

        if (booking is null) return null;

        var trip = await _uow.Trips.GetByIdAsync(booking.TripId);
        booking.PaidAt    = booking.PaidAt.HasValue ? null : DateTime.UtcNow; // toggle
        booking.UpdatedAt = DateTime.UtcNow;
        _uow.TripBookings.Update(booking);
        await _uow.SaveChangesAsync();
        return MapBooking(booking, trip?.AllowInstallments ?? false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Flexible Payments
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<BookingPaymentDto>> GetPaymentsAsync(Guid bookingId)
    {
        var payments = await _uow.BookingPayments.Query()
            .Where(p => p.BookingId == bookingId && !p.IsDeleted)
            .OrderBy(p => p.PaidAt)
            .ToListAsync();
        return payments.Select(MapPayment);
    }

    public async Task<BookingPaymentDto> AddPaymentAsync(Guid tripId, Guid bookingId, AddPaymentDto dto)
    {
        var booking = await _uow.TripBookings.Query()
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.TripId == tripId && !b.IsDeleted)
            ?? throw new InvalidOperationException("Booking not found.");

        // Validate: amount must be > 0
        if (dto.AmountPaid <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero.");

        // Validate: total paid would not exceed amount due
        decimal alreadyPaid = booking.Payments.Where(p => !p.IsDeleted).Sum(p => p.AmountPaid);
        if (alreadyPaid + dto.AmountPaid > booking.AmountDue)
            throw new InvalidOperationException(
                $"Total payments ({alreadyPaid + dto.AmountPaid:F2}) would exceed amount due ({booking.AmountDue:F2}).");

        var payment = new BookingPayment
        {
            BookingId  = bookingId,
            AmountPaid = dto.AmountPaid,
            PaidAt     = DateTime.UtcNow,
            Notes      = (dto.Notes ?? string.Empty).Trim()
        };

        await _uow.BookingPayments.AddAsync(payment);
        await _uow.SaveChangesAsync();

        return MapPayment(payment);
    }

    public async Task<bool> DeletePaymentAsync(Guid tripId, Guid paymentId)
    {
        var payment = await _uow.BookingPayments.Query()
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == paymentId && !p.IsDeleted);

        if (payment is null) return false;
        // Ensure the payment belongs to the requested trip
        if (payment.Booking?.TripId != tripId) return false;

        _uow.BookingPayments.SoftDelete(payment);
        await _uow.SaveChangesAsync();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Attendance
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<TripAttendanceDto>> GetAttendanceAsync(Guid tripId)
    {
        var bookings = await _uow.TripBookings.Query()
            .Include(b => b.Member).ThenInclude(m => m!.Troop)
            .Where(b => b.TripId == tripId && b.BookingStatus == BookingStatus.Confirmed && !b.IsDeleted)
            .OrderBy(b => b.Member!.FirstName)
            .ToListAsync();

        var existing = await _uow.TripAttendanceRecords.Query()
            .Where(r => r.TripId == tripId && !r.IsDeleted)
            .ToListAsync();

        return bookings.Select(b =>
        {
            var rec = existing.FirstOrDefault(r => r.MemberId == b.MemberId);
            return new TripAttendanceDto
            {
                TripId         = tripId,
                MemberId       = b.MemberId,
                MemberName     = b.Member?.FullName ?? "",
                TroopName      = b.Member?.Troop?.Name ?? "",
                MemberCustomId = b.Member?.CustomId ?? 0,
                Status         = rec?.Status ?? 1,  // 1 = Absent
                Notes          = rec?.Notes ?? ""
            };
        });
    }

    public async Task SaveAttendanceAsync(Guid tripId, SaveTripAttendanceDto dto)
    {
        var trip = await _uow.Trips.GetByIdAsync(tripId)
            ?? throw new InvalidOperationException("Trip not found.");

        foreach (var entry in dto.Records)
        {
            var rec = await _uow.TripAttendanceRecords.Query()
                .FirstOrDefaultAsync(r => r.TripId == tripId && r.MemberId == entry.MemberId && !r.IsDeleted);

            bool wasPresent = rec?.Status == 0;

            if (rec is null)
            {
                rec = new TripAttendanceRecord
                {
                    TripId   = tripId,
                    MemberId = entry.MemberId,
                    Status   = entry.Status,
                    Notes    = entry.Notes
                };
                await _uow.TripAttendanceRecords.AddAsync(rec);
            }
            else
            {
                rec.Status    = entry.Status;
                rec.Notes     = entry.Notes;
                rec.UpdatedAt = DateTime.UtcNow;
                _uow.TripAttendanceRecords.Update(rec);
            }

            // Award points only on the first transition TO Present (status 0).
            if (trip.HasPoints && trip.PointValue.HasValue
                && entry.Status == 0 && !wasPresent)
            {
                var noteKey = $"Trip attendance: {trip.Name}";
                var alreadyAwarded = await _uow.MemberPoints.AnyAsync(
                    p => p.MemberId == entry.MemberId && p.Note == noteKey);

                if (!alreadyAwarded)
                {
                    await _uow.MemberPoints.AddAsync(new MemberPoints
                    {
                        MemberId = entry.MemberId,
                        Points   = trip.PointValue.Value,
                        Date     = DateTime.UtcNow,
                        Note     = noteKey,
                        AddedBy  = Guid.Empty
                    });
                }
            }
        }

        await _uow.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mappers
    // ─────────────────────────────────────────────────────────────────────────

    private static TripDto MapTrip(Trip t) => new()
    {
        Id                = t.Id,
        Name              = t.Name,
        Description       = t.Description,
        TripDate          = t.TripDate,
        Location          = t.Location,
        Price             = t.Price,
        SiblingPrice      = t.SiblingPrice,
        MaxCapacity       = t.MaxCapacity,
        GroupId           = t.GroupId,
        HasPoints         = t.HasPoints,
        PointValue        = t.PointValue,
        Status            = t.Status,
        StatusName        = t.Status.ToString(),
        AllowInstallments = t.AllowInstallments,
        CreatedBy         = t.CreatedBy,
        CreatedAt         = t.CreatedAt,
        ConfirmedCount    = t.Bookings.Count(b => !b.IsDeleted && b.BookingStatus == BookingStatus.Confirmed),
        WaitingCount      = t.Bookings.Count(b => !b.IsDeleted && b.BookingStatus == BookingStatus.Waiting),
        TotalCollected    = t.Bookings
            .Where(b => !b.IsDeleted && b.BookingStatus == BookingStatus.Confirmed)
            .Sum(b => t.AllowInstallments
                ? b.Payments.Where(p => !p.IsDeleted).Sum(p => p.AmountPaid)
                : (b.PaidAt.HasValue ? b.AmountDue : 0m))
    };

    private static TripBookingDto MapBooking(TripBooking b, bool allowInstallments)
    {
        var payments  = b.Payments
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.PaidAt)
            .Select(MapPayment)
            .ToList();
        decimal totalPaid = payments.Sum(p => p.AmountPaid);

        return new TripBookingDto
        {
            Id                = b.Id,
            TripId            = b.TripId,
            MemberId          = b.MemberId,
            MemberName        = b.Member?.FullName ?? "",
            TroopName         = b.Member?.Troop?.Name ?? "",
            MemberCustomId    = b.Member?.CustomId ?? 0,
            BookingStatus     = b.BookingStatus,
            StatusName        = b.BookingStatus.ToString(),
            IsSibling         = b.IsSibling,
            AmountDue         = b.AmountDue,
            IsPaid            = allowInstallments
                                    ? totalPaid >= b.AmountDue
                                    : b.PaidAt.HasValue,
            PaidAt            = b.PaidAt,
            Notes             = b.Notes,
            CreatedAt         = b.CreatedAt,
            AllowInstallments = allowInstallments,
            TotalPaid         = totalPaid,
            Payments          = payments
        };
    }

    private static BookingPaymentDto MapPayment(BookingPayment p) => new()
    {
        Id         = p.Id,
        BookingId  = p.BookingId,
        AmountPaid = p.AmountPaid,
        PaidAt     = p.PaidAt,
        Notes      = p.Notes
    };
}
