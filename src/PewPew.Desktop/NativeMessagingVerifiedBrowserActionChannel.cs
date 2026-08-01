using PewPew.Application.Automation;
using PewPew.Application.BrowserExtension;

namespace PewPew.Desktop;

public sealed class NativeMessagingVerifiedBrowserActionChannel(NativeMessagingBrowserCommandBroker broker)
    : IVerifiedBrowserActionChannel
{
    public async Task<string?> DispatchAndReadbackAsync(
        NativeMessagingBrowserCommand command,
        CancellationToken cancellationToken)
    {
        if (!broker.TryQueue(command, out _))
        {
            return "queue_denied";
        }

        var response = await broker.WaitForReadbackAsync(command.CommandId, cancellationToken).ConfigureAwait(false);
        return response.IsSuccess ? response.ReasonCode : null;
    }
}
