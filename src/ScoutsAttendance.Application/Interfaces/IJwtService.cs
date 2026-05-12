using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    bool ValidateToken(string token);
    Guid? GetUserIdFromToken(string token);
}
