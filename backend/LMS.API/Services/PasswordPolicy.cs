namespace LMS.API.Services;

/// <summary>
/// Política institucional de contraseñas.
/// Alineada con NIST SP 800-63B y las recomendaciones de la Resolución MinTIC 1519
/// de 2020 / MSPI: mínimo 8 caracteres, máximo 64, mayúscula, minúscula, número,
/// carácter especial, bloqueo de contraseñas comunes y de datos predecibles del titular.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 64;

    private const string SpecialCharacters = "!@#$%^&*()_+-=[]{};':\"\\|,.<>/?~`";

    // Lista corta de contraseñas comprometidas de uso frecuente.
    private static readonly HashSet<string> BlockedPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "1234", "12345", "123456", "12345678", "123456789", "1234567890",
        "admin", "admin123", "docente", "docente123", "alumno", "alumno123",
        "qwerty", "password", "clave123", "colegio1", "escuela1",
        "Admin123!", "Docente1!", "Alumno123!", "Col2026!"
    };

    /// <summary>
    /// Determina si un carácter se considera carácter especial válido.
    /// </summary>
    public static bool IsSpecialCharacter(char c)
        => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c);

    /// <summary>
    /// Valida una contraseña en texto plano. Devuelve el motivo legible del rechazo.
    /// Exige entre 8 y 64 caracteres, al menos una mayúscula, una minúscula, un número y un carácter especial.
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
            return (false, "La contraseña debe incluir al menos un carácter especial (por ejemplo: @, #, !, $, %).");
        }

        if (password.Distinct().Count() < Math.Min(4, password.Length))
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
    /// Genera una contraseña temporal criptográficamente segura dentro del rango permitido
    /// (mayúscula, minúscula, dígito y carácter especial, sin caracteres ambiguos) para entregar al acudiente o usuario.
    /// </summary>
    public static string GenerateTemporaryPassword(int length = 12)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*+-_?";
        const string specials = "!@#$%&*+-_?";
        var safeLength = Math.Clamp(length, MinLength, MaxLength);
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

        // Mezclar aleatoriamente las posiciones (Fisher-Yates) para que no siempre empiece con el mismo orden
        for (var i = safeLength - 1; i > 0; i--)
        {
            var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

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
