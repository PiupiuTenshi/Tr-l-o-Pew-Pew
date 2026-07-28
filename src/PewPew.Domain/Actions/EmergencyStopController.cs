using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Domain.Actions;

public sealed record EmergencyStopRequest(
    string CorrelationId,
    EntityId ActorId,
    EntityId SourceDeviceId,
    EntityId TargetDeviceId);

public sealed record EmergencyStopResult(
    IReadOnlyList<EntityId> CancelledTaskIds,
    IReadOnlyList<EntityId> ExternalStopRequiredTaskIds,
    AuditRecord AuditRecord);

public static class EmergencyStopController
{
    public static EmergencyStopResult Activate(
        AssistantProfile profile,
        IEnumerable<ActionTask> tasks,
        EmergencyStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            throw new ArgumentException("Correlation ID is required.", nameof(request));
        }

        if (profile.Status is AssistantProfileStatus.Active or AssistantProfileStatus.Paused)
        {
            profile.EnterSafeMode();
        }

        if (profile.Status != AssistantProfileStatus.SafeMode)
        {
            throw new InvalidOperationException("Emergency Stop requires an active or paused assistant profile.");
        }

        var cancelledTaskIds = new List<EntityId>();
        var externalStopRequiredTaskIds = new List<EntityId>();

        foreach (var task in tasks)
        {
            if (task.Status is ActionTaskStatus.Queued or ActionTaskStatus.WaitingConfirmation)
            {
                task.Cancel();
                cancelledTaskIds.Add(task.Id);
                continue;
            }

            if (task.Status == ActionTaskStatus.Running && task.SupportsCancellation)
            {
                task.Cancel();
                cancelledTaskIds.Add(task.Id);
                continue;
            }

            if (task.Status == ActionTaskStatus.Running)
            {
                externalStopRequiredTaskIds.Add(task.Id);
            }
        }

        var audit = new AuditRecord(
            EntityId.New(),
            request.CorrelationId,
            request.ActorId,
            request.SourceDeviceId,
            request.TargetDeviceId,
            "emergency_stop",
            "safe_mode_activated");
        audit.Seal();

        return new EmergencyStopResult(cancelledTaskIds, externalStopRequiredTaskIds, audit);
    }

    public static void Dispatch(AssistantProfile profile, ActionTask task)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(task);

        if (profile.Status != AssistantProfileStatus.Active)
        {
            throw new InvalidOperationException("The assistant control plane is not active; action dispatch is denied.");
        }

        task.Dispatch();
    }
}
