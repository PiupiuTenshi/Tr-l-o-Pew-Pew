using PewPew.Domain.Voice;
using PewPew.Infrastructure.Voice;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Tests for the consent-bound encrypted profile sample vault lifecycle.
/// Covers consent guard, quota, TTL/expiry, cancel, revoke/purge,
/// idempotency, no-raw-payload, and illegal-transition paths.
/// No real voice data or ONNX inference — only synthetic byte fixtures.
/// </summary>
public sealed class VoiceProfileSampleVaultTests
{
    private static readonly TimeSpan RawSampleTtl = TimeSpan.FromMinutes(15);

    // ── 1. Consent guard ─────────────────────────────────────────────

    [Fact]
    public void AddSampleMetadataRequiresCollectingSamplesStatus()
    {
        var profile = NewProfile();
        var metadata = CreateSampleMetadata();

        // Draft status — not collecting
        var ex = Assert.Throws<InvalidOperationException>(() => profile.AddSampleMetadata(metadata));
        Assert.Contains("denied", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoreWithoutConsentIsRejectedByDomainGuard()
    {
        var profile = NewProfile();

        // No consent recorded → StartSampleCollection throws
        Assert.Throws<InvalidOperationException>(
            () => profile.StartSampleCollection(DateTimeOffset.UtcNow, RawSampleTtl));
    }

    // ── 2. Quota ─────────────────────────────────────────────────────

    [Fact]
    public void AddSampleMetadataRejectsWhenMaxSamplesReached()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        // Fill to max
        for (var i = 0; i < VoiceWakeProfile.MaxSamples; i++)
        {
            profile.AddSampleMetadata(CreateSampleMetadata(now));
        }

        Assert.Equal(VoiceWakeProfile.MaxSamples, profile.Samples.Count);

        // 13th sample must fail
        var ex = Assert.Throws<InvalidOperationException>(
            () => profile.AddSampleMetadata(CreateSampleMetadata(now)));
        Assert.Contains("Maximum sample count", ex.Message);
    }

    // ── 3. TTL / expiry ──────────────────────────────────────────────

    [Fact]
    public void GetExpiredSamplesReturnsOnlyExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        var shortTtl = CreateSampleMetadata(now, ttl: TimeSpan.FromMinutes(1));
        var longTtl = CreateSampleMetadata(now, ttl: TimeSpan.FromMinutes(30));

        profile.AddSampleMetadata(shortTtl);
        profile.AddSampleMetadata(longTtl);

        var expired = profile.GetExpiredSamples(now.AddMinutes(5));

        Assert.Single(expired);
        Assert.Equal(shortTtl.SampleId, expired[0].SampleId);
    }

    // ── 4. Cancel cleanup ────────────────────────────────────────────

    [Fact]
    public void CancelSampleCollectionClearsAllSampleMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        profile.AddSampleMetadata(CreateSampleMetadata(now));
        profile.AddSampleMetadata(CreateSampleMetadata(now));

        Assert.Equal(2, profile.Samples.Count);

        profile.CancelSampleCollection();

