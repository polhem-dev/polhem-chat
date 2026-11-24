using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>
/// How a turn's summary is kept in the history: a <see cref="UsageContent"/> at the end of the turn's last assistant
/// message, carrying the elapsed time and the outcome in its additional properties. Plain values survive any
/// serialization of the history, and the content is removed before the history is sent to the model.
/// </summary>
internal static class TurnRecord
{
    public const string ElapsedKey = "polhem.chat.elapsedMilliseconds";
    public const string OutcomeKey = "polhem.chat.outcome";

    public static UsageContent Create(TurnSummaryEntry summary) =>
        new(summary.Usage ?? new UsageDetails())
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [ElapsedKey] = (long)summary.Elapsed.TotalMilliseconds,
                [OutcomeKey] = summary.Outcome.ToString(),
            },
        };

    /// <summary>Reads a summary written by <see cref="Create"/>.</summary>
    /// <returns><see langword="null"/> for a <see cref="UsageContent"/> this package did not write.</returns>
    public static TurnSummaryEntry? Read(UsageContent content)
    {
        var properties = content.AdditionalProperties;
        if (properties is null
            || !properties.TryGetValue(ElapsedKey, out var elapsedValue)
            || ReadInt64(elapsedValue) is not { } elapsed)
        {
            return null;
        }
        var outcome = properties.TryGetValue(OutcomeKey, out var outcomeValue)
            && Enum.TryParse<TurnOutcome>(ReadString(outcomeValue), ignoreCase: false, out var parsed)
                ? parsed
                : TurnOutcome.Completed;
        var usage = content.Details;
        var reported = usage.InputTokenCount is not null || usage.OutputTokenCount is not null
            || usage.TotalTokenCount is not null;
        return new TurnSummaryEntry(TimeSpan.FromMilliseconds(elapsed), reported ? usage : null, outcome);
    }

    // After a round trip through JSON the values come back as JsonElement rather than as the types written.
    private static long? ReadInt64(object? value) => value switch
    {
        long number => number,
        int number => number,
        double number => (long)number,
        string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
        JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var number) => number,
        _ => null,
    };

    private static string? ReadString(object? value) => value switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };
}
