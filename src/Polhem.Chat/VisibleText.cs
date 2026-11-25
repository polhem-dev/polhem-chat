using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// Text from the model shown so that what the user reads is what a tool receives: characters that draw nothing or
/// reorder the text around them show as escapes. A view shows tool arguments, tool results and file names through it.
/// </summary>
public static class VisibleText
{
    /// <summary>
    /// Replaces control characters other than line feed and tab, format characters (zero-width characters, the
    /// bidirectional overrides and isolates, tags) and line and paragraph separators with <c>\u{XXXX}</c>.
    /// </summary>
    /// <remarks>
    /// WARNING: A bidirectional override makes the text read differently from the characters it holds, and a zero-width
    /// character hides in it (the Trojan Source attacks). A user who approves a call must see every character the tool
    /// will receive, so none of these may be drawn as they are.
    /// </remarks>
    /// <param name="text">The text to show.</param>
    /// <returns>The text with those characters escaped; an empty string for <see langword="null"/>.</returns>
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text) || !NeedsEscape(text))
        {
            return text ?? string.Empty;
        }
        var builder = new StringBuilder(text.Length + 16);
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsHidden(rune))
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\u{{{rune.Value:X4}}}");
            }
            else
            {
                builder.Append(rune.ToString());
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Formats the arguments of a tool call as indented JSON, with every value in full and the text escaped as by
    /// <see cref="Escape"/>. Letters of any script show as they are.
    /// </summary>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The JSON text; an empty string when there are no arguments.</returns>
    public static string FormatArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return string.Empty;
        }
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            // The text is shown, not parsed: the default encoder would turn every non-ASCII letter into an escape, and
            // Escape below makes the characters that matter visible instead.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in arguments)
            {
                writer.WritePropertyName(name);
                WriteValue(writer, value);
            }
            writer.WriteEndObject();
        }
        return Escape(Encoding.UTF8.GetString(stream.ToArray()));
    }

    /// <summary>
    /// Lists the arguments of a tool call as one <c>name: value</c> line each, escaped as by <see cref="Escape"/>.
    /// </summary>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The lines; an empty string when there are no arguments.</returns>
    public static string ListArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return string.Empty;
        }
        var text = new StringBuilder();
        foreach (var (name, value) in arguments)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }
            text.Append(Escape(name)).Append(": ").Append(FormatValue(value));
        }
        return text.ToString();
    }

    /// <summary>
    /// Formats a tool argument or result as text, escaped as by <see cref="Escape"/>. A JSON element, which is what most
    /// providers return, formats as its JSON text.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text; an empty string for <see langword="null"/>.</returns>
    public static string FormatValue(object? value) => Escape(Convert.ToString(value, CultureInfo.InvariantCulture));

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case JsonElement element:
                element.WriteTo(writer);
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case bool flag:
                writer.WriteBooleanValue(flag);
                return;
        }
        // Values an application put in the arguments itself: written as JSON when the contracts of
        // Microsoft.Extensions.AI know their type, which keeps this free of reflection, and as text otherwise.
        try
        {
            JsonSerializer.SerializeToElement(value, AIJsonUtilities.DefaultOptions.GetTypeInfo(value.GetType())).WriteTo(writer);
        }
        // A value the contracts know but cannot write, such as one that refers to itself, shows as text too.
        catch (Exception exception) when (exception is NotSupportedException or JsonException)
        {
            writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
    }

    private static bool NeedsEscape(string text) => text.EnumerateRunes().Any(IsHidden);

    private static bool IsHidden(Rune rune) =>
        Rune.GetUnicodeCategory(rune) switch
        {
            UnicodeCategory.Control => rune.Value is not ('\n' or '\t'),
            UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator => true,
            _ => false,
        };
}
