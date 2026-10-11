using Xemele;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdContent;

internal sealed class ContentFixture
{
    internal static readonly string[] Routes = ["dictionary name baseline", "borrowed compiled name hit/miss", "warm unbounded declaration cursor", "warm nullable group cursor", "warm wildcard cursor", "owned lazy cursor object", "owned cursor initialization and match", "owned all-type content compilation", "real XML compiler/content composition", "owned UPA diagnostics"];
    internal const string Source = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='item' type='xs:string'/><xs:element name='根' type='xs:string' substitutionGroup='item'/><xs:complexType name='T'><xs:sequence><xs:element ref='item' minOccurs='0' maxOccurs='unbounded'/></xs:sequence></xs:complexType><xs:complexType name='G'><xs:sequence minOccurs='0' maxOccurs='unbounded'><xs:element name='a' minOccurs='0'/><xs:element name='b' minOccurs='0'/></xs:sequence></xs:complexType><xs:complexType name='W'><xs:sequence><xs:any namespace='##other' processContents='lax' minOccurs='0' maxOccurs='unbounded'/></xs:sequence></xs:complexType><xs:element name='root' type='T'/></xs:schema>";
    internal const string Invalid = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:complexType name='T'><xs:choice><xs:element name='a'/><xs:element name='a'/></xs:choice></xs:complexType></xs:schema>";
    internal const string Instance = "<root><item>one</item><根>two</根><item>three</item></root>";
    internal readonly SchemaCompileOptions Options = new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } };
    private readonly XsdCompiledSchema graph, invalid;
    private readonly XsdTypeDefinition type;
    private readonly XsdContentProgram program, groupProgram, wildcardProgram;
    private readonly ContentNode nameNode;
    private readonly Dictionary<SchemaName, int> baseline;
    private readonly SchemaName[] candidates = [new("", "item"), new("", "unknown"), new("", "根"), new("", "item-more"), new("urn:other", "item")];
    private readonly IXsdContentCursor declarationCursor, groupCursor, wildcardCursor;
    private readonly ISchemaOperation warmOperation;
    private int sink;
    internal ContentFixture()
    {
        graph = ParseCompile(Source); invalid = ParseCompile(Invalid);
        type = graph.Types.Single(t => t.Name == new SchemaName("", "T"));
        program = NewProgram(); groupProgram = CompileNamed("G"); wildcardProgram = CompileNamed("W");
        nameNode = ((CompiledContent)program).Nodes[0];
        baseline = new() { [new("", "item")] = graph.GlobalElements[new("", "item")], [new("", "根")] = graph.GlobalElements[new("", "根")] };
        warmOperation = new SchemaCompileOperation(Options);
        var matcher = new XsdContentMatcher(); declarationCursor = matcher.CreateCursor(program); groupCursor = matcher.CreateCursor(groupProgram); wildcardCursor = matcher.CreateCursor(wildcardProgram);
        // Preparation is an owned setup boundary, separated from warm primitives.
        for (int route = 0; route < 5; route++) for (int i = 0; i < 32; i++) Run(route);
    }
    private XsdCompiledSchema ParseCompile(string text)
    { var context = new SchemaCompileContext(Options); return new XmlSchemaCompiler().Compile(context.Parse(text).Document, context.Options, context.Operation); }
    private XsdContentProgram CompileNamed(string name)
        => new XsdContentCompiler().Compile(graph, graph.Types.Single(t => t.Name == new SchemaName("", name)), new SchemaCompileOperation(Options));
    internal XsdContentProgram NewProgram() => new XsdContentCompiler().Compile(graph, type, new SchemaCompileOperation(Options));
    internal IXsdContentCursor NewInitializedCursor()
    {
        var cursor = new XsdContentMatcher().CreateCursor(program);
        if (!cursor.Accept(new("", "item"), new SchemaCompileOperation(Options), out int? id, out var policy) || id != baseline[new("", "item")] || policy != XsdProcessContents.Strict)
            throw new InvalidOperationException("Owned cursor control failed.");
        return cursor;
    }
    internal void Run(int route)
    {
        switch (route)
        {
            case 0:
                foreach (var candidate in candidates)
                {
                    bool found = baseline.TryGetValue(candidate, out int id);
                    if (found != (candidate.NamespaceUri.Length == 0 && candidate.LocalName is "item" or "根")) throw new InvalidOperationException("Baseline semantics changed.");
                    sink ^= found ? id : -1;
                }
                break;
            case 1:
                foreach (var candidate in candidates)
                {
                    bool found = nameNode.Matches(candidate, warmOperation, out int? id, out var policy);
                    if (found != baseline.TryGetValue(candidate, out int expected) || found && (id != expected || policy != XsdProcessContents.Strict)) throw new InvalidOperationException("Compiled name semantics changed.");
                    sink ^= id ?? -1;
                }
                break;
            case 2:
                if (!declarationCursor.Accept(new("", "item"), warmOperation, out int? declaration, out var contents) || declaration != baseline[new("", "item")] || contents != XsdProcessContents.Strict || !declarationCursor.Complete(warmOperation)) throw new InvalidOperationException("Declaration cursor changed.");
                sink ^= declaration.Value; break;
            case 3:
                if (!groupCursor.Accept(new("", "a"), warmOperation, out int? a, out _) || a is null || !groupCursor.Accept(new("", "b"), warmOperation, out int? b, out _) || b is null || !groupCursor.Complete(warmOperation)) throw new InvalidOperationException("Group cursor changed.");
                sink ^= a.Value ^ b.Value; break;
            case 4:
                if (!wildcardCursor.Accept(new("urn:foreign", "child"), warmOperation, out int? wildcard, out var process) || wildcard is not null || process != XsdProcessContents.Lax || !wildcardCursor.Complete(warmOperation)) throw new InvalidOperationException("Wildcard cursor changed.");
                sink++; break;
            case 5:
                var lazy = new XsdContentMatcher().CreateCursor(program); GC.KeepAlive(lazy); break;
            case 6:
                GC.KeepAlive(NewInitializedCursor()); break;
            case 7:
                GC.KeepAlive(NewProgram()); break;
            case 8:
                var model = ParseCompile(Source); var definition = model.Types.Single(t => t.Name == new SchemaName("", "T"));
                var operation = new SchemaCompileOperation(Options); var compiled = new XsdContentCompiler().Compile(model, definition, operation);
                var cursor = new XsdContentMatcher().CreateCursor(compiled); var document = new SchemaCompileContext(Options).Parse(Instance).Document;
                int count = 0;
                foreach (var node in document.RootElement.Children)
                { if (node.Kind != XmlNodeKind.Element) continue; if (!cursor.Accept(new(node.NamespaceUri, node.LocalName), operation, out int? match, out _) || match is null) throw new InvalidOperationException("XML composition changed."); count++; }
                if (count != 3 || !cursor.Complete(operation)) throw new InvalidOperationException("XML composition incomplete."); sink ^= count; GC.KeepAlive(document); break;
            case 9:
                try { new XsdContentCompiler().Compile(invalid, invalid.Types.Single(t => t.Name == new SchemaName("", "T")), new SchemaCompileOperation(Options)); throw new InvalidOperationException("UPA accepted."); }
                catch (SchemaCompileException error) { if (!error.Diagnostics.Any(d => d.Code == "XSD.SchemaRestriction")) throw; GC.KeepAlive(error); }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
    }
    internal int Consume() => sink;
}
