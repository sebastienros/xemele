namespace Xemele.Tool;
// Preserve whether an I/O failure belongs to an invocation file or a granted resource.
internal sealed class GuardedFileInput(Stream inner, bool invocation) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int Read(Span<byte> buffer)
    {
        try { return inner.Read(buffer); }
        catch (IOException ex) { if (invocation) throw new InvocationException(ex.Message); throw new RejectionException(ex.Message); }
    }
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        try { if (disposing) inner.Dispose(); }
        catch (IOException ex) { if (invocation) throw new InvocationException(ex.Message); throw new RejectionException(ex.Message); }
        finally { base.Dispose(disposing); }
    }
}
