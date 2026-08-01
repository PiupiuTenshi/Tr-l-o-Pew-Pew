namespace PewPew.Application.Automation;

/// <summary>
/// Independent readback data returned by the browser extension after an action.
/// The executor compares this against the expected post-condition to determine
/// verified/failed/unknown outcome. Contains metadata only — no page text,
/// DOM content, secrets, or raw audio.
/// </summary>
public sealed record BrowserReadbackSnapshot(
    string TabId,
    string CurrentUrl,
    long NavigationGeneration,
    string? MediaState,
    string? ElementState);
