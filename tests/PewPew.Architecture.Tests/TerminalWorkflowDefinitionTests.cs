using PewPew.Application.Terminal;
using PewPew.Domain.Terminal;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit and security tests for <see cref="TerminalWorkflowDefinition"/> domain lifecycle,
/// executable allowlist validation, SHA-256 hash pinning/approval, argument sanitization,
/// shell injection defense, and parameter binding.
/// </summary>
public sealed class TerminalWorkflowDefinitionTests
{
    private static readonly string ValidExe = "dotnet";
    private static readonly string ValidWorkDir = "E:\\Project\\PewPew";
    private static readonly string[] SampleBuildArgs = ["build", "{project_name}"];
    private static readonly string[] SampleProjectPlaceholders = ["project_name"];
    private static readonly string[] SamplePathPlaceholders = ["project_path"];
    private static readonly string[] SampleStatusArgs = ["status"];
    private static readonly string[] SampleTestArgs = ["test"];
    private static readonly string[] SampleRunArgs = ["run"];

    [Fact]
    public void CreateDraftWorkflowSucceedsAndComputesValidHash()
    {
        var workflow = TerminalWorkflowService.CreateDraft(
            "dotnet_build",
            "1.0.0",
            ValidExe,
            ValidWorkDir,
            SampleBuildArgs,
            SampleProjectPlaceholders,
            TerminalWorkflowRiskLevel.Medium);

        Assert.Equal("dotnet_build", workflow.Name);
        Assert.Equal("1.0.0", workflow.Version);
        Assert.Equal(ValidExe, workflow.ExecutablePath);
        Assert.Equal(TerminalWorkflowStatus.Draft, workflow.Status);
        Assert.Equal(TerminalWorkflowRiskLevel.Medium, workflow.RiskLevel);
        Assert.Equal(64, workflow.ExpectedSha256Hash.Length);
    }

    [Fact]
    public void CreateDraftRejectsMalformedPinnedExecutableHash()
    {
        var exception = Assert.Throws<ArgumentException>(() => TerminalWorkflowService.CreateDraft(
            "dotnet_build",
            "1.0.0",
            ValidExe,
            ValidWorkDir,
            SampleBuildArgs,
            SampleProjectPlaceholders,
            TerminalWorkflowRiskLevel.Medium,
            expectedExecutableSha256Hash: "not-a-sha256"));

        Assert.Contains("Executable SHA-256 hash", exception.Message);
    }

    [Fact]
    public void LifecycleTransitionsSubmitApproveActivateSucceed()
    {
        var now = DateTimeOffset.UtcNow;
        var workflow = TerminalWorkflowService.CreateDraft(
            "git_status",
            "1.0.0",
            "git",
            ValidWorkDir,
            SampleStatusArgs,
            Array.Empty<string>(),
            TerminalWorkflowRiskLevel.Low,
            nowUtc: now);

        // 1. Submit
        workflow.Submit(now.AddSeconds(1));
        Assert.Equal(TerminalWorkflowStatus.Submitted, workflow.Status);

        // 2. Approve with matching hash
        var approved = workflow.Approve(workflow.ExpectedSha256Hash, now.AddSeconds(2));
        Assert.True(approved);
        Assert.Equal(TerminalWorkflowStatus.PolicyApproved, workflow.Status);

        // 3. Activate
        workflow.Activate(now.AddSeconds(3));
        Assert.Equal(TerminalWorkflowStatus.Active, workflow.Status);
    }

