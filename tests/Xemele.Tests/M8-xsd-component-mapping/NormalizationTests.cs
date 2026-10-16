using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdComponentMapping;

public sealed class NormalizationTests
{
    private static readonly Uri Source = new("https://mapping.test/normalize.xsd");
    [Theory]
    [InlineData("name")]
    [InlineData("id")]
    [InlineData("qname")]
    [InlineData("wildcard")]
    public void WhitespaceIsChargedDuringActualNormalizationWithNullCapCancellation(string route)
    {
        const int padding = 4096;
        var token = route switch
        {
            "name" => "Name",
            "id" => "ID",
            "qname" => "xs:string",
            _ => ""
        };
        var padded = new string(' ', padding) + token + new string(' ', padding);
        string Schema(string value) => route switch
        {
            "name" => NeighborTests.Wrap($"<xs:element name='{value}'/>"),
            "id" => NeighborTests.Wrap("<xs:element name='r'/>", $"id='{value}'"),
            "qname" => NeighborTests.Wrap($"<xs:element name='r' type='{value}'/>"),
            _ => NeighborTests.Wrap($"<xs:element name='r'><xs:complexType><xs:anyAttribute namespace='{value}'/></xs:complexType></xs:element>"),
        };
        var c = new SchemaCompileContext(new()
        {
            BaseUri = Source
        });
        var plain = c.Parse(Schema(token)).Document;
        var document = c.Parse(Schema(padded)).Document;
        var compiler = new XmlSchemaCompiler();
        var baseline = new Ledger();
        var observed = new Ledger();
        compiler.Compile(plain, c.Options, baseline);
        compiler.Compile(document, c.Options, observed);
        // The declaration name maps twice (registration and element declaration).
        // Every padded input gets one charged canonical pass and one charged
        // normalization pass; no later graph work varies with lexical whitespace.
        var expectedDelta = route == "name" ? 2 * (4L * padding + token.Length) : 4L * padding + token.Length;
        Assert.Equal(expectedDelta, observed.Work - baseline.Work);
        Assert.NotNull(compiler.Compile(document, c.Options, new SchemaCompileOperation(new()
        {
            MaxOperations = observed.Work,
            MaxComponents = observed.Components
        })));
        var shortWork = new SchemaCompileOperation(new()
        {
            MaxOperations = observed.Work - 1
        });
        Assert.Throws<SchemaCompileException>(() => compiler.Compile(document, c.Options, shortWork));
        Assert.False(shortWork.IsComplete);
        using var cancellation = new CancellationTokenSource();
        // Initial zero-charge marks the helper's entry. Cancellation occurs after
        // the complete canonical scan, 128 scalars into actual normalization.
        var line = route == "id" ? 1 : 2;
        var cancel = new Ledger(cancellation.Token, line, padded.Length + 128) { Cancel = cancellation };
        Assert.Throws<OperationCanceledException>(() => compiler.Compile(document, c.Options, cancel));
        Assert.Equal(padded.Length + 128, cancel.SinceEntry);
        Assert.Equal(Source, cancel.Last.SourceUri);
        Assert.Equal(line, cancel.Last.LineNumber);
        var stopped = new SchemaCompileOperation(new()
        {
            MaxOperations = cancel.Work
        });
        var error = Assert.Throws<SchemaCompileException>(() => compiler.Compile(document, c.Options, stopped));
        Assert.Equal("SCHEMA.Limit", error.Diagnostics[^1].Code);
        Assert.Equal(cancel.Last, error.Diagnostics[^1].Location);
    }
    private sealed class Ledger(CancellationToken token = default, int line = -1, long cancelAt = -1) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null }, CancellationToken = token });
        internal long Work, SinceEntry; internal int Components; internal XmlLocation Last; internal CancellationTokenSource? Cancel;
        private bool entered;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long n, XmlLocation at)
        {
            if (at.LineNumber == line)
            {
                if (n == 0)
                {
                    entered = true;
                    SinceEntry = 0;
                }
                else if (entered)
                {
                    SinceEntry += n;
                    if (SinceEntry == cancelAt)
                    {
                        Cancel?.Cancel();
                    }
                }
                Last = at;
            }
            var ok = inner.Charge(n, at);
            if (ok)
            {
                Work += n;
            }

            return ok;
        }
        public bool ChargeComponents(int n, XmlLocation at)
        {
            var ok = inner.ChargeComponents(n, at);
            if (ok)
            {
                Components += n;
            }

            return ok;
        }
        public bool AddDiagnostic(XmlDiagnostic d) => inner.AddDiagnostic(d); public void Refuse(string c, string m, XmlLocation at) => inner.Refuse(c, m, at);
    }
}
