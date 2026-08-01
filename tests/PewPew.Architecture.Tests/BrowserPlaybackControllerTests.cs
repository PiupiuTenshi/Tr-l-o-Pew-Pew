using PewPew.Application.Automation;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit & security tests for <see cref="BrowserPlaybackController"/>, tab switching,
/// video element selection, playback control actions, ambiguity clarification,
/// and post-action readback verification.
/// </summary>
public sealed class BrowserPlaybackControllerTests
{
    private static readonly string SampleTabId = "tab_browser_100";

    [Fact]
    public void ExecuteActionFailsClosedUntilBrowserAdapterIsConfigured()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SwitchTab,
            TitlePattern: "YouTube Video Page");

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.False(result.IsSuccess);
        Assert.False(result.RequiresClarification);
        Assert.Null(result.ReadbackEvidence);
        Assert.Equal("browser_playback_adapter_not_configured", result.FailureReason);
    }

    [Fact]
    public void ExecuteActionSelectVideoFailsClosedUntilBrowserAdapterIsConfigured()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SelectVideo,
            TargetSelector: "video#main-player");

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("browser_playback_adapter_not_configured", result.FailureReason);
    }

    [Fact]
    public void ExecuteActionPlayAndPauseFailClosedUntilBrowserAdapterIsConfigured()
    {
        var playRequest = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.Play,
            TargetSelector: "video#player");

        var playResult = BrowserPlaybackController.ExecuteAction(playRequest);

        Assert.False(playResult.IsSuccess);
        Assert.Equal("browser_playback_adapter_not_configured", playResult.FailureReason);

        var pauseRequest = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.Pause,
            TargetSelector: "video#player");

        var pauseResult = BrowserPlaybackController.ExecuteAction(pauseRequest);

        Assert.False(pauseResult.IsSuccess);
        Assert.Equal("browser_playback_adapter_not_configured", pauseResult.FailureReason);
    }

    [Fact]
    public void ExecuteActionSeekFailsClosedUntilBrowserAdapterIsConfigured()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.Seek,
            TargetSelector: "video#player",
            PositionSeconds: 124.5);

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("browser_playback_adapter_not_configured", result.FailureReason);
    }

    [Fact]
    public void ExecuteActionSetVolumeFailsClosedUntilBrowserAdapterIsConfigured()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SetVolume,
            TargetSelector: "video#player",
            VolumePercent: 75);

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("browser_playback_adapter_not_configured", result.FailureReason);
    }

    [Fact]
    public void ExecuteActionRejectsAmbiguousVideoCandidates()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SelectVideo,
            TargetSelector: "video");

        var candidates = new[] { "video#player-1 (Main Stream)", "video#player-2 (Ad Preview)" };

        var result = BrowserPlaybackController.ExecuteAction(request, matchingCandidates: candidates);

        Assert.False(result.IsSuccess);
        Assert.True(result.RequiresClarification);
        Assert.Equal("selector_ambiguity", result.FailureReason);
        Assert.NotNull(result.ClarificationOptions);
        Assert.Equal(2, result.ClarificationOptions.Count);
    }

    [Fact]
    public void ExecuteActionRejectsNegativeSeekPosition()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.Seek,
            PositionSeconds: -10.0);

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_seek_position: Seek position cannot be negative", result.FailureReason);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(105)]
    public void ExecuteActionRejectsInvalidVolumeRange(int invalidVolume)
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SetVolume,
            VolumePercent: invalidVolume);

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_volume_percent: Volume must be between 0 and 100", result.FailureReason);
    }

    [Fact]
    public void ExecuteActionRejectsEmptyTabId()
    {
        var request = new BrowserPlaybackRequest(
            TabId: "   ",
            ActionKind: BrowserActionKind.Play);

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_tab_id: TabId is required", result.FailureReason);
    }
}
