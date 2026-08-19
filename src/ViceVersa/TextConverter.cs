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

    /// <summary>True if the character is an unaccented Latin letter.</summary>
    public static bool IsLatinLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

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
            if (IsLatinLetter(c))
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
    /// <param name="smartCase">
    /// Applies to Auto mode only. Caps Lock on the Windows Hebrew layout emits
    /// Latin uppercase instead of Hebrew, so an ALL-CAPS word inside a selection
    /// that also contains Hebrew is already what the user meant and is left alone,
    /// while a lone capital stuck to Hebrew text is folded to lowercase. Forced
    /// directions stay literal.
    /// </param>
    public static string Convert(string text, ConversionDirection direction, bool smartCase = true)
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
                return ConvertAuto(text, smartCase);
        }
    }

    private static string ConvertAuto(string text, bool smartCase)
    {
        var result = new StringBuilder(text.Length);
        var word = new StringBuilder(16);

        // Whether the selection as a whole shows any Hebrew. A word is judged
        // against this, not only against itself.
        bool textHasHebrew = ContainsHebrew(text);

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                FlushWord(word, result, smartCase, textHasHebrew);
                result.Append(c);
            }
            else
            {
                word.Append(c);
            }
        }

        FlushWord(word, result, smartCase, textHasHebrew);
        return result.ToString();
    }

    private static void FlushWord(StringBuilder word, StringBuilder result, bool smartCase, bool textHasHebrew)
    {
        if (word.Length == 0)
        {
            return;
        }

        result.Append(ConvertWord(word.ToString(), smartCase, textHasHebrew));
        word.Clear();
    }

    /// <summary>Converts one whitespace-free token.</summary>
    private static string ConvertWord(string word, bool smartCase, bool textHasHebrew)
    {
        bool hasHebrew = ContainsHebrew(word);
        bool hasLatin = ContainsLatin(word);

        // Digits, punctuation and symbols on their own carry no layout evidence.
        if (!hasHebrew && !hasLatin)
        {
            return word;
        }

        return hasHebrew
            ? ConvertHebrewWord(word, smartCase)
            : ConvertEnglishWord(word, smartCase, textHasHebrew);
    }

    /// <summary>
    /// Converts a word typed on the Hebrew layout back to English. Latin letters
    /// inside such a word came from Caps Lock rather than from the Hebrew map, so
    /// they are left as they are. The one exception is a run of a single letter,
    /// a stray Caps Lock capital, which is folded to lowercase to match the rest
    /// of the word. Longer runs keep their case, so brand names glued to a Hebrew
    /// prefix survive.
    /// </summary>
    private static string ConvertHebrewWord(string word, bool smartCase)
    {
        if (!smartCase)
        {
            return ToEnglish(word);
        }

        var result = new StringBuilder(word.Length);
        int i = 0;

        while (i < word.Length)
        {
            if (IsLatinLetter(word[i]))
            {
                int start = i;

                while (i < word.Length && IsLatinLetter(word[i]))
                {
                    i++;
                }

                string run = word[start..i];
                result.Append(run.Length == 1 ? run.ToLowerInvariant() : run);
            }
            else
            {
                result.Append(HebrewToEnglishMap.TryGetValue(word[i], out char mapped) ? mapped : word[i]);
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts a word typed on the English layout to Hebrew. An all-uppercase
    /// word is left alone, but only when the selection also contains Hebrew.
    /// Without that evidence the selection is more likely to be Hebrew typed on
    /// the English layout with Caps Lock on, where AKUO really does mean shalom
    /// and still has to convert.
    /// </summary>
    private static string ConvertEnglishWord(string word, bool smartCase, bool textHasHebrew)
    {
        if (smartCase && textHasHebrew && IsAllUppercaseLatin(word))
        {
            return word;
        }

        return ToHebrew(word);
    }

    /// <summary>
    /// True when every Latin letter in the word is uppercase and there is at
    /// least one. A single letter counts. The caller only asks this question
    /// about a selection that already contains Hebrew, and in that selection a
    /// lone Latin capital is Caps Lock output the user meant to keep, such as
    /// the I in "I need API help".
    /// </summary>
    private static bool IsAllUppercaseLatin(string word)
    {
        int letters = 0;

        foreach (char c in word)
        {
            if (!IsLatinLetter(c))
            {
                continue;
            }

            if (!char.IsUpper(c))
            {
                return false;
            }

            letters++;
        }

        return letters >= 1;
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
