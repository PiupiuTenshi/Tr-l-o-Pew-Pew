using PewPew.Application.Terminal;
using PewPew.Desktop;
using PewPew.Domain.Workers;
using System.Security.Cryptography;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class AppContainerDirectTerminalWorkloadLauncherTests
{
    [Fact]
    public async Task MissingExecutableHashFailsBeforeAnyAppContainerProcessStarts()
    {
        using var root = new TemporaryDirectory();
        var launcher = new AppContainerDirectTerminalWorkloadLauncher(root.Path);
        var started = false;

        var exception = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => launcher.LaunchAsync(
            Request(Path.Combine(root.Path, "workload.exe"), root.Path, expectedHash: null),
            _ => started = true,
            TestContext.Current.CancellationToken));

        Assert.Equal("terminal_workload_hash_missing", exception.ReasonCode);
        Assert.False(started);
    }

    [Fact]
    public async Task MismatchedExecutableHashFailsBeforeAnyAppContainerProcessStarts()
    {
        using var root = new TemporaryDirectory();
        var executable = Path.Combine(root.Path, "workload.exe");
        await File.WriteAllBytesAsync(executable, [1, 2, 3], TestContext.Current.CancellationToken);
        var launcher = new AppContainerDirectTerminalWorkloadLauncher(root.Path);
        var started = false;

        var exception = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => launcher.LaunchAsync(
            Request(executable, root.Path, new string('0', 64)),
            _ => started = true,
            TestContext.Current.CancellationToken));

        Assert.Equal("terminal_workload_hash_mismatch", exception.ReasonCode);
        Assert.False(started);
    }

    [Fact]
    public async Task ExecutableOutsideApprovedRootFailsBeforeAnyAppContainerProcessStarts()
    {
        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var launcher = new AppContainerDirectTerminalWorkloadLauncher(root.Path);
        var started = false;

        var exception = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => launcher.LaunchAsync(
            Request(Path.Combine(outside.Path, "workload.exe"), root.Path, new string('0', 64)),
            _ => started = true,
            TestContext.Current.CancellationToken));

        Assert.Equal("appcontainer_workload_scope_denied", exception.ReasonCode);
        Assert.False(started);
    }

    [Fact]
    public async Task DirectAppContainerManualEvidenceRunsOnlyTheHashPinnedFixture()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("PEWPEW_RUN_DIRECT_APPCONTAINER_WORKLOAD_TEST"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var workloadRoot = Environment.GetEnvironmentVariable("PEWPEW_DIRECT_APPCONTAINER_WORKLOAD_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(workloadRoot));
        workloadRoot = Path.GetFullPath(workloadRoot);
        var executable = Path.Combine(workloadRoot, "PewPew.IsolatedTerminalWorker.exe");
        Assert.True(File.Exists(executable));
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(executable, TestContext.Current.CancellationToken)));
        var launcher = new AppContainerDirectTerminalWorkloadLauncher(workloadRoot);
        var processId = 0;

        var result = await launcher.LaunchAsync(
            Request(executable, workloadRoot, expectedHash) with { Arguments = ["--pewpew-direct-fixture"] },
            startedProcessId => processId = startedProcessId,
            TestContext.Current.CancellationToken);

        Assert.True(processId > 0);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, result.OutputBytes);
    }

    [Fact]
    public async Task DirectAppContainerManualEvidenceCancellationTerminatesTheJobOwnedFixture()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("PEWPEW_RUN_DIRECT_APPCONTAINER_WORKLOAD_TEST"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var workloadRoot = Environment.GetEnvironmentVariable("PEWPEW_DIRECT_APPCONTAINER_WORKLOAD_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(workloadRoot));
        workloadRoot = Path.GetFullPath(workloadRoot);
        var executable = Path.Combine(workloadRoot, "PewPew.IsolatedTerminalWorker.exe");
        Assert.True(File.Exists(executable));
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(executable, TestContext.Current.CancellationToken)));
        var launcher = new AppContainerDirectTerminalWorkloadLauncher(workloadRoot);
        using var cancellation = new CancellationTokenSource();
        var processId = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launcher.LaunchAsync(
            Request(executable, workloadRoot, expectedHash) with { Arguments = ["--pewpew-direct-cancellation-fixture"] },
            startedProcessId =>
            {
                processId = startedProcessId;
                cancellation.Cancel();
            },
            cancellation.Token));

        Assert.True(processId > 0);
    }

    private static TerminalProcessLaunchRequest Request(string executable, string workingDirectory, string? expectedHash) =>
        new(
            executable,
            Array.Empty<string>(),
            workingDirectory,
            WorkerResourceQuota.Default,
            Binding: IsolatedTerminalWorkerBinding.Create("test", "workflow", "1.0", new string('a', 64)),
            ExpectedExecutableSha256Hash: expectedHash);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pewpew-t27-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
