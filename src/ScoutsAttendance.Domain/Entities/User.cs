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

    // Fine-grained permissions (stored on the entity; also embedded in JWT)
    public bool CanTakeAttendance { get; set; } = false;
    public bool CanEditMembers    { get; set; } = false;
    public bool CanCreateEvents   { get; set; } = false;
    public bool CanAccessTrips    { get; set; } = false;

    // Navigation
    public Group?  Group  { get; set; }
    public Troop?  Troop  { get; set; }
    public Member? Member { get; set; }
}
