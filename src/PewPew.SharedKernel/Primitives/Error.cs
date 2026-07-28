namespace PewPew.SharedKernel.Primitives;

public sealed record DomainError(string Code, string Message)
{
    public static readonly DomainError None = new(string.Empty, string.Empty);
}
