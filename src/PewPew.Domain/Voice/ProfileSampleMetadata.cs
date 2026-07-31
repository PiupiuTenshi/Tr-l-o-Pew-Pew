namespace PewPew.Domain.Voice;

/// <summary>
/// Non-sensitive metadata for a single voice profile sample.
/// Intentionally carries no raw audio bytes, no embedding data,
/// no file path, and no encryption key material.
/// </summary>
public sealed class ProfileSampleMetadata
{
    public ProfileSampleMetadata(
        ProfileSampleId sampleId,
        SampleEnvironmentLabel environment,
        DateTimeOffset recordedAtUtc,
        double durationSeconds,
        DateTimeOffset expiresAtUtc)
    {
        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be positive.");
        }

        if (expiresAtUtc <= recordedAtUtc)
        {
            throw new ArgumentException("Expiry must be after the recording time.", nameof(expiresAtUtc));
        }

        SampleId = sampleId;
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        RecordedAtUtc = recordedAtUtc;
        DurationSeconds = durationSeconds;
        ExpiresAtUtc = expiresAtUtc;
    }

    public ProfileSampleId SampleId { get; }

    public SampleEnvironmentLabel Environment { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public double DurationSeconds { get; }

    /// <summary>
    /// Whether the embedding has been successfully extracted from
    /// the raw audio, making the raw data eligible for immediate deletion.
    /// </summary>
    public bool IsExtracted { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAtUtc;

    public void MarkExtracted()
    {
        if (IsExtracted)
        {
            return; // idempotent
        }

        IsExtracted = true;
    }
}
