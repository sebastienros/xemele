using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdFacade;

internal sealed class FacadeFixture
{
    internal const string Source = """
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
<xs:element name="root"><xs:complexType><xs:sequence><xs:element name="row" maxOccurs="unbounded">
<xs:complexType><xs:sequence><xs:element name="value" type="xs:int" default="01" minOccurs="0"/><xs:element name="fixed" type="xs:int" fixed="01" minOccurs="0"/></xs:sequence><xs:attribute name="n" type="xs:int" default="1"/><xs:attribute name="id" type="xs:ID"/><xs:attribute name="ref" type="xs:IDREF"/></xs:complexType>
</xs:element></xs:sequence></xs:complexType><xs:unique name="U"><xs:selector xpath="row"/><xs:field xpath="@n"/></xs:unique></xs:element>
<xs:element name="open"/><xs:element name="number" type="xs:int" nillable="true"/>
</xs:schema>
""";
    internal const string Instance = "<root><row n='1'/><row n='2'/></root>";
    internal const string Invalid = "<root><row n='bad'/></root>";
    private const string IncludeSource = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:include schemaLocation='urn:included'/></xs:schema>";
    private const string InvalidConstraint = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:simpleType name='P'><xs:restriction base='xs:int'><xs:pattern value='0[0-9]'/></xs:restriction></xs:simpleType><xs:element name='unused' type='P' default='01'/></xs:schema>";
    private const string NotationSource = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:notation name='N' public='notation'/><xs:simpleType name='NType'><xs:restriction base='xs:NOTATION'><xs:enumeration value='N'/></xs:restriction></xs:simpleType><xs:simpleType name='L'><xs:list itemType='NType'/></xs:simpleType><xs:element name='notations' type='L' default='N N N'/></xs:schema>";
    private readonly XmlSchema notationSchema;
    private readonly XmlDocument notationInstance;
    internal static readonly string[] Routes = ["validate-dom-valid", "validate-dom-invalid", "typed-default-fixed", "identity-duplicate", "document-ids", "long-instance", "wildcard-scopes", "nil-instance", "work-refusal", "validate-string", "validate-stream", "malformed-instance", "compile-dom", "compile-string", "compile-stream", "compile-include", "compile-resource-denied", "compile-invalid-constraint", "compile-notation-list", "validate-notation-list"];
    private readonly XmlSchema schema;
    private readonly XmlDocument source;
    private readonly XmlDocument[] documents;
    private readonly byte[] sourceBytes = Encoding.UTF8.GetBytes(Source), instanceBytes = Encoding.UTF8.GetBytes(Instance);
    private readonly ValidationOptions unlimited = new() { MaxOperations = null }, stopped = new() { MaxOperations = 1 };
    private readonly SchemaCompileOptions included = new() { Resolver = new Resolver() };
    private int consumed;
    internal FacadeFixture()
    {
        source = XmlDocument.Parse(Source);
        schema = XmlSchema.Compile(source);
        notationSchema = XmlSchema.Compile(NotationSource);
        notationInstance = XmlDocument.Parse("<notations>N N</notations>");
        var longInstance = "<root>" + string.Concat(Enumerable.Range(1, 64).Select(i => $"<row n='{i}'/>")) + "</root>";
        documents = [XmlDocument.Parse(Instance), XmlDocument.Parse(Invalid), XmlDocument.Parse("<root><row><value/><fixed>1</fixed></row></root>"), XmlDocument.Parse("<root><row/><row n='01'/></root>"), XmlDocument.Parse("<root><row n='1' id='a'/><row n='2' ref='a'/></root>"), XmlDocument.Parse(longInstance), XmlDocument.Parse("<open><unknown><nested/></unknown></open>"), XmlDocument.Parse("<number xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/>"), XmlDocument.Parse(Instance)];
    }
    internal object RunOwned(int route)
    {
        if (route == 19)
        {
            var notation = notationSchema.Validate(notationInstance, unlimited);
            if (!notation.IsValid || !notation.IsComplete)
            {
                throw new InvalidOperationException("Notation list validation failed.");
            }

            consumed += notation.Diagnostics.Count + 1;
            return notation;
        }
        if (route >= 12)
        {
            XmlSchema compiled;
            try
            {
                switch (route)
                {
                    case 12:
                        compiled = XmlSchema.Compile(source);
                        break;
                    case 13:
                        compiled = XmlSchema.Compile(Source);
                        break;
                    case 14:
                        using (var input = new MemoryStream(sourceBytes, false))
                        {
                            compiled = XmlSchema.Compile(input);
                            if (!input.CanRead)
                            {
                                throw new InvalidOperationException("Caller stream closed.");
                            }
                        }
                        break;
                    case 15:
                        compiled = XmlSchema.Compile(IncludeSource, included);
                        break;
                    case 16:
                        compiled = XmlSchema.Compile(IncludeSource);
                        break;
                    case 17:
                        compiled = XmlSchema.Compile(InvalidConstraint);
                        break;
                    case 18:
                        compiled = XmlSchema.Compile(NotationSource);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(route));
                }
            }
            catch (SchemaCompileException error) when (route is 16 or 17)
            {
                if (error.Diagnostics.Count == 0)
                {
                    throw new InvalidOperationException("Missing compile diagnostic.");
                }

                consumed += error.Diagnostics.Count + 1;
                return error;
            }
            if (route is 16 or 17)
            {
                throw new InvalidOperationException("Expected compile refusal did not occur.");
            }

