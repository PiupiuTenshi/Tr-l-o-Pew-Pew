using PewPew.Application.Actions;
using PewPew.Application.BrowserExtension;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.Automation;

/// <summary>In-memory, metadata-only active tab context learned from an authenticated extension poll.</summary>
public sealed class BrowserActiveTabContextStore
{
    private readonly object _sync = new();
    private readonly BrowserTabContextManager _snapshots = new();
    private BrowserActiveTabContext? _current;

    public void RecordAuthenticatedPoll(NativeMessagingTransportRequest request, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sync)
        {
            if (_current is { } existing && existing.ExpiresAtUtc > nowUtc &&
                existing.SessionToken == request.SessionToken && existing.Origin == request.TargetOrigin && existing.TabId == request.TabId)
            {
                return;
            }

            var snapshot = _snapshots.CaptureSnapshot(
                request.TabId!, request.TargetOrigin!, "Active browser tab", "media", string.Empty, "media",
                nowUtc, navigationGeneration: request.NavigationGeneration ?? 0);
            _current = new BrowserActiveTabContext(
                request.SessionToken!, snapshot.Origin, snapshot.TabId, snapshot.SnapshotId.ToString("N"), snapshot.Version,
                snapshot.NavigationGeneration, snapshot.ExpiresAtUtc);
        }
    }

    public bool TryGet(DateTimeOffset nowUtc, out BrowserActiveTabContext context)
    {
        lock (_sync)
        {
            if (_current is not null && _current.ExpiresAtUtc > nowUtc)
            {
                context = _current;
                return true;
            }
        }

        context = default!;
        return false;
    }
}

public sealed record BrowserActiveTabContext(string SessionToken, string Origin, string TabId, string SnapshotId, int SnapshotVersion, long NavigationGeneration, DateTimeOffset ExpiresAtUtc);
public sealed record BrowserMediaActionPrompt(string RequestId, string Action, string Origin, string TabId, DateTimeOffset ExpiresAtUtc);
public sealed record BrowserMediaActionResult(BrowserExecutorOutcome Outcome, string ReasonCode);

/// <summary>Application authority for the CR-014 single-use, confirmation-bound browser-media flow.</summary>
public sealed class BrowserMediaActionCoordinator
{
    private static readonly HashSet<string> Actions = ["play", "pause", "mute", "unmute"];
    private readonly BrowserActiveTabContextStore _contexts;
    private readonly Dictionary<string, BrowserActionControlRequest> _pending = new(StringComparer.Ordinal);
    private readonly EntityId _userId = EntityId.New();
    private readonly EntityId _deviceId = EntityId.New();
    private readonly EntityId _sessionId = EntityId.New();
    private readonly AssistantProfile _profile;

    public BrowserMediaActionCoordinator(BrowserActiveTabContextStore contexts)
    {
        _contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
        _profile = new AssistantProfile(EntityId.New(), _userId);
        _profile.CompleteProvisioning();
    }

    public BrowserMediaActionPrompt? RequestSingleUseAction(string action, DateTimeOffset nowUtc)
    {
        if (!Actions.Contains(action) || !_contexts.TryGet(nowUtc, out var context))
        {
            return null;
        }

        var scope = PermissionScope.Create(_userId, _deviceId, "browser_media", context.Origin, action, false);
        var grant = new PermissionGrant(EntityId.New(), scope, nowUtc.AddMinutes(1));
        grant.Submit();
        grant.Approve();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, "browser_media", context.Origin,
            $"{action}|{context.TabId}|{context.SnapshotId}|{context.SnapshotVersion}|{context.NavigationGeneration}");
        var plan = new ActionPlan(definition, nowUtc.AddMinutes(1));
        plan.SubmitForPolicyReview();
        plan.RequireConfirmation();
        var confirmation = new ConfirmationRequest(EntityId.New(), _userId, _sessionId, _deviceId, definition.Hash, nowUtc.AddMinutes(1));
        var task = new ActionTask(EntityId.New(), definition.Id, _userId, nowUtc.AddMinutes(1), supportsCancellation: true);
        var requestId = Guid.NewGuid().ToString("N");
        var authorization = new ActionDispatchRequest(_profile, plan, grant, scope, confirmation, task, _userId, _sessionId, _deviceId, nowUtc, requestId);
        var command = new NativeMessagingBrowserCommand(Guid.NewGuid().ToString("N"), context.SessionToken, context.Origin, context.TabId,
            context.SnapshotId, context.SnapshotVersion, context.NavigationGeneration, action, definition.Hash, requestId);
        _pending[requestId] = new BrowserActionControlRequest(authorization, command);
        return new BrowserMediaActionPrompt(requestId, action, context.Origin, context.TabId, nowUtc.AddMinutes(1));
    }

    public async Task<BrowserMediaActionResult> ConfirmAndExecuteAsync(string requestId, IVerifiedBrowserActionChannel channel, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (!_pending.Remove(requestId, out var request))
        {
            return new(BrowserExecutorOutcome.Denied, "browser_confirmation_missing_or_consumed");
        }

        if (nowUtc >= request.Authorization.Plan.ExpiresAtUtc || nowUtc >= request.Authorization.Task.TimeoutAtUtc)
        {
            return new(BrowserExecutorOutcome.Denied, "browser_confirmation_expired");
        }

        var authorization = request.Authorization with { OccurredAtUtc = nowUtc };
        var result = await BrowserActionControlPath.ExecuteAsync(new BrowserActionControlRequest(authorization, request.Command), channel, cancellationToken).ConfigureAwait(false);
        return new(result.Outcome, result.ReasonCode);
    }
}
