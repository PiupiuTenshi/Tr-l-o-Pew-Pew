using System.Globalization;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PewPew.Application.Speech;

namespace PewPew.Desktop;

/// <summary>
/// Windows-only adapter for installed System.Speech voices. It never sends text off-device.
/// </summary>
public sealed class WindowsSpeechOutput : ILocalSpeechOutput, IDisposable
{
    private readonly SpeechSynthesizer? _synthesizer;
    private readonly string? _defaultVoiceName;

    public WindowsSpeechOutput()
    {
        try
        {
            _synthesizer = new SpeechSynthesizer();
            _defaultVoiceName = _synthesizer.Voice.Name;
            AvailableVoices = DiscoverVoices(_synthesizer, _defaultVoiceName);
            SelectedVoiceId = AvailableVoices[0].Id;

            // Prefer Vietnamese on this Windows-first assistant. The user can always choose another local voice.
            var vietnameseVoice = AvailableVoices.FirstOrDefault(voice =>
                voice.DisplayName.Contains("Microsoft An", StringComparison.OrdinalIgnoreCase)) ??
                AvailableVoices.FirstOrDefault(voice =>
                    string.Equals(voice.CultureName, "vi-VN", StringComparison.OrdinalIgnoreCase));
            if (vietnameseVoice is not null)
            {
                _ = SelectVoice(vietnameseVoice.Id);
            }
        }
        catch
        {
            _synthesizer = null;
            AvailableVoices = Array.Empty<LocalSpeechVoice>();
            SelectedVoiceId = string.Empty;
        }
    }

    public IReadOnlyList<LocalSpeechVoice> AvailableVoices { get; }

    public string SelectedVoiceId { get; private set; }

    public SpeechOutputResult SelectVoice(string voiceId)
    {
        if (_synthesizer is null)
        {
            return new SpeechOutputResult(SpeechOutputStatus.Unavailable, "Windows speech is unavailable.");
        }

        var voice = AvailableVoices.FirstOrDefault(candidate => candidate.Id == voiceId);
        if (voice is null)
        {
            return new SpeechOutputResult(SpeechOutputStatus.Failed, "The selected voice is not installed.");
        }

        try
        {
            if (voice.Id.StartsWith("culture:", StringComparison.Ordinal))
            {
                _synthesizer.SelectVoiceByHints(
                    VoiceGender.NotSet,
                    VoiceAge.NotSet,
                    0,
                    CultureInfo.GetCultureInfo(voice.CultureName));
            }
            else
            {
                _synthesizer.SelectVoice(voice.Id["voice:".Length..]);
            }

            SelectedVoiceId = voice.Id;
            return new SpeechOutputResult(SpeechOutputStatus.Completed);
        }
        catch
        {
            return new SpeechOutputResult(SpeechOutputStatus.Failed, "Windows could not activate the selected voice.");
        }
    }

    public Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new SpeechOutputResult(SpeechOutputStatus.Completed));
        }

        if (_synthesizer is null)
        {
            return Task.FromResult(new SpeechOutputResult(SpeechOutputStatus.Unavailable));
        }

        var completion = new TaskCompletionSource<SpeechOutputResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        SpeechSynthesizer? synthesizer = _synthesizer;
        EventHandler<SpeakCompletedEventArgs>? handler = null;
        CancellationTokenRegistration cancellationRegistration = default;

        void Complete(SpeechOutputResult result)
        {
            if (!completion.TrySetResult(result))
            {
                return;
            }

            synthesizer.SpeakCompleted -= handler;
            cancellationRegistration.Dispose();
        }

        handler = (_, eventArgs) =>
        {
            if (eventArgs.Cancelled)
            {
                Complete(new SpeechOutputResult(SpeechOutputStatus.Cancelled));
            }
            else if (eventArgs.Error is not null)
            {
                Complete(new SpeechOutputResult(SpeechOutputStatus.Failed));
            }
            else
            {
                Complete(new SpeechOutputResult(SpeechOutputStatus.Completed));
            }
        };

        try
        {
            synthesizer.SpeakCompleted += handler;
            cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    synthesizer.SpeakAsyncCancelAll();
                }
                catch
                {
                    // The controller maps a cancellation-time provider failure to its visible fallback state.
                }

                Complete(new SpeechOutputResult(SpeechOutputStatus.Cancelled));
            });
            synthesizer.SpeakAsync(text);
        }
        catch
        {
            Complete(new SpeechOutputResult(SpeechOutputStatus.Failed));
        }

        return completion.Task;
    }

    public Task CancelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_synthesizer is not null)
        {
            _synthesizer.SpeakAsyncCancelAll();
        }

        return Task.CompletedTask;
    }

    public void Dispose() => _synthesizer?.Dispose();

    private static List<LocalSpeechVoice> DiscoverVoices(SpeechSynthesizer synthesizer, string defaultVoiceName)
    {
        var voices = new List<LocalSpeechVoice>
        {
            new($"voice:{defaultVoiceName}", $"Windows default - {defaultVoiceName}", synthesizer.Voice.Culture.Name)
        };

        AddRegistryVoices(voices);

        return voices;
    }

    private static void AddRegistryVoices(List<LocalSpeechVoice> voices)
    {
        string[] paths =
        [
            @"SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens",
            @"SOFTWARE\Microsoft\Speech\Voices\Tokens"
        ];

        try
        {
            foreach (var path in paths)
            {
                using var voiceTokens = Registry.LocalMachine.OpenSubKey(path);
                if (voiceTokens is null)
                {
                    continue;
                }

                foreach (var tokenName in voiceTokens.GetSubKeyNames())
                {
                    using var token = voiceTokens.OpenSubKey(tokenName);
                    var displayName = token?.GetValue(null) as string;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    var id = $"voice:{displayName}";
                    if (voices.Any(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    voices.Add(new LocalSpeechVoice(id, displayName, ExtractCulture(tokenName)));
                }
            }
        }
        catch
        {
            // Registry discovery is optional. Speech output remains usable with the Windows default voice.
        }
    }

    private static string ExtractCulture(string tokenName)
    {
        var match = Regex.Match(tokenName, @"_(?<culture>[a-z]{2}-[A-Z]{2})_", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["culture"].Value : string.Empty;
    }
}
