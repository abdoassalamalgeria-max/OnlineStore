using BookStore.Application.Features.Account;
using BookStore.Application.Features.Reviews;
using BookStore.Domain.Catalog;
using BookStore.Domain.Common;
using FluentValidation.TestHelper;

namespace BookStore.Application.UnitTests;

public sealed class ReviewRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rating_outside_1_to_5_is_rejected(int rating) =>
        Assert.Throws<DomainException>(() => new Review(Guid.NewGuid(), Guid.NewGuid(), "Amine", rating, null));

    [Fact]
    public void Blank_comment_is_stored_as_null_and_text_is_trimmed()
    {
        var review = new Review(Guid.NewGuid(), Guid.NewGuid(), "Amine", 4, "   ");
        Assert.Null(review.Comment);

        review.Update(5, "  رواية رائعة  ");
        Assert.Equal("رواية رائعة", review.Comment);
        Assert.Equal(5, review.Rating);
    }

    [Fact]
    public void Overlong_comment_is_rejected() =>
        Assert.Throws<DomainException>(() =>
            new Review(Guid.NewGuid(), Guid.NewGuid(), "Amine", 3, new string('x', Review.MaxCommentLength + 1)));

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void Validator_checks_the_rating_range(int rating, bool valid)
    {
        var result = new UpsertReviewRequestValidator().TestValidate(new UpsertReviewRequest(rating, null));
        Assert.Equal(valid, result.IsValid);
    }
}

public sealed class AccountValidatorTests
{
    [Fact]
    public void New_password_must_differ_from_the_current_one() =>
        new ChangePasswordRequestValidator()
            .TestValidate(new ChangePasswordRequest("Secret123", "Secret123"))
            .ShouldHaveValidationErrorFor(r => r.NewPassword);

    [Fact]
    public void New_password_must_be_strong() =>
        new ChangePasswordRequestValidator()
            .TestValidate(new ChangePasswordRequest("Secret123", "weak"))
            .ShouldHaveValidationErrorFor(r => r.NewPassword);

    [Fact]
    public void Profile_requires_a_name_and_an_algerian_phone_if_given()
    {
        var validator = new UpdateProfileRequestValidator();

        validator.TestValidate(new UpdateProfileRequest("", null)).ShouldHaveValidationErrorFor(r => r.FullName);
        validator.TestValidate(new UpdateProfileRequest("Amine", "12345")).ShouldHaveValidationErrorFor(r => r.PhoneNumber);
        validator.TestValidate(new UpdateProfileRequest("Amine", null)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new UpdateProfileRequest("Amine", "0661234567")).ShouldNotHaveAnyValidationErrors();
    }
}
