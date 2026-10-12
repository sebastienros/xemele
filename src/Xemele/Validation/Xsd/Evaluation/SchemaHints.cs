namespace Xemele.Validation;

internal sealed partial class XsdValidationRun
{
    private XsdHintNamespaces? hintNamespaces;

    // Check against preceding tags before recording any physical name on this
    // tag. This global bookkeeping also applies under skip; Values.cs alone
    // retains ownership of lexical/typed assessment and attribute identity hooks.
    private bool SchemaHints(XmlNode node)
    {
        if (hintNamespaces is null) { Retain(); hintNamespaces = new(); }
        bool valid = true;
        foreach (var attribute in node.Attributes)
        {
            current = document.OriginalLocation(attribute); Work();
            if (!XsdHintNamespaces.Equal(attribute.NamespaceUri, Xsi, this, current)) continue;
            bool late = false;
            if (XsdHintNamespaces.Equal(attribute.LocalName, "schemaLocation", this, current))
            {
                var remaining = attribute.Value.AsSpan();
                while (XsdHintNamespaces.Token(ref remaining, this, current, out var uri))
                {
                    // An unmatched token is the existing lexical assessor's
                    // concern. A complete pair retains neither token string.
                    if (!XsdHintNamespaces.Token(ref remaining, this, current, out _)) break;
                    late |= hintNamespaces.Contains(uri, this, current);
                }
            }
            else if (XsdHintNamespaces.Equal(attribute.LocalName, "noNamespaceSchemaLocation", this, current))
                late = hintNamespaces.Contains("", this, current);
            if (late) valid = Error("XSD.HintOrder", "Schema location hint follows an earlier physical namespace use.");
        }
        current = document.OriginalLocation(node);
        hintNamespaces.Add(node.NamespaceUri, this, current);
        foreach (var attribute in node.Attributes)
        {
            current = document.OriginalLocation(attribute); Work();
            hintNamespaces.Add(attribute.NamespaceUri, this, current);
        }
        current = document.OriginalLocation(node);
        return valid;
    }
}

// DOM-owned strings survive for this run only. Linear ordinal probes charge
// every candidate and compared UTF-16 unit, bounding even adversarial equal
// prefixes through the shared work ledger. Growth is reserved before allocation;
// the representational bound remains finite with null quotas. No hash collision
// behavior, hidden uncharged comparisons, token copies or static document state.
internal sealed class XsdHintNamespaces
{
    private string[]? names;
    private int count;
    internal bool Contains(ReadOnlySpan<char> name, ISchemaOperation op, XmlLocation at)
    {
        XsdEvaluationText.Work(op, at, 0);
        for (int i = 0; i < count; i++)
        {
            XsdEvaluationText.Work(op, at);
            if (Equal(name, names![i], op, at)) return true;
        }
        return false;
    }
    internal void Add(string name, ISchemaOperation op, XmlLocation at)
    {
        if (Contains(name, op, at)) return;
        if (count == Array.MaxLength)
        { op.Refuse("VALIDATION.Limit", "Namespace-use table capacity exceeded.", at); throw new XsdEvaluationStopped(); }
        if (names is null || count == names.Length)
        {
            int capacity = (int)Math.Min(Array.MaxLength, Math.Max(4L, (long)count * 2));
            XsdEvaluationText.Retain(op, at);
            var next = new string[capacity];
            for (int i = 0; i < count; i++) { XsdEvaluationText.Work(op, at); next[i] = names![i]; }
            names = next;
        }
        XsdEvaluationText.Retain(op, at);
        names[count++] = name;
    }
    internal static bool Equal(ReadOnlySpan<char> left, ReadOnlySpan<char> right, ISchemaOperation op, XmlLocation at)
    {
        XsdEvaluationText.Work(op, at, 0);
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
        {
            XsdEvaluationText.Work(op, at);
            if (left[i] != right[i]) return false;
        }
        return true;
    }
    internal static bool Token(ref ReadOnlySpan<char> remaining, ISchemaOperation op, XmlLocation at, out ReadOnlySpan<char> token)
    {
        while (!remaining.IsEmpty)
        {
            XsdEvaluationText.Work(op, at);
            if (!White(remaining[0])) break;
            remaining = remaining[1..];
        }
        int length = 0;
        while (length < remaining.Length)
        {
            XsdEvaluationText.Work(op, at);
            if (White(remaining[length])) break;
            length++;
        }
        token = remaining[..length]; remaining = remaining[length..];
        return length != 0;
    }
    private static bool White(char c) => c is ' ' or '\t' or '\r' or '\n';
}
