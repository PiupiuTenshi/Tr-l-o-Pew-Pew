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
using PewPew.Application.Automation;
using PewPew.Application.Voice;
using PewPew.Domain.Voice;
using PewPew.SharedKernel.Configuration;
using System.Diagnostics;
using System.Security.Cryptography;

namespace PewPew.Desktop;

public sealed class DesktopApp : Avalonia.Application
{
    private static ILocalSpeechOutput _speechOutput = new UnavailableSpeechOutput();
    private static ILocalSpeechTranscriber _speechTranscriber = new UnavailableSpeechTranscriber();
    private static VoiceWakeProfileEnrollmentService? _voiceProfileEnrollment;
    private static BrowserMediaActionCoordinator? _browserMedia;
    private static IVerifiedBrowserActionChannel? _browserChannel;
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

    public static void ConfigureVoiceProfileEnrollment(VoiceWakeProfileEnrollmentService enrollment)
    {
        _voiceProfileEnrollment = enrollment ?? throw new ArgumentNullException(nameof(enrollment));
    }

    public static void ConfigureBrowserMedia(BrowserMediaActionCoordinator coordinator, IVerifiedBrowserActionChannel channel)
    {
        _browserMedia = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _browserChannel = channel ?? throw new ArgumentNullException(nameof(channel));
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

        var enrollmentStatus = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetName(enrollmentStatus, "Voice profile enrollment status");
        var enrollmentConsent = new CheckBox
        {
            Content = "I consent to collect short local voice samples for a wake profile. Samples are encrypted, expire in 15 minutes, and can be deleted below.",
            IsEnabled = _voiceProfileEnrollment is not null
        };
        AutomationProperties.SetName(enrollmentConsent, "Voice profile enrollment consent");
        var environmentPicker = new ComboBox
        {
            ItemsSource = new[] { "Quiet room", "Normal room", "Noisy room", "Headset" },
            SelectedIndex = 0,
            IsEnabled = false
        };
        AutomationProperties.SetName(environmentPicker, "Voice sample environment");
        var beginEnrollment = new Button { Content = "Grant consent and begin enrollment", IsEnabled = false };
        var startSample = new Button { Content = "Start sample recording", IsEnabled = false };
        var saveSample = new Button { Content = "Save encrypted sample", IsEnabled = false };
        var cancelEnrollment = new Button { Content = "Cancel enrollment and delete samples", IsEnabled = false };
        var withdrawConsent = new Button { Content = "Withdraw consent and delete profile", IsEnabled = false };
        AutomationProperties.SetName(beginEnrollment, "Begin voice profile enrollment");
        AutomationProperties.SetName(startSample, "Start explicit voice profile sample recording");
        AutomationProperties.SetName(saveSample, "Save encrypted voice profile sample");
        AutomationProperties.SetName(cancelEnrollment, "Cancel voice profile enrollment and delete samples");
        AutomationProperties.SetName(withdrawConsent, "Withdraw voice profile consent and delete profile");
        var enrollmentStopwatch = new Stopwatch();
        var enrollmentCaptureActive = false;

        void UpdateEnrollmentControls(string? notice = null)
        {
            var enrollment = _voiceProfileEnrollment;
            if (enrollment is null)
            {
                enrollmentStatus.Text = "Voice-profile enrollment is unavailable. Text and push-to-talk remain available.";
                return;
            }

            var snapshot = enrollment.Snapshot;
            enrollmentStatus.Text = notice ?? $"Profile: {snapshot.Status}; samples: {snapshot.SampleCount}/{snapshot.SampleLimit}. Biometric matching is not active until P02-T21 validation.";
            beginEnrollment.IsEnabled = !enrollmentCaptureActive && snapshot.Status == VoiceWakeProfileStatus.Draft && enrollmentConsent.IsChecked == true;
            environmentPicker.IsEnabled = snapshot.Status == VoiceWakeProfileStatus.CollectingSamples && !enrollmentCaptureActive;
            startSample.IsEnabled = snapshot.Status == VoiceWakeProfileStatus.CollectingSamples && !enrollmentCaptureActive && !speechInput.IsListening;
            saveSample.IsEnabled = enrollmentCaptureActive && speechInput.IsListening;
            cancelEnrollment.IsEnabled = snapshot.Status == VoiceWakeProfileStatus.CollectingSamples || enrollmentCaptureActive;
            withdrawConsent.IsEnabled = snapshot.HasConsent || snapshot.Status == VoiceWakeProfileStatus.CollectingSamples;
        }

        async Task CancelEnrollmentCaptureAsync()
        {
            if (enrollmentCaptureActive && speechInput.IsListening)
            {
                await speechInput.CancelAsync();
            }

            enrollmentCaptureActive = false;
            enrollmentStopwatch.Reset();
            UpdateAudioControls();
        }

        void LockGeneralSpeechControlsForEnrollmentCapture()
        {
            startListening.IsEnabled = false;
            checkWakePhrase.IsEnabled = false;
            stopAndTranscribe.IsEnabled = false;
            cancelListening.IsEnabled = false;
        }

        enrollmentConsent.IsCheckedChanged += (_, _) => UpdateEnrollmentControls();
        beginEnrollment.Click += (_, _) =>
        {
            if (_voiceProfileEnrollment is null || enrollmentConsent.IsChecked != true)
            {
                UpdateEnrollmentControls("Consent is required before collecting a voice sample.");
                return;
            }

            var result = _voiceProfileEnrollment.BeginEnrollment(DateTimeOffset.UtcNow);
            UpdateEnrollmentControls(result.IsSuccess
                ? "Consent recorded. Choose an environment, then record a short 3–5 second sample."
                : result.Error.Message);
        };
        startSample.Click += async (_, _) =>
        {
            if (_voiceProfileEnrollment is null)
            {
                return;
            }

            await speechInput.StartAsync(CancellationToken.None);
            enrollmentCaptureActive = speechInput.IsListening;
            if (enrollmentCaptureActive)
            {
                enrollmentStopwatch.Restart();
                UpdateEnrollmentControls("Recording a local enrollment sample. Press Save encrypted sample when finished; no transcript is retained.");
            }
            else
            {
                UpdateEnrollmentControls(speechInput.StatusLabel);
            }

            UpdateAudioControls();
            if (enrollmentCaptureActive)
            {
                LockGeneralSpeechControlsForEnrollmentCapture();
            }
        };
        saveSample.Click += async (_, _) =>
        {
            if (_voiceProfileEnrollment is null || !enrollmentCaptureActive)
            {
                return;
            }

            await using var audio = await speechInput.StopCaptureAsync(CancellationToken.None);
            enrollmentCaptureActive = false;
            enrollmentStopwatch.Stop();
            if (audio is null)
            {
                UpdateEnrollmentControls("No sample was saved. Microphone capture ended before audio was available.");
                UpdateAudioControls();
                return;
            }

            byte[]? sampleBytes = null;
            try
            {
                sampleBytes = audio.ToArray();
                var environment = new SampleEnvironmentLabel(environmentPicker.SelectedItem as string ?? "Normal room");
                var duration = enrollmentStopwatch.Elapsed;
                var result = await _voiceProfileEnrollment.StoreCapturedSampleAsync(
                    sampleBytes,
                    environment,
                    duration,
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
                UpdateEnrollmentControls(result.IsSuccess
                    ? "Encrypted local sample saved. Record additional samples in different environments; matching remains inactive until P02-T21."
                    : result.Error.Message);
            }
            finally
            {
                if (sampleBytes is not null)
                {
                    CryptographicOperations.ZeroMemory(sampleBytes);
                }

                if (audio.TryGetBuffer(out var buffer))
                {
                    CryptographicOperations.ZeroMemory(buffer.Array!.AsSpan(buffer.Offset, buffer.Count));
                }
            }

            UpdateAudioControls();
        };
        cancelEnrollment.Click += async (_, _) =>
        {
            if (_voiceProfileEnrollment is null)
            {
                return;
            }

            await CancelEnrollmentCaptureAsync();
            var result = await _voiceProfileEnrollment.CancelEnrollmentAsync(CancellationToken.None);
            UpdateEnrollmentControls(result.IsSuccess
                ? "Enrollment cancelled. All encrypted samples for this profile were deleted."
                : result.Error.Message);
        };
        withdrawConsent.Click += async (_, _) =>
        {
            if (_voiceProfileEnrollment is null)
            {
                return;
            }

            await CancelEnrollmentCaptureAsync();
            var result = await _voiceProfileEnrollment.WithdrawConsentAndDeleteAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            if (result.IsSuccess)
            {
                _voiceProfileEnrollment.ResetAfterDeletion("Pew Pew");
                enrollmentConsent.IsChecked = false;
            }

            UpdateEnrollmentControls(result.IsSuccess
                ? "Consent withdrawn and local voice profile data deleted."
                : result.Error.Message);
        };
        UpdateEnrollmentControls();

        var browserStatus = new TextBlock { Text = "Connect the Edge extension, then choose a media action. Each action needs a separate confirmation.", TextWrapping = TextWrapping.Wrap };
        var browserConfirm = new Button { Content = "Confirm browser action", IsEnabled = false };
        var browserCancel = new Button { Content = "Cancel pending browser action", IsEnabled = false };
        var browserAction = new ComboBox { ItemsSource = new[] { "play", "pause", "mute", "unmute" }, SelectedIndex = 1 };
        BrowserMediaActionPrompt? pendingBrowserAction = null;
        var requestBrowserAction = new Button { Content = "Request browser media action" };
        AutomationProperties.SetName(requestBrowserAction, "Request a single-use browser media action");
        AutomationProperties.SetName(browserConfirm, "Confirm the pending browser media action");
        requestBrowserAction.Click += (_, _) =>
        {
            var action = browserAction.SelectedItem as string ?? "pause";
            pendingBrowserAction = _browserMedia?.RequestSingleUseAction(action, DateTimeOffset.UtcNow);
            browserConfirm.IsEnabled = pendingBrowserAction is not null;
            browserCancel.IsEnabled = pendingBrowserAction is not null;
            browserStatus.Text = pendingBrowserAction is null
                ? "No fresh active Edge tab context is available. Connect the extension and focus an HTTP(S) media tab."
                : $"Confirm {pendingBrowserAction.Action} for the active tab. This single-use request expires at {pendingBrowserAction.ExpiresAtUtc.LocalDateTime:HH:mm:ss}.";
        };
        browserConfirm.Click += async (_, _) =>
        {
            if (pendingBrowserAction is null || _browserMedia is null || _browserChannel is null)
            {
                return;
            }
            browserConfirm.IsEnabled = false;
            browserCancel.IsEnabled = false;
            var result = await _browserMedia.ConfirmAndExecuteAsync(pendingBrowserAction.RequestId, _browserChannel, DateTimeOffset.UtcNow, CancellationToken.None);
            browserStatus.Text = $"Browser action outcome: {result.Outcome} ({result.ReasonCode}).";
            pendingBrowserAction = null;
        };
        browserCancel.Click += (_, _) =>
        {
            pendingBrowserAction = null;
            browserConfirm.IsEnabled = false;
            browserCancel.IsEnabled = false;
            browserStatus.Text = "Pending browser action cancelled before dispatch. No browser side effect was sent.";
        };

        // ── Section helper: creates a titled card with colored header ──
        static Border CreateSection(string title, string helpText, Color headerColor, params Control[] children)
        {
            var header = new Border
            {
                Background = new SolidColorBrush(headerColor),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Padding = new Thickness(14, 8),
                Child = new TextBlock
                {
                    Text = title,
                    FontSize = 15,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White
                }
            };
            var help = new TextBlock
            {
                Text = helpText,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 8)
            };
            var body = new StackPanel { Spacing = 10 };
            body.Children.Add(help);
            foreach (var child in children)
            {
                body.Children.Add(child);
            }

            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse("#30FFFFFF")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new StackPanel
                {
                    Children =
                    {
                        header,
                        new Border
                        {
                            Padding = new Thickness(14, 10),
                            Child = body
                        }
                    }
                }
            };
        }

