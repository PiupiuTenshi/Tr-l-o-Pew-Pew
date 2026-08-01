using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using PewPew.Application.BrowserExtension;
using PewPew.Application.Automation;

namespace PewPew.Desktop;

public static class NativeMessagingPipeName
{
    public static string ForCurrentUser()
    {
        var user = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("current_user_sid_unavailable");
        var digest = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user)));
        return $"pewpew-native-{digest[..24]}";
    }
}

public sealed class NativeMessagingPipeServer : IAsyncDisposable
{
    private readonly DesktopExtensionBridge _bridge;
    private readonly ExtensionOriginPolicyValidator _originPolicy;
    private readonly NativeMessagingBrowserCommandBroker _commandBroker;
    private readonly BrowserActiveTabContextStore _contexts;
    private readonly CancellationTokenSource _stopSource = new();
    private Task? _serveTask;

    public NativeMessagingPipeServer(
        DesktopExtensionBridge bridge,
        ExtensionOriginPolicyValidator originPolicy,
        string? pipeName = null,
        NativeMessagingBrowserCommandBroker? commandBroker = null,
        BrowserActiveTabContextStore? contexts = null)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _originPolicy = originPolicy ?? throw new ArgumentNullException(nameof(originPolicy));
        _commandBroker = commandBroker ?? new NativeMessagingBrowserCommandBroker();
        _contexts = contexts ?? new BrowserActiveTabContextStore();
        PipeName = pipeName ?? NativeMessagingPipeName.ForCurrentUser();
    }

    public string PipeName { get; }

    public void Start()
    {
        _serveTask ??= Task.Run(() => ServeAsync(_stopSource.Token));
    }

    public NativeMessagingTransportResponse Process(NativeMessagingTransportRequest request, DateTimeOffset nowUtc)
    {
        if (!NativeMessagingTransportProtocol.TryValidate(request, out var failure))
        {
            return NativeMessagingTransportResponse.Rejected(failure);
        }

        if (request.Kind == "connect")
        {
            if (!_originPolicy.HasExplicitAllowedOrigins)
            {
                return NativeMessagingTransportResponse.Rejected("transport_origin_allowlist_required");
            }
            return NativeMessagingTransportResponse.Accepted(_bridge.IssueSessionToken(nowUtc));
        }

        var bridgeResponse = _bridge.ProcessRequest(
            new ExtensionBridgeRequest(
                request.SessionToken!,
                request.Nonce!,
                request.TimestampUtc!.Value,
                request.TargetOrigin!,
                "native_transport"),
            nowUtc);
        if (!bridgeResponse.IsSuccess)
        {
            return NativeMessagingTransportResponse.Rejected(bridgeResponse.ReasonCode);
        }

        if (request.Kind == "command_poll")
        {
            _contexts.RecordAuthenticatedPoll(request, nowUtc);
            return _commandBroker.Poll(request);
        }

        if (request.Kind == "action_readback")
        {
            return _commandBroker.CompleteReadback(request);
        }

        if (request.Kind == "cancel")
        {
            _bridge.Disconnect();
            return NativeMessagingTransportResponse.Accepted();
        }

        return NativeMessagingTransportResponse.Accepted();
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var stream = CreateCurrentUserPipe();
            try
            {
                await stream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var request = await NativeMessagingFrameCodec.ReadAsync<NativeMessagingTransportRequest>(stream, cancellationToken).ConfigureAwait(false);
                var response = request is null
                    ? NativeMessagingTransportResponse.Rejected("transport_frame_invalid")
                    : Process(request, DateTimeOffset.UtcNow);
                await NativeMessagingFrameCodec.WriteAsync(stream, response, cancellationToken).ConfigureAwait(false);
                if (request?.Kind == "cancel" && response.IsSuccess)
                {
                    _stopSource.Cancel();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                // Connection-specific failure: fail closed and accept a fresh pipe connection.
            }
            catch (JsonException)
            {
                // Malformed input never reaches the bridge.
            }
        }
    }

    private NamedPipeServerStream CreateCurrentUserPipe()
    {
        var security = CreateCurrentUserPipeSecurity();

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            NativeMessagingTransportProtocol.MaximumFrameBytes,
            NativeMessagingTransportProtocol.MaximumFrameBytes,
            security,
            HandleInheritability.None,
            (PipeAccessRights)0);
    }

    public static PipeSecurity CreateCurrentUserPipeSecurity()
    {
        var sid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("current_user_sid_unavailable");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return security;
    }

    public async ValueTask DisposeAsync()
    {
        _stopSource.Cancel();
        _bridge.Disconnect();
        if (_serveTask is not null)
        {
            try { await _serveTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _stopSource.Dispose();
    }
}

public sealed class NativeMessagingHost
{
    private readonly string _pipeName;

    public NativeMessagingHost(string pipeName) => _pipeName = pipeName;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        while (!cancellationToken.IsCancellationRequested)
        {
            var request = await NativeMessagingFrameCodec.ReadAsync<NativeMessagingTransportRequest>(input, cancellationToken).ConfigureAwait(false);
            if (request is null)
            {
                return;
            }

            var response = await RelayAsync(request, cancellationToken).ConfigureAwait(false);
            await NativeMessagingFrameCodec.WriteAsync(output, response, cancellationToken).ConfigureAwait(false);
            if (request.Kind == "cancel" && response.IsSuccess)
            {
                return;
            }
        }
    }

    private async Task<NativeMessagingTransportResponse> RelayAsync(NativeMessagingTransportRequest request, CancellationToken cancellationToken)
    {
        if (!NativeMessagingTransportProtocol.TryValidate(request, out var failure))
        {
            return NativeMessagingTransportResponse.Rejected(failure);
        }

        await using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            await NativeMessagingFrameCodec.WriteAsync(pipe, request, cancellationToken).ConfigureAwait(false);
            return await NativeMessagingFrameCodec.ReadAsync<NativeMessagingTransportResponse>(pipe, cancellationToken).ConfigureAwait(false)
                ?? NativeMessagingTransportResponse.Rejected("desktop_transport_response_missing");
        }
        catch (TimeoutException)
        {
            return NativeMessagingTransportResponse.Rejected("desktop_transport_unavailable");
        }
        catch (IOException)
        {
            return NativeMessagingTransportResponse.Rejected("desktop_transport_unavailable");
        }
    }
}

