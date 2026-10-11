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
    {
        ArgumentNullException.ThrowIfNull(Namespaces); ArgumentNullException.ThrowIfNull(Functions); ArgumentNullException.ThrowIfNull(Limits);
        this.Limits = Limits with { };
        var namespaces = new Dictionary<string, string>(StringComparer.Ordinal) { ["xml"] = TreeValidation.XmlUri };
        foreach (var binding in Namespaces)
        {
            ArgumentNullException.ThrowIfNull(binding.Key); ArgumentNullException.ThrowIfNull(binding.Value);
            ChargeBinding((long)binding.Key.Length + binding.Value.Length);
            TreeValidation.Name(binding.Key);
            if (binding.Key == "xmlns" || binding.Value.Length == 0 || (binding.Key == "xml" && binding.Value != TreeValidation.XmlUri)) throw new ArgumentException("Invalid static namespace binding.", nameof(Namespaces));
            TreeValidation.Text(binding.Value);
            if (binding.Key == "xml") continue;
            namespaces.Add(binding.Key, binding.Value);
        }
        var functions = new Dictionary<XPathExpandedName, XPathFunctionSignature>();
        foreach (var function in Functions)
        {
            ModelGuard.Name(function.Key); ModelGuard.Signature(function.Value);
            if (function.Key != function.Value.Name) throw new ArgumentException("Signature key mismatch.", nameof(Functions));
            if (function.Key.NamespaceUri.Length == 0)
            {
                if (!XPathCoreSignatures.ByName.TryGetValue(function.Key, out var core) || core != function.Value) throw new ArgumentException("Invalid core function registration.", nameof(Functions));
            }
            if (function.Key.NamespaceUri.Length != 0) ChargeBinding((long)function.Key.NamespaceUri.Length + function.Key.LocalName.Length);
            functions.Add(function.Key, function.Value);
        }
        this.Namespaces = new ReadOnlyDictionary<string, string>(namespaces);
        this.Functions = new ReadOnlyDictionary<XPathExpandedName, XPathFunctionSignature>(functions);
    }
    internal static XPathStaticContext FromOptions(XPathCompileOptions? options)
    {
        options ??= new(); ArgumentNullException.ThrowIfNull(options.Functions);
        ArgumentNullException.ThrowIfNull(options.Limits); ArgumentNullException.ThrowIfNull(options.Namespaces);
        if (options.Limits.MaxTokens is int tokenLimit && (long)options.Namespaces.Count + options.Functions.Count > tokenLimit)
            throw new XPathCompileException(new("XPATH-COMPILE-LIMIT", XPathDiagnosticCategory.CompileLimit, "XPath static context limit exceeded.", default));
        var functions = new Dictionary<XPathExpandedName, XPathFunctionSignature>(XPathCoreSignatures.ByName);
        foreach (var signature in options.Functions)
        {
            ModelGuard.Signature(signature);
            if (signature.Name.NamespaceUri.Length == 0) throw new ArgumentException("Extensions require a namespace.", nameof(options));
            functions.Add(signature.Name, signature);
        }
        return new(options.Namespaces, functions, options.Limits);
    }
}
internal sealed record XPathDynamicContext
{
    internal IReadOnlyDictionary<XPathExpandedName, XPathValue> Variables { get; }
    internal IReadOnlyDictionary<XPathExpandedName, XPathExtensionFunction> Functions { get; }
    internal XPathEvaluationLimits Limits { get; }
    internal CancellationToken CancellationToken { get; }
    internal XPathDynamicContext(XPathContext? options)
    {
        options ??= new(); ArgumentNullException.ThrowIfNull(options.Variables); ArgumentNullException.ThrowIfNull(options.Functions); ArgumentNullException.ThrowIfNull(options.Limits);
        options.CancellationToken.ThrowIfCancellationRequested();
        var variables = new Dictionary<XPathExpandedName, XPathValue>();
        foreach (var variable in options.Variables) variables.Add(ModelGuard.Name(variable.Key), variable.Value);
        var functions = new Dictionary<XPathExpandedName, XPathExtensionFunction>();
        foreach (var function in options.Functions)
        {
            ModelGuard.Name(function.Key); ArgumentNullException.ThrowIfNull(function.Value);
            if (function.Key.NamespaceUri.Length == 0) throw new ArgumentException("Extensions require a namespace.", nameof(options));
            functions.Add(function.Key, function.Value);
        }
        Variables = new ReadOnlyDictionary<XPathExpandedName, XPathValue>(variables);
        Functions = new ReadOnlyDictionary<XPathExpandedName, XPathExtensionFunction>(functions);
        Limits = options.Limits with { }; CancellationToken = options.CancellationToken;
    }
}
