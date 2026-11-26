namespace Polhem.Chat.UnitTests;

/// <summary>What <see cref="VisibleText.EscapeBidiControls"/> escapes in a reply, and what it leaves as it is.</summary>
public class VisibleTextTests
{
    [Theory(DisplayName = "Each bidirectional embedding, override and isolate shows as an escape")]
    [InlineData("\u202A", "\\u{202A}")]
    [InlineData("\u202B", "\\u{202B}")]
    [InlineData("\u202C", "\\u{202C}")]
    [InlineData("\u202D", "\\u{202D}")]
    [InlineData("\u202E", "\\u{202E}")]
    [InlineData("\u2066", "\\u{2066}")]
    [InlineData("\u2067", "\\u{2067}")]
    [InlineData("\u2068", "\\u{2068}")]
    [InlineData("\u2069", "\\u{2069}")]
    public void EscapeBidiControls_Control_ShowsAsEscape(string control, string escape)
    {
        Assert.Equal($"report{escape}fdp.exe", VisibleText.EscapeBidiControls($"report{control}fdp.exe"));
    }

    [Theory(DisplayName = "Right-to-left text, directional marks, joiners and the characters next to the controls stay as they are")]
    [InlineData("مرحبا بالعالم")]
    [InlineData("שלום עולם (2024)")]
    [InlineData("a\u200Eb\u200Fc\u061Cd")]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467")]
    [InlineData("می\u200Cخواهم")]
    [InlineData("\u2065\u2029\u206A")]
    public void EscapeBidiControls_OtherText_Unchanged(string text)
    {
        Assert.Same(text, VisibleText.EscapeBidiControls(text));
    }

    [Theory(DisplayName = "A numeric character reference to a bidirectional control shows as the same escape")]
    [InlineData("&#x202E;", "\\u{202E}")]
    [InlineData("&#X202e;", "\\u{202E}")]
    [InlineData("&#x000202A;", "\\u{202A}")]
    [InlineData("&#8238;", "\\u{202E}")]
    [InlineData("&#0008294;", "\\u{2066}")]
    [InlineData("&#8297;", "\\u{2069}")]
    [InlineData("&#+8238;", "\\u{202E}")]
    [InlineData("&# 8238;", "\\u{202E}")]
    [InlineData("&#8238 ;", "\\u{202E}")]
    [InlineData("&#\n8238;", "\\u{202E}")]
    [InlineData("&#x00000000000000000000000000000000000000000202E;", "\\u{202E}")]
    public void EscapeBidiControls_Reference_ShowsAsEscape(string reference, string escape)
    {
        Assert.Equal($"report{escape}fdp.exe", VisibleText.EscapeBidiControls($"report{reference}fdp.exe"));
    }

    [Theory(DisplayName = "Other references, and references without their semicolon, stay as they are")]
    [InlineData("&#x202F; &#8239; &#x2065; &#8293; &#x200F; &amp; &#x202E")]
    [InlineData("&#x2066x;")]
    [InlineData("&#x 202E; &#x+202E; &#rlo; &#;")]
    public void EscapeBidiControls_OtherReference_Unchanged(string text)
    {
        Assert.Equal(text, VisibleText.EscapeBidiControls(text));
    }

    [Fact(DisplayName = "No text escapes to an empty string")]
    public void EscapeBidiControls_Null_Empty()
    {
        Assert.Equal(string.Empty, VisibleText.EscapeBidiControls(null));
    }
}
