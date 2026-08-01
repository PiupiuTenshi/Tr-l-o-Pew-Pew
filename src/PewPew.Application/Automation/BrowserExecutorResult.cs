namespace PewPew.Application.Automation;

/// <summary>
/// Outcome of a browser executor operation. Four terminal states:
/// <list type="bullet">
///   <item><c>Verified</c> — readback confirmed the post-condition.</item>
///   <item><c>Failed</c> — binding denied or readback mismatch.</item>
///   <item><c>Unknown</c> — side-effect sent but readback lost/inconclusive. Never retried automatically.</item>
///   <item><c>Denied</c> — pre-I/O policy denial; no side-effect occurred.</item>
/// </list>
/// Audit evidence contains IDs and status hashes only — no page text, DOM, secrets, or raw audio.
/// </summary>
public sealed record BrowserExecutorResult(
    BrowserExecutorOutcome Outcome,
    string ReasonCode,
    string? ReadbackEvidence = null)
{
    public bool IsVerified => Outcome == BrowserExecutorOutcome.Verified;

    public static BrowserExecutorResult Verified(string readbackEvidence) =>
        new(BrowserExecutorOutcome.Verified, "verified", readbackEvidence);

    public static BrowserExecutorResult Failed(string reasonCode) =>
        new(BrowserExecutorOutcome.Failed, reasonCode);

    public static BrowserExecutorResult Unknown(string reasonCode) =>
        new(BrowserExecutorOutcome.Unknown, reasonCode);

    public static BrowserExecutorResult Denied(string reasonCode) =>
        new(BrowserExecutorOutcome.Denied, reasonCode);
}

/// <summary>
/// Terminal outcome classification for browser executor operations.
/// </summary>
public enum BrowserExecutorOutcome
{
    /// <summary>Independent readback confirmed the expected post-condition.</summary>
    Verified = 1,

    /// <summary>Binding denied or readback confirmed a mismatch with expected state.</summary>
    Failed = 2,

    /// <summary>Side-effect was sent but readback was lost or inconclusive. No automatic retry.</summary>
    Unknown = 3,

    /// <summary>Pre-I/O policy denial. No side-effect occurred.</summary>
    Denied = 4
}
