using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using PewPew.Application.Speech;
using PewPew.Application.Voice;
using PewPew.SharedKernel.Configuration;

namespace PewPew.Desktop;

public sealed class DesktopApp : Avalonia.Application
{
    private static ILocalSpeechOutput _speechOutput = new UnavailableSpeechOutput();
    private static ILocalSpeechTranscriber _speechTranscriber = new UnavailableSpeechTranscriber();
    private TrayIcon? _trayIcon;
    private bool _isExiting;

    public static void ConfigureSpeechOutput(ILocalSpeechOutput speechOutput)
    {
        _speechOutput = speechOutput ?? throw new ArgumentNullException(nameof(speechOutput));
    }

    public static void ConfigureSpeechTranscriber(ILocalSpeechTranscriber speechTranscriber)
    {
        _speechTranscriber = speechTranscriber ?? throw new ArgumentNullException(nameof(speechTranscriber));
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
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
        var speechInput = new LocalSpeechInputController(
            audioSession,
            new WindowsMicrophoneCapture(),
            new LocalSpeechInteractionService(_speechTranscriber));
        var wakePhraseActivation = new WakePhraseActivationService(
            new PcmWaveVoiceActivityGate(),
            new WhisperWakePhraseDetector(_speechTranscriber),
            new LocalSpeechInputListeningActivator(speechInput));
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
        var voicePicker = new ComboBox
        {
            ItemsSource = speech.AvailableVoices,
            SelectedItem = speech.SelectedVoice,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(voicePicker, "Local speech voice selection");
        var voiceStatus = new TextBlock
        {
            Text = speech.SelectedVoice?.DisplayName ?? "Windows default voice is unavailable.",
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetName(voiceStatus, "Selected local speech voice");
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
            voicePicker.IsEnabled = !speech.IsSpeaking && speech.AvailableVoices.Count > 0;
            voiceStatus.Text = speech.SelectedVoice?.DisplayName ?? speech.StatusLabel;
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
        voicePicker.SelectionChanged += (_, _) =>
        {
            if (voicePicker.SelectedItem is not LocalSpeechVoice voice || voice.Id == speech.SelectedVoiceId)
            {
                return;
            }

            _ = speech.SelectVoice(voice.Id);
            voicePicker.SelectedItem = speech.SelectedVoice;
            UpdateSpeechControls();
        };

        var audioStatus = new TextBlock
        {
            Text = audioSession.ConsentLabel,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetName(audioStatus, "Microphone consent and listening status");
        var startListening = new Button
        {
            Content = "Start listening",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetName(startListening, "Start an explicit local microphone session");
        var stopAndTranscribe = new Button
        {
            Content = "Stop and transcribe",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false
        };
        AutomationProperties.SetName(stopAndTranscribe, "Stop listening and transcribe locally");
        var cancelListening = new Button
        {
            Content = "Cancel listening",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false
        };
        AutomationProperties.SetName(cancelListening, "Cancel the active push-to-talk session");
        var checkWakePhrase = new Button
        {
            Content = "Check wake phrase",
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = false
        };
        AutomationProperties.SetName(checkWakePhrase, "Check captured local audio for the wake phrase");
        void UpdateAudioControls()
        {
            audioStatus.Text = speechInput.StatusLabel;
            cancelListening.IsEnabled = speechInput.IsListening;
            startListening.IsEnabled = !speechInput.IsListening;
            stopAndTranscribe.IsEnabled = speechInput.IsListening;
            checkWakePhrase.IsEnabled = speechInput.IsListening;
        }

        async Task StartListeningAsync()
        {
            if (!speechInput.IsListening)
            {
                await speechInput.StartAsync(CancellationToken.None);
                UpdateAudioControls();
            }
        }

        async Task StopAndTranscribeAsync()
        {
            var result = await speechInput.StopAndTranscribeAsync(CancellationToken.None);
            if (result is not null)
            {
                if (result.Transcription.Status == LocalSpeechTranscriptionStatus.Transcribed)
                {
                    shell.SubmitText(result.Transcription.Transcript);
                    statusText.Text = shell.StatusLabel;
                    responseText.Text = shell.ResponseLabel;
                    await SpeakResponseAsync();
                }
                else
                {
                    responseText.Text = result.Transcription.ClarificationPrompt ?? result.Transcription.Reason ?? shell.ResponseLabel;
                }
            }

            _trayIcon?.ToolTipText = $"Pew Pew — {shell.ModeLabel}: {speechInput.StatusLabel}";
            UpdateAudioControls();
        }

        async Task CheckWakePhraseAsync()
        {
            await using var audio = await speechInput.StopCaptureAsync(CancellationToken.None);
            if (audio is null)
            {
                UpdateAudioControls();
                return;
            }

            var result = await wakePhraseActivation.TryActivateAsync(audio, CancellationToken.None);
            audioStatus.Text = result.Status switch
            {
                WakePhraseActivationStatus.ListeningStarted => "Wake phrase detected locally. Listening for your request.",
                WakePhraseActivationStatus.Cancelled => "Wake phrase check was cancelled. Text input remains available.",
                _ => "Wake phrase was not detected. Audio was discarded; text input remains available."
            };
            cancelListening.IsEnabled = speechInput.IsListening;
            startListening.IsEnabled = !speechInput.IsListening;
            stopAndTranscribe.IsEnabled = speechInput.IsListening;
            checkWakePhrase.IsEnabled = speechInput.IsListening;
        }

        startListening.Click += async (_, _) => await StartListeningAsync();
        stopAndTranscribe.Click += async (_, _) => await StopAndTranscribeAsync();
        checkWakePhrase.Click += async (_, _) => await CheckWakePhraseAsync();
        cancelListening.Click += async (_, _) =>
        {
            if (speechInput.IsListening)
            {
                await speechInput.CancelAsync();
                UpdateAudioControls();
            }
        };

        var content = new Border
        {
            Padding = new Thickness(24),
            Child = new ScrollViewer
            {
                Content = new StackPanel
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
                    voicePicker,
                    voiceStatus,
                    new TextBlock
                    {
                        Text = "Start listening grants microphone consent only for this session. Stop and transcribe processes audio locally, then discards it.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    audioStatus,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { startListening, checkWakePhrase, stopAndTranscribe, cancelListening }
                    },
                    new TextBlock { Text = "Text command", FontWeight = FontWeight.SemiBold },
                    input,
                    new TextBlock { Text = "Press Ctrl+Enter to submit from the keyboard." },
                    submit,
                    new Separator(),
                    new TextBlock { Text = "Response", FontWeight = FontWeight.SemiBold },
                        responseText
                    }
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
        window.Closing += (_, eventArgs) =>
        {
            if (!_isExiting)
            {
                OnWindowClosing(window, eventArgs);
                return;
            }

            speechInput.Dispose();
        };
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
        public IReadOnlyList<LocalSpeechVoice> AvailableVoices => Array.Empty<LocalSpeechVoice>();

        public string SelectedVoiceId => string.Empty;

        public SpeechOutputResult SelectVoice(string voiceId) =>
            new(SpeechOutputStatus.Unavailable, "Windows speech is unavailable.");

        public Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new SpeechOutputResult(SpeechOutputStatus.Unavailable));

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnavailableSpeechTranscriber : ILocalSpeechTranscriber
    {
        public Task<LocalSpeechTranscriptionResult> TranscribeAsync(Stream waveAudio, CancellationToken cancellationToken) =>
            Task.FromResult(new LocalSpeechTranscriptionResult(
                LocalSpeechTranscriptionStatus.Unavailable,
                Reason: "Local STT is unavailable. Text input remains available."));
    }
}
