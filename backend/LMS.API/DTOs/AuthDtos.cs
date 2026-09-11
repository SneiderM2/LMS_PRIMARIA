using System.ComponentModel.DataAnnotations;

namespace LMS.API.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "El Documento o Carnet Escolar es requerido")]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    [Required(ErrorMessage = "El Documento o Carnet Escolar es requerido")]
    [MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es requerido")]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(4, ErrorMessage = "La contraseña debe tener al menos 4 caracteres")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Rol: "Student", "Teacher", "Admin" o "ALUMNO", "DOCENTE", "ADMINISTRADOR"
    /// </summary>
    [Required(ErrorMessage = "El rol es requerido")]
    public string Role { get; set; } = "Student";

    /// <summary>
    /// Grado para estudiantes (ej. "1°", "2°", "3°", "4°", "5°" o "2° Primaria")
    /// </summary>
    public string? Grade { get; set; }
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public UserDto User { get; set; } = new();
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
}
