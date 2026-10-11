using System.Collections.ObjectModel;
using Xemele.XPath.Internal;
namespace Xemele.XPath;

public enum XPathDiagnosticCategory { Syntax, StaticContext, CompileLimit, Evaluation, EvaluationLimit }
public sealed record XPathDiagnostic(string Code, XPathDiagnosticCategory Category, string Message, XPathSourceSpan Span)
{
    private string code = ModelGuard.Nonempty(Code);
    public string Code { get => code; init => code = ModelGuard.Nonempty(value); }
    private XPathDiagnosticCategory category = ModelGuard.Enum(Category);
    public XPathDiagnosticCategory Category { get => category; init => category = ModelGuard.Enum(value); }
    private string message = ModelGuard.Message(Message);
    public string Message { get => message; init => message = ModelGuard.Message(value); }
    private XPathSourceSpan span = ModelGuard.Span(Span);
    public XPathSourceSpan Span { get => span; init => span = ModelGuard.Span(value); }
}
public sealed class XPathCompileException : Exception
{
    public XPathCompileException(XPathDiagnostic diagnostic) : base(ModelGuard.Diagnostic(diagnostic, true).Message) => Diagnostic = diagnostic;
    public XPathDiagnostic Diagnostic { get; }
}
public sealed class XPathEvaluationException : Exception
{
    public XPathEvaluationException(XPathDiagnostic diagnostic) : base(ModelGuard.Diagnostic(diagnostic, false).Message) => Diagnostic = diagnostic;
    public XPathDiagnostic Diagnostic { get; }
}
public sealed record XPathCompileLimits
{
    private int? characters = 65536, tokens = 16384, nodes = 16384, depth = 256;
    public int? MaxExpressionCharacters { get => characters; init => characters = ModelGuard.Positive(value); }
    public int? MaxTokens { get => tokens; init => tokens = ModelGuard.Positive(value); }
    public int? MaxAstNodes { get => nodes; init => nodes = ModelGuard.Positive(value); }
    public int? MaxDepth { get => depth; init => depth = ModelGuard.Positive(value); }
}
public sealed record XPathEvaluationLimits
{
    private long? operations = 1000000, characters = 16000000;
    private int? input = 100000, nodes = 100000, strings = 1000000;
    public long? MaxOperations { get => operations; init => operations = ModelGuard.Positive(value); }
    public int? MaxInputItems { get => input; init => input = ModelGuard.Positive(value); }
    public long? MaxInputCharacters { get => characters; init => characters = ModelGuard.Positive(value); }
    public int? MaxNodeSetItems { get => nodes; init => nodes = ModelGuard.Positive(value); }
    public int? MaxStringCharacters { get => strings; init => strings = ModelGuard.Positive(value); }
}
public sealed record XPathFunctionSignature(XPathExpandedName Name, int MinArguments, int? MaxArguments)
{
    private XPathExpandedName name = ModelGuard.Name(Name);
    private int minimum = ModelGuard.Arity(MinArguments, MaxArguments);
    private int? maximum = MaxArguments;
    public XPathExpandedName Name { get => name; init => name = ModelGuard.Name(value); }
    public int MinArguments { get => minimum; init => minimum = ModelGuard.Arity(value, maximum); }
    public int? MaxArguments { get => maximum; init { ModelGuard.Arity(minimum, value); maximum = value; } }
}
public sealed record XPathCompileOptions
{
    public IReadOnlyDictionary<string, string> Namespaces { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<XPathFunctionSignature> Functions { get; init; } = Array.Empty<XPathFunctionSignature>();
    public XPathCompileLimits Limits { get; init; } = new();
}
public sealed record XPathContext
{
    public IReadOnlyDictionary<XPathExpandedName, XPathValue> Variables { get; init; } = new Dictionary<XPathExpandedName, XPathValue>();
    public IReadOnlyDictionary<XPathExpandedName, XPathExtensionFunction> Functions { get; init; } = new Dictionary<XPathExpandedName, XPathExtensionFunction>();
    public XPathEvaluationLimits Limits { get; init; } = new();
    public CancellationToken CancellationToken { get; init; }
}
public delegate XPathValue XPathExtensionFunction(XPathFunctionContext context, IReadOnlyList<XPathValue> arguments);
public sealed class XPathFunctionContext
{
    private readonly IXPathBudget budget;
    internal XPathFunctionContext(XPathFocus focus, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (focus.Item.IsDefault || focus.Position < 1 || focus.Size < focus.Position) throw new ArgumentException("Invalid function focus.", nameof(focus));
        ContextItem = focus.Item; Position = focus.Position; Size = focus.Size; this.budget = budget;
    }
    public XPathItem ContextItem { get; }
    public int Position { get; }
    public int Size { get; }
    public CancellationToken CancellationToken => budget.CancellationToken;
    public void ChargeOperations(long operations) { if (operations < 0) throw new ArgumentOutOfRangeException(nameof(operations)); budget.ChargeOperations(operations); }
}
internal static class ModelGuard
{
    internal static string Nonempty(string value) { ArgumentNullException.ThrowIfNull(value); if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Empty string.", nameof(value)); return value; }
    internal static string Message(string value) { Nonempty(value); if (value.Length > 256) throw new ArgumentException("Diagnostic message exceeds 256 UTF-16 units.", nameof(value)); return value; }
    internal static T Enum<T>(T value) where T : struct, Enum { if (!System.Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); return value; }
    internal static XPathSourceSpan Span(XPathSourceSpan value) { if (value.Start < 0 || value.Length < 0 || value.Start > int.MaxValue - value.Length) throw new ArgumentOutOfRangeException(nameof(value)); return value; }
    internal static XPathExpandedName Name(XPathExpandedName value) { ArgumentNullException.ThrowIfNull(value.NamespaceUri); TreeValidation.Name(value.LocalName); return value; }
    internal static int? Positive(int? value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); return value; }
    internal static long? Positive(long? value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); return value; }
    internal static int Arity(int min, int? max) { if (min < 0 || max < min) throw new ArgumentOutOfRangeException(nameof(min)); return min; }
    internal static XPathFunctionSignature Signature(XPathFunctionSignature signature)
    { ArgumentNullException.ThrowIfNull(signature); Name(signature.Name); Arity(signature.MinArguments, signature.MaxArguments); return signature; }
    internal static XPathDiagnostic Diagnostic(XPathDiagnostic diagnostic, bool compile)
    {
        ArgumentNullException.ThrowIfNull(diagnostic); Nonempty(diagnostic.Code); Message(diagnostic.Message); Span(diagnostic.Span); Enum(diagnostic.Category);
        if (compile ? diagnostic.Category is not (XPathDiagnosticCategory.Syntax or XPathDiagnosticCategory.StaticContext or XPathDiagnosticCategory.CompileLimit) : diagnostic.Category is not (XPathDiagnosticCategory.Evaluation or XPathDiagnosticCategory.EvaluationLimit))
            throw new ArgumentException("Diagnostic category does not match exception.", nameof(diagnostic));
        return diagnostic;
    }
    internal static IReadOnlyList<T> Snapshot<T>(IReadOnlyList<T> values) where T : class
    {
        ArgumentNullException.ThrowIfNull(values); var owned = new T[values.Count];
        for (int i = 0; i < owned.Length; i++) { owned[i] = values[i]; ArgumentNullException.ThrowIfNull(owned[i]); }
        return Array.AsReadOnly(owned);
    }
}
