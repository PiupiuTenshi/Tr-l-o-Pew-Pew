namespace PewPew.Application.Speech;

public enum SpeechOutputStatus
{
    Completed,
    Cancelled,
    Unavailable,
    Failed
}

public sealed record SpeechOutputResult(SpeechOutputStatus Status, string? Reason = null);

/// <summary>
/// Local-only speech output boundary. Implementations must not upload text or audio.
/// </summary>
public interface ILocalSpeechOutput
{
    Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken);

    Task CancelAsync(CancellationToken cancellationToken);
}
