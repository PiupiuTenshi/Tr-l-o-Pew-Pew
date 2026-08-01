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
    string? SnapshotId = null);

public sealed record NativeMessagingTransportResponse(
    bool IsSuccess,
    string ReasonCode,
    string? SessionToken = null)
{
    public static NativeMessagingTransportResponse Accepted(string? sessionToken = null) =>
        new(true, "accepted", sessionToken);

    public static NativeMessagingTransportResponse Rejected(string reasonCode) =>
        new(false, reasonCode);
}

public static class NativeMessagingTransportProtocol
{
    public const int CurrentVersion = 1;
    public const int MaximumFrameBytes = 64 * 1024;

    private static readonly HashSet<string> AllowedKinds = ["connect", "heartbeat", "cancel"];

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
            !IsBounded(request.TargetOrigin, 2_048) || !IsBounded(request.TabId, 128) ||
            !IsBounded(request.SnapshotId, 128) || request.TimestampUtc is null)
        {
            failureReason = "transport_authenticated_binding_required";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool IsBounded(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
}
