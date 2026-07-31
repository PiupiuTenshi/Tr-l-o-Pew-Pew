using PewPew.Application.EmergencyStop;
using PewPew.Application.Workers;
using PewPew.Domain.Workers;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit tests for <see cref="WorkerSandboxManager"/>, <see cref="WorkerResourceQuota"/>,
/// and <see cref="WorkerProcess"/> resource monitoring / quota enforcement.
/// </summary>
public sealed class WorkerSandboxManagerTests
{
    private static readonly EntityId OwnerTaskId = EntityId.New();
    private static readonly EntityId DeviceId = EntityId.New();

    [Fact]
    public void CreateWorkerSetsQuotaAndTimeout()
    {
        var quota = new WorkerResourceQuota(maxRamMb: 512, maxExecutionDuration: TimeSpan.FromMinutes(2));
        var now = DateTimeOffset.UtcNow;

        var worker = WorkerSandboxManager.CreateWorker(OwnerTaskId, DeviceId, quota, now);

        Assert.Equal(OwnerTaskId, worker.OwnerTaskId);
        Assert.Equal(DeviceId, worker.DeviceId);
        Assert.Equal(512, worker.Quota.MaxRamMb);
        Assert.Equal(now.AddMinutes(2), worker.TimeoutAtUtc);
        Assert.Equal(WorkerProcessStatus.Created, worker.Status);
    }

    [Fact]
    public void WorkerProcessEnforcesTimeout()
    {
        var quota = new WorkerResourceQuota(maxExecutionDuration: TimeSpan.FromSeconds(5));
        var start = DateTimeOffset.UtcNow;
        var worker = WorkerSandboxManager.CreateWorker(OwnerTaskId, DeviceId, quota, start);

        worker.Start();
        worker.MarkRunning(rootProcessId: 1234);

        // Before timeout
        worker.CheckTimeout(start.AddSeconds(4));
        Assert.Equal(WorkerProcessStatus.Running, worker.Status);

        // After timeout
        worker.CheckTimeout(start.AddSeconds(6));
        Assert.Equal(WorkerProcessStatus.Failed, worker.Status);
        Assert.Equal("execution_timeout", worker.FailureReason);
    }

    [Fact]
    public void WorkerProcessEnforcesRamQuotaBreach()
    {
        var quota = new WorkerResourceQuota(maxRamMb: 100);
        var start = DateTimeOffset.UtcNow;
        var worker = WorkerSandboxManager.CreateWorker(OwnerTaskId, DeviceId, quota, start);

        worker.Start();
        worker.MarkRunning(rootProcessId: 5678);

        // Normal usage
        worker.RecordResourceUsage(currentRamMb: 50, outputSizeBytes: 1000);
        Assert.Equal(WorkerProcessStatus.Running, worker.Status);
        Assert.Equal(50, worker.PeakRamMb);

        // RAM breach
        worker.RecordResourceUsage(currentRamMb: 150, outputSizeBytes: 1000);
        Assert.Equal(WorkerProcessStatus.Failed, worker.Status);
        Assert.Equal(150, worker.PeakRamMb);
        Assert.Contains("RAM quota exceeded", worker.FailureReason);
    }

    [Fact]
    public void WorkerProcessEnforcesOutputSizeQuotaBreach()
    {
        var quota = new WorkerResourceQuota(maxOutputSizeBytes: 1024); // 1 KiB
        var start = DateTimeOffset.UtcNow;
        var worker = WorkerSandboxManager.CreateWorker(OwnerTaskId, DeviceId, quota, start);

        worker.Start();
        worker.MarkRunning(rootProcessId: 9999);

        // Output size breach
        worker.RecordResourceUsage(currentRamMb: 20, outputSizeBytes: 2048);
        Assert.Equal(WorkerProcessStatus.Failed, worker.Status);
        Assert.Contains("Output size quota exceeded", worker.FailureReason);
    }

    [Fact]
    public async Task MonitorWorkerAsyncTriggersStopperOnBreach()
    {
        var quota = new WorkerResourceQuota(maxRamMb: 100);
        var start = DateTimeOffset.UtcNow;
        var worker = WorkerSandboxManager.CreateWorker(OwnerTaskId, DeviceId, quota, start);

        worker.Start();
        worker.MarkRunning(rootProcessId: 4321);

        var stopperSpy = new StopperSpy();
        var metrics = new WorkerResourceMetrics(CurrentRamMb: 200, OutputSizeBytes: 500); // RAM breach

        await WorkerSandboxManager.MonitorWorkerAsync(worker, metrics, stopperSpy, start, TestContext.Current.CancellationToken);

        Assert.Equal(WorkerProcessStatus.Failed, worker.Status);
        Assert.True(stopperSpy.WasStopCalled);
    }

    [Fact]
    public void SanitizeAndTruncateOutputReturnsOriginalWhenWithinQuota()
    {
        var quota = new WorkerResourceQuota(maxOutputSizeBytes: 1024);
        var input = "Short log message";

        var result = WorkerSandboxManager.SanitizeAndTruncateOutput(input, quota);

        Assert.Equal(input, result);
    }

    [Fact]
    public void SanitizeAndTruncateOutputTruncatesAndAppendsNotice()
    {
        var quota = new WorkerResourceQuota(maxOutputSizeBytes: 100);
        var longInput = new string('X', 500);

        var result = WorkerSandboxManager.SanitizeAndTruncateOutput(longInput, quota);

        Assert.Contains("[OUTPUT TRUNCATED - QUOTA EXCEEDED]", result);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(result) <= 100);
    }

    [Theory]
    [InlineData(0, 80, 1000)]
    [InlineData(-10, 80, 1000)]
    [InlineData(256, 0, 1000)]
    [InlineData(256, 101, 1000)]
    [InlineData(256, 80, -5)]
    public void WorkerResourceQuotaValidationRejectsInvalidLimits(int maxRamMb, int maxCpuPercent, long maxOutputBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkerResourceQuota(maxRamMb, maxCpuPercent, TimeSpan.FromSeconds(10), maxOutputBytes));
    }

    private sealed class StopperSpy : ILocalWorkerProcessStopper
    {
        public bool WasStopCalled { get; private set; }

        public Task<LocalWorkerProcessStopResult> StopProcessTreeAsync(WorkerProcess worker, CancellationToken cancellationToken)
        {
            WasStopCalled = true;
            return Task.FromResult(LocalWorkerProcessStopResult.Stopped("stopped_by_spy"));
        }
    }
}
