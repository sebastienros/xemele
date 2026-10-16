// Immutable owned model and independently implementable lane seams.
using Xemele.XPath;
namespace Xemele.XPath.Internal;

internal enum XPathAxis
{
    Child, Descendant, Parent, Ancestor, FollowingSibling, PrecedingSibling, Following, Preceding, Attribute, Namespace, Self, DescendantOrSelf, AncestorOrSelf
}
internal enum XPathBinaryOperator
{
    Or, And, Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual, Add, Subtract, Multiply, Divide, Modulo, Union
}
internal enum XPathNodeTestKind
{
    Name, AnyPrincipal, NamespaceWildcard, Node, Text, Comment, ProcessingInstruction
}
internal sealed record XPathNodeTest
{
    internal XPathNodeTestKind Kind
    {
        get;
    }
    internal XPathExpandedName Name
    {
        get;
    }
    internal string? ProcessingInstructionTarget
    {
        get;
    }
    internal XPathNodeTest(XPathNodeTestKind Kind, XPathExpandedName Name, string? ProcessingInstructionTarget) : this(Kind, Name, ProcessingInstructionTarget, default) { }
    internal XPathNodeTest(XPathNodeTestKind Kind, XPathExpandedName Name, string? ProcessingInstructionTarget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ModelGuard.Enum(Kind);
        if (Kind == XPathNodeTestKind.Name)
        {
            ModelGuard.Name(Name, cancellationToken);
        }
        else if (Kind == XPathNodeTestKind.NamespaceWildcard)
        {
            if (string.IsNullOrEmpty(Name.NamespaceUri) || !string.IsNullOrEmpty(Name.LocalName))
            {
                throw new ArgumentException("Invalid namespace wildcard.", nameof(Name));
            }
        }
        else if (Name != default)
        {
            throw new ArgumentException("Unexpected name payload.", nameof(Name));
        }

        if (Kind != XPathNodeTestKind.ProcessingInstruction && ProcessingInstructionTarget is not null)
        {
            throw new ArgumentException("Unexpected PI payload.", nameof(ProcessingInstructionTarget));
        }

        if (ProcessingInstructionTarget is not null)
        {
            ContextScan.Text(ProcessingInstructionTarget, cancellationToken);
        }

        this.Kind = Kind;
        this.Name = Name;
        this.ProcessingInstructionTarget = ProcessingInstructionTarget;
    }
}
internal abstract record XPathAst
{
    public XPathSourceSpan Span
    {
        get;
    }
    protected XPathAst(XPathSourceSpan Span)
    {
        this.Span = ModelGuard.Span(Span);
    }
}
internal sealed record XPathLiteral : XPathAst
{
    public XPathValue Value
    {
        get;
    }
    internal XPathLiteral(XPathValue Value, XPathSourceSpan Span) : base(Span)
    {
        this.Value = Value;
        if (Value.Kind is not (XPathValueKind.String or XPathValueKind.Number))
        {
            throw new ArgumentException("Literal must be string or number.", nameof(Value));
        }
    }
}
internal sealed record XPathVariable : XPathAst
{
    public XPathExpandedName Name
    {
        get;
    }
    internal XPathVariable(XPathExpandedName Name, XPathSourceSpan Span) : this(Name, Span, default) { }
    internal XPathVariable(XPathExpandedName Name, XPathSourceSpan Span, CancellationToken cancellationToken) : base(Span)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Name = ModelGuard.Name(Name, cancellationToken);
    }
}
internal sealed record XPathUnaryMinus : XPathAst
{
    public XPathAst Operand
    {
        get;
    }
    internal XPathUnaryMinus(XPathAst Operand, XPathSourceSpan Span) : base(Span)
    {
        this.Operand = Operand ?? throw new ArgumentNullException(nameof(Operand));
    }
}
internal sealed record XPathBinary : XPathAst
{
    public XPathBinaryOperator Operator
    {
        get;
    }
    public XPathAst Left
    {
        get;
    }
    public XPathAst Right
    {
        get;
    }
    internal XPathBinary(XPathBinaryOperator Operator, XPathAst Left, XPathAst Right, XPathSourceSpan Span) : base(Span)
    {
        this.Operator = ModelGuard.Enum(Operator);
        this.Left = Left ?? throw new ArgumentNullException(nameof(Left));
        this.Right = Right ?? throw new ArgumentNullException(nameof(Right));
    }
}
internal sealed record XPathCall : XPathAst
{
    public XPathFunctionSignature Signature
    {
        get;
    }
    public IReadOnlyList<XPathAst> Arguments
    {
        get;
    }
    internal XPathCall(XPathFunctionSignature Signature, IReadOnlyList<XPathAst> Arguments, XPathSourceSpan Span) : this(Signature, Arguments, Span, default) { }
    internal XPathCall(XPathFunctionSignature Signature, IReadOnlyList<XPathAst> Arguments, XPathSourceSpan Span, CancellationToken cancellationToken) : base(Span)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Signature = ModelGuard.Signature(Signature, cancellationToken);
        this.Arguments = ModelGuard.Snapshot(Arguments, cancellationToken);
        if (this.Arguments.Count < Signature.MinArguments || (Signature.MaxArguments is int max && this.Arguments.Count > max))
        {
            throw new ArgumentException("Invalid call arity.", nameof(Arguments));
        }
    }
}
internal sealed record XPathFilter : XPathAst
{
    public XPathAst Input
    {
        get;
    }
    public IReadOnlyList<XPathAst> Predicates
    {
        get;
    }
    internal XPathFilter(XPathAst Input, IReadOnlyList<XPathAst> Predicates, XPathSourceSpan Span) : this(Input, Predicates, Span, default) { }
    internal XPathFilter(XPathAst Input, IReadOnlyList<XPathAst> Predicates, XPathSourceSpan Span, CancellationToken cancellationToken) : base(Span)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Input = Input ?? throw new ArgumentNullException(nameof(Input));
        this.Predicates = ModelGuard.Snapshot(Predicates, cancellationToken);
    }
}
internal sealed record XPathStep
{
    public XPathAxis Axis
    {
        get;
    }
    public XPathNodeTest Test
    {
        get;
    }
    public IReadOnlyList<XPathAst> Predicates
    {
        get;
    }
    public XPathSourceSpan Span
    {
        get;
    }
    internal XPathStep(XPathAxis Axis, XPathNodeTest Test, IReadOnlyList<XPathAst> Predicates, XPathSourceSpan Span) : this(Axis, Test, Predicates, Span, default) { }
    internal XPathStep(XPathAxis Axis, XPathNodeTest Test, IReadOnlyList<XPathAst> Predicates, XPathSourceSpan Span, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Axis = ModelGuard.Enum(Axis);
        this.Test = Test ?? throw new ArgumentNullException(nameof(Test));
        this.Predicates = ModelGuard.Snapshot(Predicates, cancellationToken);
        this.Span = ModelGuard.Span(Span);
    }
}
internal sealed record XPathPath : XPathAst
{
    public bool Absolute
    {
        get;
    }
    public XPathAst? Input
    {
        get;
    }
    public IReadOnlyList<XPathStep> Steps
    {
        get;
    }
    internal XPathPath(bool Absolute, XPathAst? Input, IReadOnlyList<XPathStep> Steps, XPathSourceSpan Span) : this(Absolute, Input, Steps, Span, default) { }
    internal XPathPath(bool Absolute, XPathAst? Input, IReadOnlyList<XPathStep> Steps, XPathSourceSpan Span, CancellationToken cancellationToken) : base(Span)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Absolute = Absolute;
        this.Input = Input;
        this.Steps = ModelGuard.Snapshot(Steps, cancellationToken);
        if (Absolute && Input is not null)
        {
            throw new ArgumentException("Absolute path cannot have an input.", nameof(Input));
        }
    }
}

internal interface IXPathBudget
{
    CancellationToken CancellationToken
    {
        get;
    }
    void ChargeOperations(long amount);
    void CheckNodeSetSize(int count);
    void CheckStringSize(int utf16Characters);
}
internal interface IXPathCompiler
{
    XPathAst Compile(string expression, XPathStaticContext context, CancellationToken cancellationToken);
}
internal readonly record struct XPathFocus(XPathItem Item, int Position, int Size, int Index = -1);
internal interface IXPathNavigator
{
    XPathItem Root
    {
        get;
    }
    IReadOnlyList<XPathItem> SelectAxis(XPathItem item, XPathAxis axis, IXPathBudget budget);
    int CompareDocumentOrder(XPathItem left, XPathItem right, IXPathBudget budget);
    IReadOnlyList<XPathItem> SelectId(string id, IXPathBudget budget);
    string? FindLanguage(XPathItem item, IXPathBudget budget);
}
internal interface IXPathFunctionLibrary
{
    IReadOnlyList<XPathFunctionSignature> Signatures
    {
        get;
    }
    XPathValue Invoke(XPathFunctionSignature function, XPathFocus focus, IReadOnlyList<XPathValue> arguments, IXPathNavigator navigator, IXPathBudget budget);
}
