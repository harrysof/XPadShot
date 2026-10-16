using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XPadShot.Models;
using XPadShot.ViewModels;
using XPadShot.Views;

namespace XPadShot;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsComboHeld))
                SetHeldVisuals(viewModel.IsComboHeld);
        };
        viewModel.CaptureFlash += FlashCapture;
        Closed += (_, _) => viewModel.Dispose();

        // Keep every child (aurora included) inside the rounded shell.
        ShellInner.SizeChanged += (_, _) => UpdateShellClip();
        UpdateShellClip();

        SetHeldVisuals(false);
    }

    private void UpdateShellClip()
    {
        var size = ShellInner.RenderSize;
        if (size.Width > 0 && size.Height > 0)
            ShellInner.Clip = new RectangleGeometry(new Rect(size), 9, 9);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // drag aborted (e.g. button released mid-gesture)
            }
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void AddProfile_Click(object sender, RoutedEventArgs e) => _viewModel.AddProfile();

    private void RemoveProfile_Click(object sender, RoutedEventArgs e) => _viewModel.RemoveSelectedProfile();

    private void AddShortcut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ShortcutDialog("New recording shortcut", "", ShortcutModifiers.None, 0)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true)
            _viewModel.AddCustomShortcut(dialog.ShortcutName, dialog.Modifiers, dialog.VirtualKey);
    }

    private void EditShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProfile is not { RecordShortcutId: { Length: > 0 } id } ||
            _viewModel.FindCustomShortcut(id) is not { } shortcut)
            return;

        var dialog = new ShortcutDialog("Edit recording shortcut", shortcut.Name, shortcut.Modifiers, shortcut.VirtualKey)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true)
            _viewModel.UpdateCustomShortcut(id, dialog.ShortcutName, dialog.Modifiers, dialog.VirtualKey);
    }

    private void DeleteShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProfile is not { RecordShortcutId: { Length: > 0 } id } ||
            _viewModel.FindCustomShortcut(id) is not { } shortcut)
            return;

        var dialog = new ConfirmDialog(
            "Delete shortcut",
            $"Delete \"{shortcut.Name}\"? Profiles using it will fall back to the Game Bar record.",
            "Delete")
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true)
            _viewModel.DeleteCustomShortcut(id);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = _viewModel.FolderPath;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // opening the folder is best-effort
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.BeginRename();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            RenameBox.Focus();
            RenameBox.SelectAll();
        }), DispatcherPriority.Input);
    }

    private void RenameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CancelRename();
            e.Handled = true;
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e) => _viewModel.CommitRename();

    /// <summary>Lights the LED glow while both combo buttons are held (slow breathing pulse).</summary>
    private void SetHeldVisuals(bool held)
    {
        var fade = TimeSpan.FromMilliseconds(160);

        LedGlow.BeginAnimation(OpacityProperty, held
            ? new DoubleAnimation(1.0, 0.72, TimeSpan.FromMilliseconds(1100))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            }
            : new DoubleAnimation(0, fade));
    }

    /// <summary>White-blue burst shaped like the button logo when a capture completes.</summary>
    private void FlashCapture()
    {
        LedFlash.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0, TimeSpan.FromMilliseconds(520))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
    }
}
