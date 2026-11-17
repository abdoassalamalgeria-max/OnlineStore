using Microsoft.AspNetCore.Identity;

namespace BookStore.Infrastructure.Identity;

/// <summary>
/// The Identity user. Lives in Infrastructure because it inherits from IdentityUser; the rest of
/// the domain refers to users only by their Guid id.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public string FullName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
