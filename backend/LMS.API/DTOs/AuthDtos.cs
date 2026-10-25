using System.ComponentModel.DataAnnotations;

namespace LMS.API.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "El Documento o Carnet Escolar es requerido")]
    [MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Honeypot: Campo señuelo que los bots autocompletan. Debe llegar siempre vacío.
    /// </summary>
    public string? HoneypotTrap { get; set; }

    /// <summary>
    /// Token emitido por el widget de verificación CAPTCHA.
    /// </summary>
    public string? CaptchaToken { get; set; }

    /// <summary>
    /// Si es true, cierra la sesión activa en otro dispositivo y continúa el login.
    /// </summary>
    public bool ForceLogin { get; set; } = false;
}

public class AcceptDataPolicyDto
{
    public bool Accepted { get; set; }

    /// <summary>Versión de la política que el titular declara aceptar.</summary>
    public string? Version { get; set; }

    public string? GuardianFullName { get; set; }
    public string? GuardianDocument { get; set; }
    public bool GuardianAuthorization { get; set; }
}

public class GoogleLoginDto
{
    [Required]
    public string IdToken { get; set; } = string.Empty;

    public string? HoneypotTrap { get; set; }

    /// <summary>
    /// Si es true, cierra la sesión activa en otro dispositivo y continúa el login.
    /// </summary>
    public bool ForceLogin { get; set; } = false;
}

/// <summary>
/// Respuesta cuando se detecta una sesión activa preexistente (HTTP 409).
/// </summary>
public class ActiveSessionResponseDto
{
    public string Code { get; set; } = "ACTIVE_SESSION";
    public string Message { get; set; } = string.Empty;
    public string? DeviceHint { get; set; }
    public DateTime? SessionStartedAt { get; set; }
}

public class RegisterRequestDto
{
    [Required(ErrorMessage = "El Documento o Carnet Escolar es requerido")]
    [MaxLength(100)]
    [RegularExpression(@"^[A-Za-z0-9._@-]{3,100}$",
        ErrorMessage = "El documento o carnet solo puede contener letras, números, punto, guion, guion bajo y @.")]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es requerido")]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener entre 8 y 64 caracteres, incluyendo mayúscula, minúscula, número y símbolo (@, #, !, etc.)")]
    [MaxLength(64, ErrorMessage = "La contraseña no puede superar 64 caracteres")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Rol solicitado. El registro público solo permite "Student"/"ALUMNO" y, con código de
    /// invitación institucional, "Teacher"/"DOCENTE". Nunca permite administradores.
    /// </summary>
    [Required(ErrorMessage = "El rol es requerido")]
    public string Role { get; set; } = "Student";

    /// <summary>
    /// Grado para estudiantes (ej. "1°", "2°", "3°", "4°", "5°" o "2° Primaria")
    /// </summary>
    public string? Grade { get; set; }

    /// <summary>Código de invitación entregado por la institución para registrar docentes.</summary>
    public string? TeacherInviteCode { get; set; }

    // Autorización del representante legal para menores de edad (Ley 1581 de 2012, art. 7).
    public string? GuardianFullName { get; set; }
    public string? GuardianDocument { get; set; }
    public bool GuardianAuthorization { get; set; }

    /// <summary>Aceptación explícita de la política de tratamiento de datos.</summary>
    public bool AcceptDataPolicy { get; set; }
    public string? DataPolicyVersion { get; set; }

    public string? HoneypotTrap { get; set; }
    public string? CaptchaToken { get; set; }
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public UserDto User { get; set; } = new();
    public bool RequiresPolicyAcceptance { get; set; } = false;
    public string DataPolicyVersion { get; set; } = string.Empty;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public int InternalId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public bool DataPolicyAccepted { get; set; } = false;
    public string? DataPolicyVersion { get; set; }
    public bool RequiresPolicyAcceptance { get; set; } = false;
}

public class ChangePasswordDto
{
    [Required(ErrorMessage = "La contraseña actual es requerida")]
    [MaxLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es requerida")]
    [MinLength(8, ErrorMessage = "La nueva contraseña debe tener entre 8 y 64 caracteres, incluyendo mayúscula, minúscula, número y símbolo (@, #, !, etc.)")]
    [MaxLength(64, ErrorMessage = "La nueva contraseña no puede superar 64 caracteres")]
    public string NewPassword { get; set; } = string.Empty;

    public string? ConfirmNewPassword { get; set; }
}
