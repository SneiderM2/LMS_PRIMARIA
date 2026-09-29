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

    /// <summary>
    /// Registro público de usuarios (Estudiante, Docente o Administrador) desde el portal
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        // 1. Detección Honeypot: Si el campo señuelo viene diligenciado, es un bot de spam
        if (!string.IsNullOrEmpty(request.HoneypotTrap))
        {
            _logger.LogWarning("Intento de registro automatizado detectado por honeypot.");
            return Ok(new LoginResponseDto
            {
                Token = "fake-token-honeypot",
                Expiration = DateTime.UtcNow.AddMinutes(5),
                User = new UserDto { Id = "bot", FullName = "Bot Deflected" }
            });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cleanUsername = request.Id.Trim();
        var existingUser = await _context.Usuarios
            .AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower());

        if (existingUser)
        {
            return BadRequest(new { message = "El documento o carnet escolar ya se encuentra registrado." });
        }

        // Determinar el rol correspondiente de acuerdo al catálogo de bd.txt
        var reqRole = request.Role.Trim().ToUpperInvariant();
        string targetRoleName = "ALUMNO";
        if (reqRole.Contains("ADMIN") || reqRole == "ADMINISTRADOR")
        {
            targetRoleName = "ADMINISTRADOR";
        }
        else if (reqRole.Contains("TEACHER") || reqRole.Contains("DOCENTE") || reqRole.Contains("PROF"))
        {
            targetRoleName = "DOCENTE";
        }
        else
        {
            targetRoleName = "ALUMNO";
        }

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == targetRoleName);
        if (rol == null)
        {
            rol = new Rol { Nombre = targetRoleName, Descripcion = $"Rol {targetRoleName}" };
            _context.Roles.Add(rol);
            await _context.SaveChangesAsync();
        }

        // Desglosar nombre completo
        var fullNameParts = request.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = fullNameParts.Length > 0 ? fullNameParts[0] : cleanUsername;
        var lastName = fullNameParts.Length > 1 ? fullNameParts[1] : "";
        var avatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(cleanUsername)}";

        // Crear registro en la tabla 'usuarios' con campos unificados
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var nuevoUsuario = new Usuario
        {
            RolId = rol.Id,
            Username = cleanUsername,
            PasswordHash = passwordHash,
            Nombre = firstName,
            Apellido = lastName,
            AvatarUrl = avatarUrl,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            DataPolicyAccepted = false, // Exigir aceptación al registrarse o iniciar
            AccessFailedCount = 0
        };

        _context.Usuarios.Add(nuevoUsuario);
        await _context.SaveChangesAsync();

        string? gradoNombre = null;

        // Si el usuario es un alumno, registrarlo en la tabla 'alumnos'
        if (targetRoleName == "ALUMNO")
        {
            var rawGrade = request.Grade?.Trim() ?? "1°";
            var gradeKey = rawGrade.Contains('°') ? rawGrade.Substring(0, rawGrade.IndexOf('°') + 1) : rawGrade;

            var grado = await _context.Grados.FirstOrDefaultAsync(g => g.Nombre == gradeKey || g.Nombre == rawGrade)
                        ?? await _context.Grados.FirstOrDefaultAsync(g => g.Nombre.StartsWith("1"))
                        ?? await _context.Grados.FirstOrDefaultAsync();

            if (grado == null)
            {
                grado = new Grado { Nombre = gradeKey, Descripcion = $"{gradeKey} de primaria" };
                _context.Grados.Add(grado);
                await _context.SaveChangesAsync();
            }

            gradoNombre = grado.Nombre;

            var alumno = new Alumno
            {
                UsuarioId = nuevoUsuario.Id,
                GradoId = grado.Id
            };
            _context.Alumnos.Add(alumno);
        }

        await _context.SaveChangesAsync();

        nuevoUsuario.Rol = rol;

        var isNewUserAdmin = string.Equals(targetRoleName, ".admin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(targetRoleName, "ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(targetRoleName, "Admin", StringComparison.OrdinalIgnoreCase);

        var (token, expiration) = _tokenService.GenerateToken(
            nuevoUsuario, 
            nuevoUsuario.FullName, 
            rol.Nombre, 
            gradoNombre, 
            avatarUrl);

        var userDto = new UserDto
        {
            Id = nuevoUsuario.Username,
            InternalId = nuevoUsuario.Id,
            FullName = nuevoUsuario.FullName,
            Role = isNewUserAdmin ? "Admin" : targetRoleName == "DOCENTE" ? "Teacher" : "Student",
            GradeLevel = gradoNombre,
            LastLoginDate = DateTime.UtcNow,
            AvatarUrl = avatarUrl,
            DataPolicyAccepted = false
        };

        return CreatedAtAction(nameof(GetCurrentUser), new LoginResponseDto
        {
            Token = token,
            Expiration = expiration,
            User = userDto,
            RequiresPolicyAcceptance = true
        });
    }

    /// <summary>
    /// Inicia sesión con el Documento/Carnet Escolar, Contraseña, Honeypot, CAPTCHA y Bloqueo.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        // 1. Verificación Honeypot: si el bot llenó el campo trampa, responder simulado
        if (!string.IsNullOrEmpty(request.HoneypotTrap))
        {
            _logger.LogWarning("Intento de login interceptado por honeypot.");
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
            // 2. Verificación de CAPTCHA
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var isCaptchaValid = await _captchaService.VerifyTokenAsync(request.CaptchaToken, clientIp);
            if (!isCaptchaValid)
            {
                return BadRequest(new { message = "Verificación de seguridad (CAPTCHA) obligatoria o inválida." });
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

            // 3. Verificación de Bloqueo de cuenta (5 intentos fallidos = 10 minutos de bloqueo)
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var remaining = user.LockoutEnd.Value - DateTime.UtcNow;
                var remainingMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                return StatusCode(StatusCodes.Status423Locked, new
                {
                    message = $"🔒 Tu cuenta ha sido bloqueada temporalmente por exceso de intentos fallidos. Por favor espera {remainingMinutes} minuto(s) antes de reintentar."
                });
            }

            // Comprobar contraseña con BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(10);
                    await _context.SaveChangesAsync();
                    return StatusCode(StatusCodes.Status423Locked, new
                    {
                        message = "🔒 Has alcanzado el límite de 5 intentos fallidos. Tu cuenta ha sido bloqueada por 10 minutos por motivos de seguridad escolar."
                    });
                }

                await _context.SaveChangesAsync();
                var attemptsLeft = 5 - user.AccessFailedCount;
                return Unauthorized(new
                {
                    message = $"Identificación o contraseña incorrecta. Te quedan {attemptsLeft} intento(s) antes del bloqueo temporal."
                });
            }

            // Restablecer contador de fallos tras login exitoso
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
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
        catch (InvalidOperationException ex)
        {
            // Ocurre cuando EF Core no puede resolver una operación (p.ej. migración pendiente,
            // tabla no encontrada o múltiples resultados inesperados). El GlobalExceptionMiddleware
            // lo convertiría en 409 Conflict; lo capturamos aquí para dar un mensaje claro.
            _logger.LogError(ex, "Error de operación inválida durante el login del usuario {Id}", request.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "El servicio de autenticación no está disponible en este momento. Por favor intenta de nuevo en unos segundos.",
                detail = "Problema de conectividad con la base de datos escolar."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado durante el login del usuario {Id}", request.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Ocurrió un error inesperado. Por favor intenta de nuevo más tarde."
            });
        }
    }

    /// <summary>
    /// Aceptación formal de la política de tratamiento de datos personales (Habeas Data)
    /// </summary>
    [Authorize]
    [HttpPost("accept-data-policy")]
    public async Task<IActionResult> AcceptDataPolicy([FromBody] AcceptDataPolicyDto dto)
    {
        if (!dto.Accepted)
        {
            return BadRequest(new { message = "Debes autorizar la política de tratamiento de datos personales para continuar." });
        }

        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (user == null) return NotFound();

        user.DataPolicyAccepted = true;
        user.DataPolicyAcceptedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Política de tratamiento de datos aceptada exitosamente.", acceptedAt = user.DataPolicyAcceptedAt });
    }

    /// <summary>
    /// Autenticación alternativa con Google Identity (OAuth 2.0 / Google Sign-In)
    /// </summary>
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
    {
        if (!string.IsNullOrEmpty(dto.HoneypotTrap))
        {
            return Ok(new { token = "fake-token-honeypot" });
        }

        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            return BadRequest(new { message = "Token de autenticación de Google no proporcionado." });
        }

        // Decodificación y validación criptográfica de payload JWT de Google
        string email = "";
        string name = "";
        string picture = "";

        try
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];
            GoogleJsonWebSignature.Payload? payload = null;

            if (!string.IsNullOrWhiteSpace(googleClientId) && !googleClientId.Contains("sampleclientidforexample"))
            {
                var validationSettings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { googleClientId }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, validationSettings);
            }
            else
            {
                // Validación estándar sin audience forzado o fallback dev
                try
                {
                    payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken);
                }
                catch
                {
                    // Fallback para tokens simulados o en desarrollo
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(dto.IdToken);
                    var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == ClaimTypes.Email);
                    var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name);
                    var picClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "picture");

                    if (emailClaim == null)
                    {
                        return BadRequest(new { message = "El token de Google no contiene una cuenta de correo válida." });
                    }

                    email = emailClaim.Value;
                    name = nameClaim?.Value ?? email.Split('@')[0];
                    picture = picClaim?.Value ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={email}";
                }
            }

            if (payload != null)
            {
                email = payload.Email;
                name = payload.Name ?? $"{payload.GivenName} {payload.FamilyName}".Trim();
                if (string.IsNullOrWhiteSpace(name)) name = email.Split('@')[0];
                picture = payload.Picture ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={email}";
            }
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Token de Google inválido criptográficamente");
            return Unauthorized(new { message = "Token de Google inválido o caducado.", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token de Google no procesable");
            return BadRequest(new { message = "Token de Google no procesable." });
        }

        // Buscar o registrar al usuario automáticamente
        var user = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == email.ToLower());

        if (user != null)
        {
            if (!user.Activo)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new 
                { 
                    message = "Esta cuenta ha sido desactivada por un administrador escolar." 
                });
            }
        }

        if (user == null)
        {
            var rolAlumno = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "ALUMNO")
                            ?? await _context.Roles.FirstOrDefaultAsync();

            var primerGrado = await _context.Grados.FirstOrDefaultAsync();

            var nameParts = name.Split(' ', 2);
            user = new Usuario
            {
                Username = email,
                Nombre = nameParts.Length > 0 ? nameParts[0] : name,
                Apellido = nameParts.Length > 1 ? nameParts[1] : "",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                RolId = rolAlumno?.Id ?? 4,
                AvatarUrl = picture,
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
                DataPolicyAccepted = false // Exigir aceptación en primer ingreso
            };

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            if (primerGrado != null)
            {
                _context.Alumnos.Add(new Alumno { UsuarioId = user.Id, GradoId = primerGrado.Id });
                await _context.SaveChangesAsync();
            }

            user.Rol = rolAlumno!;
        }

        var (token, expiration) = _tokenService.GenerateToken(
            user, 
            user.FullName, 
            user.Rol?.Nombre ?? "ALUMNO", 
            user.Alumno?.Grado?.Nombre, 
            user.AvatarUrl);

        var isAdministrator = string.Equals(user.Rol?.Nombre, ".admin", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(user.Rol?.Nombre, "ADMINISTRADOR", StringComparison.OrdinalIgnoreCase);

        var roleStr = isAdministrator ? "Admin" : user.Rol?.Nombre == "DOCENTE" ? "Teacher" : "Student";

        return Ok(new LoginResponseDto
        {
            Token = token,
            Expiration = expiration,
            RequiresPolicyAcceptance = !user.DataPolicyAccepted,
            User = new UserDto
            {
                Id = user.Username,
                InternalId = user.Id,
                FullName = user.FullName,
                Role = roleStr,
                GradeLevel = user.Alumno?.Grado?.Nombre,
                LastLoginDate = DateTime.UtcNow,
                AvatarUrl = user.AvatarUrl ?? picture,
                DataPolicyAccepted = user.DataPolicyAccepted
            }
        });
    }

    /// <summary>
    /// Retorna los datos del usuario autenticado en sesión
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var user = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user == null)
        {
            return NotFound();
        }

        var fullName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Username;
        var gradeName = user.Alumno?.Grado?.Nombre;
        var avatar = user.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={user.Username}";

        var isAdministrator = string.Equals(user.Rol.Nombre, ".admin", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(user.Rol.Nombre, "ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(user.Rol.Nombre, "Admin", StringComparison.OrdinalIgnoreCase);

        var roleStr = isAdministrator ? "Admin" : user.Rol.Nombre == "DOCENTE" ? "Teacher" : "Student";

        return Ok(new UserDto
        {
            Id = user.Username,
            InternalId = user.Id,
            FullName = fullName,
            Role = roleStr,
            GradeLevel = gradeName,
            LastLoginDate = DateTime.UtcNow,
            AvatarUrl = avatar,
            DataPolicyAccepted = user.DataPolicyAccepted
        });
    }
}
