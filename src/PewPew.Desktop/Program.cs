using Avalonia;
using PewPew.Application.Voice;
using PewPew.Infrastructure.Voice;
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
        if (args.Length > 0 && args[0].StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
        {
            new NativeMessagingHost(NativeMessagingPipeName.ForCurrentUser())
                .RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            return;
        }

        var configuration = StartupConfiguration.LoadFromEnvironment();
        DesktopApp.ConfigureSpeechOutput(new WindowsSpeechOutput());
        DesktopApp.ConfigureSpeechTranscriber(
            new WhisperLocalSpeechTranscriber(LocalSpeechModelOptions.LoadFromEnvironment(configuration)));
        DesktopApp.ConfigureVoiceProfileEnrollment(
            new VoiceWakeProfileEnrollmentService(
                new EncryptedLocalVoiceProfileSampleVault(),
                "Pew Pew"));
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DesktopApp>().UsePlatformDetect();
}
