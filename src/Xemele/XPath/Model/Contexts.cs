using System.Collections.ObjectModel;
using Xemele.XPath.Internal;
namespace Xemele.XPath;

/// <summary>Identifies the stage or resource limit responsible for an XPath diagnostic.</summary>
public enum XPathDiagnosticCategory
{
    /// <summary>The expression has invalid syntax.</summary>
    Syntax,
    /// <summary>The static namespace or function context is invalid.</summary>
    StaticContext,
    /// <summary>A compilation resource limit was exceeded.</summary>
    CompileLimit,
    /// <summary>The expression could not be evaluated.</summary>
    Evaluation,
    /// <summary>An evaluation resource limit was exceeded.</summary>
    EvaluationLimit
}
/// <summary>Describes an XPath error and the corresponding expression source span.</summary>
/// <param name="Code">The nonempty diagnostic identifier.</param>
/// <param name="Category">The error category.</param>
/// <param name="Message">The nonempty message, limited to 256 UTF-16 code units.</param>
/// <param name="Span">The valid source span associated with the error.</param>
public sealed record XPathDiagnostic(string Code, XPathDiagnosticCategory Category, string Message, XPathSourceSpan Span)
{
    private string code = ModelGuard.Nonempty(Code);
    /// <summary>Gets the nonempty diagnostic identifier.</summary>
    public string Code
    {
        get => code; init => code = ModelGuard.Nonempty(value);
    }
    private XPathDiagnosticCategory category = ModelGuard.Enum(Category);
    /// <summary>Gets the stage or resource limit associated with the error.</summary>
    public XPathDiagnosticCategory Category
    {
        get => category; init => category = ModelGuard.Enum(value);
    }
    private string message = ModelGuard.Message(Message);
    /// <summary>Gets the nonempty error description, limited to 256 UTF-16 code units.</summary>
    public string Message
    {
        get => message; init => message = ModelGuard.Message(value);
    }
    private XPathSourceSpan span = ModelGuard.Span(Span);
    /// <summary>Gets the expression source span associated with the error.</summary>
    public XPathSourceSpan Span
    {
        get => span; init => span = ModelGuard.Span(value);
    }
}
/// <summary>Reports an XPath syntax, static-context, or compilation-limit error.</summary>
public sealed class XPathCompileException : Exception
{
    /// <summary>Creates a compilation exception from a validated diagnostic.</summary>
    /// <param name="diagnostic">A syntax, static-context, or compilation-limit diagnostic.</param>
    /// <exception cref="ArgumentException">The diagnostic has an incompatible category or invalid metadata.</exception>
    /// <exception cref="ArgumentNullException">The diagnostic is null.</exception>
    public XPathCompileException(XPathDiagnostic diagnostic) : base(ModelGuard.Diagnostic(diagnostic, true).Message) => Diagnostic = diagnostic;
    /// <summary>Gets the diagnostic that caused compilation to fail.</summary>
    public XPathDiagnostic Diagnostic
    {
        get;
    }
}
/// <summary>Reports an XPath evaluation or evaluation-limit error.</summary>
public sealed class XPathEvaluationException : Exception
{
    /// <summary>Creates an evaluation exception from a validated diagnostic.</summary>
    /// <param name="diagnostic">An evaluation or evaluation-limit diagnostic.</param>
    /// <exception cref="ArgumentException">The diagnostic has an incompatible category or invalid metadata.</exception>
    /// <exception cref="ArgumentNullException">The diagnostic is null.</exception>
    public XPathEvaluationException(XPathDiagnostic diagnostic) : base(ModelGuard.Diagnostic(diagnostic, false).Message) => Diagnostic = diagnostic;
    /// <summary>Gets the diagnostic that caused evaluation to fail.</summary>
    public XPathDiagnostic Diagnostic
    {
        get;
    }
}
/// <summary>Bounds XPath compilation; each limit must be positive or null to disable that limit.</summary>
public sealed record XPathCompileLimits
{
    private int? characters = 65536, tokens = 16384, nodes = 16384, depth = 256;
    /// <summary>Gets the expression and static-context UTF-16 character limit; the default is 65,536.</summary>
    public int? MaxExpressionCharacters
    {
        get => characters; init => characters = ModelGuard.Positive(value);
    }
    /// <summary>Gets the expression and static-context token limit; the default is 16,384.</summary>
    public int? MaxTokens
    {
        get => tokens; init => tokens = ModelGuard.Positive(value);
    }
    /// <summary>Gets the abstract-syntax-tree node limit; the default is 16,384.</summary>
    public int? MaxAstNodes
    {
        get => nodes; init => nodes = ModelGuard.Positive(value);
    }
    /// <summary>Gets the expression nesting limit; the default is 256.</summary>
    public int? MaxDepth
    {
        get => depth; init => depth = ModelGuard.Positive(value);
    }
}
/// <summary>Bounds an XPath evaluation; each limit must be positive or null to disable that limit.</summary>
public sealed record XPathEvaluationLimits
{
    internal static readonly XPathEvaluationLimits Default = new();
    private long? operations = 1000000, characters = 16000000;
    private int? input = 100000, nodes = 100000, strings = 1000000;
    /// <summary>Gets the operation-work limit, including explicitly charged extension work; the default is 1,000,000.</summary>
    public long? MaxOperations
    {
        get => operations; init => operations = ModelGuard.Positive(value);
    }
    /// <summary>Gets the input-snapshot item limit; the default is 100,000.</summary>
    public int? MaxInputItems
    {
        get => input; init => input = ModelGuard.Positive(value);
    }
    /// <summary>Gets the input-snapshot character limit; the default is 16,000,000.</summary>
    public long? MaxInputCharacters
    {
        get => characters; init => characters = ModelGuard.Positive(value);
    }
    /// <summary>Gets the maximum number of items in a node-set; the default is 100,000.</summary>
    public int? MaxNodeSetItems
    {
        get => nodes; init => nodes = ModelGuard.Positive(value);
    }
    /// <summary>Gets the UTF-16 code-unit limit for a result string; the default is 1,000,000.</summary>
    public int? MaxStringCharacters
    {
        get => strings; init => strings = ModelGuard.Positive(value);
    }
}
/// <summary>Declares the expanded name and permitted arity of an XPath extension function.</summary>
/// <param name="Name">The function's validated expanded name.</param>
/// <param name="MinArguments">The nonnegative minimum argument count.</param>
/// <param name="MaxArguments">The maximum argument count, or null for no upper bound.</param>
public sealed record XPathFunctionSignature(XPathExpandedName Name, int MinArguments, int? MaxArguments)
{
    private XPathExpandedName name = ModelGuard.Name(Name);
    private int minimum = ModelGuard.Arity(MinArguments, MaxArguments);
    private int? maximum = MaxArguments;
    /// <summary>Gets the function's namespace URI and local name.</summary>
    public XPathExpandedName Name
    {
        get => name; init => name = ModelGuard.Name(value);
    }
    /// <summary>Gets the minimum accepted argument count.</summary>
    public int MinArguments
    {
        get => minimum; init => minimum = ModelGuard.Arity(value, maximum);
    }
    /// <summary>Gets the maximum accepted argument count, or null for no upper bound.</summary>
    public int? MaxArguments
    {
        get => maximum; init
        {
            ModelGuard.Arity(minimum, value);
            maximum = value;
        }
    }
}
/// <summary>Provides the static namespace bindings, extension signatures, and limits for XPath compilation.</summary>
public sealed record XPathCompileOptions
{
    /// <summary>Gets the prefix-to-namespace-URI bindings snapshotted during compilation.</summary>
    public IReadOnlyDictionary<string, string> Namespaces { get; init; } = new Dictionary<string, string>();
    /// <summary>Gets the extension-function signatures available during compilation.</summary>
    public IReadOnlyList<XPathFunctionSignature> Functions { get; init; } = [];
    /// <summary>Gets the resource limits applied to compilation.</summary>
    public XPathCompileLimits Limits { get; init; } = new();
}
/// <summary>Provides variable values, extension implementations, limits, and cancellation for one XPath evaluation.</summary>
public sealed record XPathContext
{
    /// <summary>Gets the variable bindings keyed by expanded name and snapshotted at evaluation entry.</summary>
    public IReadOnlyDictionary<XPathExpandedName, XPathValue> Variables { get; init; } = new Dictionary<XPathExpandedName, XPathValue>();
    /// <summary>Gets the extension implementations keyed by their compiled expanded names.</summary>
    public IReadOnlyDictionary<XPathExpandedName, XPathExtensionFunction> Functions { get; init; } = new Dictionary<XPathExpandedName, XPathExtensionFunction>();
    /// <summary>Gets the resource limits applied to this evaluation.</summary>
    public XPathEvaluationLimits Limits { get; init; } = new();
    /// <summary>Gets the token checked during evaluation and exposed to extension functions.</summary>
    public CancellationToken CancellationToken
    {
        get; init;
    }
}
/// <summary>Implements an XPath extension function.</summary>
/// <param name="context">The current focus and shared cancellation and operation budget.</param>
/// <param name="arguments">The evaluated arguments in call order.</param>
/// <returns>The function's XPath result.</returns>
/// <remarks>Callback exceptions propagate. Charge additional work through <see cref="XPathFunctionContext.ChargeOperations"/>.</remarks>
public delegate XPathValue XPathExtensionFunction(XPathFunctionContext context, IReadOnlyList<XPathValue> arguments);
/// <summary>Exposes the evaluation focus and shared budget to an XPath extension function.</summary>
public sealed class XPathFunctionContext
{
    private readonly IXPathBudget budget;
    internal XPathFunctionContext(XPathFocus focus, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (focus.Item.IsDefault || focus.Position < 1 || focus.Size < focus.Position)
        {
            throw new ArgumentException("Invalid function focus.", nameof(focus));
        }

        ContextItem = focus.Item;
        Position = focus.Position;
        Size = focus.Size;
        this.budget = budget;
    }
    /// <summary>Gets the current context item captured by the evaluation.</summary>
    public XPathItem ContextItem
    {
        get;
    }
    /// <summary>Gets the one-based position of the context item in the current focus.</summary>
    public int Position
    {
        get;
    }
    /// <summary>Gets the number of items in the current focus.</summary>
    public int Size
    {
        get;
    }
    /// <summary>Gets the evaluation's cancellation token.</summary>
    public CancellationToken CancellationToken => budget.CancellationToken;
    /// <summary>Charges extension work against the evaluation's shared operation budget.</summary>
    /// <param name="operations">The nonnegative number of work units; zero still checks cancellation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The charge is negative.</exception>
    /// <exception cref="XPathEvaluationException">The operation budget is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public void ChargeOperations(long operations)
    {
        if (operations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operations));
        }

        budget.ChargeOperations(operations);
    }
}
internal static class ModelGuard
{
    internal static string Nonempty(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Empty string.", nameof(value));
        }

