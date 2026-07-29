using PewPew.SharedKernel.Configuration;

namespace PewPew.Desktop;

public enum DesktopShellStatus
{
    Ready,
    Understanding,
    InputRequired
}

public sealed class DesktopShellState
{
    public DesktopShellState(StartupConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ModeLabel = configuration.PrivateMode ? "Private Mode" : "Local Mode";
        Status = DesktopShellStatus.Ready;
        StatusLabel = "Ready for text input";
        ResponseLabel = "Text input is available. Voice and local actions are not enabled yet.";
    }

    public string ModeLabel { get; }

    public DesktopShellStatus Status { get; private set; }

    public string StatusLabel { get; private set; }

    public string ResponseLabel { get; private set; }

    public void SubmitText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Status = DesktopShellStatus.InputRequired;
            StatusLabel = "Enter text before submitting";
            ResponseLabel = "No action was requested.";
            return;
        }

        Status = DesktopShellStatus.Understanding;
        StatusLabel = "Understanding text input";
        ResponseLabel = "Đã nhận văn bản. Chức năng thực thi cục bộ chưa được bật.";
    }
}
