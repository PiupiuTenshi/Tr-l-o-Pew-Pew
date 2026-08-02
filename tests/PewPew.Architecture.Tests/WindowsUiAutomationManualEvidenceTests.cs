using System.Diagnostics;
using PewPew.Application.Actions;
using PewPew.Application.UiAutomation;
using PewPew.Desktop.Accessibility;
using PewPew.Domain.Actions;
using PewPew.Domain.Assistant;
using PewPew.Domain.Permissions;
using PewPew.SharedKernel.Primitives;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Opt-in, non-admin manual evidence. Run only with
/// PEWPEW_RUN_MANUAL_UIA_TEST=1 after explicit user approval.
/// It focuses the single Notepad process explicitly selected by the operator,
/// reads focus back, and neither writes content nor closes any application.
/// </summary>
public sealed class WindowsUiAutomationManualEvidenceTests
{
    [Fact]
    [Trait("Category", "Manual")]
    public async Task FocusesEmptyNotepadThroughBoundControlPathAndReadsBackFocus()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PEWPEW_RUN_MANUAL_UIA_TEST"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var processIdText = Environment.GetEnvironmentVariable("PEWPEW_UIA_TEST_PROCESS_ID");
        Assert.True(int.TryParse(processIdText, out var processId) && processId > 0,
            "Set PEWPEW_UIA_TEST_PROCESS_ID to the PID of one manually opened empty Notepad window.");

        using var process = Process.GetProcessById(processId);
        Assert.Equal("notepad", process.ProcessName, ignoreCase: true);

        var target = new UiTargetScope("notepad", WindowTitlePattern: "*", ProcessId: process.Id);
        var request = CreateAuthorizedRequest(target);
        var adapter = new WindowsAccessibilityAdapter();

        var result = await WindowsUiAutomationControlPath.ExecuteAsync(request, adapter, null, TestContext.Current.CancellationToken);

        Assert.Equal(WindowsUiAutomationOutcome.Verified, result.Outcome);
        Assert.Equal("verified", result.ReasonCode);
    }

    private static WindowsUiAutomationControlRequest CreateAuthorizedRequest(UiTargetScope target)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = EntityId.New();
        var sessionId = EntityId.New();
        var deviceId = EntityId.New();
        var scope = PermissionScope.Create(userId, deviceId, "windows.uia", target.TargetId, UiActionKind.Focus.ToString(), false);
        var grant = new PermissionGrant(EntityId.New(), scope, now.AddMinutes(1));
        grant.Submit();
        grant.Approve();
        var profile = new AssistantProfile(EntityId.New(), userId);
        profile.CompleteProvisioning();
        var definition = StructuredActionPlan.Create(EntityId.New(), 1, "windows.uia", target.TargetId, UiActionKind.Focus.ToString());
        var plan = new ActionPlan(definition, now.AddMinutes(1));
        plan.SubmitForPolicyReview();
        plan.RequireConfirmation();
        var task = new ActionTask(EntityId.New(), definition.Id, userId, now.AddMinutes(1), true);
        const string correlationId = "manual-uia-notepad-focus";
        var confirmation = new ConfirmationRequest(EntityId.New(), userId, sessionId, deviceId, definition.Hash, now.AddMinutes(1));
        var authorization = new ActionDispatchRequest(profile, plan, grant, scope, confirmation, task, userId, sessionId, deviceId, now, correlationId);
        var command = new VerifiedWindowsUiAutomationCommand("manual-notepad-focus", target, UiActionKind.Focus, definition.Hash, null, "focused", correlationId);
        return new(authorization, command);
    }
}
