using System.Security.Cryptography;
using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdImportVisibility;

// Prepared inputs are owned by this fixture; each Run owns its compile operation,
// resource streams and returned graph/schema/diagnostic snapshot. No global cache.
internal sealed class ImportFixture
{
    private static readonly Uri Basis = new("https://imports.test/main.xsd");
    private static string Wrap(string body, string attributes = "") => "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' " + attributes + ">" + body + "</xs:schema>";
    internal static readonly string[] Routes = ["original-elemZ007-reject", "original-schZ005-reject", "derived-elemZ007-principal-import", "derived-schZ005-principal-import", "graph-local-namespace-only", "graph-local-unavailable-location", "graph-nested-principal-unauthorized", "graph-included-own-import", "graph-principal-cannot-lend", "graph-authorized-missing-component", "graph-mutual-imports", "graph-work-refusal", "graph-resource-denied", "cancel-after-acquisition", "original-annotation-a001-reject", "appinfo-invalid-digit-scheme", "appinfo-invalid-digit-http", "documentation-invalid-digit-scheme", "documentation-invalid-digit-http", "appinfo-numeric-relative", "documentation-normalized-absolute", "documentation-opaque-payload", "annotation-work-refusal", "annotation-cancel-lexical"];
    private readonly byte[][] originals = new byte[2][];
    private readonly byte[][] children = new byte[2][], foreignOriginal = new byte[2][];
    private readonly Uri[] originalUris = new Uri[2];
    private readonly string[] childNames = ["elemZ007_a.xsd", "schZ005a.xsd"], foreignNames = ["elemZ007_b.xsd", "schZ005b.xsd"];
    private readonly XmlDocument[] derived = new XmlDocument[2], synthetic;
    private readonly byte[] foreign, child, grand, unauthorized, mutual;
    private readonly byte[] annotationOriginal;
    private readonly Uri annotationUri;
    private readonly XmlDocument[] annotations;
    private int consumed;
    internal ImportFixture()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests/fixtures/M8-xsd-import-visibility")))
        {
            dir = dir.Parent;
        }

