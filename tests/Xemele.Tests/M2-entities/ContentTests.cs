using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class ContentTests
{
    [Theory]
    [InlineData("plain", "owned", "<r>owned</r>")]
    [InlineData("markup", "<b>text&amp;more</b>", "<r><b>text&more</b></r>")]
    [InlineData("pair", "\U0001F642", "<r>\U0001F642</r>")]
    public void ParsedInternalContentAtEveryPrimarySplitAndCallbackPause(string name, string replacement, string expected)
    {
        var input = "<r>&" + name + ";</r>";
        var context = Context(Entity(name, replacement));
        for (var split = 0; split <= input.Length; split++)
        {
            var baseline = Read(input, context, split, -1);
            Assert.Equal(expected, baseline.Output.ToString());
            Assert.Null(baseline.Diagnostic);
            for (var pause = 0; pause < baseline.Calls; pause++)
            {
                var paused = Read(input, context, split, pause);
                Assert.Null(paused.Diagnostic);
                Assert.Equal(expected, paused.Output.ToString());
                Assert.Equal(baseline.Calls, paused.Calls);
            }
        }
    }

    [Theory]
    [InlineData("&e;", "XML.EntityCycle")]
    [InlineData("<b>", "XML.Syntax")]
    [InlineData("</r><r>", "XML.Syntax")]
    [InlineData("<", "XML.Syntax")]
    [InlineData("&amp", "XML.Syntax")]
    public void EntityRecursionAndSourceBoundaryFailuresAreStructured(string replacement, string expected)
    {
        var result = Read("<r>&e;</r>", Context(Entity("e", replacement)), 5, -1);
        Assert.Equal(expected, result.Diagnostic!.Code);
    }

    [Fact]
    public void NestedExpansionAndTransientScalarDepthUseOneLedger()
    {
        var context = Context(Entity("a", "&b;"), Entity("b", "&#65;"));
        var exact = new XmlParseOptions { Limits = new() { EntityDepth = 3, ExpandedCharacters = 9 } };
        // a replacement has 3 scalars, b replacement 5, numeric result 1.
        var accepted = Read("<r>&a;</r>", context, 0, -1, exact);
        Assert.Null(accepted.Diagnostic);
        Assert.Equal("<r>A</r>", accepted.Output.ToString());
        Assert.Equal("XML.ResourceLimit", Read("<r>&a;</r>", context, 0, -1,
            exact with
            {
                Limits = exact.Limits with
                {
                    ExpandedCharacters = 8
                }
            }).Diagnostic!.Code);
        Assert.Equal("XML.ResourceLimit", Read("<r>&a;</r>", context, 0, -1,
            exact with
            {
                Limits = exact.Limits with
                {
                    EntityDepth = 2
                }
            }).Diagnostic!.Code);
    }

    [Fact]
    public void EntityUnitsDoNotConsumePrimaryDocumentQuota()
    {
        const string input = "<r>&e;</r>";
        var result = Read(input, Context(Entity("e", new string('x', 1000))), 3, -1,
            new()
            {
                Limits = new()
                {
                    DocumentCharacters = input.Length
                }
            });
        Assert.Null(result.Diagnostic);
        Assert.Equal("<r>" + new string('x', 1000) + "</r>", result.Output.ToString());
    }

    [Fact]
    public void StandaloneRejectsExternalDeclarationWithoutResolverWork()
    {
        var result = Read("<?xml version='1.0' standalone='yes'?><r>&e;</r>",
            Context(Entity("e", "internal") with
            {
                IsExternal = true
            }), 10, -1);
        Assert.Equal("XML.Standalone", result.Diagnostic!.Code);
    }

    private static DtdEntityDeclaration Entity(string name, string replacement) => new(name, false, replacement, null, null, null, false, default);
    private static XmlEntityContext Context(params DtdEntityDeclaration[] entities) => new(new([], [], entities, [], []));

    private static RecordingSink Read(string input, IXmlEntityContext context, int split, int pause, XmlParseOptions? options = null)
    {
        using var core = new XmlParserCore(options ?? new(), context);
        var sink = new RecordingSink(pause);
        var offset = 0;
        var first = true;
        while (true)
        {
            var end = first ? split : input.Length;
            var result = core.Append(input.AsSpan(offset, end - offset), [], !first, sink, TestContext.Current.CancellationToken);
            offset += result.Consumed;
            if (result.Step == ScanStep.Rejected)
            {
                sink.Diagnostic = result.Diagnostic;
                return sink;
            }
            if (result.Step == ScanStep.Completed)
            {
                Assert.Equal(input.Length, offset);
                return sink;
            }
            if (result.Step == ScanStep.NeedInput)
            {
                first = false;
            }
        }
    }

    internal sealed class RecordingSink(int pause = -1) : IXmlEventSink
    {
        internal readonly StringBuilder Output = new();
        internal int Calls;
        internal XmlDiagnostic? Diagnostic;
        private bool Next() => Calls++ != pause;
        public bool StartDocument(XmlLocation location) => Next();
        public bool Declaration(XmlDeclarationView declaration) => Next();
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
        {
            Output.Append('<').Append(name.LocalName).Append('>');
            return Next();
        }
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => Next();
        public bool Attribute(XmlAttributeView attribute) => Next();
        public bool StartElementContent() => Next();
        public bool EndElement(XmlNameView name, XmlLocation location)
        {
            Output.Append("</").Append(name.LocalName).Append('>');
            return Next();
        }
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => Next();
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location)
        {
            Output.Append(text);
            return Next();
        }
        public bool EndDocument(XmlLocation location) => Next();
    }
}
