using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class HookTests
{
    private sealed class Program : XsdIdentityProgram;
    private sealed class Hooks : IXsdIdentityFactory, IXsdIdentitySession
    {
        internal int Entered, Attributes, Left, Completed;
        public IXsdIdentitySession Create(XsdCompiledSchema schema, XsdIdentityProgram program, XmlDocument document) => this;
        public void EnterElement(XmlNode node, XsdElementDeclaration? declaration, ISchemaOperation op) => Entered++;
        public void Attribute(XmlNode owner, SchemaName name, XsdValidatedValue value, ISchemaOperation op) => Attributes++;
        public void LeaveElement(XmlNode node, XsdValidatedValue value, ISchemaOperation op) => Left++;
        public void Complete(ISchemaOperation op) => Completed++;
    }
    private sealed class NoCursor : IXsdContentMatcher
    {
        public IXsdContentCursor CreateCursor(XsdContentProgram program) => throw new InvalidOperationException("Unavailable declaration reached content cursor.");
    }
    [Fact]
    public void RequiredAbsenceCannotEmitAnyIdentitySuccessOrCreateCursor()
    {
        var (schema, builder, op) = Compile("<xs:element name='bad' type='absent' nillable='true' default='7'/>");
        var programs = new XsdContentCompiler().CompileAll(schema, op, new XmlFacetEvaluator(), builder);
        var hooks = new Hooks();
        var result = new XmlSchemaValidator().Validate(schema, XmlDocument.Parse("<bad xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/>", cancellationToken: TestContext.Current.CancellationToken), new(), programs, new NoCursor(), new XmlFacetEvaluator(), hooks, new Program(), builder.Seal());
        Missing(result);
        Assert.Equal(0, hooks.Entered + hooks.Attributes + hooks.Left + hooks.Completed);
    }
    [Fact]
    public void FrozenEvaluatorCannotAssessPartialGraphWithoutAvailabilityEnvelope()
    {
        var (schema, builder, op) = Compile("<xs:element name='bad' type='absent'/><xs:element name='good' type='xs:int'/>");
        var programs = new XsdContentCompiler().CompileAll(schema, op, new XmlFacetEvaluator(), builder);
        IXsdEvaluator evaluator = new XmlSchemaValidator();
        Assert.Throws<ArgumentException>(() => evaluator.Validate(schema, XmlDocument.Parse("<good>7</good>", cancellationToken: TestContext.Current.CancellationToken), new(), programs, new XsdContentMatcher(), new XmlFacetEvaluator(), null, null));
    }
}