        // Helper to create a ScrollViewer with vertical scrolling enabled for small windows
        static ScrollViewer CreateTabScrollViewer(Control content)
        {
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = content
            };
        }

        // ════════════════════════════════════════════════════════════════
        //  TAB 1 — 💬 Trợ lý (Main Assistant: Input, Voice, Response)
        // ════════════════════════════════════════════════════════════════
        var assistantTab = CreateTabScrollViewer(new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
            Children =
            {
                // Mode indicator
                new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#183A5A")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6),
                    Margin = new Thickness(0, 0, 0, 4),
                    Child = new TextBlock { Text = shell.ModeLabel, Foreground = Brushes.White }
                },

                // Text input
                CreateSection(
                    "⌨  Nhập lệnh  —  Command Input",
                    "Nhập yêu cầu bằng văn bản. Nhấn Ctrl+Enter hoặc nút Submit để gửi.",
                    Color.Parse("#2C3E50"),
                    input,
                    submit
                ),

                // Microphone quick control
                CreateSection(
                    "🎙  Giọng nói  —  Voice Input",
                    "Nhấn \"Start listening\" để bật micro, nói lệnh, rồi nhấn \"Stop and transcribe\" để chuyển thành văn bản.",
                    Color.Parse("#1A5276"),
                    audioStatus,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children = { startListening, stopAndTranscribe, cancelListening }
                    }
                ),

                // Response
                CreateSection(
                    "💬  Kết quả  —  Response",
                    "Phản hồi từ trợ lý và trạng thái hoạt động được cập nhật tự động tại đây.",
                    Color.Parse("#6C3483"),
                    new TextBlock { Text = "Trạng thái  (Status)", FontWeight = FontWeight.SemiBold },
                    statusText,
                    new Separator(),
                    new TextBlock { Text = "Phản hồi  (Response)", FontWeight = FontWeight.SemiBold },
                    responseText
                )
            }
        });

        // ════════════════════════════════════════════════════════════════
        //  TAB 2 — ⚙ Cài đặt (Settings: System Voice Selection)
        // ════════════════════════════════════════════════════════════════
        var settingsTab = CreateTabScrollViewer(new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
            Children =
            {
                CreateSection(
                    "🔊  Giọng đọc hệ thống  —  System Voice",
                    "Chọn giọng đọc hệ thống Windows cho trợ lý. Thay đổi có hiệu lực ngay cho lần đọc kế tiếp.",
                    Color.Parse("#1A5276"),
                    voicePicker,
                    voiceStatus
                ),

                CreateSection(
                    "🛡  Chính sách bảo mật & Xử lý cục bộ  —  Local Privacy Policy",
                    "• Mọi xử lý giọng nói, nhận diện từ khóa và lệnh đều diễn ra cục bộ (Local-only) trên máy tính của bạn.\n"
                    + "• Âm thanh thu từ micro chỉ tồn tại tạm thời trong bộ nhớ RAM và được giải phóng ngay sau khi xử lý xong.\n"
                    + "• Không có dữ liệu âm thanh hay văn bản nào được gửi lên server cloud.",
                    Color.Parse("#283747")
                )
            }
        });

        // ════════════════════════════════════════════════════════════════
        //  TAB 3 — 🧪 Developer (Developer Mode: Advanced Testing Suite)
        // ════════════════════════════════════════════════════════════════
        var devTestPanel = new StackPanel
        {
            Spacing = 8,
            IsVisible = false,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // 1. Wake phrase testing card
        devTestPanel.Children.Add(CreateSection(
            "🎙  Test 1: Kiểm tra nhận diện từ khóa Wake Phrase (\"Pew Pew\")",
            "Công cụ kiểm tra trực tiếp khả năng nhận diện từ khóa kích hoạt:\n"
            + "1. Nhấn \"Start listening\" ở tab Trợ lý (hoặc bật micro).\n"
            + "2. Nói từ khóa \"Pew Pew\" rõ ràng.\n"
            + "3. Nhấn \"Check wake phrase\" dưới đây để kiểm tra kết quả nhận diện cục bộ.",
            Color.Parse("#1E8449"),
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { checkWakePhrase }
            }
        ));

        // 2. Voice profile enrollment testing card
        devTestPanel.Children.Add(CreateSection(
            "🎤  Test 2: Đăng ký Profile giọng nói sinh trắc học  (Voice Profile Enrollment — Local Only)",
            "Công cụ thử nghiệm thu thập mẫu giọng nói ngắn (3–5s) ở nhiều môi trường để huấn luyện profile cá nhân:\n"
            + "• Tick ô đồng ý → Chọn môi trường → Ghi mẫu → Lưu mã hóa AES-256.\n"
            + "• Mẫu tự hết hạn sau 15 phút. Matching chưa kích hoạt cho đến P02-T21.",
            Color.Parse("#1E6B4F"),
            enrollmentConsent,
            enrollmentStatus,
            environmentPicker,
            beginEnrollment,
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { startSample, saveSample, cancelEnrollment, withdrawConsent }
            }
        ));

        // 3. Browser media action testing card
        devTestPanel.Children.Add(CreateSection(
            "🌐  Test 3: Điều khiển Trình duyệt  (Browser Media Action)",
            "Công cụ thử nghiệm gửi lệnh điều khiển media đến Extension trình duyệt Chrome/Edge:\n"
            + "• Chọn hành động → Gửi yêu cầu single-use → Xác nhận gửi lệnh.",
            Color.Parse("#7D6608"),
            browserStatus,
            browserAction,
            requestBrowserAction,
            browserConfirm,
            browserCancel
        ));

        // 4. Speech output test card
        devTestPanel.Children.Add(CreateSection(
            "🔈  Test 4: Phát âm thanh phản hồi  (Speech Output Test)",
            "Thử nghiệm đọc to phản hồi bằng giọng đọc hiện tại.",
            Color.Parse("#5B2C6F"),
            speechStatus,
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { speakResponse, stopSpeaking }
            }
        ));

        var devToggle = new CheckBox
        {
            Content = "🔧 Bật chế độ Developer (Developer Mode) — Hiển thị toàn bộ công cụ kiểm tra",
            IsChecked = false,
            Margin = new Thickness(0, 8, 0, 0)
        };
        devToggle.IsCheckedChanged += (_, _) =>
        {
            devTestPanel.IsVisible = devToggle.IsChecked == true;
        };

        var devTab = CreateTabScrollViewer(new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
            Children =
            {
                CreateSection(
                    "🧪  Chế độ Developer  —  Developer Mode",
                    "Chế độ này dành riêng cho kiểm thử và gỡ lỗi toàn bộ các tính năng (Wake phrase, Voice profile, Browser media, Speech output).\n"
                    + "Mặc định các công cụ kiểm tra được ẩn để giữ giao diện gọn gàng. Tick vào ô bên dưới để bật.",
                    Color.Parse("#D35400"),
                    devToggle
                ),
                devTestPanel
            }
        });

        // ════════════════════════════════════════════════════════════════
        //  MAIN LAYOUT — Grid & TabControl for Responsive Resizing
        // ════════════════════════════════════════════════════════════════
        var tabs = new TabControl
        {
            TabStripPlacement = Dock.Top,
            Items =
            {
                new TabItem { Header = "💬  Trợ lý", Content = assistantTab },
                new TabItem { Header = "⚙  Cài đặt", Content = settingsTab },
                new TabItem { Header = "🧪  Developer", Content = devTab }
            }
        };

        var titleHeader = new TextBlock
        {
            Text = "Pew Pew Assistant",
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(titleHeader, 0);
        Grid.SetRow(tabs, 1);

        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { titleHeader, tabs }
        };

        var content = new Border
        {
            Padding = new Thickness(16),
            Child = mainGrid
        };

        var window = new Window
        {
            Title = "Pew Pew Assistant",
            Width = 800,
            Height = 600,
            MinWidth = 520,
            MinHeight = 380,
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
