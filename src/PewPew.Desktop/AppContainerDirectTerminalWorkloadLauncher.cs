using System.Security.Cryptography;
using PewPew.Application.Terminal;

namespace PewPew.Desktop;

/// <summary>
/// DEC-015 implementation. Desktop owns the AppContainer process handle and
/// the Job Object; an ordinary workload has no control IPC endpoint to expose.
/// Only a hash-pinned executable below the approved workload root may start.
/// </summary>
public sealed class AppContainerDirectTerminalWorkloadLauncher : IAppContainerTerminalWorkloadLauncher
{
    private readonly string _workloadRoot;

    public AppContainerDirectTerminalWorkloadLauncher(string? workloadRoot = null)
    {
        _workloadRoot = Path.GetFullPath(workloadRoot ?? WindowsAppContainerTerminalWorkerProvisioner.DefaultWorkerDirectory);
    }

    public bool HasAuthenticatedLocalControl => OperatingSystem.IsWindows() && Directory.Exists(_workloadRoot);

    public bool ProvidesRestrictedJobObject => HasAuthenticatedLocalControl;

    public async Task<TerminalProcessRunResult> LaunchAsync(
        TerminalProcessLaunchRequest request,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProcessStarted);
        Validate(request);

        var started = System.Diagnostics.Stopwatch.StartNew();
        using var launched = WindowsAppContainerWorkerLauncher.LaunchDirect(
            request.ExecutablePath,
            request.WorkingDirectory,
            request.Arguments);
        onProcessStarted(launched.ProcessId);
        var exitCode = await launched.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new TerminalProcessRunResult(exitCode, 0, false, 0, started.Elapsed);
    }

    private void Validate(TerminalProcessLaunchRequest request)
    {
        if (!HasAuthenticatedLocalControl)
        {
            throw new TerminalProcessBoundaryViolationException("appcontainer_workload_root_unavailable");
        }
        if (request.Binding is null || request.Environment is not null)
        {
            throw new TerminalProcessBoundaryViolationException("isolated_terminal_worker_binding_invalid");
        }
        if (string.IsNullOrWhiteSpace(request.ExpectedExecutableSha256Hash))
        {
            throw new TerminalProcessBoundaryViolationException("terminal_workload_hash_missing");
        }

        var executablePath = Path.GetFullPath(request.ExecutablePath);
        var workingDirectory = Path.GetFullPath(request.WorkingDirectory);
        if (!IsWithinRoot(executablePath) || !IsWithinRoot(workingDirectory))
        {
            throw new TerminalProcessBoundaryViolationException("appcontainer_workload_scope_denied");
        }
        if (!File.Exists(executablePath) || !Directory.Exists(workingDirectory))
        {
            throw new TerminalProcessBoundaryViolationException("appcontainer_workload_artifact_missing");
        }
        if (File.GetAttributes(executablePath).HasFlag(FileAttributes.ReparsePoint) ||
            File.GetAttributes(workingDirectory).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new TerminalProcessBoundaryViolationException("appcontainer_workload_scope_denied");
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(request.ExpectedExecutableSha256Hash);
        }
        catch (FormatException)
        {
            throw new TerminalProcessBoundaryViolationException("terminal_workload_hash_invalid");
        }

        var actualHash = SHA256.HashData(File.ReadAllBytes(executablePath));
        if (!CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash))
        {
            throw new TerminalProcessBoundaryViolationException("terminal_workload_hash_mismatch");
        }
    }

    private bool IsWithinRoot(string path)
    {
        var exactRoot = _workloadRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = exactRoot + Path.DirectorySeparatorChar;
        return string.Equals(path, exactRoot, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
