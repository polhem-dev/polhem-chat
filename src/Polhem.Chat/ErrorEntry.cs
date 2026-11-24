namespace Polhem.Chat;

/// <summary>An error the model reported in its stream, or the exception that ended a turn.</summary>
public sealed class ErrorEntry : ChatEntry
{
    /// <summary>Initializes a new instance of the <see cref="ErrorEntry"/> class.</summary>
    /// <param name="message">The message to show.</param>
    /// <param name="errorCode">The error code the service reported, if any.</param>
    /// <param name="details">More details the service reported, if any.</param>
    public ErrorEntry(string message, string? errorCode = null, string? details = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message = message;
        ErrorCode = errorCode;
        Details = details;
    }

    /// <summary>Gets the message to show.</summary>
    public string Message { get; }

    /// <summary>Gets the error code the service reported, if any.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets more details the service reported, if any.</summary>
    public string? Details { get; }
}
