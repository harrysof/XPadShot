using System.Windows.Input;

namespace XPadShot.Models;

/// <summary>One selectable key in the shortcut editor.</summary>
public sealed record ShortcutKey(int VirtualKey, string Display);

/// <summary>The fixed list of keys offered by the shortcut editor. Picking keys
/// from dropdowns replaced the old "press your keys" capture dialog, which
/// could misread chords.</summary>
public static class ShortcutKeyCatalog
{
    public static IReadOnlyList<ShortcutKey> Items { get; } = Build();

    private static readonly Dictionary<int, string> Names =
        Items.ToDictionary(k => k.VirtualKey, k => k.Display);

    public static string DisplayName(int virtualKey)
    {
        if (virtualKey == 0)
            return "";

        if (Names.TryGetValue(virtualKey, out var name))
            return name;

        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        return key == Key.None ? "?" : key.ToString();
    }

    private static List<ShortcutKey> Build()
    {
        var items = new List<ShortcutKey>();

        void Add(Key key, string display) =>
            items.Add(new ShortcutKey(KeyInterop.VirtualKeyFromKey(key), display));

        for (var key = Key.A; key <= Key.Z; key++)
            Add(key, key.ToString());

        for (var key = Key.D0; key <= Key.D9; key++)
            Add(key, (key - Key.D0).ToString());

        for (var key = Key.F1; key <= Key.F12; key++)
            Add(key, key.ToString());

        for (var key = Key.NumPad0; key <= Key.NumPad9; key++)
            Add(key, "Num " + (key - Key.NumPad0));

        Add(Key.Add, "Num +");
        Add(Key.Subtract, "Num -");
        Add(Key.Multiply, "Num *");
        Add(Key.Divide, "Num /");
        Add(Key.Decimal, "Num .");

        Add(Key.Up, "Up");
        Add(Key.Down, "Down");
        Add(Key.Left, "Left");
        Add(Key.Right, "Right");
        Add(Key.Home, "Home");
        Add(Key.End, "End");
        Add(Key.Prior, "Page Up");
        Add(Key.Next, "Page Down");
        Add(Key.Insert, "Insert");
        Add(Key.Delete, "Delete");
        Add(Key.Back, "Backspace");
        Add(Key.Tab, "Tab");
        Add(Key.Return, "Enter");
        Add(Key.Space, "Space");
        Add(Key.Escape, "Escape");
        Add(Key.Capital, "Caps Lock");
        Add(Key.NumLock, "Num Lock");
        Add(Key.Scroll, "Scroll Lock");
        Add(Key.Snapshot, "Print Screen");
        Add(Key.Pause, "Pause");
        Add(Key.Apps, "Menu");

        Add(Key.OemMinus, "-");
        Add(Key.OemPlus, "=");
        Add(Key.OemOpenBrackets, "[");
        Add(Key.OemCloseBrackets, "]");
        Add(Key.OemPipe, "\\");
        Add(Key.OemSemicolon, ";");
        Add(Key.OemQuotes, "'");
        Add(Key.OemComma, ",");
        Add(Key.OemPeriod, ".");
        Add(Key.OemQuestion, "/");
        Add(Key.OemTilde, "`");

        return items;
    }
}
