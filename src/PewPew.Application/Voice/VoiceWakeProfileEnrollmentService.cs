using PewPew.Domain.Voice;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.Voice;

/// <summary>
/// Coordinates explicit, local-only voice-profile enrollment. It never exposes
/// raw audio, storage paths, keys, or embeddings to callers and it never
/// activates a profile; matching and validation belong to P02-T21.
/// </summary>
public sealed class VoiceWakeProfileEnrollmentService
{
    public static readonly TimeSpan RawSampleTtl = TimeSpan.FromMinutes(15);

    private readonly IVoiceProfileSampleVault _vault;
    private VoiceWakeProfile _profile;

    public VoiceWakeProfileEnrollmentService(IVoiceProfileSampleVault vault, string wakePhrase)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _profile = CreateProfile(wakePhrase);
    }

    public VoiceWakeProfileEnrollmentSnapshot Snapshot => new(
        _profile.Status,
        _profile.ConsentRecordedAtUtc is not null,
        _profile.Samples.Count,
        VoiceWakeProfile.MaxSamples,
        _profile.CanProceedToTraining());

    public Result BeginEnrollment(DateTimeOffset now)
    {
        try
        {
            if (_profile.Status == VoiceWakeProfileStatus.Draft)
            {
                _profile.RecordConsent(now);
                _profile.StartSampleCollection(now, RawSampleTtl);
            }
            else if (_profile.Status != VoiceWakeProfileStatus.CollectingSamples)
            {
                return Result.Failure(new DomainError("voice_profile.enrollment_not_available", "Voice-profile enrollment is not available in the current state."));
            }

            return Result.Success();
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure(new DomainError("voice_profile.enrollment_denied", exception.Message));
        }
    }

    public async Task<Result> StoreCapturedSampleAsync(
        ReadOnlyMemory<byte> wavBytes,
        SampleEnvironmentLabel environment,
        TimeSpan duration,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (_profile.Status != VoiceWakeProfileStatus.CollectingSamples)
        {
            return Result.Failure(new DomainError("voice_profile.capture_denied", "Explicit enrollment consent is required before saving a voice sample."));
        }

        if (duration <= TimeSpan.Zero)
        {
            return Result.Failure(new DomainError("voice_profile.invalid_duration", "Captured voice sample duration must be positive."));
        }

        if (_profile.RawSampleRetentionExpiresAtUtc is null || now >= _profile.RawSampleRetentionExpiresAtUtc.Value)
        {
            await CancelEnrollmentAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(new DomainError("voice_profile.capture_expired", "Enrollment expired and temporary samples were deleted."));
        }

        if (_profile.Samples.Count >= VoiceWakeProfile.MaxSamples)
        {
            return Result.Failure(new DomainError("voice_profile.sample_quota_reached", "The voice-profile sample limit has been reached."));
        }

        var stored = await _vault.StoreEncryptedSampleAsync(
            _profile.Id,
            wavBytes,
            environment,
            RawSampleTtl,
            cancellationToken).ConfigureAwait(false);
        if (stored.IsFailure)
        {
            return Result.Failure(stored.Error);
        }

        try
        {
            _profile.AddSampleMetadata(new ProfileSampleMetadata(
                stored.Value,
                environment,
                now,
                duration.TotalSeconds,
                now.Add(RawSampleTtl)));
            return Result.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await _vault.DeleteSampleAsync(_profile.Id, stored.Value, cancellationToken).ConfigureAwait(false);
            return Result.Failure(new DomainError("voice_profile.capture_not_saved", "The captured voice sample was discarded."));
        }
    }

    public async Task<Result> CancelEnrollmentAsync(CancellationToken cancellationToken)
    {
        var purge = await _vault.PurgeAllSamplesAsync(_profile.Id, cancellationToken).ConfigureAwait(false);
        if (purge.IsFailure)
        {
            return Result.Failure(purge.Error);
        }

        if (_profile.Status == VoiceWakeProfileStatus.CollectingSamples)
        {
            _profile.CancelSampleCollection();
        }

        return Result.Success();
    }

    public async Task<Result> WithdrawConsentAndDeleteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            _profile.RevokeVoiceConsent(now);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure(new DomainError("voice_profile.revoke_denied", exception.Message));
        }

        var purge = await _vault.PurgeAllSamplesAsync(_profile.Id, cancellationToken).ConfigureAwait(false);
        if (purge.IsFailure)
        {
            return Result.Failure(purge.Error);
        }

        _profile.Purge(now);
        return Result.Success();
    }

    public void ResetAfterDeletion(string wakePhrase)
    {
        if (_profile.Status != VoiceWakeProfileStatus.Deleted)
        {
            throw new InvalidOperationException("A voice profile must be deleted before it can be reset.");
        }

        _profile = CreateProfile(wakePhrase);
    }

    private static VoiceWakeProfile CreateProfile(string wakePhrase) => new(EntityId.New(), wakePhrase);
}

public sealed record VoiceWakeProfileEnrollmentSnapshot(
    VoiceWakeProfileStatus Status,
    bool HasConsent,
    int SampleCount,
    int SampleLimit,
    bool IsReadyForTraining);
