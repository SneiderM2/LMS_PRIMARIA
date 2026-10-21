using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

public class PasswordPolicyTests
{
    [Fact]
    public void Accepts_password_with_upper_lower_digit_and_special_char()
    {
        var result = PasswordPolicy.Validate("Colegio2026!", "sneider", "Sneider Martinez");

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [InlineData("Cort1!")]               // Menos de 8 caracteres
    [InlineData("solominusc1!")]         // Sin mayúscula
    [InlineData("SOLOMAYUSC1!")]         // Sin minúscula
    [InlineData("SinNumeros!!")]         // Sin número
    [InlineData("SinEspecial2026")]      // Sin carácter especial
    [InlineData("Colegio 2026!")]        // Con espacio en blanco
    [InlineData("123456789012345")]      // Solo números
    public void Rejects_weak_passwords(string password)
    {
        var result = PasswordPolicy.Validate(password, "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Rejects_common_password()
    {
        var result = PasswordPolicy.Validate("Password123!", "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_password_containing_identity()
    {
        var result = PasswordPolicy.Validate("Sneider2026!", "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Generates_temporary_password_satisfying_strict_policy()
    {
        var password = PasswordPolicy.GenerateTemporaryPassword();

        Assert.Equal(16, password.Length);
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsDigit(c));
        Assert.Contains(password, c => PasswordPolicy.IsSpecialCharacter(c));
        Assert.True(PasswordPolicy.Validate(password).IsValid);
    }
}
