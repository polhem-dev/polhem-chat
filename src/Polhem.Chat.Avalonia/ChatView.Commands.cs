using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Polhem.Chat.Avalonia;

/// <summary>The host's commands the user reaches by typing a slash at the start of the prompt box.</summary>
public partial class ChatView
{
    /// <summary>Defines the <see cref="Commands"/> property.</summary>
    public static readonly StyledProperty<IEnumerable<PromptCommand>?> CommandsProperty =
        AvaloniaProperty.Register<ChatView, IEnumerable<PromptCommand>?>(nameof(Commands));

    private const string CommandListPart = "PART_CommandList";
    private const string CommandAnnouncementPart = "PART_CommandAnnouncement";
    private const string PromptBoxPart = "PART_PromptBox";
    private const string CommandsPseudoClass = ":commands";

    private ListBox? _commandList;
    private TextBlock? _commandAnnouncement;
    private ChatPromptBox? _promptBox;

    /// <summary>
    /// Gets or sets the commands offered in the prompt box. Typing <c>/</c> at the start of the box opens a list of those
    /// whose name matches what follows; the arrow keys move in it, Enter or a click chooses a command and Esc closes it.
    /// The view follows an observable collection as the host changes it. Without commands, a slash is plain text.
    /// </summary>
    public IEnumerable<PromptCommand>? Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    /// <summary>Gets a value indicating whether the list of commands is open under the user's typing.</summary>
    public bool IsCommandListOpen => _composer.IsCommandListOpen;

    private void TakeCommandList(INameScope scope)
    {
        if (_commandList is not null)
        {
            _commandList.Tapped -= OnCommandTapped;
        }
        _commandList = scope.Find<ListBox>(CommandListPart);
        _commandAnnouncement = scope.Find<TextBlock>(CommandAnnouncementPart);
        _promptBox = scope.Find<ChatPromptBox>(PromptBoxPart);
        if (_commandList is not null)
        {
            _commandList.Tapped += OnCommandTapped;
        }
        UpdateCommandList();
    }

    private void UpdateCommandList()
    {
        PseudoClasses.Set(CommandsPseudoClass, _composer.IsCommandListOpen);
        if (_commandList is not null)
        {
            if (!ReferenceEquals(_commandList.ItemsSource, _composer.CommandMatches))
            {
                _commandList.ItemsSource = _composer.CommandMatches;
            }
            _commandList.SelectedItem = _composer.SelectedCommand;
            if (_composer.SelectedCommand is { } selected)
            {
                _commandList.ScrollIntoView(selected);
            }
        }
        if (_commandAnnouncement is not null)
        {
            _commandAnnouncement.Text = _composer.DescribeSelectedCommand() ?? string.Empty;
        }
    }

    // NOTE: The keys go to the list only from the prompt box, and never while an input method composes: the arrows then
    // move among its candidates and Esc cancels the composition.
    private bool HandleCommandKey(KeyEventArgs e)
    {
        if (!_composer.IsCommandListOpen || e.KeyModifiers != KeyModifiers.None
            || e.Source is not ChatPromptBox { IsComposing: false })
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.Up:
                _composer.MoveCommandSelection(-1);
                return true;
            case Key.Down:
                _composer.MoveCommandSelection(1);
                return true;
            case Key.Escape:
                _composer.CloseCommandList();
                return true;
            default:
                return false;
        }
    }

    private void ChooseCommand(PromptCommand? command)
    {
        var choosing = _composer.ChooseCommandAsync(command);
        // The text the command put in the box is for the user to finish, so the caret goes after it.
        if (_promptBox is not null)
        {
            _promptBox.CaretIndex = _promptBox.Text?.Length ?? 0;
        }
        _ = choosing;
    }

    private void OnCommandTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as StyledElement)?.DataContext is PromptCommand command)
        {
            e.Handled = true;
            ChooseCommand(command);
            _promptBox?.Focus();
        }
    }
}
