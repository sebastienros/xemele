using System.Text;
namespace Xemele.Parsing.Dtd;
internal sealed partial class DtdParser
{
    private long referenceScalars;
    private static bool Space(int c) => c is ' ' or '\t' or '\n' or '\r';
    private void AddScalar(StringBuilder buffer, int c, XmlLocation at)
    {
        if (!Bounded(tokenScalars, at) || buffer.Length > int.MaxValue - 2) { if (!terminal) Refuse(ValueBudget.Limit(at)); return; }
        tokenScalars++;
        Span<char> pair = stackalloc char[2]; int count = ValueSyntax.WriteScalar(c, pair); buffer.Append(pair[..count]);
        if (lex == Lex.Literal && !IgnoreCurrentDeclaration)
            for (int i = 0; i < count; i++) { literalLocations.Add(physicalDtdLocation); literalOrigins.Add(Current.External || Current.Entered); }
    }
    private void BeginToken(XmlLocation at)
    { scratch.Clear(); tokenScalars = 0; tokenAt = at; tokenPhysicalAt = physicalDtdLocation; tokenSpace = separator; separator = false; }
    private void Emit(bool literal = false)
    {
        string text = scratch.ToString();
        var info = literal && !IgnoreCurrentDeclaration ? new DtdLiteralInfo(physicalLiteral.ToString(), text,
            literalLocations.Count == 0 ? literalValueAt : literalLocations[0], literalLocations.ToArray(), literalOrigins.ToArray()) : null;
        tokens.Add(new(text, literal, tokenSpace, tokenAt, Current.Id, info, tokenPhysicalAt)); scratch.Clear(); tokenScalars = 0;
        physicalLiteral.Clear(); physicalLiteralScalars = 0; literalLocations.Clear(); literalOrigins.Clear();
    }
    private void Symbol(int c, XmlLocation at)
    { tokens.Add(new(new string((char)c, 1), false, separator, at, Current.Id)); separator = false; }
    private void Process(int c, XmlLocation at, CancellationToken cancellation, bool replay = false)
    {
        cancellation.ThrowIfCancellationRequested();
        if (terminal) return;
        if (!replay && declaration.Length != 0)
        { if (!Bounded(declarationScalars, at)) return; declarationScalars++; }
        switch (lex)
        {
            case Lex.Between:
                if (Space(c)) { separator = true; return; }
                if (c == '%') { BeginParameter(Lex.Between, at); return; }
                if (c == '<') { lexicalEntered = Current.Entered; lexicalSource = Current.Id; lexicalAt = at; lex = Lex.Open; return; }
                if (c == ']' && includes.Count != 0) { lexicalSource = Current.Id; lexicalAt = at; ignoreRun = 1; lex = Lex.ConditionalEnd; return; }
                break;
            case Lex.Open:
                if (c == '!') { lex = Lex.Bang; return; }
                if (c == '?') { BeginToken(at); lex = Lex.PiTarget; return; }
                break;
            case Lex.Bang:
                if (c == '-') { lex = Lex.CommentStart; return; }
                if (c == '[' && Current.External) { BeginToken(at); lex = Lex.Conditional; return; }
                if (c is >= 'A' and <= 'Z') { BeginToken(at); AddScalar(scratch, c, at); lex = Lex.Keyword; return; }
                break;
            case Lex.Keyword:
                // Inclusion supplies the required S before/after a PE. A PE may
                // follow the complete markup keyword without literal whitespace.
                if (c == '%' && Current.External)
                { BeginParameter(Lex.Keyword, at); return; }
                if (c is >= 'A' and <= 'Z') { AddScalar(scratch, c, at); return; }
                if (!Space(c)) break;
                declaration = scratch.ToString(); scratch.Clear();
                if (declaration is not ("ELEMENT" or "ATTLIST" or "ENTITY" or "NOTATION")) break;
                declarationAt = lexicalAt; declarationExternal = Current.External; declarationInParameter = lexicalEntered; declarationScalars = declaration.Length + 3;
                if (!Bounded(declarationScalars - 1, at)) return;
                separator = true; tokens.Clear(); lex = Lex.Declaration; return;
            case Lex.Declaration:
                if (Space(c)) { separator = true; return; }
                if (c == '>')
                {
                    ParseDeclaration(cancellation); if (terminal) return;
                    tokens.Clear(); declaration = ""; declarationScalars = 0; lex = Lex.Between; separator = false; return;
                }
                if (c == '%')
                {
                    if (declaration == "ENTITY" && tokens.Count == 0) { Symbol(c, at); return; }
                    if (!Current.External) { Reject("Parameter reference inside an internal-subset declaration.", at); return; }
                    BeginParameter(Lex.Declaration, at); return;
                }
                if (c is '\'' or '"')
                {
                    quote = (char)c; quoteSource = Current.Id; BeginToken(at); lex = Lex.Literal;
                    physicalLiteral.Clear(); physicalLiteralScalars = 0; literalLocations.Clear(); literalOrigins.Clear();
                    literalValueAt = new(at.SourceUri, at.LineNumber, at.LinePosition == 0 ? 0 : at.LinePosition + 1, null);
                    literalEntity = declaration == "ENTITY" && (tokens.Count == 1 || tokens.Count == 2 && tokens[0].Text == "%"); return;
                }
                if (ValueSyntax.IsNameChar(c)) { BeginToken(at); AddScalar(scratch, c, at); lex = Lex.Name; return; }
                if (c is '(' or ')' or '|' or ',' or '?' or '*' or '+' or '#') { Symbol(c, at); return; }
                break;
            case Lex.Name:
                if (ValueSyntax.IsNameChar(c)) { AddScalar(scratch, c, at); return; }
                Emit(); lex = Lex.Declaration; Process(c, at, cancellation, true); return;
            case Lex.Literal:
                if (c == quote && Current.Id == quoteSource) { Emit(true); lex = Lex.Declaration; return; }
                if (c == '%' && literalEntity)
                {
                    if (!Current.External) { Reject("Parameter reference inside an internal entity declaration.", at); return; }
                    BeginParameter(Lex.Literal, at); return;
                }
                if (c == '<' && declaration == "ATTLIST") { Reject("Literal less-than in an attribute default.", at); return; }
                AddScalar(scratch, c, at); return;
            case Lex.Parameter:
                if (c == ';')
                {
                    if (reference.Length == 0) break;
                    ExpandParameter(reference.ToString(), at, cancellation); return;
                }
                if (reference.Length == 0 ? !ValueSyntax.IsNameStart(c) : !ValueSyntax.IsNameChar(c)) break;
                // Reference scratch has a separate scalar limit from the enclosing literal.
                if (reference.Length >= int.MaxValue - 2 || limits.TokenCharacters is int max && referenceScalars >= max) { Refuse(ValueBudget.Limit(at)); return; }
                Span<char> pair = stackalloc char[2]; int units = ValueSyntax.WriteScalar(c, pair); reference.Append(pair[..units]); referenceScalars++; return;
            case Lex.CommentStart:
                if (c == '-') { lex = Lex.Comment; tokenScalars = 0; return; } break;
            case Lex.Comment:
                if (!Bounded(tokenScalars, at)) return; tokenScalars++;
                if (c == '-') lex = Lex.CommentDash; return;
            case Lex.CommentDash:
                if (!Bounded(tokenScalars, at)) return; tokenScalars++;
                lex = c == '-' ? Lex.CommentEnd : Lex.Comment; return;
            case Lex.CommentEnd:
                if (c == '>' && Current.Id == lexicalSource) { lex = Lex.Between; return; } break;
            case Lex.PiTarget:
                if (c != ':' && (scratch.Length == 0 ? ValueSyntax.IsNameStart(c) : ValueSyntax.IsNameChar(c))) { AddScalar(scratch, c, at); return; }
                if (scratch.Length == 0 || scratch.ToString().Equals("xml", StringComparison.OrdinalIgnoreCase)) break;
                scratch.Clear();
                if (Space(c)) { piData = true; lex = Lex.Pi; return; }
                if (c == '?') { piData = false; lex = Lex.PiQuestion; return; } break;
            case Lex.Pi:
                if (!Bounded(tokenScalars, at)) return; tokenScalars++;
                if (c == '?') lex = Lex.PiQuestion; return;
            case Lex.PiQuestion:
                if (c == '>' && Current.Id == lexicalSource) { lex = Lex.Between; return; }
                if (!piData) break;
                if (!Bounded(tokenScalars, at)) return; tokenScalars++;
                lex = c == '?' ? Lex.PiQuestion : Lex.Pi; return;
            case Lex.Conditional:
                if (c == '%') { BeginParameter(Lex.Conditional, at); return; }
                if (Space(c)) { if (scratch.Length != 0) separator = true; return; }
                if (c == '[')
                {
                    if (scratch.ToString() == "INCLUDE") { includes.Push(Current.Id); scratch.Clear(); lex = Lex.Between; return; }
                    if (scratch.ToString() == "IGNORE") { scratch.Clear(); ignoreDepth = 1; ignoreRun = 0; lex = Lex.Ignore; return; }
                    break;
                }
                if (separator && scratch.Length != 0 || c is not (>= 'A' and <= 'Z')) break;
                AddScalar(scratch, c, at); return;
            case Lex.ConditionalEnd:
                if (ignoreRun == 1 && c == ']') { ignoreRun = 2; return; }
                if (ignoreRun == 2 && c == '>' && includes.Count > Current.IncludeFloor) { includes.Pop(); lex = Lex.Between; return; } break;
            case Lex.Ignore:
                // A three-character recognizer skips arbitrarily long IGNORE bodies, including nested sections.
                if (ignoreRun == 0) { ignoreRun = c == '<' ? 1 : c == ']' ? 3 : 0; return; }
                if (ignoreRun == 1) { ignoreRun = c == '!' ? 2 : c == '<' ? 1 : c == ']' ? 3 : 0; return; }
                if (ignoreRun == 2)
                { if (c == '[') { if (ignoreDepth == int.MaxValue) { Refuse(ValueBudget.Limit(at)); return; } ignoreDepth++; ignoreRun = 0; } else ignoreRun = c == '<' ? 1 : c == ']' ? 3 : 0; return; }
                if (ignoreRun == 3) { ignoreRun = c == ']' ? 4 : c == '<' ? 1 : 0; return; }
                if (c == '>')
                { ignoreRun = 0; if (--ignoreDepth == 0) lex = Lex.Between; }
                else ignoreRun = c == ']' ? 4 : c == '<' ? 1 : 0;
                return;
        }
        Reject("Invalid DTD syntax.", at);
    }
    private void BeginParameter(Lex resume, XmlLocation at)
    { HasParameterReferences = true; parameterResume = resume; parameterAt = at; reference.Clear(); referenceScalars = 0; lex = Lex.Parameter; }
    private void ExpandParameter(string name, XmlLocation at, CancellationToken cancellation)
    {
        if (name.AsSpan().Contains(':')) { Reject("Parameter entity names must be NCNames.", parameterAt); return; }
        if (IgnoreCurrentDeclaration && parameterResume == Lex.Literal)
        { lex = parameterResume; reference.Clear(); return; }
        if (active.Contains(name)) { Reject("Recursive parameter entity.", parameterAt, "DTD.EntityCycle"); return; }
        DtdEntityDeclaration? entity = null;
        foreach (var candidate in parameter) { cancellation.ThrowIfCancellationRequested(); if (candidate.Name == name) { entity = candidate; break; } }
        if (entity is null)
        {
            if (!nonvalidatingReadState || standaloneReadState && !Current.External && !Current.Entered)
            { Reject("Undeclared parameter entity.", parameterAt, "DTD.UndeclaredEntity"); return; }
            FirstUnreadParameter ??= parameterAt;
            // No replacement is invented. An unread declaration separator can
            // simply end here; an unknown insertion within grammar cannot be
            // represented by the current immutable declaration model.
            if (parameterResume != Lex.Between)
            { Reject("Unread parameter entity prevents representing this declaration.", parameterAt, "DTD.UnresolvedDeclaration"); return; }
            lex = parameterResume; reference.Clear(); separator = true; return;
        }
        lex = parameterResume; reference.Clear();
        if (nextId == int.MaxValue) { Refuse(ValueBudget.Limit(parameterAt)); return; }
        if (!Accept(budget.EnterEntity(parameterAt))) return;
        Source? child = null;
        try
        {
            bool literal = parameterResume == Lex.Literal;
            // Internal replacement text is included in the current external source,
            // independently of the source where its PE declaration was retained.
            // Nested internal PEs inherit that same containing source. An actual
            // external PE receives its redirected final URI when the caller pushes it.
            child = new(nextId++, Current.External || entity.SystemId is not null, new(Current.Uri, 1, 1, null), name, entity.ReplacementText, true, literal);
            DtdLiteralInfo.Entities.TryGetValue(entity, out child.LiteralInfo);
            child.Insertion = parameterResume;
            child.IncludeFloor = parameterResume == Lex.Between ? includes.Count : Current.IncludeFloor;
            active.Add(name);
            if (!literal) Process(' ', parameterAt, cancellation);
            if (entity.ReplacementText is null)
            // Resource resolution owns the declaration's system identifier/base;
            // the reference cursor still owns entry budgets and local diagnostics.
            { pending = child; request = new(name, entity.SystemId, entity.PublicId, entity.Location); }
            else sources.Add(child);
        }
        catch
        {
            if (child is null || !sources.Contains(child) && pending != child)
            { active.Remove(name); try { budget.ExitEntity(); } catch { } }
            throw;
        }
    }
}
