using PewPew.Application.UiAutomation;
using PewPew.Desktop.Accessibility;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit and security tests for <see cref="WindowsAccessibilityAdapter"/> and <see cref="UiAutomationTargetAllowlist"/>.
/// Verifies that the legacy adapter path is disabled and the allowlist remains fail-closed.
/// </summary>
public sealed class WindowsAccessibilityAdapterTests
{
    [Fact]
    public async Task LegacyControlPathDoesNotFabricateAllowlistedActionSuccess()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("notepad", WindowTitlePattern: "Untitled*", AutomationId: "btnSave");

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", result.FailureReason);
        Assert.Empty(result.ActionEvidence);
    }

    [Fact]
    public async Task LegacyControlPathDoesNotExposeTypedPayload()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("notepad", WindowTitlePattern: "*", AutomationId: "txtBody");

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.SetText, valuePayload: "Sample text input", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", result.FailureReason);
        Assert.DoesNotContain("Sample text input", result.ActionEvidence);
    }

    [Fact]
    public async Task LegacyControlPathDoesNotPretendFocusOrReadSucceeded()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("explorer", ElementName: "FolderList");

        var focusResult = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Focus, valuePayload: null, TestContext.Current.CancellationToken);
        Assert.False(focusResult.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", focusResult.FailureReason);

        var readResult = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.ReadText, valuePayload: "Documents", TestContext.Current.CancellationToken);
        Assert.False(readResult.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", readResult.FailureReason);
    }

    [Theory]
    [InlineData("consent")]
    [InlineData("consent.exe")]
    public async Task ExecuteUiActionAsyncRejectsUacConsentDialog(string uacProcess)
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope(uacProcess, AutomationId: "btnYes");

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", result.FailureReason);
        Assert.Empty(result.ActionEvidence);
    }

    [Theory]
    [InlineData("regedit")]
    [InlineData("taskmgr")]
    [InlineData("cmd")]
    [InlineData("powershell")]
    public async Task ExecuteUiActionAsyncRejectsRegistryEditorAndSystemTools(string prohibitedProcess)
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope(prohibitedProcess);

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", result.FailureReason);
    }

    [Fact]
    public async Task ExecuteUiActionAsyncRejectsUnallowlistedProcess()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("untrusted_random_app");

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", result.FailureReason);
    }

    [Fact]
    public async Task AllowProcessRegistersNewNonProhibitedApp()
    {
        var allowlist = new UiAutomationTargetAllowlist();
        var adapter = new WindowsAccessibilityAdapter(allowlist);
        var target = new UiTargetScope("mycustomapp");

        // Allow process
        var registered = allowlist.AllowProcess("mycustomapp");
        Assert.True(registered);

        // The old adapter remains disabled even after allowlist registration.
        var afterResult = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);
        Assert.False(afterResult.IsSuccess);
        Assert.Equal("uia_legacy_control_path_disabled", afterResult.FailureReason);

        // Attempting to allow UAC dialog 'consent.exe' must be rejected
        var allowedUac = allowlist.AllowProcess("consent.exe");
        Assert.False(allowedUac);
    }
}
