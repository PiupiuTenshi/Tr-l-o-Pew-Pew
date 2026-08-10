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

    public CancellationToken Register(
        ActionTask task,
        WorkerProcess worker,
        ILocalWorkerProcessStopper stopper,
        WorkerOwnershipBinding? binding = null)
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

            if (!_registrations.TryAdd(task.Id, new Registration(task, worker, stopper, binding)))
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
            if (!registration.Worker.RootProcessId.HasValue)
            {
                return new LocalWorkerStopResult(taskId, false, "worker_cancellation_requested_before_process_attach", "root_process_not_attached");
            }

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

    /// <summary>
    /// Requests cancellation and process-tree stop for workers bound to exactly
    /// one permission grant. It never scans by capability or wildcard scope.
    /// </summary>
    public Task<IReadOnlyList<LocalWorkerStopResult>> StopByPermissionAsync(EntityId permissionGrantId) =>
        StopBoundAsync(binding => binding.PermissionGrantId == permissionGrantId);

    /// <summary>
    /// Requests cancellation and process-tree stop for workers bound to exactly
    /// one skill package. It does not stop unbound or unrelated workers.
    /// </summary>
    public Task<IReadOnlyList<LocalWorkerStopResult>> StopBySkillAsync(EntityId skillPackageId) =>
        StopBoundAsync(binding => binding.SkillPackageId == skillPackageId);

    /// <summary>Removes a completed worker registration and disposes its cancellation source.</summary>
    public void Deregister(EntityId taskId)
    {
        lock (_gate)
        {
            if (_registrations.Remove(taskId, out var registration))
            {
                registration.Cancellation.Dispose();
            }
        }
    }

    private async Task<IReadOnlyList<LocalWorkerStopResult>> StopBoundAsync(Func<WorkerOwnershipBinding, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        EntityId[] taskIds;
        lock (_gate)
        {
            taskIds = _registrations.Values
                .Where(registration => registration.Binding is not null && predicate(registration.Binding))
                .Select(registration => registration.Task.Id)
                .ToArray();
        }

        var results = new List<LocalWorkerStopResult>(taskIds.Length);
        foreach (var taskId in taskIds)
        {
            results.Add(await StopAsync(taskId).ConfigureAwait(false));
        }

        return results;
    }

    private sealed class Registration(
        ActionTask task,
        WorkerProcess worker,
        ILocalWorkerProcessStopper stopper,
        WorkerOwnershipBinding? binding)
    {
        public ActionTask Task { get; } = task;
        public WorkerProcess Worker { get; } = worker;
        public ILocalWorkerProcessStopper Stopper { get; } = stopper;
        public WorkerOwnershipBinding? Binding { get; } = binding;
        public CancellationTokenSource Cancellation { get; } = new();
        public Task<LocalWorkerProcessStopResult>? StopAttempt { get; set; }
    }
}

/// <summary>Exact authorization/package identity bound at worker registration time.</summary>
public sealed record WorkerOwnershipBinding(EntityId PermissionGrantId, EntityId SkillPackageId);

public sealed record LocalWorkerStopResult(EntityId TaskId, bool IsStopped, string Evidence, string? FailureReason)
{
    internal static LocalWorkerStopResult AlreadyStopped(EntityId taskId) =>
        new(taskId, true, "worker_not_registered_or_already_stopped", null);
}
