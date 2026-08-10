using System.Text;
using System.Windows.Forms;

namespace ViceVersa;

/// <summary>A modifier plus key combination, parsed from and rendered to text like "Ctrl+Shift+X".</summary>
public sealed class Hotkey
{
    public bool Control { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }
    public bool Win { get; init; }
    public Keys Key { get; init; } = Keys.F12;

    public static Hotkey Default => new() { Shift = true, Key = Keys.F12 };

    public uint Modifiers
    {
        get
        {
            uint value = Native.MOD_NOREPEAT;

            if (Control)
            {
                value |= Native.MOD_CONTROL;
            }

            if (Alt)
            {
                value |= Native.MOD_ALT;
            }

            if (Shift)
            {
                value |= Native.MOD_SHIFT;
            }

            if (Win)
            {
                value |= Native.MOD_WIN;
            }

            return value;
        }
    }

    public uint VirtualKey => (uint)Key;

    public bool IsValid => Key != Keys.None
                           && Key != Keys.ControlKey
                           && Key != Keys.ShiftKey
                           && Key != Keys.Menu
                           && Key != Keys.LWin
                           && Key != Keys.RWin;

    public override string ToString()
    {
        var sb = new StringBuilder();

        if (Control)
        {
            sb.Append("Ctrl+");
        }

        if (Alt)
        {
            sb.Append("Alt+");
        }

        if (Shift)
        {
            sb.Append("Shift+");
        }

        if (Win)
        {
            sb.Append("Win+");
        }

        sb.Append(Key);
        return sb.ToString();
    }

    public static Hotkey Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default;
        }

        bool control = false;
        bool alt = false;
        bool shift = false;
        bool win = false;
        Keys key = Keys.None;

        foreach (string rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            string part = rawPart.Trim();

            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    control = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "win":
                case "windows":
                    win = true;
                    break;
                default:
                    if (Enum.TryParse(part, true, out Keys parsed))
                    {
                        key = parsed;
                    }

                    break;
            }
        }

        var hotkey = new Hotkey { Control = control, Alt = alt, Shift = shift, Win = win, Key = key };
        return hotkey.IsValid ? hotkey : Default;
    }

    public static Hotkey FromKeyEvent(KeyEventArgs e) => new()
    {
        Control = e.Control,
        Alt = e.Alt,
        Shift = e.Shift,
        Key = e.KeyCode
    };
}
