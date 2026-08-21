using PewPew.Application.Terminal;
using PewPew.Desktop;
using PewPew.Domain.Workers;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class AuthenticatedIsolatedTerminalWorkerBrokerTests
{
    [Fact]
    public async Task UnauthenticatedWireIsDeniedBeforeWorkerCanReceiveInvocation()
    {
        var wire = new WireClient(authenticated: false);
        var broker = new AuthenticatedIsolatedTerminalWorkerBroker(wire);

        var error = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() =>
            broker.ExecuteAsync(CreateInvocation(), _ => throw new Xunit.Sdk.XunitException("must not start"), TestContext.Current.CancellationToken));

        Assert.Equal("isolated_terminal_worker_ipc_unauthenticated", error.ReasonCode);
        Assert.Equal(0, wire.Calls);
    }

    [Fact]
    public async Task ReplayIsDeniedBeforeSecondWorkerDelivery()
    {
        var wire = new WireClient(authenticated: true);
        var broker = new AuthenticatedIsolatedTerminalWorkerBroker(wire);
        var invocation = CreateInvocation();

        await broker.ExecuteAsync(invocation, _ => { }, TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() =>
            broker.ExecuteAsync(invocation, _ => { }, TestContext.Current.CancellationToken));

        Assert.Equal("isolated_terminal_worker_replay_denied", error.ReasonCode);
        Assert.Equal(1, wire.Calls);
    }

    [Fact]
    public async Task MismatchedRequestBindingIsDeniedBeforeDelivery()
    {
        var wire = new WireClient(authenticated: true);
        var broker = new AuthenticatedIsolatedTerminalWorkerBroker(wire);
        var binding = IsolatedTerminalWorkerBinding.Create("worker", "workflow", "1.0", "hash");
        var request = new TerminalProcessLaunchRequest("dotnet", ["--version"], Directory.GetCurrentDirectory(), WorkerResourceQuota.Default,
            Binding: IsolatedTerminalWorkerBinding.Create("worker", "workflow", "1.0", "hash"));

        var error = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() =>
            broker.ExecuteAsync(new IsolatedTerminalWorkerInvocation(binding, request), _ => { }, TestContext.Current.CancellationToken));

        Assert.Equal("isolated_terminal_worker_binding_invalid", error.ReasonCode);
        Assert.Equal(0, wire.Calls);
    }

    [Fact]
    public async Task UnexpectedWireFailureIsRedactedToMetadataOnlyReason()
    {
        var wire = new WireClient(authenticated: true, exception: new InvalidOperationException("C:\\secret\\command"));
        var broker = new AuthenticatedIsolatedTerminalWorkerBroker(wire);

        var error = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() =>
            broker.ExecuteAsync(CreateInvocation(), _ => { }, TestContext.Current.CancellationToken));

        Assert.Equal("isolated_terminal_worker_ipc_unavailable", error.ReasonCode);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IsolatedTerminalWorkerInvocation CreateInvocation()
    {
        var binding = IsolatedTerminalWorkerBinding.Create("worker", "workflow", "1.0", "hash");
        var request = new TerminalProcessLaunchRequest("dotnet", ["--version"], Directory.GetCurrentDirectory(), WorkerResourceQuota.Default,
            Binding: binding);
        return new(binding, request);
    }

    private sealed class WireClient(bool authenticated, Exception? exception = null) : IIsolatedTerminalWorkerWireClient
    {
        public int Calls { get; private set; }

        public bool IsAuthenticated => authenticated;

        public bool ProvidesRestrictedJobObject => authenticated;

        public Task<TerminalProcessRunResult> SendAsync(
            IsolatedTerminalWorkerInvocation invocation,
            Action<int> onProcessStarted,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (exception is not null)
            {
                return Task.FromException<TerminalProcessRunResult>(exception);
            }

            return Task.FromResult(new TerminalProcessRunResult(0, 0, false, 1, TimeSpan.Zero));
        }
    }
}
