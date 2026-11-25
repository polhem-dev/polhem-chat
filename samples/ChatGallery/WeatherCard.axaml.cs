using System.Text.Json.Serialization.Metadata;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Microsoft.Extensions.AI;
using Polhem.Chat;

namespace ChatGallery;

/// <summary>
/// The gallery's own view of a weather tool call. It reads the arguments and the result with
/// <see cref="ToolCallEntry.TryGetArgument"/> and <see cref="ToolCallEntry.TryGetResult"/>, which take either the values a
/// scripted client passes or the JSON a real model's client passes.
/// </summary>
public sealed partial class WeatherCard : UserControl
{
    private static readonly JsonTypeInfo<string> s_text =
        (JsonTypeInfo<string>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(string));

    public WeatherCard() => InitializeComponent();

    /// <summary>Converts the call (and its arguments, so the text follows them) to the card's title.</summary>
    public static IMultiValueConverter Title { get; } = new FuncMultiValueConverter<object?, string>(values =>
        values.FirstOrDefault() is ToolCallEntry call && call.TryGetArgument("city", s_text, out var city)
            ? "☀ Weather in " + city
            : "☀ Weather");

    /// <summary>Converts the call (and its result, so the text follows it) to the forecast, one day a line.</summary>
    public static IMultiValueConverter Forecast { get; } = new FuncMultiValueConverter<object?, string>(values =>
        values.FirstOrDefault() is ToolCallEntry call && call.TryGetResult(s_text, out var forecast)
            ? forecast.Replace(". ", ".\n", StringComparison.Ordinal)
            : string.Empty);
}
