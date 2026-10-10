namespace Xemele.Parsing.Dtd;
internal sealed partial class DtdParser
{
    // Tokens are owned bounded declaration scratch. They are released at '>'; the
    // immutable model alone owns accepted names/defaults/content thereafter.
    private int read;
    private Token Peek => read < tokens.Count ? tokens[read] : default;
    private bool Take(string text) { if (read < tokens.Count && !Peek.Literal && Peek.Text == text) { read++; return true; } return false; }
    private bool Require(string text) { if (Take(text)) return true; Reject("Expected DTD token.", read < tokens.Count ? Peek.At : declarationAt); return false; }
    private bool RequiredSpace() { if (read < tokens.Count && Peek.Space) return true; Reject("Required whitespace is missing.", read < tokens.Count ? Peek.At : declarationAt); return false; }
    private string? Name(bool nmtoken = false, bool space = false)
    {
        if (space && !RequiredSpace() || read == tokens.Count || Peek.Literal || !ValidName(Peek.Text, nmtoken))
        { if (!terminal) Reject("Expected XML name.", read < tokens.Count ? Peek.At : declarationAt); return null; }
        Token token = tokens[read++];
        if (!names.Contains(token.Text))
        {
            if (!Accept(budget.ChargeName(token.Text, token.At))) return null;
            names.Add(token.Text);
        }
        return token.Text;
    }
    private static bool ValidName(string? value, bool nmtoken = false)
    {
        if (string.IsNullOrEmpty(value)) return false;
        bool first = true;
        foreach (var rune in value.EnumerateRunes())
        { if (first && !nmtoken ? !ValueSyntax.IsNameStart(rune.Value) : !ValueSyntax.IsNameChar(rune.Value)) return false; first = false; }
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
        read = 0;
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
        if (!elements.Exists(x => x.Name == name)) elements.Add(new(name, content, IsExternalDeclaration, declarationAt));
    }
    private bool IsExternalDeclaration => sources.Find(s => s.Id == declarationSource)?.External ?? Current.External;
    private DtdOccurrence Occurrence()
    { if (Take("?")) return DtdOccurrence.Optional; if (Take("*")) return DtdOccurrence.ZeroOrMore; if (Take("+")) return DtdOccurrence.OneOrMore; return DtdOccurrence.Once; }
    private sealed class Group(int source)
    { internal readonly List<DtdParticle> Children = []; internal string? Separator; internal readonly int Source = source; }
    private DtdParticle? Content(CancellationToken cancellation)
    {
        if (!Require("(")) return null;
        if (Take("#"))
        {
            if (!Require("PCDATA")) return null;
            List<DtdParticle> mixed = [];
            while (Take("|")) { string? name = Name(); if (name is null) return null; mixed.Add(new(DtdParticleKind.Name, name, DtdOccurrence.Once, [])); }
            if (!Require(")")) return null;
            DtdOccurrence occurrence = Occurrence();
            if (mixed.Count > 0 ? occurrence != DtdOccurrence.ZeroOrMore : occurrence is not (DtdOccurrence.Once or DtdOccurrence.ZeroOrMore))
            { Reject("Invalid mixed content occurrence.", declarationAt); return null; }
            return new(DtdParticleKind.Mixed, null, occurrence, mixed);
        }
        var stack = new Stack<Group>(); stack.Push(new(tokens[read - 1].Source));
        bool child = true;
        while (!terminal && read < tokens.Count)
        {
            cancellation.ThrowIfCancellationRequested();
            Group group = stack.Peek();
            if (child)
            {
                if (Take("(")) { stack.Push(new(tokens[read - 1].Source)); child = true; continue; }
                string? name = Name(); if (name is null) return null;
                group.Children.Add(new(DtdParticleKind.Name, name, Occurrence(), [])); child = false; continue;
            }
            if (Take(")"))
            {
                if (group.Children.Count == 0 || tokens[read - 1].Source != group.Source) break;
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
                do { string? token = Name(nmtoken: !notation); if (token is null) return; values.Add(token); } while (Take("|"));
                if (!Require(")")) return;
            }
            if (!RequiredSpace()) return;
            DtdDefaultKind kind; string? value = null;
            if (Take("#"))
            {
                if (Take("IMPLIED")) kind = DtdDefaultKind.Implied;
                else if (Take("REQUIRED")) kind = DtdDefaultKind.Required;
                else if (Take("FIXED")) { kind = DtdDefaultKind.Fixed; value = Literal(space: true); if (value is null) return; }
                else { Reject("Invalid default declaration.", Peek.At); return; }
            }
            else { kind = DtdDefaultKind.Value; value = Literal(); if (value is null) return; }
            if (value is not null && DecodeLiteral(value, cancellation, false) is null) return;
            if (!attributes.Exists(a => a.ElementName == element && a.Name == name)) attributes.Add(new(element, name, type, values, kind, value, external, at));
        }
    }
    private void ParseEntity(CancellationToken cancellation)
    {
        bool isParameter = Take("%");
        string? name = Name(space: true); if (name is null || !RequiredSpace()) return;
        string? replacement = null, system = null, pub = null, notation = null;
        if (Peek.Literal)
        { string? raw = Literal(); if (raw is null) return; replacement = DecodeLiteral(raw, cancellation, true); if (replacement is null) return; }
        else
        {
            if (!ExternalId(false, out system, out pub)) return;
            if (read < tokens.Count)
            {
                if (isParameter || !RequiredSpace() || !Require("NDATA")) return;
                notation = Name(space: true); if (notation is null) return;
            }
        }
        if (!isParameter && !Predefined(name, replacement)) { Reject("Invalid predefined entity redeclaration.", declarationAt); return; }
        if (read != tokens.Count) return;
        var target = isParameter ? parameter : general;
        if (!target.Exists(e => e.Name == name)) target.Add(new(name, isParameter, replacement, system, pub, notation, IsExternalDeclaration, declarationAt));
    }
    private string? DecodeLiteral(string raw, CancellationToken cancellation, bool entity)
    {
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
    private static bool Predefined(string name, string? replacement) => name switch
    {
        "lt" => replacement is "&#60;" or "&#x3c;" or "&#x3C;",
        "amp" => replacement is "&#38;" or "&#x26;",
        "gt" => replacement is ">" or "&#62;" or "&#x3e;" or "&#x3E;",
        "apos" => replacement is "'" or "&#39;" or "&#x27;",
        "quot" => replacement is "\"" or "&#34;" or "&#x22;",
        _ => true
    };
    private bool ExternalId(bool notation, out string? system, out string? pub)
    {
        system = pub = null;
        if (Take("SYSTEM")) { system = Literal(space: true); return system is not null; }
        if (!Require("PUBLIC")) return false;
        pub = Literal(space: true); if (pub is null) return false;
        foreach (char c in pub)
            if (!(c is '\r' or '\n' or ' ' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' || "-'()+,./:=?;!*#@$_%".Contains(c)))
            { Reject("Invalid public identifier character.", declarationAt); return false; }
        if (read < tokens.Count && Peek.Literal) system = Literal(space: true);
        else if (!notation) { Reject("PUBLIC entity identifier requires a system literal.", declarationAt); return false; }
        return !terminal;
    }
    private void ParseNotation()
    {
        string? name = Name(space: true); if (name is null || !RequiredSpace()) return;
        if (!ExternalId(true, out string? system, out string? pub)) return;
        if (read == tokens.Count && !notations.Exists(n => n.Name == name)) notations.Add(new(name, system, pub, declarationAt));
    }
}
