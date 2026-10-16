using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XPadShot.Models;

namespace XPadShot.Views;

/// <summary>Add/edit dialog for a custom recording shortcut: a name plus
/// modifiers picked with checkboxes and a key picked from a dropdown.</summary>
public partial class ShortcutDialog : Window
{
    private bool _loading;

    public string ShortcutName { get; private set; } = "";
    public ShortcutModifiers Modifiers { get; private set; }
    public int VirtualKey { get; private set; }

    public ShortcutDialog(string title, string name, ShortcutModifiers modifiers, int virtualKey)
    {
        InitializeComponent();

        _loading = true;
        TitleText.Text = title;
        Title = title;
        NameBox.Text = name;
        CtrlBox.IsChecked = modifiers.HasFlag(ShortcutModifiers.Ctrl);
        AltBox.IsChecked = modifiers.HasFlag(ShortcutModifiers.Alt);
        ShiftBox.IsChecked = modifiers.HasFlag(ShortcutModifiers.Shift);
        WinBox.IsChecked = modifiers.HasFlag(ShortcutModifiers.Win);
        KeyBox.ItemsSource = ShortcutKeyCatalog.Items;
        if (virtualKey != 0)
            KeyBox.SelectedValue = virtualKey;
        _loading = false;

        UpdatePreview();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Modifier_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    private void KeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private ShortcutModifiers ReadModifiers()
    {
        var modifiers = ShortcutModifiers.None;

        if (CtrlBox.IsChecked == true)
            modifiers |= ShortcutModifiers.Ctrl;
        if (AltBox.IsChecked == true)
            modifiers |= ShortcutModifiers.Alt;
        if (ShiftBox.IsChecked == true)
            modifiers |= ShortcutModifiers.Shift;
        if (WinBox.IsChecked == true)
            modifiers |= ShortcutModifiers.Win;

        return modifiers;
    }

    private void UpdatePreview()
    {
        if (_loading)
            return;

        var virtualKey = KeyBox.SelectedValue is int value ? value : 0;
        PreviewText.Text = virtualKey == 0
            ? "Pick the key for this shortcut."
            : "Sends: " + ShortcutText.Describe(ReadModifiers(), virtualKey);
        SaveButton.IsEnabled = virtualKey != 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (KeyBox.SelectedValue is not int virtualKey || virtualKey == 0)
            return;

        ShortcutName = NameBox.Text;
        Modifiers = ReadModifiers();
        VirtualKey = virtualKey;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

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
                // drag aborted
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
