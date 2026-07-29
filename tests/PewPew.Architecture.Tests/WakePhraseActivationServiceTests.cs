using PewPew.Application.Voice;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class WakePhraseActivationServiceTests
{
    [Fact]
    public async Task DetectedWakePhraseStartsListeningOnlyThroughActivator()
    {
        var activator = new FakeActivator(started: true);
        var service = new WakePhraseActivationService(new FakeVad(true), new FakeDetector(WakePhraseDetectionStatus.Detected), activator);

        var result = await service.TryActivateAsync(new MemoryStream([1, 2]), TestContext.Current.CancellationToken);

        Assert.Equal(WakePhraseActivationStatus.ListeningStarted, result.Status);
        Assert.True(activator.WasCalled);
    }

    [Fact]
    public async Task SilenceDoesNotInvokeWhisperOrStartListening()
    {
        var detector = new FakeDetector(WakePhraseDetectionStatus.Detected);
        var activator = new FakeActivator(started: true);
        var service = new WakePhraseActivationService(new FakeVad(false), detector, activator);

        var result = await service.TryActivateAsync(new MemoryStream(), TestContext.Current.CancellationToken);

        Assert.Equal(WakePhraseActivationStatus.NotDetected, result.Status);
        Assert.False(detector.WasCalled);
        Assert.False(activator.WasCalled);
    }

    [Fact]
    public async Task UnmatchedOrUnavailableWakePhraseDoesNotStartListening()
    {
        var activator = new FakeActivator(started: true);
        var service = new WakePhraseActivationService(new FakeVad(true), new FakeDetector(WakePhraseDetectionStatus.Unavailable), activator);

        var result = await service.TryActivateAsync(new MemoryStream([1]), TestContext.Current.CancellationToken);

        Assert.Equal(WakePhraseActivationStatus.NotDetected, result.Status);
        Assert.False(activator.WasCalled);
    }

    [Fact]
    public async Task CancellationIsTerminalAndDoesNotStartListening()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var activator = new FakeActivator(started: true);
        var service = new WakePhraseActivationService(new FakeVad(true), new FakeDetector(WakePhraseDetectionStatus.Detected), activator);

        var result = await service.TryActivateAsync(new MemoryStream([1]), cancellation.Token);

        Assert.Equal(WakePhraseActivationStatus.Cancelled, result.Status);
        Assert.False(activator.WasCalled);
    }

    private sealed class FakeVad(bool hasSpeech) : ILocalVoiceActivityGate
    {
        public Task<bool> HasSpeechAsync(Stream waveAudio, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(hasSpeech);
        }
    }

    private sealed class FakeDetector(WakePhraseDetectionStatus status) : ILocalWakePhraseDetector
    {
        public bool WasCalled { get; private set; }

        public Task<WakePhraseDetectionResult> DetectAsync(Stream speechSegment, CancellationToken cancellationToken)
        {
            WasCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WakePhraseDetectionResult(status));
        }
    }

    private sealed class FakeActivator(bool started) : ILocalListeningActivator
    {
        public bool WasCalled { get; private set; }

        public Task<bool> StartListeningAsync(CancellationToken cancellationToken)
        {
            WasCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(started);
        }
    }
}
