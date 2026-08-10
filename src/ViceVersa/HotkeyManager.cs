using System.Windows.Forms;

namespace ViceVersa;

/// <summary>
/// Owns a hidden message window and the system-wide hotkey registration.
/// Windows delivers WM_HOTKEY to the thread that registered it, so this must
/// live on the UI thread.
/// </summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x0B1D;

    private bool _registered;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams());
    }

    public event EventHandler? Pressed;

    public bool Register(Hotkey hotkey)
    {
        Unregister();

        if (!hotkey.IsValid)
        {
            return false;
        }

        _registered = Native.RegisterHotKey(Handle, HotkeyId, hotkey.Modifiers, hotkey.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            Native.UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();

        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }
}
