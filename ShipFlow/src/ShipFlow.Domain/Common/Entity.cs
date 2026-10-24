namespace ShipFlow.Domain.Common;

/// <summary>Base type for entities with a client-independent, server-generated identity.</summary>
public abstract class Entity
{
    /// <summary>Used only by the persistence layer to materialize the entity; never call it from domain or application code.</summary>
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Id = Guard.NotEmpty(id, nameof(id));
    }

    public Guid Id { get; }
}
