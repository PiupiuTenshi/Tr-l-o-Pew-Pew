namespace PewPew.Application.Speech;

public enum LocalSpeechTranscriptionStatus
{
    Transcribed,
    ClarificationRequired,
    Cancelled,
    Unavailable,
    Failed
}

/// <summary>
/// A local-only transcription result. Transcript text is working-session data and
/// must not be written to logs or long-term storage by this boundary.
/// </summary>
public sealed record LocalSpeechTranscriptionResult(
    LocalSpeechTranscriptionStatus Status,
    string? Transcript = null,
    float? Confidence = null,
    string? ClarificationPrompt = null,
    string? Reason = null);

/// <summary>
/// Application boundary for speech-to-text. Implementations must not upload the
/// supplied audio stream and must honour cancellation.
/// </summary>
public interface ILocalSpeechTranscriber
{
    Task<LocalSpeechTranscriptionResult> TranscribeAsync(Stream waveAudio, CancellationToken cancellationToken);
}

public static class LocalSpeechTranscriptionPolicy
{
    public const float DefaultMinimumConfidence = 0.55f;

    public static LocalSpeechTranscriptionResult Evaluate(string? transcript, float? confidence)
    {
        var normalizedTranscript = transcript?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            return new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.ClarificationRequired,
                Confidence: confidence,
                ClarificationPrompt: "I could not hear a clear request. Please try again or use text.");
        }

        if (confidence is null || confidence < DefaultMinimumConfidence)
        {
            return new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.ClarificationRequired,
                Confidence: confidence,
                ClarificationPrompt: "I am not confident I understood. Please say that again or use text.");
        }

        return new LocalSpeechTranscriptionResult(
            LocalSpeechTranscriptionStatus.Transcribed,
            normalizedTranscript,
            confidence);
    }
}
