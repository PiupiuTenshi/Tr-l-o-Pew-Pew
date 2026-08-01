using Avalonia;
using PewPew.Application.Voice;
using PewPew.Application.Automation;
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
        var origins = (Environment.GetEnvironmentVariable("PEWPEW_BROWSER_ALLOWED_ORIGINS") ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var contexts = new BrowserActiveTabContextStore();
        var broker = new NativeMessagingBrowserCommandBroker();
        var transport = new NativeMessagingPipeServer(
            new PewPew.Application.BrowserExtension.DesktopExtensionBridge(
                new PewPew.Application.BrowserExtension.ExtensionOriginPolicyValidator(origins)),
            new PewPew.Application.BrowserExtension.ExtensionOriginPolicyValidator(origins),
            commandBroker: broker,
            contexts: contexts);
        transport.Start();
        DesktopApp.ConfigureBrowserMedia(new BrowserMediaActionCoordinator(contexts), new NativeMessagingVerifiedBrowserActionChannel(broker));
        DesktopApp.ConfigureSpeechOutput(new WindowsSpeechOutput());
        DesktopApp.ConfigureSpeechTranscriber(
            new WhisperLocalSpeechTranscriber(LocalSpeechModelOptions.LoadFromEnvironment(configuration)));
        DesktopApp.ConfigureVoiceProfileEnrollment(
            new VoiceWakeProfileEnrollmentService(
                new EncryptedLocalVoiceProfileSampleVault(),
                "Pew Pew"));
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<DesktopApp>().UsePlatformDetect();
}
