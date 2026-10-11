using System.Globalization;
using System.Text;
namespace Xemele.XPath;

public enum XPathValueKind { NodeSet = 0, Boolean = 1, Number = 2, String = 3 }
public enum XPathItemKind { Root = 0, Element = 1, Text = 2, Comment = 3, ProcessingInstruction = 4, Attribute = 5, Namespace = 6 }
public readonly record struct XPathExpandedName
{
    private readonly string namespaceUri, localName;
    public string NamespaceUri { get => namespaceUri; init { ArgumentNullException.ThrowIfNull(value); namespaceUri = value; } }
    public string LocalName { get => localName; init { TreeValidation.Name(value); localName = value; } }
    public XPathExpandedName(string NamespaceUri, string LocalName) { namespaceUri = null!; localName = null!; this.NamespaceUri = NamespaceUri; this.LocalName = LocalName; }
    private XPathExpandedName(string uri) { namespaceUri = uri; localName = ""; }
    internal static XPathExpandedName NamespaceWildcard(string uri)
    { ArgumentNullException.ThrowIfNull(uri); if (uri.Length == 0) throw new ArgumentException("Empty wildcard URI.", nameof(uri)); return new(uri); }
    public void Deconstruct(out string NamespaceUri, out string LocalName) { NamespaceUri = this.NamespaceUri; LocalName = this.LocalName; }
}
public readonly record struct XPathSourceSpan
{
    private readonly int start, length;
    public int Start { get => start; init { if (value < 0 || value > int.MaxValue - length) throw new ArgumentOutOfRangeException(nameof(value)); start = value; } }
    public int Length { get => length; init { if (value < 0 || value > int.MaxValue - start) throw new ArgumentOutOfRangeException(nameof(value)); length = value; } }
    public XPathSourceSpan(int Start, int Length) { start = length = 0; this.Start = Start; this.Length = Length; }
    public void Deconstruct(out int Start, out int Length) { Start = this.Start; Length = this.Length; }
}
public readonly struct XPathValue
{
    private readonly XPathValueKind kind;
    private readonly double number;
    private readonly string? text;
    private readonly IReadOnlyList<XPathItem>? items;
    private XPathValue(XPathValueKind kind, double number = 0, string? text = null, IReadOnlyList<XPathItem>? items = null)
    { this.kind = kind; this.number = number; this.text = text; this.items = items; }
    public XPathValueKind Kind => kind;
    public static XPathValue FromNumber(double value) => new(XPathValueKind.Number, value);
    public static XPathValue FromBoolean(bool value) => new(XPathValueKind.Boolean, value ? 1 : 0);
    public static XPathValue FromString(string value) { ArgumentNullException.ThrowIfNull(value); return new(XPathValueKind.String, text: value); }
    public static XPathValue FromNodeSet(IEnumerable<XPathItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var unique = new HashSet<XPathItem>(); var owned = new List<XPathItem>(); XmlDocument? document = null;
        // Caller enumeration executes once. Item metadata is already an owned snapshot.
        foreach (var item in items)
        {
            if (item.IsDefault) throw new ArgumentException("Default node-set item.", nameof(items));
            document ??= item.Document;
            if (!ReferenceEquals(document, item.Document)) throw new ArgumentException("Mixed node-set documents.", nameof(items));
            if (unique.Add(item)) owned.Add(item);
        }
        if (owned.Count == 0) return default;
        // Topology is read once at this materialization boundary, never by the retained result.
        var order = ItemOrder.Capture(document!);
        foreach (var item in owned) if (!order.ContainsKey(item.Identity)) throw new ArgumentException("Detached or absent node-set identity.", nameof(items));
        owned.Sort((a, b) => order[a.Identity].CompareTo(order[b.Identity]));
        return new(XPathValueKind.NodeSet, items: Array.AsReadOnly(owned.ToArray()));
    }
    // Input is already normalized against the navigator's entry snapshot. Never
    // query live handles here: an extension may have removed/reordered that tree.
    internal static XPathValue FromSnapshotNodeSet(IReadOnlyList<XPathItem> normalized, Internal.IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(normalized); ArgumentNullException.ThrowIfNull(budget);
        budget.ChargeOperations(0); budget.CheckNodeSetSize(normalized.Count);
        if (normalized.Count == 0) return default;
        budget.ChargeOperations(normalized.Count); // reject before destination growth
        var owned = new XPathItem[normalized.Count];
        for (int i = 0; i < owned.Length; i++)
        {
            budget.ChargeOperations(0);
            var item = normalized[i];
            if (item.IsDefault) throw new ArgumentException("Default snapshot item.", nameof(normalized));
            if (i != 0 && !ReferenceEquals(owned[0].Identity.Node.Document, item.Identity.Node.Document)) throw new ArgumentException("Mixed snapshot documents.", nameof(normalized));
            owned[i] = item;
        }
        budget.ChargeOperations(0);
        return new(XPathValueKind.NodeSet, items: Array.AsReadOnly(owned));
    }
    public IReadOnlyList<XPathItem> AsNodeSet() => kind == XPathValueKind.NodeSet ? items ?? Array.Empty<XPathItem>() : throw new InvalidOperationException("Value is not a node-set.");
    public bool AsBoolean() => kind switch { XPathValueKind.NodeSet => items is { Count: > 0 }, XPathValueKind.Boolean => number != 0, XPathValueKind.Number => number != 0 && !double.IsNaN(number), _ => text!.Length != 0 };
    public double AsNumber() => kind switch { XPathValueKind.Number or XPathValueKind.Boolean => number, _ => XPathConversions.ParseNumber(AsString()) };
    public string AsString() => kind switch { XPathValueKind.String => text!, XPathValueKind.Boolean => number == 0 ? "false" : "true", XPathValueKind.Number => XPathConversions.FormatNumber(number), _ => items is { Count: > 0 } ? items[0].StringValue : "" };
}
internal static class XPathConversions
{
    internal static bool IsSpace(char c) => c is ' ' or '\t' or '\r' or '\n';
    internal static double ParseNumber(ReadOnlySpan<char> input)
    {
        int start = 0, end = input.Length;
        while (start < end && IsSpace(input[start])) start++;
        while (end > start && IsSpace(input[end - 1])) end--;
        var value = input[start..end]; int i = 0, digits = 0;
        if (!value.IsEmpty && value[0] == '-') i++;
        while (i < value.Length && value[i] is >= '0' and <= '9') { i++; digits++; }
        if (i < value.Length && value[i] == '.') { i++; while (i < value.Length && value[i] is >= '0' and <= '9') { i++; digits++; } }
        if (digits == 0 || i != value.Length) return double.NaN;
        return double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double result) ? result : double.NaN;
    }
    internal static string FormatNumber(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";
        if (value == 0) return "0";
        // R provides the shortest round-tripping digits. Expand only the exponent,
        // avoiding culture-dependent formatting or a fixed precision truncation.
        string roundTrip = value.ToString("R", CultureInfo.InvariantCulture);
        int exponentAt = roundTrip.IndexOf('E');
        if (exponentAt < 0) return roundTrip;
        ReadOnlySpan<char> mantissa = roundTrip.AsSpan(0, exponentAt);
        bool negative = mantissa[0] == '-'; if (negative) mantissa = mantissa[1..];
        int dot = mantissa.IndexOf('.'); int decimalAt = (dot < 0 ? mantissa.Length : dot) + int.Parse(roundTrip.AsSpan(exponentAt + 1), CultureInfo.InvariantCulture);
        Span<char> digits = stackalloc char[17]; int count = 0;
        foreach (char c in mantissa) if (c != '.') digits[count++] = c;
        var result = new StringBuilder(negative ? 328 : 327); if (negative) result.Append('-');
        if (decimalAt <= 0) { result.Append("0."); result.Append('0', -decimalAt); result.Append(digits[..count]); }
        else if (decimalAt >= count) { result.Append(digits[..count]); result.Append('0', decimalAt - count); }
        else { result.Append(digits[..decimalAt]); result.Append('.'); result.Append(digits[decimalAt..count]); }
        return result.ToString();
    }
}
