using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Xunit;
using ProductDocument = Xemele.XmlDocument;

namespace Xemele.Conformance.Tests.XmlSubset;

public sealed class CorpusTests
{
    private static string Root
    {
        get
        {
            var path = new DirectoryInfo(AppContext.BaseDirectory);
            while (path is not null && !File.Exists(Path.Combine(path.FullName, "global.json"))) path = path.Parent;
            return path?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    private static JsonDocument Inventory() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tests/corpora/xml/inventory.json")));
    private static string Resources => Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
    internal static byte[] VerifiedBytes(string root, string relative, string hash)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(prefix, StringComparison.Ordinal) || relative.Split('/', '\\').Contains("..")) throw new InvalidDataException("Outside resource root.");
        // No symlink may redirect the hashed fixture outside the resource tree.
        for (var item = new FileInfo(path) as FileSystemInfo; item is not null; item = item is FileInfo f ? f.Directory : ((DirectoryInfo)item).Parent)
            {
                if (item.LinkTarget is not null) throw new InvalidDataException("Symbolic resource path.");
                if (item.FullName == root) break;
            }
        byte[] bytes = File.ReadAllBytes(path);
        if (!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(hash, StringComparison.Ordinal)) throw new InvalidDataException("Resource hash mismatch.");
        return bytes;
    }
    public static IEnumerable<object[]> Rejections()
    {
        using var inventory = Inventory();
        return inventory.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("m1").GetProperty("outcome").GetString() == "rejected")
            .Select(c => new object[] { c.GetProperty("id").GetString()!, c.GetProperty("inputPath").GetString()!, c.GetProperty("inputSha256").GetString()! }).ToArray();
    }
    [Theory]
    [MemberData(nameof(Rejections))]
    public void EverySupportedRejectionExecutesOriginalBytes(string id, string path, string hash)
    {
        byte[] bytes = VerifiedBytes(Resources, path, hash);
        bool accepted = ProductDocument.TryParse(bytes, out var document, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken);
        Record("product-", id, new { id, path, sha256 = hash, profile = "xml-1.0-fifth-edition-m1", outcome = accepted ? "accepted" : "rejected", diagnostics });
        Assert.False(accepted, id);
        Assert.Null(document);
        var diagnostic = Assert.Single(diagnostics);
        Assert.True(diagnostic.Code.StartsWith("XML.", StringComparison.Ordinal) || diagnostic.Code.StartsWith("INPUT.", StringComparison.Ordinal), id);
        if (path == "xmlconf/eduni/namespaces/1.0/042.xml") Assert.Equal("XML.Syntax", diagnostic.Code);
        Assert.NotEmpty(diagnostic.Message);
        Assert.True(diagnostic.Location.LineNumber >= 1 && diagnostic.Location.LinePosition >= 1, id);
        Assert.InRange(diagnostic.Location.ByteOffset!.Value, 0, bytes.LongLength);
        string ownedMessage = diagnostic.Message;
        using var stream = new ShortReadStream(bytes);
        var failure = Assert.Throws<XmlParseException>(() => ProductDocument.Load(stream, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(diagnostic, Assert.Single(failure.Diagnostics));
        Assert.True(stream.CanRead);
        Assert.Equal(diagnostic, Assert.Single(Assert.Throws<XmlParseException>(() => ProductDocument.Parse(bytes, cancellationToken: TestContext.Current.CancellationToken)).Diagnostics));
        Array.Fill(bytes, (byte)0);
        Assert.Equal(ownedMessage, diagnostic.Message);
    }
    [Fact]
    public void FullDiscoveryAndAllResourcesRemainVisibleAndVerified()
    {
        using var inventory = Inventory();
        var cases = inventory.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(2585, cases.Length);
        Assert.Equal(2585, cases.Select(c => c.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(242, cases.Count(c => c.GetProperty("m1").GetProperty("outcome").GetString() == "rejected"));
        var gaps = cases.Where(c => c.GetProperty("m1").GetProperty("outcome").GetString() == "unsupported-profile").ToArray();
        Assert.Equal(2343, gaps.Length);
        foreach (var gap in gaps) Assert.NotEmpty(gap.GetProperty("m1").GetProperty("reasons").EnumerateArray());
        var files = inventory.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(3386, files.Length);
        foreach (var file in files) Assert.Equal(file.GetProperty("bytes").GetInt32(), VerifiedBytes(Resources, file.GetProperty("path").GetString()!, file.GetProperty("sha256").GetString()!).Length);
    }
    [Fact]
    public void BootstrapOriginalExpectationsExecute()
    {
        string root = Path.Combine(Root, "tests/corpora/bootstrap");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var fixtures = manifest.RootElement.GetProperty("fixtures").EnumerateArray().ToArray();
        Assert.Equal(2, fixtures.Length);
        foreach (var fixture in fixtures)
        {
            byte[] bytes = VerifiedBytes(root, fixture.GetProperty("path").GetString()!, fixture.GetProperty("sha256").GetString()!);
            bool accepted = fixture.GetProperty("classification").GetString() == "accepted";
            Assert.Equal(accepted, ProductDocument.TryParse(bytes, out var document, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken));
            if (accepted) { Assert.Empty(diagnostics); Assert.Equal("root", document!.RootElement.LocalName); Assert.Equal(SnapshotReference(bytes), Snapshot(document)); }
            else { Assert.Null(document); Assert.Single(diagnostics); }
        }
    }
    [Fact]
    public void CorruptionMissingTraversalAndSiblingAreInfrastructureFailures()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "xemele-corpus-negative-" + Guid.NewGuid());
        Directory.CreateDirectory(temporary);
        try
        {
            File.WriteAllText(Path.Combine(temporary, "input"), "<r/>");
            string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("<r/>")));
            Assert.Equal("<r/>", Encoding.UTF8.GetString(VerifiedBytes(temporary, "input", hash)));
            Assert.Equal("<r/>", Encoding.UTF8.GetString(VerifiedBytes(temporary + Path.DirectorySeparatorChar, "input", hash)));
            Assert.Throws<InvalidDataException>(() => VerifiedBytes(temporary, temporary + "-sibling/input", hash));
            Assert.Throws<InvalidDataException>(() => VerifiedBytes(temporary, "input", new string('0', 64)));
            Assert.Throws<FileNotFoundException>(() => VerifiedBytes(temporary, "missing", hash));
            Assert.Throws<InvalidDataException>(() => VerifiedBytes(temporary, "../input", hash));

        }
        finally { Directory.Delete(temporary, true); }
    }
    [Fact]
    public void SymlinkResourceAndRootAreInfrastructureFailures()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "xemele-corpus-symlink-" + Guid.NewGuid());
        Directory.CreateDirectory(temporary);
        try
        {
            File.WriteAllText(Path.Combine(temporary, "input"), "<r/>");
            Directory.CreateDirectory(Path.Combine(temporary, "actual"));
            File.WriteAllText(Path.Combine(temporary, "actual/input"), "<r/>");
            try
            {
                File.CreateSymbolicLink(Path.Combine(temporary, "link"), Path.Combine(temporary, "input"));
                Directory.CreateSymbolicLink(Path.Combine(temporary, "root-link"), Path.Combine(temporary, "actual"));
            }
            catch (Exception error) when (error is PlatformNotSupportedException or UnauthorizedAccessException || (OperatingSystem.IsWindows() && error is IOException))
            {
                Assert.Skip("Symlink creation unavailable: " + error.GetType().Name + ": " + error.Message);
            }
            string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("<r/>")));
            Assert.Throws<InvalidDataException>(() => VerifiedBytes(temporary, "link", hash));
            Assert.Throws<InvalidDataException>(() => VerifiedBytes(Path.Combine(temporary, "root-link"), "input", hash));
            Assert.Throws<InvalidDataException>(() => VerifiedBytes(temporary, "root-link/input", hash));
        }
        finally { Directory.Delete(temporary, true); }
    }
    private static void Record(string prefix, string id, object result)
    {
        string directory = Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESULTS") ?? Path.Combine(Root, "artifacts/M1-corpus/case-results");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, prefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id))) + ".json"), JsonSerializer.Serialize(result));
    }
    public static IEnumerable<object[]> Accepted() => new[]
    {
        "<r/>",
        "<?xml version='1.0' standalone='yes'?><!--before--><?p data?><r a='a&#x9;&#xA;&#xD;&quot;&amp;&lt;'>A\r\nB\rC&amp;&#xD;&#x1F600;<![CDATA[x<y]]><!--inside--><?q value?><e/>tail</r><!--after-->",
        "<p:r a='1' xmlns:p='urn:p' xmlns='urn:d' p:b='2'><child/><plain xmlns=''/><p:e xmlns:p='urn:q'/></p:r>",
        "<xmlns xmlns:p='urn:x' p:xmlns='ok' xml:lang='en'><p:xmlns/></xmlns>",
        "<r>  <e/> \t\n </r>",
        "<r><![CDATA[]]><![CDATA[a]]>b<![CDATA[c]]></r>",
        "<r a='a\r\nb\tc'>𐀀éΩ&#x9;&#xA;&#xD;&gt;</r>",
        "<r><a><b/><c/></a><d/></r>"
    }.Select((xml, i) => new object[] { "authored-" + i, xml });
    [Theory]
    [MemberData(nameof(Accepted))]
    public void IndependentlyReadModelsSurviveAllTransportsAndRoundTrips(string id, string xml)
    {
        string expected = SnapshotReference(Encoding.UTF8.GetBytes(xml));
        var characters = ProductDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected, Snapshot(characters));
        Assert.Equal(expected, Snapshot(ProductDocument.Parse(xml.AsSpan(), cancellationToken: TestContext.Current.CancellationToken)));
        Assert.True(ProductDocument.TryParse(xml, out var tried, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken), id);
        Assert.Empty(diagnostics); Assert.Equal(expected, Snapshot(tried!));
        Assert.Equal(expected, Snapshot(ProductDocument.Parse(characters.ToXmlString(cancellationToken: TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken)));
        foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, false, true), new UnicodeEncoding(true, false, true) })
        foreach (bool bom in new[] { false, true })
        {
            string declaration = encoding.CodePage switch { 65001 => "utf-8", 1200 => "utf-16le", _ => "utf-16be" };
            string source = xml.StartsWith("<?xml", StringComparison.Ordinal) ? xml[(xml.IndexOf("?>", StringComparison.Ordinal) + 2)..] : xml;
            byte[] prefix = bom ? encoding.CodePage switch { 65001 => [0xef, 0xbb, 0xbf], 1200 => [0xff, 0xfe], _ => [0xfe, 0xff] } : [];
            byte[] bytes = [.. prefix, .. encoding.GetBytes("<?xml version='1.0' encoding='" + declaration + "'?>" + source)];
            Assert.Equal(expected, Snapshot(ProductDocument.Parse(bytes, cancellationToken: TestContext.Current.CancellationToken)));
            using var input = new ShortReadStream(bytes);
            Assert.Equal(expected, Snapshot(ProductDocument.Load(input, cancellationToken: TestContext.Current.CancellationToken))); Assert.True(input.CanRead);
            using var output = new MemoryStream();
            characters.Save(output, new() { Encoding = encoding, EmitByteOrderMark = bom }, TestContext.Current.CancellationToken);
            byte[] saved = output.ToArray();
            Assert.Equal(expected, SnapshotReference(saved));
            Assert.Equal(expected, Snapshot(ProductDocument.Parse(saved, cancellationToken: TestContext.Current.CancellationToken))); Assert.True(output.CanWrite);
        }
    }
    // Independent BCL reader oracle retains lexical kinds, explicit declarations,
    // preferred prefixes and insertion order. Only adjacent Text nodes coalesce:
    // M1 serializer deliberately erases adjacent Text boundaries.
    private static string SnapshotReference(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = XmlReader.Create(input, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = false, IgnoreComments = false, IgnoreProcessingInstructions = false });
        var rows = new List<string>();
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                rows.Add(Row("Element", reader.LocalName, reader.NamespaceURI, reader.Prefix));
                bool empty = reader.IsEmptyElement;
                var declarations = new List<string>(); var attributes = new List<string>();
                if (reader.MoveToFirstAttribute()) do
                {
                    if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/") declarations.Add(Row("Namespace", reader.Prefix == "xmlns" ? reader.LocalName : "", reader.Value));
                    else attributes.Add(Row("Attribute", reader.LocalName, reader.NamespaceURI, reader.Prefix, reader.Value));
                } while (reader.MoveToNextAttribute());
                rows.AddRange(declarations); rows.AddRange(attributes); reader.MoveToElement();
                if (empty) rows.Add(Row("End"));
            }
            else if (reader.NodeType == XmlNodeType.EndElement) rows.Add(Row("End"));
            else if (reader.NodeType is XmlNodeType.Text or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            { if (reader.Depth > 0) AddText(rows, reader.Value); }
            else if (reader.NodeType == XmlNodeType.CDATA) rows.Add(Row("CData", reader.Value));
            else if (reader.NodeType == XmlNodeType.Comment) rows.Add(Row("Comment", reader.Value));
            else if (reader.NodeType == XmlNodeType.ProcessingInstruction) rows.Add(Row("ProcessingInstruction", reader.LocalName, reader.Value));
        }
        return string.Join('\n', rows);
    }
    private static string Snapshot(ProductDocument document)
    {
        var rows = new List<string>();
        var stack = new Stack<(XmlNode Node, bool End)>();
        foreach (var child in ReverseChildren(document.DocumentNode)) stack.Push((child, false));
        while (stack.TryPop(out var item))
        {
            var node = item.Node;
            if (item.End) { rows.Add(Row("End")); continue; }
            if (node.Kind == XmlNodeKind.Element)
            {
                rows.Add(Row("Element", node.LocalName, node.NamespaceUri, node.Prefix));
                foreach (var ns in node.NamespaceDeclarations) rows.Add(Row("Namespace", ns.Prefix, ns.Value));
                foreach (var a in node.Attributes) rows.Add(Row("Attribute", a.LocalName, a.NamespaceUri, a.Prefix, a.Value));
                stack.Push((node, true)); foreach (var child in ReverseChildren(node)) stack.Push((child, false));
            }
            else if (node.Kind == XmlNodeKind.Text) AddText(rows, node.Value);
            else if (node.Kind == XmlNodeKind.ProcessingInstruction) rows.Add(Row(node.Kind.ToString(), node.LocalName, node.Value));
            else rows.Add(Row(node.Kind.ToString(), node.Value));
        }
        return string.Join('\n', rows);
    }
    private static IEnumerable<XmlNode> ReverseChildren(XmlNode node)
    {
        for (var child = node.LastChild; !child.IsDefault; child = child.PreviousSibling) yield return child;
    }
    private static string Row(params string[] fields) => JsonSerializer.Serialize(fields);
    private static void AddText(List<string> rows, string value)
    {
        if (rows.Count > 0 && rows[^1].StartsWith("[\"Text\",", StringComparison.Ordinal))
        { string prior = JsonSerializer.Deserialize<string[]>(rows[^1])![1]; rows[^1] = Row("Text", prior + value); }
        else rows.Add(Row("Text", value));
    }
    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }
}
