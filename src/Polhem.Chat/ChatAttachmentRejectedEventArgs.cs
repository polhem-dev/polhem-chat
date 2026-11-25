namespace Polhem.Chat;

/// <summary>Provides data for the <see cref="ChatComposer.AttachmentRejected"/> event.</summary>
/// <param name="fileName">The name of the file as <see cref="ChatAttachmentLimits.FileNameOf"/> gives it.</param>
/// <param name="problem">Why it was not attached.</param>
public sealed class ChatAttachmentRejectedEventArgs(string fileName, ChatAttachmentProblem problem) : EventArgs
{
    /// <summary>
    /// Gets the name of the file as <see cref="ChatAttachmentLimits.FileNameOf"/> gives it: without its folders and
    /// without characters that draw nothing. It is empty when nothing else was left of the name.
    /// </summary>
    public string FileName { get; } = fileName;

    /// <summary>Gets why the file was not attached.</summary>
    public ChatAttachmentProblem Problem { get; } = problem;
}
