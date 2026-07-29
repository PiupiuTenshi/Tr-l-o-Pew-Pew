using PewPew.Application.Speech;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using LegacySpeechSynthesizer = System.Speech.Synthesis.SpeechSynthesizer;

namespace PewPew.Desktop;

/// <summary>
/// Windows-only local TTS adapter. It prefers the OneCore/WinRT voice catalog so
/// Windows-installed voices such as Microsoft An can be selected without cloud TTS.
/// </summary>
public sealed class WindowsSpeechOutput : ILocalSpeechOutput, IDisposable
{
    private readonly IReadOnlyList<VoiceInformation> _oneCoreVoices;
    private readonly LegacySpeechSynthesizer? _legacySynthesizer;
    private MediaPlayer? _activePlayer;
    private bool _disposed;

    public WindowsSpeechOutput()
    {
        try
        {
            _oneCoreVoices = SpeechSynthesizer.AllVoices.ToArray();
        }
        catch
        {
            _oneCoreVoices = Array.Empty<VoiceInformation>();
        }

        AvailableVoices = _oneCoreVoices
            .Select(voice => new LocalSpeechVoice($"onecore:{voice.Id}", voice.DisplayName, voice.Language))
            .ToArray();

        if (AvailableVoices.Count > 0)
        {
            var preferred = AvailableVoices.FirstOrDefault(voice =>
                voice.DisplayName.Contains("Microsoft An", StringComparison.OrdinalIgnoreCase)) ??
                AvailableVoices.FirstOrDefault(voice =>
                    string.Equals(voice.CultureName, "vi-VN", StringComparison.OrdinalIgnoreCase)) ??
                AvailableVoices[0];
            SelectedVoiceId = preferred.Id;
            return;
        }

        try
        {
            _legacySynthesizer = new LegacySpeechSynthesizer();
            var defaultVoice = _legacySynthesizer.Voice;
            var legacyVoice = new LocalSpeechVoice(
                $"legacy:{defaultVoice.Name}",
                $"Windows default - {defaultVoice.Name}",
                defaultVoice.Culture.Name);
            AvailableVoices = [legacyVoice];
            SelectedVoiceId = legacyVoice.Id;
        }
        catch
        {
            AvailableVoices = Array.Empty<LocalSpeechVoice>();
            SelectedVoiceId = string.Empty;
        }
    }

    public IReadOnlyList<LocalSpeechVoice> AvailableVoices { get; }

    public string SelectedVoiceId { get; private set; }

    public SpeechOutputResult SelectVoice(string voiceId)
    {
        if (_disposed)
        {
            return new SpeechOutputResult(SpeechOutputStatus.Unavailable, "Windows speech is unavailable.");
        }

        if (AvailableVoices.All(voice => voice.Id != voiceId))
        {
            return new SpeechOutputResult(SpeechOutputStatus.Failed, "The selected voice is not installed.");
        }

        SelectedVoiceId = voiceId;
        return new SpeechOutputResult(SpeechOutputStatus.Completed);
    }

    public Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new SpeechOutputResult(SpeechOutputStatus.Completed));
        }

        if (_disposed || AvailableVoices.Count == 0)
        {
            return Task.FromResult(new SpeechOutputResult(SpeechOutputStatus.Unavailable));
        }

        return SelectedVoiceId.StartsWith("onecore:", StringComparison.Ordinal)
            ? SpeakOneCoreAsync(text, cancellationToken)
            : SpeakLegacyAsync(text, cancellationToken);
    }

    public Task CancelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var player = Interlocked.Exchange(ref _activePlayer, null);
        if (player is not null)
        {
            try
            {
                player.Pause();
            }
            finally
            {
                player.Dispose();
            }
        }

        _legacySynthesizer?.SpeakAsyncCancelAll();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ = CancelAsync(CancellationToken.None);
        _legacySynthesizer?.Dispose();
    }

    private async Task<SpeechOutputResult> SpeakOneCoreAsync(string text, CancellationToken cancellationToken)
    {
        var selectedId = SelectedVoiceId["onecore:".Length..];
        var selectedVoice = _oneCoreVoices.FirstOrDefault(voice => voice.Id == selectedId);
        if (selectedVoice is null)
        {
            return new SpeechOutputResult(SpeechOutputStatus.Unavailable, "The selected Windows voice is unavailable.");
        }

        try
        {
            using var synthesizer = new SpeechSynthesizer { Voice = selectedVoice };
            using var stream = await synthesizer.SynthesizeTextToStreamAsync(text).AsTask(cancellationToken).ConfigureAwait(false);
            using var player = new MediaPlayer();
            var completion = new TaskCompletionSource<SpeechOutputResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            player.MediaEnded += (_, _) => completion.TrySetResult(new SpeechOutputResult(SpeechOutputStatus.Completed));
            player.MediaFailed += (_, _) => completion.TrySetResult(new SpeechOutputResult(SpeechOutputStatus.Failed));
            player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            Interlocked.Exchange(ref _activePlayer, player)?.Dispose();
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    player.Pause();
                }
                catch
                {
                    // The controller maps cancellation-time media failures to the visible text fallback.
                }

                completion.TrySetResult(new SpeechOutputResult(SpeechOutputStatus.Cancelled));
            });

            player.Play();
            var result = await completion.Task.ConfigureAwait(false);
            Interlocked.CompareExchange(ref _activePlayer, null, player);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new SpeechOutputResult(SpeechOutputStatus.Cancelled);
        }
        catch
        {
            return new SpeechOutputResult(SpeechOutputStatus.Failed);
        }
    }

    private Task<SpeechOutputResult> SpeakLegacyAsync(string text, CancellationToken cancellationToken)
    {
        if (_legacySynthesizer is null)
        {
            return Task.FromResult(new SpeechOutputResult(SpeechOutputStatus.Unavailable));
        }

        var completion = new TaskCompletionSource<SpeechOutputResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<System.Speech.Synthesis.SpeakCompletedEventArgs>? handler = null;
        CancellationTokenRegistration registration = default;

        void Complete(SpeechOutputResult result)
        {
            if (!completion.TrySetResult(result))
            {
                return;
            }

            _legacySynthesizer.SpeakCompleted -= handler;
            registration.Dispose();
        }

        handler = (_, eventArgs) => Complete(eventArgs.Cancelled
            ? new SpeechOutputResult(SpeechOutputStatus.Cancelled)
            : eventArgs.Error is not null
                ? new SpeechOutputResult(SpeechOutputStatus.Failed)
                : new SpeechOutputResult(SpeechOutputStatus.Completed));

        try
        {
            _legacySynthesizer.SpeakCompleted += handler;
            registration = cancellationToken.Register(() =>
            {
                _legacySynthesizer.SpeakAsyncCancelAll();
                Complete(new SpeechOutputResult(SpeechOutputStatus.Cancelled));
            });
            _legacySynthesizer.SpeakAsync(text);
        }
        catch
        {
            Complete(new SpeechOutputResult(SpeechOutputStatus.Failed));
        }

        return completion.Task;
    }
}
