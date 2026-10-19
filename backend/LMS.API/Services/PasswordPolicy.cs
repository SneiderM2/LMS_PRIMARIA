namespace LMS.API.Services;

/// <summary>
/// Política institucional de contraseñas.
/// Alineada con NIST SP 800-63B, OWASP ASVS 4.0 (V2.1) y las recomendaciones de la
/// Resolución MinTIC 1519 de 2020 / MSPI: longitud mínima, sin composición forzada
/// artificial, bloqueo de contraseñas comunes y de datos predecibles del titular.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    // Lista corta de contraseñas comprometidas de uso frecuente en Colombia y en el proyecto.
    private static readonly HashSet<string> BlockedPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "123456789", "12345678", "1234567890", "password", "password123",
        "admin123", "docente123", "estudiante123", "qwerty123", "abc123456",
        "colombia2026", "lmsprimaria", "lms2026", "colegio123", "escuela123",
        "admin123456", "Password123", "Password1234", "12345678910", "123456789a",
        "1q2w3e4r5t6y", "iloveyou", "princesa", "bogota2026", "medellin2026"
    };

    /// <summary>
    /// Valida una contraseña en texto plano. Devuelve el motivo legible del rechazo.
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

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return (false, "La contraseña debe combinar letras y números.");
        }

        if (password.Distinct().Count() < 6)
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
    /// Genera una contraseña temporal criptográficamente segura (letras + números, sin
    /// caracteres ambiguos) para entregar al acudiente o al usuario.
    /// </summary>
    public static string GenerateTemporaryPassword(int length = 16)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var buffer = new char[length];

        for (var i = 0; i < length; i++)
        {
            buffer[i] = alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        // Garantizar al menos una mayúscula, una minúscula y un dígito.
        buffer[0] = 'A';
        buffer[1] = 'b';
        buffer[2] = (char)('2' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(8));

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
