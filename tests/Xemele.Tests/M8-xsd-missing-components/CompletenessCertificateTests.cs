using Xemele.Validation;
using Xunit;
using Ledger = Xemele.Tests.M8XsdContent.OperationTests.Ledger;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class CompletenessCertificateTests
{
    private static XsdCompiledSchema Complete(string body = "")
    {
        var context = new SchemaCompileContext(new() { BaseUri = Basis });
        return new XmlSchemaCompiler().Compile(context.Parse(Wrap(body)).Document, context);
    }
    private static void Reject(XsdCompiledSchema graph)
    {
        var operation = new Counted();
        Assert.Throws<ArgumentException>(() => new XsdContentCompiler().CompileAll(graph, operation, new XmlFacetEvaluator()));
        Assert.True(operation.Work > 0); Assert.Null(operation.RemainingWork);
    }
    [Fact]
    public void MissingUnusedListWithNoElementsCannotObtainACompleteCertificate()
    {
        var (partial, _, _) = Compile("<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType>");
        Assert.Empty(partial.Elements); Reject(partial);
        // Removing all datatype wrappers must not hide an orphan definition.
        Reject(partial with { Types = [] });
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReplacingTheFrozenListOrRegistryCannotBorrowAnotherGraphsCertificate(bool replaceList)
    {
        var complete = Complete();
        var (partial, _, _) = Compile("<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType>");
        Reject(replaceList ? complete with { SimpleTypes = partial.SimpleTypes } : partial with { Datatypes = complete.Datatypes });
        var copied = new XsdCompiledSchema(partial.Elements, partial.Attributes, [], partial.SimpleTypes,
            partial.Identities, partial.Notations, partial.GlobalElements, partial.GlobalAttributes, complete.Datatypes);
        Reject(copied);
        Reject(partial with { Datatypes = new XsdDefinitionRegistry(partial.SimpleTypes) });
    }
    [Theory]
    [InlineData("null-type")] [InlineData("absent-type")] [InlineData("absent-head")] [InlineData("removed-types")]
    public void SimpleCertificateDoesNotCertifyElementTypeOrAffiliationReferences(string change)
    {
        var complete = Complete("<xs:element name='root' type='xs:int'/>"); var element = Assert.Single(complete.Elements);
        var altered = change switch
        {
            "null-type" => complete with { Elements = [element with { Type = null }] },
            "absent-type" => complete with { Elements = [element with { Type = new SchemaName("", "absent") }] },
            "absent-head" => complete with { Elements = [element with { SubstitutionGroup = new SchemaName("", "absent") }] },
            _ => complete with { Types = [] }
        };
        Reject(altered);
    }
    [Fact]
    public void CertificateMatchesOnlyThePostSnapshotListAndExactRegistryDefinitions()
    {
        var complete = Complete(); var registry = Assert.IsType<XsdDefinitionRegistry>(complete.Datatypes);
        Assert.True(registry.Certifies(complete.SimpleTypes));
        var copied = complete with { SimpleTypes = complete.SimpleTypes };
        Assert.NotSame(complete.SimpleTypes, copied.SimpleTypes); Assert.False(registry.Certifies(copied.SimpleTypes));
        var certified = new Counted(); ComponentAvailability.RequireComplete(complete, certified);
        Assert.Equal(0, certified.Work); Assert.Equal(2, certified.Polls); Assert.Equal(0, certified.Components);
        var fallback = new Counted(); ComponentAvailability.RequireComplete(copied, fallback);
        // 1 anySimpleType +19 primitive +22 derived atomic +3 builtin lists.
        Assert.Equal(45, fallback.Work); Assert.Equal(0, fallback.Components);
        var equivalent = complete with { Datatypes = new XsdDefinitionRegistry(complete.SimpleTypes) };
        var replaced = new Counted(); ComponentAvailability.RequireComplete(equivalent, replaced); Assert.Equal(45, replaced.Work);
        var changed = new XsdDefinitionRegistry(complete.SimpleTypes.Select(t => t with { }).ToArray());
        var graph = complete with { Datatypes = changed };
        Assert.Same(changed, changed.Certify(graph, new Counted())); Assert.False(changed.Certifies(graph.SimpleTypes));
    }
    [Fact]
    public void RegistryAndCertificateStorageAndInspectionHaveIndependentExactQuotas()
    {
        var complete = Complete(); var definitions = complete.SimpleTypes;
        // Registry:45 entry inspections; dictionary/wrapper/registry3 +45 entries.
        var storage = new Counted { WorkCap = 45, ComponentCap = 48 };
        var registry = XsdDefinitionRegistry.Create(definitions, storage);
        Assert.Equal(45, storage.Work); Assert.Equal(48, storage.Components);
        Assert.Throws<SchemaCompileException>(() => XsdDefinitionRegistry.Create(definitions, new Counted { WorkCap = 44 }));
        Assert.Throws<SchemaCompileException>(() => XsdDefinitionRegistry.Create(definitions, new Counted { ComponentCap = 47 }));
        var graph = complete with { Datatypes = registry };
        // Proof:45 definitions +3 item lookups; one immutable certificate wrapper.
        var proof = new Counted { WorkCap = 48, ComponentCap = 1 };
        var certified = registry.Certify(graph, proof);
        Assert.Equal(48, proof.Work); Assert.Equal(1, proof.Components); Assert.True(certified.Certifies(graph.SimpleTypes));
        Assert.Throws<SchemaCompileException>(() => registry.Certify(graph, new Counted { WorkCap = 47 }));
        Assert.Throws<SchemaCompileException>(() => registry.Certify(graph, new Counted { ComponentCap = 0 }));
        Assert.Throws<ArgumentException>(() => registry.Certify(complete, new Counted()));
    }
    [Theory]
    [InlineData("construction")] [InlineData("proof")] [InlineData("fallback")] [InlineData("certified")]
    public void NullCapCancellationCannotPublishOrSkipTheCertificateBoundary(string stage)
    {
        var complete = Complete(); using var cancellation = new CancellationTokenSource();
        var operation = new Ledger(token: cancellation.Token);
        Assert.Null(operation.RemainingWork);
        if (stage == "construction")
        {
            operation.OnCharge = _ => { if (operation.Work == 2) cancellation.Cancel(); };
            Assert.Throws<OperationCanceledException>(() => XsdDefinitionRegistry.Create(complete.SimpleTypes, operation));
        }
        else if (stage == "proof")
        {
            var registry = XsdDefinitionRegistry.Create(complete.SimpleTypes, new Counted());
            var graph = complete with { Datatypes = registry };
            operation.OnCharge = _ => { if (operation.Work == 2) cancellation.Cancel(); };
            Assert.Throws<OperationCanceledException>(() => registry.Certify(graph, operation));
        }
        else
        {
            var graph = stage == "fallback" ? complete with { SimpleTypes = complete.SimpleTypes } : complete;
            // On the certified path cancel at its final poll, after proof match.
            operation.OnCharge = call => { if (stage == "fallback" ? operation.Work == 2 : call == 2) cancellation.Cancel(); };
            Assert.Throws<OperationCanceledException>(() => ComponentAvailability.RequireComplete(graph, operation));
        }
        Assert.True(cancellation.IsCancellationRequested); Assert.True(operation.IsComplete);
    }
    [Fact]
    public void MutationDuringChargedRegistryConstructionCannotPublishASchema()
    {
        var context = new SchemaCompileContext(new() { BaseUri = Basis }); var document = context.Parse(Wrap("")).Document;
        var operation = new RegistryMutation(context.Operation, () => document.RootElement.SetAttribute("version", "changed"));
        Assert.Throws<InvalidOperationException>(() => ((IXsdCompiler)new XmlSchemaCompiler()).Compile(document, context.Options, operation));
        Assert.True(operation.Changed);
    }
    private sealed class RegistryMutation(ISchemaOperation inner, Action mutate) : ISchemaOperation
    {
        internal bool Changed;
        public XmlResourceLimits Limits => inner.Limits;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete;
        public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation at) => inner.Charge(work, at);
        public bool ChargeComponents(int count, XmlLocation at)
        {
            bool accepted = inner.ChargeComponents(count, at);
            // Compiler registry factory charges its dictionary/wrapper/object
            // together, after the preceding source-integrity check.
            if (accepted && count == 3 && !Changed) { Changed = true; mutate(); }
            return accepted;
        }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
        public void Refuse(string code, string message, XmlLocation at) => inner.Refuse(code, message, at);
    }
}
