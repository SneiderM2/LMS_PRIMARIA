namespace LMS.API.Configuration;

/// <summary>
/// Parámetros de cumplimiento de la Ley 1581 de 2012, el Decreto 1074 de 2015
/// (Decreto Único Reglamentario del Sector Comercio) y los lineamientos de la SIC.
/// Los textos legales definitivos deben ser aprobados por el responsable del tratamiento.
/// </summary>
public sealed class PrivacyOptions
{
    public const string SectionName = "Privacy";

    /// <summary>Versión vigente de la política. Cambiarla obliga a todos los titulares a aceptar de nuevo.</summary>
    public string PolicyVersion { get; set; } = "2026-10-19";

    public string PolicyEffectiveDate { get; set; } = "2026-10-19";

    public string PolicyUrl { get; set; } = "https://sneiderm2.github.io/LMS_PRIMARIA/privacidad.html";

    /// <summary>Responsable del tratamiento (institución educativa).</summary>
    public string ControllerName { get; set; } = "Institución Educativa — LMS Primaria";

    public string ControllerNit { get; set; } = "Por definir";

    public string ControllerAddress { get; set; } = "Colombia";

    public string ContactEmail { get; set; } = "privacidad@lmsprimaria.edu.co";

    public string ContactPhone { get; set; } = "Por definir";

    public string DpoName { get; set; } = "Oficial de Protección de Datos";

    /// <summary>
    /// Exige autorización del padre/madre/acudiente para registrar menores de edad
    /// (Ley 1581 de 2012, artículo 7; Ley 1098 de 2006).
    /// </summary>
    public bool RequireGuardianConsentForMinors { get; set; } = true;

    /// <summary>Años de conservación de la historia académica (obligación escolar).</summary>
    public int AcademicRetentionYears { get; set; } = 5;

    /// <summary>Días de conservación de logs con datos personales (IP, usuario).</summary>
    public int LogRetentionDays { get; set; } = 90;

    /// <summary>Meses de inactividad tras los cuales se anonimiza una cuenta.</summary>
    public int InactivityAnonymizationMonths { get; set; } = 36;

    /// <summary>
    /// Habilita la anonimización automática por inactividad. Debe activarse solo
    /// cuando el responsable del tratamiento haya aprobado la retención declarada.
    /// </summary>
    public bool EnableInactivityAnonymization { get; set; } = false;

    /// <summary>
    /// Modo simulación: registra las cuentas candidatas sin modificar datos.
    /// Se recomienda mantenerlo en true durante la primera ejecución en producción.
    /// </summary>
    public bool InactivityAnonymizationDryRun { get; set; } = true;

    /// <summary>Clave dedicada para firmar (HMAC) la evidencia de consentimiento.</summary>
    public string EvidenceHmacKey { get; set; } = string.Empty;

    public string SuperintendenceUrl { get; set; } = "https://www.sic.gov.co";
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Proveedor de almacenamiento: "Local" (por defecto) o "Supabase".</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Ruta física privada (fuera de wwwroot) donde se guardan los adjuntos locales.</summary>
    public string RootPath { get; set; } = string.Empty;

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    public int MaxFilesPerSubmission { get; set; } = 5;

    /// <summary>
    /// Permite que Supabase Storage caiga silenciosamente al disco local si falla.
    /// En producción debe permanecer en false para no perder archivos en un disco efímero.
    /// </summary>
    public bool AllowLocalFallback { get; set; } = false;

    public SupabaseStorageOptions Supabase { get; set; } = new();
}

public sealed class SupabaseStorageOptions
{
    public string Url { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "lms-uploads";
}

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    /// <summary>Código de invitación institucional para registrar docentes. Vacío = registro docente deshabilitado.</summary>
    public string TeacherInviteCode { get; set; } = string.Empty;

    public bool AllowPublicStudentRegistration { get; set; } = true;
}
