namespace PewPew.Application.BrowserExtension;

/// <summary>
/// Bounded, metadata-only messages that may cross the Native Messaging and named-pipe boundary.
/// They are transport messages, never browser actions.
/// </summary>
public sealed record NativeMessagingTransportRequest(
    int Version,
    string Kind,
    string CorrelationId,
    string? SessionToken = null,
    string? Nonce = null,
    DateTimeOffset? TimestampUtc = null,
    string? TargetOrigin = null,
    string? TabId = null,
    string? SnapshotId = null,
    int? SnapshotVersion = null,
    long? NavigationGeneration = null,
    string? CommandId = null,
    string? PayloadHash = null,
    string? ReadbackStatus = null);

/// <summary>
/// The only browser command the native transport may deliver. It contains no
/// selector, JavaScript, DOM/text content, credential, or raw URL. Policy and
/// confirmation are decided by Desktop before this value is created.
/// </summary>
public sealed record NativeMessagingBrowserCommand(
    string CommandId,
    string SessionToken,
    string TargetOrigin,
    string TabId,
    string SnapshotId,
    int SnapshotVersion,
    long NavigationGeneration,
    string ActionKind,
    string PayloadHash,
    string AuthorizationId);

public sealed record NativeMessagingTransportResponse(
    bool IsSuccess,
    string ReasonCode,
    string? SessionToken = null,
    NativeMessagingBrowserCommand? BrowserCommand = null)
{
    public static NativeMessagingTransportResponse Accepted(
        string? sessionToken = null,
        NativeMessagingBrowserCommand? browserCommand = null) =>
        new(true, "accepted", sessionToken, browserCommand);

    public static NativeMessagingTransportResponse Rejected(string reasonCode) =>
        new(false, reasonCode);
}

public static class NativeMessagingTransportProtocol
{
    public const int CurrentVersion = 1;
    public const int MaximumFrameBytes = 64 * 1024;

    private static readonly HashSet<string> AllowedKinds = ["connect", "heartbeat", "cancel", "command_poll", "action_readback"];
    private static readonly HashSet<string> AllowedBrowserActions = ["play", "pause", "mute", "unmute"];

    public static bool TryValidate(NativeMessagingTransportRequest? request, out string failureReason)
    {
        if (request is null || request.Version != CurrentVersion)
        {
            failureReason = "transport_protocol_version_invalid";
            return false;
        }

        if (!AllowedKinds.Contains(request.Kind ?? string.Empty))
        {
            failureReason = "transport_message_kind_denied";
            return false;
        }

        if (!IsBounded(request.CorrelationId, 128))
        {
            failureReason = "transport_correlation_invalid";
            return false;
        }

        if (request.Kind == "connect")
        {
            failureReason = string.Empty;
            return true;
        }

        if (!IsBounded(request.SessionToken, 128) || !IsBounded(request.Nonce, 128) ||
            !IsBounded(request.TargetOrigin, 2_048) || !IsBounded(request.TabId, 128) || request.TimestampUtc is null ||
            (request.Kind != "command_poll" && !IsBounded(request.SnapshotId, 128)))
        {
            failureReason = "transport_authenticated_binding_required";
            return false;
        }

        if (request.Kind == "action_readback" &&
            (!IsBounded(request.CommandId, 128) || !IsBounded(request.ReadbackStatus, 64)))
        {
            failureReason = "transport_readback_binding_required";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool IsBounded(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    public static bool TryValidateBrowserCommand(NativeMessagingBrowserCommand? command, out string failureReason)
    {
        if (command is null || !IsBounded(command.CommandId, 128) || !IsBounded(command.SessionToken, 128) ||
            !IsBounded(command.TargetOrigin, 2_048) || !IsBounded(command.TabId, 128) ||
            !IsBounded(command.SnapshotId, 128) || !IsBounded(command.PayloadHash, 128) ||
            !IsBounded(command.AuthorizationId, 128) || command.SnapshotVersion < 1 || command.NavigationGeneration < 0 ||
            !AllowedBrowserActions.Contains(command.ActionKind ?? string.Empty))
        {
            failureReason = "browser_command_binding_invalid";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
}
