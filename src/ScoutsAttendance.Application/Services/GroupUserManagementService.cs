using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Users;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Services;

public interface IGroupUserManagementService
{
    /// <summary>Returns users in the caller's group (or all users for SystemAdmin).</summary>
    Task<IEnumerable<GroupUserDto>> GetGroupUsersAsync();

    /// <summary>
    /// Updates role + permissions for a user in the caller's group.
    /// Throws <see cref="UnauthorizedAccessException"/> if the target is outside the caller's group
    /// or if a restricted role is being assigned by a GroupLeaderAdmin.
    /// Throws <see cref="InvalidOperationException"/> if trying to edit own role.
    /// </summary>
    Task<GroupUserDto?> UpdateRolePermissionsAsync(Guid targetUserId, UpdateRolePermissionsDto dto, Guid requesterId);
}

public class GroupUserManagementService : IGroupUserManagementService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    /// <summary>Roles that GroupLeaderAdmin is NOT allowed to assign.</summary>
    private static readonly HashSet<UserRole> RestrictedRoles = new()
    {
        UserRole.SystemAdmin,
        UserRole.GroupLeaderAdmin
    };

    public GroupUserManagementService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<GroupUserDto>> GetGroupUsersAsync()
    {
        var query = _uow.Users.Query()
            .Include(u => u.Group)
            .Where(u => !u.IsDeleted);

        // GroupLeaderAdmin: scope to own group only
        if (_currentUser.IsGroupLeaderAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(u => u.GroupId == _currentUser.GroupId.Value);
        // SystemAdmin: no scope restriction (sees all users)

        var users = await query.OrderBy(u => u.Username).ToListAsync();
        return users.Select(MapGroupUser);
    }

    public async Task<GroupUserDto?> UpdateRolePermissionsAsync(
        Guid targetUserId,
        UpdateRolePermissionsDto dto,
        Guid requesterId)
    {
        var target = await _uow.Users.Query()
            .FirstOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted);

        if (target is null) return null;

        // GroupLeaderAdmin security checks
        if (_currentUser.IsGroupLeaderAdmin)
        {
            // Cannot edit own role
            if (targetUserId == requesterId)
                throw new InvalidOperationException("You cannot change your own role.");

            // Can only manage users in the same group
            if (!_currentUser.GroupId.HasValue || target.GroupId != _currentUser.GroupId.Value)
                throw new UnauthorizedAccessException("You can only manage users within your own group.");

            // Cannot assign SystemAdmin or GroupLeaderAdmin
            if (RestrictedRoles.Contains(dto.Role))
                throw new UnauthorizedAccessException(
                    "You are not authorized to assign the SystemAdmin or GroupLeaderAdmin role.");
        }

        // Apply changes
        target.Role              = dto.Role;
        target.CanTakeAttendance = dto.CanTakeAttendance;
        target.CanEditMembers    = dto.CanEditMembers;
        target.CanCreateEvents   = dto.CanCreateEvents;
        target.CanAccessTrips    = dto.CanAccessTrips;
        target.CanAccessDashboard   = dto.CanAccessDashboard;
        target.CanAccessTroops      = dto.CanAccessTroops;
        target.CanAccessMembers     = dto.CanAccessMembers;
        target.CanAccessExcuses     = dto.CanAccessExcuses;
        target.CanAccessEvents      = dto.CanAccessEvents;
        target.CanAccessAttendance  = dto.CanAccessAttendance;
        target.CanAccessPoints      = dto.CanAccessPoints;
        target.CanAccessLeaderboard = dto.CanAccessLeaderboard;
        target.CanAccessExamScores  = dto.CanAccessExamScores;
        target.CanAccessReports     = dto.CanAccessReports;
        target.CanAccessBadges      = dto.CanAccessBadges;
        target.CanAccessProjects    = dto.CanAccessProjects;
        target.UpdatedAt            = DateTime.UtcNow;

        _uow.Users.Update(target);
        await _uow.SaveChangesAsync();

        return MapGroupUser(target);
    }

    private static GroupUserDto MapGroupUser(User u) => new()
    {
        Id               = u.Id,
        Username         = u.Username,
        Email            = u.Email,
        Role             = u.Role,
        RoleName         = u.Role.ToString(),
        IsActive         = u.IsActive,
        CanTakeAttendance = u.CanTakeAttendance,
        CanEditMembers    = u.CanEditMembers,
        CanCreateEvents   = u.CanCreateEvents,
        CanAccessTrips    = u.CanAccessTrips,
        CanAccessDashboard   = u.CanAccessDashboard,
        CanAccessTroops      = u.CanAccessTroops,
        CanAccessMembers     = u.CanAccessMembers,
        CanAccessExcuses     = u.CanAccessExcuses,
        CanAccessEvents      = u.CanAccessEvents,
        CanAccessAttendance  = u.CanAccessAttendance,
        CanAccessPoints      = u.CanAccessPoints,
        CanAccessLeaderboard = u.CanAccessLeaderboard,
        CanAccessExamScores  = u.CanAccessExamScores,
        CanAccessReports     = u.CanAccessReports,
        CanAccessBadges      = u.CanAccessBadges,
        CanAccessProjects    = u.CanAccessProjects
    };
}
