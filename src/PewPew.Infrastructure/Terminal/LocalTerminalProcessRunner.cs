using System.Diagnostics;
using System.Text;
using PewPew.Application.Terminal;

namespace PewPew.Infrastructure.Terminal;

/// <summary>
/// Local process adapter for structured workflows. It uses ProcessStartInfo's
/// argument list (never a shell), attaches the real PID immediately, bounds
/// output accounting, and kills its owned process tree on cancellation/quota.
/// </summary>
public sealed class LocalTerminalProcessRunner : ITerminalProcessRunner
{
    public bool ProvidesNetworkIsolation => IsNetworkIsolationAvailable();

    public async Task<TerminalProcessRunResult> RunAsync(
        TerminalProcessLaunchRequest request,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProcessStarted);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateBoundary(request);

        // Windows Process alone cannot enforce a per-process network deny. Do
        // not run a workflow merely because its quota says AllowNetworkAccess=false.
        if (!ProvidesNetworkIsolation)
        {
            throw new TerminalProcessBoundaryViolationException("terminal_network_isolation_unavailable");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Do not inherit desktop/session variables (including credentials). A
        // future isolated adapter must supply a separately reviewed allowlist.
        startInfo.Environment.Clear();

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            throw new InvalidOperationException("terminal_process_start_failed");
        }

        try
        {
            onProcessStarted(process.Id);
        }
        catch
        {
            KillProcessTree(process);
            throw;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var counter = new OutputCounter(request.Quota.MaxOutputSizeBytes, () => KillProcessTree(process));
        using var cancellationRegistration = cancellationToken.Register(() => KillProcessTree(process));

        await Task.WhenAll(
            CountOutputAsync(process.StandardOutput, counter),
            CountOutputAsync(process.StandardError, counter),
            process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var duration = Stopwatch.GetElapsedTime(startedAt);
        var peakRamMb = ReadPeakRamMb(process);

        return new TerminalProcessRunResult(
            process.ExitCode,
            counter.TotalBytes,
            counter.IsLimitExceeded,
            peakRamMb,
            duration);
    }

    private static void ValidateBoundary(TerminalProcessLaunchRequest request)
    {
        if (request.Quota.AllowNetworkAccess)
        {
            throw new TerminalProcessBoundaryViolationException("terminal_network_access_not_allowed");
        }

        if (request.Environment is { Count: > 0 })
        {
            throw new TerminalProcessBoundaryViolationException("terminal_environment_not_allowed");
        }

        if (!Path.IsPathFullyQualified(request.WorkingDirectory))
        {
            throw new TerminalProcessBoundaryViolationException("terminal_working_directory_not_absolute");
        }

        if (request.WorkingDirectory
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or ".."))
        {
            throw new TerminalProcessBoundaryViolationException("terminal_working_directory_traversal_denied");
        }

        if (!Directory.Exists(request.WorkingDirectory))
        {
            throw new TerminalProcessBoundaryViolationException("terminal_working_directory_not_found");
        }

        if ((File.GetAttributes(request.WorkingDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new TerminalProcessBoundaryViolationException("terminal_working_directory_reparse_point_denied");
        }
    }

    // A ProcessStartInfo process has no per-process network deny primitive. A
    // future Windows sandbox/AppContainer adapter may replace this probe only
    // after supplying enforceable isolation and its own integration evidence.
    private static bool IsNetworkIsolationAvailable() => false;

    private static async Task CountOutputAsync(StreamReader reader, OutputCounter counter)
    {
        var buffer = new char[1024];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }

            counter.Add(Encoding.UTF8.GetByteCount(buffer.AsSpan(0, count)));
        }
    }

    private static int ReadPeakRamMb(Process process)
    {
        try
        {
            return (int)Math.Min(int.MaxValue, process.PeakWorkingSet64 / (1024L * 1024L));
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and kill request.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The caller receives cancellation/unknown rather than a fabricated success.
        }
    }

    private sealed class OutputCounter(long limit, Action stopProcess)
    {
        private long _totalBytes;
        private int _limitExceeded;

        public long TotalBytes => Interlocked.Read(ref _totalBytes);

        public bool IsLimitExceeded => Volatile.Read(ref _limitExceeded) != 0;

        public void Add(int bytes)
        {
            var total = Interlocked.Add(ref _totalBytes, bytes);
            if (total > limit && Interlocked.Exchange(ref _limitExceeded, 1) == 0)
            {
                stopProcess();
            }
        }
    }
}