        Assert.Empty(profile.Samples);
        Assert.Equal(VoiceWakeProfileStatus.Draft, profile.Status);
    }

    // ── 5. Revoke / purge ────────────────────────────────────────────

    [Fact]
    public void RevokeAndPurgeClearAllSamplesAndFeatureMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = ActiveProfileWithSamples(now);

        Assert.NotEmpty(profile.Samples);
        Assert.NotNull(profile.FeatureReference);

        profile.RevokeVoiceConsent(now.AddMinutes(2));

        Assert.Empty(profile.Samples);
        Assert.Equal(VoiceWakeProfileStatus.Revoked, profile.Status);

        profile.Purge(now.AddMinutes(3));

        Assert.Empty(profile.Samples);
        Assert.Null(profile.FeatureReference);
        Assert.Equal(VoiceWakeProfileStatus.Deleted, profile.Status);
    }

    // ── 6. Idempotent operations ─────────────────────────────────────

    [Fact]
    public void AddSampleMetadataIsDuplicateIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);
        var metadata = CreateSampleMetadata(now);

        profile.AddSampleMetadata(metadata);
        profile.AddSampleMetadata(metadata); // same ID — idempotent

        Assert.Single(profile.Samples);
    }

    [Fact]
    public void RemoveSampleMetadataIsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);
        var unknownId = ProfileSampleId.New();

        // Removing non-existent sample returns false, no exception
        var result = profile.RemoveSampleMetadata(unknownId);

        Assert.False(result);
        Assert.Empty(profile.Samples);
    }

    // ── 7. No raw payload in domain metadata ─────────────────────────

    [Fact]
    public void ProfileSampleMetadataContainsNoRawAudioOrPath()
    {
        var metadata = CreateSampleMetadata();

        // ProfileSampleMetadata has no byte[], Stream, or path properties
        var properties = typeof(ProfileSampleMetadata).GetProperties();
        foreach (var prop in properties)
        {
            Assert.NotEqual(typeof(byte[]), prop.PropertyType);
            Assert.NotEqual(typeof(Stream), prop.PropertyType);
            Assert.NotEqual(typeof(ReadOnlyMemory<byte>), prop.PropertyType);
        }

        // Verify the metadata carries only expected non-sensitive fields
        Assert.NotEqual(Guid.Empty, metadata.SampleId.Value);
        Assert.NotNull(metadata.Environment);
        Assert.True(metadata.DurationSeconds > 0);
    }

    // ── 8. Illegal transition — Draft ────────────────────────────────

    [Fact]
    public void AddSampleMetadataRejectedInDraftStatus()
    {
        var profile = NewProfile();
        var metadata = CreateSampleMetadata();

        var ex = Assert.Throws<InvalidOperationException>(() => profile.AddSampleMetadata(metadata));
        Assert.Contains("denied", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(VoiceWakeProfileStatus.Draft, profile.Status);
    }

    // ── 9. Illegal transition — Deleted (terminal) ───────────────────

    [Fact]
    public void AddSampleMetadataRejectedInDeletedStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = ActiveProfileWithSamples(now);

        profile.RevokeVoiceConsent(now.AddMinutes(1));
        profile.Purge(now.AddMinutes(2));

        Assert.Equal(VoiceWakeProfileStatus.Deleted, profile.Status);

        var ex = Assert.Throws<InvalidOperationException>(
            () => profile.AddSampleMetadata(CreateSampleMetadata()));
        Assert.Contains("denied", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 10. MarkSampleExtracted — unknown ID ─────────────────────────

    [Fact]
    public void MarkSampleExtractedReturnsFalseForUnknownId()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        profile.AddSampleMetadata(CreateSampleMetadata(now));

        var result = profile.MarkSampleExtracted(ProfileSampleId.New());

        Assert.False(result);
    }

    [Fact]
    public void MarkSampleExtractedIsIdempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);
        var metadata = CreateSampleMetadata(now);

        profile.AddSampleMetadata(metadata);

        Assert.True(profile.MarkSampleExtracted(metadata.SampleId));
        Assert.True(profile.MarkSampleExtracted(metadata.SampleId)); // idempotent

        Assert.True(profile.Samples[0].IsExtracted);
    }

    // ── 11. Environment diversity ────────────────────────────────────

    [Fact]
    public void CanProceedToTrainingRequiresMinimumEnvironments()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        // Add 8 samples but only 2 environments
        var env1 = new SampleEnvironmentLabel("quiet room");
        var env2 = new SampleEnvironmentLabel("headset");

        for (var i = 0; i < 4; i++)
        {
            profile.AddSampleMetadata(CreateSampleMetadata(now, environment: env1));
        }

        for (var i = 0; i < 4; i++)
        {
            profile.AddSampleMetadata(CreateSampleMetadata(now, environment: env2));
        }

        Assert.Equal(8, profile.Samples.Count);
        Assert.False(profile.CanProceedToTraining()); // only 2 envs, need 3
    }

    // ── 12. Training guard ───────────────────────────────────────────

    [Fact]
    public void CanProceedToTrainingReturnsFalseWithInsufficientSamples()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        // Add only 3 samples (need 8)
        profile.AddSampleMetadata(CreateSampleMetadata(now, environment: new SampleEnvironmentLabel("quiet room")));
        profile.AddSampleMetadata(CreateSampleMetadata(now, environment: new SampleEnvironmentLabel("headset")));
        profile.AddSampleMetadata(CreateSampleMetadata(now, environment: new SampleEnvironmentLabel("normal room")));

        Assert.Equal(3, profile.Samples.Count);
        Assert.False(profile.CanProceedToTraining());
    }

    [Fact]
    public void CanProceedToTrainingReturnsTrueWhenRequirementsMet()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        var envs = new[]
        {
            new SampleEnvironmentLabel("quiet room"),
            new SampleEnvironmentLabel("headset"),
            new SampleEnvironmentLabel("normal room")
        };

        // Add 8+ samples across 3+ environments
        for (var i = 0; i < 9; i++)
        {
            profile.AddSampleMetadata(CreateSampleMetadata(now, environment: envs[i % 3]));
        }

        Assert.Equal(9, profile.Samples.Count);
        Assert.True(profile.CanProceedToTraining());
    }

    // ── 13. FailTraining clears samples ──────────────────────────────

    [Fact]
    public void FailTrainingClearsSampleMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        profile.AddSampleMetadata(CreateSampleMetadata(now));
        profile.CompleteSampleCollection(now.AddMinutes(1));

        Assert.Equal(VoiceWakeProfileStatus.Training, profile.Status);
        Assert.NotEmpty(profile.Samples);

        profile.FailTraining();

        Assert.Empty(profile.Samples);
        Assert.Equal(VoiceWakeProfileStatus.Draft, profile.Status);
    }

    // ── 14. GetExtractedSamples ──────────────────────────────────────

    [Fact]
    public void GetExtractedSamplesReturnsOnlyExtractedEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        var sample1 = CreateSampleMetadata(now);
        var sample2 = CreateSampleMetadata(now);

        profile.AddSampleMetadata(sample1);
        profile.AddSampleMetadata(sample2);

        profile.MarkSampleExtracted(sample1.SampleId);

        var extracted = profile.GetExtractedSamples();

        Assert.Single(extracted);
        Assert.Equal(sample1.SampleId, extracted[0].SampleId);
    }

    // ── 15. EncryptedLocalVoiceProfileSampleVault Infrastructure tests ───────

    [Fact]
    public async Task EncryptedVaultStoresDeletesAndPurgesEncryptedSamples()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pewpew-vault-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var vault = new EncryptedLocalVoiceProfileSampleVault(tempDir);
            var profileId = EntityId.New();
            var label = new SampleEnvironmentLabel("quiet room");
            byte[] syntheticAudio = [0x01, 0x02, 0x03, 0x04, 0x05];
            var ct = TestContext.Current.CancellationToken;

            // 1. Store
            var storeResult = await vault.StoreEncryptedSampleAsync(
                profileId, syntheticAudio, label, TimeSpan.FromMinutes(10), ct);
            Assert.True(storeResult.IsSuccess);
            var sampleId = storeResult.Value;

            // 2. HasSample
            var hasSample = await vault.HasSampleAsync(profileId, sampleId, ct);
            Assert.True(hasSample);

            // 3. Delete
            var deleteResult = await vault.DeleteSampleAsync(profileId, sampleId, ct);
            Assert.True(deleteResult.IsSuccess);

            hasSample = await vault.HasSampleAsync(profileId, sampleId, ct);
            Assert.False(hasSample);

            // 4. Store another and PurgeAll
            var storeResult2 = await vault.StoreEncryptedSampleAsync(
                profileId, syntheticAudio, label, TimeSpan.FromMinutes(10), ct);
            Assert.True(storeResult2.IsSuccess);

            var purgeResult = await vault.PurgeAllSamplesAsync(profileId, ct);
            Assert.True(purgeResult.IsSuccess);
            Assert.Equal(1, purgeResult.Value);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EncryptedVaultPurgesExpiredSamples()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pewpew-vault-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var vault = new EncryptedLocalVoiceProfileSampleVault(tempDir);
            var profileId = EntityId.New();
            var label = new SampleEnvironmentLabel("quiet room");
            byte[] syntheticAudio = [0x01, 0x02, 0x03];
            var ct = TestContext.Current.CancellationToken;

            // Store sample expiring in 1 ms
            var storeResult = await vault.StoreEncryptedSampleAsync(
                profileId, syntheticAudio, label, TimeSpan.FromMilliseconds(1), ct);
            Assert.True(storeResult.IsSuccess);

            await Task.Delay(10, ct);

            var now = DateTimeOffset.UtcNow;
            var purgeResult = await vault.PurgeExpiredSamplesAsync(profileId, now, ct);
            Assert.True(purgeResult.IsSuccess);
            Assert.Equal(1, purgeResult.Value);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }


    // ── Helpers ──────────────────────────────────────────────────────

    private static VoiceWakeProfile NewProfile() => new(EntityId.New(), "Hey Pew Pew");

    private static VoiceWakeProfile CollectingProfile(DateTimeOffset now)
    {
        var profile = NewProfile();
        profile.RecordConsent(now);
        profile.StartSampleCollection(now, RawSampleTtl);
        return profile;
    }

    private static VoiceWakeProfile ActiveProfileWithSamples(DateTimeOffset now)
    {
        var profile = CollectingProfile(now);

        var envs = new[]
        {
            new SampleEnvironmentLabel("quiet room"),
            new SampleEnvironmentLabel("headset"),
            new SampleEnvironmentLabel("normal room")
        };

        for (var i = 0; i < VoiceWakeProfile.MinSamplesForTraining; i++)
        {
            profile.AddSampleMetadata(CreateSampleMetadata(now, environment: envs[i % 3]));
        }

        profile.CompleteSampleCollection(now.AddMinutes(1));
        profile.CompleteTraining("local-feature-v1");
        profile.PassValidation();
        return profile;
    }

    private static ProfileSampleMetadata CreateSampleMetadata(
        DateTimeOffset? now = null,
        TimeSpan? ttl = null,
        SampleEnvironmentLabel? environment = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var sampleTtl = ttl ?? RawSampleTtl;

        return new ProfileSampleMetadata(
            ProfileSampleId.New(),
            environment ?? new SampleEnvironmentLabel("test-environment"),
            timestamp,
            durationSeconds: 4.0,
            expiresAtUtc: timestamp.Add(sampleTtl));
    }
}
