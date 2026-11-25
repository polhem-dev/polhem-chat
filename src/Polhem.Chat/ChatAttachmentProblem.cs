namespace Polhem.Chat;

/// <summary>Why a file was not attached.</summary>
public enum ChatAttachmentProblem
{
    /// <summary>The file is larger than <see cref="ChatAttachmentLimits.MaxFileSize"/>.</summary>
    TooLarge,

    /// <summary>The files together would be larger than <see cref="ChatAttachmentLimits.MaxTotalSize"/>.</summary>
    TotalTooLarge,

    /// <summary>The prompt already has <see cref="ChatAttachmentLimits.MaxCount"/> files.</summary>
    TooMany,

    /// <summary>The type of the file is not in <see cref="ChatAttachmentLimits.AllowedMediaTypes"/>, or is not known.</summary>
    TypeNotAllowed,

    /// <summary>The content of the file is not what its name says, for example a program named like a picture.</summary>
    ContentMismatch,

    /// <summary>The file is empty, could not be read, or is text that is not valid UTF-8.</summary>
    Unreadable,
}
