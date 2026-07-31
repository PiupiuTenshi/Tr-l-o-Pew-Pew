namespace PewPew.Application.UiAutomation;

/// <summary>
/// Target specification for a UI Automation interaction.
/// Identifies the application process, window title pattern, and target element.
/// </summary>
public sealed record UiTargetScope(
    string ProcessName,
    string? WindowTitlePattern = null,
    string? AutomationId = null,
    string? ControlTypeName = null,
    string? ElementName = null)
{
    public UiTargetScope(string processName) : this(processName, null, null, null, null) { }

    public string TargetId => $"{ProcessName}:{AutomationId ?? ElementName ?? "window"}";
}
