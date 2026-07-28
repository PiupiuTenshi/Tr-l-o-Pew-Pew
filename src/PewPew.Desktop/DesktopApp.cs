using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PewPew.SharedKernel.Configuration;

namespace PewPew.Desktop;

public sealed class DesktopApp : Application
{
    private TrayIcon? _trayIcon;
    private bool _isExiting;

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
        void SubmitText()
        {
            shell.SubmitText(input.Text);
            statusText.Text = shell.StatusLabel;
            responseText.Text = shell.ResponseLabel;
            _trayIcon?.ToolTipText = $"Pew Pew — {shell.ModeLabel}: {shell.StatusLabel}";
        }

        submit.Click += (_, _) => SubmitText();
        input.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Enter && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                SubmitText();
                eventArgs.Handled = true;
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
}
