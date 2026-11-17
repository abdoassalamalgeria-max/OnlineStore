using BookStore.Application.Features.Auth;
using BookStore.Domain.Catalog;
using BookStore.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookStore.Infrastructure.Persistence.Seed;

/// <summary>
/// Applies pending migrations, ensures the roles and the admin account exist, and inserts the demo
/// catalog when the books table is empty. Idempotent: safe to run on every start.
/// Wilayas come from the migration, not from here.
/// </summary>
public sealed partial class DatabaseInitializer(
    AppDbContext db,
    RoleManager<IdentityRole<Guid>> roles,
    UserManager<ApplicationUser> users,
    IOptions<AdminSeedOptions> adminOptions,
    TimeProvider clock,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(bool seedDemoData, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await EnsureRolesAsync();
        await EnsureAdminAsync();

        if (seedDemoData && !await db.Books.AnyAsync(cancellationToken))
        {
            await SeedCatalogAsync(cancellationToken);
        }
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
            }
        }
    }

    private async Task EnsureAdminAsync()
    {
        var options = adminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            LogAdminSkipped(logger);
            return;
        }

        var admin = await users.FindByEmailAsync(options.Email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = options.Email,
                Email = options.Email,
                EmailConfirmed = true,
                FullName = options.FullName,
                CreatedAt = clock.GetUtcNow(),
            };
            var created = await users.CreateAsync(admin, options.Password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create the admin account: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            }

            LogAdminCreated(logger, options.Email);
        }

        if (!await users.IsInRoleAsync(admin, Roles.Admin))
        {
            await users.AddToRoleAsync(admin, Roles.Admin);
        }
    }

    private async Task SeedCatalogAsync(CancellationToken cancellationToken)
    {
        var categories = CatalogSeedData.Categories.ToDictionary(
            c => c.Slug,
            c => new Category(c.Ar, c.Fr, c.En, c.Slug));
        var authors = CatalogSeedData.Authors.ToDictionary(
            a => a.Slug,
            a => new Author(a.Name, a.Slug));

        var index = 0;
        foreach (var seed in CatalogSeedData.Books)
        {
            var book = new Book(
                seed.Title,
                seed.Slug,
                seed.Description,
                CatalogSeedData.SyntheticIsbn(++index),
                seed.Price,
                seed.Stock,
                seed.PageCount,
                seed.Language,
                new DateOnly(seed.Year, 1, 1),
                CatalogSeedData.PublisherFor(seed.Language));
            book.SetPrice(seed.Price, seed.SalePrice);
            book.SetFeatured(seed.Featured);

            foreach (var slug in seed.Authors)
            {
                book.AddAuthor(authors[slug]);
            }

            foreach (var slug in seed.Categories)
            {
                book.AddCategory(categories[slug]);
            }

            db.Books.Add(book);
        }

        await db.SaveChangesAsync(cancellationToken);
        LogSeeded(logger, CatalogSeedData.Books.Length, authors.Count, categories.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded demo catalog: {Books} books, {Authors} authors, {Categories} categories")]
    private static partial void LogSeeded(ILogger logger, int books, int authors, int categories);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created admin account {Email}")]
    private static partial void LogAdminCreated(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed:Admin:Email/Password not configured; no admin account was created")]
    private static partial void LogAdminSkipped(ILogger logger);
}
