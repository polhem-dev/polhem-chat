using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// The text box a <see cref="ChatView"/> takes prompts in. Enter raises <see cref="SubmitRequested"/>, Shift+Enter
/// starts a new line, and an Enter that confirms the composition of an input method (Chinese, Japanese, Korean) does
/// neither. It is styled like any <see cref="TextBox"/> of the active theme.
/// </summary>
public class ChatPromptBox : TextBox
{
    /// <summary>Defines the <see cref="SubmitRequested"/> event, which bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SubmitRequestedEvent =
        RoutedEvent.Register<ChatPromptBox, RoutedEventArgs>(nameof(SubmitRequested), RoutingStrategies.Bubble);

    private TextPresenter? _presenter;

    static ChatPromptBox()
    {
        AcceptsReturnProperty.OverrideDefaultValue<ChatPromptBox>(true);
        TextWrappingProperty.OverrideDefaultValue<ChatPromptBox>(TextWrapping.Wrap);
    }

    /// <summary>Occurs when the user presses Enter to send the prompt.</summary>
    public event EventHandler<RoutedEventArgs>? SubmitRequested
    {
        add => AddHandler(SubmitRequestedEvent, value);
        remove => RemoveHandler(SubmitRequestedEvent, value);
    }

    /// <summary>
    /// Gets a value indicating whether an input method is composing text that it has not committed yet.
    /// </summary>
    public bool IsComposing => !string.IsNullOrEmpty(_presenter?.PreeditText);

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // WARNING: An input method confirms its composition with Enter. On Windows that key arrives as ImeProcessed; on
        // the other platforms it arrives as Enter while the uncommitted text is still on the presenter. Sending in either
        // case would send half-typed text, so both go to the text box, which leaves them to the input method.
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && !IsComposing)
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(SubmitRequestedEvent, this));
            return;
        }
        base.OnKeyDown(e);
    }
}
