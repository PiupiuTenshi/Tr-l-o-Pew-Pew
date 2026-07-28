using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Voice;

public enum VoiceWakeProfileStatus
{
    Draft,
    CollectingSamples,
    Training,
    Validating,
    Active,
    Disabled,
    Retraining,
    Revoked,
    Deleted
}

/// <summary>
/// Lifecycle and retention metadata for a locally trained wake profile.
/// Raw audio and feature bytes intentionally never enter this aggregate.
/// </summary>
public sealed class VoiceWakeProfile
{
    public VoiceWakeProfile(EntityId id, string wakePhrase)
    {
        if (string.IsNullOrWhiteSpace(wakePhrase))
        {
            throw new ArgumentException("A wake phrase is required.", nameof(wakePhrase));
        }

        Id = id;
        WakePhrase = wakePhrase.Trim();
    }

    public EntityId Id { get; }

    public string WakePhrase { get; }

    public VoiceWakeProfileStatus Status { get; private set; } = VoiceWakeProfileStatus.Draft;

    public DateTimeOffset? ConsentRecordedAtUtc { get; private set; }

    public DateTimeOffset? ConsentRevokedAtUtc { get; private set; }

    public DateTimeOffset? RawSampleRetentionExpiresAtUtc { get; private set; }

    public string? FeatureReference { get; private set; }

    public bool CanDetectWakeWord => Status == VoiceWakeProfileStatus.Active;

    public void RecordConsent(DateTimeOffset consentedAtUtc)
    {
        Require(VoiceWakeProfileStatus.Draft);
        ConsentRecordedAtUtc = consentedAtUtc;
    }

    public void StartSampleCollection(DateTimeOffset now, TimeSpan rawSampleTtl)
    {
        Require(VoiceWakeProfileStatus.Draft);
        if (ConsentRecordedAtUtc is null)
        {
            throw new InvalidOperationException("Voice-profile consent is required before collecting samples.");
        }

        SetRawSampleRetention(now, rawSampleTtl);
        Status = VoiceWakeProfileStatus.CollectingSamples;
    }

    public void CancelSampleCollection()
    {
        Require(VoiceWakeProfileStatus.CollectingSamples);
        ClearRawSampleRetention();
        Status = VoiceWakeProfileStatus.Draft;
    }

    public void CompleteSampleCollection(DateTimeOffset now)
    {
        Require(VoiceWakeProfileStatus.CollectingSamples);
        EnsureRawSampleRetentionIsCurrent(now);
        Status = VoiceWakeProfileStatus.Training;
    }

    public void CompleteTraining(string featureReference)
    {
        Require(VoiceWakeProfileStatus.Training, VoiceWakeProfileStatus.Retraining);
        if (string.IsNullOrWhiteSpace(featureReference))
        {
            throw new ArgumentException("A local feature reference is required.", nameof(featureReference));
        }

        FeatureReference = featureReference.Trim();
        ClearRawSampleRetention();
        Status = VoiceWakeProfileStatus.Validating;
    }

    public void FailTraining()
    {
        Require(VoiceWakeProfileStatus.Training);
        ClearRawSampleRetention();
        Status = VoiceWakeProfileStatus.Draft;
    }

    public void RequireMoreSamples(DateTimeOffset now, TimeSpan rawSampleTtl)
    {
        Require(VoiceWakeProfileStatus.Validating);
        SetRawSampleRetention(now, rawSampleTtl);
        Status = VoiceWakeProfileStatus.CollectingSamples;
    }

    public void PassValidation() => Move(VoiceWakeProfileStatus.Validating, VoiceWakeProfileStatus.Active);

    public void Disable() => Move(VoiceWakeProfileStatus.Active, VoiceWakeProfileStatus.Disabled);

    public void Enable() => Move(VoiceWakeProfileStatus.Disabled, VoiceWakeProfileStatus.Active);

    public void StartRetraining()
    {
        Require(VoiceWakeProfileStatus.Active, VoiceWakeProfileStatus.Disabled);
        Status = VoiceWakeProfileStatus.Retraining;
    }

    public void RevokeVoiceConsent(DateTimeOffset now)
    {
        Require(VoiceWakeProfileStatus.Active, VoiceWakeProfileStatus.Disabled, VoiceWakeProfileStatus.Retraining);
        ClearRawSampleRetention();
        ConsentRevokedAtUtc = now;
        Status = VoiceWakeProfileStatus.Revoked;
    }

    public void Purge(DateTimeOffset now)
    {
        Require(VoiceWakeProfileStatus.Revoked);
        ClearRawSampleRetention();
        FeatureReference = null;
        ConsentRecordedAtUtc = null;
        ConsentRevokedAtUtc = now;
        Status = VoiceWakeProfileStatus.Deleted;
    }

    public bool ExpireRawSampleRetention(DateTimeOffset now)
    {
        if (RawSampleRetentionExpiresAtUtc is null || now < RawSampleRetentionExpiresAtUtc)
        {
            return false;
        }

        ClearRawSampleRetention();
        return true;
    }

    private void SetRawSampleRetention(DateTimeOffset now, TimeSpan rawSampleTtl)
    {
        if (rawSampleTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(rawSampleTtl), "Raw sample retention must have a positive TTL.");
        }

        RawSampleRetentionExpiresAtUtc = now.Add(rawSampleTtl);
    }

    private void EnsureRawSampleRetentionIsCurrent(DateTimeOffset now)
    {
        if (RawSampleRetentionExpiresAtUtc is null || now >= RawSampleRetentionExpiresAtUtc)
        {
            ClearRawSampleRetention();
            throw new InvalidOperationException("Temporary raw sample retention has expired.");
        }
    }

    private void ClearRawSampleRetention() => RawSampleRetentionExpiresAtUtc = null;

    private void Move(VoiceWakeProfileStatus expected, VoiceWakeProfileStatus next)
    {
        Require(expected);
        Status = next;
    }

    private void Require(params VoiceWakeProfileStatus[] expected)
    {
        if (!expected.Contains(Status))
        {
            throw new InvalidOperationException($"Voice wake profile transition denied from {Status}.");
        }
    }
}
