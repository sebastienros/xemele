using System.Text;
using Xemele.Internal.Operations;
namespace Xemele.Resolution.XInclude;

internal sealed partial class IncludeOperation
{
    private async ValueTask<XmlDocument> ReadXml(XmlResolvedResource resource)
    {
        var parse = options.ParseOptions with { Resolver = new EntityResolver(this, options.ParseOptions.Resolver) };
        byte[] buffer = new byte[4096];
        if (asynchronous)
        {
            var adapter = new IncludeAsyncInput(parse, token, resource.SourceUri, n => Charge(Quota.DocumentCharacters, n));
            bool failed = false;
            try
            {
                while (adapter.Diagnostic is null)
                {
                    token.ThrowIfCancellationRequested();
                    int count = await resource.Content.ReadAsync(buffer.AsMemory(0, ReadCapacity(buffer.Length)), token).ConfigureAwait(false);
                    Charge(Quota.ExternalBytes, count);
                    if (count == 0) { await adapter.CompleteAsync().ConfigureAwait(false); break; }
                    await adapter.AppendAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
                }
                RejectParser(adapter.Diagnostic, resource.SourceUri);
                return adapter.Document;
            }
            catch { failed = true; throw; }
            finally
            {
                if (failed) { try { await adapter.DisposeAsync().ConfigureAwait(false); } catch { } }
                else await adapter.DisposeAsync().ConfigureAwait(false);
            }
        }
        else
        {
            var adapter = new IncludeByteInput(parse, token, resource.SourceUri, n => Charge(Quota.DocumentCharacters, n));
            bool failed = false;
            try
            {
                while (adapter.Diagnostic is null)
                {
                    token.ThrowIfCancellationRequested();
                    int count = resource.Content.Read(buffer.AsSpan(0, ReadCapacity(buffer.Length)));
                    Charge(Quota.ExternalBytes, count);
                    if (count == 0) { adapter.Complete(); break; }
                    adapter.Append(buffer.AsSpan(0, count));
                }
                RejectParser(adapter.Diagnostic, resource.SourceUri);
                return adapter.Document;
            }
            catch { failed = true; throw; }
            finally
            {
                if (failed) { try { adapter.Dispose(); } catch { } }
                else adapter.Dispose();
            }
        }
    }
    private int ReadCapacity(int requested)
    {
        if (options.Limits.ExternalBytes is not long maximum) return requested;
        long remaining = maximum - budget.Count(Quota.ExternalBytes);
        return (int)Math.Min(requested, remaining >= int.MaxValue ? int.MaxValue : remaining + 1);
    }
    private static void RejectParser(XmlDiagnostic? diagnostic, Uri source)
    {
        if (diagnostic is null) return;
        bool limit = diagnostic.Code.Contains("Limit", StringComparison.Ordinal) || diagnostic.Code.Contains("Quota", StringComparison.Ordinal);
        throw new Rejection(limit ? "Limit" : "ResourceError", !limit, source);
    }
    private async ValueTask<string> ReadText(XmlResolvedResource resource, string? label)
    {
        using var storage = new MemoryStream();
        byte[] buffer = new byte[4096];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int count = asynchronous ? await resource.Content.ReadAsync(buffer.AsMemory(0, ReadCapacity(buffer.Length)), token).ConfigureAwait(false) : resource.Content.Read(buffer.AsSpan(0, ReadCapacity(buffer.Length)));
            Charge(Quota.ExternalBytes, count);
            if (count == 0) break;
            if (count > int.MaxValue - storage.Length) throw new Rejection("Limit", uri: resource.SourceUri);
            storage.Write(buffer.AsSpan(0, count));
        }
        string decoded;
        try { decoded = DecodeText(storage.GetBuffer().AsSpan(0, checked((int)storage.Length)), label); }
        catch (DecoderFallbackException) { throw new Rejection("ResourceError", true, resource.SourceUri); }
        Charge(Quota.DocumentCharacters, decoded.Length);
        try { TreeValidation.Text(decoded); }
        catch (ArgumentException) { throw new Rejection("ResourceError", true, resource.SourceUri); }
        return decoded;
    }
    private static string DecodeText(ReadOnlySpan<byte> bytes, string? label)
    {
        label ??= "utf-8";
        Encoding encoding;
        bool utf16Generic = label.Equals("utf-16", StringComparison.OrdinalIgnoreCase);
        if (label.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) encoding = new UTF8Encoding(false, true);
        else if (utf16Generic || label.Equals("utf-16le", StringComparison.OrdinalIgnoreCase)) encoding = new UnicodeEncoding(false, false, true);
        else if (label.Equals("utf-16be", StringComparison.OrdinalIgnoreCase)) encoding = new UnicodeEncoding(true, false, true);
        else if (label.Equals("us-ascii", StringComparison.OrdinalIgnoreCase)) encoding = Encoding.GetEncoding(20127, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        else if (label.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase)) encoding = Encoding.Latin1;
        else if (label.Equals("windows-1252", StringComparison.OrdinalIgnoreCase))
        {
            if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) || bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) || bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) throw new Rejection("ResourceError", true);
            return Decode1252(bytes);
        }
        else throw new Rejection("ResourceError", true);
        int bom = bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? 65001 :
            bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) ? 1200 : bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]) ? 1201 : 0;
        if (utf16Generic && bom == 0) throw new Rejection("ResourceError", true);
        if (bom != 0)
        {
            if (utf16Generic && bom is 1200 or 1201) encoding = new UnicodeEncoding(bom == 1201, false, true);
            if (encoding.CodePage != bom) throw new Rejection("ResourceError", true);
            bytes = bytes[(bom == 65001 ? 3 : 2)..];
        }
        return NormalizeLines(encoding.GetString(bytes));
    }
    private static string Decode1252(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<char> special = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008DŽ\u008F\u0090‘’“”•–—˜™š›œ\u009DžŸ";
        char[] text = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D) throw new Rejection("ResourceError", true);
            text[i] = bytes[i] is >= 128 and <= 159 ? special[bytes[i] - 128] : (char)bytes[i];
        }
        return NormalizeLines(new string(text));
    }
    private static string NormalizeLines(string text) => text.Contains('\r') ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n') : text;

    private sealed class EntityResolver(IncludeOperation operation, XmlResourceResolver? resolver) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            operation.Charge(Quota.ExternalResources, 1);
            return Wrap(resolver?.Resolve(request, cancellationToken));
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            operation.Charge(Quota.ExternalResources, 1);
            return Wrap(resolver is null ? null : await resolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false));
        }
        private XmlResolvedResource? Wrap(XmlResolvedResource? resource) => resource is null ? null : new(new EntityStream(operation, resource), resource.SourceUri);
    }
    private sealed class EntityStream(IncludeOperation operation, XmlResolvedResource resource) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            operation.token.ThrowIfCancellationRequested();
            int count = resource.Content.Read(buffer[..operation.ReadCapacity(buffer.Length)]); operation.Charge(Quota.ExternalBytes, count); return count;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            operation.token.ThrowIfCancellationRequested();
            int count = await resource.Content.ReadAsync(buffer[..operation.ReadCapacity(buffer.Length)], cancellationToken).ConfigureAwait(false);
            operation.Charge(Quota.ExternalBytes, count); return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        protected override void Dispose(bool disposing) { if (disposing) resource.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => resource.DisposeAsync();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
