namespace PewPew.SharedKernel.Primitives;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
