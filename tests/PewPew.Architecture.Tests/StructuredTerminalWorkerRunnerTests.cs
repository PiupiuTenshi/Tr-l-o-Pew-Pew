using PewPew.Application.Actions;
using PewPew.Application.Terminal;
using PewPew.Domain.Terminal;
using PewPew.Domain.Workers;
using PewPew.Infrastructure.Terminal;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class StructuredTerminalWorkerRunnerTests
{
    [Fact]
    public async Task LocalAdapterFailsClosedWhenNetworkIsolationIsUnavailable()
    {
        var processId = 0;
        var adapter = new LocalTerminalProcessRunner();

        var exception = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => adapter.RunAsync(
            new TerminalProcessLaunchRequest(
                "dotnet",
                ["--version"],
                Directory.GetCurrentDirectory(),
                WorkerResourceQuota.Default),
            startedProcessId => processId = startedProcessId,
            TestContext.Current.CancellationToken));

        Assert.Equal("terminal_network_isolation_unavailable", exception.ReasonCode);
        Assert.Equal(0, processId);
    }

    [Fact]
    public async Task LocalAdapterRejectsTraversalAndEnvironmentInjectionBeforeProcessStart()
    {
        var adapter = new LocalTerminalProcessRunner();
        var traversal = Path.Combine(Directory.GetCurrentDirectory(), "..");
        var environment = new Dictionary<string, string> { ["SECRET_TOKEN"] = "must_not_cross_boundary" };

        var traversalException = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => adapter.RunAsync(
            new TerminalProcessLaunchRequest("dotnet", ["--version"], traversal, WorkerResourceQuota.Default),
            _ => throw new Xunit.Sdk.XunitException("process must not start"),
            TestContext.Current.CancellationToken));

        var environmentException = await Assert.ThrowsAsync<TerminalProcessBoundaryViolationException>(() => adapter.RunAsync(
            new TerminalProcessLaunchRequest("dotnet", ["--version"], Directory.GetCurrentDirectory(), WorkerResourceQuota.Default, environment),
            _ => throw new Xunit.Sdk.XunitException("process must not start"),
            TestContext.Current.CancellationToken));

        Assert.Equal("terminal_working_directory_traversal_denied", traversalException.ReasonCode);
        Assert.Equal("terminal_environment_not_allowed", environmentException.ReasonCode);
        Assert.DoesNotContain("must_not_cross_boundary", environmentException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunnerRefusesAnAdapterThatCannotEnforceNetworkPolicy()
    {
        var workflow = ActiveWorkflow("dotnet_build", ["build"]);
        var worker = RunningWorker();
        var runner = new UnisolatedRunner();

        var outcome = await StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow, null, workflow.ExpectedSha256Hash, worker, runner, TestContext.Current.CancellationToken);

        Assert.Equal(WorkerExecutionStatus.Failed, outcome.Status);
        Assert.Equal("terminal_network_isolation_unavailable", outcome.FailureReason);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task RunnerRejectsAnIsolatedHostWithoutEveryRequiredControl()
    {
        var workflow = ActiveWorkflow("dotnet_build", ["build"]);
        var worker = RunningWorker();
        var runner = new IncompleteIsolatedHost();

        var outcome = await StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow, null, workflow.ExpectedSha256Hash, worker, runner, TestContext.Current.CancellationToken);

        Assert.Equal(WorkerExecutionStatus.Failed, outcome.Status);
        Assert.Equal("terminal_network_isolation_unavailable", outcome.FailureReason);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task BuildWorkflowBindsRealPidAndReturnsMetadataOnlySuccessEvidence()
    {
        var executableHash = new string('b', 64);
        var workflow = ActiveWorkflow("dotnet_build", ["build"], executableHash);
        var worker = RunningWorker();
        var runner = new StubProcessRunner(new TerminalProcessRunResult(0, 42, false, 30, TimeSpan.FromMilliseconds(12)));

        var outcome = await StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow,
            null,
            workflow.ExpectedSha256Hash,
            worker,
            runner,
            TestContext.Current.CancellationToken);

        Assert.Equal(WorkerExecutionStatus.Completed, outcome.Status);
        Assert.Equal(7357, worker.RootProcessId);
        Assert.Contains("terminal_exit_code_0", outcome.VerificationEvidence);
        Assert.DoesNotContain("build output", outcome.VerificationEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("dotnet", runner.Request!.ExecutablePath);
        Assert.Equal(["build"], runner.Request.Arguments);
        Assert.Equal(executableHash, runner.Request.ExpectedExecutableSha256Hash);
    }

    [Fact]
    public async Task NonzeroExitFailsWithoutReportingRawOutput()
    {
        var workflow = ActiveWorkflow("dotnet_test", ["test"]);
        var worker = RunningWorker();
        var runner = new StubProcessRunner(new TerminalProcessRunResult(1, 81, false, 20, TimeSpan.FromSeconds(1)));

        var outcome = await StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow, null, workflow.ExpectedSha256Hash, worker, runner, TestContext.Current.CancellationToken);

        Assert.Equal(WorkerExecutionStatus.Failed, outcome.Status);
        Assert.Equal("terminal_exit_code_1", outcome.FailureReason);
    }

    [Fact]
    public async Task OutputQuotaBreachIsUnknownAndNeverEligibleForAutomaticRetry()
    {
        var workflow = ActiveWorkflow("dotnet_start", ["run"]);
        var worker = RunningWorker(new WorkerResourceQuota(maxOutputSizeBytes: 16));
        var runner = new StubProcessRunner(new TerminalProcessRunResult(0, 17, true, 20, TimeSpan.FromSeconds(1)));

        var outcome = await StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow, null, workflow.ExpectedSha256Hash, worker, runner, TestContext.Current.CancellationToken);

        Assert.Equal(WorkerExecutionStatus.Unknown, outcome.Status);
        Assert.Equal("terminal_output_or_resource_quota_exceeded", outcome.FailureReason);
        Assert.Equal(WorkerProcessStatus.Failed, worker.Status);
    }

    [Fact]
    public async Task CancellationPropagatesToProcessRunnerAndProducesCancelledOutcome()
    {
        var workflow = ActiveWorkflow("dotnet_stop", ["test"]);
        var worker = RunningWorker();
        var runner = new StubProcessRunner(exception: new OperationCanceledException());

        var outcome = await StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow, null, workflow.ExpectedSha256Hash, worker, runner, TestContext.Current.CancellationToken);

        Assert.Equal(WorkerExecutionStatus.Cancelled, outcome.Status);
        Assert.Equal("terminal_execution_cancelled", outcome.FailureReason);
        Assert.False(runner.ReceivedCancellation);
    }

    [Fact]
    public async Task MismatchedWorkflowHashStopsBeforeAnyProcessStarts()
    {
        var workflow = ActiveWorkflow("dotnet_build", ["build"]);
        var worker = RunningWorker();
        var runner = new StubProcessRunner(new TerminalProcessRunResult(0, 0, false, 1, TimeSpan.Zero));

        await Assert.ThrowsAsync<InvalidOperationException>(() => StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow, null, new string('0', 64), worker, runner, TestContext.Current.CancellationToken));

        Assert.Null(runner.Request);
        Assert.Equal(TerminalWorkflowStatus.PolicyRejected, workflow.Status);
    }

    [Fact]
    public async Task ProcessStartFailureReturnsRedactedReasonCode()
    {
        var workflow = ActiveWorkflow("dotnet_build", ["build"]);
        var worker = RunningWorker();
        var runner = new StubProcessRunner(
            exception: new System.ComponentModel.Win32Exception("sensitive machine path"),
            startProcess: false);

        var outcome = await StructuredTerminalWorkerRunner.ExecuteAsync(
            workflow, null, workflow.ExpectedSha256Hash, worker, runner, TestContext.Current.CancellationToken);

        Assert.Equal(WorkerExecutionStatus.Failed, outcome.Status);
        Assert.Equal("terminal_process_start_failed", outcome.FailureReason);
        Assert.DoesNotContain("sensitive", outcome.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    private static TerminalWorkflowDefinition ActiveWorkflow(
        string name,
        IReadOnlyList<string> arguments,
        string? expectedExecutableSha256Hash = null)
    {
        var now = DateTimeOffset.UtcNow;
        var workflow = TerminalWorkflowService.CreateDraft(
            name,
            "1.0.0",
            "dotnet",
            "E:\\Project\\PewPew",
            arguments,
            Array.Empty<string>(),
            TerminalWorkflowRiskLevel.Medium,
            expectedExecutableSha256Hash: expectedExecutableSha256Hash,
            nowUtc: now);
        workflow.Submit(now.AddSeconds(1));
        Assert.True(workflow.Approve(workflow.ExpectedSha256Hash, now.AddSeconds(2)));
        workflow.Activate(now.AddSeconds(3));
        return workflow;
    }

    private static WorkerProcess RunningWorker(WorkerResourceQuota? quota = null)
    {
        var worker = new WorkerProcess(
            EntityId.New(),
            EntityId.New(),
            EntityId.New(),
            DateTimeOffset.UtcNow.AddMinutes(1),
            quota);
        worker.Start();
        worker.MarkRunning();
        return worker;
    }

    private sealed class StubProcessRunner : IIsolatedTerminalWorkerHost
    {
        private readonly TerminalProcessRunResult? _result;
        private readonly Exception? _exception;

        private readonly bool _startProcess;

        public StubProcessRunner(TerminalProcessRunResult? result = null, Exception? exception = null, bool startProcess = true)
        {
            _result = result;
            _exception = exception;
            _startProcess = startProcess;
        }

        public TerminalProcessLaunchRequest? Request { get; private set; }

        public bool ProvidesNetworkIsolation => true;

        public IsolatedTerminalWorkerReadiness GetReadiness() =>
            new(true, "ready", true, true, true);

        public bool ReceivedCancellation { get; private set; }

        public Task<TerminalProcessRunResult> RunAsync(
            TerminalProcessLaunchRequest request,
            Action<int> onProcessStarted,
            CancellationToken cancellationToken)
        {
            Request = request;
            ReceivedCancellation = cancellationToken.IsCancellationRequested;
            cancellationToken.ThrowIfCancellationRequested();
            if (_startProcess)
            {
                onProcessStarted(7357);
            }

            return _exception is not null
                ? Task.FromException<TerminalProcessRunResult>(_exception)
                : Task.FromResult(_result!);
        }
    }

    private sealed class UnisolatedRunner : ITerminalProcessRunner
    {
        public bool ProvidesNetworkIsolation => false;

        public bool WasCalled { get; private set; }

        public Task<TerminalProcessRunResult> RunAsync(
            TerminalProcessLaunchRequest request,
            Action<int> onProcessStarted,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new Xunit.Sdk.XunitException("unisolated process runner must not execute");
        }
    }

    private sealed class IncompleteIsolatedHost : IIsolatedTerminalWorkerHost
    {
        public bool ProvidesNetworkIsolation => true;

        public bool WasCalled { get; private set; }

        public IsolatedTerminalWorkerReadiness GetReadiness() =>
            new(true, "authenticated_local_control_missing", true, true, false);

        public Task<TerminalProcessRunResult> RunAsync(
            TerminalProcessLaunchRequest request,
            Action<int> onProcessStarted,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new Xunit.Sdk.XunitException("incomplete isolated host must not execute");
        }
    }
}
