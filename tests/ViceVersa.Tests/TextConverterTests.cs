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

    // ---------------------------------------------------------------- smart case
    //
    // Caps Lock on the Windows Hebrew layout emits Latin uppercase instead of
    // Hebrew. So an ALL-CAPS word inside a selection that also holds Hebrew is
    // what the user typed on purpose, and a lone capital glued to Hebrew text is
    // Caps Lock noise.

    [Fact]
    public void AcronymSurvivesAndTheStrayCapitalIsFolded()
    {
        // Typed as "API services" with the Hebrew layout on and Caps Lock stuck.
        Assert.Equal("API services", TextConverter.Convert("API S\u05e7\u05e8\u05d4\u05df\u05d1\u05e7\u05d3", ConversionDirection.Auto));
    }

    [Theory]
    [InlineData("API \u05e9\u05dc\u05d5\u05dd", "API akuo")]
    [InlineData("MTN2 \u05e9\u05dc\u05d5\u05dd", "MTN2 akuo")]
    [InlineData("SQL, \u05e9\u05dc\u05d5\u05dd", "SQL, akuo")]
    public void AllUppercaseWordsSurviveWhenHebrewIsPresent(string input, string expected)
    {
        Assert.Equal(expected, TextConverter.Convert(input, ConversionDirection.Auto));
    }

    [Theory]
    [InlineData("AKUO", "\u05e9\u05dc\u05d5\u05dd")]
    [InlineData("AKUO CUER YUC", "\u05e9\u05dc\u05d5\u05dd \u05d1\u05d5\u05e7\u05e8 \u05d8\u05d5\u05d1")]
    [InlineData("API", "\u05e9\u05e4\u05df")]
    public void AllUppercaseStillConvertsWithoutHebrewEvidence(string input, string expected)
    {
        // Hebrew typed on the English layout with Caps Lock on. Nothing in the
        // selection is Hebrew, so the acronym guard must stay out of the way.
        Assert.Equal(expected, TextConverter.Convert(input, ConversionDirection.Auto));
    }

    [Fact]
    public void AcronymGluedToHebrewKeepsItsCase()
    {
        Assert.Equal("APIervices", TextConverter.Convert("API\u05e7\u05e8\u05d4\u05df\u05d1\u05e7\u05d3", ConversionDirection.Auto));
    }

    [Theory]
    [InlineData("\u05d1-Zoom", "c-Zoom")]
    [InlineData("Gmail\u05e9\u05dc\u05d9", "Gmailakh")]
    public void BrandNamesGluedToHebrewKeepTheirCase(string input, string expected)
    {
        // Only a run of exactly one Latin letter is treated as a stray capital.
        Assert.Equal(expected, TextConverter.Convert(input, ConversionDirection.Auto));
    }

    [Fact]
    public void ASingleCapitalWithoutHebrewEvidenceStillConverts()
    {
        // Nothing in the selection is Hebrew, so "A" is read as Hebrew typed on
        // the English layout with Caps Lock on.
        Assert.Equal("\u05e9", TextConverter.Convert("A", ConversionDirection.Auto));
    }

    [Theory]
    [InlineData("I \u05de\u05e7\u05e7\u05d2 API \u05d9\u05e7\u05da\u05e4", "I need API help")]
    [InlineData("A DB /\u05d5\u05e7\u05e8\u05d8", "A DB query")]
    [InlineData("OK I \u05e9\u05e2\u05e8\u05e7\u05e7", "OK I agree")]
    public void ALoneCapitalNextToHebrewSurvives(string input, string expected)
    {
        // Caps Lock on the Hebrew layout emits Latin uppercase, so a one letter
        // word such as "I" or "A" is already the character the user wanted. It
        // survives only when the selection also holds Hebrew, which is the same
        // evidence the acronym rule uses. Without that evidence the test above
        // applies instead.
        Assert.Equal(expected, TextConverter.Convert(input, ConversionDirection.Auto));
    }

    [Fact]
    public void TitleCaseWordsStillConvert()
    {
        Assert.Equal(
            TextConverter.Convert("hello", ConversionDirection.Auto),
            TextConverter.Convert("Hello", ConversionDirection.Auto));
    }

    [Fact]
    public void SymbolOnlyTokensAreLeftAlone()
    {
        Assert.Equal("...", TextConverter.Convert("...", ConversionDirection.Auto));
        Assert.Equal("!?", TextConverter.Convert("!?", ConversionDirection.Auto));
    }

    [Fact]
    public void SmartCaseOffRestoresTheOldBehaviour()
    {
        Assert.Equal(
            "\u05e9\u05e4\u05df Services",
            TextConverter.Convert("API S\u05e7\u05e8\u05d4\u05df\u05d1\u05e7\u05d3", ConversionDirection.Auto, smartCase: false));
    }

    [Fact]
    public void ForcedDirectionsIgnoreSmartCase()
    {
        // A forced direction is a literal instruction, so the acronym converts.
        Assert.Equal("\u05e9\u05e4\u05df", TextConverter.Convert("API", ConversionDirection.EnglishToHebrew));
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
        Assert.True(settings.SmartCase);
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

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.0", "1.2.0")]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData("V1.2.0", "1.2.0")]
    [InlineData("  v1.2.0  ", "1.2.0")]
    [InlineData("v1.2.0-beta.1", "1.2.0")]
    [InlineData("v2.0", "2.0.0")]
    public void ParsesReleaseTags(string tag, string expected)
    {
        Assert.True(UpdateChecker.TryParseTag(tag, out Version parsed));
        Assert.Equal(expected, parsed.ToString(3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("latest")]
    [InlineData("v")]
    public void RejectsTagsWithoutAVersion(string? tag)
    {
        Assert.False(UpdateChecker.TryParseTag(tag, out _));
    }

    [Fact]
    public void ATagAboveTheCurrentVersionCountsAsNewer()
    {
        Assert.True(UpdateChecker.IsNewer("v1.0.1", new Version(1, 0, 0)));
        Assert.True(UpdateChecker.IsNewer("v1.1.0", new Version(1, 0, 9)));
        Assert.True(UpdateChecker.IsNewer("v2.0.0", new Version(1, 9, 9)));
    }

    [Fact]
    public void TheSameOrAnOlderTagIsNotNewer()
    {
        Assert.False(UpdateChecker.IsNewer("v1.0.0", new Version(1, 0, 0)));
        Assert.False(UpdateChecker.IsNewer("v1.0.0", new Version(1, 0, 1)));
        Assert.False(UpdateChecker.IsNewer("v0.9.0", new Version(1, 0, 0)));
    }

    [Fact]
    public void AFourPartAssemblyVersionDoesNotLookOlderThanItsOwnTag()
    {
        // The build stamps the assembly as 1.0.0.0 while the tag reads v1.0.0.
        // Comparing them raw reports an update forever, because an absent
        // revision sorts below a zero one.
        Assert.False(UpdateChecker.IsNewer("v1.0.0", new Version(1, 0, 0, 0)));
    }

    [Fact]
    public void GarbageTagsNeverCountAsNewer()
    {
        Assert.False(UpdateChecker.IsNewer("nightly", new Version(1, 0, 0)));
        Assert.False(UpdateChecker.IsNewer(null, new Version(1, 0, 0)));
    }
}
