namespace Polhem.Chat.UnitTests;

/// <summary>
/// The packages under src, with the assemblies each one may reference besides .NET.
/// <see cref="DependencyGateTests.PackageList_ProjectsUnderSrc_AllListed"/> fails when a package under src is missing
/// here, so every gate that reads this list covers a new package too.
/// </summary>
internal static class Packages
{
    public static readonly IReadOnlyDictionary<string, string[]> AllowedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Polhem.Chat"] = ["Microsoft.Extensions.AI.Abstractions"],
        ["Polhem.Chat.Avalonia"] = ["Polhem.Chat", "Microsoft.Extensions.AI.Abstractions", "Avalonia", "Polhem.Markdown"],
        ["Polhem.Chat.Blazor"] = ["Polhem.Chat", "Microsoft.Extensions.AI.Abstractions", "Microsoft.AspNetCore.Components", "Polhem.Markdown"],
    };

    public static TheoryData<string> Names => [.. AllowedReferences.Keys];
}
