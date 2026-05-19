using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Events;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Application.Services;

public interface IEventService
{
    Task<IEnumerable<EventDto>> GetAllAsync(Guid? groupId = null, Guid? troopId = null, bool activeOnly = false);
    Task<EventDto?> GetByIdAsync(Guid id);
    Task<EventDto> CreateAsync(CreateEventDto dto);
    Task<EventDto?> UpdateAsync(Guid id, UpdateEventDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public class EventService : IEventService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public EventService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<EventDto>> GetAllAsync(Guid? groupId = null, Guid? troopId = null, bool activeOnly = false)
    {
        var query = _uow.Events.Query()
            .Include(e => e.Group)
            .Include(e => e.Troop)
            .Include(e => e.AttendanceRecords)
            .Where(e => !e.IsDeleted);

        if (activeOnly)
            query = query.Where(e => e.IsActive);

        if (!_currentUser.IsSystemAdmin)
        {
            if (_currentUser.HasTroopScope)
                // Troop-scoped: see group-wide events and their own troop events
                query = query.Where(e => e.TroopId == null || e.TroopId == _currentUser.TroopId!.Value);
            else if (_currentUser.GroupId.HasValue)
                query = query.Where(e => e.GroupId == _currentUser.GroupId.Value);
        }

        // Caller-supplied filters override scoping
        if (groupId.HasValue) query = query.Where(e => e.GroupId == groupId.Value);
        if (troopId.HasValue) query = query.Where(e => e.TroopId == troopId.Value);

        var events = await query.OrderByDescending(e => e.EventDate).ToListAsync();
        return events.Select(MapToDto);
    }

    public async Task<EventDto?> GetByIdAsync(Guid id)
    {
        var e = await _uow.Events.Query()
            .Include(e => e.Group)
            .Include(e => e.Troop)
            .Include(e => e.AttendanceRecords)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        return e is null ? null : MapToDto(e);
    }

    public async Task<EventDto> CreateAsync(CreateEventDto dto)
    {
        // SystemAdmin must supply a GroupId explicitly in the DTO
        Guid groupId;
        if (_currentUser.IsSystemAdmin)
        {
            if (dto.GroupId is null)
                throw new InvalidOperationException("SystemAdmin must supply a GroupId when creating an event");
            groupId = dto.GroupId.Value;
        }
        else
        {
            groupId = _currentUser.GroupId
                      ?? throw new InvalidOperationException("User has no group assigned");
        }

        // Normalise to UTC midnight of the chosen calendar date so timezone drift
        // (e.g. Egypt UTC+2 sending "May 20 local" as "May 19 22:00 UTC") never
        // shifts the stored date to the previous day.
        var eventDateUtc = DateTime.SpecifyKind(dto.EventDate.Date, DateTimeKind.Utc);

        var ev = new Domain.Entities.Event
        {
            Name          = dto.Name,
            Description   = dto.Description,
            EventDate     = eventDateUtc,
            GroupId       = groupId,
            TroopId       = dto.TroopId,
            PresentPoints = dto.PresentPoints,
            LatePoints    = dto.LatePoints,
            ExcusedPoints = dto.ExcusedPoints,
            AbsentPoints  = dto.AbsentPoints,
            CreatedBy     = _currentUser.UserId
        };

        await _uow.Events.AddAsync(ev);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(ev.Id) ?? throw new Exception("Failed to load event");
    }

    public async Task<EventDto?> UpdateAsync(Guid id, UpdateEventDto dto)
    {
        var ev = await _uow.Events.GetByIdAsync(id);
        if (ev is null) return null;

        ev.Name          = dto.Name;
        ev.Description   = dto.Description;
        ev.EventDate     = DateTime.SpecifyKind(dto.EventDate.Date, DateTimeKind.Utc);
        ev.IsActive      = dto.IsActive;
        ev.PresentPoints = dto.PresentPoints;
        ev.LatePoints    = dto.LatePoints;
        ev.ExcusedPoints = dto.ExcusedPoints;
        ev.AbsentPoints  = dto.AbsentPoints;
        ev.UpdatedAt     = DateTime.UtcNow;

        _uow.Events.Update(ev);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var ev = await _uow.Events.GetByIdAsync(id);
        if (ev is null) return false;
        _uow.Events.SoftDelete(ev);
        await _uow.SaveChangesAsync();
        return true;
    }

    private static EventDto MapToDto(Domain.Entities.Event e) => new()
    {
        Id              = e.Id,
        Name            = e.Name,
        Description     = e.Description,
        EventDate       = e.EventDate,
        GroupId         = e.GroupId,
        GroupName       = e.Group?.Name ?? string.Empty,
        TroopId         = e.TroopId,
        TroopName       = e.Troop?.Name,
        IsActive        = e.IsActive,
        PresentPoints   = e.PresentPoints,
        LatePoints      = e.LatePoints,
        ExcusedPoints   = e.ExcusedPoints,
        AbsentPoints    = e.AbsentPoints,
        AttendanceCount = e.AttendanceRecords?.Count(a => !a.IsDeleted) ?? 0,
        CreatedAt       = e.CreatedAt
    };
}
