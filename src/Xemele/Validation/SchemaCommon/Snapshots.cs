using System.Collections.ObjectModel;
using Xemele.XPath;

namespace Xemele.Validation;

// These allocations are persistence/setup boundaries, never borrowed-token helpers.
internal static class SchemaSnapshots
{
    internal static string Required(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }
    internal static int Nonnegative(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }

    internal static IReadOnlyList<T> List<T>(IEnumerable<T> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var owned = new List<T>();
        foreach (var value in input)
        {
            if (value is null)
            {
                throw new ArgumentException("Null snapshot member.", nameof(input));
            }

            owned.Add(value);
        }
        return owned.AsReadOnly();
    }

    internal static IReadOnlyDictionary<string, string> Map(IReadOnlyDictionary<string, string> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var owned = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in input)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            owned.Add(key, value);
        }
        return new ReadOnlyDictionary<string, string>(owned);
    }

    internal static SchemaCompileOptions Compile(SchemaCompileOptions? options)
    {
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        if (options.BaseUri is { IsAbsoluteUri: false })
        {
            throw new ArgumentException("Absolute base URI required.", nameof(options));
        }

        if (options.MaxOperations is <= 0 || options.MaxComponents is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        options.CancellationToken.ThrowIfCancellationRequested();
        return options with
        {
            Limits = options.Limits with
            {
            }
        };
    }

    internal static SchematronCompileOptions Compile(SchematronCompileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Schema);
        ArgumentNullException.ThrowIfNull(options.XPathLimits);
        if (!Enum.IsDefined(options.Edition) || !Enum.IsDefined(options.QueryBinding))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        return options with
        {
            Schema = Compile(options.Schema),
            XPathLimits = options.XPathLimits with
            {
            }
        };
    }

    internal static SchematronValidationOptions Validation(SchematronValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Validation);
        ArgumentNullException.ThrowIfNull(options.Validation.Limits);
        ArgumentNullException.ThrowIfNull(options.XPathLimits);
        var validation = options.Validation;
        validation.Limits.Validate();
        if (validation.MaxOperations is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        validation.CancellationToken.ThrowIfCancellationRequested();
        return options with
        {
            Validation = validation with
            {
                Limits = validation.Limits with
                {
                }
            },
            XPathLimits = options.XPathLimits with
            {
            }
        };
    }

    // Contexts retain strings/URI values only. Ancestors and namespace handles are borrowed.
    internal static SchemaNamespaceContext Context(XmlNode element, ISchemaOperation operation, Uri? sourceUri = null)
    {
        element.Require();
        var document = element.OwnerDocument;
        var revision = document.ContentRevision;
        if (revision < 0)
        {
            throw new InvalidOperationException("Source revision exhausted.");
        }

        var ancestors = new List<XmlNode>();
        for (var node = element; !node.IsDefault; node = node.Parent)
        {
            if (!operation.Charge(1, default))
            {
                throw Failure(operation);
            }

            ancestors.Add(node);
        }
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal) { ["xml"] = "http://www.w3.org/XML/1998/namespace" };
        var baseUri = sourceUri ?? document.ValidationDeclarationLocation.SourceUri;
        for (var i = ancestors.Count - 1; i >= 0; i--)
        {
            var node = ancestors[i];
            if (node.Kind != XmlNodeKind.Element)
            {
                continue;
            }

            foreach (var ns in node.NamespaceDeclarations)
            {
                if (!operation.Charge(1, default))
                {
                    throw Failure(operation);
                }

                prefixes[ns.Prefix] = ns.Value;
            }
            if (node.GetAttribute("base", "http://www.w3.org/XML/1998/namespace") is string lexical)
            {
                if (!Uri.TryCreate(lexical, UriKind.Absolute, out var absolute))
                {
                    if (baseUri is null || !Uri.TryCreate(baseUri, lexical, out absolute))
                    {
                        operation.Refuse("SCHEMA.ResourceDenied", "Relative xml:base has no absolute base.", default);
                        throw Failure(operation);
                    }
                }
                baseUri = absolute;
            }
        }
        if (document.ContentRevision != revision)
        {
            throw new InvalidOperationException("Source changed during namespace snapshot.");
        }

        operation.CancellationToken.ThrowIfCancellationRequested();
        return new(prefixes, prefixes.TryGetValue("", out var value) ? value : "", baseUri);
    }

    internal static SchemaCompileException Failure(ISchemaOperation operation) => operation switch
    {
        SchemaCompileOperation compile => compile.Exception(),
        _ => new([new("SCHEMA.Limit", XmlDiagnosticSeverity.Fatal, "Schema operation stopped.", default)])
    };
}
