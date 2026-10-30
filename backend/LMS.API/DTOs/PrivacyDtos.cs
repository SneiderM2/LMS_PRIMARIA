using System.ComponentModel.DataAnnotations;

namespace LMS.API.DTOs;

public class ConsentRequestDto
{
    public bool Accepted { get; set; }

    /// <summary>Versión de la política que el titular declara aceptar.</summary>
    public string? Version { get; set; }

    public string? GuardianFullName { get; set; }
    public string? GuardianDocument { get; set; }
    public string? GuardianDocumentType { get; set; } = "CC";
    public bool GuardianAuthorization { get; set; }
    public bool AutorizaServicioEducativo { get; set; } = true;
    public bool AutorizaDatosOpcionales { get; set; } = false;
    public bool AutorizaTransmisionInternacional { get; set; } = true;
}

public class SolicitudArcoCreateDto
{
    /// <summary>
    /// Tipo de solicitud ARCO: CONOCER (Consulta - 10 días hábiles),
    /// ACTUALIZAR, RECTIFICAR, SUPRIMIR o REVOCAR (Reclamo - 15 días hábiles).
    /// </summary>
    [Required(ErrorMessage = "El tipo de solicitud ARCO es obligatorio.")]
    [MaxLength(30)]
    public string TipoSolicitud { get; set; } = "CONOCER";

    [Required(ErrorMessage = "La descripción de la solicitud es obligatoria.")]
    [MinLength(10, ErrorMessage = "Describa su solicitud con al menos 10 caracteres.")]
    [MaxLength(2000)]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? SolicitanteNombre { get; set; }

    [MaxLength(50)]
    public string? SolicitanteDocumento { get; set; }
}

public class RevokeGuardianConsentDto
{
    [Required(ErrorMessage = "Debe indicar el motivo de la revocatoria del consentimiento.")]
    [MinLength(5)]
    [MaxLength(500)]
    public string Motivo { get; set; } = string.Empty;
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
