using System.Windows.Forms;

namespace ViceVersa;

internal static class Program
{
    private const string MutexName = "Global\\ViceVersa.SingleInstance.4F2A1C";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Vice Versa is already running. Look for the tray icon next to the clock.",
                "Vice Versa",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var context = new TrayApplicationContext();
        Application.Run(context);
    }
}
