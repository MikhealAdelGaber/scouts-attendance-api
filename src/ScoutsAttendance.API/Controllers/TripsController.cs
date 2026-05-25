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
    private readonly ITripService         _trips;
    private readonly ICurrentUserService  _current;

    public TripsController(ITripService trips, ICurrentUserService current)
    {
        _trips   = trips;
        _current = current;
    }

    // ─── Trip CRUD ────────────────────────────────────────────────────────────

    /// <summary>List all trips visible to the caller (scoped by group).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<TripDto>>>> GetAll()
    {
        if (!_current.CanAccessTrips)
            return Forbid();

        var groupId = _current.IsSystemAdmin ? (Guid?)null : _current.GroupId;
        var result  = await _trips.GetAllAsync(groupId);
        return Ok(ApiResponse<IEnumerable<TripDto>>.Ok(result));
    }

    /// <summary>Get a single trip with its bookings.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TripDto>>> GetById(Guid id)
    {
        if (!_current.CanAccessTrips) return Forbid();

        var trip = await _trips.GetByIdAsync(id);
        if (trip is null) return NotFound(ApiResponse<TripDto>.Fail("Trip not found"));

        // Scope check
        if (!_current.IsSystemAdmin && trip.GroupId != _current.GroupId)
            return Forbid();

        return Ok(ApiResponse<TripDto>.Ok(trip));
    }

    /// <summary>Create a new trip (scoped to caller's group).</summary>
    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<TripDto>>> Create([FromBody] CreateTripDto dto)
    {
        if (!_current.CanAccessTrips) return Forbid();

        var groupId = _current.GroupId ?? Guid.Empty;
        if (!_current.IsSystemAdmin && groupId == Guid.Empty)
            return BadRequest(ApiResponse<TripDto>.Fail("No group assigned to your account."));

        var result = await _trips.CreateAsync(dto, groupId, _current.Username);
        return Ok(ApiResponse<TripDto>.Ok(result, "Trip created"));
    }

    /// <summary>Update a trip.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<TripDto>>> Update(Guid id, [FromBody] UpdateTripDto dto)
    {
        if (!_current.CanAccessTrips) return Forbid();

        var result = await _trips.UpdateAsync(id, dto, _current.GroupId, _current.IsSystemAdmin);
        return result is null
            ? NotFound(ApiResponse<TripDto>.Fail("Trip not found"))
            : Ok(ApiResponse<TripDto>.Ok(result, "Trip updated"));
    }

    /// <summary>Delete a trip.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _trips.DeleteAsync(id, _current.GroupId, _current.IsSystemAdmin);
        return ok
            ? Ok(ApiResponse.Ok("Trip deleted"))
            : NotFound(ApiResponse.Fail("Trip not found"));
    }

    // ─── Bookings ─────────────────────────────────────────────────────────────

    /// <summary>List all bookings for a trip.</summary>
    [HttpGet("{tripId:guid}/bookings")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TripBookingDto>>>> GetBookings(Guid tripId)
    {
        if (!_current.CanAccessTrips) return Forbid();
        var result = await _trips.GetBookingsAsync(tripId);
        return Ok(ApiResponse<IEnumerable<TripBookingDto>>.Ok(result));
    }

    /// <summary>Book a member on a trip.</summary>
    [HttpPost("{tripId:guid}/bookings")]
    public async Task<ActionResult<ApiResponse<TripBookingDto>>> BookMember(
        Guid tripId, [FromBody] CreateBookingDto dto)
    {
        if (!_current.CanAccessTrips) return Forbid();

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

    /// <summary>Cancel a booking (and promote first waiting-list member if needed).</summary>
    [HttpDelete("{tripId:guid}/bookings/{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<TripBookingDto>>> CancelBooking(Guid tripId, Guid bookingId)
    {
        if (!_current.CanAccessTrips) return Forbid();
        var result = await _trips.CancelBookingAsync(bookingId);
        return result is null
            ? NotFound(ApiResponse<TripBookingDto>.Fail("Booking not found"))
            : Ok(ApiResponse<TripBookingDto>.Ok(result, "Booking cancelled"));
    }

    /// <summary>Toggle paid status for a booking.</summary>
    [HttpPost("{tripId:guid}/bookings/{bookingId:guid}/mark-paid")]
    public async Task<ActionResult<ApiResponse<TripBookingDto>>> MarkPaid(Guid tripId, Guid bookingId)
    {
        if (!_current.CanAccessTrips) return Forbid();
        var result = await _trips.MarkPaidAsync(bookingId);
        return result is null
            ? NotFound(ApiResponse<TripBookingDto>.Fail("Booking not found"))
            : Ok(ApiResponse<TripBookingDto>.Ok(result, "Payment status updated"));
    }

    // ─── Attendance ───────────────────────────────────────────────────────────

    /// <summary>Get attendance for a trip (confirmed members).</summary>
    [HttpGet("{tripId:guid}/attendance")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TripAttendanceDto>>>> GetAttendance(Guid tripId)
    {
        if (!_current.CanAccessTrips) return Forbid();
        var result = await _trips.GetAttendanceAsync(tripId);
        return Ok(ApiResponse<IEnumerable<TripAttendanceDto>>.Ok(result));
    }

    /// <summary>Save attendance for a trip (upsert).</summary>
    [HttpPost("{tripId:guid}/attendance")]
    public async Task<ActionResult<ApiResponse>> SaveAttendance(
        Guid tripId, [FromBody] SaveTripAttendanceDto dto)
    {
        if (!_current.CanAccessTrips) return Forbid();

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
}
