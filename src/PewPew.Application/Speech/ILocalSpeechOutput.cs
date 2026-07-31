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
/// A local speech voice exposed to the presentation layer. The identifier is adapter-owned.
/// </summary>
public sealed record LocalSpeechVoice(string Id, string DisplayName, string CultureName)
{
    public const string AutomaticId = "automatic";

    public override string ToString() => DisplayName;
}

/// <summary>
/// Local-only speech output boundary. Implementations must not upload text or audio.
/// </summary>
public interface ILocalSpeechOutput
{
    IReadOnlyList<LocalSpeechVoice> AvailableVoices { get; }

    string SelectedVoiceId { get; }

    SpeechOutputResult SelectVoice(string voiceId);

    Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken);

    Task CancelAsync(CancellationToken cancellationToken);
}
