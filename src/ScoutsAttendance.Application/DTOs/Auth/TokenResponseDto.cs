namespace ScoutsAttendance.Application.DTOs.Auth;

public class TokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? TroopId { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Fine-grained permission flags — mirrors what the JWT claims carry.
    // Including them here means the Angular client doesn't need to decode the
    // JWT itself; it just reads them from the stored AuthUser object.
    public bool CanTakeAttendance { get; set; }
    public bool CanEditMembers    { get; set; }
    public bool CanCreateEvents   { get; set; }
    public bool CanAccessTrips    { get; set; }
}
