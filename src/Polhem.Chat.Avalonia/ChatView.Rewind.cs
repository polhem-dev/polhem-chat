using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Polhem.Chat.Avalonia;

/// <summary>
/// Sending the last prompt again: as it was (regenerate), or changed in the prompt box (edit). Both replace the last
/// prompt and everything after it.
/// </summary>
public partial class ChatView
{
    /// <summary>Defines the <see cref="AllowsRewind"/> property.</summary>
    public static readonly StyledProperty<bool?> AllowsRewindProperty =
        AvaloniaProperty.Register<ChatView, bool?>(nameof(AllowsRewind));

    private const string EditingPseudoClass = ":editing";

    private ViewCommand? _regenerateCommand;
    private ViewCommand? _editLastPromptCommand;
    private ViewCommand? _cancelEditCommand;

    /// <summary>
    /// Occurs when the user asks to send the last prompt again, without a <see cref="Runner"/>. The application calls
    /// <see cref="ChatConversation.RewindLastPrompt"/> and sends the prompt it returns.
    /// </summary>
    public event EventHandler? RegenerateRequested;

    /// <summary>
    /// Gets or sets a value indicating whether the user can send the last prompt again or change it. By default,
    /// <see langword="null"/>, they can when the view has a <see cref="Runner"/>, and cannot when the application drives
    /// the conversation itself: what the tools of the replaced turns did is not undone, and only the application knows
    /// whether that is safe.
    /// </summary>
    public bool? AllowsRewind
    {
        get => GetValue(AllowsRewindProperty);
        set => SetValue(AllowsRewindProperty, value);
    }

    /// <summary>Gets a value indicating whether the prompt box holds the last prompt for the user to change.</summary>
    public bool IsEditingPrompt => _composer.IsEditingPrompt;

    /// <summary>
    /// Gets the command that sends the last prompt again in place of its turn. Its parameter, when there is one, must be
    /// the last reply, which is where the default template shows it.
    /// </summary>
    public ICommand RegenerateCommand => _regenerateCommand ??= new ViewCommand(_ => _composer.Regenerate(), CanRegenerate);

    /// <summary>
    /// Gets the command that puts the last prompt, its parameter, in the prompt box for the user to change; submitting
    /// then replaces it and everything after it.
    /// </summary>
    public ICommand EditLastPromptCommand => _editLastPromptCommand ??= new ViewCommand(
        parameter =>
        {
            _composer.EditLastPrompt((UserPromptEntry)parameter!);
            FocusPrompt();
        },
        parameter => parameter is UserPromptEntry entry && _composer.CanEditLastPrompt(entry));

    /// <summary>Gets the command that stops changing the last prompt and gives the box back what it held before.</summary>
    public ICommand CancelEditCommand => _cancelEditCommand ??= new ViewCommand(_ => _composer.CancelEdit(), _ => IsEditingPrompt);

    private bool CanRegenerate(object? parameter) => parameter switch
    {
        null => _composer.CanRegenerate(),
        AssistantReplyEntry reply => _composer.CanRegenerate(reply),
        _ => false,
    };

    private void OnComposerRegenerateRequested(object? sender, EventArgs e) => RegenerateRequested?.Invoke(this, e);

    private void UpdateEditing()
    {
        PseudoClasses.Set(EditingPseudoClass, IsEditingPrompt);
        _cancelEditCommand?.RaiseCanExecuteChanged();
    }

    private void UpdateRewind()
    {
        _regenerateCommand?.RaiseCanExecuteChanged();
        _editLastPromptCommand?.RaiseCanExecuteChanged();
    }

    private void FocusPrompt() =>
        this.GetVisualDescendants().OfType<ChatPromptBox>().FirstOrDefault(box => box.TemplatedParent == this)?.Focus();
}
