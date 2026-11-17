using BookStore.Application.Common;
using BookStore.Application.Features.Catalog;
using BookStore.Domain.Catalog;
using BookStore.Domain.Common;
using FluentValidation.TestHelper;

namespace BookStore.Application.UnitTests;

public sealed class BookQueryValidatorTests
{
    private readonly BookQueryValidator _validator = new();

    [Fact]
    public void Default_query_is_valid() =>
        _validator.TestValidate(new BookQuery()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_must_be_at_least_one(int page) =>
        _validator.TestValidate(new BookQuery { Page = page }).ShouldHaveValidationErrorFor(q => q.Page);

    [Theory]
    [InlineData(0)]
    [InlineData(BookQuery.MaxPageSize + 1)]
    public void Page_size_is_bounded(int size) =>
        _validator.TestValidate(new BookQuery { PageSize = size }).ShouldHaveValidationErrorFor(q => q.PageSize);

    [Fact]
    public void Unknown_language_is_rejected() =>
        _validator.TestValidate(new BookQuery { Language = "de" }).ShouldHaveValidationErrorFor(q => q.Language);

    [Theory]
    [InlineData("ar")]
    [InlineData("fr")]
    [InlineData("en")]
    [InlineData("ber")]
    public void Supported_languages_are_accepted(string language) =>
        _validator.TestValidate(new BookQuery { Language = language }).ShouldNotHaveValidationErrorFor(q => q.Language);

    [Fact]
    public void Max_price_below_min_price_is_rejected() =>
        _validator.TestValidate(new BookQuery { MinPrice = 2000, MaxPrice = 1000 })
            .ShouldHaveValidationErrorFor(q => q.MaxPrice);

    [Fact]
    public void Search_longer_than_100_chars_is_rejected() =>
        _validator.TestValidate(new BookQuery { Search = new string('a', 101) })
            .ShouldHaveValidationErrorFor(q => q.Search);
}

public sealed class LikePatternTests
{
    [Theory]
    [InlineData("camus", "%camus%")]
    [InlineData("  ذاكرة ", "%ذاكرة%")]
    [InlineData("100%", @"%100\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData(@"c:\x", @"%c:\\x%")]
    public void Escapes_wildcards_and_wraps_term(string input, string expected) =>
        Assert.Equal(expected, LikePattern.Contains(input));
}

public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 12, 0)]
    [InlineData(30, 12, 3)]
    [InlineData(24, 12, 2)]
    [InlineData(1, 12, 1)]
    public void Total_pages_rounds_up(int total, int pageSize, int expectedPages) =>
        Assert.Equal(expectedPages, new PagedResult<int>([], 1, pageSize, total).TotalPages);

    [Fact]
    public void Navigation_flags_reflect_position()
    {
        var middle = new PagedResult<int>([], Page: 2, PageSize: 12, TotalCount: 30);

        Assert.True(middle.HasPreviousPage);
        Assert.True(middle.HasNextPage);
        Assert.False(new PagedResult<int>([], 3, 12, 30).HasNextPage);
    }
}

public sealed class BookRulesTests
{
    private static Book NewBook(int price = 1500, int stock = 5) =>
        new("Titre", "titre", "desc", "9780000000011", price, stock, 100, BookLanguages.French, null, "Éditeur");

    [Fact]
    public void Effective_price_is_sale_price_when_on_sale()
    {
        var book = NewBook();
        book.SetPrice(1500, 1200);

        Assert.Equal(1200, book.EffectivePrice);
    }

    [Fact]
    public void Effective_price_is_price_without_sale() => Assert.Equal(1500, NewBook().EffectivePrice);

    [Theory]
    [InlineData(1500)]
    [InlineData(1600)]
    [InlineData(0)]
    public void Sale_price_must_be_positive_and_below_price(int salePrice) =>
        Assert.Throws<DomainException>(() => NewBook().SetPrice(1500, salePrice));

    [Fact]
    public void Price_must_be_positive() => Assert.Throws<DomainException>(() => NewBook(price: 0));

    [Fact]
    public void Stock_cannot_be_negative() => Assert.Throws<DomainException>(() => NewBook(stock: -1));

    [Fact]
    public void Adding_the_same_author_twice_keeps_one()
    {
        var book = NewBook();
        var author = new Author("Albert Camus", "albert-camus");

        book.AddAuthor(author);
        book.AddAuthor(author);

        Assert.Single(book.Authors);
    }
}
