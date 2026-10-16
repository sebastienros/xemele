using Xemele.Validation;
using Xunit;
using P = Xemele.Tests.M8XsdValidate.EvaluatorTests;
namespace Xemele.Tests.M8XsdContentFailureAssessment;

public sealed class BoundaryTests
{
    private const string Xml = "<r><u/><uuu/></r>";
    private static XmlResourceLimits Unlimited => new() { Depth = null, TokenCharacters = null, DocumentCharacters = null, Diagnostics = null, EvaluationWork = null, AttributesPerElement = null };
    [Theory]
    [InlineData("depth", 2, true)]
    [InlineData("depth", 1, false)]
    [InlineData("token", 3, true)]
    [InlineData("token", 2, false)]
    [InlineData("document", 5, true)]
    [InlineData("document", 4, false)]
    public void RecoveryRetainsExactResourceBoundaries(string limit, int cap, bool complete)
    {
        var limits = limit switch
        {
            "depth" => Unlimited with { Depth = cap },
            "token" => Unlimited with { TokenCharacters = cap },
            _ => Unlimited with { DocumentCharacters = cap }
        };
        var result = P.Prepare(ContinuationTests.Root).Run(Xml, new()
        {
            Limits = limits,
            MaxOperations = null
        });
        Assert.Equal(complete, result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal(complete ? "XSD.Content" : "VALIDATION.Limit", result.Diagnostics[^1].Code);
    }
    [Theory]
    [InlineData("MaxOperations")]
    [InlineData("EvaluationWork")]
    [InlineData("Both")]
    public void RecoveryUsesOneSharedWorkLedgerAtExactBoundary(string limit)
    {
        var p = P.Prepare(ContinuationTests.Root);
        const long cap = 1_000_000;
        var hook = new Hook();
        Assert.True(p.Run(Xml, new()
        {
            MaxOperations = cap,
            Limits = Unlimited
        }, hook).IsComplete);
        var work = cap - hook.Remaining!.Value;
        Assert.True(work > 0);
        ValidationOptions Options(long value) => new()
        {
            MaxOperations = limit == "EvaluationWork" ? null : value,
            Limits = Unlimited with
            {
                EvaluationWork = limit == "MaxOperations" ? null : value
            }
        };
        Assert.True(p.Run(Xml, Options(work), new Hook()).IsComplete);
        var result = p.Run(Xml, Options(work - 1), new Hook());
        Assert.False(result.IsComplete);
        Assert.Equal("VALIDATION.Limit", result.Diagnostics[^1].Code);
    }
    [Fact]
    public void CancellationDuringRecoveryWithAllNullCapsPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var hook = new Hook { OnEnter = (node, _) => { if (node.LocalName == "uuu") { cancellation.Cancel(); } } };
        Assert.Throws<OperationCanceledException>(() => P.Prepare(ContinuationTests.Root).Run(Xml, new() { Limits = Unlimited, MaxOperations = null, CancellationToken = cancellation.Token }, hook));
        Assert.Equal(new[] { "r", "u", "uuu" }, hook.Entered);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerExceptionAndMutationDuringRecoveryPropagate(bool mutation)
    {
        var p = P.Prepare(ContinuationTests.Root);
        var doc = XmlDocument.Parse(Xml, cancellationToken: TestContext.Current.CancellationToken);
        var exception = new SchemaCompileException([new("CALLER.Test", XmlDiagnosticSeverity.Error, "caller", default)]);
        var hook = new Hook { OnEnter = (node, _) => { if (node.LocalName == "uuu") { if (mutation) { node.SetAttribute("changed", "yes"); } else { throw exception; } } } };
        if (mutation)
        {
            Assert.Throws<InvalidOperationException>(() => p.Run(doc, factory: hook));
        }
        else
        {
            Assert.Same(exception, Assert.Throws<SchemaCompileException>(() => p.Run(doc, factory: hook)));
        }

        Assert.Equal(new[] { "r", "u", "uuu" }, hook.Entered);
    }
    [Theory]
    [InlineData("skip")]
    [InlineData("strict")]
    [InlineData("id")]
    public void FailureOutputsAreIgnoredAndOnlyFailedCursorIsBypassed(string output)
    {
        var p = P.Prepare(ContinuationTests.Root + "<xs:element name='n' type='xs:int'/>");
        var matcher = new FailureMatcher(p.Programs[p.Schema.Elements[p.Schema.GlobalElements[new("", "r")]].Type!.Value], output, null);
        var result = new XmlSchemaValidator().Validate(p.Schema, XmlDocument.Parse("<r><n>bad</n><u><n>bad</n></u></r>", cancellationToken: TestContext.Current.CancellationToken), new(), p.Programs, matcher, p.Facets, null, null);
        Assert.True(result.IsComplete);
        Assert.Equal(3, result.Diagnostics.Count);
        Assert.Equal(1, matcher.Accepts);
        Assert.Equal(0, matcher.Completions);
        Assert.Equal(2, result.Diagnostics.Count(d => d.Code.StartsWith("XSD.Datatype", StringComparison.Ordinal)));
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "XSD.UndeclaredElement");
    }
    [Fact]
    public void PollAfterRejectedAcceptPrecedesSemanticRecovery()
    {
        var p = P.Prepare(ContinuationTests.Root);
        using var cancellation = new CancellationTokenSource();
        var matcher = new FailureMatcher(p.Programs[p.Schema.Elements[p.Schema.GlobalElements[new("", "r")]].Type!.Value], "strict", cancellation);
        Assert.Throws<OperationCanceledException>(() => new XmlSchemaValidator().Validate(p.Schema, XmlDocument.Parse("<r><u/></r>", cancellationToken: TestContext.Current.CancellationToken), new() { Limits = Unlimited, MaxOperations = null, CancellationToken = cancellation.Token }, p.Programs, matcher, p.Facets, null, null));
        Assert.Equal(1, matcher.Accepts);
        Assert.Equal(0, matcher.Completions);
    }
    private sealed class FailureMatcher(XsdContentProgram root, string output, CancellationTokenSource? cancellation) : IXsdContentMatcher, IXsdContentCursor
    {
        internal int Accepts, Completions;
        public IXsdContentCursor CreateCursor(XsdContentProgram program) => ReferenceEquals(root, program) ? this : new XsdContentMatcher().CreateCursor(program);
        public bool Accept(SchemaName name, ISchemaOperation operation, out int? id, out XsdProcessContents policy)
        {
            Accepts++;
            id = output == "id" ? int.MaxValue : null;
            policy = output == "skip" ? XsdProcessContents.Skip : XsdProcessContents.Strict;
            cancellation?.Cancel();
            return false;
        }
        public bool Complete(ISchemaOperation operation)
        {
            Completions++;
            throw new InvalidOperationException("Failed cursor completion must not be invoked.");
        }
    }
    private sealed class Hook : IXsdIdentityFactory, IXsdIdentitySession
    {
        internal Action<XmlNode, ISchemaOperation>? OnEnter; internal long? Remaining; internal List<string> Entered = [];
        public IXsdIdentitySession Create(XsdCompiledSchema schema, XsdIdentityProgram program, XmlDocument document) => this;
        public void EnterElement(XmlNode node, XsdElementDeclaration? declaration, ISchemaOperation operation)
        {
            Entered.Add(node.LocalName);
            OnEnter?.Invoke(node, operation);
        }
        public void Attribute(XmlNode owner, SchemaName name, XsdValidatedValue value, ISchemaOperation operation)
        {
        }
        public void LeaveElement(XmlNode node, XsdValidatedValue value, ISchemaOperation operation)
        {
        }
        public void Complete(ISchemaOperation operation) => Remaining = operation.RemainingWork;
    }
}
