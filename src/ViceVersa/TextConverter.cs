using System.Text;

namespace ViceVersa;

public enum ConversionDirection
{
    Auto,
    EnglishToHebrew,
    HebrewToEnglish
}

public enum TextScript
{
    Unknown,
    English,
    Hebrew
}

/// <summary>
/// Converts text between the US English and Israeli Hebrew keyboard layouts by
/// physical key position. This is not translation: it answers "what would this
/// text have been if I had the other layout active while typing it".
///
/// The map is generated and verified by tools/keymap_reference.py.
/// </summary>
public static class TextConverter
{
    // Physical key positions on a US QWERTY keyboard.
    private const string EnglishKeys = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`";

    // What the standard Windows Hebrew (he-IL, SI-1452) layout produces from
    // those same physical keys, in the same order.
    private const string HebrewKeys = "/'קראטוןםפ][שדגכעיחלךף,זסבהנמצתץ.;";

    private static readonly Dictionary<char, char> EnglishToHebrewMap = new();
    private static readonly Dictionary<char, char> HebrewToEnglishMap = new();

    static TextConverter()
    {
        for (int i = 0; i < EnglishKeys.Length; i++)
        {
            EnglishToHebrewMap[EnglishKeys[i]] = HebrewKeys[i];
            HebrewToEnglishMap[HebrewKeys[i]] = EnglishKeys[i];
        }

        // Hebrew has no letter case. Shift plus a letter still refers to the same
        // physical key, so uppercase Latin letters map to the same Hebrew letter.
        // The reverse direction always produces lowercase.
        foreach (char c in EnglishKeys)
        {
            if (char.IsLetter(c))
            {
                EnglishToHebrewMap[char.ToUpperInvariant(c)] = EnglishToHebrewMap[c];
            }
        }
    }

    /// <summary>True if the character is a Hebrew letter (U+05D0 to U+05EA).</summary>
    public static bool IsHebrewLetter(char c) => c >= 'א' && c <= 'ת';

    public static bool ContainsHebrew(string text)
    {
        foreach (char c in text)
        {
            if (IsHebrewLetter(c))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsLatin(string text)
    {
        foreach (char c in text)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
            {
                return true;
            }
        }

        return false;
    }

    public static TextScript DetectScript(string text)
    {
        if (ContainsHebrew(text))
        {
            return TextScript.Hebrew;
        }

        return ContainsLatin(text) ? TextScript.English : TextScript.Unknown;
    }

    public static string ToHebrew(string text) => Map(text, EnglishToHebrewMap);

    public static string ToEnglish(string text) => Map(text, HebrewToEnglishMap);

    /// <summary>
    /// Converts a whole string. In Auto mode each whitespace-separated word is
    /// judged on its own, so mixed Hebrew and English text converts correctly in
    /// a single pass. Whitespace is preserved exactly.
    /// </summary>
    public static string Convert(string text, ConversionDirection direction)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        switch (direction)
        {
            case ConversionDirection.EnglishToHebrew:
                return ToHebrew(text);
            case ConversionDirection.HebrewToEnglish:
                return ToEnglish(text);
            default:
                return ConvertAuto(text);
        }
    }

    private static string ConvertAuto(string text)
    {
        var result = new StringBuilder(text.Length);
        var word = new StringBuilder(16);

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                FlushWord(word, result);
                result.Append(c);
            }
            else
            {
                word.Append(c);
            }
        }

        FlushWord(word, result);
        return result.ToString();
    }

    private static void FlushWord(StringBuilder word, StringBuilder result)
    {
        if (word.Length == 0)
        {
            return;
        }

        string chunk = word.ToString();
        result.Append(ContainsHebrew(chunk) ? ToEnglish(chunk) : ToHebrew(chunk));
        word.Clear();
    }

    private static string Map(string text, Dictionary<char, char> map)
    {
        var sb = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            sb.Append(map.TryGetValue(c, out char mapped) ? mapped : c);
        }

        return sb.ToString();
    }
}
