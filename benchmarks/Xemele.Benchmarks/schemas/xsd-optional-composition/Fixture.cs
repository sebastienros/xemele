using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdOptionalComposition;

internal sealed class OptionalFixture
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly Uri Basis = new("https://optional.test/main.xsd");
    internal static readonly string[] Routes = [
        "graph-optional-include-absent", "graph-optional-import-absent", "graph-empty-redefine-absent", "graph-annotation-redefine-absent",
        "graph-include-same-uri-later-available", "graph-import-same-uri-later-available", "facade-mandatory-redefine-resolved",
        "mandatory-redefine-unavailable", "optional-shared-work-refusal", "optional-legacy-null-denial", "optional-explicit-denial", "optional-no-resolver-denial",
        "optional-resolved-malformed", "optional-resolved-wrong-namespace", "optional-missing-required-reference", "cancel-after-unavailable", "cancel-after-acquisition",
        "facade-original-anyURI_a002", "facade-original-anyURI_a004-derived-valid", "facade-original-schD7", "facade-original-schG8",
        "facade-include-later-available", "validation-original-a004-derived-valid", "caller-read-primary-survives-disposal"
    ];
    private readonly XmlDocument[] synthetic;
    private readonly byte[] child, imported, malformed, wrongNamespace;
    private readonly byte[][] originals, available;
    private readonly Uri[] originalUris;
    private readonly XmlDocument[] instances;
    private readonly XmlDocument valid = XmlDocument.Parse("<root>2</root>"), invalid = XmlDocument.Parse("<root>1</root>");
    private readonly XmlSchema cached;
    private int consumed;
    internal OptionalFixture()
    {
        var element = "<xs:element name='root' type='xs:int'/>";
        var include = "<xs:include schemaLocation='child'/>";
        var import = "<xs:import namespace='urn:b' schemaLocation='child'/>";
        var redefine = "<xs:redefine schemaLocation='child'><xs:simpleType name='T'><xs:restriction base='T'><xs:minInclusive value='2'/></xs:restriction></xs:simpleType></xs:redefine><xs:element name='root' type='T'/>";
        synthetic = [
            XmlDocument.Parse(Wrap(include + element)), XmlDocument.Parse(Wrap(import + element)),
            XmlDocument.Parse(Wrap("<xs:redefine schemaLocation='child'/>" + element)),
            XmlDocument.Parse(Wrap("<xs:redefine schemaLocation='child'><xs:annotation><xs:documentation>分類</xs:documentation></xs:annotation></xs:redefine>" + element)),
            XmlDocument.Parse(Wrap(include + include + "<xs:element name='root' type='T'/>")),
            XmlDocument.Parse(Wrap(import + import + "<xs:element name='root' type='b:T'/>", "xmlns:b='urn:b'")),
            XmlDocument.Parse(Wrap(redefine)), XmlDocument.Parse(Wrap(redefine)),
            XmlDocument.Parse(Wrap(include + element)), XmlDocument.Parse(Wrap(include + element)),
            XmlDocument.Parse(Wrap(import + element)), XmlDocument.Parse(Wrap("<xs:redefine schemaLocation='child'><xs:annotation/></xs:redefine>" + element)),
            XmlDocument.Parse(Wrap(include + element)), XmlDocument.Parse(Wrap(include + element)),
            XmlDocument.Parse(Wrap(import + "<xs:element name='root' type='b:missing'/>", "xmlns:b='urn:b'")),
            XmlDocument.Parse(Wrap(include + element)), XmlDocument.Parse(Wrap(include + element))
        ];
        var type = "<xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>";
        child = Encoding.UTF8.GetBytes(Wrap(type));
        imported = Encoding.UTF8.GetBytes(Wrap(type, "targetNamespace='urn:b'"));
        malformed = Encoding.UTF8.GetBytes("<xs:schema xmlns:xs='" + Xs + "'>");
        wrongNamespace = Encoding.UTF8.GetBytes(Wrap("", "targetNamespace='urn:wrong'"));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-optional-composition")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Pinned fixtures missing.");
        }

        var root = Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-optional-composition");
        string[] paths = ["msData/datatypes/Facets/anyURI/anyURI_a002.xsd", "msData/datatypes/Facets/anyURI/anyURI_a004.xsd", "msData/schema/schD7_a.xsd", "msData/schema/schG8_a.xsd"];
        string[] instancePaths = ["msData/datatypes/Facets/anyURI/anyURI_a002.xml", "msData/datatypes/Facets/anyURI/anyURI_a004.xml", "msData/schema/schD7.xml", "msData/schema/schG8.xml"];
        originals = new byte[4][];
        originalUris = new Uri[4];
        instances = new XmlDocument[4];
        for (var i = 0; i < 4; i++)
        {
            var path = Path.Combine(root, paths[i]);
            originals[i] = File.ReadAllBytes(path);
            originalUris[i] = new Uri(path);
            instances[i] = XmlDocument.Parse(File.ReadAllText(Path.Combine(root, instancePaths[i])));
        }
        available = [File.ReadAllBytes(Path.Combine(root, "msData/schema/schD7_c.xsd")), File.ReadAllBytes(Path.Combine(root, "msData/schema/schG8_c.xsd"))];
        var resolver = new Resolver(this, 18, null);
        using var stream = new MemoryStream(originals[1], writable: false);
        cached = XmlSchema.Compile(stream, new()
        {
            BaseUri = originalUris[1],
            Resolver = resolver
        });
        resolver.Verify();
        if (!stream.CanRead)
        {
            throw new InvalidOperationException("Caller principal closed.");
        }
    }
    private static string Wrap(string body, string attributes = "") => "<xs:schema xmlns:xs='" + Xs + "' " + attributes + ">" + body + "</xs:schema>";
    internal object Run(int route)
    {
        if ((uint)route >= Routes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }

        if (route == 22)
        {
            var result = cached.Validate(instances[1]);
            if (!result.IsComplete || !result.IsValid)
            {
                throw new InvalidOperationException("Separate W3C4126 derived validation failed; original INVALID disagreement remains in metadata.");
            }

            consumed += result.Diagnostics.Count + 1;
            return result;
        }
        using var cancellation = route is 15 or 16 ? new CancellationTokenSource() : null;
        var resolver = new Resolver(this, route, cancellation);
        var legacy = route == 9 ? new Legacy() : null;
        var options = new SchemaCompileOptions
        {
            BaseUri = route is >= 17 and <= 20 ? originalUris[route - 17] : Basis,
            Resolver = route == 11 ? null : route == 9 ? legacy : resolver,
            MaxOperations = route == 8 ? 1 : 10_000_000,
            CancellationToken = cancellation?.Token ?? default
        };
        try
        {
            if (route is <= 5)
            {
                var operation = new SchemaCompileOperation(options);
                var graph = new XmlSchemaCompiler().Compile(synthetic[route], options, operation);
                if (!operation.IsComplete || !graph.GlobalElements.ContainsKey(new("", "root")))
                {
                    throw new InvalidOperationException("Ordinary root lost after absence.");
                }

                if (route is 4 or 5 && !graph.SimpleTypes.Any(t => t.Name == new SchemaName(route == 5 ? "urn:b" : "", "T")))
                {
                    throw new InvalidOperationException("Later available type lost.");
                }

                resolver.Verify();
                consumed += graph.GlobalElements.Count + resolver.Calls;
                return graph;
            }
            if (route is 6 or 21)
            {
                var schema = XmlSchema.Compile(synthetic[route == 6 ? 6 : 4], options);
                var accepted = schema.Validate(valid);
                var rejected = schema.Validate(invalid);
                if (!accepted.IsComplete || !accepted.IsValid || !rejected.IsComplete || rejected.IsValid != (route == 21))
                {
                    throw new InvalidOperationException("Mandatory facet or later dependency semantics changed.");
                }

                resolver.Verify();
                consumed += accepted.Diagnostics.Count + rejected.Diagnostics.Count + resolver.Calls + 1;
                GC.KeepAlive(accepted);
                GC.KeepAlive(rejected);
                return schema;
            }
            if (route is >= 17 and <= 20)
            {
                var index = route - 17;
                using var stream = new MemoryStream(originals[index], writable: false);
                var schema = XmlSchema.Compile(stream, options);
                var result = schema.Validate(instances[index]);
                if (!stream.CanRead || !result.IsComplete || !result.IsValid)
                {
                    throw new InvalidOperationException("Unchanged original derived/metadata control failed.");
                }

                resolver.Verify();
                consumed += result.Diagnostics.Count + resolver.Calls + 1;
                GC.KeepAlive(result);
                return schema;
            }
            // Invalid/cancelled/caller routes must never publish a schema.
            var input = route == 23 ? synthetic[0] : synthetic[route];
            new XmlSchemaCompiler().Compile(input, options, new SchemaCompileOperation(options));
        }
        catch (SchemaCompileException error) when (route is >= 7 and <= 14)
        {
            var expected = route switch
            {
                8 => "SCHEMA.Limit",
                12 => "SCHEMA.Syntax",
                13 or 14 => "XSD.SchemaRestriction",
                _ => "SCHEMA.ResourceDenied"
            };
            if (error.Diagnostics.Count != 1 || error.Diagnostics[0].Code != expected)
            {
                throw new InvalidOperationException("Wrong schema rejection: " + Routes[route]);
            }

            if (route == 9)
            {
                if (legacy!.Calls != 1)
                {
                    throw new InvalidOperationException("Legacy callback changed.");
                }
            }
            else
            {
                resolver.Verify();
            }

            consumed += error.Diagnostics.Count + 1;
            return error;
        }
        catch (OperationCanceledException error) when (route is 15 or 16)
        {
            if (error.CancellationToken != cancellation!.Token)
            {
                throw new InvalidOperationException("Cancellation token changed.");
            }

            resolver.Verify();
            consumed += resolver.Calls + 1;
            return error;
        }
        catch (XmlParseException error) when (route == 23)
        {
            if (!ReferenceEquals(error, resolver.Primary) || error.Diagnostics.Count != 1 || error.Diagnostics[0].Code != "CALLER.Read")
            {
                throw new InvalidOperationException("Primary exception replaced.");
            }

            resolver.Verify();
            consumed += error.Diagnostics.Count + resolver.Calls;
            return error;
        }
        throw new InvalidOperationException("Invalid schema published: " + Routes[route]);
    }
    internal int Consume() => consumed;
    private sealed class Legacy : XmlResourceResolver
    {
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Calls++;
            return null;
        }
    }
    private sealed class Resolver(OptionalFixture fixture, int route, CancellationTokenSource? cancellation) : XmlResourceResolver
    {
        internal int Calls;
        private readonly List<OwnedStream> streams = [];
        internal XmlParseException? Primary;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) => throw new InvalidOperationException("Explicit results only.");
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Calls++;
            if (request.Kind != XmlResourceKind.Schema)
            {
                throw new InvalidOperationException("Schema kind changed.");
            }

            var principal = route is >= 17 and <= 20 ? fixture.originalUris[route - 17] : Basis;
            if (request.Referrer != principal)
            {
                throw new InvalidOperationException("Referrer changed.");
            }

            if (route is >= 17 and <= 20)
            {
                var missing = route switch
                {
                    17 => "名前空間URI",
                    18 => "http://127.0.0.1/must%20not%20resolve.xyzzy",
                    19 => "not-exist.xsd",
                    _ => "not-exist"
                };
                if (request.SystemId == missing)
                {
                    if (request.Uri != new Uri(principal, missing))
                    {
                        throw new InvalidOperationException("Missing identity changed.");
                    }

                    return XmlResourceResolution.Unavailable;
                }
                var name = route == 19 ? "schD7_c.xsd" : route == 20 ? "schG8_c.xsd" : throw new InvalidOperationException("Unexpected original reference.");
                if (request.SystemId != name || request.Uri != new Uri(principal, name))
                {
                    throw new InvalidOperationException("Available original identity changed.");
                }

                return Resolved(fixture.available[route - 19], request.Uri);
            }
            if (request.SystemId != "child" || request.Uri != new Uri(Basis, "child"))
            {
                throw new InvalidOperationException("Synthetic identity changed.");
            }

            if (route == 10)
            {
                return XmlResourceResolution.Denied;
            }

            if (route == 15)
            {
                cancellation!.Cancel();
                return XmlResourceResolution.Unavailable;
            }
            if (route == 16)
            {
                cancellation!.Cancel();
                return Resolved(fixture.child, request.Uri);
            }
            if (route == 23)
            {
                Primary = new XmlParseException([new("CALLER.Read", XmlDiagnosticSeverity.Error, "caller", default)]);
                var stream = new OwnedStream(fixture.child, Primary, new IOException("cleanup"));
                streams.Add(stream);
                return XmlResourceResolution.FromResource(new(stream, request.Uri));
            }
            if (route is 4 or 5 or 21)
            {
                return Calls == 1 ? XmlResourceResolution.Unavailable : Resolved(route == 5 ? fixture.imported : fixture.child, request.Uri);
            }

            if (route == 6)
            {
                return Resolved(fixture.child, request.Uri);
            }

            if (route == 12)
            {
                return Resolved(fixture.malformed, request.Uri);
            }

            if (route == 13)
            {
                return Resolved(fixture.wrongNamespace, request.Uri);
            }

            return XmlResourceResolution.Unavailable;
        }
        private XmlResourceResolution Resolved(byte[] bytes, Uri source)
        {
            var stream = new OwnedStream(bytes);
            streams.Add(stream);
            return XmlResourceResolution.FromResource(new(stream, source));
        }
        internal void Verify()
        {
            var expected = route switch
            {
                8 or 11 => 0,
                4 or 5 or 19 or 20 or 21 => 2,
                17 or 18 => 3,
                _ => 1
            };
            var owned = route switch
            {
                4 or 5 or 6 or 12 or 13 or 16 or 19 or 20 or 21 or 23 => 1,
                _ => 0
            };
            if (Calls != expected || streams.Count != owned || streams.Any(s => s.Disposals != 1))
            {
                throw new InvalidOperationException("Acquisition/disposal counts changed: " + Routes[route]);
            }
        }
    }
    private sealed class OwnedStream(byte[] bytes, Exception? read = null, Exception? cleanup = null) : MemoryStream(bytes, writable: false)
    {
        internal int Disposals;
        public override int Read(Span<byte> buffer)
        {
            if (read is not null)
            {
                throw read;
            }

            return base.Read(buffer);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
                if (cleanup is not null)
                {
                    throw cleanup;
                }
            }
            base.Dispose(disposing);
        }
    }
}
