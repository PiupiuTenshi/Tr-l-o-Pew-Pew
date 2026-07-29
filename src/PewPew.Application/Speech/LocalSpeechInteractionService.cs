using PewPew.Domain.Interactions;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.Speech;

/// <summary>
/// Keeps a voice interaction inside the existing domain lifecycle. A transcript
/// never dispatches an action; a low-confidence result only opens clarification.
/// </summary>
public sealed class LocalSpeechInteractionService(ILocalSpeechTranscriber transcriber)
{
    private static readonly TimeSpan InteractionTtl = TimeSpan.FromMinutes(2);

    public async Task<LocalSpeechInteractionResult> TranscribeAsync(
        Stream waveAudio,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(waveAudio);

        var session = new InteractionSession(EntityId.New(), now.Add(InteractionTtl));
        session.BeginInput();
        session.CaptureInput();

        var transcription = await transcriber.TranscribeAsync(waveAudio, cancellationToken).ConfigureAwait(false);
        if (transcription.Status != LocalSpeechTranscriptionStatus.ClarificationRequired)
        {
            if (transcription.Status == LocalSpeechTranscriptionStatus.Cancelled)
            {
                session.Cancel();
            }

            return new LocalSpeechInteractionResult(session, transcription);
        }

        session.RequireClarification();
        var clarification = new ClarificationRequest(EntityId.New(), "voice input", now.Add(InteractionTtl));
        clarification.Present();
        return new LocalSpeechInteractionResult(session, transcription, clarification);
    }
}

public sealed record LocalSpeechInteractionResult(
    InteractionSession Session,
    LocalSpeechTranscriptionResult Transcription,
    ClarificationRequest? Clarification = null);
