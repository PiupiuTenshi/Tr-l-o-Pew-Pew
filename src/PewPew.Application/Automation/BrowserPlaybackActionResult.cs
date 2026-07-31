namespace PewPew.Application.Automation;

/// <summary>
/// Result record of executing a browser playback or tab switch action.
/// Contains success status, post-action readback evidence, ambiguity clarification options,
/// and failure reason.
/// </summary>
public sealed record BrowserPlaybackActionResult(
    bool IsSuccess,
    bool RequiresClarification,
    string? ClarificationMessage,
    IReadOnlyList<string>? ClarificationOptions,
    string? ReadbackEvidence,
    string? FailureReason)
{
    public static BrowserPlaybackActionResult VerifiedSuccess(string readbackEvidence) =>
        new(true, false, null, null, readbackEvidence, null);

    public static BrowserPlaybackActionResult Ambiguous(string message, IEnumerable<string> options) =>
        new(false, true, message, options.ToList().AsReadOnly(), null, "selector_ambiguity");

    public static BrowserPlaybackActionResult Failed(string failureReason) =>
        new(false, false, null, null, null, failureReason);
}
