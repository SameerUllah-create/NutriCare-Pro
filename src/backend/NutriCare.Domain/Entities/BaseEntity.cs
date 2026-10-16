namespace NutriCare.Domain.Entities;

/// <summary>
/// Base entity class with common properties for all domain entities
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for the entity
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update timestamp (UTC)
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Domain events that occurred during entity lifecycle
    /// </summary>
    private readonly List<DomainEvent> _domainEvents = new();

    /// <summary>
    /// Get and clear domain events
    /// </summary>
    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    /// <summary>
    /// Clear domain events
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Add domain event
    /// </summary>
    protected void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Check if entity is soft deleted
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;
}

/// <summary>
/// Base class for domain events
/// </summary>
public abstract class DomainEvent
{
    protected DomainEvent()
    {
        DateOccurred = DateTime.UtcNow;
    }

    public DateTime DateOccurred { get; protected set; }
}
