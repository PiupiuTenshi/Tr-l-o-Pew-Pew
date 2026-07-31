using System.Reflection;
using PewPew.Application.Actions;
using PewPew.Application.FolderSkills;
using PewPew.Application.IntentRouting;
using PewPew.Desktop;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Audit;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class ReadOnlyFolderSkillServiceTests
{
    [Fact]
    public void FindCommandAcceptsBoundedQueryButOpenRejectsTraversalOrAbsolutePath()
    {
        var root = AllowedFolderRoot.Create("documents", Path.GetTempPath());
        var proposal = new LocalIntentProposal(
            1,
            LocalIntentName.FindFile,
            new Dictionary<string, string> { ["query"] = "budget-2026.xlsx" });

        Assert.True(ReadOnlyFolderCommand.TryCreateFind(proposal, root, out var command));
        Assert.Equal(ReadOnlyFolderOperation.Find, command!.Operation);
        Assert.Throws<ArgumentException>(() => ReadOnlyFolderCommand.CreateOpen(root, "..\\outside.txt"));
        Assert.Throws<ArgumentException>(() => ReadOnlyFolderCommand.CreateOpen(root, Path.GetTempFileName()));
    }

    [Fact]
    public void FolderAdapterLeaseCannotBeConstructedByPresentationCode()
    {
        Assert.Empty(typeof(AuthorizedReadOnlyFolderCommand).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void ReparsePointAttributeIsRejectedByTheSharedFolderGuard()
    {
        Assert.True(ReadOnlyFolderPathGuards.IsReparsePoint(FileAttributes.Directory | FileAttributes.ReparsePoint));
        Assert.False(ReadOnlyFolderPathGuards.IsReparsePoint(FileAttributes.Normal));
    }

    [Fact]
    public async Task WindowsAdapterFindsOnlyRelativeEntriesUnderConfiguredRoot()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"pewpew-folder-skill-{Guid.NewGuid():N}");
        var nestedDirectory = Path.Combine(rootPath, "reports");
        Directory.CreateDirectory(nestedDirectory);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(nestedDirectory, "budget-2026.xlsx"),
                "test",
                TestContext.Current.CancellationToken);
            var scenario = NewScenario(permissionActive: true, rootPath);
            var result = await new ReadOnlyFolderSkillService(new WindowsReadOnlyFolderSkillAdapter()).ExecuteAsync(
                scenario.Request,
                scenario.Command,
                TestContext.Current.CancellationToken);

            var entry = Assert.Single(result.Entries);
            Assert.Equal(ActionTaskStatus.Completed, result.TaskStatus);
            Assert.Equal(Path.Combine("reports", "budget-2026.xlsx"), entry.RelativePath);
            Assert.False(Path.IsPathFullyQualified(entry.RelativePath));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task MissingReadPermissionDeniesBeforeFolderAdapterAndSealsAudit()
    {
        var scenario = NewScenario(permissionActive: false);
        var adapter = new RecordingAdapter();
        var service = new ReadOnlyFolderSkillService(adapter);

        var result = await service.ExecuteAsync(scenario.Request, scenario.Command, CancellationToken.None);

        Assert.Equal("permission_denied", result.ReasonCode);
        Assert.False(adapter.WasCalled);
        Assert.Equal(ActionTaskStatus.Queued, scenario.Task.Status);
        Assert.Equal(AuditRecordStatus.Sealed, result.Dispatch!.AuditRecord.Status);
    }

    [Fact]
    public async Task ApprovedReadCommandRecordsOnlyVerifiedEntries()
    {
        var scenario = NewScenario(permissionActive: true);
        var adapter = new RecordingAdapter(ReadOnlyFolderAdapterResult.Verified(
            new[] { new ReadOnlyFolderEntry("reports\\budget-2026.xlsx") },
            "folder_find_verified"));
        var service = new ReadOnlyFolderSkillService(adapter);

        var result = await service.ExecuteAsync(scenario.Request, scenario.Command, CancellationToken.None);

        Assert.True(adapter.WasCalled);
        Assert.Equal(ActionTaskStatus.Completed, scenario.Task.Status);
        Assert.Equal("folder_find_verified", scenario.Task.VerificationEvidence);
        Assert.Single(result.Entries);
        Assert.True(result.Dispatch!.IsAllowed);
        Assert.Equal(AuditRecordStatus.Sealed, result.Dispatch.AuditRecord.Status);
    }

    [Fact]
    public async Task CancellationAfterApprovalDoesNotCallAdapter()
    {
        var scenario = NewScenario(permissionActive: true);
        var adapter = new RecordingAdapter();
        var service = new ReadOnlyFolderSkillService(adapter);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await service.ExecuteAsync(scenario.Request, scenario.Command, cancellation.Token);

        Assert.False(adapter.WasCalled);
        Assert.Equal(ActionTaskStatus.Cancelled, scenario.Task.Status);
        Assert.Equal("cancelled", result.ReasonCode);
        Assert.True(result.Dispatch!.IsAllowed);
    }

    private static Scenario NewScenario(bool permissionActive, string? rootPath = null)
    {
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        var root = AllowedFolderRoot.Create("documents", rootPath ?? Path.GetTempPath());
        var proposal = new LocalIntentProposal(
            1,
            LocalIntentName.FindFile,
            new Dictionary<string, string> { ["query"] = "budget" });
        Assert.True(ReadOnlyFolderCommand.TryCreateFind(proposal, root, out var command));
        var scope = PermissionScope.Create(userId, deviceId, ReadOnlyFolderCommand.SkillName, root.Id, "read", false);
        var permission = new PermissionGrant(EntityId.New(), scope, DateTimeOffset.UtcNow.AddMinutes(1));
        permission.Submit();
        if (permissionActive)
        {
            permission.Approve();
        }

        var profile = new AssistantProfile(EntityId.New(), userId);
        profile.CompleteProvisioning();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, ReadOnlyFolderCommand.SkillName, root.Id, "");
        var plan = new ActionPlan(definition, DateTimeOffset.UtcNow.AddMinutes(1));
        plan.SubmitForPolicyReview();
        plan.ApproveByPolicy();
        var task = new ActionTask(EntityId.New(), definition.Id, userId, DateTimeOffset.UtcNow.AddMinutes(1), true);

        return new Scenario(
            command!,
            task,
            new ActionDispatchRequest(
                profile, plan, permission, scope, null, task, userId, sessionId, deviceId,
                DateTimeOffset.UtcNow, "folder-skill-correlation"));
    }

    private sealed record Scenario(ReadOnlyFolderCommand Command, ActionTask Task, ActionDispatchRequest Request);

    private sealed class RecordingAdapter(ReadOnlyFolderAdapterResult? result = null) : IReadOnlyFolderSkillAdapter
    {
        public bool WasCalled { get; private set; }

        public Task<ReadOnlyFolderAdapterResult> ExecuteAsync(
            AuthorizedReadOnlyFolderCommand command,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(result ?? ReadOnlyFolderAdapterResult.Verified(Array.Empty<ReadOnlyFolderEntry>(), "folder_find_verified"));
        }
    }
}
