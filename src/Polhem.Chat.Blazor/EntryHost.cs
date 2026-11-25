using PropertyChangedEventArgs = System.ComponentModel.PropertyChangedEventArgs;
using Microsoft.AspNetCore.Components;

namespace Polhem.Chat.Blazor;

/// <summary>
/// Shows one entry of the conversation with the view's template, and renders again only when that entry changes or the
/// view asks for it with a new <see cref="Revision"/>.
/// </summary>
/// <remarks>
/// NOTE: The view renders its list whenever an entry is added; without this, every entry would render with it, and every
/// earlier reply would parse its Markdown again with each token of the newest.
/// </remarks>
internal sealed class EntryHost : IComponent, IDisposable
{
    private RenderHandle _handle;
    private RenderThrottle? _throttle;
    private ChatEntry? _watched;
    private RenderFragment<ChatEntry>? _template;
    private int _revision = -1;
    private bool _canRewind;
    private bool _disposed;

    /// <summary>Gets or sets the entry to show.</summary>
    [Parameter, EditorRequired]
    public ChatEntry Entry { get; set; } = null!;

    /// <summary>Gets or sets the template that draws an entry; the view passes the same instance every time.</summary>
    [Parameter, EditorRequired]
    public RenderFragment<ChatEntry> Template { get; set; } = null!;

    /// <summary>Gets or sets a number the view changes when the template draws differently, such as with new texts.</summary>
    [Parameter]
    public int Revision { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry shows the button that sends its prompt again or changes it, which
    /// the template reads from the view.
    /// </summary>
    [Parameter]
    public bool CanRewind { get; set; }

    public void Attach(RenderHandle renderHandle)
    {
        _handle = renderHandle;
        _throttle = new RenderThrottle(work => renderHandle.Dispatcher.InvokeAsync(work), Render);
    }

    public Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        var changed = !ReferenceEquals(Entry, _watched) || !ReferenceEquals(Template, _template) || Revision != _revision
            || CanRewind != _canRewind;
        if (!ReferenceEquals(Entry, _watched))
        {
            if (_watched is not null)
            {
                _watched.PropertyChanged -= OnEntryChanged;
            }
            _watched = Entry;
            _watched.PropertyChanged += OnEntryChanged;
        }
        _template = Template;
        _revision = Revision;
        _canRewind = CanRewind;
        if (changed)
        {
            Render();
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _disposed = true;
        _throttle?.Stop();
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnEntryChanged;
        }
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e) => _throttle?.Request();

    private void Render()
    {
        if (!_disposed && _template is not null && _watched is not null)
        {
            _handle.Render(_template(_watched));
        }
    }
}