            consumed++;
            return compiled;
        }
        ValidationResult result;
        if (route == 9)
        {
            result = schema.Validate(Instance, unlimited);
        }
        else if (route == 10)
        {
            using var input = new MemoryStream(instanceBytes, false);
            result = schema.Validate(input, unlimited);
            if (!input.CanRead)
            {
                throw new InvalidOperationException("Caller stream closed.");
            }
        }
        else
        {
            result = route == 11 ? schema.Validate("<root>", unlimited) : schema.Validate(documents[route], route == 8 ? stopped : unlimited);
        }

        bool valid = route is not (1 or 3 or 8 or 11), complete = route is not (8 or 11);
        if (result.IsValid != valid || result.IsComplete != complete)
        {
            throw new InvalidOperationException("Independent route expectation failed: " + Routes[route]);
        }

        consumed += result.Diagnostics.Count + 1;
        return result;
    }
    internal int Consume() => consumed;
    private sealed class Resolver : XmlResourceResolver
    {
        private readonly byte[] content = Encoding.UTF8.GetBytes(Source);
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(new MemoryStream(content, false), request.Uri);
        }
    }
}
internal sealed class FacadePrimitiveFixture
{
    private readonly PrimitiveOperation operation = new();
    private readonly string longPrefix = new('p', 2048);
    private readonly string qualified;
    private int consumed;
    internal FacadePrimitiveFixture() => qualified = longPrefix + ":N";
    internal void Run(int route)
    {
        if (route == 0)
        {
            var remaining = "a b c".AsSpan();
            var count = 0;
            while (!remaining.IsEmpty)
            {
                var end = XsdFacadeText.IndexOf(remaining, ' ', operation, default);
                if (end < 0)
                {
                    end = remaining.Length;
                }

                count++;
                remaining = end == remaining.Length ? [] : remaining[(end + 1)..];
            }
            if (count != 3)
            {
                throw new InvalidOperationException("List scan oracle failed.");
            }

            consumed += count;
        }
        else
        {
            if (XsdFacadeText.IndexOf(qualified, ':', operation, default) != 2048 ||
                XsdFacadeText.IndexOf(longPrefix, ':', operation, default) != -1)
            {
                throw new InvalidOperationException("QName delimiter oracle failed.");
            }

            consumed++;
        }
    }
    internal int Consume() => consumed;
    private sealed class PrimitiveOperation : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new(); public CancellationToken CancellationToken => default;
        public bool IsComplete => true; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation location) => true;
        public bool ChargeComponents(int count, XmlLocation location) => true;
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException();
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException();
    }
}
