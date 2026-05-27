using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        bool isAdmin = user.Role == Domain.Enums.UserRole.SystemAdmin;

        // Derive effective action-permission values from role + explicit flags
        bool canAttend = isAdmin
                       || user.Role == Domain.Enums.UserRole.AttendanceOnly
                       || user.CanTakeAttendance;
        bool canEdit   = isAdmin || user.CanEditMembers;
        bool canEvents = isAdmin || user.CanCreateEvents;
        bool canTrips  = isAdmin || user.CanAccessTrips;

        // Page-access permissions — SystemAdmin always true; others use stored value
        bool B(bool stored) => isAdmin || stored;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name,  user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role,  user.Role.ToString()),
            new Claim("groupId",           user.GroupId?.ToString() ?? string.Empty),
            new Claim("troopId",           user.TroopId?.ToString() ?? string.Empty),
            // Action permissions
            new Claim("canTakeAttendance", canAttend.ToString().ToLower()),
            new Claim("canEditMembers",    canEdit.ToString().ToLower()),
            new Claim("canCreateEvents",   canEvents.ToString().ToLower()),
            new Claim("canAccessTrips",    canTrips.ToString().ToLower()),
            // Page-access permissions
            new Claim("canAccessTroops",      B(user.CanAccessTroops).ToString().ToLower()),
            new Claim("canAccessMembers",     B(user.CanAccessMembers).ToString().ToLower()),
            new Claim("canAccessExcuses",     B(user.CanAccessExcuses).ToString().ToLower()),
            new Claim("canAccessEvents",      B(user.CanAccessEvents).ToString().ToLower()),
            new Claim("canAccessAttendance",  B(user.CanAccessAttendance).ToString().ToLower()),
            new Claim("canAccessPoints",      B(user.CanAccessPoints).ToString().ToLower()),
            new Claim("canAccessLeaderboard", B(user.CanAccessLeaderboard).ToString().ToLower()),
            new Claim("canAccessExamScores",  B(user.CanAccessExamScores).ToString().ToLower()),
            new Claim("canAccessReports",     B(user.CanAccessReports).ToString().ToLower()),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true
            }, out _);
            return true;
        }
        catch { return false; }
    }

    public Guid? GetUserIdFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var idClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        return idClaim is null ? null : Guid.Parse(idClaim.Value);
    }
}
