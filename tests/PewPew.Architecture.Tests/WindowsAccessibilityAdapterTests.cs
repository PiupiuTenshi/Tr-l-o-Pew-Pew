using PewPew.Application.UiAutomation;
using PewPew.Desktop.Accessibility;
using Xunit;

namespace PewPew.Architecture.Tests;

/// <summary>
/// Unit and security tests for <see cref="WindowsAccessibilityAdapter"/> and <see cref="UiAutomationTargetAllowlist"/>.
/// Verifies process allowlisting, post-action readback evidence, and strict rejection of UAC consent dialogs.
/// </summary>
public sealed class WindowsAccessibilityAdapterTests
{
    [Fact]
    public async Task ExecuteUiActionAsyncClickOnAllowlistedProcessSucceedsWithReadback()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("notepad", WindowTitlePattern: "Untitled*", AutomationId: "btnSave");

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains("Action = Click", result.ActionEvidence);
        Assert.Contains("notepad:btnSave", result.ActionEvidence);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task ExecuteUiActionAsyncSetTextSucceedsWithReadback()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("notepad", WindowTitlePattern: "*", AutomationId: "txtBody");

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.SetText, valuePayload: "Sample text input", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains("Action = SetText", result.ActionEvidence);
        Assert.Contains("Sample text input", result.ActionEvidence);
    }

    [Fact]
    public async Task ExecuteUiActionAsyncFocusAndReadTextSucceedWithReadback()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("explorer", ElementName: "FolderList");

        var focusResult = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Focus, valuePayload: null, TestContext.Current.CancellationToken);
        Assert.True(focusResult.IsSuccess);
        Assert.Contains("Focused", focusResult.ActionEvidence);

        var readResult = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.ReadText, valuePayload: "Documents", TestContext.Current.CancellationToken);
        Assert.True(readResult.IsSuccess);
        Assert.Contains("Documents", readResult.ActionEvidence);
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
        Assert.Contains("target_process_prohibited", result.FailureReason);
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
        Assert.Contains("target_process_prohibited", result.FailureReason);
    }

    [Fact]
    public async Task ExecuteUiActionAsyncRejectsUnallowlistedProcess()
    {
        var adapter = new WindowsAccessibilityAdapter();
        var target = new UiTargetScope("untrusted_random_app");

        var result = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("target_process_not_allowlisted", result.FailureReason);
    }

    [Fact]
    public async Task AllowProcessRegistersNewNonProhibitedApp()
    {
        var allowlist = new UiAutomationTargetAllowlist();
        var adapter = new WindowsAccessibilityAdapter(allowlist);
        var target = new UiTargetScope("mycustomapp");

        // Before allowlist registration
        var beforeResult = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);
        Assert.False(beforeResult.IsSuccess);

        // Allow process
        var registered = allowlist.AllowProcess("mycustomapp");
        Assert.True(registered);

        // After allowlist registration
        var afterResult = await adapter.ExecuteUiActionAsync(
            target, UiActionKind.Click, valuePayload: null, TestContext.Current.CancellationToken);
        Assert.True(afterResult.IsSuccess);

        // Attempting to allow UAC dialog 'consent.exe' must be rejected
        var allowedUac = allowlist.AllowProcess("consent.exe");
        Assert.False(allowedUac);
    }
}
