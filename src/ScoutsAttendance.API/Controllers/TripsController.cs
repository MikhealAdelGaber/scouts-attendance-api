using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Trips;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TripsController : ControllerBase
{
    private readonly ITripService        _trips;
    private readonly ICurrentUserService _current;
    private readonly ITripExportService  _export;

    public TripsController(
        ITripService        trips,
        ICurrentUserService current,
        ITripExportService  export)
    {
        _trips   = trips;
        _current = current;
        _export  = export;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Shared permission gate for all trip endpoints.
    /// Returns a 403 result when the caller lacks CanAccessTrips; null means OK.
    /// </summary>
    private ActionResult? RequireTripsPermission() =>
        _current.CanAccessTrips
            ? null
            : StatusCode(403, ApiResponse.Fail("You don't have permission to access Trips & Camps."));

    /// <summary>
    /// Verifies the caller (non-SystemAdmin) owns the trip with <paramref name="tripId"/>.
    /// Returns null on success; a 403 or 404 result if the check fails.
    /// </summary>
    private async Task<ActionResult?> RequireTripGroupAccessAsync(Guid tripId)
    {
        if (_current.IsSystemAdmin) return null;             // admins see everything

        var trip = await _trips.GetByIdAsync(tripId);
        if (trip is null)
            return NotFound(ApiResponse.Fail("Trip not found"));

        if (trip.GroupId != _current.GroupId)
            return StatusCode(403, ApiResponse.Fail("You don't have permission to access this trip."));

        return null;
    }

    // ─── Trip CRUD ────────────────────────────────────────────────────────────

    /// <summary>List all trips visible to the caller (scoped by group).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<TripDto>>>> GetAll()
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var groupId = _current.IsSystemAdmin ? (Guid?)null : _current.GroupId;
        var result  = await _trips.GetAllAsync(groupId);
        return Ok(ApiResponse<IEnumerable<TripDto>>.Ok(result));
    }

    /// <summary>Get a single trip.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TripDto>>> GetById(Guid id)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var trip = await _trips.GetByIdAsync(id);
        if (trip is null)
            return NotFound(ApiResponse<TripDto>.Fail("Trip not found"));

        if (!_current.IsSystemAdmin && trip.GroupId != _current.GroupId)
            return StatusCode(403, ApiResponse.Fail("You don't have permission to access this trip."));

        return Ok(ApiResponse<TripDto>.Ok(trip));
    }

    /// <summary>
    /// Create a new trip.
    /// SystemAdmin must supply a GroupId in the body (they have no automatic group).
    /// GroupLeader/AttendanceOnly get GroupId auto-set from JWT.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<TripDto>>> Create([FromBody] CreateTripDto dto)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        Guid groupId;
        if (_current.IsSystemAdmin)
        {
            if (!dto.GroupId.HasValue || dto.GroupId == Guid.Empty)
                return BadRequest(ApiResponse<TripDto>.Fail(
                    "SystemAdmin must specify a GroupId when creating a trip."));
            groupId = dto.GroupId.Value;
        }
        else
        {
            groupId = _current.GroupId ?? Guid.Empty;
            if (groupId == Guid.Empty)
                return BadRequest(ApiResponse<TripDto>.Fail(
                    "No group assigned to your account. Contact a system administrator."));
        }

        var result = await _trips.CreateAsync(dto, groupId, _current.Username);
        return Ok(ApiResponse<TripDto>.Ok(result, "Trip created"));
    }

    /// <summary>Update a trip (GroupLeader+ only).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<TripDto>>> Update(Guid id, [FromBody] UpdateTripDto dto)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var callerGroupId = (_current.IsSystemAdmin && dto.GroupId.HasValue)
            ? dto.GroupId
            : _current.GroupId;

        var result = await _trips.UpdateAsync(id, dto, callerGroupId, _current.IsSystemAdmin);
        return result is null
            ? NotFound(ApiResponse<TripDto>.Fail("Trip not found"))
            : Ok(ApiResponse<TripDto>.Ok(result, "Trip updated"));
    }

    /// <summary>Delete a trip (GroupLeader+ only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var ok = await _trips.DeleteAsync(id, _current.GroupId, _current.IsSystemAdmin);
        return ok
            ? Ok(ApiResponse.Ok("Trip deleted"))
            : NotFound(ApiResponse.Fail("Trip not found"));
    }

    // ─── Bookings ─────────────────────────────────────────────────────────────

    /// <summary>List all bookings for a trip (all roles with CanAccessTrips).</summary>
    [HttpGet("{tripId:guid}/bookings")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TripBookingDto>>>> GetBookings(Guid tripId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var result = await _trips.GetBookingsAsync(tripId);
        return Ok(ApiResponse<IEnumerable<TripBookingDto>>.Ok(result));
    }

    /// <summary>
    /// Book a member on a trip.
    /// All roles with CanAccessTrips can add bookings (GroupLeader AND AttendanceOnly).
    /// </summary>
    [HttpPost("{tripId:guid}/bookings")]
    public async Task<ActionResult<ApiResponse<TripBookingDto>>> BookMember(
        Guid tripId, [FromBody] CreateBookingDto dto)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        try
        {
            var result = await _trips.BookMemberAsync(tripId, dto);
            return Ok(ApiResponse<TripBookingDto>.Ok(result, "Member booked"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TripBookingDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Cancel a booking and promote the first waiting-list member if needed.
    /// All roles with CanAccessTrips (GroupLeader AND AttendanceOnly) can cancel bookings.
    /// </summary>
    [HttpDelete("{tripId:guid}/bookings/{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<TripBookingDto>>> CancelBooking(Guid tripId, Guid bookingId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var result = await _trips.CancelBookingAsync(bookingId);
        return result is null
            ? NotFound(ApiResponse<TripBookingDto>.Fail("Booking not found"))
            : Ok(ApiResponse<TripBookingDto>.Ok(result, "Booking cancelled"));
    }

    /// <summary>
    /// Toggle paid status for a booking.
    /// All roles with CanAccessTrips (GroupLeader AND AttendanceOnly) can mark paid.
    /// </summary>
    [HttpPost("{tripId:guid}/bookings/{bookingId:guid}/mark-paid")]
    public async Task<ActionResult<ApiResponse<TripBookingDto>>> MarkPaid(Guid tripId, Guid bookingId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var result = await _trips.MarkPaidAsync(bookingId);
        return result is null
            ? NotFound(ApiResponse<TripBookingDto>.Fail("Booking not found"))
            : Ok(ApiResponse<TripBookingDto>.Ok(result, "Payment status updated"));
    }

    // ─── Flexible payments ────────────────────────────────────────────────────

    /// <summary>
    /// Get all payment records for a specific booking.
    /// All roles with CanAccessTrips.
    /// </summary>
    [HttpGet("{tripId:guid}/bookings/{bookingId:guid}/payments")]
    public async Task<ActionResult<ApiResponse<IEnumerable<BookingPaymentDto>>>> GetPayments(
        Guid tripId, Guid bookingId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var result = await _trips.GetPaymentsAsync(bookingId);
        return Ok(ApiResponse<IEnumerable<BookingPaymentDto>>.Ok(result));
    }

    /// <summary>
    /// Record a new payment toward a booking.
    /// Validates amount &gt; 0 and total would not exceed amountDue.
    /// All roles with CanAccessTrips.
    /// </summary>
    [HttpPost("{tripId:guid}/bookings/{bookingId:guid}/payments")]
    public async Task<ActionResult<ApiResponse<BookingPaymentDto>>> AddPayment(
        Guid tripId, Guid bookingId, [FromBody] AddPaymentDto dto)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        try
        {
            var result = await _trips.AddPaymentAsync(tripId, bookingId, dto);
            return Ok(ApiResponse<BookingPaymentDto>.Ok(result, "Payment recorded"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<BookingPaymentDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Delete (soft) a payment record.
    /// All roles with CanAccessTrips.
    /// </summary>
    [HttpDelete("{tripId:guid}/bookings/{bookingId:guid}/payments/{paymentId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeletePayment(
        Guid tripId, Guid bookingId, Guid paymentId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var ok = await _trips.DeletePaymentAsync(tripId, paymentId);
        return ok
            ? Ok(ApiResponse.Ok("Payment deleted"))
            : NotFound(ApiResponse.Fail("Payment not found"));
    }

    // ─── Attendance ───────────────────────────────────────────────────────────

    /// <summary>Get attendance for a trip (all roles with CanAccessTrips).</summary>
    [HttpGet("{tripId:guid}/attendance")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TripAttendanceDto>>>> GetAttendance(Guid tripId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var result = await _trips.GetAttendanceAsync(tripId);
        return Ok(ApiResponse<IEnumerable<TripAttendanceDto>>.Ok(result));
    }

    /// <summary>
    /// Save attendance for a trip (upsert).
    /// All roles with CanAccessTrips can mark attendance (GroupLeader AND AttendanceOnly).
    /// </summary>
    [HttpPost("{tripId:guid}/attendance")]
    public async Task<ActionResult<ApiResponse>> SaveAttendance(
        Guid tripId, [FromBody] SaveTripAttendanceDto dto)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        try
        {
            await _trips.SaveAttendanceAsync(tripId, dto);
            return Ok(ApiResponse.Ok("Attendance saved"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    // ─── Export ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Export trip data as Excel (.xlsx).
    /// All roles with CanAccessTrips can export.
    /// </summary>
    [HttpGet("{tripId:guid}/export/excel")]
    public async Task<IActionResult> ExportExcel(Guid tripId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var (bytes, filename) = await _export.ExportExcelAsync(tripId);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    /// <summary>
    /// Export trip data as PDF.
    /// All roles with CanAccessTrips can export.
    /// </summary>
    [HttpGet("{tripId:guid}/export/pdf")]
    public async Task<IActionResult> ExportPdf(Guid tripId)
    {
        var deny = RequireTripsPermission();
        if (deny is not null) return deny;

        var scope = await RequireTripGroupAccessAsync(tripId);
        if (scope is not null) return scope;

        var (bytes, filename) = await _export.ExportPdfAsync(tripId, _current.Username);
        return File(bytes, "application/pdf", filename);
    }
}
