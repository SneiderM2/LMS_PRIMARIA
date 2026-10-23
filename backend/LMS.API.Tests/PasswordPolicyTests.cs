using LMS.API.Services;
using Xunit;

namespace LMS.API.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Ab1@")]          // Exactamente 4 caracteres (mínimo)
    [InlineData("Sol9#")]         // 5 caracteres
    [InlineData("SciKid9!")]      // 8 caracteres
    [InlineData("Profe2026@")]    // Exactamente 10 caracteres (máximo)
    public void Accepts_valid_passwords_between_4_and_10_characters(string password)
    {
        var result = PasswordPolicy.Validate(password, "sneider", "Sneider Martinez");

        Assert.True(result.IsValid, result.Message);
    }

    [Theory]
    [InlineData("A1@")]                  // Menos de 4 caracteres
    [InlineData("Colegio2026!")]         // Más de 10 caracteres (12)
    [InlineData("solomin1!")]            // Sin mayúscula
    [InlineData("SOLOMAY1!")]            // Sin minúscula
    [InlineData("SinNum!")]              // Sin número
    [InlineData("SinEspec1")]            // Sin carácter especial
    [InlineData("Ab 1@")]                // Con espacio en blanco
    [InlineData("12345678")]             // Solo números
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
    public void Generates_temporary_password_satisfying_4_to_10_policy()
    {
        var password = PasswordPolicy.GenerateTemporaryPassword(8);

        Assert.InRange(password.Length, 4, 10);
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsDigit(c));
        Assert.Contains(password, c => PasswordPolicy.IsSpecialCharacter(c));
        Assert.True(PasswordPolicy.Validate(password).IsValid);
    }
}
