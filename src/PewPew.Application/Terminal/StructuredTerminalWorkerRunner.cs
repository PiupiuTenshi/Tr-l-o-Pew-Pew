using PewPew.Application.Actions;
using PewPew.Domain.Terminal;
using PewPew.Domain.Workers;

namespace PewPew.Application.Terminal;

/// <summary>
/// Executes one active, hash-pinned terminal workflow through a worker-owned
/// process adapter. This service deliberately contains no OS process calls.
/// </summary>
public static class StructuredTerminalWorkerRunner
{
    public static async Task<WorkerExecutionOutcome> ExecuteAsync(
        TerminalWorkflowDefinition workflow,
        IReadOnlyDictionary<string, string>? parameterValues,
        string providedSha256Hash,
        WorkerProcess worker,
        ITerminalProcessRunner processRunner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(processRunner);

        if (worker.Status != WorkerProcessStatus.Running)
        {
            throw new InvalidOperationException("Terminal execution requires a running worker.");
        }

        var remaining = worker.TimeoutAtUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            worker.CheckTimeout(DateTimeOffset.UtcNow);
            return WorkerExecutionOutcome.Unknown("terminal_execution_timeout");
        }

        var prepared = TerminalWorkflowService.PrepareExecution(
            workflow,
            parameterValues is null ? null : new Dictionary<string, string>(parameterValues),
            providedSha256Hash,
            DateTimeOffset.UtcNow);

        if (processRunner is not IIsolatedTerminalWorkerHost isolatedHost ||
            !isolatedHost.ProvidesNetworkIsolation ||
            !HasRequiredIsolationControls(isolatedHost.GetReadiness()))
        {
            return WorkerExecutionOutcome.Failed("terminal_network_isolation_unavailable");
        }

        using var timeout = new CancellationTokenSource(remaining);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            var binding = IsolatedTerminalWorkerBinding.Create(
                worker.Id.ToString(),
                workflow.Id.ToString(),
                workflow.Version,
                workflow.ExpectedSha256Hash);
            var result = await processRunner.RunAsync(
                new TerminalProcessLaunchRequest(
                    prepared.ExecutablePath,
                    prepared.BoundArguments,
                    prepared.WorkingDirectoryRoot,
                    worker.Quota,
                    Binding: binding,
                    ExpectedExecutableSha256Hash: prepared.ExpectedExecutableSha256Hash),
                worker.AttachRootProcessId,
                linkedCancellation.Token).ConfigureAwait(false);

            worker.RecordResourceUsage(result.PeakRamMb, result.OutputBytes);
            if (result.OutputLimitExceeded || worker.Status == WorkerProcessStatus.Failed)
            {
                return WorkerExecutionOutcome.Unknown("terminal_output_or_resource_quota_exceeded");
            }

            if (result.ExitCode != 0)
            {
                return WorkerExecutionOutcome.Failed($"terminal_exit_code_{result.ExitCode}");
            }

            return WorkerExecutionOutcome.Success(
                $"terminal_exit_code_0;duration_ms={(long)result.Duration.TotalMilliseconds};output_bytes={result.OutputBytes}");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            worker.CheckTimeout(DateTimeOffset.UtcNow);
            return WorkerExecutionOutcome.Unknown("terminal_execution_timeout");
        }
        catch (OperationCanceledException)
        {
            return WorkerExecutionOutcome.Cancelled("terminal_execution_cancelled");
        }
        catch (TerminalProcessBoundaryViolationException exception)
        {
            return WorkerExecutionOutcome.Failed(exception.ReasonCode);
        }
        catch (Exception)
        {
            return WorkerExecutionOutcome.Failed(
                worker.RootProcessId.HasValue
                    ? "terminal_process_failed"
                    : "terminal_process_start_failed");
        }
    }

    private static bool HasRequiredIsolationControls(IsolatedTerminalWorkerReadiness readiness) =>
        readiness.IsReady &&
        readiness.HasNetworkDeniedAppContainer &&
        readiness.HasRestrictedJobObject &&
        readiness.HasAuthenticatedLocalControl;
}
