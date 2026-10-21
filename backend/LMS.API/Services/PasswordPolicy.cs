namespace LMS.API.Services;

/// <summary>
/// Política institucional de contraseñas.
/// Alineada con OWASP ASVS 4.0 (V2.1), NIST SP 800-63B y las recomendaciones de la
/// Resolución MinTIC 1519 de 2020 / MSPI: mínimo 8 caracteres, mayúscula, minúscula,
/// número, carácter especial, bloqueo de contraseñas comunes y de datos predecibles del titular.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    private const string SpecialCharacters = "!@#$%^&*()_+-=[]{};':\"\\|,.<>/?~`";

    // Lista corta de contraseñas comprometidas de uso frecuente en Colombia y en el proyecto.
    private static readonly HashSet<string> BlockedPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "123456789", "12345678", "1234567890", "password", "password123",
        "admin123", "docente123", "estudiante123", "qwerty123", "abc123456",
        "colombia2026", "lmsprimaria", "lms2026", "colegio123", "escuela123",
        "admin123456", "Password123", "Password123!", "Password1234", "12345678910", "123456789a",
        "1q2w3e4r5t6y", "iloveyou", "princesa", "bogota2026", "medellin2026",
        "Admin123!", "Docente123!", "Estudiante123!", "Colombia2026!"
    };

    /// <summary>
    /// Determina si un carácter se considera carácter especial válido.
    /// </summary>
    public static bool IsSpecialCharacter(char c)
        => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c);

    /// <summary>
    /// Valida una contraseña en texto plano. Devuelve el motivo legible del rechazo.
    /// Exige mínimo 8 caracteres, al menos una mayúscula, una minúscula, un número y un carácter especial.
    /// </summary>
    public static (bool IsValid, string Message) Validate(string? password, string? username = null, string? fullName = null)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "La contraseña es obligatoria.");
        }

        if (password.Length < MinLength)
        {
            return (false, $"La contraseña debe tener al menos {MinLength} caracteres.");
        }

        if (password.Length > MaxLength)
        {
            return (false, $"La contraseña no puede superar {MaxLength} caracteres.");
        }

        if (password.Any(char.IsWhiteSpace))
        {
            return (false, "La contraseña no debe contener espacios en blanco.");
        }

        if (!password.Any(char.IsUpper))
        {
            return (false, "La contraseña debe incluir al menos una letra mayúscula (A-Z).");
        }

        if (!password.Any(char.IsLower))
        {
            return (false, "La contraseña debe incluir al menos una letra minúscula (a-z).");
        }

        if (!password.Any(char.IsDigit))
        {
            return (false, "La contraseña debe incluir al menos un número (0-9).");
        }

        if (!password.Any(IsSpecialCharacter))
        {
            return (false, "La contraseña debe incluir al menos un carácter especial (por ejemplo: !@#$%&*+-_?).");
        }

        if (password.Distinct().Count() < 5)
        {
            return (false, "La contraseña es demasiado repetitiva. Usa una combinación más variada.");
        }

        if (BlockedPasswords.Contains(password))
        {
            return (false, "La contraseña elegida es de uso común y no ofrece seguridad suficiente.");
        }

        var normalized = password.ToLowerInvariant();

        foreach (var token in TokenizeIdentity(username, fullName))
        {
            // Se rechazan coincidencias completas o parciales significativas con el nombre/carnet.
            if (token.Length >= 4 && normalized.Contains(token))
            {
                return (false, "La contraseña no debe contener tu nombre, apellido ni tu documento o carnet.");
            }
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Genera una contraseña temporal criptográficamente segura (mayúscula, minúscula,
    /// dígito y carácter especial, sin caracteres ambiguos) para entregar al acudiente o al usuario.
    /// </summary>
    public static string GenerateTemporaryPassword(int length = 16)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*+-_?";
        const string specials = "!@#$%&*+-_?";
        var safeLength = Math.Max(MinLength, length);
        var buffer = new char[safeLength];

        for (var i = 0; i < safeLength; i++)
        {
            buffer[i] = alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        // Garantizar al menos una mayúscula, una minúscula, un dígito y un carácter especial.
        buffer[0] = (char)('A' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(26));
        buffer[1] = (char)('a' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(26));
        buffer[2] = (char)('2' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(8));
        buffer[3] = specials[System.Security.Cryptography.RandomNumberGenerator.GetInt32(specials.Length)];

        return new string(buffer);
    }

    private static IEnumerable<string> TokenizeIdentity(string? username, string? fullName)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            yield return username.Trim().ToLowerInvariant().Replace(" ", string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            foreach (var part in fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length >= 4)
                {
                    yield return part.ToLowerInvariant();
                }
            }
        }
    }
}
