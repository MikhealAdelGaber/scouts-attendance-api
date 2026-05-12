using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Users;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IUserManagementService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task<UserDto?> UpdateAsync(Guid id, UpdateUserDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<UserLeaderDto>> GetAvailableLeadersAsync();
}

public class UserManagementService : IUserManagementService
{
    private readonly IUnitOfWork _uow;

    public UserManagementService(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _uow.Users.Query()
            .Include(u => u.Group)
            .Include(u => u.Troop)
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.Username)
            .ToListAsync();

        return users.Select(Map);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Group)
            .Include(u => u.Troop)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

        return user is null ? null : Map(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        var exists = await _uow.Users.AnyAsync(
            u => u.Username == dto.Username || u.Email == dto.Email);
        if (exists)
            throw new InvalidOperationException("Username or email already taken");

        // Set sensible permission defaults per role
        bool canAttend = dto.Role is UserRole.AttendanceOnly || dto.CanTakeAttendance;
        bool canEdit   = dto.CanEditMembers;
        bool canEvents = dto.CanCreateEvents;

        var user = new User
        {
            Username          = dto.Username.Trim(),
            Email             = dto.Email.Trim().ToLower(),
            PasswordHash      = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role              = dto.Role,
            GroupId           = dto.GroupId,
            TroopId           = dto.TroopId,
            IsActive          = true,
            CanTakeAttendance = canAttend,
            CanEditMembers    = canEdit,
            CanCreateEvents   = canEvents
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        return await GetByIdAsync(user.Id) ?? throw new Exception("Failed to load user");
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var user = await _uow.Users.GetByIdAsync(id);
        if (user is null) return null;

        user.Role              = dto.Role;
        user.GroupId           = dto.GroupId;
        user.TroopId           = dto.TroopId;
        user.IsActive          = dto.IsActive;
        user.CanTakeAttendance = dto.CanTakeAttendance;
        user.CanEditMembers    = dto.CanEditMembers;
        user.CanCreateEvents   = dto.CanCreateEvents;
        user.UpdatedAt         = DateTime.UtcNow;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _uow.Users.GetByIdAsync(id);
        if (user is null) return false;
        _uow.Users.SoftDelete(user);
        await _uow.SaveChangesAsync();
        return true;
    }

    /// <summary>Returns users eligible to be set as a troop leader.</summary>
    public async Task<IEnumerable<UserLeaderDto>> GetAvailableLeadersAsync()
    {
        var users = await _uow.Users.Query()
            .Where(u => !u.IsDeleted && u.IsActive
                     && (u.Role == UserRole.GroupLeader || u.Role == UserRole.SystemAdmin))
            .OrderBy(u => u.Username)
            .ToListAsync();

        return users.Select(u => new UserLeaderDto
        {
            Id       = u.Id,
            Username = u.Username,
            Email    = u.Email,
            Display  = $"{u.Username} ({u.Email})"
        });
    }

    private static UserDto Map(User u) => new()
    {
        Id                = u.Id,
        Username          = u.Username,
        Email             = u.Email,
        Role              = u.Role,
        RoleName          = u.Role.ToString(),
        GroupId           = u.GroupId,
        GroupName         = u.Group?.Name,
        TroopId           = u.TroopId,
        TroopName         = u.Troop?.Name,
        IsActive          = u.IsActive,
        CanTakeAttendance = u.CanTakeAttendance,
        CanEditMembers    = u.CanEditMembers,
        CanCreateEvents   = u.CanCreateEvents,
        CreatedAt         = u.CreatedAt
    };
}
