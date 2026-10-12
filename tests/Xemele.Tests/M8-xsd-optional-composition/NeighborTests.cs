using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdOptionalComposition;

public sealed class NeighborTests
{
    internal static readonly Uri Basis = new("https://optional.test/main.xsd");
    internal static string Wrap(string body, string attributes = "") => $"<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' {attributes}>{body}</xs:schema>";
    internal sealed class Explicit(Func<XmlResourceRequest, CancellationToken, XmlResourceResolution> callback) : XmlResourceResolver
    {
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException("explicit only");
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken cancellationToken) { Calls++; return callback(request, cancellationToken); }
    }
    internal sealed class OwnedStream(byte[] data) : MemoryStream(data)
    {
        internal int Disposals;
        internal Action? OnDispose;
        internal Exception? DisposeFailure { get; init; }
        internal Exception? ReadFailure { get; init; }
        internal Action? AfterRead { get; init; }
        internal OwnedStream(string source) : this(Encoding.UTF8.GetBytes(source)) { }
        public override int Read(Span<byte> buffer) { if (ReadFailure is { } error) throw error; int n = base.Read(buffer); AfterRead?.Invoke(); return n; }
        protected override void Dispose(bool disposing) { if (disposing) { Disposals++; OnDispose?.Invoke(); if (DisposeFailure is { } error) throw error; } base.Dispose(disposing); }
    }
}
