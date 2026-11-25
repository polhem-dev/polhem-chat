using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>Reading the arguments and the result of a tool call as typed values.</summary>
public class ToolCallValueTests
{
    private static JsonTypeInfo<T> Contract<T>() => (JsonTypeInfo<T>)AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));

    [Fact(DisplayName = "An argument reads as the value it is, or from the JSON a client passed")]
    public void TryGetArgument_ReadsValuesAndJson()
    {
        var call = new ToolCallEntry("c1", "get_weather")
        {
            Arguments = new Dictionary<string, object?>
            {
                ["city"] = "Kyoto",
                ["days"] = JsonSerializer.SerializeToElement(3, Contract<int>()),
                ["note"] = JsonSerializer.SerializeToElement("rain", Contract<string>()),
            },
        };

        Assert.True(call.TryGetArgument("city", Contract<string>(), out var city));
        Assert.Equal("Kyoto", city);
        Assert.True(call.TryGetArgument("days", Contract<int>(), out var days));
        Assert.Equal(3, days);
        Assert.True(call.TryGetArgument("note", Contract<string>(), out var note));
        Assert.Equal("rain", note);
    }

    [Fact(DisplayName = "A missing argument, or one of another type, does not read")]
    public void TryGetArgument_MissingOrWrongType_ReturnsFalse()
    {
        var call = new ToolCallEntry("c1", "get_weather")
        {
            Arguments = new Dictionary<string, object?>
            {
                ["days"] = JsonSerializer.SerializeToElement("three", Contract<string>()),
                ["none"] = null,
            },
        };

        Assert.False(call.TryGetArgument("city", Contract<string>(), out _));
        Assert.False(call.TryGetArgument("days", Contract<int>(), out _));
        Assert.False(call.TryGetArgument("none", Contract<string>(), out _));
        Assert.False(new ToolCallEntry("c2", "x").TryGetArgument("city", Contract<string>(), out _));
    }

    [Fact(DisplayName = "The result reads from JSON or as the value it is, and not before it arrives")]
    public void TryGetResult_ReadsTheResult()
    {
        var call = new ToolCallEntry("c1", "get_weather");
        Assert.False(call.TryGetResult(Contract<string>(), out _));

        call.Result = JsonSerializer.SerializeToElement(new[] { 18, 16, 14 }, Contract<int[]>());
        Assert.True(call.TryGetResult(Contract<int[]>(), out var highs));
        Assert.Equal([18, 16, 14], highs);

        call.Result = "sunny";
        Assert.True(call.TryGetResult(Contract<string>(), out var text));
        Assert.Equal("sunny", text);
    }
}
