using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.EmergencyStop;

/// <summary>
/// Binds the domain emergency-stop decision to local task ownership. It is
/// deliberately local-only: after Safe Mode, no task is resumed or dispatched.
/// </summary>
public sealed class EmergencyStopExecutionService(LocalWorkerOwnershipRegistry ownership)
{
    private readonly LocalWorkerOwnershipRegistry _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));

    public async Task<EmergencyStopExecutionResult> ActivateAsync(
        AssistantProfile profile,
        IEnumerable<ActionTask> tasks,
        EmergencyStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(request);

        var taskList = tasks.ToArray();
        var domainResult = EmergencyStopController.Activate(profile, taskList, request);
        var stopResults = new List<LocalWorkerStopResult>();

        foreach (var task in taskList)
        {
            var result = await _ownership.StopAsync(task.Id).ConfigureAwait(false);
            stopResults.Add(result);

            if (task.Status == ActionTaskStatus.Running && _ownership.IsStopRequested(task.Id))
            {
                // A process-tree stop can race with a side effect. Unknown is
                // terminal for dispatch and requires explicit reconciliation.
                task.MarkOutcomeUnknown();
            }
        }

        return new EmergencyStopExecutionResult(
            domainResult.CancelledTaskIds,
            domainResult.ExternalStopRequiredTaskIds,
            stopResults,
            domainResult.AuditRecord);
    }
}

public sealed record EmergencyStopExecutionResult(
    IReadOnlyList<EntityId> CancelledTaskIds,
    IReadOnlyList<EntityId> ExternalStopRequiredTaskIds,
    IReadOnlyList<LocalWorkerStopResult> LocalWorkerStops,
    AuditRecord AuditRecord);
