using PewPew.Application.Speech;
using PewPew.Application.Voice;
using System.Globalization;
using System.Text;

namespace PewPew.Desktop;

/// <summary>
/// Adapts the existing local-only Whisper transcriber to wake phrase matching.
/// The transcript stays in-process and is never persisted or logged.
/// </summary>
public sealed class WhisperWakePhraseDetector(ILocalSpeechTranscriber transcriber) : ILocalWakePhraseDetector
{
    private static readonly string[] WakePhraseAliases =
    [
        "hey pew pew",
        "okay pew pew",
        "chao pew pew",
        "pew pew"
    ];

    public async Task<WakePhraseDetectionResult> DetectAsync(Stream speechSegment, CancellationToken cancellationToken)
    {
        var transcription = await transcriber.TranscribeAsync(speechSegment, cancellationToken).ConfigureAwait(false);
        return transcription.Status switch
        {
            LocalSpeechTranscriptionStatus.Cancelled => new(WakePhraseDetectionStatus.Cancelled, "wake_detection_cancelled"),
            LocalSpeechTranscriptionStatus.Unavailable => new(WakePhraseDetectionStatus.Unavailable, "wake_detector_unavailable"),
            LocalSpeechTranscriptionStatus.Failed => new(WakePhraseDetectionStatus.Failed, "wake_detector_failed"),
            LocalSpeechTranscriptionStatus.Transcribed when IsWakePhrase(transcription.Transcript) => new(WakePhraseDetectionStatus.Detected),
            _ => new(WakePhraseDetectionStatus.NotDetected, "wake_phrase_not_detected")
        };
    }

    private static bool IsWakePhrase(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return false;
        }

        var normalized = string.Concat(transcript.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                && (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)))
            .ToArray())
            .ToLowerInvariant();
        return WakePhraseAliases.Any(alias => normalized.Contains(alias, StringComparison.Ordinal));
    }
}
