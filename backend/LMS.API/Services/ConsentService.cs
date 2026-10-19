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

    public ConsentService(IOptions<PrivacyOptions> options, IConfiguration configuration)
    {
        _options = options.Value;

        _hmacKey = !string.IsNullOrWhiteSpace(_options.EvidenceHmacKey)
            ? _options.EvidenceHmacKey
            : configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException(
                "No hay clave disponible para firmar la evidencia de consentimiento (Privacy:EvidenceHmacKey o Jwt:SecretKey).");
    }

    public string CurrentVersion => _options.PolicyVersion;

    public bool IsCurrent(Usuario user)
        => user.DataPolicyAccepted
           && string.Equals(user.DataPolicyVersion, _options.PolicyVersion, StringComparison.Ordinal);

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
