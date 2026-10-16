#if UNRESOLVED_DRIVER
global using EntityBaseBenchmarks = Xemele.Benchmarks.UnresolvedReferenceBenchmarks;
#endif
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Syntax;
namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class UnresolvedReferenceBenchmarks
{
    private const string Prefix = "<!DOCTYPE r [<!ENTITY % p ''>%p;]>";
    private const string Chunk = "a&x;&y;🙂&𐀀;";
    private static readonly string LongName = new('n', 200);
    private readonly string document = Prefix + "<r>" + Chunk + "b&" + LongName + ";c</r>";
    private readonly XmlParseOptions options = new() { DtdProcessing = XmlDtdProcessing.Parse };
    private readonly OracleSink sink = new(false);
    [GlobalSetup]
    public void Setup()
    {
        EntityBaseProvenance.Gate("before");
        Check();
    }
    [GlobalCleanup] public void Cleanup() => EntityBaseProvenance.Gate("after");

    // Preserve the verified two-case driver's method names. This case executes
    // the actual parser and borrowed events, not an independently scripted model.
    [Benchmark]
    public object InternalNestedModel()
    {
        sink.Reset();
        using var core = new XmlParserCore(options);
        var result = core.Append(document, [], true, sink, default);
        if (result.Step != ScanStep.Completed || result.Consumed != document.Length || result.Diagnostic is not null ||
            sink.References != 4 || sink.Units != 5 || sink.Ended != 4)
        {
            throw new Exception("Complete actual event oracle.");
        }

        return sink;
    }
    [Benchmark] public object RedirectedComposition() => XmlDocument.Parse(document, options);
    public void Check()
    {
        InternalNestedModel();
        var dom = (XmlDocument)RedirectedComposition();
        var root = dom.RootElement;
        if (root.LocalName != "r" || root.NamespaceUri != "" || root.Prefix != "" || root.Parent != dom.DocumentNode ||
            dom.DocumentNode.FirstChild != root || dom.DocumentNode.LastChild != root || dom.UnresolvedReferences.Count != 4)
        {
            throw new Exception("Owned root/document oracle.");
        }

        foreach (var attribute in root.Attributes)
        {
            throw new Exception("Unexpected owned attribute.");
        }

        foreach (var declaration in root.NamespaceDeclarations)
        {
            throw new Exception("Unexpected owned namespace.");
        }

        string[] text = ["a", "🙂", "b", "c"];
        var children = new XmlNode[4];
        var childIndex = 0;
        XmlNode previous = default;
        foreach (var child in root.Children)
        {
            if (childIndex == 4 || child.Kind != XmlNodeKind.Text || child.Value != text[childIndex] ||
                child.Parent != root || child.PreviousSibling != previous || !child.FirstChild.IsDefault)
            {
                throw new Exception("Complete owned text/topology oracle.");
            }

            children[childIndex++] = child;
            previous = child;
        }
        if (childIndex != 4 || !children[3].NextSibling.IsDefault)
        {
            throw new Exception("Owned child count/tail oracle.");
        }

        string[] names = ["x", "y", "𐀀", LongName];
        int[] boundaries = [1, 1, 2, 3];
        int[] columns = [Prefix.Length + 5, Prefix.Length + 8, Prefix.Length + 12, Prefix.Length + 16];
        for (var i = 0; i < 4; i++)
        {
            var occurrence = dom.UnresolvedReferences[i];
            if (occurrence.Name != names[i] || occurrence.LexicalReference != "&" + names[i] + ";" ||
                occurrence.Sequence != i || occurrence.ChildBoundary != boundaries[i] || occurrence.Parent != root ||
                occurrence.PreviousSibling != children[boundaries[i] - 1] ||
                occurrence.Location != new XmlLocation(null, 1, columns[i], null))
            {
                throw new Exception("Complete owned occurrence oracle.");
            }
        }
        var type = dom.DocumentType;
        if (type is null || type.Name != "r" || type.SystemId is not null || type.PublicId is not null ||
            type.InternalSubset != "<!ENTITY % p ''>%p;" || dom.Dtd is not { } dtd || dtd.Elements.Count != 0 ||
            dtd.Attributes.Count != 0 || dtd.GeneralEntities.Count != 0 || dtd.ParameterEntities.Count != 1 || dtd.Notations.Count != 0)
        {
            throw new Exception("Complete owned DOCTYPE/table oracle.");
        }

        var parameter = dtd.ParameterEntities[0];
        if (parameter.Name != "p" || !parameter.IsParameter || parameter.ReplacementText != "" || parameter.IsExternal ||
            parameter.SystemId is not null || parameter.PublicId is not null || parameter.NotationName is not null ||
            parameter.Location != new XmlLocation(null, 1, 14, null))
        {
            throw new Exception("Complete owned PE declaration oracle.");
        }

        try
        {
            dom.ToXmlString();
            throw new Exception("Loss refusal absent.");
        }
        catch (InvalidOperationException error) when (error.Message == "Document contains unresolved entity references.") { }
        if (Environment.GetCommandLineArgs().Contains("--allocation"))
        {
            BorrowedAllocation();
        }
    }
    private void BorrowedAllocation()
    {
        using var core = new XmlParserCore(options);
        var borrowed = new OracleSink(true);
        if (core.Append(Prefix + "<r>", [], false, borrowed, default).Step != ScanStep.NeedInput)
        {
            throw new Exception("Borrowed initialization.");
        }

        for (var i = 0; i < 128; i++)
        {
            AppendBorrowed(core, borrowed);
        }

        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1024; i++)
        {
            AppendBorrowed(core, borrowed);
        }

        var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        if (bytes != 0 || borrowed.References != 1152 * 3 || borrowed.Units != 1152 * 3 || borrowed.Ended != 1152 * 2)
        {
            throw new Exception("0 B borrowed callback interval/counts.");
        }

        if (core.Append("</r>", [], true, borrowed, default).Step != ScanStep.Completed)
        {
            throw new Exception("Borrowed finalization.");
        }

        Console.WriteLine("UNRESOLVED_BORROWED " + JsonSerializer.Serialize(new
        {
            iterations = 1024,
            utf16PerOperation = Chunk.Length,
            bytes,
            coldAndOwnedExcluded = true
        }));
    }
    private static void AppendBorrowed(XmlParserCore core, OracleSink sink)
    {
        var result = core.Append(Chunk, [], false, sink, default);
        if (result.Step != ScanStep.NeedInput || result.Consumed != Chunk.Length || result.Diagnostic is not null)
        {
            throw new Exception("Borrowed append oracle.");
        }
    }
    private sealed class OracleSink(bool repeated) : IXmlEventSink, IXmlUnresolvedReferenceSink
    {
        internal int References, Units, Ended;
        private int stage, textOffset;
        private bool textOpen;
        internal void Reset()
        {
            References = Units = Ended = stage = textOffset = 0;
            textOpen = false;
        }
        private int ChunkStart => Prefix.Length + 3 + (repeated ? Ended / 2 * 11 : 0);
        private string ExpectedText => (repeated ? Ended % 2 : Ended) switch
        {
            0 => "a",
            1 => "🙂",
            2 => "b",
            3 => "c",
            _ => throw new Exception("Unexpected text token.")
        };
        private int TextColumn => ChunkStart + ((repeated ? Ended % 2 : Ended) switch { 0 => 1, 1 => 8, 2 => 12, 3 => 215, _ => 0 });
        private static void At(XmlLocation actual, int column)
        {
            if (actual != new XmlLocation(null, 1, column, null))
            {
                throw new Exception("Authored active scalar/location oracle.");
            }
        }
        public bool UnresolvedReference(XmlUnresolvedReferenceView reference)
        {
            var ordinal = repeated ? References % 3 : References;
            ReadOnlySpan<char> expected = ordinal switch
            {
                0 => "x",
                1 => "y",
                2 => "𐀀",
                3 => LongName,
                _ => throw new Exception("Unexpected reference.")
            };
            var completed = repeated ? References / 3 * 2 + (ordinal < 2 ? 1 : 2) : ordinal < 2 ? 1 : ordinal;
            if (stage != 3 || textOpen || Ended != completed || !reference.Name.SequenceEqual(expected) ||
                reference.LexicalReference.Length != expected.Length + 2 || reference.LexicalReference[0] != '&' ||
                reference.LexicalReference[^1] != ';' || !reference.LexicalReference[1..^1].SequenceEqual(expected))
            {
                throw new Exception("Authored borrowed identity/order oracle.");
            }

            var start = Prefix.Length + 3 + (repeated ? References / 3 * 11 : 0);
            At(reference.Location, start + (ordinal switch
            {
                0 => 2,
                1 => 5,
                2 => 9,
                _ => 13
            }));
            References++;
            return true;
        }
        public bool StartDocument(XmlLocation at)
        {
            if (stage != 0)
            {
                throw new Exception("Document order.");
            }

            At(at, 1);
            stage = 1;
            return true;
        }
        public bool Declaration(XmlDeclarationView view) => throw new Exception("Unexpected declaration.");
        public bool StartElement(XmlNameView name, bool empty, XmlLocation at)
        {
            if (stage != 1 || empty || !name.LocalName.SequenceEqual("r") || !name.NamespaceUri.IsEmpty || !name.Prefix.IsEmpty)
            {
                throw new Exception("Authored root oracle.");
            }

            At(at, Prefix.Length + 1);
            stage = 2;
            return true;
        }
        public bool NamespaceDeclaration(XmlNamespaceView view) => throw new Exception("Unexpected namespace.");
        public bool Attribute(XmlAttributeView view) => throw new Exception("Unexpected attribute.");
        public bool StartElementContent()
        {
            if (stage != 2)
            {
                throw new Exception("Content order.");
            }

            stage = 3;
            return true;
        }
        public bool EndElement(XmlNameView name, XmlLocation at)
        {
            if (stage != 3 || textOpen || !name.LocalName.SequenceEqual("r") || !name.NamespaceUri.IsEmpty || !name.Prefix.IsEmpty ||
                (repeated ? References % 3 != 0 || Ended != References / 3 * 2 : References != 4 || Ended != 4))
            {
                throw new Exception("Authored end/order oracle.");
            }

            At(at, Prefix.Length + 3 + (repeated ? References / 3 * 11 + 1 : 216));
            stage = 4;
            return true;
        }
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation at)
        {
            var expectedReferences = repeated ? Ended / 2 * 3 + (Ended % 2 == 0 ? 0 : 2) : Ended switch
            {
                0 => 0,
                1 => 2,
                2 => 3,
                3 => 4,
                _ => -1
            };
            if (stage != 3 || textOpen || kind != XmlTextKind.Text || !target.IsEmpty || References != expectedReferences)
            {
                throw new Exception("Authored text boundary/order oracle.");
            }

            At(at, TextColumn);
            textOpen = true;
            textOffset = 0;
            return true;
        }
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation at)
        {
            if (!textOpen || text.Length > ExpectedText.Length - textOffset || !text.SequenceEqual(ExpectedText.AsSpan(textOffset, text.Length)))
            {
                throw new Exception("Authored exact borrowed text oracle.");
            }

            var start = TextColumn;
            if (ended)
            {
                if (!text.IsEmpty || textOffset != ExpectedText.Length)
                {
                    throw new Exception("Completed text oracle.");
                }

                At(at, ChunkStart + ((repeated ? Ended % 2 : Ended) switch
                {
                    0 => 2,
                    1 => 9,
                    2 => 13,
                    _ => 216
                }));
                textOpen = false;
                Ended++;
            }
            else
            {
                At(at, start + textOffset);
                textOffset += text.Length;
                Units += text.Length;
            }
            return true;
        }
        public bool EndDocument(XmlLocation at)
        {
            if (stage != 4)
            {
                throw new Exception("End document order.");
            }

            At(at, Prefix.Length + 3 + (repeated ? References / 3 * 11 + 5 : 220));
            stage = 5;
            return true;
        }
    }
}
