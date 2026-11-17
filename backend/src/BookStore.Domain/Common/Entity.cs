namespace BookStore.Domain.Common;

/// <summary>Base class for persisted entities with a time-ordered GUID key (v7 sorts well in B-tree indexes).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>Entities whose creation/update times are stamped automatically on SaveChanges.</summary>
public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
