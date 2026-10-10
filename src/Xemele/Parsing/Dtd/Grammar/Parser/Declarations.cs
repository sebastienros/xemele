namespace Xemele.Parsing.Dtd;
internal sealed partial class DtdParser
{
    // Tokens are owned bounded declaration scratch. They are released at '>'; the
    // immutable model alone owns accepted names/defaults/content thereafter.
    private int read;
    private CancellationToken grammarCancellation;
    private Token Peek => read < tokens.Count ? tokens[read] : default;
    private bool Take(string text) { if (read < tokens.Count && !Peek.Literal && Peek.Text == text) { read++; return true; } return false; }
    private bool Require(string text) { if (Take(text)) return true; Reject("Expected DTD token.", read < tokens.Count ? Peek.At : declarationAt); return false; }
    private bool RequiredSpace() { if (read < tokens.Count && Peek.Space) return true; Reject("Required whitespace is missing.", read < tokens.Count ? Peek.At : declarationAt); return false; }
    private string? Name(bool nmtoken = false, bool space = false, bool ncname = false)
    {
        if (space && !RequiredSpace() || read == tokens.Count || Peek.Literal || !ValidName(Peek.Text, nmtoken))
        { if (!terminal) Reject("Expected XML name.", read < tokens.Count ? Peek.At : declarationAt); return null; }
        if (ncname && Peek.Text.AsSpan().Contains(':'))
        { Reject("Entity and notation names must be NCNames.", Peek.At, "XML.Syntax"); return null; }
        Token token = tokens[read++];
        if (!IgnoreCurrentDeclaration && !names.Contains(token.Text))
        {
            if (!Accept(budget.ChargeName(token.Text, token.At))) return null;
            names.Add(token.Text);
        }
        return token.Text;
    }
    private bool ValidName(string? value, bool nmtoken = false)
    {
        if (string.IsNullOrEmpty(value)) return false;
        bool first = true;
        foreach (var rune in value.EnumerateRunes())
        { grammarCancellation.ThrowIfCancellationRequested(); if (first && !nmtoken ? !ValueSyntax.IsNameStart(rune.Value) : !ValueSyntax.IsNameChar(rune.Value)) return false; first = false; }
        return true;
    }
    private string? Literal(bool space = false)
    {
        if (space && !RequiredSpace() || read == tokens.Count || !Peek.Literal)
        { if (!terminal) Reject("Expected quoted literal.", read < tokens.Count ? Peek.At : declarationAt); return null; }
        return tokens[read++].Text;
    }
    private void ParseDeclaration(CancellationToken cancellation)
    {
        read = 0; grammarCancellation = cancellation;
        switch (declaration)
        {
            case "ELEMENT": ParseElement(cancellation); break;
            case "ATTLIST": ParseAttributes(cancellation); break;
            case "ENTITY": ParseEntity(cancellation); break;
            case "NOTATION": ParseNotation(); break;
        }
        if (!terminal && read != tokens.Count) Reject("Unexpected declaration suffix.", Peek.At);
    }
    private void ParseElement(CancellationToken cancellation)
    {
        string? name = Name(space: true); if (name is null || !RequiredSpace()) return;
        DtdParticle? content;
        if (Take("EMPTY")) content = new(DtdParticleKind.Empty, null, DtdOccurrence.Once, []);
        else if (Take("ANY")) content = new(DtdParticleKind.Any, null, DtdOccurrence.Once, []);
        else content = Content(cancellation);
        if (content is null || terminal || read != tokens.Count) return;
        if (!HasElement(name)) elements.Add(new(name, content, IsExternalDeclaration, declarationAt));
    }
    private bool IsExternalDeclaration => declarationExternal;
    private DtdOccurrence Occurrence()
    { if (read < tokens.Count && Peek.Space && Peek.Text is "?" or "*" or "+") { Reject("Occurrence indicator must be adjacent.", Peek.At); return DtdOccurrence.Once; } if (Take("?")) return DtdOccurrence.Optional; if (Take("*")) return DtdOccurrence.ZeroOrMore; if (Take("+")) return DtdOccurrence.OneOrMore; return DtdOccurrence.Once; }
    private sealed class Group
    { internal readonly List<DtdParticle> Children = []; internal string? Separator; }
    private DtdParticle? Content(CancellationToken cancellation)
    {
        if (!Require("(")) return null;
        if (Take("#"))
        {
            if (!Adjacent("PCDATA")) return null;
            List<DtdParticle> mixed = [];
            while (Take("|")) { cancellation.ThrowIfCancellationRequested(); string? name = Name(); if (name is null) return null; mixed.Add(new(DtdParticleKind.Name, name, DtdOccurrence.Once, [])); }
            if (!Require(")")) return null;
            DtdOccurrence occurrence = Occurrence();
            if (mixed.Count > 0 ? occurrence != DtdOccurrence.ZeroOrMore : occurrence is not (DtdOccurrence.Once or DtdOccurrence.ZeroOrMore))
            { Reject("Invalid mixed content occurrence.", declarationAt); return null; }
            return new(DtdParticleKind.Mixed, null, occurrence, mixed);
        }
        var stack = new Stack<Group>(); stack.Push(new());
        bool child = true;
        while (!terminal && read < tokens.Count)
        {
            cancellation.ThrowIfCancellationRequested();
            Group group = stack.Peek();
            if (child)
            {
                if (Take("(")) { stack.Push(new()); child = true; continue; }
                string? name = Name(); if (name is null) return null;
                group.Children.Add(new(DtdParticleKind.Name, name, Occurrence(), [])); child = false; continue;
            }
            if (Take(")"))
            {
                if (group.Children.Count == 0) break;
                var particle = new DtdParticle(group.Separator == "|" ? DtdParticleKind.Choice : DtdParticleKind.Sequence, null, Occurrence(), group.Children);
                stack.Pop(); if (stack.Count == 0) return particle;
                stack.Peek().Children.Add(particle); child = false; continue;
            }
            string? separator = Take("|") ? "|" : Take(",") ? "," : null;
            if (separator is null || group.Separator is not null && separator != group.Separator) break;
            group.Separator = separator; child = true;
        }
        Reject("Invalid children content model.", read < tokens.Count ? Peek.At : declarationAt); return null;
    }
    private void ParseAttributes(CancellationToken cancellation)
    {
        string? element = Name(space: true); if (element is null) return;
        while (!terminal && read < tokens.Count)
        {
            cancellation.ThrowIfCancellationRequested();
            XmlLocation at = Peek.At; bool external = IsExternalDeclaration;
            string? name = Name(space: true); if (name is null || !RequiredSpace()) return;
            DtdAttributeType type; List<string> values = [];
            if (Take("CDATA")) type = DtdAttributeType.CData;
            else if (Take("ID")) type = DtdAttributeType.Id;
            else if (Take("IDREF")) type = DtdAttributeType.IdRef;
            else if (Take("IDREFS")) type = DtdAttributeType.IdRefs;
            else if (Take("ENTITY")) type = DtdAttributeType.Entity;
            else if (Take("ENTITIES")) type = DtdAttributeType.Entities;
            else if (Take("NMTOKEN")) type = DtdAttributeType.NmToken;
            else if (Take("NMTOKENS")) type = DtdAttributeType.NmTokens;
            else
            {
                bool notation = Take("NOTATION"); type = notation ? DtdAttributeType.Notation : DtdAttributeType.Enumeration;
                if (notation && !RequiredSpace() || !Require("(")) return;
                do { string? token = Name(nmtoken: !notation, ncname: notation); if (token is null) return; values.Add(token); } while (Take("|"));
                if (!Require(")")) return;
            }
            if (!RequiredSpace()) return;
            DtdDefaultKind kind; string? value = null;
            if (Take("#"))
            {
                if (AdjacentTake("IMPLIED")) kind = DtdDefaultKind.Implied;
                else if (AdjacentTake("REQUIRED")) kind = DtdDefaultKind.Required;
                else if (AdjacentTake("FIXED")) { kind = DtdDefaultKind.Fixed; value = Literal(space: true); if (value is null) return; }
                else { Reject("Invalid default declaration.", Peek.At); return; }
            }
            else { kind = DtdDefaultKind.Value; value = Literal(); if (value is null) return; }
            if (value is not null && (DecodeLiteral(value, cancellation, false) is null ||
                !IgnoreCurrentDeclaration && !ValidateDeclaredDefault(value, at, cancellation))) return;
            if (!IgnoreCurrentDeclaration && !HasAttribute(element, name)) attributes.Add(new(element, name, type, values, kind, value, external, at));
        }
    }
    private void ParseEntity(CancellationToken cancellation)
    {
        bool isParameter = Take("%");
        string? name = Name(space: true, ncname: true); if (name is null || !RequiredSpace()) return;
        string? replacement = null, system = null, pub = null, notation = null;
        if (Peek.Literal)
        { string? raw = Literal(); if (raw is null) return; replacement = DecodeLiteral(raw, cancellation, true); if (replacement is null) return; }
        else
        {
            if (!ExternalId(false, out system, out pub)) return;
            if (read < tokens.Count)
            {
                if (isParameter || !RequiredSpace() || !Require("NDATA")) return;
                notation = Name(space: true, ncname: true); if (notation is null) return;
            }
        }
        if (!isParameter && !(IgnoreCurrentDeclaration ? UnprocessedPredefined(name, replacement) : Predefined(name, replacement))) { Reject("Invalid predefined entity redeclaration.", declarationAt); return; }
        if (read != tokens.Count) return;
        var target = isParameter ? parameter : general;
        if (!IgnoreCurrentDeclaration && !HasEntity(target, name))
        {
            target.Add(new(name, isParameter, replacement, system, pub, notation, IsExternalDeclaration, declarationAt));
            if (!isParameter && declarationInParameter) (parameterOriginGeneral ??= new(StringComparer.Ordinal)).Add(name);
        }
    }
    private string? DecodeLiteral(string raw, CancellationToken cancellation, bool entity)
    {
        if (IgnoreCurrentDeclaration)
            return ValidateUnprocessedLiteral(raw, cancellation) ? raw : null;
        // Default declarations stay raw. Numeric references are validated here;
        // their expansion accounting and normalization belong to application.
        using var decoder = new DtdValueDecoder(entity ? limits : limits with { ExpandedCharacters = null, EntityDepth = null }, declarationAt, entity ? budget : null);
        Span<char> output = stackalloc char[64]; var result = entity ? new System.Text.StringBuilder() : null;
        int offset = 0;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var r = decoder.Decode(raw.AsSpan(offset), output, true, cancellation); offset += r.Consumed;
            result?.Append(output[..r.Written]);
            if (r.Step == DtdValueStep.Rejected) { Refuse(r.Diagnostic!); return null; }
            if (r.Step == DtdValueStep.Completed) return entity ? result!.ToString() : raw;
        }
    }
    // Ignored declarations still obey reference syntax/scalar WFCs. This is
    // borrowed lexical validation: no replacement owner, decoder, name retention
    // or expansion-budget work is created for a declaration not processed.
    private bool ValidateUnprocessedLiteral(string raw, CancellationToken cancellation)
    {
        for (int i = 0; i < raw.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (raw[i] != '&') continue;
            int start = ++i;
            while (i < raw.Length && raw[i] != ';') { cancellation.ThrowIfCancellationRequested(); i++; }
            if (i == raw.Length || i == start)
            { Reject("Invalid reference in ignored declaration.", declarationAt); return false; }
            var name = raw.AsSpan(start, i - start);
            if (name[0] == '#')
            {
                if (!UnprocessedNumeric(name, out _))
                { Reject("Invalid numeric reference in ignored declaration.", declarationAt); return false; }
            }
            else
            {
                bool first = true;
                foreach (var rune in name.EnumerateRunes())
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (first ? !ValueSyntax.IsNameStart(rune.Value) : !ValueSyntax.IsNameChar(rune.Value))
                    { Reject("Invalid entity reference in ignored declaration.", declarationAt); return false; }
                    first = false;
                }
            }
        }
        return true;
    }
    private bool UnprocessedNumeric(ReadOnlySpan<char> name, out int scalar)
    {
        scalar = 0; int i = 1, radix = 10;
        if (i < name.Length && name[i] == 'x') { radix = 16; i++; }
        int start = i;
        for (; i < name.Length; i++)
        {
            grammarCancellation.ThrowIfCancellationRequested();
            int digit = ValueSyntax.Digit(name[i], radix);
            if (digit < 0 || scalar > (0x10FFFF - digit) / radix) return false;
            scalar = scalar * radix + digit;
        }
        return i > start && ValueSyntax.IsXmlScalar(scalar);
    }
    private bool UnprocessedPredefined(string name, string? raw)
    {
        int expected = name switch { "lt" => 60, "amp" => 38, "gt" => 62, "apos" => 39, "quot" => 34, _ => -1 };
        if (expected < 0) return true;
        if (raw is null) return false;
        int index = 0;
        int Next()
        {
            grammarCancellation.ThrowIfCancellationRequested();
            if (index == raw.Length) return -1;
            char unit = raw[index++];
            if (unit != '&' || index == raw.Length || raw[index] != '#') return unit;
            int start = index;
            while (index < raw.Length && raw[index] != ';') { grammarCancellation.ThrowIfCancellationRequested(); index++; }
            if (index == raw.Length || !UnprocessedNumeric(raw.AsSpan(start, index - start), out int scalar)) return -1;
            index++; return scalar;
        }
        // Validate the predefined declaration constraint by inspecting scalar
        // syntax, without producing or retaining expanded replacement text.
        int first = Next();
        if (name is not ("lt" or "amp") && first == expected && Next() == -1) return true;
        if (first != '&' || Next() != '#') return false;
        int c = Next(), radix = 10, value = 0, digits = 0;
        if (c == 'x') { radix = 16; c = Next(); }
        while (c != ';' && c >= 0)
        {
            int digit = ValueSyntax.Digit(c, radix);
            if (digit < 0 || value > (0x10FFFF - digit) / radix) return false;
            value = value * radix + digit; digits++; c = Next();
        }
        return c == ';' && digits != 0 && value == expected && Next() == -1;
    }

    private bool Predefined(string name, string? replacement)
    {
        int expected = name switch { "lt" => 60, "amp" => 38, "gt" => 62, "apos" => 39, "quot" => 34, _ => -1 };
        if (expected < 0) return true;
        if (replacement is null) return false;
        if (name is not ("lt" or "amp") && replacement.Length == 1 && replacement[0] == expected) return true;
        ReadOnlySpan<char> text = replacement;
        if (text.Length < 4 || !text.StartsWith("&#") || text[^1] != ';') return false;
        int i = 2, radix = 10, scalar = 0;
        if (text[i] == 'x') { radix = 16; i++; }
        int start = i;
        for (; i < text.Length - 1; i++)
        { grammarCancellation.ThrowIfCancellationRequested(); int d = ValueSyntax.Digit(text[i], radix); if (d < 0 || scalar > (0x10FFFF - d) / radix) return false; scalar = scalar * radix + d; }
        return i > start && scalar == expected;
    }
    private bool ExternalId(bool notation, out string? system, out string? pub)
    {
        system = pub = null;
        if (Take("SYSTEM")) { system = Literal(space: true); return system is not null; }
        if (!Require("PUBLIC")) return false;
        pub = Literal(space: true); if (pub is null) return false;
        foreach (char c in pub)
        { grammarCancellation.ThrowIfCancellationRequested();
            if (!(c is '\r' or '\n' or ' ' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' || "-'()+,./:=?;!*#@$_%".Contains(c)))
            { Reject("Invalid public identifier character.", declarationAt); return false; }
        }
        if (read < tokens.Count && Peek.Literal) system = Literal(space: true);
        else if (!notation) { Reject("PUBLIC entity identifier requires a system literal.", declarationAt); return false; }
        return !terminal;
    }
    private bool AdjacentTake(string text) => read < tokens.Count && !Peek.Space && Take(text);
    private bool Adjacent(string text) { if (AdjacentTake(text)) return true; Reject("Keyword must be adjacent to its marker.", read < tokens.Count ? Peek.At : declarationAt); return false; }
    private bool HasElement(string name) { foreach (var e in elements) { grammarCancellation.ThrowIfCancellationRequested(); if (e.Name == name) return true; } return false; }
    private bool HasAttribute(string element, string name) { foreach (var a in attributes) { grammarCancellation.ThrowIfCancellationRequested(); if (a.ElementName == element && a.Name == name) return true; } return false; }
    private bool HasEntity(List<DtdEntityDeclaration> table, string name) { foreach (var e in table) { grammarCancellation.ThrowIfCancellationRequested(); if (e.Name == name) return true; } return false; }
    private bool HasNotation(string name) { foreach (var n in notations) { grammarCancellation.ThrowIfCancellationRequested(); if (n.Name == name) return true; } return false; }
    private void ParseNotation()
    {
        string? name = Name(space: true, ncname: true); if (name is null || !RequiredSpace()) return;
        if (!ExternalId(true, out string? system, out string? pub)) return;
        if (read == tokens.Count && !HasNotation(name)) notations.Add(new(name, system, pub, declarationAt));
    }
}
