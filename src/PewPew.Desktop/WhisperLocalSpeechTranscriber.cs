using System.Security.Cryptography;
using System.Text;
using PewPew.Application.Speech;
using Whisper.net;

namespace PewPew.Desktop;

/// <summary>
/// CPU-only Whisper.net adapter. It reads a pre-provisioned local GGML model and
/// never uses Whisper.net's downloader or any network API.
/// </summary>
public sealed class WhisperLocalSpeechTranscriber : ILocalSpeechTranscriber, IDisposable
{
    private readonly LocalSpeechModelOptions _options;
    private readonly SemaphoreSlim _serialGate = new(1, 1);
    private WhisperFactory? _factory;
    private bool _disposed;

    public WhisperLocalSpeechTranscriber(LocalSpeechModelOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<LocalSpeechTranscriptionResult> TranscribeAsync(Stream waveAudio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(waveAudio);
        if (_disposed)
        {
            return new LocalSpeechTranscriptionResult(LocalSpeechTranscriptionStatus.Unavailable, Reason: "Local STT is unavailable.");
        }

        try
        {
            await _serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new LocalSpeechTranscriptionResult(LocalSpeechTranscriptionStatus.Cancelled);
        }

        try
        {
            var modelCheck = await ValidateModelAsync(cancellationToken).ConfigureAwait(false);
            if (modelCheck is not null)
            {
                return modelCheck;
            }

            _factory ??= WhisperFactory.FromPath(_options.ModelPath, new WhisperFactoryOptions { UseGpu = false });
            using var processor = _factory.CreateBuilder()
                .WithLanguageDetection()
                .WithProbabilities()
                .Build();

            var transcript = new StringBuilder();
            var probabilityTotal = 0f;
            var segmentCount = 0;
            await foreach (var segment in processor.ProcessAsync(waveAudio, cancellationToken).ConfigureAwait(false))
            {
                transcript.Append(segment.Text);
                probabilityTotal += segment.Probability;
                segmentCount++;
            }

            var confidence = segmentCount == 0 ? 0f : probabilityTotal / segmentCount;
            return LocalSpeechTranscriptionPolicy.Evaluate(transcript.ToString(), confidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new LocalSpeechTranscriptionResult(LocalSpeechTranscriptionStatus.Cancelled);
        }
        catch (Exception exception)
        {
            return new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.Failed,
                Reason: DescribeFailure(exception));
        }
        finally
        {
            _serialGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _factory?.Dispose();
        _serialGate.Dispose();
    }

    private async Task<LocalSpeechTranscriptionResult?> ValidateModelAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.ModelPath))
        {
            return new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.Unavailable,
                Reason: $"Local speech model is not available. Configure {LocalSpeechModelOptions.ModelPathEnvironmentKey}.");
        }

        if (string.IsNullOrWhiteSpace(_options.ExpectedSha256))
        {
            return null;
        }

        await using var stream = File.OpenRead(_options.ModelPath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(_options.ExpectedSha256)))
        {
            return new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.Unavailable,
                Reason: "Local speech model integrity verification failed.");
        }

        return null;
    }

    private static string DescribeFailure(Exception exception) => exception switch
    {
        InvalidDataException => "The captured microphone audio could not be decoded locally. Try speaking for at least two seconds, then stop again.",
        UnauthorizedAccessException => "Local STT could not read the model or microphone audio. Check Windows microphone permission and the local model file.",
        _ => "The local STT runtime could not process this recording. Try again after restarting Pew Pew; text input remains available."
    };
}
