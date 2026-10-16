using System.Windows;
using System.Windows.Input;

namespace XPadShot.Views;

/// <summary>A small glass confirm prompt with a custom title, message and
/// confirm-button label. ShowDialog returns true when confirmed.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmLabel)
    {
        InitializeComponent();

        TitleText.Text = title;
        Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

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
}
