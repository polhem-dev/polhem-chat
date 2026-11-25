using Microsoft.Extensions.AI;

namespace Polhem.Chat;

/// <summary>What <see cref="ChatAttachmentLimits.Check"/> found: the attachment to send, or why there is none.</summary>
public sealed class ChatAttachmentCheck
{
    private ChatAttachmentCheck(string fileName, DataContent? attachment, ChatAttachmentProblem? problem)
    {
        FileName = fileName;
        Attachment = attachment;
        Problem = problem;
    }

    /// <summary>Gets the name of the file, without the folders of its path.</summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the attachment, with its media type read from the content and the file's name, or <see langword="null"/> when
    /// the file is not allowed.
    /// </summary>
    public DataContent? Attachment { get; }

    /// <summary>Gets why the file is not allowed, or <see langword="null"/> when it is.</summary>
    public ChatAttachmentProblem? Problem { get; }

    /// <summary>Gets a value indicating whether the file may be attached.</summary>
    public bool IsAllowed => Attachment is not null;

    internal static ChatAttachmentCheck Allowed(string fileName, DataContent attachment) => new(fileName, attachment, null);

    internal static ChatAttachmentCheck Rejected(string fileName, ChatAttachmentProblem problem) => new(fileName, null, problem);
}
