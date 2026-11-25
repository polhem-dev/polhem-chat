using Bunit;
using Polhem.Markdown.Blazor;

namespace Polhem.Chat.Blazor.UnitTests;

/// <summary>
/// The Markdown viewer the component builds on renders under bUnit at the lowest Blazor version the component supports.
/// </summary>
public class MarkdownViewerTests : BunitContext
{
    [Fact(DisplayName = "The Markdown viewer renders a reply's emphasis as markup")]
    public void Render_Emphasis_AsMarkup()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var viewer = Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, "Hello **world**"));

        Assert.Equal("world", viewer.Find("strong").TextContent);
    }
}
