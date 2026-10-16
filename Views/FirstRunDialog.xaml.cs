using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XPadShot.Models;
using XPadShot.ViewModels;

namespace XPadShot.Views;

public partial class FirstRunDialog : Window
{
    private readonly IReadOnlyList<ButtonOption> _allButtons;

    public ControllerButton First { get; private set; } = ControllerButton.LB;
    public ControllerButton Second { get; private set; } = ControllerButton.A;

    public FirstRunDialog()
    {
        InitializeComponent();

        _allButtons = Enum.GetValues<ControllerButton>()
            .Where(b => b != ControllerButton.None)
            .Select(b => new ButtonOption(b, b.Label()))
            .ToList();

        FirstCombo.ItemsSource = _allButtons;
        FirstCombo.SelectedValue = ControllerButton.LB;
        RefreshSecondOptions();
        SecondCombo.SelectedValue = ControllerButton.A;
    }

    private void FirstCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshSecondOptions();

    private void RefreshSecondOptions()
    {
        var first = FirstCombo.SelectedValue is ControllerButton selected ? selected : ControllerButton.LB;

        var options = new List<ButtonOption> { new(ControllerButton.None, "None") };
        options.AddRange(_allButtons.Where(o => o.Value != first));
        SecondCombo.ItemsSource = options;

        if (SecondCombo.SelectedValue is not ControllerButton second || second == first)
            SecondCombo.SelectedValue = first == ControllerButton.A ? ControllerButton.None : ControllerButton.A;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        First = FirstCombo.SelectedValue is ControllerButton first ? first : ControllerButton.LB;
        Second = SecondCombo.SelectedValue is ControllerButton second ? second : ControllerButton.None;
        DialogResult = true;
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
                // drag aborted
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
