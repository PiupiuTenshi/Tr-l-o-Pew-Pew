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
    string? ElementName = null,
    int? ProcessId = null)
{
    public UiTargetScope(string processName) : this(processName, null, null, null, null, null) { }

    public string TargetId => ProcessId is { } processId
        ? $"{ProcessName}:{AutomationId ?? ElementName ?? "window"}:pid-{processId.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : $"{ProcessName}:{AutomationId ?? ElementName ?? "window"}";
}
