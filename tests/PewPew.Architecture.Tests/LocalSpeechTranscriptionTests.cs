using System.Buffers.Binary;
using PewPew.Application.Speech;
using PewPew.Desktop;
using PewPew.Domain.Interactions;
using PewPew.SharedKernel.Configuration;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class LocalSpeechTranscriptionTests
{
    [Fact]
    public void EmptyTranscriptRequiresClarification()
    {
        var result = LocalSpeechTranscriptionPolicy.Evaluate(" ", 0.9f);

        Assert.Equal(LocalSpeechTranscriptionStatus.ClarificationRequired, result.Status);
        Assert.Null(result.Transcript);
        Assert.NotNull(result.ClarificationPrompt);
    }

    [Fact]
    public void LowConfidenceTranscriptRequiresClarificationInsteadOfReturningCommandText()
    {
        var result = LocalSpeechTranscriptionPolicy.Evaluate("open calculator", 0.54f);

        Assert.Equal(LocalSpeechTranscriptionStatus.ClarificationRequired, result.Status);
        Assert.Null(result.Transcript);
    }

    [Fact]
    public void HighConfidenceTranscriptIsReturnedAsWorkingSessionData()
    {
        var result = LocalSpeechTranscriptionPolicy.Evaluate("  mở máy tính  ", 0.72f);

        Assert.Equal(LocalSpeechTranscriptionStatus.Transcribed, result.Status);
        Assert.Equal("mở máy tính", result.Transcript);
    }

    [Fact]
    public async Task LowConfidenceSpeechMovesInteractionToClarificationWithoutActionDispatch()
    {
        var service = new LocalSpeechInteractionService(
            new FakeTranscriber(new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.ClarificationRequired,
                Confidence: 0.2f,
                ClarificationPrompt: "Please repeat.")));
        await using var audio = new MemoryStream(CreateSilentWave());

        var result = await service.TranscribeAsync(audio, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(InteractionStatus.WaitingClarification, result.Session.Status);
        Assert.NotNull(result.Clarification);
        Assert.Equal(ClarificationStatus.Presented, result.Clarification!.Status);
    }

    [Fact]
    public async Task CancelledSpeechLeavesTheSessionTerminal()
    {
        var service = new LocalSpeechInteractionService(
            new FakeTranscriber(new LocalSpeechTranscriptionResult(LocalSpeechTranscriptionStatus.Cancelled)));
        await using var audio = new MemoryStream(CreateSilentWave());

        var result = await service.TranscribeAsync(audio, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(InteractionStatus.Cancelled, result.Session.Status);
        Assert.Null(result.Clarification);
    }

    [Fact]
    public async Task MicrophoneStartFailureClearsConsentAndKeepsTextFallbackAvailable()
    {
        var audioSession = new AudioSessionState();
        var microphone = new FakeMicrophoneCapture(throwOnStart: true);
        var controller = new LocalSpeechInputController(
            audioSession,
            microphone,
            new LocalSpeechInteractionService(new FakeTranscriber(new LocalSpeechTranscriptionResult(LocalSpeechTranscriptionStatus.Transcribed, "ignored", 1f))));

        await controller.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(controller.IsListening);
        Assert.Equal(AudioSessionStatus.Cancelled, audioSession.Status);
        Assert.Equal("Microphone is unavailable. Check Windows microphone permission or use text.", controller.StatusLabel);
    }

    [Fact]
    public async Task PushToTalkTranscriptReturnsTextWithoutDispatchingAnAction()
    {
        var audioSession = new AudioSessionState();
        var controller = new LocalSpeechInputController(
            audioSession,
            new FakeMicrophoneCapture(),
            new LocalSpeechInteractionService(new FakeTranscriber(new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.Transcribed,
                "open calculator",
                0.8f))));

        await controller.StartAsync(TestContext.Current.CancellationToken);
        var result = await controller.StopAndTranscribeAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("open calculator", result!.Transcription.Transcript);
        Assert.Equal(InteractionStatus.Understanding, result.Session.Status);
        Assert.Equal("Local speech was transcribed. No action was run.", controller.StatusLabel);
        Assert.Equal(0, audioSession.TemporaryBufferLength);
    }

    [Fact]
    public async Task CancellingPushToTalkClearsAudioAndDoesNotTranscribe()
    {
        var audioSession = new AudioSessionState();
        var microphone = new FakeMicrophoneCapture();
        var controller = new LocalSpeechInputController(
            audioSession,
            microphone,
            new LocalSpeechInteractionService(new FakeTranscriber(new LocalSpeechTranscriptionResult(LocalSpeechTranscriptionStatus.Transcribed, "ignored", 1f))));

        await controller.StartAsync(TestContext.Current.CancellationToken);
        await controller.CancelAsync();

        Assert.False(controller.IsListening);
        Assert.True(microphone.WasCancelled);
        Assert.Equal(AudioSessionStatus.Cancelled, audioSession.Status);
    }

    [Fact]
    public async Task ModelHashMismatchFailsClosedBeforeNativeModelLoad()
    {
        var modelPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(modelPath, [0x01, 0x02, 0x03], TestContext.Current.CancellationToken);
            using var transcriber = new WhisperLocalSpeechTranscriber(
                new LocalSpeechModelOptions(modelPath, new string('0', 64)));
            await using var audio = new MemoryStream(CreateSilentWave());

            var result = await transcriber.TranscribeAsync(audio, TestContext.Current.CancellationToken);

            Assert.Equal(LocalSpeechTranscriptionStatus.Unavailable, result.Status);
            Assert.Equal("Local speech model integrity verification failed.", result.Reason);
        }
        finally
        {
            File.Delete(modelPath);
        }
    }

    [Fact]
    public async Task PreprovisionedModelProcessesSilenceOnlyWhenExplicitlyRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PEWPEW_RUN_LOCAL_MODEL_TEST"), "true", StringComparison.Ordinal))
        {
            return;
        }

        var configuration = new StartupConfiguration(
            PrivateMode: true,
            LocalDataDirectory: Path.Combine(Path.GetTempPath(), "PewPew-test"));
        var model = LocalSpeechModelOptions.LoadFromEnvironment(configuration);
        Assert.True(File.Exists(model.ModelPath));
        Assert.False(string.IsNullOrWhiteSpace(model.ExpectedSha256));

        using var transcriber = new WhisperLocalSpeechTranscriber(model);
        await using var audio = new MemoryStream(CreateSilentWave());
        var result = await transcriber.TranscribeAsync(audio, TestContext.Current.CancellationToken);

        Assert.Equal(LocalSpeechTranscriptionStatus.ClarificationRequired, result.Status);
    }

    private sealed class FakeTranscriber(LocalSpeechTranscriptionResult result) : ILocalSpeechTranscriber
    {
        public Task<LocalSpeechTranscriptionResult> TranscribeAsync(Stream waveAudio, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class FakeMicrophoneCapture(bool throwOnStart = false) : ILocalMicrophoneCapture
    {
        public bool IsRecording { get; private set; }

        public bool WasCancelled { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (throwOnStart)
            {
                throw new InvalidOperationException("microphone unavailable");
            }

            IsRecording = true;
            return Task.CompletedTask;
        }

        public Task<MemoryStream> StopAsync(CancellationToken cancellationToken)
        {
            IsRecording = false;
            return Task.FromResult(new MemoryStream(CreateSilentWave()));
        }

        public Task CancelAsync()
        {
            WasCancelled = true;
            IsRecording = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private static byte[] CreateSilentWave()
    {
        const int sampleRate = 16_000;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int dataLength = sampleRate * channels * (bitsPerSample / 8) / 10;
        var output = new byte[44 + dataLength];
        "RIFF"u8.CopyTo(output);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4), 36 + dataLength);
        "WAVEfmt "u8.CopyTo(output.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(22), channels);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(28), sampleRate * channels * (bitsPerSample / 8));
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(32), (short)(channels * (bitsPerSample / 8)));
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(34), bitsPerSample);
        "data"u8.CopyTo(output.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(40), dataLength);
        return output;
    }
}
