namespace PewPew.Application.BrowserExtension;

/// <summary>
/// Request message sent over the Desktop–Extension bridge.
/// Requires session token authentication, anti-replay nonce, timestamp, and origin.
/// </summary>
public sealed record ExtensionBridgeRequest(
    string SessionToken,
    string Nonce,
    DateTimeOffset TimestampUtc,
    string TargetOrigin,
    string Command,
    string? Payload = null);
