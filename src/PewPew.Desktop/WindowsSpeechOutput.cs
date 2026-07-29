using System.Speech.Synthesis;
using PewPew.Application.Speech;

namespace PewPew.Desktop;

/// <summary>
/// Windows-only adapter for installed System.Speech voices. It never sends text off-device.
/// </summary>
public sealed class WindowsSpeechOutput : ILocalSpeechOutput, IDisposable
{
    private readonly SpeechSynthesizer? _synthesizer;

    public WindowsSpeechOutput()
    {
        try
        {
            _synthesizer = new SpeechSynthesizer();
        }
        catch
        {
            _synthesizer = null;
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
}
