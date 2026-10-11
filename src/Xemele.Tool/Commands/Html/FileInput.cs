namespace Xemele.Tool;
internal sealed class HtmlFileInput(Stream inner) : Stream
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
        catch (IOException ex) { throw new HtmlInvocationException(ex.Message); }
    }
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        try { if (disposing) inner.Dispose(); }
        catch (IOException ex) { throw new HtmlInvocationException(ex.Message); }
        finally { base.Dispose(disposing); }
    }
}
