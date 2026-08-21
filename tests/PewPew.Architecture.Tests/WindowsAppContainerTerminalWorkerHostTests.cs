using PewPew.Application.Terminal;
using PewPew.Desktop;
using PewPew.Domain.Workers;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class WindowsAppContainerTerminalWorkerHostTests
{
    [Fact]
    public void DefaultWorkerDirectoryIsUnderLocalAppData()
    {
        var localAppData = Path.GetFullPath(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var workerDirectory = WindowsAppContainerTerminalWorkerProvisioner.DefaultWorkerDirectory;

        Assert.StartsWith(localAppData, workerDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("PewPew", "TerminalWorker"), workerDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProvisioningManualEvidenceCreatesOnlyTheApprovedProfileAndDirectory()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("PEWPEW_RUN_APPCONTAINER_PROVISIONING_TEST"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var provisioner = new WindowsAppContainerTerminalWorkerProvisioner();
        var result = provisioner.Provision();

        Assert.Equal(WindowsAppContainerTerminalWorkerProvisioner.ProfileName, result.ProfileName);
        Assert.Equal(WindowsAppContainerTerminalWorkerProvisioner.DefaultWorkerDirectory, result.WorkerDirectory);
        Assert.True(Directory.Exists(result.WorkerDirectory));
        Assert.True(result.Readiness.HasNetworkDeniedAppContainer);
        Assert.False(result.Readiness.IsReady);
        Assert.Equal("isolated_terminal_workload_launcher_unavailable", result.Readiness.ReasonCode);
    }

    [Fact]
    public void RestrictedJobManualEvidenceIsOptInAndClosesDeterministically()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("PEWPEW_RUN_RESTRICTED_JOB_TEST"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var job = WindowsRestrictedJobObject.Create();
        Assert.True(job.IsActive);
    }

    [Fact]
    public async Task AppContainerWorkerManualEvidenceRunsOnlyTheHarmlessWorkerFixture()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("PEWPEW_RUN_APPCONTAINER_WORKER_TEST"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var workerDirectory = Environment.GetEnvironmentVariable("PEWPEW_APPCONTAINER_WORKER_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(workerDirectory));
        workerDirectory = Path.GetFullPath(workerDirectory);
        var workerExecutable = Path.Combine(workerDirectory, "PewPew.IsolatedTerminalWorker.exe");
        Assert.True(File.Exists(workerExecutable));

        var binding = IsolatedTerminalWorkerBinding.Create("manual-worker", "manual-fixture", "1.0", "fixture-hash");
        var invocation = new IsolatedTerminalWorkerInvocation(
            binding,
            new TerminalProcessLaunchRequest(workerExecutable, ["--pewpew-isolated-fixture"], workerDirectory, WorkerResourceQuota.Default, Binding: binding));
        var client = new AppContainerNamedPipeWireClient(workerExecutable);
        var processId = 0;

        var result = await client.SendAsync(invocation, processIdValue => processId = processIdValue, TestContext.Current.CancellationToken);

        Assert.True(processId > 0);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.OutputLimitExceeded);
    }

    [Fact]
    public async Task AppContainerWorkerManualEvidenceRunsOnlyTheAppContainerChildFixture()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("PEWPEW_RUN_APPCONTAINER_WORKER_TEST"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var workerDirectory = Environment.GetEnvironmentVariable("PEWPEW_APPCONTAINER_WORKER_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(workerDirectory));
        workerDirectory = Path.GetFullPath(workerDirectory);
        var workerExecutable = Path.Combine(workerDirectory, "PewPew.IsolatedTerminalWorker.exe");
        Assert.True(File.Exists(workerExecutable));

        var binding = IsolatedTerminalWorkerBinding.Create("manual-child", "manual-fixture", "1.0", "fixture-hash");
        var invocation = new IsolatedTerminalWorkerInvocation(
            binding,
            new TerminalProcessLaunchRequest(workerExecutable, ["--pewpew-child-fixture"], workerDirectory, WorkerResourceQuota.Default, Binding: binding));
        var client = new AppContainerNamedPipeWireClient(workerExecutable);
        var processId = 0;

        var result = await client.SendAsync(invocation, processIdValue => processId = processIdValue, TestContext.Current.CancellationToken);

        Assert.True(processId > 0);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.OutputLimitExceeded);
    }

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
