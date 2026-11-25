using Microsoft.JSInterop;

namespace Polhem.Chat.Blazor;

/// <summary>
/// Reads a file the browser streams, and reports a file the browser can no longer give as an <see cref="IOException"/>,
/// which the composer refuses as unreadable.
/// </summary>
/// <remarks>
/// NOTE: The browser forgets the files of an input once it receives new ones, so a file the user dropped earlier that
/// is not read yet goes away when they drop, paste or pick more; the page may also go away, or its circuit, while a file
/// is read. Each of those fails the read with an exception of the interop, which would otherwise end the circuit.
/// </remarks>
internal sealed class BrowserFileStream(Stream inner) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The browser's files are read asynchronously.");

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.ReadAsync(buffer, cancellationToken);
        }
        catch (JSException exception)
        {
            throw new IOException("The browser no longer has the file.", exception);
        }
        catch (JSDisconnectedException exception)
        {
            throw new IOException("The page that had the file went away.", exception);
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
        // Nothing is written.
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
