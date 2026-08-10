namespace ViceVersa;

/// <summary>
/// Changes the active Windows input language of the target window so the next
/// keystrokes land in the language the converted text is now in.
/// </summary>
internal static class InputLanguageSwitcher
{
    private const ushort LangHebrew = 0x040D;
    private const ushort LangEnglishPrimary = 0x09;

    private const string KlidHebrew = "0000040D";
    private const string KlidEnglishUs = "00000409";

    private const uint KLF_ACTIVATE = 0x00000001;

    public static void SwitchTo(IntPtr targetWindow, TextScript script)
    {
        if (script == TextScript.Unknown)
        {
            return;
        }

        IntPtr layout = FindLayout(script);

        if (layout == IntPtr.Zero)
        {
            // The layout is installed on the machine but not loaded in this
            // process's list. Load it, then try again.
            layout = Native.LoadKeyboardLayout(
                script == TextScript.Hebrew ? KlidHebrew : KlidEnglishUs,
                KLF_ACTIVATE);
        }

        if (layout == IntPtr.Zero)
        {
            return;
        }

        IntPtr window = targetWindow != IntPtr.Zero ? targetWindow : Native.GetForegroundWindow();

        if (window == IntPtr.Zero)
        {
            return;
        }

        Native.PostMessage(window, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout);

        IntPtr focused = Native.GetFocusedWindow();

        if (focused != IntPtr.Zero && focused != window)
        {
            Native.PostMessage(focused, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout);
        }
    }

    private static IntPtr FindLayout(TextScript script)
    {
        int count = Native.GetKeyboardLayoutList(0, Array.Empty<IntPtr>());

        if (count <= 0)
        {
            return IntPtr.Zero;
        }

        var layouts = new IntPtr[count];
        count = Native.GetKeyboardLayoutList(count, layouts);

        for (int i = 0; i < count; i++)
        {
            ushort langId = (ushort)(layouts[i].ToInt64() & 0xFFFF);

            if (script == TextScript.Hebrew && langId == LangHebrew)
            {
                return layouts[i];
            }

            if (script == TextScript.English && (langId & 0x3FF) == LangEnglishPrimary)
            {
                return layouts[i];
            }
        }

        return IntPtr.Zero;
    }
}
