using PewPew.Domain.Actions;
using PewPew.Domain.Workers;
using PewPew.SharedKernel.Primitives;

namespace PewPew.Application.EmergencyStop;

/// <summary>
/// Owns the local cancellation token and process-tree stop operation for each
/// running ActionTask. Stop requests permanently block reuse of the task ID.
/// </summary>
public sealed class LocalWorkerOwnershipRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<EntityId, Registration> _registrations = [];
    private readonly HashSet<EntityId> _stopRequestedTaskIds = [];

    public CancellationToken Register(ActionTask task, WorkerProcess worker, ILocalWorkerProcessStopper stopper)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(stopper);

        if (task.Status != ActionTaskStatus.Running || worker.Status != WorkerProcessStatus.Running || worker.OwnerTaskId != task.Id)
        {
            throw new InvalidOperationException("Only a running worker bound to its running action task can be registered.");
        }

        lock (_gate)
        {
            if (_stopRequestedTaskIds.Contains(task.Id))
            {
                throw new InvalidOperationException("An emergency-stopped task cannot be registered or resumed.");
            }

            if (!_registrations.TryAdd(task.Id, new Registration(task, worker, stopper)))
            {
                throw new InvalidOperationException("A worker is already registered for this action task.");
            }

            return _registrations[task.Id].Cancellation.Token;
        }
    }

    public async Task<LocalWorkerStopResult> StopAsync(EntityId taskId)
    {
        Registration? registration;
        Task<LocalWorkerProcessStopResult>? inFlight;

        lock (_gate)
        {
            _stopRequestedTaskIds.Add(taskId);
            if (!_registrations.TryGetValue(taskId, out registration))
            {
                return LocalWorkerStopResult.AlreadyStopped(taskId);
            }

            registration.Cancellation.Cancel();
            inFlight = registration.StopAttempt ??= registration.Stopper.StopProcessTreeAsync(registration.Worker, CancellationToken.None);
        }

        LocalWorkerProcessStopResult processResult;
        try
        {
            processResult = await inFlight.ConfigureAwait(false);
        }
        catch (Exception)
        {
            processResult = LocalWorkerProcessStopResult.Failed("worker_stop_adapter_failed");
        }

        lock (_gate)
        {
            if (processResult.IsStopped)
            {
                if (registration.Worker.Status is WorkerProcessStatus.Starting or WorkerProcessStatus.Running or WorkerProcessStatus.Stopping)
                {
                    registration.Worker.EmergencyKill();
                }

                _registrations.Remove(taskId);
            }
            else
            {
                registration.StopAttempt = null;
            }
        }

        return new LocalWorkerStopResult(taskId, processResult.IsStopped, processResult.Evidence, processResult.FailureReason);
    }

    public bool IsStopRequested(EntityId taskId)
    {
        lock (_gate)
        {
            return _stopRequestedTaskIds.Contains(taskId);
        }
    }

    private sealed class Registration(ActionTask task, WorkerProcess worker, ILocalWorkerProcessStopper stopper)
    {
        public ActionTask Task { get; } = task;
        public WorkerProcess Worker { get; } = worker;
        public ILocalWorkerProcessStopper Stopper { get; } = stopper;
        public CancellationTokenSource Cancellation { get; } = new();
        public Task<LocalWorkerProcessStopResult>? StopAttempt { get; set; }
    }
}

public sealed record LocalWorkerStopResult(EntityId TaskId, bool IsStopped, string Evidence, string? FailureReason)
{
    internal static LocalWorkerStopResult AlreadyStopped(EntityId taskId) =>
        new(taskId, true, "worker_not_registered_or_already_stopped", null);
}
