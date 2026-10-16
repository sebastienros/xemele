namespace Xemele.XPath.Internal;

internal sealed record XPathStaticContext
{
    public IReadOnlyDictionary<string, string> Namespaces
    {
        get;
    }
    public IReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature> Functions
    {
        get;
    }
    public XPathCompileLimits Limits
    {
        get;
    }
    internal int ContextTokens
    {
        get; private set;
    }
    internal int ContextCharacters
    {
        get; private set;
    }
    private readonly XPathSourceSpan entrySpan;
    private void ChargeBinding(long characters)
    {
        if (ContextTokens == int.MaxValue || characters > int.MaxValue - ContextCharacters ||
            (Limits.MaxTokens is int tokens && ContextTokens >= tokens) ||
            (Limits.MaxExpressionCharacters is int maximum && characters > maximum - ContextCharacters))
        {
            throw new XPathCompileException(new("XPATH-COMPILE-LIMIT", XPathDiagnosticCategory.CompileLimit, "XPath static context limit exceeded.", entrySpan));
        }

        ContextTokens++;
        ContextCharacters += (int)characters;
    }
    internal XPathStaticContext(IReadOnlyDictionary<string, string> Namespaces, IReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature> Functions, XPathCompileLimits Limits)
        : this(Namespaces, Functions, Limits, default, false, default) { }
    private XPathStaticContext(IReadOnlyDictionary<string, string> namespacesInput, IEnumerable<KeyValuePair<XPathExpandedName, XPathFunctionSignature>> functionsInput,
        XPathCompileLimits limits, CancellationToken cancellationToken, bool extensionsOnly, XPathSourceSpan span)
    {
        ArgumentNullException.ThrowIfNull(namespacesInput);
        ArgumentNullException.ThrowIfNull(functionsInput);
        ArgumentNullException.ThrowIfNull(limits);
        cancellationToken.ThrowIfCancellationRequested();
        Limits = limits with
        {
        };
        entrySpan = span;
        var namespaces = new ContextMap<string, string>((a, b) => ContextComparison.Equal(a, b, cancellationToken))
        {
            { "xml", TreeValidation.XmlUri }
        };
        foreach (var binding in namespacesInput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(binding.Key);
            ArgumentNullException.ThrowIfNull(binding.Value);
            ChargeBinding((long)binding.Key.Length + binding.Value.Length);
            ContextScan.Name(binding.Key, cancellationToken);
            if (binding.Key == "xmlns" || binding.Value.Length == 0 || (binding.Key == "xml" && binding.Value != TreeValidation.XmlUri))
            {
                throw new ArgumentException("Invalid static namespace binding.", nameof(namespacesInput));
            }

            ContextScan.Text(binding.Value, cancellationToken);
            if (binding.Key != "xml")
            {
                namespaces.Add(binding.Key, binding.Value);
            }
        }
        var functions = new ContextMap<XPathExpandedName, XPathFunctionSignature>((a, b) => ContextComparison.EqualName(a, b, cancellationToken));
        if (extensionsOnly)
        {
            foreach (var core in XPathCoreSignatures.All)
            {
                cancellationToken.ThrowIfCancellationRequested();
                functions.Add(core.Name, core);
            }
        }

        foreach (var function in functionsInput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(function.Value);
            ArgumentNullException.ThrowIfNull(function.Key.NamespaceUri);
            ArgumentNullException.ThrowIfNull(function.Key.LocalName);
            if (extensionsOnly || function.Key.NamespaceUri.Length != 0)
            {
                ChargeBinding((long)function.Key.NamespaceUri.Length + function.Key.LocalName.Length);
            }

            ContextScan.Name(function.Key.LocalName, cancellationToken);
            ContextScan.Poll(function.Key.NamespaceUri, cancellationToken);
            // Registration payload is validated before growth and no intermediate
            // dictionary has copied extension entries before this quota check.
            if (!ContextComparison.EqualName(function.Key, function.Value.Name, cancellationToken))
            {
                throw new ArgumentException("Signature key mismatch.", nameof(functionsInput));
            }

            ModelGuard.Arity(function.Value.MinArguments, function.Value.MaxArguments);
            if (function.Key.NamespaceUri.Length == 0)
            {
                XPathFunctionSignature? core = null;
                foreach (var candidate in XPathCoreSignatures.All)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ContextComparison.EqualName(candidate.Name, function.Key, cancellationToken))
                    {
                        core = candidate;
                        break;
                    }
                }
                if (extensionsOnly || core is null || core.MinArguments != function.Value.MinArguments || core.MaxArguments != function.Value.MaxArguments)
                {
                    throw new ArgumentException("Invalid core function registration.", nameof(functionsInput));
                }
            }
            functions.Add(function.Key, function.Value);
        }
        cancellationToken.ThrowIfCancellationRequested();
        Namespaces = namespaces.Seal();
        Functions = functions.Seal();
    }
    internal static XPathStaticContext FromOptions(XPathCompileOptions? options, CancellationToken cancellationToken = default) => FromOptions(options, cancellationToken, default);
    internal static XPathStaticContext FromOptions(XPathCompileOptions? options, CancellationToken cancellationToken, XPathSourceSpan entrySpan)
    {
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Functions);
        return new(options.Namespaces, Registrations(options.Functions), options.Limits, cancellationToken, true, entrySpan);
        static IEnumerable<KeyValuePair<XPathExpandedName, XPathFunctionSignature>> Registrations(IReadOnlyList<XPathFunctionSignature> signatures)
        {
            // Enumerable adapters also support caller dictionaries/lists whose
            // enumeration cancels; each yielded registration is checked immediately.
            foreach (var signature in signatures)
            {
                ArgumentNullException.ThrowIfNull(signature);
                yield return new(signature.Name, signature);
            }
        }
    }
}
internal sealed record XPathDynamicContext
{
    internal IReadOnlyDictionary<XPathExpandedName, XPathValue> Variables
    {
        get;
    }
    internal IReadOnlyDictionary<XPathExpandedName, XPathExtensionFunction> Functions
    {
        get;
    }
    internal XPathEvaluationLimits Limits
    {
        get;
    }
    internal CancellationToken CancellationToken
    {
        get;
    }
    internal XPathDynamicContext(XPathContext? options) : this(options, CreateBudget(options)) { }
    private static IXPathBudget CreateBudget(XPathContext? options) => new XPathBudget(options?.Limits ?? new(), default, options?.CancellationToken ?? default);
    private static readonly XPathContext DefaultOptions = new();
    private static readonly ContextMap<XPathExpandedName, XPathValue> NoVariables = new((_, _) => false);
    private static readonly ContextMap<XPathExpandedName, XPathExtensionFunction> NoFunctions = new((_, _) => false);
    static XPathDynamicContext()
    {
        NoVariables.Seal();
        NoFunctions.Seal();
    }
    internal XPathDynamicContext(XPathContext? options, IXPathBudget budget)
    {
        options ??= DefaultOptions;
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(options.Variables);
        ArgumentNullException.ThrowIfNull(options.Functions);
        ArgumentNullException.ThrowIfNull(options.Limits);
        Limits = options.Limits;
        CancellationToken = options.CancellationToken;
        CancellationToken.ThrowIfCancellationRequested();
        budget.ChargeOperations(0);
        if (options.Variables.Count == 0 && options.Functions.Count == 0)
        {
            // Nothing to copy or compare: share the immutable empty maps.
            budget.ChargeOperations(0);
            Variables = NoVariables;
            Functions = NoFunctions;
            return;
        }
        var variables = new ContextMap<XPathExpandedName, XPathValue>((a, b) => ContextComparison.EqualName(a, b, CancellationToken, budget));
        foreach (var variable in options.Variables)
        {
            BeforeEntry(variable.Key);
            if (variable.Value.Kind == XPathValueKind.String)
            {
                budget.CheckStringSize(variable.Value.AsString().Length);
            }
            else if (variable.Value.Kind == XPathValueKind.NodeSet)
            {
                budget.CheckNodeSetSize(variable.Value.AsNodeSet().Count);
            }

            variables.Add(variable.Key, variable.Value);
        }
        var functions = new ContextMap<XPathExpandedName, XPathExtensionFunction>((a, b) => ContextComparison.EqualName(a, b, CancellationToken, budget));
        foreach (var function in options.Functions)
        {
            BeforeEntry(function.Key);
            ArgumentNullException.ThrowIfNull(function.Value);
            if (function.Key.NamespaceUri.Length == 0)
            {
                throw new ArgumentException("Extensions require a namespace.", nameof(options));
            }

            functions.Add(function.Key, function.Value);
        }
        CancellationToken.ThrowIfCancellationRequested();
        budget.ChargeOperations(0);
        Variables = variables.Seal();
        Functions = functions.Seal();
        void BeforeEntry(XPathExpandedName name)
        {
            CancellationToken.ThrowIfCancellationRequested();
            budget.ChargeOperations(1);
            ArgumentNullException.ThrowIfNull(name.NamespaceUri);
            ContextScan.Poll(name.NamespaceUri, CancellationToken, budget);
            ContextScan.Name(name.LocalName, CancellationToken, budget);
        }
    }
}
