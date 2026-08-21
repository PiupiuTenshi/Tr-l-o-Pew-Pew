using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using PewPew.Application.Terminal;

if (args.Length == 1 && string.Equals(args[0], "--pewpew-child-fixture", StringComparison.Ordinal))
{
    return WorkerIsolation.IsCurrentProcessAppContainer() ? 0 : 77;
}

// P03-T27 manual fixtures exercise only direct AppContainer ownership. They
// accept no user-controlled command and never create a child process.
if (args.Length == 1 && string.Equals(args[0], "--pewpew-direct-fixture", StringComparison.Ordinal))
{
    return WorkerIsolation.IsCurrentProcessAppContainer() ? 0 : 77;
}

if (args.Length == 1 && string.Equals(args[0], "--pewpew-direct-cancellation-fixture", StringComparison.Ordinal))
{
    if (!WorkerIsolation.IsCurrentProcessAppContainer())
    {
        return 77;
    }

    await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
    return 78;
}

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    return 64;
}

if (!WorkerIsolation.IsCurrentProcessAppContainer())
{
    return 77;
}

await using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
var stage = "connect";
try
{
    await pipe.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
    stage = "read";
    var wireInvocation = await IsolatedTerminalWorkerFrameCodec.ReadAsync<IsolatedTerminalWorkerWireInvocation>(pipe, CancellationToken.None).ConfigureAwait(false);
    stage = "validate";
    var invocation = wireInvocation?.ToInvocation();
    if (invocation is null || !IsValid(invocation))
    {
        await IsolatedTerminalWorkerFrameCodec.WriteAsync(pipe, IsolatedTerminalWorkerResponse.Denied("isolated_terminal_worker_binding_invalid"), CancellationToken.None).ConfigureAwait(false);
        return 65;
    }

    // The manual evidence fixture verifies only the AppContainer launch and
    // authenticated pipe boundary. It must not start a child process.
    stage = "execute";
    var result = IsHarmlessFixture(invocation.LaunchRequest)
        ? new TerminalProcessRunResult(0, 0, false, 1, TimeSpan.Zero)
        : await RunAsync(invocation.LaunchRequest, CancellationToken.None).ConfigureAwait(false);
    stage = "write";
    await IsolatedTerminalWorkerFrameCodec.WriteAsync(pipe, IsolatedTerminalWorkerResponse.Completed(result), CancellationToken.None).ConfigureAwait(false);
    return 0;
}
catch (OperationCanceledException)
{
    return 1223;
}
catch
{
    if (pipe.IsConnected)
    {
        try
        {
            await IsolatedTerminalWorkerFrameCodec.WriteAsync(
                pipe,
                IsolatedTerminalWorkerResponse.Denied($"isolated_terminal_worker_{stage}_failed"),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // The desktop side treats an absent response as an uncertain result.
        }
    }
    return 1;
}

static bool IsValid(IsolatedTerminalWorkerInvocation invocation) =>
    invocation.LaunchRequest.Binding == invocation.Binding &&
    invocation.LaunchRequest.Environment is null &&
    !string.IsNullOrWhiteSpace(invocation.Binding.CorrelationId) &&
    !string.IsNullOrWhiteSpace(invocation.Binding.Nonce) &&
    Path.IsPathFullyQualified(invocation.LaunchRequest.ExecutablePath) &&
    Path.IsPathFullyQualified(invocation.LaunchRequest.WorkingDirectory);

static bool IsHarmlessFixture(TerminalProcessLaunchRequest request) =>
    request.Arguments.Count == 1 &&
    string.Equals(request.Arguments[0], "--pewpew-isolated-fixture", StringComparison.Ordinal);

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

internal static class WorkerIsolation
{
    private const int TokenIsAppContainer = 29;

    public static bool IsCurrentProcessAppContainer()
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0008, out var token))
        {
            return false;
        }

        try
        {
            var value = 0;
            return GetTokenInformation(token, TokenIsAppContainer, ref value, sizeof(int), out _) && value != 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int tokenInformationClass, ref int tokenInformation, int tokenInformationLength, out int returnLength);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
