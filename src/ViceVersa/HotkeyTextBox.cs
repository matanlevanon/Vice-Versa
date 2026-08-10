using System.Windows.Forms;

namespace ViceVersa;

/// <summary>A read-only text box that records the next key combination pressed into it.</summary>
internal sealed class HotkeyTextBox : TextBox
{
    private Hotkey _value = Hotkey.Default;

    public HotkeyTextBox()
    {
        ReadOnly = true;
        Cursor = Cursors.Hand;
        Text = _value.ToString();
        TextAlign = HorizontalAlignment.Center;
    }

    public Hotkey Value
    {
        get => _value;
        set
        {
            _value = value;
            Text = value.ToString();
        }
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode == Keys.Escape)
        {
            return;
        }

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None)
        {
            return;
        }

        Hotkey candidate = Hotkey.FromKeyEvent(e);

        if (candidate.IsValid)
        {
            Value = candidate;
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        BackColor = System.Drawing.SystemColors.Info;
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        BackColor = System.Drawing.SystemColors.Window;
    }
}