        return value;
    }
    internal static string Message(string value)
    {
        Nonempty(value);
        if (value.Length > 256)
        {
            throw new ArgumentException("Diagnostic message exceeds 256 UTF-16 units.", nameof(value));
        }

        return value;
    }
    internal static T Enum<T>(T value) where T : struct, Enum
    {
        if (!System.Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }
    internal static XPathSourceSpan Span(XPathSourceSpan value)
    {
        if (value.Start < 0 || value.Length < 0 || value.Start > int.MaxValue - value.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }
    internal static XPathExpandedName Name(XPathExpandedName value) => Name(value, default);
    internal static XPathExpandedName Name(XPathExpandedName value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value.NamespaceUri);
        ContextScan.Name(value.LocalName, cancellationToken);
        return value;
    }
    internal static int? Positive(int? value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }
    internal static long? Positive(long? value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value;
    }
    internal static int Arity(int min, int? max)
    {
        if (min < 0 || max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(min));
        }

        return min;
    }
    internal static XPathFunctionSignature Signature(XPathFunctionSignature signature) => Signature(signature, default);
    internal static XPathFunctionSignature Signature(XPathFunctionSignature signature, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signature);
        Name(signature.Name, cancellationToken);
        Arity(signature.MinArguments, signature.MaxArguments);
        return signature;
    }
    internal static XPathDiagnostic Diagnostic(XPathDiagnostic diagnostic, bool compile)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        Nonempty(diagnostic.Code);
        Message(diagnostic.Message);
        Span(diagnostic.Span);
        Enum(diagnostic.Category);
        if (compile ? diagnostic.Category is not (XPathDiagnosticCategory.Syntax or XPathDiagnosticCategory.StaticContext or XPathDiagnosticCategory.CompileLimit) : diagnostic.Category is not (XPathDiagnosticCategory.Evaluation or XPathDiagnosticCategory.EvaluationLimit))
        {
            throw new ArgumentException("Diagnostic category does not match exception.", nameof(diagnostic));
        }

        return diagnostic;
    }
    internal static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> values) where T : class => Snapshot(values, default);
    internal static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> values, CancellationToken cancellationToken) where T : class
    {
        ArgumentNullException.ThrowIfNull(values);
        cancellationToken.ThrowIfCancellationRequested();
        var count = values.Count;
        cancellationToken.ThrowIfCancellationRequested();
        var owned = new T[count];
        for (var i = 0; i < owned.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owned[i] = values[i];
            ArgumentNullException.ThrowIfNull(owned[i]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Array.AsReadOnly(owned);
    }
}
