using Microsoft.Extensions.AI;

namespace ChatGallery;

/// <summary>
/// The tool the gallery asks approval for. It deletes nothing: it only says what it would have deleted. A prompt that
/// asks to delete something has the scripted model call it, with long arguments and a character that reverses the text
/// after it, so the approval card shows both.
/// </summary>
public static class DemoTools
{
    public const string DeleteNotesTool = "delete_notes";

    /// <summary>Gets the tool, wrapped so that the client asks the user before running it.</summary>
    public static AIFunction DeleteNotes { get; } = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(
        (string folder, string reason) => $"Deleted 12 notes from {folder}.",
        DeleteNotesTool,
        "Deletes the notes in a folder. Ask only when the user wants notes deleted."));

    /// <summary>Gets the options that offer the tool to the model.</summary>
    public static ChatOptions Options => new() { Tools = [DeleteNotes] };

    /// <summary>Wraps a client so that it runs the tools, asking approval where they need it.</summary>
    public static IChatClient WithTools(IChatClient client) => client.AsBuilder().UseFunctionInvocation().Build();

    public static bool AsksToDelete(string prompt) =>
        prompt.Contains("delete", StringComparison.OrdinalIgnoreCase) || prompt.Contains("刪除", StringComparison.Ordinal);

    /// <summary>The arguments the scripted model passes: long enough to scroll, with a hidden right-to-left override.</summary>
    public static Dictionary<string, object?> Arguments(bool chinese)
    {
        var lines = Enumerable.Range(1, 30).Select(line => chinese
            ? $"第 {line} 則筆記已超過一年沒有開啟。"
            : $"Note {line} has not been opened for over a year.");
        return new Dictionary<string, object?>
        {
            ["folder"] = "notes/2024/report‮fdp.exe",
            ["reason"] = string.Join("\n", lines),
        };
    }

    /// <summary>What the scripted model says once the call ran or was rejected.</summary>
    public static string After(FunctionResultContent result, bool chinese)
    {
        var text = result.Result?.ToString() ?? string.Empty;
        var rejected = text.Contains("rejected", StringComparison.OrdinalIgnoreCase);
        return (rejected, chinese) switch
        {
            (true, true) => "好的，筆記都保留著，沒有刪除任何東西。",
            (true, false) => "All right, I left the notes alone. Nothing was deleted.",
            (false, true) => "已刪除。工具回報：" + text,
            (false, false) => "Done. The tool said: " + text,
        };
    }
}
