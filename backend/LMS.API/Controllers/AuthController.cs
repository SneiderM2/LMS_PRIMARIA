using System.Security.Claims;
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

    public AuthController(LMSDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Registro público de usuarios (Estudiante, Docente o Administrador) desde el portal
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
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
            FechaCreacion = DateTime.UtcNow
        };

        _context.Usuarios.Add(nuevoUsuario);
        await _context.SaveChangesAsync();

        string? gradoNombre = null;

        // Si el usuario es un alumno, registrarlo en la tabla 'alumnos'
        if (targetRoleName == "ALUMNO")
        {
            var rawGrade = request.Grade?.Trim() ?? "1°";
            // Extraer solo la parte del grado (ej. "2° Primaria" -> "2°")
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

        // Cargar navegaciones para respuesta
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
            AvatarUrl = avatarUrl
        };

        return CreatedAtAction(nameof(GetCurrentUser), new LoginResponseDto
        {
            Token = token,
            Expiration = expiration,
            User = userDto
        });
    }

    /// <summary>
    /// Inicia sesión con el Documento/Carnet Escolar y Contraseña.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cleanUsername = request.Id.Trim();
        var user = await _context.Usuarios
            .Include(u => u.Rol)
            .Include(u => u.Alumno)
                .ThenInclude(a => a!.Grado)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername.ToLower() && u.Activo);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Identificación escolar o contraseña incorrecta." });
        }

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
            User = new UserDto
            {
                Id = user.Username,
                InternalId = user.Id,
                FullName = fullName,
                Role = roleStr,
                GradeLevel = gradeName,
                LastLoginDate = DateTime.UtcNow,
                AvatarUrl = avatar
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
            AvatarUrl = avatar
        });
    }
}
