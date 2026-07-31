namespace PewPew.Application.UiAutomation;

/// <summary>
/// Supported UI Automation action kinds.
/// </summary>
public enum UiActionKind
{
    /// <summary>
    /// Invokes or clicks a UI control (e.g. Button, MenuItem, TabItem).
    /// </summary>
    Click = 1,

    /// <summary>
    /// Sets text content on an editable UI control (e.g. Edit, Document).
    /// </summary>
    SetText = 2,

    /// <summary>
    /// Sets keyboard focus to a UI control.
    /// </summary>
    Focus = 3,

    /// <summary>
    /// Reads text value or label from a UI control for verification.
    /// </summary>
    ReadText = 4
}
