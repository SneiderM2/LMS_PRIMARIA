using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

#pragma warning disable CS8604 // Posible argumento de referencia nulo en pruebas dinámicas

public class PasswordPolicyTests
{
    private static string BuildCandidate(string upper, string lower, string digits, string symbol)
        => string.Concat(upper, lower, digits, symbol);

    public static IEnumerable<object[]> ValidPasswordSamples()
    {
        yield return new object[] { BuildCandidate("A", "bcdef", "1", "@") };         // Exactamente 8 caracteres (mínimo)
        yield return new object[] { BuildCandidate("Sci", "Kid", "9", "!") };         // 9 caracteres
        yield return new object[] { BuildCandidate("Profe", "Seguro", "2026", "@") }; // 16 caracteres
    }

    public static IEnumerable<object[]> WeakPasswordSamples()
    {
        yield return new object[] { BuildCandidate("A", "bc", "1", "@a") };           // 7 caracteres: menos del mínimo
        yield return new object[] { BuildCandidate("", "solomin", "1", "!") };        // Sin mayúscula
        yield return new object[] { BuildCandidate("SOLOMAY", "", "1", "!") };        // Sin minúscula
        yield return new object[] { BuildCandidate("Sin", "Numero", "", "!") };       // Sin número
        yield return new object[] { BuildCandidate("Sin", "Especial", "1", "") };     // Sin carácter especial
        yield return new object[] { BuildCandidate("Abc ", "", "1234", "@") };        // Con espacio en blanco
        yield return new object[] { new string('1', 8) };                             // Solo números
    }

    [Theory]
    [MemberData(nameof(ValidPasswordSamples))]
    public void Accepts_valid_passwords_between_8_and_64_characters(string password)
    {
        var result = PasswordPolicy.Validate(password, "sneider", "Sneider Martinez");

        Assert.True(result.IsValid, result.Message);
    }

    [Fact]
    public void Accepts_password_with_exactly_64_characters()
    {
        var candidate = string.Concat("A", "a", "1", "@", new string('a', 60));

        var result = PasswordPolicy.Validate(candidate);

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [MemberData(nameof(WeakPasswordSamples))]
    public void Rejects_weak_or_out_of_range_passwords(string password)
    {
        var result = PasswordPolicy.Validate(password, "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Rejects_common_password()
    {
        var commonCandidate = BuildCandidate("Ad", "min", "123", "!");
        var result = PasswordPolicy.Validate(commonCandidate, "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_password_containing_identity()
    {
        var identityCandidate = BuildCandidate("S", "neider", "9", "!");
        var result = PasswordPolicy.Validate(identityCandidate, "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Generates_temporary_password_satisfying_8_to_64_policy()
    {
        var password = PasswordPolicy.GenerateTemporaryPassword();

        Assert.InRange(password.Length, 8, 64);
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsDigit(c));
        Assert.Contains(password, c => PasswordPolicy.IsSpecialCharacter(c));
        Assert.True(PasswordPolicy.Validate(password).IsValid);
    }

    [Fact]
    public void Rejects_passwords_longer_than_maximum()
    {
        var candidate = string.Concat("A", "a", "1", "@", new string('a', 61)); // 65 caracteres

        var result = PasswordPolicy.Validate(candidate);

        Assert.False(result.IsValid);
        Assert.Contains("64", result.Message);
    }
}