        var root = Path.Combine(dir!.FullName, "tests/fixtures/M8-xsd-import-visibility");
        string[] paths = ["msData/element/elemZ007.xsd", "msData/schema/schZ005.xsd"];
        string[] hashes = ["6f6072d50e0b3bf6875ccee70f9e71714228cb2b8900627a9263a6fa79b7b1bf", "37cc9e109b421861ade4dd514263a2c27f331e79d548ffb619edb8e0be2d17c8"];
        for (var i = 0; i < 2; i++)
        {
            var path = Path.Combine(root, paths[i]);
            originalUris[i] = new(path);
            originals[i] = File.ReadAllBytes(path);
            if (Convert.ToHexStringLower(SHA256.HashData(originals[i])) != hashes[i])
            {
                throw new InvalidOperationException("Original bytes changed.");
            }

            children[i] = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path)!, childNames[i]));
            foreignOriginal[i] = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path)!, foreignNames[i]));
            derived[i] = XmlDocument.Parse(Encoding.UTF8.GetString(originals[i]).Replace("<xsd:include", "<xsd:import namespace='b'/><xsd:include", StringComparison.Ordinal));
        }
        annotationUri = new(Path.Combine(root, "msData/datatypes/Facets/anyURI/anyURI_a001.xsd"));
        annotationOriginal = File.ReadAllBytes(annotationUri.LocalPath);
        if (Convert.ToHexStringLower(SHA256.HashData(annotationOriginal)) != "51e844db424ef6692fd2779f08d58e34b52f4a068f9c39288c57c2bab20afd60")
        {
            throw new InvalidOperationException("Annotation original changed.");
        }

        string[] wrappers = ["appinfo", "appinfo", "documentation", "documentation", "appinfo", "documentation", "documentation", "documentation", "documentation"];
        string[] values = ["9999999999999999999999999999999999999999999999999999999999999anyURI:", "1111111111111111111http://foo/bar", "9999999999999999999999999999999999999999999999999999999999999anyURI:", "1111111111111111111http://foo/bar", "123", " &#x9;http://example.test/" + new string('a', 300) + "  ", "../路径/123", "http://example.test/a", "http://example.test/" + new string('a', 1024)];
        annotations = new XmlDocument[9];
        for (var i = 0; i < annotations.Length; i++)
        {
            annotations[i] = XmlDocument.Parse(Wrap("\n<xs:annotation><xs:" + wrappers[i] + " source='" + values[i] + "'><payload xmlns='urn:opaque'><xs:not-a-schema source='1:bad'/></payload></xs:" + wrappers[i] + "></xs:annotation><xs:element name='root' type='xs:int'/>"));
        }

        foreign = Encoding.UTF8.GetBytes(Wrap("<xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>", "targetNamespace='urn:b'"));
        grand = Encoding.UTF8.GetBytes(Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:element name='included' type='b:T'/>", "xmlns:b='urn:b'"));
        child = Encoding.UTF8.GetBytes(Wrap("<xs:include schemaLocation='grand'/>"));
        unauthorized = Encoding.UTF8.GetBytes(Wrap("<xs:element name='included' type='b:T'/>", "xmlns:b='urn:b'"));
        mutual = Encoding.UTF8.GetBytes(Wrap("<xs:import namespace='urn:a' schemaLocation='main.xsd'/><xs:simpleType name='T'><xs:restriction base='a:A'/></xs:simpleType>", "targetNamespace='urn:b' xmlns:a='urn:a'"));
        var include = "<xs:include schemaLocation='child'/>";
        var element = "<xs:element name='root' type='b:T'/>";
        var attrs = "targetNamespace='urn:a' xmlns:b='urn:b'";
        synthetic = [
            XmlDocument.Parse(Wrap("<xs:import namespace='urn:b'/>" + include + element, attrs)),
            XmlDocument.Parse(Wrap("<xs:import namespace='urn:b' schemaLocation='missing'/>" + include + element, attrs)),
            XmlDocument.Parse(Wrap(include + element, attrs)),
            XmlDocument.Parse(Wrap(include, attrs)),
            XmlDocument.Parse(Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/>" + include, attrs)),
            XmlDocument.Parse(Wrap("<xs:import namespace='urn:b'/>" + include + "<xs:element name='root' type='b:Missing'/>", attrs)),
            XmlDocument.Parse(Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:simpleType name='A'><xs:restriction base='xs:int'/></xs:simpleType>" + element, attrs)),
            XmlDocument.Parse(Wrap("<xs:import namespace='urn:b'/>" + include + element, attrs)),
            XmlDocument.Parse(Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/>" + element, attrs)),
            XmlDocument.Parse(Wrap(include, attrs))
        ];
    }
    internal object Run(int route)
    {
        if ((uint)route >= Routes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }

        using var cancellation = route is 13 or 23 ? new CancellationTokenSource() : null;
        var resolver = new Resolver(this, route, cancellation);
        var options = new SchemaCompileOptions { BaseUri = route < 4 ? originalUris[route % 2] : route == 14 ? annotationUri : Basis, Resolver = resolver, MaxOperations = route is 11 or 22 ? 1 : 10_000_000, CancellationToken = cancellation?.Token ?? default };
        try
        {
            object result;
            if (route < 2)
            {
                using var stream = new MemoryStream(originals[route], writable: false);
                result = XmlSchema.Compile(stream, options);
                if (!stream.CanRead)
                {
                    throw new InvalidOperationException("Caller stream closed.");
                }
            }
            else if (route == 14)
            {
                using var stream = new MemoryStream(annotationOriginal, writable: false);
                result = XmlSchema.Compile(stream, options);
            }
            else if (route < 4)
            {
                result = XmlSchema.Compile(derived[route % 2], options);
            }
            else
            {
                ISchemaOperation operation = new SchemaCompileOperation(options);
                if (route == 23)
                {
                    operation = new CancelLexical(operation, cancellation!);
                }

                var graph = new XmlSchemaCompiler().Compile(route >= 15 ? annotations[route - 15] : synthetic[route - 4], options, operation);
                var root = route == 7 ? "included" : "root";
                if (!operation.IsComplete || !graph.GlobalElements.ContainsKey(new(route >= 15 ? "" : "urn:a", root)))
                {
                    throw new InvalidOperationException("Expected compiled element lost.");
                }

                result = graph;
            }
            if (route is not (2 or 3 or 4 or 5 or 7 or 10 or 19 or 20 or 21))
            {
                throw new InvalidOperationException("Invalid route published a schema.");
            }

            resolver.Verify();
            consumed++;
            return result;
        }
        catch (SchemaCompileException error) when (route is 0 or 1 or 6 or 8 or 9 or 11 or 12 or 14 or 15 or 16 or 17 or 18 or 22)
        {
            var code = route is 11 or 22 ? "SCHEMA.Limit" : route == 12 ? "SCHEMA.ResourceDenied" : "XSD.SchemaRestriction";
            if (error.Diagnostics.Count != 1 || error.Diagnostics[0].Code != code)
            {
                throw new InvalidOperationException("Wrong rejection.");
            }

            if (route is 0 or 1 or 6 or 8 && !error.Diagnostics[0].Message.Contains("without an import", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Wrong import rejection.");
            }

            if (route < 2 && error.Diagnostics[0].Location.SourceUri != originalUris[route])
            {
                throw new InvalidOperationException("Original source identity lost.");
            }

            if (route is >= 14 and <= 18 && !error.Diagnostics[0].Message.Contains("annotation source", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Wrong annotation rejection.");
            }

            resolver.Verify();
            consumed += error.Diagnostics.Count;
            return error;
        }
        catch (OperationCanceledException error) when (route is 13 or 23)
        {
            if (error.CancellationToken != cancellation!.Token)
            {
                throw new InvalidOperationException("Wrong cancellation token.");
            }

            resolver.Verify();
            consumed++;
            return error;
        }
    }
    internal int Consume() => consumed;
    private sealed class Resolver(ImportFixture fixture, int route, CancellationTokenSource? cancellation) : XmlResourceResolver
    {
        private int calls;
        private readonly List<OwnedStream> streams = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) => throw new InvalidOperationException("Explicit results only.");
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            calls++;
            if (request.Kind != XmlResourceKind.Schema)
            {
                throw new InvalidOperationException("Wrong resource kind.");
            }

            if (route < 4)
            {
                var i = route % 2;
                var first = calls == 1;
                var name = first ? fixture.childNames[i] : fixture.foreignNames[i];
                if (request.SystemId != name || request.Uri != new Uri(fixture.originalUris[i], name) || request.Referrer != (first ? fixture.originalUris[i] : new Uri(fixture.originalUris[i], fixture.childNames[i])))
                {
                    throw new InvalidOperationException("Original acquisition identity changed.");
                }

                return Resolved(first ? fixture.children[i] : fixture.foreignOriginal[i], request.Uri);
            }
            if (route == 12)
            {
                return XmlResourceResolution.Denied;
            }

            if (route == 13)
            {
                cancellation!.Cancel();
                return Resolved(fixture.child, request.Uri);
            }
            if (request.SystemId == "missing")
            {
                return XmlResourceResolution.Unavailable;
            }

            var bytes = request.SystemId switch
            {
                "child" => route == 8 ? fixture.unauthorized : fixture.child,
                "grand" => fixture.grand,
                "foreign" => route == 10 ? fixture.mutual : fixture.foreign,
                _ => throw new InvalidOperationException("Unexpected request.")
            };
            var expectedReferrer = request.SystemId switch
            {
                "grand" => new(Basis, "child"),
                "foreign" when route is not (8 or 10) => new(Basis, "grand"),
                _ => Basis
            };
            if (request.Referrer != expectedReferrer || request.Uri != new Uri(Basis, request.SystemId!))
            {
                throw new InvalidOperationException("Synthetic source identity changed.");
            }

            return Resolved(bytes, request.Uri);
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
                >= 14 => 0,
                < 4 => 2,
                5 => 4,
                4 or 6 or 7 or 9 => 3,
                8 => 2,
                11 => 0,
                _ => 1
            };
            var owned = expected - (route is 5 or 12 ? 1 : 0);
            if (calls != expected || streams.Count != owned || streams.Any(s => s.Disposals != 1))
            {
                throw new InvalidOperationException("Resource ownership/count changed: " + Routes[route]);
            }
        }
    }
    private sealed class CancelLexical(ISchemaOperation inner, CancellationTokenSource cancellation) : ISchemaOperation
    {
        private int sourceWork;
        public XmlResourceLimits Limits => inner.Limits;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete;
        public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long amount, XmlLocation at)
        {
            var result = inner.Charge(amount, at);
            if (amount > 0 && at.LineNumber == 2 && at.LinePosition > 30 && ++sourceWork == 64)
            {
                cancellation.Cancel();
            }

            return result;
        }
        public bool ChargeComponents(int amount, XmlLocation at) => inner.ChargeComponents(amount, at);
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
        public void Refuse(string code, string message, XmlLocation at) => inner.Refuse(code, message, at);
    }
    private sealed class OwnedStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        internal int Disposals;
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }
}
