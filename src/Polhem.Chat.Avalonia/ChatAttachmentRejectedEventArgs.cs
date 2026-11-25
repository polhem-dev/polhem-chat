namespace Polhem.Chat.Avalonia;

/// <summary>Provides data for the <see cref="ChatView.AttachmentRejected"/> event.</summary>
/// <param name="fileName">The name of the file, without its folders.</param>
/// <param name="problem">Why it was not attached.</param>
public sealed class ChatAttachmentRejectedEventArgs(string fileName, ChatAttachmentProblem problem) : EventArgs
{
    /// <summary>Gets the name of the file, without its folders.</summary>
    public string FileName { get; } = fileName;

    /// <summary>Gets why the file was not attached.</summary>
    public ChatAttachmentProblem Problem { get; } = problem;
}
