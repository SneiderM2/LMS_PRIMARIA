using System.ComponentModel.DataAnnotations;

namespace LMS.API.DTOs;

public class ConsentRequestDto
{
    public bool Accepted { get; set; }

    /// <summary>Versión de la política que el titular declara aceptar.</summary>
    public string? Version { get; set; }

    public string? GuardianFullName { get; set; }
    public string? GuardianDocument { get; set; }
    public bool GuardianAuthorization { get; set; }
}

public class DeleteAccountDto
{
    [Required(ErrorMessage = "Debes confirmar con tu contraseña.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Frase exacta de confirmación: ELIMINAR MI CUENTA.</summary>
    [Required(ErrorMessage = "La frase de confirmación es obligatoria.")]
    public string Confirmation { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public class RectifyPersonalDataDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;
}
