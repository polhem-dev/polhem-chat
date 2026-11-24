using Avalonia;
using Avalonia.Controls.Templates;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// The places an application fills with content of its own: above the conversation, and above and below the prompt
/// box. Each takes no room while it is empty.
/// </summary>
public partial class ChatView
{
    /// <summary>Defines the <see cref="HeaderContent"/> property.</summary>
    public static readonly StyledProperty<object?> HeaderContentProperty =
        AvaloniaProperty.Register<ChatView, object?>(nameof(HeaderContent));

    /// <summary>Defines the <see cref="HeaderContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderContentTemplateProperty =
        AvaloniaProperty.Register<ChatView, IDataTemplate?>(nameof(HeaderContentTemplate));

    /// <summary>Defines the <see cref="PromptHeaderContent"/> property.</summary>
    public static readonly StyledProperty<object?> PromptHeaderContentProperty =
        AvaloniaProperty.Register<ChatView, object?>(nameof(PromptHeaderContent));

    /// <summary>Defines the <see cref="PromptHeaderContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> PromptHeaderContentTemplateProperty =
        AvaloniaProperty.Register<ChatView, IDataTemplate?>(nameof(PromptHeaderContentTemplate));

    /// <summary>Defines the <see cref="PromptFooterContent"/> property.</summary>
    public static readonly StyledProperty<object?> PromptFooterContentProperty =
        AvaloniaProperty.Register<ChatView, object?>(nameof(PromptFooterContent));

    /// <summary>Defines the <see cref="PromptFooterContentTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> PromptFooterContentTemplateProperty =
        AvaloniaProperty.Register<ChatView, IDataTemplate?>(nameof(PromptFooterContentTemplate));

    /// <summary>
    /// Gets or sets what the view shows above the conversation, which stays in place while the conversation scrolls: a
    /// title, a model picker, a button that clears the conversation.
    /// </summary>
    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>Gets or sets the template of <see cref="HeaderContent"/>.</summary>
    public IDataTemplate? HeaderContentTemplate
    {
        get => GetValue(HeaderContentTemplateProperty);
        set => SetValue(HeaderContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets what the view shows above the prompt box, inside its area: what the prompt is about, quick commands.
    /// </summary>
    public object? PromptHeaderContent
    {
        get => GetValue(PromptHeaderContentProperty);
        set => SetValue(PromptHeaderContentProperty, value);
    }

    /// <summary>Gets or sets the template of <see cref="PromptHeaderContent"/>.</summary>
    public IDataTemplate? PromptHeaderContentTemplate
    {
        get => GetValue(PromptHeaderContentTemplateProperty);
        set => SetValue(PromptHeaderContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets what the view shows below the prompt box, inside its area: a note that answers can be wrong, a
    /// character count.
    /// </summary>
    public object? PromptFooterContent
    {
        get => GetValue(PromptFooterContentProperty);
        set => SetValue(PromptFooterContentProperty, value);
    }

    /// <summary>Gets or sets the template of <see cref="PromptFooterContent"/>.</summary>
    public IDataTemplate? PromptFooterContentTemplate
    {
        get => GetValue(PromptFooterContentTemplateProperty);
        set => SetValue(PromptFooterContentTemplateProperty, value);
    }
}
