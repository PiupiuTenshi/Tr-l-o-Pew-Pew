using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PewPew.Application.Speech;
using PewPew.SharedKernel.Configuration;

namespace PewPew.Desktop;

public sealed class DesktopApp : Avalonia.Application
{
    private static ILocalSpeechOutput _speechOutput = new UnavailableSpeechOutput();
    private TrayIcon? _trayIcon;
    private bool _isExiting;

    public static void ConfigureSpeechOutput(ILocalSpeechOutput speechOutput)
    {
        _speechOutput = speechOutput ?? throw new ArgumentNullException(nameof(speechOutput));
    }

    public override void Initialize()
    {
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = StartupConfiguration.LoadFromEnvironment();
            var shell = new DesktopShellState(configuration);
            var window = CreateMainWindow(shell);

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = window;
            _trayIcon = CreateTrayIcon(desktop, window, shell);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Window CreateMainWindow(DesktopShellState shell)
    {
        var audioSession = new AudioSessionState();
        var speech = new SpeechOutputController(_speechOutput);
        var statusText = new TextBlock
        {
            Text = shell.StatusLabel,
            FontWeight = FontWeight.SemiBold
        };
        var responseText = new TextBlock
        {
            Text = shell.ResponseLabel,
            TextWrapping = TextWrapping.Wrap
        };
        var input = new TextBox
        {
            PlaceholderText = "Type a request. This shell does not execute actions yet.",
            MinHeight = 88,
            AcceptsReturn = true
        };
        AutomationProperties.SetName(input, "Assistant text input");
        var submit = new Button
        {
            Content = "Submit text",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        AutomationProperties.SetName(submit, "Submit assistant text");
        var speechStatus = new TextBlock
        {
            Text = speech.StatusLabel,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetName(speechStatus, "Local speech output status");
        var speakResponse = new Button
        {
            Content = "Speak response",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetName(speakResponse, "Speak the current response locally");
        var stopSpeaking = new Button
        {
            Content = "Stop speaking",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false
        };
        AutomationProperties.SetName(stopSpeaking, "Stop local speech output");
        void UpdateSpeechControls()
        {
            speechStatus.Text = speech.StatusLabel;
            stopSpeaking.IsEnabled = speech.IsSpeaking;
        }

        async Task SpeakResponseAsync()
        {
            var speaking = speech.SpeakAsync(shell.ResponseLabel);
            UpdateSpeechControls();
            await speaking;
            UpdateSpeechControls();
        }

        async Task SubmitTextAsync()
        {
            shell.SubmitText(input.Text);
            statusText.Text = shell.StatusLabel;
            responseText.Text = shell.ResponseLabel;
            _trayIcon?.ToolTipText = $"Pew Pew — {shell.ModeLabel}: {shell.StatusLabel}";
            await SpeakResponseAsync();
        }

        submit.Click += async (_, _) => await SubmitTextAsync();
        input.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                await SubmitTextAsync();
                eventArgs.Handled = true;
            }
        };

        speakResponse.Click += async (_, _) => await SpeakResponseAsync();
        stopSpeaking.Click += async (_, _) =>
        {
            await speech.CancelAsync();
            UpdateSpeechControls();
        };

        var audioStatus = new TextBlock
        {
            Text = audioSession.ConsentLabel,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetName(audioStatus, "Microphone consent and listening status");
        var pushToTalk = new Button
        {
            Content = "Hold to talk",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetName(pushToTalk, "Hold to start an explicit push-to-talk session");
        var cancelListening = new Button
        {
            Content = "Cancel listening",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false
        };
        AutomationProperties.SetName(cancelListening, "Cancel the active push-to-talk session");
        void UpdateAudioControls()
        {
            audioStatus.Text = audioSession.ConsentLabel;
            cancelListening.IsEnabled = audioSession.IsListening;
        }

        pushToTalk.PointerPressed += (_, eventArgs) =>
        {
            if (!audioSession.IsListening)
            {
                audioSession.Start();
                eventArgs.Pointer.Capture(pushToTalk);
                UpdateAudioControls();
            }
        };
        pushToTalk.PointerReleased += (_, eventArgs) =>
        {
            if (audioSession.IsListening)
            {
                audioSession.Stop();
                eventArgs.Pointer.Capture(null);
                UpdateAudioControls();
            }
        };
        pushToTalk.PointerCaptureLost += (_, _) =>
        {
            if (audioSession.IsListening)
            {
                audioSession.Cancel();
                UpdateAudioControls();
            }
        };
        cancelListening.Click += (_, _) =>
        {
            if (audioSession.IsListening)
            {
                audioSession.Cancel();
                UpdateAudioControls();
            }
        };

        var content = new Border
        {
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Pew Pew Assistant",
                        FontSize = 28,
                        FontWeight = FontWeight.Bold
                    },
                    new Border
                    {
                        Background = new SolidColorBrush(Color.Parse("#183A5A")),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12),
                        Child = new TextBlock
                        {
                            Text = shell.ModeLabel,
                            Foreground = Brushes.White
                        }
                    },
                    new TextBlock { Text = "Status", FontWeight = FontWeight.SemiBold },
                    statusText,
                    new TextBlock { Text = "Speech", FontWeight = FontWeight.SemiBold },
                    speechStatus,
                    speakResponse,
                    stopSpeaking,
                    new TextBlock { Text = "Voice", FontWeight = FontWeight.SemiBold },
                    new TextBlock
                    {
                        Text = "Push-to-talk is an explicit per-session consent control. No microphone provider is connected yet.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    audioStatus,
                    pushToTalk,
                    cancelListening,
                    new TextBlock { Text = "Text command", FontWeight = FontWeight.SemiBold },
                    input,
                    new TextBlock { Text = "Press Ctrl+Enter to submit from the keyboard." },
                    submit,
                    new Separator(),
                    new TextBlock { Text = "Response", FontWeight = FontWeight.SemiBold },
                    responseText
                }
            }
        };

        var window = new Window
        {
            Title = "Pew Pew Assistant",
            Width = 800,
            Height = 560,
            MinWidth = 560,
            MinHeight = 420,
            Content = content
        };
        window.Closing += OnWindowClosing;
        return window;
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, Window window, DesktopShellState shell)
    {
        var showItem = new NativeMenuItem("Show Pew Pew");
        showItem.Click += (_, _) => ShowWindow(window);

        var exitItem = new NativeMenuItem("Exit Pew Pew");
        exitItem.Click += (_, _) =>
        {
            _isExiting = true;
            _trayIcon?.Dispose();
            desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(showItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        var trayIcon = new TrayIcon
        {
            IsVisible = true,
            ToolTipText = $"Pew Pew — {shell.ModeLabel}: {shell.StatusLabel}",
            Menu = menu
        };
        trayIcon.Clicked += (_, _) => ShowWindow(window);
        return trayIcon;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_isExiting || sender is not Window window)
        {
            return;
        }

        eventArgs.Cancel = true;
        window.Hide();
    }

    private static void ShowWindow(Window window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        window.Activate();
    }

    private sealed class UnavailableSpeechOutput : ILocalSpeechOutput
    {
        public Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new SpeechOutputResult(SpeechOutputStatus.Unavailable));

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
