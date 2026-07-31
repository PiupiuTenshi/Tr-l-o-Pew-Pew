using Avalonia;
using PewPew.SharedKernel.Configuration;

namespace PewPew.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => DesktopHost.Start(args);
}

public static class DesktopHost
{
    [STAThread]
    public static void Start(string[] args)
    {
        var configuration = StartupConfiguration.LoadFromEnvironment();
        DesktopApp.ConfigureSpeechOutput(new WindowsSpeechOutput());
        DesktopApp.ConfigureSpeechTranscriber(
            new WhisperLocalSpeechTranscriber(LocalSpeechModelOptions.LoadFromEnvironment(configuration)));
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DesktopApp>().UsePlatformDetect();
}
