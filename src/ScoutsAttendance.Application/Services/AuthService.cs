using BCrypt.Net;
using Microsoft.Extensions.Logging;
using ScoutsAttendance.Application.DTOs.Auth;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IAuthService
{
    Task<TokenResponseDto?> LoginAsync(LoginDto dto);
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

    public async Task<TokenResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _uow.Users.FindSingleAsync(u =>
            u.Username == dto.Username && !u.IsDeleted && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return null;

        var token = _jwt.GenerateToken(user);
        return new TokenResponseDto
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            UserId = user.Id,
            GroupId = user.GroupId,
            TroopId = user.TroopId,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
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
        return new TokenResponseDto
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            UserId = user.Id,
            GroupId = user.GroupId,
            TroopId = user.TroopId,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
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
