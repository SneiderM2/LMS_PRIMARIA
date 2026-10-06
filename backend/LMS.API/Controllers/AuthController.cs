using System.Security.Claims;
using Google.Apis.Auth;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly LMSDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ICaptchaService _captchaService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        LMSDbContext context, 
        ITokenService tokenService,
        ICaptchaService captchaService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _captchaService = captchaService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("diagnostic")]
    public async Task<IActionResult> Diagnostic()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            var totalUsers = await _context.Usuarios.CountAsync();
            var roles = await _context.Roles.Select(r => r.Nombre).ToListAsync();
            var conn = _context.Database.GetDbConnection();
            var host = conn.DataSource;

            return Ok(new
            {
                status = "OK",
                databaseCanConnect = canConnect,
                totalUsers = totalUsers,
                roles = roles,
                databaseHost = host,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "DATABASE_ERROR",
                errorType = ex.GetType().FullName,
                errorMessage = ex.Message,
                innerError = ex.InnerException?.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!string.IsNullOrEmpty(request.HoneypotTrap))
        {
            return Ok(new LoginResponseDto
            {
                Token = "fake-token-honeypot",
                Expiration = DateTime.UtcNow.AddMinutes(5),
                User = new UserDto { Id = "bot", FullName = "Bot Intercepted" }
            });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var isCaptchaValid = await _captchaService.VerifyTokenAsync(request.CaptchaToken, clientIp);
            if (!isCaptchaValid)
            {
                return BadRequest(new { message = "La verificación de reCAPTCHA falló o el token ha expirado." });
            }

            var cleanUsername = request.Id.Trim();
            var user = await _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername.ToLower() && u.Activo);

            if (user == null)
            {
                return Unauthorized(new { message = "Identificación escolar o contraseña incorrecta." });
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var remaining = user.LockoutEnd.Value - DateTime.UtcNow;
                var remainingMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                return StatusCode(StatusCodes.Status423Locked, new
                {
                    message = $"Tu cuenta está bloqueada temporalmente. Espera {remainingMinutes} minuto(s)."
                });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(10);
                    await _context.SaveChangesAsync();
                    return StatusCode(StatusCodes.Status423Locked, new
                    {
                        message = "Has alcanzado el límite de 5 intentos. Tu cuenta ha sido bloqueada por 10 minutos."
                    });
                }

                await _context.SaveChangesAsync();
                return Unauthorized(new
                {
                    message = $"Identificación o contraseña incorrecta. Te quedan {5 - user.AccessFailedCount} intento(s)."
                });
            }

            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.SessionToken = Guid.NewGuid().ToString("N");
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var fullName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username;
            var gradeName = user.Alumno?.Grado?.Nombre;
            var avatar = user.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={user.Username}";

            var (token, expiration) = _tokenService.GenerateToken(
                user, 
                fullName, 
                user.Rol.Nombre, 
                gradeName, 
                avatar);

            var isAdministrator = string.Equals(user.Rol.Nombre, ".admin", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(user.Rol.Nombre, "ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(user.Rol.Nombre, "Admin", StringComparison.OrdinalIgnoreCase);

            var roleStr = isAdministrator ? "Admin" : user.Rol.Nombre == "DOCENTE" ? "Teacher" : "Student";

            return Ok(new LoginResponseDto
            {
                Token = token,
                Expiration = expiration,
                RequiresPolicyAcceptance = !user.DataPolicyAccepted,
                User = new UserDto
                {
                    Id = user.Username,
                    InternalId = user.Id,
                    FullName = fullName,
                    Role = roleStr,
                    GradeLevel = gradeName,
                    LastLoginDate = DateTime.UtcNow,
                    AvatarUrl = avatar,
                    DataPolicyAccepted = user.DataPolicyAccepted
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en login");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error interno del servidor.", detail = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(username))
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
            if (user != null)
            {
                user.SessionToken = null;
                user.LastLoginAt = null;
                await _context.SaveChangesAsync();
            }
        }
        return Ok(new { message = "Sesión cerrada correctamente." });
    }

    [Authorize]
    [HttpGet("validate-session")]
    public async Task<IActionResult> ValidateSession()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tokenSession = User.FindFirst("session_token")?.Value;

        if (string.IsNullOrEmpty(username))
            return Unauthorized(new { code = "DUPLICATE_SESSION", message = "Sesión inválida." });

        var dbSessionToken = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Username.ToLower() == username.ToLower())
            .Select(u => u.SessionToken)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(dbSessionToken) || !string.Equals(dbSessionToken, tokenSession, StringComparison.Ordinal))
        {
            return Unauthorized(new
            {
                code = "DUPLICATE_SESSION",
                message = "Tu sesión se cerró porque se inició sesión en otro dispositivo."
            });
        }

        return Ok(new { valid = true });
    }
}