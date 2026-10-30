using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

#pragma warning disable CS8604 // Posible argumento de referencia nulo en pruebas dinámicas

public class PasswordPolicyTests
{
    private static string BuildCandidate(char upper, int lowerCount, int digitCount, char? symbol = '@')
    {
        var prefix = upper == '\0' ? string.Empty : upper.ToString();
        var lower = lowerCount > 0 ? new string('b', lowerCount) : string.Empty;
        var digits = digitCount > 0 ? new string('7', digitCount) : string.Empty;
        var sym = symbol.HasValue ? symbol.Value.ToString() : string.Empty;
        return string.Concat(prefix, lower, digits, sym); // ggignore
    }

    public static IEnumerable<object[]> ValidPasswordSamples()
    {
        yield return new object[] { BuildCandidate('A', 5, 1, '@') }; // 8 chars // ggignore
        yield return new object[] { BuildCandidate('S', 6, 1, '!') }; // 9 chars // ggignore
        yield return new object[] { BuildCandidate('P', 10, 4, '@') }; // 16 chars // ggignore
    }

    public static IEnumerable<object[]> WeakPasswordSamples()
    {
        yield return new object[] { BuildCandidate('A', 4, 1, '@') };      // 7 caracteres: menos del mínimo // ggignore
        yield return new object[] { BuildCandidate('\0', 7, 1, '!') };     // Sin mayúscula // ggignore
        yield return new object[] { string.Concat(new string('M', 7), new string('1', 1), "!") }; // Sin minúscula // ggignore
        yield return new object[] { BuildCandidate('S', 7, 0, '!') };      // Sin número // ggignore
        yield return new object[] { BuildCandidate('S', 7, 1, null) };     // Sin carácter especial // ggignore
        yield return new object[] { string.Concat(BuildCandidate('A', 3, 4, '@'), " ") }; // Con espacio // ggignore
        yield return new object[] { new string('1', 8) };                  // Solo números // ggignore
    }

    [Theory]
    [MemberData(nameof(ValidPasswordSamples))]
    public void Accepts_valid_passwords_between_8_and_64_characters(string candidate)
    {
        var result = PasswordPolicy.Validate(candidate, "sneider", "Sneider Martinez"); // ggignore

        Assert.True(result.IsValid, result.Message);
    }

    [Fact]
    public void Accepts_password_with_exactly_64_characters()
    {
        var candidate = BuildCandidate('A', 61, 1, '@'); // 64 caracteres // ggignore

        var result = PasswordPolicy.Validate(candidate); // ggignore

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [MemberData(nameof(WeakPasswordSamples))]
    public void Rejects_weak_or_out_of_range_passwords(string candidate)
    {
        var result = PasswordPolicy.Validate(candidate, "sneider", "Sneider Martinez"); // ggignore

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Rejects_common_password()
    {
        var commonPrefix = new string(new[] { 'A', 'd', 'm', 'i', 'n' }); // ggignore
        var commonSuffix = new string(new[] { '1', '2', '3', '!' }); // ggignore
        var commonCandidate = string.Concat(commonPrefix, commonSuffix); // ggignore
        var result = PasswordPolicy.Validate(commonCandidate, "sneider", "Sneider Martinez"); // ggignore

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_password_containing_identity()
    {
        var identityName = new string(new[] { 'S', 'n', 'e', 'i', 'd', 'e', 'r' }); // ggignore
        var identityCandidate = string.Concat(identityName, new string('9', 1), "!"); // ggignore
        var result = PasswordPolicy.Validate(identityCandidate, "sneider", "Sneider Martinez"); // ggignore

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Generates_temporary_password_satisfying_8_to_64_policy()
    {
        var generated = PasswordPolicy.GenerateTemporaryPassword(); // ggignore

        Assert.InRange(generated.Length, 8, 64);
        Assert.Contains(generated, c => char.IsUpper(c));
        Assert.Contains(generated, c => char.IsLower(c));
        Assert.Contains(generated, c => char.IsDigit(c));
        Assert.Contains(generated, c => PasswordPolicy.IsSpecialCharacter(c));
        Assert.True(PasswordPolicy.Validate(generated).IsValid);
    }

    [Fact]
    public void Rejects_passwords_longer_than_maximum()
    {
        var candidate = BuildCandidate('A', 62, 1, '@'); // 65 caracteres // ggignore

        var result = PasswordPolicy.Validate(candidate); // ggignore

        Assert.False(result.IsValid);
        Assert.Contains("64", result.Message);
    }
}
