namespace PewPew.Application.Automation;

/// <summary>
/// Bound execution request for a browser action. Every field participates in
/// the pre-I/O binding gate; a missing or mismatched field denies before any
/// side-effect reaches the extension.
/// </summary>
public sealed record BrowserExecutorRequest(
    string SessionToken,
    string Nonce,
    DateTimeOffset TimestampUtc,
    string TargetOrigin,
    string TabId,
    Guid SnapshotId,
    int SnapshotVersion,
    long NavigationGeneration,
    BrowserActionKind ActionKind,
    string? TargetSelector = null,
    string? PayloadHash = null);
