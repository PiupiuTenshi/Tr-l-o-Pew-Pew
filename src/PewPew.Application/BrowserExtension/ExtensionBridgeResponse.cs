namespace PewPew.Application.BrowserExtension;

/// <summary>
/// Response returned by <see cref="DesktopExtensionBridge"/>.
/// </summary>
public sealed record ExtensionBridgeResponse(
    bool IsSuccess,
    string ReasonCode,
    string? DataPayload = null)
{
    public static ExtensionBridgeResponse Success(string? dataPayload = null) =>
        new(true, "allowed", dataPayload);

    public static ExtensionBridgeResponse Failed(string reasonCode) =>
        new(false, reasonCode, null);
}
