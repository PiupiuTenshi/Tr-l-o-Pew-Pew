using PewPew.Application.Speech;

namespace PewPew.Desktop;

public sealed class LocalSpeechInputController : IDisposable
{
    private readonly AudioSessionState _audioSession;
    private readonly ILocalMicrophoneCapture _microphone;
    private readonly LocalSpeechInteractionService _interactionService;

    public LocalSpeechInputController(
        AudioSessionState audioSession,
        ILocalMicrophoneCapture microphone,
        LocalSpeechInteractionService interactionService)
    {
        _audioSession = audioSession ?? throw new ArgumentNullException(nameof(audioSession));
        _microphone = microphone ?? throw new ArgumentNullException(nameof(microphone));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
    }

    public bool IsListening => _audioSession.IsListening;

    public string StatusLabel { get; private set; } = "Microphone is off. Hold to talk or use text.";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _audioSession.Start();
        try
        {
            await _microphone.StartAsync(cancellationToken).ConfigureAwait(false);
            StatusLabel = _audioSession.ConsentLabel;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _audioSession.Cancel();
            StatusLabel = "Listening cancelled. Text input remains available.";
        }
        catch
        {
            _audioSession.Cancel();
            StatusLabel = "Microphone is unavailable. Check Windows microphone permission or use text.";
        }
    }

    public async Task<LocalSpeechInteractionResult?> StopAndTranscribeAsync(CancellationToken cancellationToken)
    {
        if (!IsListening)
        {
            return null;
        }

        try
        {
            await using var audio = await _microphone.StopAsync(cancellationToken).ConfigureAwait(false);
            _audioSession.Stop();
            StatusLabel = "Understanding speech locally…";
            var result = await _interactionService.TranscribeAsync(audio, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            StatusLabel = result.Transcription.Status switch
            {
                LocalSpeechTranscriptionStatus.Transcribed => "Local speech was transcribed. No action was run.",
                LocalSpeechTranscriptionStatus.ClarificationRequired => "Speech needs clarification. No action was run.",
                LocalSpeechTranscriptionStatus.Cancelled => "Speech processing was cancelled. Text input remains available.",
                LocalSpeechTranscriptionStatus.Unavailable => result.Transcription.Reason ?? "Local STT is unavailable. Text input remains available.",
                _ => result.Transcription.Reason ?? "Local STT failed. Text input remains available."
            };
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelAsync().ConfigureAwait(false);
            return null;
        }
        catch
        {
            await CancelAsync().ConfigureAwait(false);
            StatusLabel = "Local STT failed. Text input remains available.";
            return null;
        }
    }

    /// <summary>
    /// Stops the current explicit capture and returns its in-memory WAV stream
    /// once. The caller must dispose it; no audio is retained by the session.
    /// </summary>
    public async Task<MemoryStream?> StopCaptureAsync(CancellationToken cancellationToken)
    {
        if (!IsListening)
        {
            return null;
        }

        try
        {
            var audio = await _microphone.StopAsync(cancellationToken).ConfigureAwait(false);
            _audioSession.Stop();
            StatusLabel = "Audio captured locally. Processing remains local.";
            return audio;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelAsync().ConfigureAwait(false);
            return null;
        }
        catch
        {
            await CancelAsync().ConfigureAwait(false);
            StatusLabel = "Local audio capture failed. Text input remains available.";
            return null;
        }
    }

    public async Task CancelAsync()
    {
        if (_microphone.IsRecording)
        {
            await _microphone.CancelAsync().ConfigureAwait(false);
        }

        if (IsListening)
        {
            _audioSession.Cancel();
        }

        StatusLabel = "Listening cancelled. Temporary audio was cleared.";
    }

    public void Dispose() => _microphone.Dispose();
}
