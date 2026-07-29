using PewPew.Application.Speech;

namespace PewPew.Desktop;

public enum SpeechOutputState
{
    Ready,
    Speaking,
    Cancelled,
    Unavailable,
    Failed
}

public sealed class SpeechOutputController
{
    private readonly ILocalSpeechOutput _speechOutput;
    private readonly TimeSpan _timeout;
    private CancellationTokenSource? _activeSpeech;

    public SpeechOutputController(ILocalSpeechOutput speechOutput, TimeSpan? timeout = null)
    {
        _speechOutput = speechOutput ?? throw new ArgumentNullException(nameof(speechOutput));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Speech timeout must be positive.");
        }
    }

    public SpeechOutputState State { get; private set; } = SpeechOutputState.Ready;

    public bool IsSpeaking => State == SpeechOutputState.Speaking;

    public IReadOnlyList<LocalSpeechVoice> AvailableVoices => _speechOutput.AvailableVoices;

    public string SelectedVoiceId => _speechOutput.SelectedVoiceId;

    public LocalSpeechVoice? SelectedVoice => AvailableVoices.FirstOrDefault(voice => voice.Id == SelectedVoiceId);

    public string StatusLabel => State switch
    {
        SpeechOutputState.Speaking => "Speaking locally.",
        SpeechOutputState.Cancelled => "Speech cancelled. Text remains available.",
        SpeechOutputState.Unavailable => "Local speech is unavailable. Text remains available.",
        SpeechOutputState.Failed => "Local speech failed. Text remains available.",
        _ => "Speech is ready. Text remains available."
    };

    public async Task SpeakAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            State = SpeechOutputState.Ready;
            return;
        }

        await CancelActiveSpeechAsync().ConfigureAwait(false);

        using var speech = new CancellationTokenSource(_timeout);
        _activeSpeech = speech;
        State = SpeechOutputState.Speaking;

        SpeechOutputResult result;
        try
        {
            result = await _speechOutput.SpeakAsync(text, speech.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (speech.IsCancellationRequested)
        {
            result = new SpeechOutputResult(SpeechOutputStatus.Cancelled);
        }
        catch
        {
            result = new SpeechOutputResult(SpeechOutputStatus.Failed);
        }
        finally
        {
            if (ReferenceEquals(_activeSpeech, speech))
            {
                _activeSpeech = null;
            }
        }

        State = result.Status switch
        {
            SpeechOutputStatus.Completed => SpeechOutputState.Ready,
            SpeechOutputStatus.Cancelled => SpeechOutputState.Cancelled,
            SpeechOutputStatus.Unavailable => SpeechOutputState.Unavailable,
            _ => SpeechOutputState.Failed
        };
    }

    public Task CancelAsync() => CancelActiveSpeechAsync();

    public SpeechOutputResult SelectVoice(string? voiceId)
    {
        if (IsSpeaking)
        {
            return new SpeechOutputResult(SpeechOutputStatus.Failed, "Stop speech before changing the voice.");
        }

        if (string.IsNullOrWhiteSpace(voiceId))
        {
            return new SpeechOutputResult(SpeechOutputStatus.Failed, "A voice must be selected.");
        }

        var result = _speechOutput.SelectVoice(voiceId);
        State = result.Status switch
        {
            SpeechOutputStatus.Completed => SpeechOutputState.Ready,
            SpeechOutputStatus.Unavailable => SpeechOutputState.Unavailable,
            SpeechOutputStatus.Cancelled => SpeechOutputState.Cancelled,
            _ => SpeechOutputState.Failed
        };

        return result;
    }

    private async Task CancelActiveSpeechAsync()
    {
        var speech = _activeSpeech;
        if (speech is null)
        {
            return;
        }

        speech.Cancel();
        try
        {
            await _speechOutput.CancelAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            State = SpeechOutputState.Failed;
            return;
        }

        State = SpeechOutputState.Cancelled;
    }
}
