using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace ViceVersa;

public sealed class AppSettings
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ViceVersa";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Hotkey in text form, for example "Shift+F12" or "Ctrl+Alt+H".</summary>
    public string HotkeyText { get; set; } = Hotkey.Default.ToString();

    /// <summary>Send Ctrl+A first when nothing is selected.</summary>
    public bool AutoSelectAll { get; set; } = true;

    /// <summary>Flip the Windows input language after converting.</summary>
    public bool SwitchKeyboardLayout { get; set; } = true;

    /// <summary>Put the previous clipboard contents back when done.</summary>
    public bool RestoreClipboard { get; set; } = true;

    /// <summary>Auto, EnglishToHebrew or HebrewToEnglish.</summary>
    public string Direction { get; set; } = ConversionDirection.Auto.ToString();

    public bool ShowNotifications { get; set; } = true;

    /// <summary>How long to wait for the target app to answer Ctrl+C.</summary>
    public int ClipboardTimeoutMs { get; set; } = 600;

    /// <summary>Pause after pasting before the clipboard is restored.</summary>
    public int PasteSettleMs { get; set; } = 200;

    [JsonIgnore]
    public ConversionDirection EffectiveDirection =>
        Enum.TryParse(Direction, true, out ConversionDirection parsed) ? parsed : ConversionDirection.Auto;

    [JsonIgnore]
    public Hotkey ParsedHotkey => Hotkey.Parse(HotkeyText);

    // ----------------------------------------------------------------- storage

    public static string ApplicationDirectory
    {
        get
        {
            string? processPath = Environment.ProcessPath;
            string? directory = processPath is null ? null : Path.GetDirectoryName(processPath);
            return directory ?? AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    /// <summary>
    /// Portable mode is opt-in via a marker file next to the executable. The
    /// portable zip ships with one; the installer does not.
    /// </summary>
    public static bool IsPortable => File.Exists(Path.Combine(ApplicationDirectory, "portable.txt"));

    public static string SettingsPath
    {
        get
        {
            if (IsPortable)
            {
                return Path.Combine(ApplicationDirectory, "settings.json");
            }

            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(roaming, "ViceVersa", "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            string path = SettingsPath;

            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // A corrupt settings file should never stop the app from starting.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string path = SettingsPath;
            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // Read-only location, for example a portable copy on a locked share.
        }
    }

    // --------------------------------------------------------------- autostart

    public static bool StartsWithWindows
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(RunValueName) is not null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static void SetStartWithWindows(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);

            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                string? exe = Environment.ProcessPath;

                if (!string.IsNullOrEmpty(exe))
                {
                    key.SetValue(RunValueName, "\"" + exe + "\"");
                }
            }
            else
            {
                key.DeleteValue(RunValueName, false);
            }
        }
        catch (Exception)
        {
            // Group policy or a locked-down profile can block this.
        }
    }
}
