using System.Globalization;

namespace Xemele.XPath.Internal;

// Stateless library: all retained values belong to the model or the caller's
// snapshot. Engine work uses the invocation's shared budget, never a private one.
internal sealed class XPathCoreFunctions : IXPathFunctionLibrary
{
    public IReadOnlyList<XPathFunctionSignature> Signatures => XPathCoreSignatures.All;

    public XPathValue Invoke(XPathFunctionSignature function, XPathFocus focus,
        IReadOnlyList<XPathValue> arguments, IXPathNavigator navigator, IXPathBudget budget)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(budget);
        budget.ChargeOperations(0);
        if (focus.Item.IsDefault || focus.Position < 1 || focus.Size < focus.Position)
            throw new ArgumentException("Invalid function focus.", nameof(focus));
        // Registration metadata is already validated by the immutable model; it
        // is inspected here but does not become an engine-owned result string.
        ChargeCharacters(function.Name.NamespaceUri.Length, budget);
        ChargeCharacters(function.Name.LocalName.Length, budget);
        if (!XPathCoreSignatures.ByName.TryGetValue(function.Name, out var core) || core != function)
            throw new ArgumentException("Unknown core signature.", nameof(function));
        if (arguments.Count < core.MinArguments || arguments.Count > core.MaxArguments)
            throw new ArgumentException("Invalid core argument count.", nameof(arguments));
        for (int i = 0; i < arguments.Count; i++)
        {
            budget.ChargeOperations(1);
            var value = arguments[i];
            if (value.Kind == XPathValueKind.String) Scan(value.AsString(), budget);
            if (value.Kind == XPathValueKind.NodeSet) budget.CheckNodeSetSize(value.AsNodeSet().Count);
        }
        XPathValue result;
        switch (core.Name.LocalName)
        {
            case "last": result = XPathValue.FromNumber(focus.Size); break;
            case "position": result = XPathValue.FromNumber(focus.Position); break;
            case "count": result = XPathValue.FromNumber(Nodes(arguments[0], budget).Count); break;
            case "id": result = Id(arguments[0], navigator, budget); break;
            case "local-name": case "namespace-uri": case "name":
                var first = arguments.Count == 0 ? focus.Item : First(arguments[0], budget);
                result = XPathValue.FromString(first.IsDefault ? "" : core.Name.LocalName switch
                { "local-name" => first.LocalName, "namespace-uri" => first.NamespaceUri, _ => first.Name });
                Scan(result.AsString(), budget); break;
            case "string": result = XPathValue.FromString(Text(Input(arguments, focus, budget), budget)); break;
            case "number": result = XPathValue.FromNumber(Number(Input(arguments, focus, budget), budget)); break;
            case "boolean": result = XPathValue.FromBoolean(Boolean(arguments[0], budget)); break;
            case "not": result = XPathValue.FromBoolean(!Boolean(arguments[0], budget)); break;
            case "true": result = XPathValue.FromBoolean(true); break;
            case "false": result = XPathValue.FromBoolean(false); break;
            case "floor": result = XPathValue.FromNumber(Math.Floor(Number(arguments[0], budget))); break;
            case "ceiling": result = XPathValue.FromNumber(Math.Ceiling(Number(arguments[0], budget))); break;
            case "round": result = XPathValue.FromNumber(Round(Number(arguments[0], budget))); break;
            case "sum":
                double sum = 0;
                var nodes = Nodes(arguments[0], budget);
                for (int i = 0; i < nodes.Count; i++)
                {
                    budget.ChargeOperations(1);
                    sum += Parse(nodes[i].StringValue, budget);
                }
                result = XPathValue.FromNumber(sum); break;
            case "lang":
                string requested = Text(arguments[0], budget);
                budget.ChargeOperations(1); // context identity passed to external navigator
                string? language = navigator.FindLanguage(focus.Item, budget);
                budget.ChargeOperations(0);
                bool match = false;
                if (language is not null)
                {
                    Scan(language, budget);
                    match = requested.Length <= language.Length;
                    for (int i = 0; match && i < requested.Length; i++)
                    {
                        budget.ChargeOperations(2);
                        // XML language tags use ASCII; Unicode casing is deliberately absent.
                        match = Fold(requested[i]) == Fold(language[i]);
                    }
                    if (match && requested.Length < language.Length)
                    { budget.ChargeOperations(1); match = language[requested.Length] == '-'; }
                }
                result = XPathValue.FromBoolean(match); break;
            case "concat": result = XPathValue.FromString(Concat(arguments, budget)); break;
            case "string-length":
                string lengthText = Text(Input(arguments, focus, budget), budget);
                int count = 0;
                for (int i = 0; i < lengthText.Length; count++) ReadScalar(lengthText, ref i, budget);
                result = XPathValue.FromNumber(count); break;
            case "normalize-space": result = XPathValue.FromString(Normalize(Text(Input(arguments, focus, budget), budget), budget)); break;
            case "translate": result = XPathValue.FromString(Translate(Text(arguments[0], budget), Text(arguments[1], budget), Text(arguments[2], budget), budget)); break;
            case "substring": result = XPathValue.FromString(Substring(Text(arguments[0], budget), Number(arguments[1], budget), arguments.Count == 3 ? Number(arguments[2], budget) : null, budget)); break;
            default:
                string text = Text(arguments[0], budget), search = Text(arguments[1], budget);
                int at = Find(text, search, core.Name.LocalName == "starts-with", budget);
                result = core.Name.LocalName switch
                {
                    "starts-with" or "contains" => XPathValue.FromBoolean(at >= 0),
                    "substring-before" => XPathValue.FromString(at < 0 ? "" : Copy(text, 0, at, budget)),
                    "substring-after" => XPathValue.FromString(at < 0 ? "" : Copy(text, at + search.Length, text.Length - at - search.Length, budget)),
                    _ => throw new InvalidOperationException("Core signature inventory mismatch.")
                };
                break;
        }
        budget.ChargeOperations(1); // return entry
        if (result.Kind == XPathValueKind.String) budget.CheckStringSize(result.AsString().Length);
        if (result.Kind == XPathValueKind.NodeSet) budget.CheckNodeSetSize(result.AsNodeSet().Count);
        return result;
    }

    private static XPathValue Input(IReadOnlyList<XPathValue> arguments, XPathFocus focus, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        return arguments.Count == 0 ? XPathValue.FromString(focus.Item.StringValue) : arguments[0];
    }
    private static IReadOnlyList<XPathItem> Nodes(XPathValue value, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        if (value.Kind != XPathValueKind.NodeSet) throw TypeError();
        var nodes = value.AsNodeSet(); budget.CheckNodeSetSize(nodes.Count); return nodes;
    }
    private static XPathItem First(XPathValue value, IXPathBudget budget)
    {
        var nodes = Nodes(value, budget);
        if (nodes.Count == 0) return default;
        budget.ChargeOperations(1); return nodes[0];
    }
    private static bool Boolean(XPathValue value, IXPathBudget budget)
    { budget.ChargeOperations(1); return value.AsBoolean(); }
    private static double Number(XPathValue value, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        return value.Kind is XPathValueKind.Number or XPathValueKind.Boolean ? value.AsNumber() : Parse(Text(value, budget), budget);
    }
    private static double Parse(string text, IXPathBudget budget)
    {
        budget.CheckStringSize(text.Length);
        // Validate the complete XPath grammar with bounded polling. Only a fixed
        // significant prefix goes to the runtime parser: binary64 rounding
        // boundaries terminate within 1075 decimal places. A sticky final digit
        // distinguishes an exact boundary from any later nonzero tail.
        int start = 0, end = text.Length;
        while (start < end)
        { budget.ChargeOperations(1); if (!XPathConversions.IsSpace(text[start])) break; start++; }
        while (end > start)
        { budget.ChargeOperations(1); if (!XPathConversions.IsSpace(text[end - 1])) break; end--; }
        bool negative = false;
        if (start < end) { budget.ChargeOperations(1); if (text[start] == '-') { negative = true; start++; } }
        Span<char> canonical = stackalloc char[1130];
        int p = 0; if (negative) canonical[p++] = '-';
        int digitCount = 0, beforeDot = 0, firstNonzero = -1, kept = 0;
        bool dot = false, sticky = false;
        for (int i = start; i < end; i++)
        {
            budget.ChargeOperations(1); char c = text[i];
            if (c == '.' && !dot) { dot = true; continue; }
            if (c is < '0' or > '9') return double.NaN;
            if (!dot) beforeDot++;
            if (firstNonzero < 0 && c != '0') firstNonzero = digitCount;
            digitCount++;
            if (firstNonzero < 0) continue;
            if (kept < 1100) { budget.ChargeOperations(1); canonical[p++] = c; kept++; }
            else sticky |= c != '0';
        }
        if (digitCount == 0) return double.NaN;
        if (firstNonzero < 0) return negative ? -0.0 : 0;
        if (sticky) { budget.ChargeOperations(1); canonical[p++] = '1'; kept++; }
        budget.ChargeOperations(1); canonical[p++] = 'E';
        int exponent = beforeDot - firstNonzero - kept;
        if (!exponent.TryFormat(canonical[p..], out int exponentLength, provider: CultureInfo.InvariantCulture)) throw LimitError();
        ChargeCharacters(exponentLength, budget); p += exponentLength;
        // This parser call is bounded independently of caller-controlled length.
        ChargeCharacters(p, budget);
        bool success = double.TryParse(canonical[..p], NumberStyles.AllowLeadingSign | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double result);
        budget.ChargeOperations(0);
        return success ? result : double.NaN;
    }
    private static string Text(XPathValue value, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        if (value.Kind == XPathValueKind.Number) return Format(value.AsNumber(), budget);
        if (value.Kind == XPathValueKind.NodeSet)
        {
            var first = First(value, budget);
            string text = first.IsDefault ? "" : first.StringValue;
            Scan(text, budget); return text;
        }
        string result = value.AsString(); Scan(result, budget); return result;
    }
    internal static double Round(double value)
    {
        if (value == 0 || !double.IsFinite(value)) return value;
        if (value >= -0.5 && value < 0) return -0.0;
        // Adding .5 before flooring loses a bit just below .5 and at large integers.
        double floor = Math.Floor(value);
        return value - floor >= 0.5 ? floor + 1 : floor;
    }
    private static char Fold(char value) => value is >= 'A' and <= 'Z' ? (char)(value + ('a' - 'A')) : value;
    private static XPathEvaluationException TypeError() => new(new("XPATH-TYPE", XPathDiagnosticCategory.Evaluation, "Invalid core function value.", default));
    private static XPathEvaluationException LimitError() => new(new("XPATH-EVALUATION-LIMIT", XPathDiagnosticCategory.EvaluationLimit, "XPath string capacity exceeded.", default));
    private static int Size(long size, IXPathBudget budget)
    {
        if (size > int.MaxValue) throw LimitError();
        budget.CheckStringSize((int)size); return (int)size;
    }
    private static void ChargeCharacters(int count, IXPathBudget budget)
    { for (int i = 0; i < count; i++) budget.ChargeOperations(1); }
    private static void Scan(string text, IXPathBudget budget)
    {
        budget.CheckStringSize(text.Length);
        for (int i = 0; i < text.Length;) ReadScalar(text, ref i, budget);
    }
    private static int ReadScalar(string text, ref int at, IXPathBudget budget)
    {
        budget.ChargeOperations(1); char first = text[at++];
        if (!char.IsSurrogate(first)) return first;
        if (!char.IsHighSurrogate(first) || at == text.Length) throw TypeError();
        budget.ChargeOperations(1); char second = text[at++];
        if (!char.IsLowSurrogate(second)) throw TypeError();
        return char.ConvertToUtf32(first, second);
    }
    private static string Copy(string text, int start, int length, IXPathBudget budget)
    {
        budget.CheckStringSize(length);
        return string.Create(length, (text, start, budget), static (target, state) =>
        {
            for (int i = 0; i < target.Length; i++)
            { state.budget.ChargeOperations(1); target[i] = state.text[state.start + i]; }
        });
    }
    private static string Copy(ReadOnlySpan<char> value, IXPathBudget budget)
    {
        budget.CheckStringSize(value.Length);
        // This overload only consumes fixed-size formatting scratch. The runtime
        // copy is bounded to <=256 units; longer binary64 decimal expansions use
        // an explicitly owned, polled intermediate and polled final fill.
        if (value.Length <= 256)
        {
            ChargeCharacters(value.Length, budget); budget.ChargeOperations(0);
            string result = value.ToString(); budget.ChargeOperations(0); return result;
        }
        var owned = new char[value.Length];
        for (int i = 0; i < value.Length; i++) { budget.ChargeOperations(1); owned[i] = value[i]; }
        return string.Create(owned.Length, (owned, budget), static (target, state) =>
        {
            for (int i = 0; i < target.Length; i++)
            { state.budget.ChargeOperations(1); target[i] = state.owned[i]; }
        });
    }
    private static string Format(double value, IXPathBudget budget)
    {
        // Fixed stack bounds cover all binary64 round-trip decimal forms, including
        // the 326-unit expansion of the smallest subnormal. No temporary string.
        Span<char> roundTrip = stackalloc char[32];
        if (!value.TryFormat(roundTrip, out int count, "R", CultureInfo.InvariantCulture)) throw LimitError();
        ReadOnlySpan<char> input = roundTrip[..count]; ChargeCharacters(count, budget);
        if (value == 0) return Copy("0", budget);
        int exponentAt = input.IndexOf('E');
        if (exponentAt < 0) return Copy(input, budget);
        int exponent = int.Parse(input[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        bool negative = input[0] == '-'; int start = negative ? 1 : 0;
        Span<char> digits = stackalloc char[17]; int digitsCount = 0, decimalAt = 0;
        bool dot = false;
        for (int i = start; i < exponentAt; i++)
        {
            budget.ChargeOperations(1);
            if (input[i] == '.') { dot = true; continue; }
            digits[digitsCount++] = input[i]; if (!dot) decimalAt++;
        }
        decimalAt += exponent;
        int length = Size((negative ? 1L : 0) + (decimalAt <= 0 ? 2L - decimalAt + digitsCount : Math.Max(decimalAt, digitsCount) + (decimalAt < digitsCount ? 1L : 0)), budget);
        Span<char> output = stackalloc char[350]; int p = 0;
        if (negative) output[p++] = '-';
        if (decimalAt <= 0)
        {
            output[p++] = '0'; output[p++] = '.';
            for (int i = 0; i < -decimalAt; i++) { budget.ChargeOperations(1); output[p++] = '0'; }
            ChargeCharacters(digitsCount, budget); digits[..digitsCount].CopyTo(output[p..]); p += digitsCount;
        }
        else
        {
            for (int i = 0; i < Math.Max(decimalAt, digitsCount); i++)
            {
                budget.ChargeOperations(1);
                if (i == decimalAt) output[p++] = '.';
                output[p++] = i < digitsCount ? digits[i] : '0';
            }
        }
        if (p != length) throw new InvalidOperationException("Number format length mismatch.");
        return Copy(output[..p], budget);
    }
    private static string Concat(IReadOnlyList<XPathValue> arguments, IXPathBudget budget)
    {
        // Each conversion is an owned, capped intermediate. Preflight the array's
        // entry work and final length before allocating the final owned string.
        budget.ChargeOperations(arguments.Count);
        var parts = new string[arguments.Count]; long length = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            budget.ChargeOperations(1); parts[i] = Text(arguments[i], budget);
            length += parts[i].Length; Size(length, budget);
        }
        return string.Create(Size(length, budget), (parts, budget), static (target, state) =>
        {
            int at = 0;
            foreach (string part in state.parts)
            {
                state.budget.ChargeOperations(1);
                for (int i = 0; i < part.Length; i++) { state.budget.ChargeOperations(1); target[at++] = part[i]; }
            }
        });
    }
    private static int Find(string text, string search, bool prefixOnly, IXPathBudget budget)
    {
        if (search.Length == 0) return 0;
        int last = prefixOnly ? Math.Min(0, text.Length - search.Length) : text.Length - search.Length;
        for (int at = 0; at <= last; at++)
        {
            bool same = true;
            for (int j = 0; j < search.Length; j++)
            { budget.ChargeOperations(2); if (text[at + j] != search[j]) { same = false; break; } }
            if (same) return at;
        }
        return -1;
    }
    private static string Substring(string text, double start, double? length, IXPathBudget budget)
    {
        double first = Round(start), end = length.HasValue ? first + Round(length.Value) : double.PositiveInfinity;
        int begin = -1, finish = 0, position = 1;
        for (int at = 0; at < text.Length; position++)
        {
            int before = at; ReadScalar(text, ref at, budget);
            if (position >= first && position < end) { if (begin < 0) begin = before; finish = at; }
        }
        return begin < 0 ? "" : Copy(text, begin, finish - begin, budget);
    }
    private static string Normalize(string text, IXPathBudget budget)
    {
        int length = 0; bool space = false;
        for (int i = 0; i < text.Length; i++)
        {
            budget.ChargeOperations(1);
            if (XPathConversions.IsSpace(text[i])) { space = length > 0; continue; }
            if (space) length++; length++; space = false;
        }
        Size(length, budget);
        return string.Create(length, (text, budget), static (target, state) =>
        {
            int at = 0; bool space = false;
            foreach (char c in state.text)
            {
                state.budget.ChargeOperations(1);
                if (XPathConversions.IsSpace(c)) { space = at > 0; continue; }
                if (space) { state.budget.ChargeOperations(1); target[at++] = ' '; }
                state.budget.ChargeOperations(1); target[at++] = c; space = false;
            }
        });
    }
    private static int Map(int scalar, string source, string replacement, IXPathBudget budget)
    {
        int index = 0, found = -1;
        for (int at = 0; at < source.Length; index++)
            if (ReadScalar(source, ref at, budget) == scalar) { found = index; break; }
        if (found < 0) return scalar;
        for (int at = 0, i = 0; at < replacement.Length; i++)
        { int next = ReadScalar(replacement, ref at, budget); if (i == found) return next; }
        return -1;
    }
    private static string Translate(string text, string source, string replacement, IXPathBudget budget)
    {
        long length = 0;
        for (int i = 0; i < text.Length;)
        {
            int mapped = Map(ReadScalar(text, ref i, budget), source, replacement, budget);
            if (mapped >= 0) length += mapped > 0xffff ? 2 : 1;
            Size(length, budget);
        }
        return string.Create(Size(length, budget), (text, source, replacement, budget), static (target, state) =>
        {
            int p = 0;
            for (int i = 0; i < state.text.Length;)
            {
                int mapped = Map(ReadScalar(state.text, ref i, state.budget), state.source, state.replacement, state.budget);
                if (mapped < 0) continue;
                state.budget.ChargeOperations(mapped > 0xffff ? 2 : 1);
                if (mapped <= 0xffff) target[p++] = (char)mapped;
                else { mapped -= 0x10000; target[p++] = (char)(0xd800 + (mapped >> 10)); target[p++] = (char)(0xdc00 + (mapped & 1023)); }
            }
        });
    }
    private static XPathValue Id(XPathValue argument, IXPathNavigator navigator, IXPathBudget budget)
    {
        var result = new SnapshotItems();
        if (argument.Kind == XPathValueKind.NodeSet)
        {
            var nodes = Nodes(argument, budget);
            for (int i = 0; i < nodes.Count; i++) { budget.ChargeOperations(1); AddIds(nodes[i].StringValue, result, navigator, budget); }
        }
        else AddIds(Text(argument, budget), result, navigator, budget);
        return XPathValue.FromSnapshotNodeSet(result, budget);
    }
    private static void AddIds(string text, SnapshotItems result, IXPathNavigator navigator, IXPathBudget budget)
    {
        Scan(text, budget);
        for (int i = 0; i < text.Length;)
        {
            budget.ChargeOperations(1);
            if (XPathConversions.IsSpace(text[i])) { i++; continue; }
            int start = i++;
            while (i < text.Length) { budget.ChargeOperations(1); if (XPathConversions.IsSpace(text[i])) break; i++; }
            string token = Copy(text, start, i - start, budget);
            budget.ChargeOperations(1);
            var selected = navigator.SelectId(token, budget);
            budget.ChargeOperations(0); budget.CheckNodeSetSize(selected.Count);
            for (int j = 0; j < selected.Count; j++)
            {
                budget.ChargeOperations(1); var item = selected[j];
                if (item.IsDefault) throw TypeError();
                int insert = result.Count; bool duplicate = false;
                for (int k = 0; k < result.Count; k++)
                {
                    budget.ChargeOperations(2);
                    int order = navigator.CompareDocumentOrder(item, result[k], budget);
                    budget.ChargeOperations(0);
                    if (order == 0) { duplicate = true; break; }
                    if (order < 0) { insert = k; break; }
                }
                if (duplicate) continue;
                result.Insert(insert, item, budget);
            }
        }
    }
    // Exact-size scratch avoids List.Insert's opaque growth/shift copies. Every
    // identity moves through a charged, polled write; no live topology is read.
    private sealed class SnapshotItems : IReadOnlyList<XPathItem>
    {
        private XPathItem[] items = [];
        public int Count => items.Length;
        public XPathItem this[int index] => items[index];
        internal void Insert(int index, XPathItem item, IXPathBudget budget)
        {
            if (items.Length == int.MaxValue) throw LimitError();
            budget.CheckNodeSetSize(items.Length + 1);
            var next = new XPathItem[items.Length + 1];
            for (int i = 0; i < next.Length; i++)
            {
                budget.ChargeOperations(i == index ? 1 : 2); // inspected/copied entry
                next[i] = i == index ? item : items[i < index ? i : i - 1];
            }
            budget.ChargeOperations(0); items = next;
        }
        public IEnumerator<XPathItem> GetEnumerator() => ((IEnumerable<XPathItem>)items).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
