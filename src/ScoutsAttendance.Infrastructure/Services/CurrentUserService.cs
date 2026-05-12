using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return claim is null ? Guid.Empty : Guid.Parse(claim);
        }
    }

    public string Username => User?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

    public UserRole Role
    {
        get
        {
            var role = User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(role, out var r) ? r : UserRole.AttendanceOnly;
        }
    }

    public Guid? GroupId
    {
        get
        {
            var claim = User?.FindFirst("groupId")?.Value;
            return string.IsNullOrEmpty(claim) ? null : Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public Guid? TroopId
    {
        get
        {
            var claim = User?.FindFirst("troopId")?.Value;
            return string.IsNullOrEmpty(claim) ? null : Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsSystemAdmin    => Role == UserRole.SystemAdmin;
    public bool IsGroupLeader    => Role == UserRole.GroupLeader;
    public bool IsAttendanceOnly => Role == UserRole.AttendanceOnly;

    /// <summary>True when the user has a TroopId and is NOT SystemAdmin —
    /// all data queries are automatically scoped to that troop.</summary>
    public bool HasTroopScope => !IsSystemAdmin && TroopId.HasValue;

    public bool CanTakeAttendance => ReadBoolClaim("canTakeAttendance")
                                   || IsSystemAdmin || IsAttendanceOnly;
    public bool CanEditMembers    => ReadBoolClaim("canEditMembers")
                                   || IsSystemAdmin;
    public bool CanCreateEvents   => ReadBoolClaim("canCreateEvents")
                                   || IsSystemAdmin;

    private bool ReadBoolClaim(string name)
    {
        var val = User?.FindFirst(name)?.Value;
        return val == "true";
    }
}
