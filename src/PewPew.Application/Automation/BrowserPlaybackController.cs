namespace PewPew.Application.Automation;

/// <summary>
/// Controller service for browser tab switching, video element selection, and media playback control.
/// Resolves target ambiguities by requesting user clarification and generates verified post-action
/// readback evidence.
/// </summary>
public sealed class BrowserPlaybackController
{
    private readonly BrowserTabContextManager _tabContextManager;

    public BrowserPlaybackController(BrowserTabContextManager? tabContextManager = null)
    {
        _tabContextManager = tabContextManager ?? new BrowserTabContextManager();
    }

    /// <summary>
    /// Executes a browser tab switch or video playback action.
    /// Returns <see cref="BrowserPlaybackActionResult"/> with verified readback evidence or clarification request.
    /// </summary>
    public static BrowserPlaybackActionResult ExecuteAction(

        BrowserPlaybackRequest request,
        IEnumerable<string>? matchingCandidates = null,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentUtc = nowUtc ?? DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(request.TabId))
        {
            return BrowserPlaybackActionResult.Failed("invalid_tab_id: TabId is required");
        }

        // 1. Ambiguity Resolution Check
        if (matchingCandidates != null)
        {
            var candidateList = matchingCandidates.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (candidateList.Count > 1)
            {
                return BrowserPlaybackActionResult.Ambiguous(
                    "Multiple matching video targets found. Clarification required.",
                    candidateList);
            }
        }

        if (request.ActionKind == BrowserActionKind.Seek && (request.PositionSeconds ?? 0) < 0)
        {
            return BrowserPlaybackActionResult.Failed("invalid_seek_position: Seek position cannot be negative");
        }

        if (request.ActionKind == BrowserActionKind.SetVolume && (request.VolumePercent ?? 100) is < 0 or > 100)
        {
            return BrowserPlaybackActionResult.Failed("invalid_volume_percent: Volume must be between 0 and 100");
        }

        // This controller has no extension action executor or independent browser
        // readback. Refuse the command until one is wired, rather than inventing
        // a verified result from request fields.
        return BrowserPlaybackActionResult.Failed("browser_playback_adapter_not_configured");
    }
}
