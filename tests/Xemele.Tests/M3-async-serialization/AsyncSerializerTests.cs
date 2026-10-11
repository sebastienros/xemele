using System.Text;
using System.Text.Json;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Xemele.Tests.M3AsyncSerialization;

public sealed class AsyncSerializerTests
{
    private static XmlSaveOptions Omit => new() { OmitXmlDeclaration = true };
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static XmlDocument Root(out XmlNode root)
    {
        var document = new XmlDocument(); root = document.CreateElement("r");
        document.DocumentNode.AppendChild(root); return document;
    }
    private static async Task<byte[]> Check(XmlDocument document, XmlSaveOptions options, byte[]? golden = null)
    {
        using var synchronous = new MemoryStream(); document.Save(synchronous, options, Token);
        var asynchronous = new AsyncOnlyStream();
        await document.SaveAsync(asynchronous, options, Token);
        Assert.Equal(synchronous.ToArray(), asynchronous.Bytes);
        if (golden is not null) Assert.Equal(golden, asynchronous.Bytes);
        Assert.InRange(asynchronous.MaxWrite, 1, 8192);
        Assert.True(asynchronous.CanWrite); Assert.Equal(0, asynchronous.Flushes);
        Assert.Equal(0, asynchronous.Disposals);
        return asynchronous.Bytes;
    }
    [Fact]
    public async Task ApprovedIndependentByteVectors()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tests/Contracts/Fixtures/M1/serialization-vectors.json"))) directory = directory.Parent;
        Assert.NotNull(directory);
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "tests/Contracts/Fixtures/M1/serialization-vectors.json")));
        int count = 0;
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            var document = new XmlDocument(); XmlNode root = default;
            foreach (var action in vector.GetProperty("actions").EnumerateArray())
            {
                string S(int i) => action[i].GetString()!;
                switch (S(0))
                {
                    case "element": root = document.CreateElement(S(1), action.GetArrayLength() > 2 ? S(2) : null, action.GetArrayLength() > 3 ? S(3) : null); document.DocumentNode.AppendChild(root); break;
                    case "attribute": root.SetAttribute(S(1), S(2), action.GetArrayLength() > 3 ? S(3) : null, action.GetArrayLength() > 4 ? S(4) : null); break;
                    case "namespace": root.SetNamespaceDeclaration(S(1), S(2)); break;
                    case "text": root.AppendChild(document.CreateText(S(1))); break;
                    default: throw new InvalidOperationException();
                }
            }
            var config = vector.GetProperty("options");
            var options = new XmlSaveOptions
            {
                OmitXmlDeclaration = config.TryGetProperty("OmitXmlDeclaration", out var omit) && omit.GetBoolean(),
                EmitByteOrderMark = config.TryGetProperty("EmitByteOrderMark", out var bom) && bom.GetBoolean(),
                NamespaceHandling = config.TryGetProperty("NamespaceHandling", out var policy) ? Enum.Parse<XmlNamespaceHandling>(policy.GetString()!) : XmlNamespaceHandling.Strict,
                Encoding = vector.GetProperty("encoding").GetString() switch { "utf-16le" => new UnicodeEncoding(false, true, false), "utf-16be" => new UnicodeEncoding(true, true, false), _ => new UTF8Encoding(true, false) }
            };
            await Check(document, options, Convert.FromHexString(vector.GetProperty("expectedHex").GetString()!)); count++;
        }
        Assert.Equal(6, count);
    }
    [Theory]
    [InlineData(65001, false, false)] [InlineData(65001, true, false)] [InlineData(65001, false, true)] [InlineData(65001, true, true)]
    [InlineData(1200, false, false)] [InlineData(1200, true, false)] [InlineData(1200, false, true)] [InlineData(1200, true, true)]
    [InlineData(1201, false, false)] [InlineData(1201, true, false)] [InlineData(1201, false, true)] [InlineData(1201, true, true)]
    public async Task EncodingBomDeclarationAndSurrogateBoundaries(int page, bool bom, bool omit)
    {
        var document = Root(out var root);
        string value = new string('a', 2047) + "🙂" + new string('界', 4096);
        root.AppendChild(document.CreateText(value)); root.AppendChild(document.CreateCData(value));
        root.AppendChild(document.CreateComment(value)); root.AppendChild(document.CreateProcessingInstruction("p", value));
        string label = page == 65001 ? "utf-8" : page == 1200 ? "utf-16le" : "utf-16be";
        string expected = (omit ? "" : $"<?xml version=\"1.0\" encoding=\"{label}\"?>") + $"<r>{value}<![CDATA[{value}]]><!--{value}--><?p {value}?></r>";
        var encoding = Encoding.GetEncoding(page);
        byte[] preamble = !bom ? [] : page == 65001 ? [0xef, 0xbb, 0xbf] : page == 1200 ? [0xff, 0xfe] : [0xfe, 0xff];
        await Check(document, new() { Encoding = encoding, EmitByteOrderMark = bom, OmitXmlDeclaration = omit }, preamble.Concat(encoding.GetBytes(expected)).ToArray());
    }
    [Theory]
    [InlineData(XmlNodeKind.Text, "")] [InlineData(XmlNodeKind.Text, " ")] [InlineData(XmlNodeKind.Text, "tail")] [InlineData(XmlNodeKind.CData, "")]
    public async Task LookaheadAcceptsTextAfterStructuralFirstChild(XmlNodeKind kind, string value)
    {
        var document = Root(out var root); var branch = document.CreateElement("a");
        root.AppendChild(document.CreateComment("first")); root.AppendChild(branch);
        branch.AppendChild(document.CreateElement("b")); root.AppendChild(kind == XmlNodeKind.Text ? document.CreateText(value) : document.CreateCData(value));
        string tail = kind == XmlNodeKind.Text ? value : "<![CDATA[]]>";
        await Check(document, Omit with { Indent = true }, Encoding.UTF8.GetBytes("<r><!--first--><a>\n    <b/>\n  </a>" + tail + "</r>"));
    }
    [Fact]
    public async Task PrologEpilogAdjacentTextLexicalKindsAndAttributeOrder()
    {
        var document = Root(out var root);
        document.DocumentNode.InsertBefore(document.CreateProcessingInstruction("p", ""), root);
        document.DocumentNode.AppendChild(document.CreateComment("after"));
        root.SetAttribute("a", "1"); root.SetAttribute("b", "2"); root.SetAttribute("a", "\t\n\r\"&<"); root.RemoveAttribute("b"); root.SetAttribute("b", "3");
        root.AppendChild(document.CreateText("]")); root.AppendChild(document.CreateText("]")); root.AppendChild(document.CreateText(">&<\r\n\t🙂"));
        root.AppendChild(document.CreateComment("c")); root.AppendChild(document.CreateCData("<raw>")); root.AppendChild(document.CreateProcessingInstruction("go", "yes"));
        await Check(document, new() { Indent = true }, Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<?p?>\n<r a=\"&#x9;&#xA;&#xD;&quot;&amp;&lt;\" b=\"3\">]]&gt;&amp;&lt;&#xD;\n\t🙂<!--c--><![CDATA[<raw>]]><?go yes?></r>\n<!--after-->"));
    }
    [Fact]
    public async Task RepairCollisionReuseAndSiblingRestorationGolden()
    {
        var document = new XmlDocument(); var root = document.CreateElement("r", "u", "p"); document.DocumentNode.AppendChild(root);
        root.SetNamespaceDeclaration("p", "wrong"); root.SetNamespaceDeclaration("z", "attr"); root.SetNamespaceDeclaration("a", "attr");
        root.SetAttribute("x", "1", "attr", "p"); root.SetAttribute("y", "2", "other", "ns2");
        var child = document.CreateElement("c", "u", "p"); child.SetNamespaceDeclaration("ns3", "blocked"); root.AppendChild(child);
        root.AppendChild(document.CreateElement("s", "other", "ns2")); long version = document.MutationVersion;
        await Check(document, Omit with { NamespaceHandling = XmlNamespaceHandling.Repair }, Encoding.UTF8.GetBytes("<ns1:r xmlns:p=\"wrong\" xmlns:z=\"attr\" xmlns:a=\"attr\" xmlns:ns1=\"u\" xmlns:ns3=\"other\" a:x=\"1\" ns3:y=\"2\"><p:c xmlns:ns3=\"blocked\" xmlns:p=\"u\"/><ns2:s xmlns:ns2=\"other\"/></ns1:r>"));
        Assert.Equal(version, document.MutationVersion); Assert.Equal("p", root.Prefix);
        await Assert.ThrowsAsync<InvalidOperationException>(() => document.SaveAsync(new AsyncOnlyStream(), Omit, Token));
    }
    [Fact]
    public async Task DefaultUndeclarationStrictAndRepair()
    {
        var document = new XmlDocument(); var root = document.CreateElement("r", "u"); document.DocumentNode.AppendChild(root);
        root.SetNamespaceDeclaration("", "u"); root.SetNamespaceDeclaration("q", "a&\r");
        var child = document.CreateElement("c"); root.AppendChild(child);
        await Assert.ThrowsAsync<InvalidOperationException>(() => document.SaveAsync(new AsyncOnlyStream(), Omit, Token));
        await Check(document, Omit with { NamespaceHandling = XmlNamespaceHandling.Repair }, Encoding.UTF8.GetBytes("<r xmlns=\"u\" xmlns:q=\"a&amp;&#xD;\"><c xmlns=\"\"/></r>"));
        child.SetNamespaceDeclaration("", ""); child.SetAttribute("lang", "en", "http://www.w3.org/XML/1998/namespace", "xml");
        await Check(document, Omit, Encoding.UTF8.GetBytes("<r xmlns=\"u\" xmlns:q=\"a&amp;&#xD;\"><c xmlns=\"\" xml:lang=\"en\"/></r>"));
    }
    [Theory]
    [InlineData(XmlNodeKind.Text, "\0")] [InlineData(XmlNodeKind.Text, "\ufffe")]
    [InlineData(XmlNodeKind.Comment, "--")] [InlineData(XmlNodeKind.Comment, "x-")]
    [InlineData(XmlNodeKind.CData, "]]>")] [InlineData(XmlNodeKind.ProcessingInstruction, "?>")]
    public async Task InvalidTreeLexicalStateUsesOperationErrors(XmlNodeKind kind, string value)
    {
        var document = Root(out var root);
        var node = kind switch { XmlNodeKind.Text => document.CreateText("ok"), XmlNodeKind.Comment => document.CreateComment("ok"), XmlNodeKind.CData => document.CreateCData("ok"), _ => document.CreateProcessingInstruction("p", "ok") };
        root.AppendChild(node); document.Nodes[node.Slot].Value = value;
        var output = new AsyncOnlyStream(); await Assert.ThrowsAsync<InvalidOperationException>(() => document.SaveAsync(output, Omit, Token));
        Assert.Equal(0, output.Disposals); Assert.Equal(0, output.Flushes);
    }
    [Fact]
    public async Task InvalidNamesBindingsAttributesAndSurrogates()
    {
        foreach (string value in new[] { new string((char)0xd800, 1), new string((char)0xdc00, 1) })
        {
            var document = Root(out var root); var attribute = root.SetAttribute("a", "ok"); document.AttributeSlots[attribute.Slot].Value = value;
            await Assert.ThrowsAsync<InvalidOperationException>(() => document.SaveAsync(new AsyncOnlyStream(), Omit, Token));
        }
        var d = Root(out var r); var n = r.SetNamespaceDeclaration("p", "u"); d.NamespaceSlots[n.Slot].Value = "http://www.w3.org/2000/xmlns/";
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.SaveAsync(new AsyncOnlyStream(), Omit, Token));
        d.NamespaceSlots[n.Slot].Value = "u"; var second = r.SetNamespaceDeclaration("q", "v"); d.NamespaceSlots[second.Slot].Prefix = "p";
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.SaveAsync(new AsyncOnlyStream(), Omit with { NamespaceHandling = XmlNamespaceHandling.Repair }, Token));
        d = Root(out r); var pi = d.CreateProcessingInstruction("p", ""); r.AppendChild(pi);
        d.Nodes[pi.Slot] = new XmlDocument.NodeData(XmlNodeKind.ProcessingInstruction, "XmL", "", "", "") { Parent = r.Slot };
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.SaveAsync(new AsyncOnlyStream(), Omit, Token));
    }
    [Fact]
    public async Task ArgumentsRootAndPreCancellationPrecedeOutput()
    {
        var document = Root(out _); var stream = new AsyncOnlyStream();
        await Assert.ThrowsAsync<ArgumentNullException>(() => XmlAsyncSerializer.SerializeAsync(null!, stream, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => document.SaveAsync(null!, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => document.SaveAsync(new MemoryStream([], false), cancellationToken: Token));
        foreach (var options in new[] { new XmlSaveOptions { Encoding = null! }, new() { Encoding = Encoding.ASCII }, new() { NamespaceHandling = (XmlNamespaceHandling)99 } })
            await Assert.ThrowsAsync<ArgumentException>(() => document.SaveAsync(stream, options, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new XmlDocument().SaveAsync(stream, cancellationToken: Token));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => document.SaveAsync(stream, cancellationToken: canceled.Token));
        Assert.Equal(canceled.Token, error.CancellationToken); Assert.Equal(0, stream.Writes);
        await Check(document, Omit, Encoding.UTF8.GetBytes("<r/>"));
    }
    [Fact]
    public async Task DelayedIoIsIncrementalAndPreservesFailureAndCancellationIdentity()
    {
        var document = Root(out var root); root.AppendChild(document.CreateText(new string('x', 100_000)));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new AsyncOnlyStream { Gate = gate }; Task operation = document.SaveAsync(output, Omit, Token);
        Assert.False(operation.IsCompleted); Assert.Equal(1, output.Writes); Assert.InRange(output.MaxWrite, 1, 8192);
        Assert.Empty(output.Bytes); gate.SetResult(); await operation;
        Assert.Equal(100007, output.Bytes.Length); Assert.Equal(0, output.Disposals);
        var failure = new IOException("identity"); var bad = new AsyncOnlyStream { Failure = failure };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => document.SaveAsync(bad, Omit, Token)));
        Assert.True(bad.Bytes.Length > 0); Assert.Equal(0, bad.Disposals);
        using var cancellation = new CancellationTokenSource(); var canceled = new AsyncOnlyStream { Cancellation = cancellation };
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => document.SaveAsync(canceled, Omit, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken); Assert.True(canceled.Bytes.Length > 0);
        Assert.Equal(0, canceled.Disposals); Assert.Equal(0, canceled.Flushes);
    }
    [Fact]
    public async Task DeepTraversalDoesNotUseRecursiveStack()
    {
        var document = new XmlDocument(); var current = document.CreateElement("leaf");
        for (int i = 0; i < 10000; i++) { var parent = document.CreateElement("n", "u", "p"); parent.SetNamespaceDeclaration("p", "u"); parent.AppendChild(current); current = parent; }
        document.DocumentNode.AppendChild(current);
        var bytes = await Check(document, Omit); Assert.Equal(230007, bytes.Length);
    }
    [Theory]
    [InlineData("yes")] [InlineData("no")]
    public async Task DoctypeAndStandaloneMetadataFollowM1Serialization(string standalone)
    {
        var document = XmlDocument.Parse($"<?xml version='1.0' standalone='{standalone}'?><!DOCTYPE r [<!ENTITY e 'value'>]><r>&e;</r>", new() { DtdProcessing = XmlDtdProcessing.Parse }, Token);
        Assert.NotNull(document.DocumentType);
        await Check(document, new(), Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?><r>value</r>"));
    }
    [Fact]
    public async Task OptionsSnapshotSurvivesDelayedBomWrite()
    {
        var document = Root(out var root); root.AppendChild(document.CreateText("🙂"));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new AsyncOnlyStream { Gate = gate };
        var callerEncoding = (Encoding)new UnicodeEncoding(true, true, false).Clone();
        Task operation = document.SaveAsync(stream, new() { Encoding = callerEncoding, EmitByteOrderMark = true }, Token);
        Assert.False(operation.IsCompleted);
        // A caller's mutable fallback cannot change the strict code-page snapshot.
        callerEncoding.EncoderFallback = new EncoderReplacementFallback("bad");
        gate.SetResult(); await operation;
        Assert.Equal(new byte[] { 0xfe, 0xff }.Concat(callerEncoding.GetBytes("<?xml version=\"1.0\" encoding=\"utf-16be\"?><r>🙂</r>")), stream.Bytes);
    }
    [Fact]
    public async Task ScratchOwnerReturnedExactlyOnceOnSuccessIoAndCancellation()
    {
        var document = Root(out var root); root.AppendChild(document.CreateText(new string('x', 100000)));
        using var observer = new PoolReturns();
        foreach (string outcome in new[] { "success", "io", "cancel" })
        {
            observer.Returns.Clear(); using var cancellation = new CancellationTokenSource();
            var stream = new AsyncOnlyStream { Failure = outcome == "io" ? new IOException("owner") : null, Cancellation = outcome == "cancel" ? cancellation : null };
            if (outcome == "success") await document.SaveAsync(stream, Omit, Token);
            else if (outcome == "io") await Assert.ThrowsAsync<IOException>(() => document.SaveAsync(stream, Omit, Token));
            else await Assert.ThrowsAsync<OperationCanceledException>(() => document.SaveAsync(stream, Omit, cancellation.Token));
            Assert.NotEqual(0, stream.OwnerId);
            lock (observer.Returns) Assert.Equal(1, observer.Returns.Count(id => id == stream.OwnerId));
        }
    }
    private sealed class PoolReturns : EventListener
    {
        internal readonly List<int> Returns = [];
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "System.Buffers.ArrayPoolEventSource") EnableEvents(source, EventLevel.Verbose);
        }
        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if (data.EventName == "BufferReturned" && data.Payload?[0] is int id)
                lock (Returns) Returns.Add(id);
        }
    }
    internal sealed class AsyncOnlyStream : Stream
    {
        private readonly MemoryStream storage = new();
        internal byte[] Bytes => storage.ToArray();
        internal int MaxWrite, Writes, Flushes, Disposals, OwnerId;
        internal IOException? Failure; internal CancellationTokenSource? Cancellation; internal TaskCompletionSource? Gate;
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => storage.Length; public override long Position { get => storage.Position; set => throw new NotSupportedException(); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Writes++; MaxWrite = Math.Max(MaxWrite, buffer.Length);
            Assert.True(MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> owner));
            OwnerId = RuntimeHelpers.GetHashCode(owner.Array!);
            if (Gate is not null) await Gate.Task.WaitAsync(cancellationToken);
            else await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null && storage.Length != 0) throw Failure;
            storage.Write(buffer.Span); Cancellation?.Cancel();
        }
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous write");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Synchronous write");
        public override void Flush() { Flushes++; throw new InvalidOperationException("Implicit flush"); }
        public override Task FlushAsync(CancellationToken cancellationToken) { Flushes++; throw new InvalidOperationException("Implicit flush"); }
        protected override void Dispose(bool disposing) { Disposals++; throw new InvalidOperationException("Caller disposal"); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
