using Xemele.Validation;
using Xemele.Benchmarks.M8XsdContent;
namespace Xemele.Benchmarks.M8XsdCountedMatcher;

internal sealed class CountedFixture
{
    internal const string Source = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:complexType name='T'><xs:sequence maxOccurs='1000'><xs:element name='a' maxOccurs='unbounded'/></xs:sequence></xs:complexType></xs:schema>";
    internal const string Invalid = ContentFixture.Invalid, Instance = ContentFixture.Instance;
    internal static readonly string[] Routes = [.. ContentFixture.Routes, "warm finite counted partition cursor", "owned counted cursor first initialization", "owned counted 1000-child match"];
    private readonly ContentFixture existing = new();
    private readonly SchemaCompileOptions options = new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } };
    private readonly XsdCompiledSchema graph;
    private readonly XsdTypeDefinition type;
    private readonly XsdContentProgram program;
    private readonly IXsdContentCursor warm;
    private readonly ISchemaOperation operation;
    private int sink;
    internal CountedFixture()
    {
        var context = new SchemaCompileContext(options);
        graph = new XmlSchemaCompiler().Compile(context.Parse(Source).Document, context.Options, context.Operation);
        type = graph.Types.Single(t => t.Name == new SchemaName("", "T"));
        program = NewProgram();
        operation = new SchemaCompileOperation(options);
        warm = new XsdContentMatcher().CreateCursor(program);
        // Setup owns both banks and retained states before the borrowed interval.
        for (var i = 0; i < 32; i++)
        {
            Run(10);
        }
    }
    internal XsdContentProgram NewProgram() => new XsdContentCompiler().Compile(graph, type, new SchemaCompileOperation(options));
    internal IXsdContentCursor NewInitializedCursor()
    {
        var cursor = new XsdContentMatcher().CreateCursor(program);
        Accept(cursor, new SchemaCompileOperation(options));
        return cursor;
    }
    private void Accept(IXsdContentCursor cursor, ISchemaOperation op)
    {
        if (!cursor.Accept(new("", "a"), op, out var id, out var policy) || id is null || policy != XsdProcessContents.Strict || !cursor.Complete(op))
        {
            throw new InvalidOperationException("Counted cursor semantic control failed.");
        }

        sink ^= id.Value;
    }
    internal void Run(int route)
    {
        if (route < 10)
        {
            existing.Run(route);
            return;
        }
        switch (route)
        {
            case 10:
                Accept(warm, operation);
                break;
            case 11:
                GC.KeepAlive(NewInitializedCursor());
                break;
            case 12:
                var cursor = new XsdContentMatcher().CreateCursor(program);
                var op = new SchemaCompileOperation(options);
                for (var i = 0; i < 1000; i++)
                {
                    Accept(cursor, op);
                }

                GC.KeepAlive(cursor);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }
    internal int Consume() => sink ^ existing.Consume();
}
