using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Application.Services;

public record ProfileDto(
    Guid   Id,
    string Username,
    string Email,
    string Role,
    string? GroupName,
    string? TroopName,
    bool   IsActive,
    bool   CanTakeAttendance,
    bool   CanEditMembers,
    bool   CanCreateEvents,
    DateTime CreatedAt);

public record ChangePasswordDto(string CurrentPassword, string NewPassword);

public interface IProfileService
{
    Task<ProfileDto?> GetCurrentAsync();
    Task<bool>        ChangePasswordAsync(ChangePasswordDto dto);
}

public class ProfileService : IProfileService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public ProfileService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<ProfileDto?> GetCurrentAsync()
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Group)
            .Include(u => u.Troop)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId && !u.IsDeleted);

        if (user is null) return null;

        return new ProfileDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.Group?.Name,
            user.Troop?.Name,
            user.IsActive,
            user.CanTakeAttendance,
            user.CanEditMembers,
            user.CanCreateEvents,
            user.CreatedAt);
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordDto dto)
    {
        var user = await _uow.Users.GetByIdAsync(_currentUser.UserId);
        if (user is null) return false;

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.UpdatedAt    = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return true;
    }
}
