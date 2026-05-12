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
}

public class UserLeaderDto
{
    public Guid   Id       { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string Display  { get; set; } = string.Empty;  // "username (email)"
}
