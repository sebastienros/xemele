using Xemele.Validation;
using Xunit;
using Ledger = Xemele.Tests.M8XsdContent.OperationTests.Ledger;
using static Xemele.Tests.M8XsdParticleNormalization.NeighborTests;
namespace Xemele.Tests.M8XsdParticleNormalization;

public sealed class BoundaryTests
{
    internal static XsdCompiledSchema Graph(string source)
    {
        var context = new SchemaCompileContext(new() { BaseUri = new Uri("https://particles.test/main.xsd") });
        return new XmlSchemaCompiler().Compile(context.Parse(source).Document, context);
    }
    internal static XsdContentProgram Compile(XsdCompiledSchema graph, ISchemaOperation operation) => new XsdContentCompiler().Compile(graph, graph.Types.Single(t => t.Name.LocalName == "D"), operation);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExactAggregateWorkAndComponentsCoverFreshExpansion(bool singleton)
    {
        var graph = Graph(singleton ? Singleton() : Nested("dcba")); var measured = new Ledger(); Compile(graph, measured);
        Assert.True(measured.Work > 0); Assert.True(measured.Components > 0);
        Assert.NotNull(Compile(graph, new Ledger(measured.Work, measured.Components)));
        var shortWork = new Ledger(measured.Work - 1); Assert.Throws<SchemaCompileException>(() => Compile(graph, shortWork));
        Assert.False(shortWork.IsComplete); Assert.Contains(shortWork.Diagnostics, d => d.Code == "SCHEMA.Limit");
        Assert.Throws<SchemaCompileException>(() => Compile(graph, new Ledger(componentLimit: measured.Components - 1)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EntryAndInProgressCancellationRemainBounded(bool entry)
    {
        var graph = Graph(Nested("dcba")); using var cancel = new CancellationTokenSource(); if (entry) cancel.Cancel();
        var operation = new Ledger(token: cancel.Token) { OnCharge = n => { if (n == 25) cancel.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => Compile(graph, operation)); Assert.Equal(entry ? 0 : 25, operation.Calls);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualGroupUpaKeepsCharacterAndByteSourceLocation(bool bytes)
    {
        string source = Wrap("\n<xs:group name='g'>\n<xs:choice>\n<xs:element name='x'/>\n<xs:element name='x'/>\n</xs:choice></xs:group>\n<xs:element name='root'><xs:complexType><xs:group ref='g'/></xs:complexType></xs:element>");
        var options = new SchemaCompileOptions { BaseUri = new Uri("https://particles.test/main.xsd") }; SchemaCompileException error;
        if (bytes) { using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(source)); error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(stream, options)); Assert.True(stream.CanRead); }
        else error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, options));
        var diagnostic = Assert.Single(error.Diagnostics); Assert.Equal("XSD.SchemaRestriction", diagnostic.Code); Assert.Contains("unique particle attribution", diagnostic.Message);
        Assert.Equal(options.BaseUri, diagnostic.Location.SourceUri); Assert.Equal(7, diagnostic.Location.LineNumber); Assert.Equal(41, diagnostic.Location.LinePosition);
        Assert.Equal(bytes ? (long?)source.IndexOf("<xs:group ref=", StringComparison.Ordinal) : null, diagnostic.Location.ByteOffset);
    }
    [Fact]
    public void OriginalNamedGroupPassesOneFrozenWholeGraphContentJoin()
    {
        string source = File.ReadAllText(Path.Combine(OriginalTests.Root(), "msData/additional/addB194.xsd"));
        var graph = Graph(source); var carrier = graph.Types.Single(t => t.Name.LocalName == "#model-group:THDMVV");
        var measured = new Ledger(10_000_000, 100_000);
        Assert.NotNull(new XsdContentCompiler().Compile(graph, carrier, measured));
        TestContext.Current.TestOutputHelper!.WriteLine($"types={graph.Types.Count}; oneContentJoinWork={measured.Work}; oneContentJoinComponents={measured.Components}; facadeRepeats={graph.Types.Count}");
    }
    [Fact]
    public void UnusedCarrierStillChargesWorkAndPreservesCallerExceptions()
    {
        var graph = Graph(Wrap("<xs:group name='g'><xs:choice><xs:element name='x'/><xs:element name='x'/></xs:choice></xs:group>"));
        var carrier = graph.Types.Single(t => t.Name.LocalName == "#model-group:g");
        var measured = new Ledger(); var compiler = new XsdContentCompiler(); Assert.NotNull(compiler.Compile(graph, carrier, measured));
        Assert.True(measured.Work > 0); Assert.NotNull(compiler.Compile(graph, carrier, new Ledger(measured.Work, measured.Components)));
        Assert.Throws<SchemaCompileException>(() => compiler.Compile(graph, carrier, new Ledger(measured.Work - 1)));
        var caller = new InvalidOperationException("caller"); Assert.Same(caller, Assert.Throws<InvalidOperationException>(() => compiler.Compile(graph, carrier, new Ledger { OnCharge = _ => throw caller })));
    }
}
