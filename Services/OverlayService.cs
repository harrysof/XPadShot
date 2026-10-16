using System.Windows;
using XPadShot.Views;

namespace XPadShot.Services;

/// <summary>Shows the small feedback toast (screenshot / recording state).
/// One reusable window is created lazily and hidden again when idle, so
/// showing it costs nothing between events.</summary>
public static class OverlayService
{
    private static OverlayToast? _toast;

    public static void Show(string message)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        app.Dispatcher.InvokeAsync(() =>
        {
            _toast ??= new OverlayToast();
            _toast.ShowMessage(message);
        });
    }
}
