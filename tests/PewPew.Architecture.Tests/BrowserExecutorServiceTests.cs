using PewPew.Application.Automation;
using PewPew.Application.BrowserExtension;
using PewPew.Domain.Automation;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Comprehensive tests for <see cref="BrowserExecutorService"/>:
/// binding gate denials, verified readback, unknown outcome,
/// readback mismatch, and cancellation.
/// </summary>
public sealed class BrowserExecutorServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly string TestOrigin = "https://www.youtube.com";
    private static readonly string TestTabId = "tab_42";

    // ── Helpers ──────────────────────────────────────────────────────────

    private sealed class FakeBrowserChannel : IBrowserExtensionChannel
    {
        public bool SendActionResult { get; set; } = true;
        public BrowserReadbackSnapshot? ReadbackResult { get; set; }
        public bool ThrowOnSend { get; set; }
        public bool ThrowOnReadback { get; set; }
        public int SendCallCount { get; private set; }
        public int ReadbackCallCount { get; private set; }

        public Task<bool> SendActionAsync(string tabId, BrowserActionKind actionKind, string? targetSelector, CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
            {
                throw new OperationCanceledException();
            }

            SendCallCount++;
            return Task.FromResult(SendActionResult);
        }

        public Task<BrowserReadbackSnapshot?> ReadbackAsync(string tabId, CancellationToken cancellationToken)
        {
            if (ThrowOnReadback)
            {
                throw new OperationCanceledException();
            }

            ReadbackCallCount++;
            return Task.FromResult(ReadbackResult);
        }
    }

    private static (BrowserExecutorService service, DesktopExtensionBridge bridge, BrowserTabContextManager tabContext, FakeBrowserChannel channel) CreateScenario(
        string[]? allowedOrigins = null)
    {
        var origins = allowedOrigins ?? [TestOrigin];
        var originPolicy = new ExtensionOriginPolicyValidator(origins);
        var bridge = new DesktopExtensionBridge(originPolicy);
        var tabContext = new BrowserTabContextManager();
        var channel = new FakeBrowserChannel();
        var service = new BrowserExecutorService(bridge, originPolicy, tabContext, channel);
        return (service, bridge, tabContext, channel);
    }

    private static (string sessionToken, UiTargetSnapshot snapshot, string nonce) SetupValidSession(
        DesktopExtensionBridge bridge,
        BrowserTabContextManager tabContext,
        long navigationGeneration = 1)
    {
        var sessionToken = bridge.IssueSessionToken(Now);
        var snapshot = tabContext.CaptureSnapshot(
            TestTabId, TestOrigin, "YouTube Video", "video#player", "Sample video", "none",
            Now, TimeSpan.FromSeconds(30), navigationGeneration);
        var nonce = Guid.NewGuid().ToString();
        return (sessionToken, snapshot, nonce);
    }

    private static BrowserExecutorRequest CreateRequest(
        string sessionToken, UiTargetSnapshot snapshot, string nonce,
        BrowserActionKind actionKind = BrowserActionKind.Play,
        string? targetOriginOverride = null,
        string? tabIdOverride = null,
        int? versionOverride = null,
        long? navGenOverride = null)
    {
        return new BrowserExecutorRequest(
            SessionToken: sessionToken,
            Nonce: nonce,
            TimestampUtc: Now,
            TargetOrigin: targetOriginOverride ?? TestOrigin,
            TabId: tabIdOverride ?? snapshot.TabId,
            SnapshotId: snapshot.SnapshotId,
            SnapshotVersion: versionOverride ?? snapshot.Version,
            NavigationGeneration: navGenOverride ?? snapshot.NavigationGeneration,
            ActionKind: actionKind,
            TargetSelector: "video#player");
    }

    private static BrowserReadbackSnapshot CreateValidReadback(long navigationGeneration = 1) =>
        new(TestTabId, "https://www.youtube.com/watch?v=abc", navigationGeneration, "playing", "visible");

    // ── Binding Gate Denial Tests ────────────────────────────────────────

    [Fact]
    public async Task DeniesWhenSessionTokenIsInvalid()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (_, snapshot, nonce) = SetupValidSession(bridge, tabContext);

        var request = CreateRequest("invalid_token_xyz", snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("session_token_invalid", result.ReasonCode);
        Assert.Null(result.ReadbackEvidence);
    }

    [Fact]
    public async Task DeniesWhenSessionTokenIsExpired()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        var expiredTime = Now.AddMinutes(20);

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, expiredTime, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("session_token_invalid", result.ReasonCode);
    }

    [Fact]
    public async Task DeniesWhenNonceIsReplayed()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ReadbackResult = CreateValidReadback();

        // First call succeeds
        var request1 = CreateRequest(sessionToken, snapshot, nonce);
        var result1 = await service.ExecuteAsync(request1, Now, CancellationToken.None);
        Assert.Equal(BrowserExecutorOutcome.Verified, result1.Outcome);

        // Replayed nonce denied
        var request2 = CreateRequest(sessionToken, snapshot, nonce);
        var result2 = await service.ExecuteAsync(request2, Now, CancellationToken.None);
        Assert.Equal(BrowserExecutorOutcome.Denied, result2.Outcome);
        Assert.Contains("replay", result2.ReasonCode, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeniesWhenOriginIsNotAllowlisted()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);

        var request = CreateRequest(sessionToken, snapshot, nonce, targetOriginOverride: "https://evil-site.com");
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
    }

    [Fact]
    public async Task DeniesWhenSnapshotIsExpired()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        var afterExpiry = Now.AddSeconds(31); // Snapshot TTL is 30s

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, afterExpiry, CancellationToken.None);

        // Token is still valid at +31s (15 min TTL), but snapshot is expired
        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("snapshot_not_active", result.ReasonCode);
    }

    [Fact]
    public async Task DeniesWhenSnapshotIsInvalidated()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        tabContext.InvalidateTabSnapshots(TestTabId, "tab_closed");

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("snapshot_not_active", result.ReasonCode);
    }

    [Fact]
    public async Task DeniesWhenSnapshotVersionMismatches()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);

        var request = CreateRequest(sessionToken, snapshot, nonce, versionOverride: 999);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("snapshot_version_mismatch", result.ReasonCode);
    }

    [Fact]
    public async Task DeniesWhenNavigationGenerationMismatches()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext, navigationGeneration: 5);

        var request = CreateRequest(sessionToken, snapshot, nonce, navGenOverride: 3);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("navigation_generation_mismatch", result.ReasonCode);
    }

    [Fact]
    public async Task DeniesWhenTabIdDoesNotMatchSnapshot()
    {
        var (service, bridge, tabContext, _) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);

        var request = CreateRequest(sessionToken, snapshot, nonce, tabIdOverride: "wrong_tab_99");
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("tab_id_mismatch", result.ReasonCode);
    }

    // ── Successful Execution Tests ──────────────────────────────────────

    [Fact]
    public async Task VerifiedWhenBindingPassesAndReadbackMatches()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ReadbackResult = CreateValidReadback();

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Verified, result.Outcome);
        Assert.True(result.IsVerified);
        Assert.NotNull(result.ReadbackEvidence);
        Assert.Contains("tab:tab_42", result.ReadbackEvidence);
        Assert.Contains("action:Play", result.ReadbackEvidence);
        Assert.Equal(1, channel.SendCallCount);
        Assert.Equal(1, channel.ReadbackCallCount);
    }

    [Fact]
    public async Task EvidenceContainsOnlyMetadataNoPageTextOrSecrets()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ReadbackResult = CreateValidReadback();

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.True(result.IsVerified);
        // Evidence must not contain page URL, DOM text, or secrets
        Assert.DoesNotContain("youtube.com", result.ReadbackEvidence!);
        Assert.DoesNotContain("watch?v=", result.ReadbackEvidence!);
    }

    // ── Unknown Outcome Tests ───────────────────────────────────────────

    [Fact]
    public async Task UnknownWhenReadbackIsLostAfterAction()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ReadbackResult = null; // readback lost

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Unknown, result.Outcome);
        Assert.Equal("readback_lost_after_action", result.ReasonCode);
        Assert.Null(result.ReadbackEvidence);
        Assert.Equal(1, channel.SendCallCount); // action WAS sent
    }

    [Fact]
    public async Task UnknownWhenReadbackIsCancelledAfterAction()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ThrowOnReadback = true; // readback cancelled

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Unknown, result.Outcome);
        Assert.Equal("readback_cancelled_after_action", result.ReasonCode);
    }

    // ── Readback Mismatch Tests ─────────────────────────────────────────

    [Fact]
    public async Task FailedWhenReadbackTabIdMismatches()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ReadbackResult = new BrowserReadbackSnapshot("different_tab", "https://www.youtube.com/watch?v=abc", 1, "playing", "visible");

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Failed, result.Outcome);
        Assert.Equal("readback_tab_mismatch", result.ReasonCode);
    }

    [Fact]
    public async Task FailedWhenReadbackNavigationGenerationChanged()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext, navigationGeneration: 1);
        channel.ReadbackResult = new BrowserReadbackSnapshot(TestTabId, "https://www.youtube.com/watch?v=abc", 2, "playing", "visible");

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Failed, result.Outcome);
        Assert.Equal("readback_navigation_changed", result.ReasonCode);
    }

    [Fact]
    public async Task FailedWhenReadbackOriginChanged()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ReadbackResult = new BrowserReadbackSnapshot(TestTabId, "https://evil-redirect.com/phishing", 1, "playing", "visible");

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Failed, result.Outcome);
        Assert.Equal("readback_origin_changed", result.ReasonCode);
    }

    // ── Channel Rejection Tests ─────────────────────────────────────────

    [Fact]
    public async Task FailedWhenChannelRejectsAction()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.SendActionResult = false;

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Failed, result.Outcome);
        Assert.Equal("channel_action_rejected", result.ReasonCode);
        Assert.Equal(0, channel.ReadbackCallCount); // readback never called
    }

    // ── Cancellation Tests ──────────────────────────────────────────────

    [Fact]
    public async Task DeniedWhenCancelledBeforeAction()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (sessionToken, snapshot, nonce) = SetupValidSession(bridge, tabContext);
        channel.ThrowOnSend = true;

        var request = CreateRequest(sessionToken, snapshot, nonce);
        var result = await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(BrowserExecutorOutcome.Denied, result.Outcome);
        Assert.Equal("cancelled_before_action", result.ReasonCode);
        Assert.Equal(0, channel.ReadbackCallCount);
    }

    // ── No Side-Effect on Denial Tests ──────────────────────────────────

    [Fact]
    public async Task NoSideEffectWhenBindingGateDenies()
    {
        var (service, bridge, tabContext, channel) = CreateScenario();
        var (_, snapshot, nonce) = SetupValidSession(bridge, tabContext);

        // Invalid token → denied before any channel call
        var request = CreateRequest("bad_token", snapshot, nonce);
        await service.ExecuteAsync(request, Now, CancellationToken.None);

        Assert.Equal(0, channel.SendCallCount);
        Assert.Equal(0, channel.ReadbackCallCount);
    }
}
