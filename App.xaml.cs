using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using XPadShot.Interop;
using XPadShot.Models;
using XPadShot.Services;
using XPadShot.ViewModels;
using XPadShot.Views;
using WinForms = System.Windows.Forms;

namespace XPadShot;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private WinForms.NotifyIcon? _tray;
    private MainWindow? _window;
    private MainViewModel? _viewModel;
    private bool _exiting;
    private bool _balloonShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (HasArg(e, "--test-sound"))
        {
            new SoundService().PlayShutter();
            Thread.Sleep(800);
            Shutdown(0);
            return;
        }

        var settings = new SettingsService();
        var screenshots = new ScreenshotService();

        // Headless single capture (useful for scripting and testing): XPadShot.exe --capture
        if (HasArg(e, "--capture"))
        {
            try
            {
                var (process, title) = ForegroundWatcher.GetForegroundInfo();
                screenshots.Capture(process, title);
                Shutdown(0);
            }
            catch
            {
                Shutdown(1);
            }

            return;
        }

        _mutex = new Mutex(true, "XPadShot.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            ActivateExistingWindow();
            Shutdown();
            return;
        }

        // Keep the background app alive through unexpected errors: log + inform.
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            System.Windows.MessageBox.Show(
                "Something unexpected happened, but XPadShot is still running:\n\n" + args.Exception.Message,
                "XPadShot",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                LogError(exception);
        };

        // First launch on this PC: let the user pick their screenshot combo.
        if (settings.Load().Profiles.Count == 0)
        {
            var setup = new FirstRunDialog();
            setup.ShowDialog();

            settings.Save(new AppSettings
            {
                Profiles =
                [
                    new GameProfile
                    {
                        Name = "Default",
                        First = setup.First,
                        Second = setup.Second,
                    },
                ],
                SelectedProfile = "Default",
            });
        }

        var watcher = new ControllerWatcher();
        var foreground = new ForegroundWatcher();
        var sound = new SoundService();

        _viewModel = new MainViewModel(watcher, foreground, settings, screenshots, sound);
        _window = new MainWindow(_viewModel);
        _window.Closing += OnWindowClosing;

        SetupTray();

        // --minimized (used by the "start with Windows" entry): tray only.
        if (!HasArg(e, "--minimized"))
            _window.Show();
    }

    private static bool HasArg(StartupEventArgs e, string arg) =>
        e.Args.Contains(arg, StringComparer.OrdinalIgnoreCase);

    /// <summary>Closing the window keeps the app alive in the notification tray.</summary>
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
            return;

        e.Cancel = true;
        _window?.Hide();

        if (!_balloonShown && _tray is not null)
        {
            _tray.ShowBalloonTip(
                3500,
                "XPadShot is still running",
                "Your screenshot combo stays live in the background. Right-click the tray icon to exit.",
                WinForms.ToolTipIcon.Info);
            _balloonShown = true;
        }
    }

    private void SetupTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Icon = TrayIconFactory.Create(),
            Text = "XPadShot",
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowWindowFromTray();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open XPadShot", null, (_, _) => ShowWindowFromTray());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip = menu;
    }

    private void ShowWindowFromTray()
    {
        if (_window is null)
            return;

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ExitApplication()
    {
        _exiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _tray?.Dispose();
        _tray = null;
        _mutex?.Dispose();
        _mutex = null;
        base.OnExit(e);
    }

    /// <summary>Second launch: bring the already-running instance forward.</summary>
    private static void ActivateExistingWindow()
    {
        var handle = NativeWindows.FindWindowW(null, "XPadShot");
        if (handle == IntPtr.Zero)
            return;

        NativeWindows.ShowWindow(handle, NativeWindows.SW_RESTORE);
        NativeWindows.SetForegroundWindow(handle);
    }

    private static void LogError(Exception exception)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "XPadShot.error.log"),
                $"{DateTime.Now:u} {exception}{Environment.NewLine}");
        }
        catch
        {
            // logging is best-effort
        }
    }
}
