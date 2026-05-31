using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.Interfaces;

public interface ICurrentUserService
{
    Guid    UserId        { get; }
    string  Username      { get; }
    UserRole Role         { get; }
    Guid?   GroupId       { get; }
    Guid?   TroopId       { get; }
    bool    IsAuthenticated    { get; }
    bool    IsSystemAdmin      { get; }
    bool    IsGroupLeader      { get; }
    bool    IsAttendanceOnly   { get; }
    /// <summary>True when the user has a TroopId and is NOT a SystemAdmin.
    /// All data queries are scoped to that troop.</summary>
    bool    HasTroopScope      { get; }
    bool    CanTakeAttendance  { get; }
    bool    CanEditMembers     { get; }
    bool    CanCreateEvents    { get; }
    bool    CanAccessTrips     { get; }
    /// <summary>True for SystemAdmin, GroupLeader, or any user with the canAccessBadges claim.</summary>
    bool    CanAccessBadges    { get; }
}
