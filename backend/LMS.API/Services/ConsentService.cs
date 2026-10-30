using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LMS.API.Configuration;
using LMS.API.Entities;
using Microsoft.Extensions.Options;

namespace LMS.API.Services;

/// <summary>
/// Construye y valida la evidencia de consentimiento informado exigida por el
/// artículo 9 de la Ley 1581 de 2012 y el Decreto 1074 de 2015.
/// La evidencia se firma con HMAC-SHA256 para detectar alteraciones posteriores.
/// </summary>
public sealed class ConsentService
{
    private readonly PrivacyOptions _options;
    private readonly string _hmacKey;

    public ConsentService(IOptions<PrivacyOptions> options)
    {
        _options = options.Value;

        _hmacKey = _options.EvidenceHmacKey;
        if (string.IsNullOrWhiteSpace(_hmacKey) || _hmacKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Privacy:EvidenceHmacKey es obligatorio y debe tener al menos 32 caracteres. " +
                "No se reutiliza Jwt:SecretKey para separar las claves criptográficas.");
        }
    }

    public string CurrentVersion => _options.PolicyVersion;

    public bool IsCurrent(Usuario user)
        => user.DataPolicyAccepted
           && string.Equals(user.DataPolicyVersion, _options.PolicyVersion, StringComparison.Ordinal);

    /// <summary>
    /// Valida los requisitos legales del consentimiento informado del padre, madre o acudiente
    /// para el tratamiento de datos personales de niños, niñas y adolescentes
    /// (Ley 1581 de 2012, art. 7; Ley 1098 de 2006; Decreto 1074 de 2015).
    /// </summary>
    public static (bool IsValid, string Message) ValidateGuardianConsent(
        bool guardianAuthorization,
        string? guardianFullName,
        string? guardianDocument = null)
    {
        if (!guardianAuthorization)
        {
            return (false, "Se requiere la autorización expresa del padre, madre o acudiente para el tratamiento de datos del menor (Ley 1581 de 2012, art. 7 y Ley 1098 de 2006).");
        }

        if (string.IsNullOrWhiteSpace(guardianFullName) || guardianFullName.Trim().Length < 5)
        {
            return (false, "Debe indicar el nombre completo válido del padre, madre o acudiente responsable del menor.");
        }

        if (!string.IsNullOrWhiteSpace(guardianDocument) && guardianDocument.Trim().Length < 4)
        {
            return (false, "El documento del acudiente, si se suministra, debe tener al menos 4 caracteres.");
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Verifica criptográficamente la integridad (HMAC-SHA256) de una evidencia de consentimiento almacenada.
    /// </summary>
    public bool VerifyEvidenceSignature(string? serializedEvidence)
    {
        if (string.IsNullOrWhiteSpace(serializedEvidence))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(serializedEvidence);
            if (!doc.RootElement.TryGetProperty("evidence", out var evidenceElement)
                || !doc.RootElement.TryGetProperty("signature", out var signatureElement))
            {
                return false;
            }

            var rawPayload = evidenceElement.GetRawText();
            var expectedSignature = Sign(rawPayload);
            var actualSignature = signatureElement.GetString() ?? string.Empty;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(actualSignature.ToLowerInvariant()));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Genera el JSON de evidencia (sin datos innecesarios) y su firma HMAC.
    /// </summary>
    public string BuildEvidence(
        Usuario user,
        string method,
        string? ip,
        string? userAgent,
        string? guardianFullName = null,
        string? guardianDocument = null)
    {
        var acceptedAt = DateTime.UtcNow;

        var evidence = new
        {
            policyVersion = _options.PolicyVersion,
            acceptedAt = acceptedAt.ToString("O"),
            method,
            userId = user.Id,
            usernameHash = Sha256(user.Username),
            ip = string.IsNullOrWhiteSpace(ip) ? null : ip,
            userAgent = Truncate(userAgent, 300),
            guardian = string.IsNullOrWhiteSpace(guardianFullName) && string.IsNullOrWhiteSpace(guardianDocument)
                ? null
                : new
                {
                    fullName = Truncate(guardianFullName, 150),
                    documentMasked = MaskDocument(guardianDocument),
                    authorization = true
                }
        };

        var payload = JsonSerializer.Serialize(evidence);
        var signature = Sign(payload);

        return JsonSerializer.Serialize(new
        {
            evidence = JsonSerializer.Deserialize<JsonElement>(payload),
            signature,
            algorithm = "HMAC-SHA256"
        });
    }

    public string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static string MaskDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return string.Empty;
        }

        var clean = document.Trim();
        if (clean.Length <= 3)
        {
            return "***";
        }

        return new string('*', clean.Length - 3) + clean[^3..];
    }

    /// <summary>
    /// Calcula el hash SHA-256 canónico del texto oficial de la política de privacidad vigente.
    /// </summary>
    public string ComputePolicyTextHash(string? version = null)
    {
        var v = string.IsNullOrWhiteSpace(version) ? _options.PolicyVersion : version.Trim();
        var canonicalPolicyText =
            $"POLITICA_HABEAS_DATA_LMS_PRIMARIA|VERSION={v}|RESPONSABLE={_options.ControllerName}|" +
            "LEY_1581_2012|DECRETO_1377_2013|LEY_1098_2006|FINALIDAD_EDUCATIVA_MENORES";
        return Sha256(canonicalPolicyText);
    }

    /// <summary>
    /// Construye la entidad probatoria <see cref="ConsentimientoHabeasData"/> para menores de edad.
    /// </summary>
    public ConsentimientoHabeasData CreateHabeasDataRecord(
        int alumnoId,
        int? usuarioId,
        string? guardianFullName,
        string? guardianDocument,
        string? tipoDocumento,
        bool autorizaServicioEducativo,
        bool autorizaDatosOpcionales,
        bool autorizaTransmisionInternacional,
        string? ip,
        string? userAgent)
    {
        var validDocTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CC", "CE", "TI", "PEP", "PPT", "PASAPORTE" };
        var normalizedDocType = !string.IsNullOrWhiteSpace(tipoDocumento) && validDocTypes.Contains(tipoDocumento.Trim())
            ? tipoDocumento.Trim().ToUpperInvariant()
            : "CC";

        return new ConsentimientoHabeasData
        {
            AlumnoId = alumnoId,
            UsuarioId = usuarioId,
            AcudienteNombre = Truncate(guardianFullName?.Trim(), 150) ?? "Acudiente Registrado",
            AcudienteDocumento = Truncate(MaskDocument(guardianDocument), 50) ?? "****",
            TipoDocumento = normalizedDocType,
            VersionPolitica = _options.PolicyVersion,
            HashTextoPolitica = ComputePolicyTextHash(_options.PolicyVersion),
            AutorizaServicioEducativo = autorizaServicioEducativo,
            AutorizaDatosOpcionales = autorizaDatosOpcionales,
            AutorizaTransmisionInternacional = autorizaTransmisionInternacional,
            IpOrigen = Truncate(string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(), 64) ?? "127.0.0.1",
            UserAgent = Truncate(string.IsNullOrWhiteSpace(userAgent) ? "LMS-Client" : userAgent.Trim(), 300) ?? "LMS-Client",
            FechaAutorizacionUtc = DateTime.UtcNow,
            FechaRevocatoriaUtc = null
        };
    }

    private static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= max ? value : value[..max];
    }
}
