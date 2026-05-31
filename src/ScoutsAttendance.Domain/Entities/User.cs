using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role   { get; set; }
    public Guid? GroupId   { get; set; }
    public Guid? TroopId   { get; set; }
    public bool IsActive   { get; set; } = true;

    // Fine-grained action permissions (stored on entity; embedded in JWT)
    public bool CanTakeAttendance { get; set; } = false;
    public bool CanEditMembers    { get; set; } = false;
    public bool CanCreateEvents   { get; set; } = false;
    public bool CanAccessTrips    { get; set; } = false;

    // Page-access permissions (default true — existing users keep full access)
    public bool CanAccessDashboard   { get; set; } = true;
    public bool CanAccessTroops      { get; set; } = true;
    public bool CanAccessMembers     { get; set; } = true;
    public bool CanAccessExcuses     { get; set; } = true;
    public bool CanAccessEvents      { get; set; } = true;
    public bool CanAccessAttendance  { get; set; } = true;
    public bool CanAccessPoints      { get; set; } = true;
    public bool CanAccessLeaderboard { get; set; } = true;
    public bool CanAccessExamScores  { get; set; } = true;
    public bool CanAccessReports     { get; set; } = true;

    // Badge access permission — defaults FALSE (opt-in for non-admin roles)
    public bool CanAccessBadges      { get; set; } = false;

    // Projects/Assessment access — defaults FALSE (opt-in for non-admin roles)
    public bool CanAccessProjects    { get; set; } = false;

    // Navigation
    public Group?  Group  { get; set; }
    public Troop?  Troop  { get; set; }
    public Member? Member { get; set; }
}
