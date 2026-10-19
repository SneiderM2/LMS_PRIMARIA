using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LMS.API.Entities;
using Microsoft.IdentityModel.Tokens;

namespace LMS.API.Services;

public class TokenService : ITokenService
{
    private const int DefaultExpirationMinutes = 480; // 8 horas
    private const int MaxExpirationMinutes = 1440;     // 24 horas
    private const int MinSecretLength = 48;

    private readonly IConfiguration _configuration;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;

        _secretKey = configuration["Jwt:SecretKey"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_secretKey) || _secretKey.Length < MinSecretLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SecretKey es obligatorio y debe tener al menos {MinSecretLength} caracteres. " +
                "Configúrelo mediante variable de entorno Jwt__SecretKey.");
        }

        _issuer = configuration["Jwt:Issuer"] ?? "LMS.API";
        _audience = configuration["Jwt:Audience"] ?? "LMS.Client";

        var configuredMinutes = configuration.GetValue<int?>("Jwt:ExpirationMinutes") ?? DefaultExpirationMinutes;
        _expirationMinutes = configuredMinutes is < 5 or > MaxExpirationMinutes
            ? DefaultExpirationMinutes
            : configuredMinutes;
    }

    public (string Token, DateTime Expiration) GenerateToken(
        Usuario user,
        string? fullName = null,
        string? roleName = null,
        string? gradeName = null,
        string? avatarUrl = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var name = fullName ?? user.FullName;
        if (string.IsNullOrWhiteSpace(name)) name = user.Username;

        var role = roleName ?? user.Rol?.Nombre ?? "ALUMNO";
        var avatar = avatarUrl ?? user.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(user.Username)}";
        var grade = gradeName ?? user.Alumno?.Grado?.Nombre ?? string.Empty;

        var standardRole = NormalizeRole(role);
        var issuedAt = DateTime.UtcNow;
        var expiration = issuedAt.AddMinutes(_expirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Username),
            new("user_id", user.Id.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, standardRole),
            new("role_source", role),
            new("grade_level", grade),
            new("avatar_url", avatar),
            new("session_token", user.SessionToken ?? string.Empty),
            new("token_type", "access"),
            new("amr", "pwd"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAt).ToString(),
                ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = issuedAt,
            IssuedAt = issuedAt,
            Expires = expiration,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(securityToken);

        return (tokenString, expiration);
    }

    /// <summary>
    /// Normaliza los nombres de rol históricos (español/inglés) a los tres roles canónicos.
    /// </summary>
    public static string NormalizeRole(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return "Student";
        }

        if (roleName.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
            || roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || roleName.Equals(".admin", StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        if (roleName.Equals("DOCENTE", StringComparison.OrdinalIgnoreCase)
            || roleName.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
        {
            return "Teacher";
        }

        return "Student";
    }
}
