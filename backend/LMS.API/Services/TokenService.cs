using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LMS.API.Entities;
using Microsoft.IdentityModel.Tokens;

namespace LMS.API.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime Expiration) GenerateToken(
        Usuario user,
        string? fullName = null,
        string? roleName = null,
        string? gradeName = null,
        string? avatarUrl = null)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "LmsPrimarySchoolSuperSecretKey2026!@#$%^&*()_+";
        var issuer = _configuration["Jwt:Issuer"] ?? "LMS.API";
        var audience = _configuration["Jwt:Audience"] ?? "LMS.Client";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var name = fullName ?? user.FullName;
        if (string.IsNullOrWhiteSpace(name)) name = user.Username;

        var role = roleName ?? user.Rol?.Nombre ?? "ALUMNO";
        var avatar = avatarUrl ?? user.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={user.Username}";
        var grade = gradeName ?? user.Alumno?.Grado?.Nombre ?? string.Empty;

        // Normalización dual de roles (admite nombres en español de bd.txt, inglés y .admin)
        string standardRole;
        if (role.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) || 
            role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
            role.Equals(".admin", StringComparison.OrdinalIgnoreCase))
        {
            standardRole = "Admin";
        }
        else if (role.Equals("DOCENTE", StringComparison.OrdinalIgnoreCase) || role.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
        {
            standardRole = "Teacher";
        }
        else
        {
            standardRole = "Student";
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Username),
            new("user_id", user.Id.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, standardRole),
            new(ClaimTypes.Role, role), // Incluye también el nombre exacto de la tabla roles (ej. DOCENTE, ALUMNO, ADMINISTRADOR)
            new(ClaimTypes.Role, standardRole == "Admin" ? ".admin" : standardRole),
            new("role", standardRole == "Admin" ? ".admin" : standardRole),
            new("grade_level", grade),
            new("avatar_url", avatar),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiration = DateTime.UtcNow.AddDays(7);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiration,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(securityToken);

        return (tokenString, expiration);
    }
}
