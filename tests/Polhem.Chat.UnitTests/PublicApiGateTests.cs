using System.Reflection;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>
/// No public type of the packages has the simple name of a public type in Microsoft.Extensions.AI, Avalonia, Blazor or
/// the Markdown viewers, or of a type the common .NET UI frameworks or Microsoft's own Blazor AI components define. Applications import those namespaces next to
/// ours, in exactly the file that builds a chat user interface, so a shared name means an ambiguous reference (CS0104)
/// there. Renaming a public type later is a breaking change, so the name has to be right from the start.
/// </summary>
public class PublicApiGateTests
{
    // Names that WPF, WinForms, MAUI and the Avalonia control libraries use, which no assembly loaded here declares.
    private static readonly string[] s_uiFrameworkNames =
    [
        "Binding", "Border", "Column", "Command", "Control", "Grid", "Image", "Label", "Menu", "MenuItem", "Message",
        "Page", "Panel", "Style", "Window",
    ];

    // The components of Microsoft.AspNetCore.Components.AI, which targets only .NET 11 and so cannot be loaded here. A
    // Blazor application may use it next to ours.
    private static readonly string[] s_blazorAiNames =
    [
        "AgentBoundary", "AgentContext", "BlockRenderer", "ChatPage", "ContentBlock", "MessageInput", "MessageList",
        "RichContentBlock", "UIAgent",
    ];

    private static readonly Lazy<HashSet<string>> s_takenNames = new(() =>
    {
        Assembly[] neighbours =
        [
            typeof(IChatClient).Assembly,
            typeof(global::Avalonia.AvaloniaObject).Assembly,
            typeof(global::Avalonia.Controls.Control).Assembly,
            typeof(global::Polhem.Markdown.Avalonia.MarkdownViewer).Assembly,
            typeof(global::Microsoft.AspNetCore.Components.ComponentBase).Assembly,
            typeof(global::Microsoft.AspNetCore.Components.Web.MouseEventArgs).Assembly,
            typeof(global::Polhem.Markdown.Blazor.MarkdownViewer).Assembly,
        ];
        return neighbours
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Select(SimpleName)
            .Concat(s_uiFrameworkNames)
            .Concat(s_blazorAiNames)
            .ToHashSet(StringComparer.Ordinal);
    });

    [Theory(DisplayName = "No public type of a package shares its name with a type of Microsoft.Extensions.AI or a UI framework")]
    [MemberData(nameof(Packages.Names), MemberType = typeof(Packages))]
    public void PublicTypeNames_EachPackage_DoNotCollide(string package)
    {
        var collisions = Assembly.Load(package).GetExportedTypes()
            .Where(type => s_takenNames.Value.Contains(SimpleName(type)))
            .Select(type => type.FullName!)
            .ToList();

        Assert.Empty(collisions);
    }

    private static string SimpleName(Type type)
    {
        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? type.Name : type.Name[..tick];
    }
}
