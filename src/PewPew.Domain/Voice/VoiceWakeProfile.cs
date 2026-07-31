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
/// Sample metadata tracks opaque IDs, environment labels and TTL only.
/// </summary>
public sealed class VoiceWakeProfile
{
    /// <summary>Maximum accepted samples per T18 spec.</summary>
    public const int MaxSamples = 12;

    /// <summary>Minimum samples required before training may begin.</summary>
    public const int MinSamplesForTraining = 8;

    /// <summary>Minimum distinct environment labels required before training.</summary>
    public const int MinEnvironments = 3;

    private readonly List<ProfileSampleMetadata> _samples = [];

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

    /// <summary>
    /// Non-sensitive metadata for tracked samples. Contains no raw audio,
    /// embedding, file path, or key material.
    /// </summary>
    public IReadOnlyList<ProfileSampleMetadata> Samples => _samples.AsReadOnly();

    // ── Existing lifecycle transitions ────────────────────────────────

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
        _samples.Clear();
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
        _samples.Clear();
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
        _samples.Clear();
        ConsentRevokedAtUtc = now;
        Status = VoiceWakeProfileStatus.Revoked;
    }

    public void Purge(DateTimeOffset now)
    {
        Require(VoiceWakeProfileStatus.Revoked);
        ClearRawSampleRetention();
        _samples.Clear();
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

    // ── Sample metadata management ───────────────────────────────────

    /// <summary>
    /// Registers non-sensitive metadata for a sample that has been
    /// encrypted and stored in the vault. The aggregate enforces consent,
    /// status, and quota invariants.
    /// </summary>
    public void AddSampleMetadata(ProfileSampleMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Require(VoiceWakeProfileStatus.CollectingSamples);

        if (ConsentRecordedAtUtc is null)
        {
            throw new InvalidOperationException("Voice-profile consent is required before adding samples.");
        }

        if (_samples.Count >= MaxSamples)
        {
            throw new InvalidOperationException(
                $"Maximum sample count ({MaxSamples}) reached. Remove expired or unnecessary samples first.");
        }

        if (_samples.Any(s => s.SampleId.Value == metadata.SampleId.Value))
        {
            return; // idempotent: same sample already tracked
        }

        _samples.Add(metadata);
    }

    /// <summary>
    /// Marks a sample's embedding as extracted, making the raw audio
    /// eligible for immediate deletion from the vault.
    /// </summary>
    /// <returns>True if the sample was found and marked; false if not found.</returns>
    public bool MarkSampleExtracted(ProfileSampleId sampleId)
    {
        var sample = _samples.FirstOrDefault(s => s.SampleId.Value == sampleId.Value);
        if (sample is null)
        {
            return false;
        }

        sample.MarkExtracted();
        return true;
    }

    /// <summary>
    /// Removes sample metadata by ID. Idempotent: returns true if
    /// found and removed, false if not present.
    /// </summary>
    public bool RemoveSampleMetadata(ProfileSampleId sampleId)
    {
        return _samples.RemoveAll(s => s.SampleId.Value == sampleId.Value) > 0;
    }

    /// <summary>
    /// Returns metadata for samples that have exceeded their TTL.
    /// Callers should delete the corresponding vault entries and
    /// then call <see cref="RemoveSampleMetadata"/> for each.
    /// </summary>
    public IReadOnlyList<ProfileSampleMetadata> GetExpiredSamples(DateTimeOffset now)
    {
        return _samples.Where(s => s.IsExpired(now)).ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns metadata for samples whose embedding has been extracted,
    /// making their raw audio eligible for immediate deletion.
    /// </summary>
    public IReadOnlyList<ProfileSampleMetadata> GetExtractedSamples()
    {
        return _samples.Where(s => s.IsExtracted).ToList().AsReadOnly();
    }

    /// <summary>
    /// Checks whether the profile has enough samples across enough
    /// distinct environments to proceed to training.
    /// </summary>
    public bool CanProceedToTraining()
    {
        if (_samples.Count < MinSamplesForTraining)
        {
            return false;
        }

        var distinctEnvironments = _samples
            .Select(s => s.Environment)
            .Distinct()
            .Count();

        return distinctEnvironments >= MinEnvironments;
    }

    // ── Private helpers ──────────────────────────────────────────────

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

