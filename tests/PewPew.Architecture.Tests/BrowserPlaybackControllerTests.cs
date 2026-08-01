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
    public void ExecuteActionSwitchTabReturnsVerifiedReadback()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SwitchTab,
            TitlePattern: "YouTube Video Page");

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.True(result.IsSuccess);
        Assert.False(result.RequiresClarification);
        Assert.NotNull(result.ReadbackEvidence);
        Assert.Contains("SwitchTab", result.ReadbackEvidence);
        Assert.Contains(SampleTabId, result.ReadbackEvidence);
        Assert.Contains("YouTube Video Page", result.ReadbackEvidence);
    }

    [Fact]
    public void ExecuteActionSelectVideoReturnsVerifiedReadback()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SelectVideo,
            TargetSelector: "video#main-player");

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ReadbackEvidence);
        Assert.Contains("SelectVideo", result.ReadbackEvidence);
        Assert.Contains("video#main-player", result.ReadbackEvidence);
    }

    [Fact]
    public void ExecuteActionPlayAndPauseReturnsVerifiedReadback()
    {
        var playRequest = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.Play,
            TargetSelector: "video#player");

        var playResult = BrowserPlaybackController.ExecuteAction(playRequest);

        Assert.True(playResult.IsSuccess);
        Assert.Contains("PlayerState = Playing", playResult.ReadbackEvidence);

        var pauseRequest = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.Pause,
            TargetSelector: "video#player");

        var pauseResult = BrowserPlaybackController.ExecuteAction(pauseRequest);

        Assert.True(pauseResult.IsSuccess);
        Assert.Contains("PlayerState = Paused", pauseResult.ReadbackEvidence);
    }

    [Fact]
    public void ExecuteActionSeekAppliesPositionReadback()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.Seek,
            TargetSelector: "video#player",
            PositionSeconds: 124.5);

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("Position = 124.5s", result.ReadbackEvidence);
        Assert.Contains("PlayerState = Seeked", result.ReadbackEvidence);
    }

    [Fact]
    public void ExecuteActionSetVolumeAppliesVolumeReadback()
    {
        var request = new BrowserPlaybackRequest(
            TabId: SampleTabId,
            ActionKind: BrowserActionKind.SetVolume,
            TargetSelector: "video#player",
            VolumePercent: 75);

        var result = BrowserPlaybackController.ExecuteAction(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("Volume = 75%", result.ReadbackEvidence);
        Assert.Contains("PlayerState = VolumeSet", result.ReadbackEvidence);
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
