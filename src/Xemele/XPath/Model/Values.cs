using System.Globalization;
using System.Text;
using Xemele.Internal;
namespace Xemele.XPath;

/// <summary>Identifies one of the four XPath 1.0 value types.</summary>
public enum XPathValueKind
{
    /// <summary>A set of logical nodes in captured document order.</summary>
    NodeSet = 0,
    /// <summary>A true or false value.</summary>
    Boolean = 1,
    /// <summary>An IEEE 754 double-precision number.</summary>
    Number = 2,
    /// <summary>A character string.</summary>
    String = 3
}
/// <summary>Identifies a logical node in the XPath 1.0 data model.</summary>
public enum XPathItemKind
{
    /// <summary>The document's root node.</summary>
    Root = 0,
    /// <summary>An element node.</summary>
    Element = 1,
    /// <summary>A nonempty logical run of adjacent text and CDATA nodes.</summary>
    Text = 2,
    /// <summary>A comment node.</summary>
    Comment = 3,
    /// <summary>A processing-instruction node.</summary>
    ProcessingInstruction = 4,
    /// <summary>An attribute node associated with an element.</summary>
    Attribute = 5,
    /// <summary>An in-scope namespace binding associated with an element.</summary>
    Namespace = 6
}
/// <summary>Identifies an XPath variable or function independently of its lexical prefix.</summary>
public readonly record struct XPathExpandedName
{
    private readonly string namespaceUri, localName;
    /// <summary>Gets the namespace URI, or an empty string for no namespace.</summary>
    public string NamespaceUri
    {
        get => namespaceUri; init
        {
            ArgumentNullException.ThrowIfNull(value);
            namespaceUri = value;
        }
    }
    /// <summary>Gets the validated local name.</summary>
    public string LocalName
    {
        get => localName; init
        {
            TreeValidation.Name(value);
            localName = value;
        }
    }
    /// <summary>Creates an expanded name from a namespace URI and validated local name.</summary>
    /// <param name="NamespaceUri">The namespace URI, or an empty string for no namespace.</param>
    /// <param name="LocalName">The XML local name without a prefix.</param>
    /// <exception cref="ArgumentNullException">A name component is null.</exception>
    /// <exception cref="ArgumentException">The local name is invalid.</exception>
    public XPathExpandedName(string NamespaceUri, string LocalName)
    {
        namespaceUri = null!;
        localName = null!;
        this.NamespaceUri = NamespaceUri;
        this.LocalName = LocalName;
    }
    internal XPathExpandedName(string NamespaceUri, string LocalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(NamespaceUri);
        Internal.ContextScan.Name(LocalName, cancellationToken);
        namespaceUri = NamespaceUri;
        localName = LocalName;
    }
    private XPathExpandedName(string uri)
    {
        namespaceUri = uri;
        localName = "";
    }
    internal static XPathExpandedName NamespaceWildcard(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Length == 0)
        {
            throw new ArgumentException("Empty wildcard URI.", nameof(uri));
        }

        return new(uri);
    }
    /// <summary>Extracts the expanded name's components.</summary>
    /// <param name="NamespaceUri">Receives the namespace URI.</param>
    /// <param name="LocalName">Receives the local name.</param>
    public void Deconstruct(out string NamespaceUri, out string LocalName)
    {
        NamespaceUri = this.NamespaceUri;
        LocalName = this.LocalName;
    }
}
/// <summary>Identifies a zero-based UTF-16 span within an XPath expression.</summary>
public readonly record struct XPathSourceSpan
{
    private readonly int start, length;
    /// <summary>Gets the nonnegative starting character offset.</summary>
    public int Start
    {
        get => start; init
        {
            if (value < 0 || value > int.MaxValue - length)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            start = value;
        }
    }
    /// <summary>Gets the nonnegative number of UTF-16 code units in the span.</summary>
    public int Length
    {
        get => length; init
        {
            if (value < 0 || value > int.MaxValue - start)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            length = value;
        }
    }
    /// <summary>Creates a source span whose end fits in a signed 32-bit character offset.</summary>
    /// <param name="Start">The nonnegative starting character offset.</param>
    /// <param name="Length">The nonnegative number of UTF-16 code units.</param>
    /// <exception cref="ArgumentOutOfRangeException">A component is negative or the end offset overflows.</exception>
    public XPathSourceSpan(int Start, int Length)
    {
        start = length = 0;
        this.Start = Start;
        this.Length = Length;
    }
    /// <summary>Extracts the source span's starting offset and length.</summary>
    /// <param name="Start">Receives the starting offset.</param>
    /// <param name="Length">Receives the length in UTF-16 code units.</param>
    public void Deconstruct(out int Start, out int Length)
    {
        Start = this.Start;
        Length = this.Length;
    }
}
/// <summary>Represents an immutable XPath 1.0 value; the default value is an empty node-set.</summary>
public readonly struct XPathValue
{
    private readonly XPathValueKind kind;
    private readonly double number;
    private readonly string? text;
    private readonly IReadOnlyList<XPathItem>? items;
    private XPathValue(XPathValueKind kind, double number = 0, string? text = null, IReadOnlyList<XPathItem>? items = null)
    {
        this.kind = kind;
        this.number = number;
        this.text = text;
        this.items = items;
    }
    /// <summary>Gets the stored XPath value type.</summary>
    public XPathValueKind Kind => kind;
    /// <summary>Creates a numeric XPath value.</summary>
    /// <param name="value">The number, including NaN or infinity when required.</param>
    /// <returns>The numeric value.</returns>
    public static XPathValue FromNumber(double value) => new(XPathValueKind.Number, value);
    /// <summary>Creates a Boolean XPath value.</summary>
    /// <param name="value">The Boolean to store.</param>
    /// <returns>The Boolean value.</returns>
    public static XPathValue FromBoolean(bool value) => new(XPathValueKind.Boolean, value ? 1 : 0);
    /// <summary>Creates a string XPath value.</summary>
    /// <param name="value">The nonnull string to retain.</param>
    /// <returns>The string value.</returns>
    /// <exception cref="ArgumentNullException">The string is null.</exception>
    public static XPathValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(XPathValueKind.String, text: value);
    }
    /// <summary>Materializes a node-set, removing duplicate identities and sorting by current document order.</summary>
    /// <param name="items">The items from one document, enumerated once.</param>
    /// <returns>An owned, read-only membership snapshot retaining each item's captured metadata.</returns>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    /// <exception cref="ArgumentException">An item is default, detached, absent, or belongs to a different document.</exception>
    public static XPathValue FromNodeSet(IEnumerable<XPathItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var unique = new HashSet<XPathItem>();
        var owned = new List<XPathItem>();
        XmlDocument? document = null;
        // Caller enumeration executes once. Item metadata is already an owned snapshot.
        foreach (var item in items)
        {
            if (item.IsDefault)
            {
                throw new ArgumentException("Default node-set item.", nameof(items));
            }

            document ??= item.Document;
            if (!ReferenceEquals(document, item.Document))
            {
                throw new ArgumentException("Mixed node-set documents.", nameof(items));
            }

            if (unique.Add(item))
            {
                owned.Add(item);
            }
        }
        if (owned.Count == 0)
        {
            return default;
        }
        // Topology is read once at this materialization boundary, never by the retained result.
        var order = ItemOrder.Capture(document!);
        foreach (var item in owned)
        {
            if (!order.ContainsKey(item.Identity))
            {
                throw new ArgumentException("Detached or absent node-set identity.", nameof(items));
            }
        }

        owned.Sort((a, b) => order[a.Identity].CompareTo(order[b.Identity]));
        return new(XPathValueKind.NodeSet, items: Array.AsReadOnly(owned.ToArray()));
    }
    // Input is already normalized against the navigator's entry snapshot. Never
    // query live handles here: an extension may have removed/reordered that tree.
    internal static XPathValue FromSnapshotNodeSet(IReadOnlyList<XPathItem> normalized, Internal.IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        ArgumentNullException.ThrowIfNull(budget);
        budget.ChargeOperations(0);
        budget.CheckNodeSetSize(normalized.Count);
        if (normalized.Count == 0)
        {
            return default;
        }

        budget.ChargeOperations(normalized.Count); // reject before destination growth
        if (normalized is Internal.XPathNodeList)
        {
            return new(XPathValueKind.NodeSet, items: normalized);
        }

        var owned = new XPathItem[normalized.Count];
        for (var i = 0; i < owned.Length; i++)
        {
            budget.ChargeOperations(0);
            var item = normalized[i];
            if (item.IsDefault)
            {
                throw new ArgumentException("Default snapshot item.", nameof(normalized));
            }

            if (i != 0 && !ReferenceEquals(owned[0].Identity.Node.Document, item.Identity.Node.Document))
            {
                throw new ArgumentException("Mixed snapshot documents.", nameof(normalized));
            }

            owned[i] = item;
        }
        budget.ChargeOperations(0);
        return new(XPathValueKind.NodeSet, items: Array.AsReadOnly(owned));
    }
    // Node-sets inside the engine are views over the evaluation snapshot; owned
    // copies are handed out before the snapshot could be retained by callers.
    internal XPathValue Detached() => kind == XPathValueKind.NodeSet && items is Internal.XPathNodeList list
        ? new(XPathValueKind.NodeSet, items: Array.AsReadOnly(list.ToItems()))
        : this;
    /// <summary>Gets the stored node-set without performing a type conversion.</summary>
    /// <returns>The captured node-set membership in document order.</returns>
    /// <exception cref="InvalidOperationException">The value is not a node-set.</exception>
    public IReadOnlyList<XPathItem> AsNodeSet() => kind == XPathValueKind.NodeSet ? items ?? [] : throw new InvalidOperationException("Value is not a node-set.");
    /// <summary>Converts this value to a Boolean using XPath 1.0 conversion rules.</summary>
    /// <returns>False for empty node-sets or strings, zero, and NaN; otherwise the corresponding Boolean value.</returns>
    public bool AsBoolean() => kind switch { XPathValueKind.NodeSet => items is { Count: > 0 }, XPathValueKind.Boolean => number != 0, XPathValueKind.Number => number != 0 && !double.IsNaN(number), _ => text!.Length != 0 };
    /// <summary>Converts this value to a number using XPath 1.0 conversion rules.</summary>
    /// <returns>The numeric value, or NaN when its string value is not a valid XPath number.</returns>
    public double AsNumber() => kind switch { XPathValueKind.Number or XPathValueKind.Boolean => number, _ => XPathConversions.ParseNumber(AsString()) };
    /// <summary>Converts this value to a string using XPath 1.0 conversion rules.</summary>
    /// <returns>The string value; node-sets use their first captured item, or an empty string when empty.</returns>
    public string AsString() => kind switch { XPathValueKind.String => text!, XPathValueKind.Boolean => number == 0 ? "false" : "true", XPathValueKind.Number => XPathConversions.FormatNumber(number), _ => items is { Count: > 0 } ? items[0].StringValue : "" };
}
internal static class XPathConversions
{
    internal static bool IsSpace(char c) => c is ' ' or '\t' or '\r' or '\n';
    internal static double ParseNumber(ReadOnlySpan<char> input)
    {
        int start = 0, end = input.Length;
        while (start < end && IsSpace(input[start]))
        {
            start++;
        }

        while (end > start && IsSpace(input[end - 1]))
        {
            end--;
        }

        var value = input[start..end];
        int i = 0, digits = 0;
        if (!value.IsEmpty && value[0] == '-')
        {
            i++;
        }

        while (i < value.Length && value[i] is >= '0' and <= '9')
        {
            i++;
            digits++;
        }
        if (i < value.Length && value[i] == '.')
        {
            i++;
            while (i < value.Length && value[i] is >= '0' and <= '9')
            {
                i++;
                digits++;
            }
        }
        if (digits == 0 || i != value.Length)
        {
            return double.NaN;
        }

        return double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) ? result : double.NaN;
    }
    internal static string FormatNumber(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        if (value == 0)
        {
            return "0";
        }
        // R provides the shortest round-tripping digits. Expand only the exponent,
        // avoiding culture-dependent formatting or a fixed precision truncation.
        var roundTrip = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentAt = roundTrip.IndexOf('E');
        if (exponentAt < 0)
        {
            return roundTrip;
        }

        var mantissa = roundTrip.AsSpan(0, exponentAt);
        var negative = mantissa[0] == '-';
        if (negative)
        {
            mantissa = mantissa[1..];
        }

        var dot = mantissa.IndexOf('.');
        var decimalAt = (dot < 0 ? mantissa.Length : dot) + int.Parse(roundTrip.AsSpan(exponentAt + 1), CultureInfo.InvariantCulture);
        Span<char> digits = stackalloc char[17];
        var count = 0;
        foreach (var c in mantissa)
        {
            if (c != '.')
            {
                digits[count++] = c;
            }
        }

        using var result = new ValueStringBuilder(negative ? 328 : 327);
        if (negative)
        {
            result.Append('-');
        }

        if (decimalAt <= 0)
        {
            result.Append("0.");
            result.Append('0', -decimalAt);
            result.Append(digits[..count]);
        }
        else if (decimalAt >= count)
        {
            result.Append(digits[..count]);
            result.Append('0', decimalAt - count);
        }
        else
        {
            result.Append(digits[..decimalAt]);
            result.Append('.');
            result.Append(digits[decimalAt..count]);
        }
        return result.ToString();
    }
}
