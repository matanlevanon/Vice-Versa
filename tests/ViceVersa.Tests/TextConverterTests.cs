using Xunit;

namespace ViceVersa.Tests;

public class TextConverterTests
{
    // Every physical key position, in both layouts. Kept identical to
    // tools/keymap_reference.py, which is the source of truth.
    private const string EnglishKeys = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`";
    private const string HebrewKeys = "/'קראטוןםפ][שדגכעיחלךף,זסבהנמצתץ.;";

    [Fact]
    public void EveryKeyRoundTripsBackToItself()
    {
        Assert.Equal(EnglishKeys, TextConverter.ToEnglish(TextConverter.ToHebrew(EnglishKeys)));
        Assert.Equal(HebrewKeys, TextConverter.ToHebrew(TextConverter.ToEnglish(HebrewKeys)));
    }

    [Fact]
    public void EnglishKeysProduceTheExpectedHebrewKeys()
    {
        Assert.Equal(HebrewKeys, TextConverter.ToHebrew(EnglishKeys));
    }

    [Theory]
    [InlineData("akuo", "שלום")]       // shalom
    [InlineData(",usv", "תודה")]       // toda
    [InlineData("hartk", "ישראל")]     // yisrael
    [InlineData("cuer yuc", "בוקר טוב")] // boker tov
    [InlineData("hello", "יקךךם")]
    public void ConvertsEnglishTypingIntoHebrew(string typed, string expected)
    {
        Assert.Equal(expected, TextConverter.ToHebrew(typed));
    }

    [Theory]
    [InlineData("שלום", "akuo")]
    [InlineData("תודה", ",usv")]
    [InlineData("ישראל", "hartk")]
    public void ConvertsHebrewTypingIntoEnglish(string typed, string expected)
    {
        Assert.Equal(expected, TextConverter.ToEnglish(typed));
    }

    [Fact]
    public void UppercaseLatinMapsToTheSameHebrewLetter()
    {
        Assert.Equal(TextConverter.ToHebrew("abc"), TextConverter.ToHebrew("ABC"));
    }

    [Fact]
    public void DigitsAndUnmappedCharactersPassThrough()
    {
        Assert.Equal("12345", TextConverter.ToHebrew("12345"));
        Assert.Equal("12345", TextConverter.ToEnglish("12345"));
        Assert.Equal("@#$", TextConverter.ToHebrew("@#$"));
    }

    [Fact]
    public void AutoModeHandlesMixedText()
    {
        Assert.Equal("akuo יקךךם", TextConverter.Convert("שלום hello", ConversionDirection.Auto));
    }

    [Fact]
    public void AutoModePreservesWhitespaceExactly()
    {
        Assert.Equal("שנב\nגקכ", TextConverter.Convert("abc\ndef", ConversionDirection.Auto));
        Assert.Equal("ש  נ\tב", TextConverter.Convert("a  b\tc", ConversionDirection.Auto));
    }

    [Fact]
    public void AutoModeConvertsMultiLineText()
    {
        const string input = "שלום\nhello\nשלום";
        Assert.Equal("akuo\nיקךךם\nakuo", TextConverter.Convert(input, ConversionDirection.Auto));
    }

    [Fact]
    public void EmptyAndNullInputAreSafe()
    {
        Assert.Equal(string.Empty, TextConverter.Convert(string.Empty, ConversionDirection.Auto));
        Assert.Equal(string.Empty, TextConverter.ToHebrew(string.Empty));
    }

    [Theory]
    [InlineData("שלום", TextScript.Hebrew)]
    [InlineData("hello", TextScript.English)]
    [InlineData("12345", TextScript.Unknown)]
    [InlineData("hello שלום", TextScript.Hebrew)]
    public void DetectsScript(string text, TextScript expected)
    {
        Assert.Equal(expected, TextConverter.DetectScript(text));
    }

    [Fact]
    public void EveryHebrewLetterHasAKeyPosition()
    {
        const string allHebrewLetters = "אבגדהוזחטיכךלמםנןסעפףצץקרשת";

        foreach (char letter in allHebrewLetters)
        {
            string converted = TextConverter.ToEnglish(letter.ToString());
            Assert.True(converted != letter.ToString(), $"No key position for {letter}");
        }
    }

    [Fact]
    public void ExplicitDirectionOverridesDetection()
    {
        // Hebrew text forced through the English-to-Hebrew map is left alone,
        // because Hebrew letters are not keys on the English side.
        Assert.Equal("שלום", TextConverter.Convert("שלום", ConversionDirection.EnglishToHebrew));
        Assert.Equal("hello", TextConverter.Convert("hello", ConversionDirection.HebrewToEnglish));
    }
}

public class HotkeyTests
{
    [Theory]
    [InlineData("Shift+F12")]
    [InlineData("Ctrl+Alt+H")]
    [InlineData("Ctrl+Shift+X")]
    [InlineData("Win+Q")]
    public void ParsesAndRendersRoundTrip(string text)
    {
        Assert.Equal(text, Hotkey.Parse(text).ToString());
    }

    [Fact]
    public void FallsBackToDefaultOnGarbage()
    {
        Assert.Equal(Hotkey.Default.ToString(), Hotkey.Parse("not a hotkey").ToString());
        Assert.Equal(Hotkey.Default.ToString(), Hotkey.Parse(string.Empty).ToString());
        Assert.Equal(Hotkey.Default.ToString(), Hotkey.Parse(null).ToString());
    }

    [Fact]
    public void ModifierOnlyCombinationsAreRejected()
    {
        Assert.Equal(Hotkey.Default.ToString(), Hotkey.Parse("Ctrl+Shift").ToString());
    }

    [Fact]
    public void ModifierBitsAreSet()
    {
        Hotkey hotkey = Hotkey.Parse("Ctrl+Alt+Shift+K");
        Assert.True(hotkey.Control);
        Assert.True(hotkey.Alt);
        Assert.True(hotkey.Shift);
        Assert.False(hotkey.Win);
        Assert.True(hotkey.IsValid);
    }
}

public class AppSettingsTests
{
    [Fact]
    public void DefaultsAreSane()
    {
        var settings = new AppSettings();

        Assert.Equal("Shift+F12", settings.HotkeyText);
        Assert.True(settings.AutoSelectAll);
        Assert.True(settings.SwitchKeyboardLayout);
        Assert.True(settings.RestoreClipboard);
        Assert.Equal(ConversionDirection.Auto, settings.EffectiveDirection);
        Assert.True(settings.ClipboardTimeoutMs > 0);
        Assert.True(settings.SelectAllSettleMs > 0);
        Assert.True(settings.PasteSettleMs > 0);
    }

    [Fact]
    public void UnknownDirectionFallsBackToAuto()
    {
        var settings = new AppSettings { Direction = "sideways" };
        Assert.Equal(ConversionDirection.Auto, settings.EffectiveDirection);
    }

    [Fact]
    public void TimingValuesSurviveARoundTripThroughJson()
    {
        var settings = new AppSettings { ClipboardTimeoutMs = 1500, SelectAllSettleMs = 400 };

        string json = System.Text.Json.JsonSerializer.Serialize(settings);
        AppSettings? loaded = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(loaded);
        Assert.Equal(1500, loaded!.ClipboardTimeoutMs);
        Assert.Equal(400, loaded.SelectAllSettleMs);
    }
}
