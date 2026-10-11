using System.Collections.ObjectModel;
namespace Xemele.XPath.Internal;
internal sealed record XPathStaticContext
{
    public IReadOnlyDictionary<string, string> Namespaces { get; }
    public IReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature> Functions { get; }
    public XPathCompileLimits Limits { get; }
    internal int ContextTokens { get; private set; }
    internal int ContextCharacters { get; private set; }
    private void ChargeBinding(long characters)
    {
        if (ContextTokens == int.MaxValue || characters > int.MaxValue - ContextCharacters ||
            (Limits.MaxTokens is int tokens && ContextTokens >= tokens) ||
            (Limits.MaxExpressionCharacters is int maximum && characters > maximum - ContextCharacters))
            throw new XPathCompileException(new("XPATH-COMPILE-LIMIT", XPathDiagnosticCategory.CompileLimit, "XPath static context limit exceeded.", default));
        ContextTokens++; ContextCharacters += (int)characters;
    }
    internal XPathStaticContext(IReadOnlyDictionary<string, string> Namespaces, IReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature> Functions, XPathCompileLimits Limits)
        : this(Namespaces, Functions, Limits, default, false) { }
    private XPathStaticContext(IReadOnlyDictionary<string, string> namespacesInput, IEnumerable<KeyValuePair<XPathExpandedName, XPathFunctionSignature>> functionsInput,
        XPathCompileLimits limits, CancellationToken cancellationToken, bool extensionsOnly)
    {
        ArgumentNullException.ThrowIfNull(namespacesInput); ArgumentNullException.ThrowIfNull(functionsInput); ArgumentNullException.ThrowIfNull(limits);
        cancellationToken.ThrowIfCancellationRequested(); Limits = limits with { };
        var namespaces = new Dictionary<string, string>(StringComparer.Ordinal) { ["xml"] = TreeValidation.XmlUri };
        foreach (var binding in namespacesInput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(binding.Key); ArgumentNullException.ThrowIfNull(binding.Value);
            ChargeBinding((long)binding.Key.Length + binding.Value.Length);
            ContextScan.Name(binding.Key, cancellationToken);
            if (binding.Key == "xmlns" || binding.Value.Length == 0 || (binding.Key == "xml" && binding.Value != TreeValidation.XmlUri)) throw new ArgumentException("Invalid static namespace binding.", nameof(namespacesInput));
            ContextScan.Text(binding.Value, cancellationToken);
            if (binding.Key != "xml") namespaces.Add(binding.Key, binding.Value);
        }
        var functions = new Dictionary<XPathExpandedName, XPathFunctionSignature>();
        if (extensionsOnly) foreach (var core in XPathCoreSignatures.All) { cancellationToken.ThrowIfCancellationRequested(); functions.Add(core.Name, core); }
        foreach (var function in functionsInput)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(function.Value); ArgumentNullException.ThrowIfNull(function.Key.NamespaceUri); ArgumentNullException.ThrowIfNull(function.Key.LocalName);
            if (extensionsOnly || function.Key.NamespaceUri.Length != 0) ChargeBinding((long)function.Key.NamespaceUri.Length + function.Key.LocalName.Length);
            ContextScan.Name(function.Key.LocalName, cancellationToken); ContextScan.Poll(function.Key.NamespaceUri, cancellationToken);
            // Registration payload is validated before growth and no intermediate
            // dictionary has copied extension entries before this quota check.
            if (function.Key != function.Value.Name) throw new ArgumentException("Signature key mismatch.", nameof(functionsInput));
            ModelGuard.Arity(function.Value.MinArguments, function.Value.MaxArguments);
            if (function.Key.NamespaceUri.Length == 0)
            {
                if (extensionsOnly || !XPathCoreSignatures.ByName.TryGetValue(function.Key, out var core) || core != function.Value) throw new ArgumentException("Invalid core function registration.", nameof(functionsInput));
            }
            functions.Add(function.Key, function.Value);
        }
        cancellationToken.ThrowIfCancellationRequested();
        Namespaces = new ReadOnlyDictionary<string, string>(namespaces);
        Functions = new ReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature>(functions);
    }
    internal static XPathStaticContext FromOptions(XPathCompileOptions? options, CancellationToken cancellationToken = default)
    {
        options ??= new(); ArgumentNullException.ThrowIfNull(options.Functions);
        return new(options.Namespaces, Registrations(options.Functions), options.Limits, cancellationToken, true);
        static IEnumerable<KeyValuePair<XPathExpandedName, XPathFunctionSignature>> Registrations(IReadOnlyList<XPathFunctionSignature> signatures)
        {
            // Enumerable adapters also support caller dictionaries/lists whose
            // enumeration cancels; each yielded registration is checked immediately.
            foreach (var signature in signatures)
            { ArgumentNullException.ThrowIfNull(signature); yield return new(signature.Name, signature); }
        }
    }
}
internal sealed record XPathDynamicContext
{
    internal IReadOnlyDictionary<XPathExpandedName, XPathValue> Variables { get; }
    internal IReadOnlyDictionary<XPathExpandedName, XPathExtensionFunction> Functions { get; }
    internal XPathEvaluationLimits Limits { get; }
    internal CancellationToken CancellationToken { get; }
    internal XPathDynamicContext(XPathContext? options) : this(options, CreateBudget(options)) { }
    private static IXPathBudget CreateBudget(XPathContext? options) => new XPathBudget(options?.Limits ?? new(), default, options?.CancellationToken ?? default);
    internal XPathDynamicContext(XPathContext? options, IXPathBudget budget)
    {
        options ??= new(); ArgumentNullException.ThrowIfNull(budget); ArgumentNullException.ThrowIfNull(options.Variables); ArgumentNullException.ThrowIfNull(options.Functions); ArgumentNullException.ThrowIfNull(options.Limits);
        Limits = options.Limits with { }; CancellationToken = options.CancellationToken;
        CancellationToken.ThrowIfCancellationRequested(); budget.ChargeOperations(0);
        var variables = new Dictionary<XPathExpandedName, XPathValue>();
        foreach (var variable in options.Variables)
        {
            BeforeEntry(variable.Key);
            if (variable.Value.Kind == XPathValueKind.String) budget.CheckStringSize(variable.Value.AsString().Length);
            else if (variable.Value.Kind == XPathValueKind.NodeSet) budget.CheckNodeSetSize(variable.Value.AsNodeSet().Count);
            variables.Add(variable.Key, variable.Value);
        }
        var functions = new Dictionary<XPathExpandedName, XPathExtensionFunction>();
        foreach (var function in options.Functions)
        {
            BeforeEntry(function.Key); ArgumentNullException.ThrowIfNull(function.Value);
            if (function.Key.NamespaceUri.Length == 0) throw new ArgumentException("Extensions require a namespace.", nameof(options));
            functions.Add(function.Key, function.Value);
        }
        CancellationToken.ThrowIfCancellationRequested(); budget.ChargeOperations(0);
        Variables = new ReadOnlyDictionary<XPathExpandedName, XPathValue>(variables);
        Functions = new ReadOnlyDictionary<XPathExpandedName, XPathExtensionFunction>(functions);
        void BeforeEntry(XPathExpandedName name)
        {
            CancellationToken.ThrowIfCancellationRequested(); budget.ChargeOperations(1);
            ArgumentNullException.ThrowIfNull(name.NamespaceUri);
            ContextScan.Poll(name.NamespaceUri, CancellationToken, budget);
            ContextScan.Name(name.LocalName, CancellationToken, budget);
        }
    }
}
