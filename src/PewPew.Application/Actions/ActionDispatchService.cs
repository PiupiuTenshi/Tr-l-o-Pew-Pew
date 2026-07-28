using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.Actions;

public sealed record ActionDispatchRequest(
    AssistantProfile Profile,
    ActionPlan Plan,
    PermissionGrant PermissionGrant,
    PermissionScope RequestedScope,
    ConfirmationRequest? Confirmation,
    ActionTask Task,
    EntityId UserId,
    EntityId SessionId,
    EntityId DeviceId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId);

public sealed record ActionDispatchResult(bool IsAllowed, string ReasonCode, AuditRecord AuditRecord);

public static class ActionDispatchService
{
    public static ActionDispatchResult Dispatch(ActionDispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reasonCode = Evaluate(request);
        if (reasonCode is not null)
        {
            return Denied(request, reasonCode);
        }

        if (request.Plan.Status == ActionPlanStatus.WaitingConfirmation)
        {
            request.Confirmation!.Consume(
                request.UserId,
                request.SessionId,
                request.DeviceId,
                request.Plan.Definition.Hash,
                request.OccurredAtUtc);
            request.Task.RequireConfirmation();
            request.Task.ConsumeConfirmation();
        }
        else
        {
            request.Task.Dispatch();
        }

        return Allowed(request);
    }

    private static string? Evaluate(ActionDispatchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            return "invalid_correlation";
        }

        if (request.Profile.Status != AssistantProfileStatus.Active)
        {
            return "assistant_not_active";
        }

        if (request.Profile.OwnerId != request.UserId ||
            request.Plan.Definition.Intent != request.RequestedScope.Skill ||
            request.Plan.Definition.Target != request.RequestedScope.Resource ||
            request.OccurredAtUtc >= request.Plan.ExpiresAtUtc ||
            request.OccurredAtUtc >= request.Task.TimeoutAtUtc)
        {
            return "invalid_action_context";
        }

        if (request.Task.Status != ActionTaskStatus.Queued ||
            request.Task.PlanId != request.Plan.Definition.Id ||
            request.Task.OwnerId != request.UserId ||
            request.RequestedScope.UserId != request.UserId ||
            request.RequestedScope.DeviceId != request.DeviceId)
        {
            return "invalid_action_context";
        }

        if (!request.PermissionGrant.Allows(request.RequestedScope, request.OccurredAtUtc))
        {
            return "permission_denied";
        }

        return request.Plan.Status switch
        {
            ActionPlanStatus.Approved when request.Confirmation is null => null,
            ActionPlanStatus.WaitingConfirmation when HasValidConfirmation(request) => null,
            ActionPlanStatus.WaitingConfirmation => "confirmation_invalid",
            _ => "plan_not_authorized"
        };
    }

    private static bool HasValidConfirmation(ActionDispatchRequest request) =>
        request.Confirmation is
        {
            Status: ConfirmationStatus.Requested,
            UserId: var userId,
            SessionId: var sessionId,
            DeviceId: var deviceId,
            PlanHash: var planHash,
            ExpiresAtUtc: var expiresAtUtc
        } &&
        userId == request.UserId &&
        sessionId == request.SessionId &&
        deviceId == request.DeviceId &&
        planHash == request.Plan.Definition.Hash &&
        request.OccurredAtUtc < expiresAtUtc;

    private static ActionDispatchResult Allowed(ActionDispatchRequest request) =>
        new(true, "allowed", CreateAudit(request, "allowed"));

    private static ActionDispatchResult Denied(ActionDispatchRequest request, string reasonCode) =>
        new(false, reasonCode, CreateAudit(request, reasonCode));

    private static AuditRecord CreateAudit(ActionDispatchRequest request, string policyResult)
    {
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? $"rejected-{Guid.NewGuid():N}"
            : request.CorrelationId;
        var audit = new AuditRecord(
            EntityId.New(),
            correlationId,
            request.UserId,
            request.DeviceId,
            request.DeviceId,
            "action_dispatch",
            policyResult);
        audit.Seal();
        return audit;
    }
}
