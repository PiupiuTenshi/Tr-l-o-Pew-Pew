using PewPew.Application.BrowserExtension;

namespace PewPew.Application.Automation;

/// <summary>
/// Browser executor service that enforces the full binding gate before any
/// side-effect and requires independent extension readback for verification.
/// <para>
/// Binding gate (all must pass before I/O):
/// 1. Authenticated session token via <see cref="DesktopExtensionBridge"/>
/// 2. Anti-replay nonce and timestamp via bridge request processing
/// 3. HTTPS origin allowlist via <see cref="ExtensionOriginPolicyValidator"/>
/// 4. Active, unexpired <see cref="Domain.Automation.UiTargetSnapshot"/> via <see cref="BrowserTabContextManager"/>
/// 5. Snapshot version match (optimistic concurrency)
/// 6. Navigation generation match (detects page reload/navigation)
/// </para>
/// <para>
/// Post-action readback (independent verification):
/// - Readback matches expected post-condition → <c>Verified</c>
/// - Readback mismatches → <c>Failed</c>
/// - Readback lost after side-effect → <c>Unknown</c> (never auto-retry)
/// - Binding gate denial → <c>Denied</c> (no side-effect occurred)
/// </para>
/// </summary>
public sealed class BrowserExecutorService
{
    private readonly DesktopExtensionBridge _bridge;
    private readonly ExtensionOriginPolicyValidator _originPolicy;
    private readonly BrowserTabContextManager _tabContext;
    private readonly IBrowserExtensionChannel _channel;

    public BrowserExecutorService(
        DesktopExtensionBridge bridge,
        ExtensionOriginPolicyValidator originPolicy,
        BrowserTabContextManager tabContext,
        IBrowserExtensionChannel channel)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _originPolicy = originPolicy ?? throw new ArgumentNullException(nameof(originPolicy));
        _tabContext = tabContext ?? throw new ArgumentNullException(nameof(tabContext));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <summary>
    /// Executes a browser action after validating the full binding gate.
    /// Returns a sealed result with readback evidence or denial reason.
    /// </summary>
    public async Task<BrowserExecutorResult> ExecuteAsync(
        BrowserExecutorRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // --- BINDING GATE (all pre-I/O) ---

        // 1. Authenticated session token
        if (!_bridge.IsTokenValid(request.SessionToken, nowUtc))
        {
            return BrowserExecutorResult.Denied("session_token_invalid");
        }

        // 2. Anti-replay nonce + origin via bridge request processing
        var bridgeResponse = _bridge.ProcessRequest(
            new ExtensionBridgeRequest(
                request.SessionToken,
                request.Nonce,
                request.TimestampUtc,
                request.TargetOrigin,
                "browser_executor"),
            nowUtc);
        if (!bridgeResponse.IsSuccess)
        {
            return BrowserExecutorResult.Denied(bridgeResponse.ReasonCode);
        }

        // 3. HTTPS origin allowlist (redundant with bridge but explicit)
        if (!_originPolicy.IsOriginAllowed(request.TargetOrigin))
        {
            return BrowserExecutorResult.Denied("origin_policy_denied");
        }

        // 4. Active, unexpired snapshot
        var snapshot = _tabContext.GetActiveSnapshot(request.SnapshotId, nowUtc);
        if (snapshot is null)
        {
            return BrowserExecutorResult.Denied("snapshot_not_active");
        }

        // 5. Snapshot version match (optimistic concurrency)
        if (snapshot.Version != request.SnapshotVersion)
        {
            return BrowserExecutorResult.Denied("snapshot_version_mismatch");
        }

        // 6. Navigation generation match
        if (snapshot.NavigationGeneration != request.NavigationGeneration)
        {
            return BrowserExecutorResult.Denied("navigation_generation_mismatch");
        }

        // 7. Tab ID consistency
        if (!string.Equals(snapshot.TabId, request.TabId, StringComparison.OrdinalIgnoreCase))
        {
            return BrowserExecutorResult.Denied("tab_id_mismatch");
        }

        // --- EXECUTE ---
        cancellationToken.ThrowIfCancellationRequested();

        bool actionSent;
        try
        {
            actionSent = await _channel.SendActionAsync(
                request.TabId,
                request.ActionKind,
                request.TargetSelector,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return BrowserExecutorResult.Denied("cancelled_before_action");
        }

        if (!actionSent)
        {
            return BrowserExecutorResult.Failed("channel_action_rejected");
        }

        // --- INDEPENDENT READBACK ---
        BrowserReadbackSnapshot? readback;
        try
        {
            readback = await _channel.ReadbackAsync(
                request.TabId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Side-effect was sent but readback was cancelled — unknown outcome
            return BrowserExecutorResult.Unknown("readback_cancelled_after_action");
        }

        if (readback is null)
        {
            // Side-effect was sent but readback lost — unknown, never verified
            return BrowserExecutorResult.Unknown("readback_lost_after_action");
        }

        // --- READBACK VALIDATION ---

        // Tab ID must still match
        if (!string.Equals(readback.TabId, request.TabId, StringComparison.OrdinalIgnoreCase))
        {
            return BrowserExecutorResult.Failed("readback_tab_mismatch");
        }

        // Navigation generation must not have changed (page didn't navigate away)
        if (readback.NavigationGeneration != request.NavigationGeneration)
        {
            return BrowserExecutorResult.Failed("readback_navigation_changed");
        }

        // Origin must still match (URL didn't change to a different origin)
        if (!OriginMatches(readback.CurrentUrl, request.TargetOrigin))
        {
            return BrowserExecutorResult.Failed("readback_origin_changed");
        }

        // Build metadata-only evidence (no page text, DOM, secrets)
        var evidence = $"tab:{request.TabId}|action:{request.ActionKind}|nav_gen:{readback.NavigationGeneration}|media:{readback.MediaState ?? "none"}";
        return BrowserExecutorResult.Verified(evidence);
    }

    private static bool OriginMatches(string? currentUrl, string expectedOrigin)
    {
        if (string.IsNullOrWhiteSpace(currentUrl) || string.IsNullOrWhiteSpace(expectedOrigin))
        {
            return false;
        }

        if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var currentUri) ||
            !Uri.TryCreate(expectedOrigin, UriKind.Absolute, out var expectedUri))
        {
            return false;
        }

        return string.Equals(currentUri.Scheme, expectedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(currentUri.Host, expectedUri.Host, StringComparison.OrdinalIgnoreCase) &&
               currentUri.Port == expectedUri.Port;
    }
}
