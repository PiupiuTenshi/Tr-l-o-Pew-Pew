namespace PewPew.SharedKernel.Primitives;

public abstract record DomainEvent(DateTimeOffset OccurredAtUtc)
{
    protected DomainEvent() : this(DateTimeOffset.UtcNow)
    {
    }
}
