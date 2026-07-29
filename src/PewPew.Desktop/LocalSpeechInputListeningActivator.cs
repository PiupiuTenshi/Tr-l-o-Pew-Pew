using PewPew.Application.Voice;

namespace PewPew.Desktop;

/// <summary>
/// Desktop composition adapter: wake detection may only open a fresh explicit
/// listening session. It cannot dispatch an action or consume confirmation.
/// </summary>
public sealed class LocalSpeechInputListeningActivator(LocalSpeechInputController speechInput) : ILocalListeningActivator
{
    private readonly LocalSpeechInputController _speechInput = speechInput ?? throw new ArgumentNullException(nameof(speechInput));

    public async Task<bool> StartListeningAsync(CancellationToken cancellationToken)
    {
        if (_speechInput.IsListening)
        {
            return false;
        }

        await _speechInput.StartAsync(cancellationToken).ConfigureAwait(false);
        return _speechInput.IsListening;
    }
}
