using System.Diagnostics;
using PewPew.Application.EmergencyStop;
using PewPew.Domain.Workers;

namespace PewPew.Desktop;

/// <summary>
/// Stops only the root PID bound to an assistant-owned WorkerProcess. Windows
/// performs process-tree termination; no arbitrary process identifier crosses
/// the Application boundary.
/// </summary>
public sealed class WindowsWorkerProcessStopper : ILocalWorkerProcessStopper
{
    public async Task<LocalWorkerProcessStopResult> StopProcessTreeAsync(
        WorkerProcess worker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        cancellationToken.ThrowIfCancellationRequested();

        if (worker.RootProcessId is not int processId || processId <= 0)
        {
            return LocalWorkerProcessStopResult.Failed("worker_process_id_missing");
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return LocalWorkerProcessStopResult.Stopped("worker_process_already_exited");
            }

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return LocalWorkerProcessStopResult.Stopped("worker_process_tree_terminated");
        }
        catch (ArgumentException)
        {
            return LocalWorkerProcessStopResult.Stopped("worker_process_already_exited");
        }
        catch (InvalidOperationException)
        {
            return LocalWorkerProcessStopResult.Failed("worker_process_stop_invalid_state");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return LocalWorkerProcessStopResult.Failed("worker_process_stop_denied");
        }
    }
}
