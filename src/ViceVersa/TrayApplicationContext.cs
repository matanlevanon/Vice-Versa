using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ViceVersa;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly HotkeyManager _hotkeys = new();
    private readonly NotifyIcon _tray;
    private readonly Icon _icon;

    private ToolStripMenuItem _startWithWindowsItem = null!;
    private bool _busy;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        _icon = LoadIcon();

        _tray = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _tray.DoubleClick += (_, _) => ShowSettings();

        _hotkeys.Pressed += OnHotkeyPressed;
        ApplyHotkey(showErrors: true);
        UpdateTooltip();
    }

    // ------------------------------------------------------------------ wiring

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var convertItem = new ToolStripMenuItem("Convert selection now");
        convertItem.Click += (_, _) => OnHotkeyPressed(this, EventArgs.Empty);

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => ShowSettings();

        _startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = AppSettings.StartsWithWindows
        };
        _startWithWindowsItem.Click += (_, _) => AppSettings.SetStartWithWindows(_startWithWindowsItem.Checked);

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => ShowAbout();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(convertItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(_startWithWindowsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(aboutItem);
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ApplyHotkey(bool showErrors)
    {
        Hotkey hotkey = _settings.ParsedHotkey;

        if (!_hotkeys.Register(hotkey) && showErrors)
        {
            MessageBox.Show(
                $"Could not register {hotkey}. Another application is probably using it.\n\n" +
                "Open Settings from the tray icon and pick a different combination.",
                "Vice Versa",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateTooltip()
    {
        string text = $"Vice Versa  ({_settings.ParsedHotkey})";
        _tray.Text = text.Length > 63 ? text[..63] : text;
    }

    // -------------------------------------------------------------- conversion

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            await ConvertSelectionAsync();
        }
        catch (Exception ex)
        {
            Notify("Vice Versa", "Conversion failed: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ConvertSelectionAsync()
    {
        IntPtr target = Native.GetForegroundWindow();

        // The hotkey's own modifiers are still physically down at this point.
        InputSimulator.ReleaseHeldModifiers();
        await Task.Delay(40);

        ClipboardService.Snapshot original = ClipboardService.Capture();

        string text = await CopySelectionAsync();

        // Retry on empty text, not only on an unchanged clipboard. Some apps answer
        // Ctrl+C with an empty selection by writing an empty payload, which moves the
        // clipboard sequence number without producing anything to convert. Gating the
        // retry on the sequence number alone skipped Ctrl+A in exactly those apps.
        if (string.IsNullOrEmpty(text) && _settings.AutoSelectAll)
        {
            InputSimulator.SendControlKey('A');
            await Task.Delay(_settings.SelectAllSettleMs);

            text = await CopySelectionAsync();
        }

        if (string.IsNullOrEmpty(text))
        {
            RestoreClipboard(original);
            Notify("Vice Versa", "Nothing to convert. Select some text and try again.");
            return;
        }

        string converted = TextConverter.Convert(text, _settings.EffectiveDirection);

        if (string.Equals(converted, text, StringComparison.Ordinal))
        {
            RestoreClipboard(original);
            Notify("Vice Versa", "That text has nothing to convert.");
            return;
        }

        ClipboardService.SetText(converted);
        await Task.Delay(60);
        InputSimulator.SendControlKey('V');
        await Task.Delay(_settings.PasteSettleMs);

        RestoreClipboard(original);

        if (_settings.SwitchKeyboardLayout)
        {
            InputLanguageSwitcher.SwitchTo(target, TextConverter.DetectScript(converted));
        }
    }

    /// <summary>
    /// Sends Ctrl+C and returns whatever landed on the clipboard, or an empty
    /// string when nothing did. The sequence number tells us the clipboard moved.
    /// The text tells us the move was worth something. Both have to hold.
    /// </summary>
    private async Task<string> CopySelectionAsync()
    {
        uint sequence = ClipboardService.SequenceNumber;
        InputSimulator.SendControlKey('C');

        bool changed = await ClipboardService.WaitForChangeAsync(sequence, _settings.ClipboardTimeoutMs);

        return changed ? ClipboardService.GetText() : string.Empty;
    }

    private void RestoreClipboard(ClipboardService.Snapshot original)
    {
        if (_settings.RestoreClipboard)
        {
            ClipboardService.Restore(original);
        }
    }

    private void Notify(string title, string message)
    {
        if (!_settings.ShowNotifications)
        {
            return;
        }

        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = message;
        _tray.BalloonTipIcon = ToolTipIcon.Info;
        _tray.ShowBalloonTip(2500);
    }

    // ------------------------------------------------------------------- forms

    private void ShowSettings()
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Activate();
            return;
        }

        _hotkeys.Unregister();

        _settingsForm = new SettingsForm(_settings, _icon);
        DialogResult result = _settingsForm.ShowDialog();
        _settingsForm.Dispose();
        _settingsForm = null;

        ApplyHotkey(showErrors: result == DialogResult.OK);
        _startWithWindowsItem.Checked = AppSettings.StartsWithWindows;
        UpdateTooltip();
    }

    private void ShowAbout()
    {
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        MessageBox.Show(
            "Vice Versa " + version + "\n\n" +
            "Converts text between Hebrew and English by keyboard position.\n" +
            "Press " + _settings.ParsedHotkey + " to convert the selection.\n\n" +
            (AppSettings.IsPortable ? "Running in portable mode.\n" : string.Empty) +
            "Settings file: " + AppSettings.SettingsPath,
            "About Vice Versa",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ExitApplication()
    {
        _tray.Visible = false;
        ExitThread();
    }

    // ------------------------------------------------------------------- setup

    private static Icon LoadIcon()
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("ViceVersa.app.ico");

            if (stream is not null)
            {
                return new Icon(stream);
            }
        }
        catch (Exception)
        {
            // Fall through to the system icon.
        }

        return SystemIcons.Application;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeys.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
        }

        base.Dispose(disposing);
    }
}
