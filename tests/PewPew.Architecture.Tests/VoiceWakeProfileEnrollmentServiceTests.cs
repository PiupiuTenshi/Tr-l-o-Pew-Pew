using PewPew.Application.Voice;
using PewPew.Domain.Voice;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class VoiceWakeProfileEnrollmentServiceTests
{
    [Fact]
    public async Task CaptureIsDeniedUntilExplicitConsentStartsEnrollment()
    {
        var service = new VoiceWakeProfileEnrollmentService(new FakeVault(), "Pew Pew");

        var result = await service.StoreCapturedSampleAsync(
            new byte[] { 1, 2, 3 },
            new SampleEnvironmentLabel("Quiet room"),
            TimeSpan.FromSeconds(3),
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("voice_profile.capture_denied", result.Error.Code);
        Assert.False(service.Snapshot.HasConsent);
        Assert.Equal(0, service.Snapshot.SampleCount);
    }

    [Fact]
    public async Task CancelPurgesVaultAndLeavesProfileInactive()
    {
        var vault = new FakeVault();
        var service = new VoiceWakeProfileEnrollmentService(vault, "Pew Pew");
        var now = DateTimeOffset.UtcNow;
        Assert.True(service.BeginEnrollment(now).IsSuccess);

        var stored = await service.StoreCapturedSampleAsync(
            new byte[] { 4, 5, 6 },
            new SampleEnvironmentLabel("Normal room"),
            TimeSpan.FromSeconds(3),
            now.AddSeconds(1),
            TestContext.Current.CancellationToken);
        Assert.True(stored.IsSuccess);

        var cancelled = await service.CancelEnrollmentAsync(TestContext.Current.CancellationToken);

        Assert.True(cancelled.IsSuccess);
        Assert.Equal(1, vault.PurgeCount);
        Assert.Equal(VoiceWakeProfileStatus.Draft, service.Snapshot.Status);
        Assert.Equal(0, service.Snapshot.SampleCount);
        Assert.False(service.Snapshot.IsReadyForTraining);
    }

    [Fact]
    public async Task WithdrawConsentDuringCollectionPurgesAndTerminatesProfile()
    {
        var vault = new FakeVault();
        var service = new VoiceWakeProfileEnrollmentService(vault, "Pew Pew");
        var now = DateTimeOffset.UtcNow;
        Assert.True(service.BeginEnrollment(now).IsSuccess);

        var withdrawn = await service.WithdrawConsentAndDeleteAsync(now.AddSeconds(1), TestContext.Current.CancellationToken);

        Assert.True(withdrawn.IsSuccess);
        Assert.Equal(1, vault.PurgeCount);
        Assert.Equal(VoiceWakeProfileStatus.Deleted, service.Snapshot.Status);
        Assert.False(service.Snapshot.HasConsent);
    }

    [Fact]
    public async Task ExpiredEnrollmentPurgesCapturedDataAndRejectsSave()
    {
        var vault = new FakeVault();
        var service = new VoiceWakeProfileEnrollmentService(vault, "Pew Pew");
        var now = DateTimeOffset.UtcNow;
        Assert.True(service.BeginEnrollment(now).IsSuccess);

        var result = await service.StoreCapturedSampleAsync(
            new byte[] { 7, 8, 9 },
            new SampleEnvironmentLabel("Headset"),
            TimeSpan.FromSeconds(3),
            now.Add(VoiceWakeProfileEnrollmentService.RawSampleTtl).AddTicks(1),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("voice_profile.capture_expired", result.Error.Code);
        Assert.Equal(1, vault.PurgeCount);
        Assert.Equal(VoiceWakeProfileStatus.Draft, service.Snapshot.Status);
    }

    private sealed class FakeVault : IVoiceProfileSampleVault
    {
        public int PurgeCount { get; private set; }

        public Task<Result<ProfileSampleId>> StoreEncryptedSampleAsync(EntityId profileId, ReadOnlyMemory<byte> sampleData, SampleEnvironmentLabel label, TimeSpan ttl, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(ProfileSampleId.New()));

        public Task<Result> DeleteSampleAsync(EntityId profileId, ProfileSampleId sampleId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<int>> PurgeAllSamplesAsync(EntityId profileId, CancellationToken cancellationToken)
        {
            PurgeCount++;
            return Task.FromResult(Result.Success(0));
        }

        public Task<Result<int>> PurgeExpiredSamplesAsync(EntityId profileId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(0));

        public Task<bool> HasSampleAsync(EntityId profileId, ProfileSampleId sampleId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
