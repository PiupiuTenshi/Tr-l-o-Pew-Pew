using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Voice;

/// <summary>
/// Strongly-typed opaque identifier for a vault sample.
/// Carries no raw audio, file path, or storage implementation detail.
/// </summary>
public readonly record struct ProfileSampleId
{
    public ProfileSampleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Profile sample ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ProfileSampleId New() => new(Guid.NewGuid());
}
