using System.Drawing;
using System.Windows.Forms;

namespace ViceVersa;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;

    private readonly HotkeyTextBox _hotkeyBox = new();
    private readonly ComboBox _directionBox = new();
    private readonly CheckBox _autoSelectBox = new();
    private readonly CheckBox _smartCaseBox = new();
    private readonly CheckBox _switchLayoutBox = new();
    private readonly CheckBox _restoreClipboardBox = new();
    private readonly CheckBox _notificationsBox = new();
    private readonly CheckBox _startWithWindowsBox = new();
    private readonly Label _statusLabel = new();

    public SettingsForm(AppSettings settings, Icon? icon)
    {
        _settings = settings;

        Text = "Vice Versa settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 392);
        Font = new Font("Segoe UI", 9F);
        ShowInTaskbar = true;

        if (icon is not null)
        {
            Icon = icon;
        }

        BuildLayout();
        LoadFromSettings();
    }

    public bool StartWithWindowsChanged { get; private set; }

    private void BuildLayout()
    {
        int y = 16;
        const int left = 18;
        const int width = 404;

        var hotkeyLabel = new Label
        {
            Text = "Hotkey (click the box, then press the combination)",
            Location = new Point(left, y),
            AutoSize = true
        };
        Controls.Add(hotkeyLabel);
        y += 22;

        _hotkeyBox.Location = new Point(left, y);
        _hotkeyBox.Width = width;
        Controls.Add(_hotkeyBox);
        y += 34;

        var directionLabel = new Label
        {
            Text = "Direction",
            Location = new Point(left, y),
            AutoSize = true
        };
        Controls.Add(directionLabel);
        y += 22;

        _directionBox.Location = new Point(left, y);
        _directionBox.Width = width;
        _directionBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _directionBox.Items.AddRange(new object[]
        {
            "Auto detect (per word)",
            "English keys to Hebrew",
            "Hebrew keys to English"
        });
        Controls.Add(_directionBox);
        y += 36;

        AddCheckBox(_autoSelectBox, "Select the whole field when nothing is selected", left, ref y, width);
        AddCheckBox(_smartCaseBox, "Keep ALL-CAPS words as English (auto mode)", left, ref y, width);
        AddCheckBox(_switchLayoutBox, "Switch the Windows keyboard language after converting", left, ref y, width);
        AddCheckBox(_restoreClipboardBox, "Restore the previous clipboard contents", left, ref y, width);
        AddCheckBox(_notificationsBox, "Show a notification when nothing could be converted", left, ref y, width);
        AddCheckBox(_startWithWindowsBox, "Start Vice Versa when Windows starts", left, ref y, width);

        y += 8;
        _statusLabel.Location = new Point(left, y);
        _statusLabel.Size = new Size(width, 34);
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Text = AppSettings.IsPortable
            ? "Portable mode. Settings are stored next to the executable."
            : "Settings are stored in your AppData folder.";
        Controls.Add(_statusLabel);

        var okButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(ClientSize.Width - 194, ClientSize.Height - 40),
            Size = new Size(85, 28)
        };
        okButton.Click += OnSave;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(ClientSize.Width - 103, ClientSize.Height - 40),
            Size = new Size(85, 28)
        };

        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void AddCheckBox(CheckBox box, string text, int left, ref int y, int width)
    {
        box.Text = text;
        box.Location = new Point(left, y);
        box.Size = new Size(width, 22);
        Controls.Add(box);
        y += 26;
    }

    private void LoadFromSettings()
    {
        _hotkeyBox.Value = _settings.ParsedHotkey;

        _directionBox.SelectedIndex = _settings.EffectiveDirection switch
        {
            ConversionDirection.EnglishToHebrew => 1,
            ConversionDirection.HebrewToEnglish => 2,
            _ => 0
        };

        _autoSelectBox.Checked = _settings.AutoSelectAll;
        _smartCaseBox.Checked = _settings.SmartCase;
        _switchLayoutBox.Checked = _settings.SwitchKeyboardLayout;
        _restoreClipboardBox.Checked = _settings.RestoreClipboard;
        _notificationsBox.Checked = _settings.ShowNotifications;
        _startWithWindowsBox.Checked = AppSettings.StartsWithWindows;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _settings.HotkeyText = _hotkeyBox.Value.ToString();

        _settings.Direction = _directionBox.SelectedIndex switch
        {
            1 => ConversionDirection.EnglishToHebrew.ToString(),
            2 => ConversionDirection.HebrewToEnglish.ToString(),
            _ => ConversionDirection.Auto.ToString()
        };

        _settings.AutoSelectAll = _autoSelectBox.Checked;
        _settings.SmartCase = _smartCaseBox.Checked;
        _settings.SwitchKeyboardLayout = _switchLayoutBox.Checked;
        _settings.RestoreClipboard = _restoreClipboardBox.Checked;
        _settings.ShowNotifications = _notificationsBox.Checked;

        if (_startWithWindowsBox.Checked != AppSettings.StartsWithWindows)
        {
            AppSettings.SetStartWithWindows(_startWithWindowsBox.Checked);
            StartWithWindowsChanged = true;
        }

        _settings.Save();
    }
}
