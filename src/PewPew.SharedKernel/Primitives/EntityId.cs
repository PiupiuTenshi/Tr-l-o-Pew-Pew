namespace PewPew.SharedKernel.Primitives;

public readonly record struct EntityId
{
    public EntityId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Entity ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static EntityId New() => new(Guid.NewGuid());
}
