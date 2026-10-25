using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdef1@")]                  // Exactamente 8 caracteres (mínimo)
    [InlineData("SciKid9!")]                  // 9 caracteres
    [InlineData("Profe2026@Seguro")]          // 16 caracteres
    public void Accepts_valid_passwords_between_8_and_64_characters(string password)
    {
        var result = PasswordPolicy.Validate(password, "sneider", "Sneider Martinez");

        Assert.True(result.IsValid, result.Message);
    }

    [Fact]
    public void Accepts_password_with_exactly_64_characters()
    {
        var password = "Aa1@" + new string('a', 60);

        var result = PasswordPolicy.Validate(password);

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [InlineData("Abc1@a")]                        // 7 caracteres: menos del mínimo
    [InlineData("solomin1!")]                     // Sin mayúscula
    [InlineData("SOLOMAY1!")]                     // Sin minúscula
    [InlineData("SinNumero!")]                    // Sin número
    [InlineData("SinEspecial1")]                  // Sin carácter especial
    [InlineData("Abc 1234@")]                     // Con espacio en blanco
    [InlineData("12345678")]                      // Solo números
    public void Rejects_weak_or_out_of_range_passwords(string password)
    {
        var result = PasswordPolicy.Validate(password, "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void Rejects_common_password()
    {
        var result = PasswordPolicy.Validate("Admin123!", "sneider", "Sneider Martinez");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_password_containing_identity()
    {
        var result = PasswordPolicy.Validate("Sneider9!", "sneider", "Sneider Martinez");

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
        var password = "Aa1@" + new string('a', 61); // 65 caracteres

        var result = PasswordPolicy.Validate(password);

        Assert.False(result.IsValid);
        Assert.Contains("64", result.Message);
    }
}
