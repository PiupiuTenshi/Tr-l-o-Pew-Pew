namespace PewPew.Application.Voice;

public interface ILocalVoiceActivityGate
{
    Task<bool> HasSpeechAsync(Stream waveAudio, CancellationToken cancellationToken);
}

public interface ILocalWakePhraseDetector
{
    Task<WakePhraseDetectionResult> DetectAsync(Stream speechSegment, CancellationToken cancellationToken);
}

public interface ILocalListeningActivator
{
    Task<bool> StartListeningAsync(CancellationToken cancellationToken);
}

public enum WakePhraseDetectionStatus
{
    Detected,
    NotDetected,
    Unavailable,
    Cancelled,
    Failed
}

public sealed record WakePhraseDetectionResult(WakePhraseDetectionStatus Status, string? Reason = null);

/// <summary>
/// A local wake phrase may only activate an input session. It never grants
/// permission, confirms a plan, or dispatches an action.
/// </summary>
public sealed class WakePhraseActivationService(
    ILocalVoiceActivityGate voiceActivityGate,
    ILocalWakePhraseDetector detector,
    ILocalListeningActivator listeningActivator)
{
    public async Task<WakePhraseActivationResult> TryActivateAsync(Stream waveAudio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(waveAudio);

        try
        {
            if (!await voiceActivityGate.HasSpeechAsync(waveAudio, cancellationToken).ConfigureAwait(false))
            {
                return new(WakePhraseActivationStatus.NotDetected, "voice_activity_not_detected");
            }

            if (waveAudio.CanSeek)
            {
                waveAudio.Position = 0;
            }

            var detection = await detector.DetectAsync(waveAudio, cancellationToken).ConfigureAwait(false);
            if (detection.Status != WakePhraseDetectionStatus.Detected)
            {
                return new(WakePhraseActivationStatus.NotDetected, detection.Reason ?? "wake_phrase_not_detected");
            }

            return await listeningActivator.StartListeningAsync(cancellationToken).ConfigureAwait(false)
                ? new(WakePhraseActivationStatus.ListeningStarted, "wake_phrase_detected")
                : new(WakePhraseActivationStatus.NotDetected, "listening_activation_denied");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(WakePhraseActivationStatus.Cancelled, "wake_detection_cancelled");
        }
    }
}

public enum WakePhraseActivationStatus
{
    ListeningStarted,
    NotDetected,
    Cancelled
}

public sealed record WakePhraseActivationResult(WakePhraseActivationStatus Status, string ReasonCode);
