using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// How a <see cref="PromptContext"/> is kept in the history: a <see cref="TextContent"/> in the prompt's message, before
/// the prompt's text, carrying the label in its additional properties. A plain value survives any serialization of the
/// history, and a history saved before context existed has none of these contents, so it loads as before.
/// </summary>
/// <remarks>
/// The history keeps the text as the host gave it. Only the request sent to the model wraps it with its label, through
/// <see cref="ToRequest"/>, and joins it with the prompt's text into one text block, which every provider accepts.
/// </remarks>
internal static class PromptContextRecord
{
    public const string LabelKey = "polhem.chat.contextLabel";

    public static TextContent Create(PromptContext context) =>
        new(context.Text)
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { [LabelKey] = context.Label },
        };

    /// <summary>Reads a context written by <see cref="Create"/>.</summary>
    /// <returns><see langword="null"/> for any other content.</returns>
    public static PromptContext? Read(AIContent content) =>
        content is TextContent text
        && text.AdditionalProperties is { } properties
        && properties.TryGetValue(LabelKey, out var value)
        && ReadString(value) is { } label
        && !string.IsNullOrWhiteSpace(label)
            ? new PromptContext(label, text.Text ?? string.Empty)
            : null;

    public static bool IsContext(AIContent content) => Read(content) is not null;

    /// <summary>Returns the context items of a message, in order.</summary>
    public static List<PromptContext> ReadAll(ChatMessage message) =>
        [.. message.Contents.Select(Read).OfType<PromptContext>()];

    /// <summary>Returns the text the user typed: the message's text without its context.</summary>
    public static string PromptText(ChatMessage message) =>
        string.Concat(message.Contents.Where(content => !IsContext(content)).OfType<TextContent>().Select(text => text.Text));

    /// <summary>
    /// Returns the message as it is sent to the model: its context wrapped with each label and joined with the prompt's
    /// text into one text block, ahead of the files; or the message itself when it has no context.
    /// </summary>
    public static ChatMessage ToRequest(ChatMessage message)
    {
        if (!message.Contents.Any(IsContext))
        {
            return message;
        }
        var builder = new StringBuilder();
        var rest = new List<AIContent>(message.Contents.Count);
        foreach (var content in message.Contents)
        {
            if (Read(content) is { } context)
            {
                Append(builder, context);
            }
            else
            {
                rest.Add(content);
            }
        }
        // The prompt's own text follows its context in the same block, so a provider that takes one text block per
        // message, or no text next to files, gets everything.
        string text;
        if (rest.FirstOrDefault() is TextContent prompt)
        {
            text = builder.Append(prompt.Text).ToString();
            rest.RemoveAt(0);
        }
        else
        {
            text = builder.ToString().TrimEnd('\n');
        }
        var contents = new List<AIContent>(rest.Count + 1) { new TextContent(text) };
        contents.AddRange(rest);
        return new ChatMessage(message.Role, contents)
        {
            AuthorName = message.AuthorName,
            CreatedAt = message.CreatedAt,
            MessageId = message.MessageId,
            AdditionalProperties = message.AdditionalProperties,
        };
    }

    private static void Append(StringBuilder builder, PromptContext context)
    {
        builder.Append("<context label=\"").Append(EscapeAttribute(context.Label)).Append("\">\n");
        builder.Append(context.Text);
        if (!context.Text.EndsWith('\n'))
        {
            builder.Append('\n');
        }
        builder.Append("</context>\n\n");
    }

    private static string EscapeAttribute(string label) =>
        label.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    // After a round trip through JSON the value comes back as a JsonElement rather than as the string written.
    private static string? ReadString(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };
}
