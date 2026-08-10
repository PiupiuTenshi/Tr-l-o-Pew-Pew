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
    public async Task<TerminalProcessRunResult> RunAsync(
        TerminalProcessLaunchRequest request,
        Action<int> onProcessStarted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProcessStarted);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

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
