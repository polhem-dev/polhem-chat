using System.Globalization;
using Microsoft.Extensions.AI;

namespace ChatGallery;

/// <summary>The titles of the tool cards, and the two languages the gallery offers.</summary>
public static class ToolTitles
{
    public const string CurrentTimeTool = "get_current_time";

    public static CultureInfo English { get; } = CultureInfo.GetCultureInfo("en");

    public static CultureInfo Chinese { get; } = CultureInfo.GetCultureInfo("zh-Hant");

    /// <summary>The title of a tool card, which the application provides instead of the tool's name.</summary>
    public static string? Describe(FunctionCallContent call) => call.Name switch
    {
        DemoReplies.WeatherTool when call.Arguments?.TryGetValue("city", out var city) == true => "Weather forecast: " + city,
        CurrentTimeTool => "Current time",
        DemoTools.DeleteNotesTool when call.Arguments?.TryGetValue("folder", out var folder) == true => "Delete the notes in " + folder,
        _ => null,
    };

    /// <summary>Whether the chat view has texts for the culture: those of the <c>zh-Hant</c> family.</summary>
    public static bool IsTraditionalChinese(CultureInfo culture)
    {
        for (var current = culture; !Equals(current, CultureInfo.InvariantCulture); current = current.Parent)
        {
            if (current.Name.Equals(Chinese.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
