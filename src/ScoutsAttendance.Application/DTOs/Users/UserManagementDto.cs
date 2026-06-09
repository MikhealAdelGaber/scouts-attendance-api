using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.Users;

public class UserDto
{
    public Guid     Id         { get; set; }
    public string   Username   { get; set; } = string.Empty;
    public string   Email      { get; set; } = string.Empty;
    public UserRole Role       { get; set; }
    public string   RoleName   { get; set; } = string.Empty;
    public Guid?    GroupId    { get; set; }
    public string?  GroupName  { get; set; }
    public Guid?    TroopId    { get; set; }
    public string?  TroopName  { get; set; }
    public bool     IsActive          { get; set; }
    public bool     CanTakeAttendance { get; set; }
    public bool     CanEditMembers    { get; set; }
    public bool     CanCreateEvents   { get; set; }
    public bool     CanAccessTrips    { get; set; }
    // Page-access permissions
    public bool     CanAccessDashboard   { get; set; }
    public bool     CanAccessTroops      { get; set; }
    public bool     CanAccessMembers     { get; set; }
    public bool     CanAccessExcuses     { get; set; }
    public bool     CanAccessEvents      { get; set; }
    public bool     CanAccessAttendance  { get; set; }
    public bool     CanAccessPoints      { get; set; }
    public bool     CanAccessLeaderboard { get; set; }
    public bool     CanAccessExamScores  { get; set; }
    public bool     CanAccessReports     { get; set; }
    public bool     CanAccessBadges      { get; set; }
    public bool     CanAccessProjects    { get; set; }
    public DateTime CreatedAt  { get; set; }
}

public class CreateUserDto
{
    [Required, MinLength(3)] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress]  public string Email    { get; set; } = string.Empty;
    [Required, MinLength(6)]  public string Password { get; set; } = string.Empty;
    [Required] public UserRole Role    { get; set; } = UserRole.AttendanceOnly;
    public Guid? GroupId  { get; set; }
    public Guid? TroopId  { get; set; }
    public bool  CanTakeAttendance { get; set; } = false;
    public bool  CanEditMembers    { get; set; } = false;
    public bool  CanCreateEvents   { get; set; } = false;
    public bool  CanAccessTrips    { get; set; } = false;
    // Page-access permissions — all true by default for new users
    public bool  CanAccessDashboard   { get; set; } = true;
    public bool  CanAccessTroops      { get; set; } = true;
    public bool  CanAccessMembers     { get; set; } = true;
    public bool  CanAccessExcuses     { get; set; } = true;
    public bool  CanAccessEvents      { get; set; } = true;
    public bool  CanAccessAttendance  { get; set; } = true;
    public bool  CanAccessPoints      { get; set; } = true;
    public bool  CanAccessLeaderboard { get; set; } = true;
    public bool  CanAccessExamScores  { get; set; } = true;
    public bool  CanAccessReports     { get; set; } = true;
    public bool  CanAccessBadges      { get; set; } = false;
    public bool  CanAccessProjects    { get; set; } = false;
}

public class UpdateUserDto
{
    [Required] public UserRole Role { get; set; }
    public Guid? GroupId  { get; set; }
    public Guid? TroopId  { get; set; }
    public bool  IsActive          { get; set; } = true;
    public bool  CanTakeAttendance { get; set; }
    public bool  CanEditMembers    { get; set; }
    public bool  CanCreateEvents   { get; set; }
    public bool  CanAccessTrips    { get; set; }
    // Page-access permissions
    public bool  CanAccessDashboard   { get; set; } = true;
    public bool  CanAccessTroops      { get; set; } = true;
    public bool  CanAccessMembers     { get; set; } = true;
    public bool  CanAccessExcuses     { get; set; } = true;
    public bool  CanAccessEvents      { get; set; } = true;
    public bool  CanAccessAttendance  { get; set; } = true;
    public bool  CanAccessPoints      { get; set; } = true;
    public bool  CanAccessLeaderboard { get; set; } = true;
    public bool  CanAccessExamScores  { get; set; } = true;
    public bool  CanAccessReports     { get; set; } = true;
    public bool  CanAccessBadges      { get; set; } = false;
    public bool  CanAccessProjects    { get; set; } = false;
}

public class UserLeaderDto
{
    public Guid   Id       { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string Display  { get; set; } = string.Empty;  // "username (email)"
}

public class AdminChangePasswordDto
{
    [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
}

// ─── GroupLeaderAdmin: group-scoped user management ──────────────────────────

/// <summary>Slim user DTO returned for group-scoped user list.</summary>
public class GroupUserDto
{
    public Guid     Id       { get; set; }
    public string   Username { get; set; } = string.Empty;
    public string   Email    { get; set; } = string.Empty;
    public UserRole Role     { get; set; }
    public string   RoleName { get; set; } = string.Empty;
    public bool     IsActive { get; set; }
    // Permissions editable by GroupLeaderAdmin
    public bool CanTakeAttendance { get; set; }
    public bool CanEditMembers    { get; set; }
    public bool CanCreateEvents   { get; set; }
    public bool CanAccessTrips    { get; set; }
    public bool CanAccessDashboard   { get; set; }
    public bool CanAccessTroops      { get; set; }
    public bool CanAccessMembers     { get; set; }
    public bool CanAccessExcuses     { get; set; }
    public bool CanAccessEvents      { get; set; }
    public bool CanAccessAttendance  { get; set; }
    public bool CanAccessPoints      { get; set; }
    public bool CanAccessLeaderboard { get; set; }
    public bool CanAccessExamScores  { get; set; }
    public bool CanAccessReports     { get; set; }
    public bool CanAccessBadges      { get; set; }
    public bool CanAccessProjects    { get; set; }
}

/// <summary>Payload for GroupLeaderAdmin updating another user's role + permissions.</summary>
public class UpdateRolePermissionsDto
{
    /// <summary>
    /// Allowed values: GroupLeader (2), AttendanceOnly (5).
    /// GroupLeaderAdmin cannot assign SystemAdmin (1) or GroupLeaderAdmin (6).
    /// </summary>
    [Required] public UserRole Role { get; set; }
    public bool CanTakeAttendance { get; set; }
    public bool CanEditMembers    { get; set; }
    public bool CanCreateEvents   { get; set; }
    public bool CanAccessTrips    { get; set; }
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
    public bool CanAccessBadges      { get; set; }
    public bool CanAccessProjects    { get; set; }
}
