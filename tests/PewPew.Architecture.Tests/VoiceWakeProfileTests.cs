using PewPew.Domain.Voice;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class VoiceWakeProfileTests
{
    private static readonly TimeSpan RawSampleTtl = TimeSpan.FromMinutes(5);

    [Fact]
    public void ConsentAndSuccessfulLocalTrainingActivateTheProfile()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = NewProfile();

        profile.RecordConsent(now);
        profile.StartSampleCollection(now, RawSampleTtl);
        profile.CompleteSampleCollection(now.AddMinutes(1));
        profile.CompleteTraining("local-feature-v1");
        profile.PassValidation();

        Assert.Equal(VoiceWakeProfileStatus.Active, profile.Status);
        Assert.True(profile.CanDetectWakeWord);
        Assert.Equal("local-feature-v1", profile.FeatureReference);
        Assert.Null(profile.RawSampleRetentionExpiresAtUtc);
    }

    [Fact]
    public void SampleCollectionRequiresSeparateConsent()
    {
        var profile = NewProfile();

        Assert.Throws<InvalidOperationException>(() => profile.StartSampleCollection(DateTimeOffset.UtcNow, RawSampleTtl));
        Assert.Equal(VoiceWakeProfileStatus.Draft, profile.Status);
    }

    [Fact]
    public void ExpiredRawSampleRetentionIsClearedAndCannotTrain()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = CollectingProfile(now);

        var cleared = profile.ExpireRawSampleRetention(now.Add(RawSampleTtl));

        Assert.True(cleared);
        Assert.Null(profile.RawSampleRetentionExpiresAtUtc);
        Assert.Throws<InvalidOperationException>(() => profile.CompleteSampleCollection(now.Add(RawSampleTtl)));
        Assert.Equal(VoiceWakeProfileStatus.CollectingSamples, profile.Status);
    }

    [Fact]
    public void CancellingSampleCollectionClearsTemporaryRetention()
    {
        var profile = CollectingProfile(DateTimeOffset.UtcNow);

        profile.CancelSampleCollection();

        Assert.Equal(VoiceWakeProfileStatus.Draft, profile.Status);
        Assert.Null(profile.RawSampleRetentionExpiresAtUtc);
    }

    [Fact]
    public void RevokedProfileCannotBeReenabledAndPurgeClearsFeatureMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = ActiveProfile(now);

        profile.RevokeVoiceConsent(now.AddMinutes(2));

        Assert.Equal(VoiceWakeProfileStatus.Revoked, profile.Status);
        Assert.False(profile.CanDetectWakeWord);
        Assert.Throws<InvalidOperationException>(profile.Enable);

        profile.Purge(now.AddMinutes(3));

        Assert.Equal(VoiceWakeProfileStatus.Deleted, profile.Status);
        Assert.Null(profile.FeatureReference);
        Assert.Null(profile.ConsentRecordedAtUtc);
        Assert.Throws<InvalidOperationException>(() => profile.StartSampleCollection(now.AddMinutes(4), RawSampleTtl));
    }

    [Fact]
    public void DisabledProfileCanBeRetrainedAndActivatedAgain()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = ActiveProfile(now);

        profile.Disable();
        profile.StartRetraining();
        profile.CompleteTraining("local-feature-v2");
        profile.PassValidation();

        Assert.Equal(VoiceWakeProfileStatus.Active, profile.Status);
        Assert.Equal("local-feature-v2", profile.FeatureReference);
    }

    private static VoiceWakeProfile NewProfile() => new(EntityId.New(), "Hey Pew Pew");

    private static VoiceWakeProfile CollectingProfile(DateTimeOffset now)
    {
        var profile = NewProfile();
        profile.RecordConsent(now);
        profile.StartSampleCollection(now, RawSampleTtl);
        return profile;
    }

    private static VoiceWakeProfile ActiveProfile(DateTimeOffset now)
    {
        var profile = CollectingProfile(now);
        profile.CompleteSampleCollection(now.AddMinutes(1));
        profile.CompleteTraining("local-feature-v1");
        profile.PassValidation();
        return profile;
    }
}
