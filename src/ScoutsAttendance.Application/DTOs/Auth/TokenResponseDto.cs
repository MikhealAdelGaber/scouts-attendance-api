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
}
