using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace XPadShot.Services;

/// <summary>Tray icon: the app logo, scaled down; a drawn orb as fallback.</summary>
public static class TrayIconFactory
{
    public static Icon Create() => CreateFromLogo() ?? CreateFallback();

    private static Icon? CreateFromLogo()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/assets/Xpadshot.png");
            using var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream is null)
                return null;

            using var source = new Bitmap(stream);
            using var scaled = new Bitmap(32, 32);

            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, 32, 32);
            }

            return CloneFromHicon(scaled);
        }
        catch
        {
            return null;
        }
    }

    private static Icon CreateFallback()
    {
        using var bitmap = new Bitmap(32, 32);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var orb = new GraphicsPath();
            orb.AddEllipse(2, 2, 28, 28);

            using var fill = new PathGradientBrush(orb)
            {
                CenterPoint = new PointF(13, 11),
                CenterColor = Color.FromArgb(255, 168, 216, 255),
                SurroundColors = new[] { Color.FromArgb(255, 24, 74, 130) },
            };
            g.FillPath(fill, orb);

            using var gloss = new SolidBrush(Color.FromArgb(150, 255, 255, 255));
            g.FillEllipse(gloss, 8, 5, 16, 9);
        }

        return CloneFromHicon(bitmap);
    }

    private static Icon CloneFromHicon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
