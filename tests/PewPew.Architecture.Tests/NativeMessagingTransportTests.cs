using System.Text.Json;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using PewPew.Application.BrowserExtension;
using PewPew.Desktop;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class NativeMessagingTransportTests
{
    private const string Origin = "https://trusted.example";

    [Fact]
    public void ProtocolRejectsUnknownKindAndUnauthenticatedHeartbeat()
    {
        Assert.False(NativeMessagingTransportProtocol.TryValidate(
            new NativeMessagingTransportRequest(1, "execute_javascript", "c"), out var unknownReason));
        Assert.Equal("transport_message_kind_denied", unknownReason);

        Assert.False(NativeMessagingTransportProtocol.TryValidate(
            new NativeMessagingTransportRequest(1, "heartbeat", "c"), out var bindingReason));
        Assert.Equal("transport_authenticated_binding_required", bindingReason);
    }

    [Fact]
    public void ProtocolRequiresReadbackBindingAndRejectsArbitraryBrowserCommands()
    {
        var now = DateTimeOffset.UtcNow;
        var request = BoundRequest("action_readback", "token", "nonce", now, Origin);
        Assert.False(NativeMessagingTransportProtocol.TryValidate(request, out var missingReason));
        Assert.Equal("transport_readback_binding_required", missingReason);

        var command = new NativeMessagingBrowserCommand(
            "command-1", "token", Origin, "tab-1", "snapshot-1", 1, 0,
            "execute_javascript", "AABB", "authorization-1");
        Assert.False(NativeMessagingTransportProtocol.TryValidateBrowserCommand(command, out var actionReason));
        Assert.Equal("browser_command_binding_invalid", actionReason);
    }

    [Fact]
    public void ProtocolAcceptsOnlyFullyBoundTypedMediaCommand()
    {
        var command = new NativeMessagingBrowserCommand(
            "command-1", "token", Origin, "tab-1", "snapshot-1", 1, 0,
            "pause", new string('A', 64), "authorization-1");

        Assert.True(NativeMessagingTransportProtocol.TryValidateBrowserCommand(command, out var reason));
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public async Task BrokerDeliversOnlyMatchingBoundCommandAndAcceptsMatchingReadback()
    {
        var broker = new NativeMessagingBrowserCommandBroker();
        var command = new NativeMessagingBrowserCommand(
            "command-1", "token", Origin, "tab-1", "snapshot-1", 1, 0,
            "pause", new string('A', 64), "authorization-1");
        Assert.True(broker.TryQueue(command, out var queueReason));
        Assert.Equal(string.Empty, queueReason);

        var poll = BoundRequest("command_poll", "token", "nonce-poll", DateTimeOffset.UtcNow, Origin) with { SnapshotId = null };
        var delivery = broker.Poll(poll);
        Assert.Same(command, delivery.BrowserCommand);

        var readback = BoundRequest("action_readback", "token", "nonce-readback", DateTimeOffset.UtcNow, Origin) with
        {
            CommandId = command.CommandId,
            SnapshotVersion = command.SnapshotVersion,
            NavigationGeneration = command.NavigationGeneration,
            PayloadHash = command.PayloadHash,
            ReadbackStatus = "observed"
        };
        Assert.True(broker.CompleteReadback(readback).IsSuccess);
        Assert.True((await broker.WaitForReadbackAsync(command.CommandId, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public void BrokerRejectsReadbackWithChangedPayloadBinding()
    {
        var broker = new NativeMessagingBrowserCommandBroker();
        var command = new NativeMessagingBrowserCommand(
            "command-1", "token", Origin, "tab-1", "snapshot-1", 1, 0,
            "pause", new string('A', 64), "authorization-1");
        Assert.True(broker.TryQueue(command, out _));

        var readback = BoundRequest("action_readback", "token", "nonce", DateTimeOffset.UtcNow, Origin) with
        {
            CommandId = command.CommandId,
            SnapshotVersion = command.SnapshotVersion,
            NavigationGeneration = command.NavigationGeneration,
            PayloadHash = new string('B', 64),
            ReadbackStatus = "observed"
        };
        Assert.Equal("browser_readback_binding_denied", broker.CompleteReadback(readback).ReasonCode);
    }

    [Fact]
    public void ServerIssuesEphemeralTokenThenRejectsReplayAndOriginEscape()
    {
        var now = DateTimeOffset.UtcNow;
        var server = CreateServer();
        var connection = server.Process(new NativeMessagingTransportRequest(1, "connect", "connect-1"), now);
        Assert.True(connection.IsSuccess);
        Assert.NotNull(connection.SessionToken);

        var valid = BoundRequest("heartbeat", connection.SessionToken!, "nonce-1", now, Origin);
        Assert.True(server.Process(valid, now).IsSuccess);
        Assert.StartsWith("replay_attack_detected", server.Process(valid with { CorrelationId = "retry" }, now).ReasonCode);

        var escapedOrigin = BoundRequest("heartbeat", connection.SessionToken!, "nonce-2", now, "https://evil.example");
        Assert.Equal("origin_policy_denied", server.Process(escapedOrigin, now).ReasonCode);
    }

    [Fact]
    public void ServerDeniesConnectionWhenNoExplicitOriginAllowlistExists()
    {
        var server = new NativeMessagingPipeServer(
            new DesktopExtensionBridge(),
            new ExtensionOriginPolicyValidator(),
            $"pewpew-test-{Guid.NewGuid():N}");

        var response = server.Process(new NativeMessagingTransportRequest(1, "connect", "no-origins"), DateTimeOffset.UtcNow);
        Assert.Equal("transport_origin_allowlist_required", response.ReasonCode);
    }

    [Fact]
    public void CancelInvalidatesSessionAndStopsFutureTransportMessages()
    {
        var now = DateTimeOffset.UtcNow;
        var server = CreateServer();
        var connection = server.Process(new NativeMessagingTransportRequest(1, "connect", "connect-2"), now);
        var cancel = BoundRequest("cancel", connection.SessionToken!, "nonce-cancel", now, Origin);

        Assert.True(server.Process(cancel, now).IsSuccess);
        Assert.Equal("unauthorized_caller_token", server.Process(
            BoundRequest("heartbeat", connection.SessionToken!, "nonce-after", now, Origin), now).ReasonCode);
    }

    [Fact]
    public void PipeSecurityAllowsOnlyTheCurrentUserReadWrite()
    {
        var security = NativeMessagingPipeServer.CreateCurrentUserPipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent().User;
        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .OfType<PipeAccessRule>()
            .ToArray();

        var rule = Assert.Single(rules);
        Assert.Equal(currentUser, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.True((rule.PipeAccessRights & PipeAccessRights.ReadWrite) == PipeAccessRights.ReadWrite);
    }

    [Fact]
    public async Task CurrentUserPipeRelaysConnectAndAuthenticatedHeartbeat()
    {
        await using var server = CreateServer();
        server.Start();
        var now = DateTimeOffset.UtcNow;

        var connection = await SendOverPipeAsync(server.PipeName,
            new NativeMessagingTransportRequest(1, "connect", "pipe-connect"));
        Assert.True(connection.IsSuccess);
        Assert.NotNull(connection.SessionToken);

        var heartbeat = await SendOverPipeAsync(server.PipeName,
            BoundRequest("heartbeat", connection.SessionToken!, "pipe-nonce", now, Origin));
        Assert.True(heartbeat.IsSuccess);
    }

    [Fact]
    public async Task FrameCodecRoundTripsBoundedMetadataOnlyMessage()
    {
        await using var stream = new MemoryStream();
        var request = BoundRequest("heartbeat", "token", "nonce", DateTimeOffset.UtcNow, Origin);
        await NativeMessagingFrameCodec.WriteAsync(stream, request, CancellationToken.None);
        stream.Position = 0;

        var result = await NativeMessagingFrameCodec.ReadAsync<NativeMessagingTransportRequest>(stream, CancellationToken.None);
        Assert.Equal(request, result);
    }

    [Fact]
    public async Task FrameCodecRejectsOversizedFrameBeforeDeserialization()
    {
        using var stream = new MemoryStream(BitConverter.GetBytes(NativeMessagingTransportProtocol.MaximumFrameBytes + 1));
        await Assert.ThrowsAsync<JsonException>(async () =>
            await NativeMessagingFrameCodec.ReadAsync<NativeMessagingTransportRequest>(stream, CancellationToken.None));
    }

    [Fact]
    public void ManifestGeneratorUsesExplicitChromeExtensionOriginAndNoRegistryWrite()
    {
        var origin = "chrome-extension://abcdefghijklmnopabcdefghijklmnop/";
        var json = NativeMessagingHostManifestGenerator.Generate("C:\\Program Files\\PewPew\\PewPew.Desktop.exe", [origin]);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(NativeMessagingHostManifestGenerator.HostName, document.RootElement.GetProperty("name").GetString());
        Assert.Equal("stdio", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(origin, document.RootElement.GetProperty("allowed_origins")[0].GetString());
        Assert.Throws<ArgumentException>(() => NativeMessagingHostManifestGenerator.Generate("host.exe", ["https://*/*"]));
    }

    private static NativeMessagingPipeServer CreateServer() =>
        new(
            new DesktopExtensionBridge(new ExtensionOriginPolicyValidator([Origin])),
            new ExtensionOriginPolicyValidator([Origin]),
            $"pewpew-test-{Guid.NewGuid():N}");

    private static NativeMessagingTransportRequest BoundRequest(
        string kind,
        string token,
        string nonce,
        DateTimeOffset now,
        string origin) =>
        new(1, kind, Guid.NewGuid().ToString("N"), token, nonce, now, origin, "tab-1", "snapshot-1");

    private static async Task<NativeMessagingTransportResponse> SendOverPipeAsync(
        string pipeName,
        NativeMessagingTransportRequest request)
    {
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await NativeMessagingFrameCodec.WriteAsync(client, request, CancellationToken.None);
        return await NativeMessagingFrameCodec.ReadAsync<NativeMessagingTransportResponse>(client, CancellationToken.None)
            ?? throw new InvalidOperationException("pipe_response_missing");
    }
}