    [Fact]
    public void ApproveWithIncorrectHashRejectsWorkflow()
    {
        var now = DateTimeOffset.UtcNow;
        var workflow = TerminalWorkflowService.CreateDraft(
            "git_status",
            "1.0.0",
            "git",
            ValidWorkDir,
            SampleStatusArgs,
            Array.Empty<string>(),
            TerminalWorkflowRiskLevel.Low);

        workflow.Submit(now);

        var badHash = "0000000000000000000000000000000000000000000000000000000000000000";
        var approved = workflow.Approve(badHash, now.AddSeconds(1));

        Assert.False(approved);
        Assert.Equal(TerminalWorkflowStatus.PolicyRejected, workflow.Status);
        Assert.NotNull(workflow.RejectionReason);
        Assert.Contains("Hash mismatch", workflow.RejectionReason);
    }

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("powershell.exe")]
    [InlineData("bash")]
    [InlineData("sh")]
    [InlineData("regedit.exe")]
    [InlineData("vssadmin.exe")]
    [InlineData("format.com")]
    public void ForbiddenExecutableRejectsProhibitedShellInterpreters(string forbiddenExe)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TerminalWorkflowService.CreateDraft(
                "malicious_workflow",
                "1.0.0",
                forbiddenExe,
                ValidWorkDir,
                SampleRunArgs,
                Array.Empty<string>(),
                TerminalWorkflowRiskLevel.High));

        Assert.Contains("prohibited shell interpreter", ex.Message);
    }

    [Fact]
    public void UnallowedExecutableRejectsArbitraryBinaries()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TerminalWorkflowService.CreateDraft(
                "unknown_tool_workflow",
                "1.0.0",
                "arbitrary_binary.exe",
                ValidWorkDir,
                SampleRunArgs,
                Array.Empty<string>(),
                TerminalWorkflowRiskLevel.Medium));

        Assert.Contains("approved terminal executable allowlist", ex.Message);
    }

    [Theory]
    [InlineData("build; rm -rf /")]
    [InlineData("build | grep secret")]
    [InlineData("build && echo pwnd")]
    [InlineData("build $(whoami)")]
    public void FixedArgumentsWithShellOperatorsThrowsArgumentException(string maliciousArg)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TerminalWorkflowService.CreateDraft(
                "injection_workflow",
                "1.0.0",
                ValidExe,
                ValidWorkDir,
                new[] { maliciousArg },
                Array.Empty<string>(),
                TerminalWorkflowRiskLevel.Medium));

        Assert.Contains("prohibited shell operator", ex.Message);
    }

    [Fact]
    public void SanitizeAndBindArgumentsRejectsUndeclaredPlaceholders()
    {
        var workflow = TerminalWorkflowService.CreateDraft(
            "dotnet_test",
            "1.0.0",
            ValidExe,
            ValidWorkDir,
            SampleTestArgs,
            Array.Empty<string>(), // No placeholders allowed
            TerminalWorkflowRiskLevel.Medium);

        var badValues = new Dictionary<string, string> { { "undeclared_param", "value" } };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TerminalWorkflowValidator.SanitizeAndBindArguments(workflow, badValues));

        Assert.Contains("not declared in workflow", ex.Message);
    }

    private static readonly string[] SampleTestProjectArgs = ["test", "{project_name}"];
    private static readonly string[] SampleTestPathArgs = ["test", "{project_path}"];

    [Theory]
    [InlineData("PewPew.csproj; calc.exe")]
    [InlineData("PewPew.csproj | Type secret.txt")]
    [InlineData("PewPew.csproj && echo injected")]
    public void SanitizeAndBindArgumentsRejectsShellInjectionInParameterValues(string maliciousParamValue)
    {
        var workflow = TerminalWorkflowService.CreateDraft(
            "dotnet_test",
            "1.0.0",
            ValidExe,
            ValidWorkDir,
            SampleTestProjectArgs,
            SampleProjectPlaceholders,
            TerminalWorkflowRiskLevel.Medium);

        var paramValues = new Dictionary<string, string> { { "project_name", maliciousParamValue } };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TerminalWorkflowValidator.SanitizeAndBindArguments(workflow, paramValues));

        Assert.Contains("contains illegal shell operators", ex.Message);
    }

    [Theory]
    [InlineData("../../../secret.txt")]
    [InlineData("..\\..\\Windows\\System32")]
    public void SanitizeAndBindArgumentsRejectsPathTraversalInParameterValues(string traversalPath)
    {
        var workflow = TerminalWorkflowService.CreateDraft(
            "dotnet_test",
            "1.0.0",
            ValidExe,
            ValidWorkDir,
            SampleTestPathArgs,
            SamplePathPlaceholders,
            TerminalWorkflowRiskLevel.Medium);

        var paramValues = new Dictionary<string, string> { { "project_path", traversalPath } };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TerminalWorkflowValidator.SanitizeAndBindArguments(workflow, paramValues));

        Assert.Contains("contains illegal shell operators or path traversal", ex.Message);
    }

    [Fact]
    public void PrepareExecutionFailsForUnapprovedOrRejectedWorkflows()
    {
        var now = DateTimeOffset.UtcNow;
        var workflow = TerminalWorkflowService.CreateDraft(
            "dotnet_build",
            "1.0.0",
            ValidExe,
            ValidWorkDir,
            SampleTestArgs,
            Array.Empty<string>(),
            TerminalWorkflowRiskLevel.Medium);

        // Draft state execution attempt
        var exDraft = Assert.Throws<InvalidOperationException>(() =>
            TerminalWorkflowService.PrepareExecution(workflow, null, workflow.ExpectedSha256Hash, now));

        Assert.Contains("cannot be executed from status 'Draft'", exDraft.Message);

        // Rejected state execution attempt
        workflow.Submit(now);
        workflow.Reject("Security violation", now);

        var exRejected = Assert.Throws<InvalidOperationException>(() =>
            TerminalWorkflowService.PrepareExecution(workflow, null, workflow.ExpectedSha256Hash, now));

        Assert.Contains("cannot be executed from status 'PolicyRejected'", exRejected.Message);
    }

    [Fact]
    public void CriticalRiskLevelWorkflowRejectsCreation()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            TerminalWorkflowService.CreateDraft(
                "dangerous_workflow",
                "1.0.0",
                ValidExe,
                ValidWorkDir,
                SampleTestArgs,
                Array.Empty<string>(),
                TerminalWorkflowRiskLevel.Critical));

        Assert.Contains("Critical risk rating are strictly prohibited", ex.Message);
    }
}