public static class NativeMessagingFrameCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[sizeof(int)];
        if (!await TryReadExactlyAsync(stream, lengthBytes, cancellationToken).ConfigureAwait(false))
        {
            return default;
        }

        var length = BitConverter.ToInt32(lengthBytes, 0);
        if (length <= 0 || length > NativeMessagingTransportProtocol.MaximumFrameBytes)
        {
            throw new JsonException("transport_frame_length_invalid");
        }

        var payload = new byte[length];
        if (!await TryReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false))
        {
            throw new IOException("transport_frame_truncated");
        }

        return JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if (payload.Length == 0 || payload.Length > NativeMessagingTransportProtocol.MaximumFrameBytes)
        {
            throw new InvalidOperationException("transport_frame_length_invalid");
        }

        await stream.WriteAsync(BitConverter.GetBytes(payload.Length), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TryReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return false;
            }
            read += count;
        }
        return true;
    }
}

public static class NativeMessagingHostManifestGenerator
{
    public const string HostName = "com.pewpew.assistant.bridge";
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new() { WriteIndented = true };

    public static string Generate(string hostExecutablePath, IEnumerable<string> extensionOrigins)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostExecutablePath);
        ArgumentNullException.ThrowIfNull(extensionOrigins);
        var origins = extensionOrigins.Distinct(StringComparer.Ordinal).ToArray();
        if (origins.Length == 0 || origins.Any(origin => !IsChromeExtensionOrigin(origin)))
        {
            throw new ArgumentException("native_host_origins_invalid", nameof(extensionOrigins));
        }

        var manifest = new
        {
            name = HostName,
            description = "Pew Pew Assistant Native Messaging transport host",
            path = Path.GetFullPath(hostExecutablePath),
            type = "stdio",
            allowed_origins = origins
        };
        return JsonSerializer.Serialize(manifest, ManifestSerializerOptions);
    }

    private static bool IsChromeExtensionOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        uri.Scheme.Equals("chrome-extension", StringComparison.OrdinalIgnoreCase) &&
        uri.Host.Length == 32 && uri.Host.All(character => character is >= 'a' and <= 'p');
}
