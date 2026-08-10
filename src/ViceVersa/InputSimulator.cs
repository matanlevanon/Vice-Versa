namespace ViceVersa;

/// <summary>Synthesises keystrokes with SendInput.</summary>
internal static class InputSimulator
{
    private static readonly int InputSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>();

    /// <summary>
    /// Releases every modifier the user is still physically holding. The hotkey
    /// fires while its own modifiers are down; sending Ctrl+C on top of a held
    /// Shift would produce Ctrl+Shift+C in the target app.
    /// </summary>
    public static void ReleaseHeldModifiers()
    {
        ushort[] modifiers =
        {
            Native.VK_LSHIFT, Native.VK_RSHIFT,
            Native.VK_LCONTROL, Native.VK_RCONTROL,
            Native.VK_LMENU, Native.VK_RMENU,
            Native.VK_LWIN, Native.VK_RWIN
        };

        var pending = new List<Native.INPUT>(modifiers.Length);

        foreach (ushort vk in modifiers)
        {
            if ((Native.GetAsyncKeyState(vk) & 0x8000) != 0)
            {
                pending.Add(KeyUp(vk));
            }
        }

        if (pending.Count > 0)
        {
            Send(pending.ToArray());
        }
    }

    /// <summary>Sends Ctrl plus a single character key, for example Ctrl+C.</summary>
    public static void SendControlKey(char key)
    {
        ushort vk = (ushort)char.ToUpperInvariant(key);

        Send(new[]
        {
            KeyDown(Native.VK_CONTROL),
            KeyDown(vk),
            KeyUp(vk),
            KeyUp(Native.VK_CONTROL)
        });
    }

    private static Native.INPUT KeyDown(ushort vk) => Build(vk, 0);

    private static Native.INPUT KeyUp(ushort vk) => Build(vk, Native.KEYEVENTF_KEYUP);

    private static Native.INPUT Build(ushort vk, uint flags)
    {
        var input = new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD
        };

        input.u.ki = new Native.KEYBDINPUT
        {
            wVk = vk,
            wScan = 0,
            dwFlags = flags,
            time = 0,
            dwExtraInfo = IntPtr.Zero
        };

        return input;
    }

    private static void Send(Native.INPUT[] inputs)
    {
        Native.SendInput((uint)inputs.Length, inputs, InputSize);
    }
}
