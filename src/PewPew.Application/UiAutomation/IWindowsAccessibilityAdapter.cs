namespace PewPew.Application.UiAutomation;

/// <summary>
/// Abstraction for non-admin Windows UI Accessibility Automation actions.
/// </summary>
public interface IWindowsAccessibilityAdapter
{
    /// <summary>
    /// Executes a UI automation action (Click, SetText, Focus, ReadText) against an allowlisted UI target
    /// and returns post-action UI readback verification evidence.
    /// </summary>
    Task<UiAutomationResult> ExecuteUiActionAsync(
        UiTargetScope target,
        UiActionKind actionKind,
        string? valuePayload,
        CancellationToken cancellationToken = default);
}
