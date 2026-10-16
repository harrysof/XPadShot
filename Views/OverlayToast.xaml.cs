using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XPadShot.Interop;

namespace XPadShot.Views;

/// <summary>Small feedback toast ("Screenshot taken", "Recording started" /
/// "Recording ended") that slides in from the right screen edge to the top
/// right corner and slides back out to the right on dismiss. Built to stay
/// out of the way: click-through, never takes focus, no taskbar or alt-tab
/// entry, and it re-asserts topmost on every show so it also lands over
/// borderless-fullscreen apps. Only opacity and a transform are animated -
/// no layout churn, no effects.</summary>
public partial class OverlayToast : Window
{
    /// <summary>The window is flush with the screen's right edge; the box is
    /// inset 24px from it (see XAML). Translating by the full window width
    /// puts the box completely past the edge.</summary>
    private const double SlideDistance = 256;

    private readonly TranslateTransform _slide = new();
    private readonly Storyboard _storyboard;

    public OverlayToast()
    {
        InitializeComponent();
        Root.RenderTransform = _slide;
        _storyboard = BuildStoryboard();
        _storyboard.Completed += (_, _) => Hide();
        SourceInitialized += (_, _) => ApplyOverlayStyles();
    }

    public void ShowMessage(string message)
    {
        MessageText.Text = message;

        _storyboard.Stop(this);
        Root.Opacity = 0;
        _slide.X = SlideDistance;

        if (!IsVisible)
            Show();

        PositionOnPrimaryScreen();

        NativeWindows.SetWindowPos(
            new WindowInteropHelper(this).Handle,
            NativeWindows.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeWindows.SWP_NOMOVE | NativeWindows.SWP_NOSIZE | NativeWindows.SWP_NOACTIVATE);

        _storyboard.Begin(this, true);
    }

    private void PositionOnPrimaryScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width;   // flush with the right edge, pinned in the corner
        Top = area.Top + 28;
    }

    /// <summary>Click-through + no activation + hidden from alt-tab.</summary>
    private void ApplyOverlayStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeWindows.GetWindowLongW(hwnd, NativeWindows.GWL_EXSTYLE);
        NativeWindows.SetWindowLongW(
            hwnd,
            NativeWindows.GWL_EXSTYLE,
            style | NativeWindows.WS_EX_TOOLWINDOW | NativeWindows.WS_EX_NOACTIVATE | NativeWindows.WS_EX_TRANSPARENT);
    }

    private Storyboard BuildStoryboard()
    {
        var storyboard = new Storyboard();

        // Fade in, hold, fade out.
        var opacity = new DoubleAnimationUsingKeyFrames();
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.Zero));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(1, TimeSpan.FromMilliseconds(1650)));
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(1950))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        });
        Storyboard.SetTarget(opacity, Root);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(UIElement.OpacityProperty));

        // Slide in from beyond the right screen edge, hold, slide back out.
        var slide = new DoubleAnimationUsingKeyFrames();
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(SlideDistance, TimeSpan.Zero));
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(0, TimeSpan.FromMilliseconds(1650)));
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(SlideDistance, TimeSpan.FromMilliseconds(1970))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        });
        Storyboard.SetTarget(slide, Root);
        Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

        storyboard.Children.Add(opacity);
        storyboard.Children.Add(slide);
        return storyboard;
    }
}
