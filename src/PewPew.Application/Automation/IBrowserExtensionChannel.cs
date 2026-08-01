namespace PewPew.Application.Automation;

/// <summary>
/// Application-layer adapter boundary for browser extension transport.
/// Desktop implements this via the Native Messaging named-pipe channel.
/// The executor never calls the extension directly — all I/O flows through
/// this abstraction for testability and trust boundary isolation.
/// </summary>
public interface IBrowserExtensionChannel
{
    /// <summary>
    /// Sends a bound browser action to the extension for execution in the
    /// specified tab. Returns <c>true</c> if the extension acknowledged the
    /// action; <c>false</c> if the channel is unavailable or the extension
    /// rejected the command.
    /// </summary>
    Task<bool> SendActionAsync(
        string tabId,
        BrowserActionKind actionKind,
        string? targetSelector,
        CancellationToken cancellationToken);

    /// <summary>
    /// Requests an independent post-action readback from the extension for
    /// the specified tab. Returns <c>null</c> if the tab is closed, the
    /// channel is disconnected, or the readback times out.
    /// </summary>
    Task<BrowserReadbackSnapshot?> ReadbackAsync(
        string tabId,
        CancellationToken cancellationToken);
}
