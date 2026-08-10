using System.Windows.Forms;

namespace ViceVersa;

/// <summary>
/// Clipboard access with retries. The Windows clipboard is a shared, lockable
/// resource, so every call here can legitimately fail while another process
/// holds it and must be retried rather than thrown.
/// </summary>
internal static class ClipboardService
{
    private const int RetryCount = 8;
    private const int RetryDelayMs = 25;

    public sealed class Snapshot
    {
        public bool HadText { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    public static uint SequenceNumber => Native.GetClipboardSequenceNumber();

    public static Snapshot Capture()
    {
        string text = GetText();

        return new Snapshot
        {
            HadText = !string.IsNullOrEmpty(text),
            Text = text
        };
    }

    public static void Restore(Snapshot snapshot)
    {
        if (snapshot.HadText)
        {
            SetText(snapshot.Text);
        }
        else
        {
            Retry(Clipboard.Clear);
        }
    }

    public static string GetText()
    {
        string result = string.Empty;

        Retry(() =>
        {
            result = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        });

        return result;
    }

    public static void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Retry(Clipboard.Clear);
            return;
        }

        Retry(() => Clipboard.SetText(text));
    }

    /// <summary>
    /// Waits for the clipboard sequence number to move past <paramref name="previous"/>.
    /// A copy that produced no selection leaves the number untouched, which is how
    /// the caller learns that nothing was selected.
    /// </summary>
    public static async Task<bool> WaitForChangeAsync(uint previous, int timeoutMs)
    {
        int waited = 0;

        while (waited < timeoutMs)
        {
            await Task.Delay(20).ConfigureAwait(true);
            waited += 20;

            if (SequenceNumber != previous)
            {
                // Give the owning app a moment to finish rendering the formats.
                await Task.Delay(20).ConfigureAwait(true);
                return true;
            }
        }

        return false;
    }

    private static void Retry(Action action)
    {
        for (int attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception)
            {
                if (attempt == RetryCount - 1)
                {
                    return;
                }

                Thread.Sleep(RetryDelayMs);
            }
        }
    }
}
