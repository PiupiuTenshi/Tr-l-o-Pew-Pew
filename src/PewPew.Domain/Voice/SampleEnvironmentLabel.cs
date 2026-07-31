namespace PewPew.Domain.Voice;

/// <summary>
/// User-selected environment label for a voice profile sample.
/// Bounded to a short descriptive string; the aggregate enforces
/// minimum environment diversity before training can proceed.
/// </summary>
public sealed class SampleEnvironmentLabel : IEquatable<SampleEnvironmentLabel>
{
    public const int MaxLabelLength = 50;

    public SampleEnvironmentLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Environment label cannot be empty.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLabelLength)
        {
            throw new ArgumentException(
                $"Environment label must not exceed {MaxLabelLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    public string Value { get; }

    public bool Equals(SampleEnvironmentLabel? other) =>
        other is not null &&
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as SampleEnvironmentLabel);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}
