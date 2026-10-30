using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using LMS.API.Configuration;
using LMS.API.Data;
using LMS.API.DTOs;
using LMS.API.Entities;
using LMS.API.Middleware;
using LMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly LMSDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly ICaptchaService _captchaService;
    private readonly ConsentService _consent;
    private readonly IConfiguration _configuration;
    private readonly PrivacyOptions _privacy;
    private readonly RegistrationOptions _registration;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        LMSDbContext context,
        ITokenService tokenService,
        ICaptchaService captchaService,
        ConsentService consent,
        IConfiguration configuration,
        IOptions<PrivacyOptions> privacy,
        IOptions<RegistrationOptions> registration,
        IHostEnvironment env,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _captchaService = captchaService;
        _consent = consent;
        _configuration = configuration;
        _privacy = privacy.Value;
        _registration = registration.Value;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Registro público. Solo permite estudiantes (con autorización de acudiente) y docentes
    /// con código de invitación institucional. Nunca crea administradores.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        // Honeypot: respuesta genérica, sin emitir sesión.
        if (!string.IsNullOrEmpty(request.HoneypotTrap))
        {
            _logger.LogWarning("Registro automatizado detectado por honeypot.");
            return BadRequest(new { message = "No fue posible completar el registro. Verifica los datos e intenta de nuevo." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var clientIp = ClientIp;
            if (!await _captchaService.VerifyTokenAsync(request.CaptchaToken, clientIp))
            {
                return BadRequest(new { message = "La verificación de seguridad (reCAPTCHA) falló o expiró. Inténtalo de nuevo." });
            }

            var targetRoleName = ResolveSelfRegistrationRole(request.Role);
            if (targetRoleName == null)
            {
                return BadRequest(new { message = "El registro público no permite crear cuentas administrativas." });
            }

            if (targetRoleName == "DOCENTE" && !IsValidTeacherInviteCode(request.TeacherInviteCode))
            {
                return BadRequest(new
                {
                    message = "El registro de docentes requiere un código de invitación institucional válido."
                });
            }

            if (targetRoleName == "ALUMNO" && !_registration.AllowPublicStudentRegistration)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "El registro público de estudiantes está deshabilitado. Contacta a tu institución."
                });
            }

            // Consentimiento informado (Ley 1581 de 2012, arts. 4 y 9).
            if (!request.AcceptDataPolicy)
            {
                return BadRequest(new
                {
                    code = "POLICY_REQUIRED",
                    message = "Debes aceptar la política de tratamiento de datos personales para registrarte."
                });
            }

            if (!string.IsNullOrWhiteSpace(request.DataPolicyVersion)
                && !string.Equals(request.DataPolicyVersion, _privacy.PolicyVersion, StringComparison.Ordinal))
            {
                return Conflict(new
                {
                    code = "POLICY_VERSION_CHANGED",
                    message = "La política de tratamiento cambió. Recarga la página y vuelve a leerla.",
                    currentVersion = _privacy.PolicyVersion
                });
            }

            var guardianRequired = _privacy.RequireGuardianConsentForMinors && targetRoleName == "ALUMNO";
            if (guardianRequired)
            {
                var guardianValidation = ConsentService.ValidateGuardianConsent(
                    request.GuardianAuthorization,
                    request.GuardianFullName,
                    request.GuardianDocument);

                if (!guardianValidation.IsValid)
                {
                    return BadRequest(new
                    {
                        code = "GUARDIAN_CONSENT_REQUIRED",
                        message = guardianValidation.Message
                    });
                }
            }

            var passwordCheck = PasswordPolicy.Validate(request.Password, request.Id, request.FullName);
            if (!passwordCheck.IsValid)
            {
                return BadRequest(new { code = "WEAK_PASSWORD", message = passwordCheck.Message });
            }

            var cleanUsername = request.Id.Trim();
            var exists = await _context.Usuarios
                .AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower());

            if (exists)
            {
                return BadRequest(new { message = "El documento o carnet escolar ya se encuentra registrado." });
            }

            var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == targetRoleName);
            if (rol == null)
            {
                rol = new Rol { Nombre = targetRoleName, Descripcion = $"Rol {targetRoleName}" };
                _context.Roles.Add(rol);
                await _context.SaveChangesAsync();
            }

            // Validar el grado antes de crear la cuenta para no dejar registros parciales.
            Grado? grado = null;
            string? gradoNombre = null;
            if (targetRoleName == "ALUMNO")
            {
                var rawGrade = request.Grade?.Trim() ?? "1°";
                var gradeKey = rawGrade.Contains('°') ? rawGrade[..(rawGrade.IndexOf('°') + 1)] : rawGrade;
                var grados = await _context.Grados.ToListAsync();
                grado = grados.FirstOrDefault(g => g.Nombre == gradeKey || g.Nombre == rawGrade);

                if (grado == null)
                {
                    return BadRequest(new
                    {
                        code = "INVALID_GRADE",
                        message = $"El grado '{rawGrade}' no existe. Selecciona uno de los grados registrados.",
                        validGrades = grados.Select(g => g.Nombre).OrderBy(g => g).ToArray()
                    });
                }

                gradoNombre = grado.Nombre;
            }

            var fullNameParts = request.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var firstName = fullNameParts.Length > 0 ? fullNameParts[0] : cleanUsername;
            var lastName = fullNameParts.Length > 1 ? fullNameParts[1] : string.Empty;
            var avatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(cleanUsername)}";

            var nuevoUsuario = new Usuario
            {
                RolId = rol.Id,
                Username = cleanUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                Nombre = firstName,
                Apellido = lastName,
                AvatarUrl = avatarUrl,
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
                DataPolicyAccepted = true,
                DataPolicyAcceptedAt = DateTime.UtcNow,
                DataPolicyVersion = _privacy.PolicyVersion,
                AccessFailedCount = 0,
                SessionToken = Guid.NewGuid().ToString("N")
            };

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            // La evidencia se firma una vez asignado el Id interno.
            nuevoUsuario.DataPolicyEvidence = _consent.BuildEvidence(
                nuevoUsuario,
                "registration",
                clientIp,
                Request.Headers.UserAgent.ToString(),
                request.GuardianFullName,
                request.GuardianDocument);

            if (targetRoleName == "ALUMNO" && grado != null)
            {
                _context.Alumnos.Add(new Alumno
                {
                    UsuarioId = nuevoUsuario.Id,
                    GradoId = grado.Id
                });

                var consentRecord = _consent.CreateHabeasDataRecord(
                    alumnoId: nuevoUsuario.Id,
                    usuarioId: nuevoUsuario.Id,
                    guardianFullName: request.GuardianFullName,
                    guardianDocument: request.GuardianDocument,
                    tipoDocumento: request.GuardianDocumentType,
                    autorizaServicioEducativo: request.AutorizaServicioEducativo && request.GuardianAuthorization,
                    autorizaDatosOpcionales: request.AutorizaDatosOpcionales,
                    autorizaTransmisionInternacional: request.AutorizaTransmisionInternacional,
                    ip: clientIp,
                    userAgent: Request.Headers.UserAgent.ToString());

                _context.ConsentimientosHabeasData.Add(consentRecord);
            }

            await _context.SaveChangesAsync();
            nuevoUsuario.Rol = rol;

            var (token, expiration) = _tokenService.GenerateToken(
                nuevoUsuario,
                nuevoUsuario.FullName,
                rol.Nombre,
                gradoNombre,
                avatarUrl);

            await AuditAsync(nuevoUsuario, "Autenticación: Registro de cuenta",
                $"Rol: {targetRoleName}; grado: {gradoNombre ?? "N/A"}; política: {_privacy.PolicyVersion}.");

            var userDto = new UserDto
            {
                Id = nuevoUsuario.Username,
                InternalId = nuevoUsuario.Id,
                FullName = nuevoUsuario.FullName,
                Role = rol.Nombre == "DOCENTE" ? "Teacher" : "Student",
                GradeLevel = gradoNombre,
                LastLoginDate = DateTime.UtcNow,
                AvatarUrl = avatarUrl,
                DataPolicyAccepted = true,
                DataPolicyVersion = _privacy.PolicyVersion,
                RequiresPolicyAcceptance = false
            };

            return CreatedAtAction(nameof(GetCurrentUser), new LoginResponseDto
            {
                Token = token,
                Expiration = expiration,
                User = userDto,
                RequiresPolicyAcceptance = false,
                DataPolicyVersion = _privacy.PolicyVersion
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar usuario.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "No fue posible registrar la cuenta en este momento. Intenta de nuevo en unos segundos."
            });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!string.IsNullOrEmpty(request.HoneypotTrap))
        {
            return Unauthorized(new { success = false, message = "Identificación o contraseña incorrecta." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Datos de inicio de sesión inválidos.", errors = ModelState });
        }

        try
        {
            var clientIp = ClientIp;
            bool captchaValid = false;
            try
            {
                captchaValid = await _captchaService.VerifyTokenAsync(request.CaptchaToken, clientIp);
            }
            catch (Exception exCaptcha)
            {
                _logger.LogWarning(exCaptcha, "Error verificando captcha durante login para {Username}", request.Id);
                return BadRequest(new
                {
                    success = false,
                    message = "No fue posible verificar la seguridad (reCAPTCHA) por problemas de conectividad. Inténtalo de nuevo."
                });
            }

            if (!captchaValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La verificación de seguridad (reCAPTCHA) falló o expiró."
                });
            }

            var cleanUsername = request.Id?.Trim() ?? string.Empty;
            var user = await _context.Usuarios
                .Include(u => u.Rol)
                .Include(u => u.Alumno)
                    .ThenInclude(a => a!.Grado)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername.ToLower());

            // Respuesta uniforme: no se revela si el usuario existe o está inactivo.
            if (user == null || !user.Activo)
            {
                await AuditAsync(user, "Autenticación: Intento de login fallido", $"Identificador inexistente o inactivo: {cleanUsername}", "WARNING");
                return Unauthorized(new { success = false, message = "Identificación o contraseña incorrecta." });
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var minutes = Math.Max(1, (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes));
                await AuditAsync(user, "Autenticación: Login bloqueado por intentos fallidos", null, "WARNING");
                return StatusCode(StatusCodes.Status423Locked, new
                {
                    success = false,
                    message = $"La cuenta está bloqueada temporalmente por seguridad. Intenta de nuevo en {minutes} minuto(s)."
                });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.AccessFailedCount++;

                if (user.AccessFailedCount >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                    user.AccessFailedCount = 0;
                    await _context.SaveChangesAsync();
                    await AuditAsync(user, "Autenticación: Cuenta bloqueada por fuerza bruta",
                        $"Bloqueo de {LockoutDuration.TotalMinutes} minutos.", "WARNING");

                    return StatusCode(StatusCodes.Status423Locked, new
                    {
                        success = false,
                        message = "Se alcanzó el límite de intentos. La cuenta estará bloqueada temporalmente por 15 minutos."
                    });
                }

                await _context.SaveChangesAsync();
                await AuditAsync(user, "Autenticación: Contraseña incorrecta",
                    $"Intento fallido {user.AccessFailedCount} de {MaxFailedAttempts}.", "WARNING");

                return Unauthorized(new { success = false, message = "Identificación o contraseña incorrecta." });
            }

            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.SessionToken = Guid.NewGuid().ToString("N");
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var fullName = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
            var gradeName = user.Alumno?.Grado?.Nombre;
            var avatar = user.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(user.Username)}";

            var roleName = user.Rol?.Nombre ?? "ALUMNO";
            var (token, expiration) = _tokenService.GenerateToken(user, fullName, roleName, gradeName, avatar);

            var roleStr = TokenService.NormalizeRole(roleName);
            var hasRevokedMinorConsent = roleStr == "Student" && await _context.ConsentimientosHabeasData
                .AnyAsync(c => c.AlumnoId == user.Id && c.FechaRevocatoriaUtc != null)
                && !await _context.ConsentimientosHabeasData
                .AnyAsync(c => c.AlumnoId == user.Id && c.AutorizaServicioEducativo && c.FechaRevocatoriaUtc == null);
            var requiresPolicy = !_consent.IsCurrent(user) || hasRevokedMinorConsent;

            await AuditAsync(user, "Autenticación: Inicio de sesión exitoso",
                $"Rol: {roleName}; requiere aceptación de política: {requiresPolicy}.");

            return Ok(new LoginResponseDto
            {
                Token = token,
                Expiration = expiration,
                RequiresPolicyAcceptance = requiresPolicy,
                DataPolicyVersion = _privacy.PolicyVersion,
                User = new UserDto
                {
                    Id = user.Username,
                    InternalId = user.Id,
                    FullName = fullName,
                    Role = roleStr,
                    GradeLevel = gradeName,
                    LastLoginDate = DateTime.UtcNow,
                    AvatarUrl = avatar,
                    DataPolicyAccepted = user.DataPolicyAccepted && !hasRevokedMinorConsent,
                    DataPolicyVersion = user.DataPolicyVersion,
                    RequiresPolicyAcceptance = requiresPolicy,
                    HasActiveMinorConsent = !requiresPolicy
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Acceso no autorizado durante login para {Username}", request.Id);
            return Unauthorized(new
            {
                success = false,
                message = ex.Message ?? "Identificación o contraseña incorrecta."
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Parámetros inválidos durante login para {Username}", request.Id);
            return BadRequest(new
            {
                success = false,
                message = ex.Message ?? "Los datos de autenticación son inválidos."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Operación o claims inválidos durante login para {Username}", request.Id);
            return BadRequest(new
            {
                success = false,
                message = "Error al procesar las credenciales o claims de la sesión.",
                detail = _env.IsDevelopment() ? ex.Message : null
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Fallo de conexión externa o Supabase durante login para {Username}", request.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                message = "El servicio de autenticación no está disponible en este momento. Intenta de nuevo en unos momentos.",
                detail = _env.IsDevelopment() ? ex.Message : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en login para {Username}", request.Id);
            await FileLogger.ExceptionAsync(ex, "POST /api/auth/login");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Error interno del servidor al procesar el inicio de sesión.",
                detail = _env.IsDevelopment() ? ex.Message : null
            });
        }
    }

    /// <summary>
    /// Aceptación de la política con evidencia (compatibilidad con el flujo anterior).
    /// </summary>
    [Authorize]
    [HttpPost("accept-data-policy")]
    public async Task<IActionResult> AcceptDataPolicy([FromBody] AcceptDataPolicyDto dto)
    {
        if (!dto.Accepted)
        {
            return BadRequest(new { message = "Debes aceptar la política de tratamiento de datos para continuar." });
        }

        if (!string.IsNullOrWhiteSpace(dto.Version)
            && !string.Equals(dto.Version, _privacy.PolicyVersion, StringComparison.Ordinal))
        {
            return Conflict(new { message = "La versión de la política cambió.", currentVersion = _privacy.PolicyVersion });
        }

        var user = await GetCurrentUserAsync(includeStudent: true);
        if (user == null)
        {
            return Unauthorized();
        }

        var isStudent = user.Alumno != null
                        || string.Equals(user.Rol?.Nombre, "ALUMNO", StringComparison.OrdinalIgnoreCase);

        if (_privacy.RequireGuardianConsentForMinors && isStudent)
        {
            var guardianValidation = ConsentService.ValidateGuardianConsent(
                dto.GuardianAuthorization,
                dto.GuardianFullName,
                dto.GuardianDocument);

            if (!guardianValidation.IsValid)
            {
                return BadRequest(new
                {
                    code = "GUARDIAN_CONSENT_REQUIRED",
                    message = guardianValidation.Message
                });
            }
        }

        user.DataPolicyAccepted = true;
        user.DataPolicyAcceptedAt = DateTime.UtcNow;
        user.DataPolicyVersion = _privacy.PolicyVersion;
        user.DataPolicyEvidence = _consent.BuildEvidence(
            user,
            "login-modal",
            ClientIp,
            Request.Headers.UserAgent.ToString(),
            dto.GuardianFullName,
            dto.GuardianDocument);

        if (isStudent && user.Alumno != null)
        {
            var previousConsents = await _context.ConsentimientosHabeasData
                .Where(c => c.AlumnoId == user.Alumno.UsuarioId && c.FechaRevocatoriaUtc == null)
                .ToListAsync();

            foreach (var prev in previousConsents)
            {
                prev.FechaRevocatoriaUtc = DateTime.UtcNow;
            }

            var consentRecord = _consent.CreateHabeasDataRecord(
                alumnoId: user.Alumno.UsuarioId,
                usuarioId: user.Id,
                guardianFullName: dto.GuardianFullName,
                guardianDocument: dto.GuardianDocument,
                tipoDocumento: dto.GuardianDocumentType,
                autorizaServicioEducativo: dto.AutorizaServicioEducativo,
                autorizaDatosOpcionales: dto.AutorizaDatosOpcionales,
                autorizaTransmisionInternacional: dto.AutorizaTransmisionInternacional,
                ip: ClientIp,
                userAgent: Request.Headers.UserAgent.ToString());

            _context.ConsentimientosHabeasData.Add(consentRecord);
        }

        await _context.SaveChangesAsync();

        await AuditAsync(user, "Privacidad: Aceptación de política de tratamiento de datos",
            $"Versión {_privacy.PolicyVersion}.");

        return Ok(new
        {
            message = "Política de tratamiento de datos aceptada exitosamente.",
            acceptedAt = user.DataPolicyAcceptedAt,
            version = user.DataPolicyVersion
        });
    }

    /// <summary>
    /// Autenticación con Google Identity. Exige validación criptográfica real del ID token
    /// y una cuenta institucional previamente registrada.
    /// </summary>
    [HttpPost("google-login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
    {
        if (!string.IsNullOrEmpty(dto.HoneypotTrap))
        {
            return Unauthorized(new { message = "No fue posible iniciar sesión con Google." });
        }

        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            return BadRequest(new { message = "Token de autenticación de Google no proporcionado." });
        }

        var googleClientId = _configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(googleClientId) || googleClientId.Contains("sampleclientidforexample", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Intento de login con Google sin ClientId configurado.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "El inicio de sesión con Google no está disponible en este momento."
            });
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { googleClientId }
            });
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "ID token de Google inválido.");
            return Unauthorized(new { message = "Token de Google inválido o caducado." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token de Google no procesable.");
            return BadRequest(new { message = "Token de Google no procesable." });
        }

        if (string.IsNullOrWhiteSpace(payload.Email) || payload.EmailVerified != true)
        {
            return Unauthorized(new { message = "La cuenta de Google no tiene un correo verificado." });
        }

        var email = payload.Email.Trim();
        var user = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == email.ToLower());

        if (user == null)
        {
            await AuditAsync(null, "Autenticación: Login Google con cuenta no registrada", email, "WARNING");
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "ACCOUNT_NOT_REGISTERED",
                message = "No existe una cuenta institucional asociada a este correo. Regístrate con la autorización de tu acudiente o contacta a la institución."
            });
        }

        if (!user.Activo)
        {
            await AuditAsync(user, "Autenticación: Login Google de cuenta inactiva", null, "WARNING");
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Esta cuenta ha sido desactivada por un administrador escolar."
            });
        }

        user.SessionToken = Guid.NewGuid().ToString("N");
        user.LastLoginAt = DateTime.UtcNow;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _context.SaveChangesAsync();

        var (token, expiration) = _tokenService.GenerateToken(
            user,
            user.FullName,
            user.Rol?.Nombre ?? "ALUMNO",
            user.Alumno?.Grado?.Nombre,
            user.AvatarUrl);

        var roleStr = TokenService.NormalizeRole(user.Rol?.Nombre);
        var requiresPolicy = !_consent.IsCurrent(user);

        await AuditAsync(user, "Autenticación: Inicio de sesión con Google",
            $"Correo verificado; requiere política: {requiresPolicy}.");

        return Ok(new LoginResponseDto
        {
            Token = token,
            Expiration = expiration,
            RequiresPolicyAcceptance = requiresPolicy,
            DataPolicyVersion = _privacy.PolicyVersion,
            User = new UserDto
            {
                Id = user.Username,
                InternalId = user.Id,
                FullName = user.FullName,
                Role = roleStr,
                GradeLevel = user.Alumno?.Grado?.Nombre,
                LastLoginDate = DateTime.UtcNow,
                AvatarUrl = user.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(user.Username)}",
                DataPolicyAccepted = user.DataPolicyAccepted,
                DataPolicyVersion = user.DataPolicyVersion,
                RequiresPolicyAcceptance = requiresPolicy
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await GetCurrentUserAsync(includeStudent: true);
        if (user == null)
        {
            return Unauthorized();
        }

        var roleStr = TokenService.NormalizeRole(user.Rol?.Nombre);
        var hasRevokedMinorConsent = roleStr == "Student" && await _context.ConsentimientosHabeasData
            .AnyAsync(c => c.AlumnoId == user.Id && c.FechaRevocatoriaUtc != null)
            && !await _context.ConsentimientosHabeasData
            .AnyAsync(c => c.AlumnoId == user.Id && c.AutorizaServicioEducativo && c.FechaRevocatoriaUtc == null);
        var requiresPolicy = !_consent.IsCurrent(user) || hasRevokedMinorConsent;

        return Ok(new UserDto
        {
            Id = user.Username,
            InternalId = user.Id,
            FullName = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName,
            Role = roleStr,
            GradeLevel = user.Alumno?.Grado?.Nombre,
            LastLoginDate = user.LastLoginAt,
            AvatarUrl = user.AvatarUrl ?? $"https://api.dicebear.com/7.x/bottts/svg?seed={Uri.EscapeDataString(user.Username)}",
            DataPolicyAccepted = user.DataPolicyAccepted && !hasRevokedMinorConsent,
            DataPolicyVersion = user.DataPolicyVersion,
            RequiresPolicyAcceptance = requiresPolicy,
            HasActiveMinorConsent = !requiresPolicy
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await GetCurrentUserAsync(includeStudent: false);
        if (user != null)
        {
            user.SessionToken = null;
            user.LastLoginAt = null;
            await _context.SaveChangesAsync();
            await AuditAsync(user, "Autenticación: Cierre de sesión");
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
        {
            return Unauthorized(new { code = "DUPLICATE_SESSION", message = "Sesión inválida." });
        }

        var dbSessionToken = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Username.ToLower() == username.ToLower() && u.Activo)
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

    /// <summary>
    /// Permite al usuario autenticado cambiar su contraseña validando la contraseña actual,
    /// aplicando las reglas estrictas de normalización (mínimo 8 caracteres, mayúscula,
    /// minúscula, número y carácter especial) y almacenando con BCrypt (workFactor: 12).
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await GetCurrentUserAsync(includeStudent: true);
        if (user == null || !user.Activo)
        {
            return Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            await AuditAsync(user, "Autenticación: Intento fallido de cambio de contraseña",
                "La contraseña actual proporcionada no coincide.", "WARNING");
            return BadRequest(new
            {
                code = "INVALID_CURRENT_PASSWORD",
                message = "La contraseña actual ingresada no es correcta."
            });
        }

        if (!string.IsNullOrWhiteSpace(dto.ConfirmNewPassword)
            && !string.Equals(dto.NewPassword, dto.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                code = "PASSWORD_MISMATCH",
                message = "La nueva contraseña y su confirmación no coinciden."
            });
        }

        if (string.Equals(dto.CurrentPassword, dto.NewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                code = "SAME_PASSWORD",
                message = "La nueva contraseña debe ser diferente a la contraseña actual."
            });
        }

        var passwordCheck = PasswordPolicy.Validate(dto.NewPassword, user.Username, user.FullName);
        if (!passwordCheck.IsValid)
        {
            return BadRequest(new
            {
                code = "WEAK_PASSWORD",
                message = passwordCheck.Message
            });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, workFactor: 12);
        // Rotar SessionToken para invalidar inmediatamente sesiones previas o comprometidas en otros dispositivos.
        user.SessionToken = Guid.NewGuid().ToString("N");
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _context.SaveChangesAsync();

        var (newToken, newExpiration) = _tokenService.GenerateToken(
            user,
            user.FullName,
            user.Rol?.Nombre ?? "ALUMNO",
            user.Alumno?.Grado?.Nombre,
            user.AvatarUrl);

        await AuditAsync(user, "Autenticación: Cambio de contraseña exitoso",
            "Contraseña actualizada cumpliendo política estricta, hash BCrypt (workFactor 12) y rotación de sesión.");

        return Ok(new
        {
            message = "Tu contraseña ha sido actualizada exitosamente.",
            token = newToken,
            expiration = newExpiration
        });
    }

    /// <summary>
    /// Endpoint de solicitud de recuperación de contraseña protegido por RateLimiter ("auth")
    /// y respuesta uniforme contra enumeración de cuentas de menores.
    /// </summary>
    [HttpPost("recover-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RecoverPassword([FromBody] LoginRequestDto? dto)
    {
        var requestedId = dto?.Id?.Trim() ?? "N/A";
        await AuditAsync(null, "Autenticación: Solicitud de recuperación de contraseña",
            $"Solicitud registrada para identificador: {ConsentService.MaskDocument(requestedId)}", "AUDIT");

        return Ok(new
        {
            message = "Si la cuenta existe, el docente titular o el administrador institucional recibirá la solicitud de restablecimiento verificando la identidad del acudiente."
        });
    }

    private async Task<Usuario?> GetCurrentUserAsync(bool includeStudent)
    {
        var username = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var query = _context.Usuarios.Include(u => u.Rol).AsQueryable();

        if (includeStudent)
        {
            query = query
                .Include(u => u.Alumno)
                    .ThenInclude(a => a!.Grado);
        }

        return await query.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// Determina el rol permitido en auto-registro. Devuelve null para roles administrativos.
    /// </summary>
    private static string? ResolveSelfRegistrationRole(string? requestedRole)
    {
        var role = (requestedRole ?? "Student").Trim().ToUpperInvariant();

        if (role.Contains("ADMIN"))
        {
            return null;
        }

        if (role.Contains("TEACHER") || role.Contains("DOCENTE") || role.Contains("PROF"))
        {
            return "DOCENTE";
        }

        return "ALUMNO";
    }

    private bool IsValidTeacherInviteCode(string? providedCode)
    {
        var expected = _registration.TeacherInviteCode;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(providedCode))
        {
            return false;
        }

        var provided = Encoding.UTF8.GetBytes(providedCode.Trim());
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return provided.Length == expectedBytes.Length
               && CryptographicOperations.FixedTimeEquals(provided, expectedBytes);
    }

    private async Task AuditAsync(Usuario? user, string action, string? details = null, string level = "AUDIT")
    {
        try
        {
            RequestLoggingMiddleware.SetAuditContext(
                HttpContext,
                action: action,
                details: details,
                userId: user?.Id.ToString(),
                userEmail: user?.Username,
                userRole: user?.Rol?.Nombre);

            await FileLogger.LogAsync(level, $"{action} — Usuario: {user?.Username ?? "Anónimo"} ({user?.FullName ?? "-"}) | {details ?? "Sin detalles adicionales"}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible registrar la auditoría de autenticación.");
        }
    }
}
