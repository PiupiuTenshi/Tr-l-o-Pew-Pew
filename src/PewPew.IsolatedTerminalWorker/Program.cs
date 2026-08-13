using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using PewPew.Application.Terminal;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    return 64;
}

try
{
    await using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipe.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
    var invocation = await IsolatedTerminalWorkerFrameCodec.ReadAsync<IsolatedTerminalWorkerInvocation>(pipe, CancellationToken.None).ConfigureAwait(false);
    if (invocation is null || !IsValid(invocation))
    {
        await IsolatedTerminalWorkerFrameCodec.WriteAsync(pipe, IsolatedTerminalWorkerResponse.Denied("isolated_terminal_worker_binding_invalid"), CancellationToken.None).ConfigureAwait(false);
        return 65;
    }

    var result = await RunAsync(invocation.LaunchRequest, CancellationToken.None).ConfigureAwait(false);
    await IsolatedTerminalWorkerFrameCodec.WriteAsync(pipe, IsolatedTerminalWorkerResponse.Completed(result), CancellationToken.None).ConfigureAwait(false);
    return 0;
}
catch (OperationCanceledException)
{
    return 1223;
}
catch
{
    return 1;
}

static bool IsValid(IsolatedTerminalWorkerInvocation invocation) =>
    invocation.LaunchRequest.Binding == invocation.Binding &&
    invocation.LaunchRequest.Environment is null &&
    !string.IsNullOrWhiteSpace(invocation.Binding.CorrelationId) &&
    !string.IsNullOrWhiteSpace(invocation.Binding.Nonce) &&
    Path.IsPathFullyQualified(invocation.LaunchRequest.ExecutablePath) &&
    Path.IsPathFullyQualified(invocation.LaunchRequest.WorkingDirectory);

static async Task<TerminalProcessRunResult> RunAsync(TerminalProcessLaunchRequest request, CancellationToken cancellationToken)
{
    var started = Stopwatch.StartNew();
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    foreach (var argument in request.Arguments)
    {
        process.StartInfo.ArgumentList.Add(argument);
    }

    if (!process.Start())
    {
        throw new InvalidOperationException("isolated_terminal_worker_process_start_failed");
    }

    var output = DrainAsync(process.StandardOutput, request.Quota.MaxOutputSizeBytes, cancellationToken);
    var error = DrainAsync(process.StandardError, request.Quota.MaxOutputSizeBytes, cancellationToken);
    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    var outputBytes = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
    return new TerminalProcessRunResult(
        process.ExitCode,
        outputBytes,
        outputBytes > request.Quota.MaxOutputSizeBytes,
        (int)Math.Max(1, process.PeakWorkingSet64 / (1024 * 1024)),
        started.Elapsed);
}

static async Task<long> DrainAsync(StreamReader reader, long maximum, CancellationToken cancellationToken)
{
    var buffer = new char[512];
    long total = 0;
    while (total <= maximum)
    {
        var read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            break;
        }
        total += System.Text.Encoding.UTF8.GetByteCount(buffer, 0, read);
    }
    return total;
}

internal sealed record IsolatedTerminalWorkerResponse(string Status, string ReasonCode, TerminalProcessRunResult? Result)
{
    public static IsolatedTerminalWorkerResponse Completed(TerminalProcessRunResult result) => new("completed", string.Empty, result);
    public static IsolatedTerminalWorkerResponse Denied(string reasonCode) => new("denied", reasonCode, null);
}

internal static class IsolatedTerminalWorkerFrameCodec
{
    private const int MaximumFrameBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        if (!await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return default;
        }
        var length = BitConverter.ToInt32(header, 0);
        if (length <= 0 || length > MaximumFrameBytes)
        {
            throw new InvalidDataException("isolated_worker_frame_invalid");
        }
        var payload = new byte[length];
        if (!await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false))
        {
            throw new EndOfStreamException();
        }
        return JsonSerializer.Deserialize<T>(payload, Options);
    }

    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        if (payload.Length == 0 || payload.Length > MaximumFrameBytes)
        {
            throw new InvalidDataException("isolated_worker_frame_invalid");
        }
        await stream.WriteAsync(BitConverter.GetBytes(payload.Length), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }
            offset += read;
        }
        return true;
    }
}
