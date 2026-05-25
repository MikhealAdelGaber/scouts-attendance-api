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

        // Derive effective permissions from role + explicit flags
        bool canAttend = user.Role == Domain.Enums.UserRole.SystemAdmin
                       || user.Role == Domain.Enums.UserRole.AttendanceOnly
                       || user.CanTakeAttendance;
        bool canEdit   = user.Role == Domain.Enums.UserRole.SystemAdmin
                       || user.CanEditMembers;
        bool canEvents = user.Role == Domain.Enums.UserRole.SystemAdmin
                       || user.CanCreateEvents;
        bool canTrips  = user.Role == Domain.Enums.UserRole.SystemAdmin
                       || user.CanAccessTrips;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name,  user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role,  user.Role.ToString()),
            new Claim("groupId",           user.GroupId?.ToString() ?? string.Empty),
            new Claim("troopId",           user.TroopId?.ToString() ?? string.Empty),
            new Claim("canTakeAttendance", canAttend.ToString().ToLower()),
            new Claim("canEditMembers",    canEdit.ToString().ToLower()),
            new Claim("canCreateEvents",   canEvents.ToString().ToLower()),
            new Claim("canAccessTrips",    canTrips.ToString().ToLower())
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
