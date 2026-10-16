using System.Reflection;
using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8XsdWildcardComposition;

public sealed class CompositionTests
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly Uri Source = new("https://source.test/schema.xsd");
    private static readonly XmlLocation At = new(Source, 17, 4, null);
    private delegate XsdWildcard Compose(XsdWildcard a, XsdWildcard b, XmlLocation at);
    private static Compose Bind(bool union, ISchemaOperation op) => typeof(XsdCompilation)
        .GetMethod(union ? "Union" : "Intersection", BindingFlags.Instance | BindingFlags.NonPublic)!
        .CreateDelegate<Compose>(new XsdCompilation(new(), op));
    private static XsdWildcard W(bool negated, params string[] names) => new(negated, names, XsdProcessContents.Strict);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryLegalConstraintPairMatchesIndependentFiniteWitnessOracle(bool union)
    {
        XsdWildcard[] values = [W(true), W(true, ""), W(true, "urn:t", ""), W(true, "urn:u", ""), W(false), W(false, ""), W(false, "urn:t"), W(false, "urn:t", ""), W(false, "urn:u", "urn:v")];
        string[] witnesses = ["", "urn:t", "urn:u", "urn:v", "urn:unlisted"];
        foreach (var a in values)
        {
            foreach (var b in values)
            {
                var negated = union ? a.Negated || b.Negated : a.Negated && b.Negated;
                bool Allows(XsdWildcard w, string n) => w.Negated != w.Namespaces.Contains(n);
                bool Expected(string n) => union ? Allows(a, n) || Allows(b, n) : Allows(a, n) && Allows(b, n);
                var excluded = witnesses.Where(n => !Expected(n)).ToArray();
                var expressible = !negated || excluded.Length == 0 || excluded.Length <= 2 && excluded.Contains("");
                var call = Bind(union, new SchemaCompileOperation());
                if (!expressible)
                {
                    var error = Assert.Throws<SchemaCompileException>(() => call(a, b, At));
                    Assert.Equal(At, Assert.Single(error.Diagnostics).Location);
                }
                else
                {
                    var result = call(a, b, At);
                    Assert.Equal(negated, result.Negated);
                    foreach (var n in witnesses)
                    {
                        Assert.Equal(Expected(n), Allows(result, n));
                    }

                    Assert.Equal(result.Namespaces.Count, result.Namespaces.Distinct().Count());
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectInvalidUnionKeepsCharacterOrByteCompositionLocation(bool bytes)
    {
        const string body = "<xs:complexType name='B'><xs:anyAttribute namespace='##other'/></xs:complexType>\n<xs:complexType name='T'><xs:complexContent>\n <xs:extension base='t:B'><xs:anyAttribute namespace='##local'/></xs:extension></xs:complexContent></xs:complexType>";
        var context = new SchemaCompileContext(new()
        {
            BaseUri = Source
        });
        var xml = Wrap(body);
        SchemaDocumentScope scope;
        if (bytes)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            scope = context.Parse(stream);
        }
        else
        {
            scope = context.Parse(xml);
        }

        var error = Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(scope.Document, context.Options, context.Operation));
        var d = Assert.Single(error.Diagnostics);
        Assert.Equal("XSD.SchemaRestriction", d.Code);
        Assert.Equal(Source, d.Location.SourceUri);
        Assert.Equal(4, d.Location.LineNumber);
        Assert.Equal(2, d.Location.LinePosition);
        Assert.Equal(bytes, d.Location.ByteOffset.HasValue);
        if (bytes)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(xml.AsSpan(0, xml.IndexOf("<xs:extension", StringComparison.Ordinal))), d.Location.ByteOffset);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidIntermediateIsRejectedBeforeLaterFiniteIntersection(bool redirect)
    {
        var foreign = $"<xs:schema xmlns:xs='{Xs}' targetNamespace='urn:u'><xs:attributeGroup name='foreign'><xs:anyAttribute namespace='##other'/></xs:attributeGroup></xs:schema>";
        var principal = Wrap("<xs:import namespace='urn:u' schemaLocation='urn:foreign'/>\n<xs:attributeGroup name='local'><xs:anyAttribute namespace='##other'/></xs:attributeGroup>\n<xs:complexType name='T'><xs:attributeGroup ref='t:local'/>\n <xs:attributeGroup ref='u:foreign'/><xs:anyAttribute namespace='urn:v'/></xs:complexType>");
        var final = new Uri("https://redirect.test/final.xsd");
        var resolver = new Resolver(r => r.Uri.OriginalString == "urn:foreign" ? new(new MemoryStream(Encoding.UTF8.GetBytes(foreign)), r.Uri) : new(new MemoryStream(Encoding.UTF8.GetBytes(principal)), final));
        var context = new SchemaCompileContext(new()
        {
            BaseUri = Source,
            Resolver = resolver
        });
        var document = context.Parse(redirect ? Wrap("<xs:include schemaLocation='child.xsd'/>") : principal).Document;
        var error = Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(document, context.Options, context.Operation));
        var d = Assert.Single(error.Diagnostics);
        Assert.Equal("XSD.SchemaRestriction", d.Code);
        Assert.Equal(redirect ? final : Source, d.Location.SourceUri);
        Assert.Equal(5, d.Location.LineNumber);
        Assert.Equal(2, d.Location.LinePosition);
        Assert.Equal(redirect, d.Location.ByteOffset.HasValue);
    }

    [Theory]
    [InlineData(true, "urn:t", true, "", (int)XsdProcessContents.Skip)]
    [InlineData(true, "##local ##targetNamespace", true, "", (int)XsdProcessContents.Skip)]
    [InlineData(false, "urn:v", false, "urn:v", (int)XsdProcessContents.Skip)]
    public void AuthoredLocalWildcardControlsProcessContents(bool extension, string local, bool expectedNegated, string expected, int process)
    {
        var body = extension
            ? $"<xs:complexType name='B'><xs:anyAttribute namespace='##other' processContents='lax'/></xs:complexType><xs:complexType name='T'><xs:complexContent><xs:extension base='t:B'><xs:anyAttribute namespace='{local}' processContents='skip'/></xs:extension></xs:complexContent></xs:complexType>"
            : $"<xs:attributeGroup name='g'><xs:anyAttribute namespace='##other' processContents='strict'/></xs:attributeGroup><xs:complexType name='T'><xs:attributeGroup ref='t:g'/><xs:anyAttribute namespace='{local}' processContents='skip'/></xs:complexType>";
        var context = new SchemaCompileContext();
        var graph = new XmlSchemaCompiler().Compile(context.Parse(Wrap(body)).Document, context.Options, context.Operation);
        var w = graph.Types.Single(t => t.Name == new SchemaName("urn:t", "T")).AnyAttribute!;
        Assert.Equal(expectedNegated, w.Negated);
        Assert.Equal((XsdProcessContents)process, w.ProcessContents);
        if (local == "##local ##targetNamespace")
        {
            Assert.Empty(w.Namespaces);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(w.Namespaces));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExactWorkComponentsAndCancellationBoundCompositionItself(bool union)
    {
        var a = W(false, "urn:shared-long-name", "urn:left");
        var b = W(false, "urn:shared-long-name", "urn:right");
        var ledger = new Ledger();
        var result = Bind(union, ledger)(a, b, At);
        Assert.True(ledger.Work > 20);
        Assert.Equal(1 + result.Namespaces.Count, ledger.Components);
        var exact = new Ledger(ledger.Work, ledger.Components);
        Bind(union, exact)(a, b, At);
        Assert.True(exact.IsComplete);
        var workShort = new Ledger(ledger.Work - 1);
        Assert.Throws<SchemaCompileException>(() => Bind(union, workShort)(a, b, At));
        Assert.Equal(At, workShort.Last);
        var componentsShort = new Ledger(componentLimit: ledger.Components - 1);
        Assert.Throws<SchemaCompileException>(() => Bind(union, componentsShort)(a, b, At));
        Assert.Equal(At, componentsShort.Last);
        using var cancel = new CancellationTokenSource();
        var interrupted = new Ledger(token: cancel.Token) { OnCharge = n => { if (n == 12) { cancel.Cancel(); } } };
        Assert.Throws<OperationCanceledException>(() => Bind(union, interrupted)(a, b, At));
        Assert.Equal(12, interrupted.Calls);
        Assert.Equal(["urn:shared-long-name", "urn:left"], a.Namespaces);
        Assert.Equal(["urn:shared-long-name", "urn:right"], b.Namespaces);
    }

    private static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}' xmlns:t='urn:t' xmlns:u='urn:u' targetNamespace='urn:t'>\n{body}</xs:schema>";
    private sealed class Resolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return resolve(request);
        }
    }
    private sealed class Ledger(long? workLimit = null, int? componentLimit = null, CancellationToken token = default) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { MaxOperations = workLimit, MaxComponents = componentLimit, CancellationToken = token });
        internal long Work; internal int Components, Calls; internal XmlLocation Last; internal Action<int>? OnCharge;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long n, XmlLocation at)
        {
            Last = at;
            OnCharge?.Invoke(++Calls);
            var ok = inner.Charge(n, at);
            if (ok)
            {
                Work += n;
            }

            return ok;
        }
        public bool ChargeComponents(int n, XmlLocation at)
        {
            Last = at;
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
