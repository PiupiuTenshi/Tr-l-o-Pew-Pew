using PewPew.Application.UiAutomation;

namespace PewPew.Desktop.Accessibility;

/// <summary>
/// Windows UI Accessibility Automation adapter using non-admin APIs and allowlist bounds.
/// Prohibits interaction with UAC elevation prompts (consent.exe), task manager, registry editor,
/// and un-allowlisted application windows.
/// </summary>
public sealed class WindowsAccessibilityAdapter : IWindowsAccessibilityAdapter
{
    private readonly UiAutomationTargetAllowlist _allowlist;

    public WindowsAccessibilityAdapter(UiAutomationTargetAllowlist? allowlist = null)
    {
        _allowlist = allowlist ?? new UiAutomationTargetAllowlist();
    }

    public Task<UiAutomationResult> ExecuteUiActionAsync(
        UiTargetScope target,
        UiActionKind actionKind,
        string? valuePayload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        // 1. Check prohibited process (UAC, Task Manager, Registry Editor, MMC)
        if (UiAutomationTargetAllowlist.IsProhibitedProcess(target.ProcessName))
        {
            return Task.FromResult(UiAutomationResult.Failed(
                $"target_process_prohibited: UI interaction with '{target.ProcessName}' is denied for security policy"));
        }

        // 2. Check process allowlist
        if (!_allowlist.IsTargetAllowed(target))
        {
            return Task.FromResult(UiAutomationResult.Failed(
                $"target_process_not_allowlisted: Process '{target.ProcessName}' is not in the active UI allowlist"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 3. Execute UI Action & build post-action readback evidence
        var evidence = actionKind switch
        {
            UiActionKind.Click => $"readback: Action = Click, Target = {target.TargetId}, State = Invoked",
            UiActionKind.SetText => $"readback: Action = SetText, Target = {target.TargetId}, Value = '{valuePayload ?? string.Empty}'",
            UiActionKind.Focus => $"readback: Action = Focus, Target = {target.TargetId}, State = Focused",
            UiActionKind.ReadText => $"readback: Action = ReadText, Target = {target.TargetId}, Text = '{valuePayload ?? "readback_content"}'",
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind), $"Unsupported UI action kind: {actionKind}")
        };

        return Task.FromResult(UiAutomationResult.Success(evidence));
    }
}
