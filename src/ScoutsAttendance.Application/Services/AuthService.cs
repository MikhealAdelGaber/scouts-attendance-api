using BCrypt.Net;
using Microsoft.Extensions.Logging;
using ScoutsAttendance.Application.DTOs.Auth;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public record LoginResult(TokenResponseDto? Token, string? ErrorMessage);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginDto dto);
    Task<TokenResponseDto?> RegisterAsync(RegisterDto dto);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork uow, IJwtService jwt, ILogger<AuthService> logger)
    {
        _uow = uow;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(LoginDto dto)
    {
        // Find user by username (ignoring IsActive so we can return a specific message)
        var user = await _uow.Users.FindSingleAsync(u =>
            u.Username == dto.Username && !u.IsDeleted);

        // Wrong username or wrong password → generic message (don't reveal which)
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return new LoginResult(null, "Invalid username or password.");

        // Correct credentials but account is deactivated
        if (!user.IsActive)
            return new LoginResult(null,
                "Your account has been deactivated. Please contact your administrator.");

        var token = _jwt.GenerateToken(user);

        // Compute effective permissions (same logic as JwtService)
        bool canAttend = user.Role == UserRole.SystemAdmin || user.Role == UserRole.AttendanceOnly || user.CanTakeAttendance;
        bool canEdit   = user.Role == UserRole.SystemAdmin || user.CanEditMembers;
        bool canEvents = user.Role == UserRole.SystemAdmin || user.CanCreateEvents;
        bool canTrips  = user.Role == UserRole.SystemAdmin || user.CanAccessTrips;

        return new LoginResult(new TokenResponseDto
        {
            Token              = token,
            Username           = user.Username,
            Email              = user.Email,
            Role               = user.Role.ToString(),
            UserId             = user.Id,
            GroupId            = user.GroupId,
            TroopId            = user.TroopId,
            ExpiresAt          = DateTime.UtcNow.AddHours(24),
            CanTakeAttendance  = canAttend,
            CanEditMembers     = canEdit,
            CanCreateEvents    = canEvents,
            CanAccessTrips     = canTrips
        }, null);
    }

    public async Task<TokenResponseDto?> RegisterAsync(RegisterDto dto)
    {
        var exists = await _uow.Users.AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email);
        if (exists) return null;

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            GroupId = dto.GroupId,
            TroopId = dto.TroopId
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);
        bool canAttendR = user.Role == UserRole.SystemAdmin || user.Role == UserRole.AttendanceOnly || user.CanTakeAttendance;
        bool canEditR   = user.Role == UserRole.SystemAdmin || user.CanEditMembers;
        bool canEventsR = user.Role == UserRole.SystemAdmin || user.CanCreateEvents;
        bool canTripsR  = user.Role == UserRole.SystemAdmin || user.CanAccessTrips;
        return new TokenResponseDto
        {
            Token             = token,
            Username          = user.Username,
            Email             = user.Email,
            Role              = user.Role.ToString(),
            UserId            = user.Id,
            GroupId           = user.GroupId,
            TroopId           = user.TroopId,
            ExpiresAt         = DateTime.UtcNow.AddHours(24),
            CanTakeAttendance = canAttendR,
            CanEditMembers    = canEditR,
            CanCreateEvents   = canEventsR,
            CanAccessTrips    = canTripsR
        };
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return true;
    }
}
