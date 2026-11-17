using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookStore.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` so migrations can be created without running the Api or having secrets.
/// `migrations add` never connects; for `database update` pass --connection explicitly.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(options, "Host=localhost;Database=bookstore_design");
        return new AppDbContext(options.Options);
    }
}
