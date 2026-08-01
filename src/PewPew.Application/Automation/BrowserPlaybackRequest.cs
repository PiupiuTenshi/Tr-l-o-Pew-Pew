namespace PewPew.Application.Automation;

/// <summary>
/// Request record for browser tab switching, video selection, or playback control actions.
/// </summary>
public sealed record BrowserPlaybackRequest(
    string TabId,
    BrowserActionKind ActionKind,
    string? TargetSelector = null,
    string? TitlePattern = null,
    double? PositionSeconds = null,
    int? VolumePercent = null);
