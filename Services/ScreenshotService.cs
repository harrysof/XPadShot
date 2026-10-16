using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace XPadShot.Services;

public readonly record struct CaptureResult(string Path, long Bytes, int Width, int Height);

/// <summary>
/// Captures the whole desktop (all monitors, like Win+PrtScn) and saves a PNG
/// into Pictures\Screenshots, named "&lt;app&gt; - dd-MM-yyyy HH-mm-ss.png".
/// </summary>
public sealed class ScreenshotService
{
    private const int MaxLabelLength = 60;

    private static readonly string[] BrowserSuffixes =
    [
        " - Brave",
        " - Google Chrome",
        " - Chromium",
        " - Microsoft Edge",
        " - Mozilla Firefox",
        " - Firefox",
        " - Opera",
        " - Vivaldi",
        " - LibreWolf",
    ];

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public string FolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

    public CaptureResult Capture(string? process, string? title)
    {
        Directory.CreateDirectory(FolderPath);

        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }

        var path = NextFilePath(process, title);
        bitmap.Save(path, ImageFormat.Png);

        return new CaptureResult(path, new FileInfo(path).Length, width, height);
    }

    /// <summary>
    /// Builds the display label for a screenshot from the foreground app:
    /// window title preferred (browser suffix stripped), process name as the
    /// fallback, "Screenshot" as the last resort.
    /// </summary>
    public static string BuildLabel(string? process, string? title)
    {
        var text = (title ?? "").Trim();

        foreach (var suffix in BrowserSuffixes)
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^suffix.Length].TrimEnd();
                break;
            }
        }

        var invalid = Path.GetInvalidFileNameChars();
        text = new string(text.Where(c => !invalid.Contains(c)).ToArray());
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Trim(' ', '.');

        if (text.Length > MaxLabelLength)
        {
            text = text[..MaxLabelLength];
            if (char.IsHighSurrogate(text[^1]))
                text = text[..^1];
            text = text.TrimEnd(' ', '.');
        }

        if (text.Length == 0)
            text = (process ?? "").Trim();

        return text.Length == 0 ? "Screenshot" : text;
    }

    private string NextFilePath(string? process, string? title)
    {
        string label = BuildLabel(process, title);
        string name = $"{label} - {DateTime.Now:dd-MM-yyyy HH-mm-ss}";
        return MakeUnique(Path.Combine(FolderPath, name + ".png"));
    }

    private static string MakeUnique(string path)
    {
        if (!File.Exists(path))
            return path;

        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);

        for (int i = 2; ; i++)
        {
            var candidate = Path.Combine(folder, $"{name} ({i}).png");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
