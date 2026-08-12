using PewPew.Application.Terminal;
using PewPew.Desktop;
using PewPew.Domain.Workers;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class WindowsAppContainerTerminalWorkerHostTests
{
    [Fact]
    public async Task UnprovisionedHostFailsClosedBeforeAnyProcessCanStart()
    {
        var host = new WindowsAppContainerTerminalWorkerHost(new Provisioner(
            new IsolatedTerminalWorkerReadiness(false, "appcontainer_profile_missing", false, false, false)));
        var processStarted = false;

        var exception = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => host.RunAsync(
            new TerminalProcessLaunchRequest("dotnet", ["--version"], Directory.GetCurrentDirectory(), WorkerResourceQuota.Default),
            _ => processStarted = true,
            TestContext.Current.CancellationToken));

        Assert.False(host.ProvidesNetworkIsolation);
        Assert.False(processStarted);
        Assert.Equal("isolated_terminal_worker_unavailable", exception.ReasonCode);
    }

    [Fact]
    public async Task PartiallyProvisionedHostStillFailsClosedWithoutFallback()
    {
        var host = new WindowsAppContainerTerminalWorkerHost(new Provisioner(
            new IsolatedTerminalWorkerReadiness(true, "ready", true, true, false)));

        var exception = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => host.RunAsync(
            new TerminalProcessLaunchRequest("dotnet", ["--version"], Directory.GetCurrentDirectory(), WorkerResourceQuota.Default),
            _ => throw new Xunit.Sdk.XunitException("process must not start"),
            TestContext.Current.CancellationToken));

        Assert.False(host.ProvidesNetworkIsolation);
        Assert.Equal("isolated_terminal_worker_unavailable", exception.ReasonCode);
    }

    [Fact]
    public void BindingRejectsMissingFieldsAndUsesNewNonceForEachInvocation()
    {
        Assert.Throws<ArgumentException>(() => IsolatedTerminalWorkerBinding.Create("", "workflow", "1.0", "hash"));

        var first = IsolatedTerminalWorkerBinding.Create("corr-1", "workflow", "1.0", "hash");
        var second = IsolatedTerminalWorkerBinding.Create("corr-1", "workflow", "1.0", "hash");

        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.Equal("corr-1", first.CorrelationId);
    }

    private sealed class Provisioner(IsolatedTerminalWorkerReadiness readiness) : IWindowsAppContainerTerminalWorkerProvisioner
    {
        public IsolatedTerminalWorkerReadiness GetReadiness() => readiness;
    }
}
