using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace PewPew.Desktop;

public sealed class DesktopApp : Application
{
    public override void Initialize()
    {
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "Pew Pew Assistant",
                Width = 960,
                Height = 640
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
