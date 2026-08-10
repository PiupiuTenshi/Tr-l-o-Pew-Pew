using PewPew.Application.EmergencyStop;
using PewPew.Application.Workers;
using PewPew.Domain.Actions;
using PewPew.Domain.Audit;
using PewPew.Domain.Skills;
using PewPew.Domain.Workers;

namespace PewPew.Application.Actions;

/// <summary>
/// Result returned by <see cref="WorkerActionDispatcher"/>.
/// </summary>
public sealed record WorkerDispatchResult(
    bool IsAllowed,
    bool IsExecutedSuccessfully,
    string ReasonCode,
    AuditRecord AuditRecord,
    WorkerProcess? Worker = null,
    WorkerExecutionOutcome? Outcome = null);

/// <summary>
/// Dispatcher service that integrates Policy Engine verification, <see cref="SkillPackage"/> capability authorization,
/// worker process sandbox creation, worker ownership tracking, post-execution verification,
/// and task outcome reconciliation (<see cref="ActionTaskStatus.Completed"/>, <see cref="ActionTaskStatus.Unknown"/>, <see cref="ActionTaskStatus.Cancelled"/>, <see cref="ActionTaskStatus.Failed"/>).
/// </summary>
public static class WorkerActionDispatcher
{
    /// <summary>
    /// Dispatches an action request to a sandbox worker process, monitors execution,
    /// verifies post-action conditions, and reconciles the <see cref="ActionTask"/> state.
    /// </summary>
    public static async Task<WorkerDispatchResult> DispatchAndExecuteAsync(
        ActionDispatchRequest request,
        SkillPackage skillPackage,
        WorkerResourceQuota? quota,
        ILocalWorkerProcessStopper stopper,
        LocalWorkerOwnershipRegistry registry,
        Func<WorkerProcess, CancellationToken, Task<WorkerExecutionOutcome>> executionPayload,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(skillPackage);
        ArgumentNullException.ThrowIfNull(stopper);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(executionPayload);

        // 1. Evaluate policy via ActionDispatchService
        var policyResult = ActionDispatchService.Dispatch(request);
        if (!policyResult.IsAllowed)
        {
            return new WorkerDispatchResult(
                IsAllowed: false,
                IsExecutedSuccessfully: false,
                ReasonCode: policyResult.ReasonCode,
                AuditRecord: policyResult.AuditRecord);
        }

        // 2. Verify SkillPackage capability grant
        if (!skillPackage.CanExecuteCapability(request.RequestedScope.Skill))
        {
            request.Task.Fail("skill_capability_denied");
            return new WorkerDispatchResult(
                IsAllowed: true,
                IsExecutedSuccessfully: false,
                ReasonCode: "skill_capability_denied",
                AuditRecord: policyResult.AuditRecord);
        }

        // 3. Instantiate WorkerProcess via WorkerSandboxManager
        var worker = WorkerSandboxManager.CreateWorker(request.Task.Id, request.DeviceId, quota, now);
        worker.Start();
        // The worker adapter attaches a real PID after starting an OS process.
        // Never fabricate a PID: Emergency Stop must not target another process.
        worker.MarkRunning();

        // 4. Register in LocalWorkerOwnershipRegistry for Emergency Stop / cancellation tracking
        var registrationToken = registry.Register(
            request.Task,
            worker,
            stopper,
            new WorkerOwnershipBinding(request.PermissionGrant.Id, skillPackage.Id));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, registrationToken);


        // 5. Execute worker payload & verify post-action outcome
        try
        {
            var outcome = await executionPayload(worker, linkedCancellation.Token).ConfigureAwait(false);

            ReconcileTaskOutcome(request.Task, worker, outcome);

            var isSuccess = outcome.Status == WorkerExecutionStatus.Completed;
            return new WorkerDispatchResult(
                IsAllowed: true,
                IsExecutedSuccessfully: isSuccess,
                ReasonCode: isSuccess ? "allowed" : outcome.FailureReason ?? outcome.Status.ToString().ToLowerInvariant(),
                AuditRecord: policyResult.AuditRecord,
                Worker: worker,
                Outcome: outcome);
        }
        catch (OperationCanceledException)
        {
            if (request.Task.Status != ActionTaskStatus.Cancelled)
            {
                request.Task.Cancel();
            }

            if (worker.IsActive)
            {
                worker.EmergencyKill();
            }

            return new WorkerDispatchResult(
                IsAllowed: true,
                IsExecutedSuccessfully: false,
                ReasonCode: "task_cancelled",
                AuditRecord: policyResult.AuditRecord,
                Worker: worker,
                Outcome: WorkerExecutionOutcome.Cancelled("cancellation_token_triggered"));
        }
        catch (Exception ex)
        {
            request.Task.Fail($"worker_exception: {ex.Message}");
            if (worker.IsActive)
            {
                worker.MarkFailed(ex.Message);
            }

            return new WorkerDispatchResult(
                IsAllowed: true,
                IsExecutedSuccessfully: false,
                ReasonCode: "worker_exception",
                AuditRecord: policyResult.AuditRecord,
                Worker: worker,
                Outcome: WorkerExecutionOutcome.Failed(ex.Message));
        }
        finally
        {
            registry.Deregister(request.Task.Id);
        }
    }

    /// <summary>
    /// Reconciles an <see cref="ActionTask"/> that was in <see cref="ActionTaskStatus.Unknown"/> state
    /// back to <see cref="ActionTaskStatus.Completed"/> or <see cref="ActionTaskStatus.Failed"/>.
    /// </summary>
    public static bool ReconcileUnknownTask(ActionTask task, WorkerExecutionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(outcome);

        if (task.Status != ActionTaskStatus.Unknown)
        {
            return false;
        }

        if (outcome.Status == WorkerExecutionStatus.Completed && !string.IsNullOrWhiteSpace(outcome.VerificationEvidence))
        {
            task.ReconcileCompleted(outcome.VerificationEvidence);
            return true;
        }

        task.ReconcileFailed(outcome.FailureReason ?? "reconciliation_failed");
        return true;
    }

    private static void ReconcileTaskOutcome(ActionTask task, WorkerProcess worker, WorkerExecutionOutcome outcome)
    {
        switch (outcome.Status)
        {
            case WorkerExecutionStatus.Completed:
                var evidence = string.IsNullOrWhiteSpace(outcome.VerificationEvidence)
                    ? "worker_execution_verified"
                    : outcome.VerificationEvidence;
                task.Complete(evidence);
                if (worker.IsActive)
                {
                    worker.RequestStop();
                    worker.MarkStopped();
                }
                break;

            case WorkerExecutionStatus.Cancelled:
                task.Cancel();
                if (worker.IsActive)
                {
                    worker.EmergencyKill();
                }
                break;

            case WorkerExecutionStatus.Unknown:
                task.MarkOutcomeUnknown();
                if (worker.IsActive)
                {
                    worker.MarkFailed(outcome.FailureReason ?? "unknown_post_condition");
                }
                break;

            case WorkerExecutionStatus.Failed:
            default:
                task.Fail(outcome.FailureReason ?? "worker_execution_failed");
                if (worker.IsActive)
                {
                    worker.MarkFailed(outcome.FailureReason ?? "worker_execution_failed");
                }
                break;
        }
    }
}
