using BookStore.Application.Features.Auth;
using FluentValidation.TestHelper;

namespace BookStore.Application.UnitTests;

public sealed class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest Valid(string? phone = "0550123456", string password = "Secret123") =>
        new("Amine Benali", "amine@example.dz", password, phone);

    [Fact]
    public void Valid_request_passes() => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("0550123456")]      // Djezzy/Ooredoo/Mobilis mobile
    [InlineData("0661234567")]
    [InlineData("0770123456")]
    [InlineData("+213550123456")]   // international form
    [InlineData("021234567")]       // Algiers landline
    [InlineData(null)]              // optional
    [InlineData("")]
    public void Accepts_algerian_phone_numbers(string? phone) =>
        _validator.TestValidate(Valid(phone)).ShouldNotHaveValidationErrorFor(r => r.PhoneNumber);

    [Theory]
    [InlineData("0850123456")]      // 08 is not a mobile prefix
    [InlineData("055012345")]       // too short
    [InlineData("+33612345678")]    // French number
    [InlineData("05501234567")]     // too long
    public void Rejects_other_phone_numbers(string phone) =>
        _validator.TestValidate(Valid(phone)).ShouldHaveValidationErrorFor(r => r.PhoneNumber);

    [Theory]
    [InlineData("short1A")]         // < 8 chars
    [InlineData("alllowercase1")]   // no uppercase
    [InlineData("ALLUPPERCASE1")]   // no lowercase
    [InlineData("NoDigitsHere")]    // no digit
    public void Rejects_weak_passwords(string password) =>
        _validator.TestValidate(Valid(password: password)).ShouldHaveValidationErrorFor(r => r.Password);

    [Fact]
    public void Rejects_invalid_email() =>
        _validator.TestValidate(Valid() with { Email = "nope" }).ShouldHaveValidationErrorFor(r => r.Email);
}

public sealed class LoginAndRefreshValidatorTests
{
    [Fact]
    public void Login_requires_email_and_password()
    {
        var result = new LoginRequestValidator().TestValidate(new LoginRequest("", ""));

        result.ShouldHaveValidationErrorFor(r => r.Email);
        result.ShouldHaveValidationErrorFor(r => r.Password);
    }

    [Fact]
    public void Refresh_requires_a_token() =>
        new RefreshRequestValidator().TestValidate(new RefreshRequest("")).ShouldHaveValidationErrorFor(r => r.RefreshToken);
}
