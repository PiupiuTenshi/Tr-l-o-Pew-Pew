namespace PewPew.Application.Automation;

/// <summary>
/// Types of browser tab navigation and media playback actions.
/// </summary>
public enum BrowserActionKind
{
    SwitchTab = 1,
    SelectVideo = 2,
    Play = 3,
    Pause = 4,
    Seek = 5,
    SetVolume = 6
}
