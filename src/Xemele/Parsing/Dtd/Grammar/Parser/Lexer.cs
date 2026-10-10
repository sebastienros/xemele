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
    }
    private void BeginToken(XmlLocation at)
    { scratch.Clear(); tokenScalars = 0; tokenAt = at; tokenSpace = separator; separator = false; }
    private void Emit(bool literal = false)
    { tokens.Add(new(scratch.ToString(), literal, tokenSpace, tokenAt, Current.Id)); scratch.Clear(); tokenScalars = 0; }
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
                if (c == '<') { lexicalSource = Current.Id; lexicalAt = at; lex = Lex.Open; return; }
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
                if (c is >= 'A' and <= 'Z') { AddScalar(scratch, c, at); return; }
                if (!Space(c)) break;
                declaration = scratch.ToString(); scratch.Clear();
                if (declaration is not ("ELEMENT" or "ATTLIST" or "ENTITY" or "NOTATION")) break;
                declarationAt = lexicalAt; declarationSource = lexicalSource; declarationScalars = declaration.Length + 3;
                if (!Bounded(declarationScalars - 1, at)) return;
                separator = true; tokens.Clear(); lex = Lex.Declaration; return;
            case Lex.Declaration:
                if (Space(c)) { separator = true; return; }
                if (c == '>')
                {
                    if (Current.Id != declarationSource) { Reject("Declaration delimiter crosses an entity boundary.", at); return; }
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
                if (Space(c)) { lex = Lex.Pi; return; }
                if (c == '?') { lex = Lex.PiQuestion; return; } break;
            case Lex.Pi:
                if (!Bounded(tokenScalars, at)) return; tokenScalars++;
                if (c == '?') lex = Lex.PiQuestion; return;
            case Lex.PiQuestion:
                if (c == '>' && Current.Id == lexicalSource) { lex = Lex.Between; return; }
                if (!Bounded(tokenScalars, at)) return; tokenScalars++;
                lex = c == '?' ? Lex.PiQuestion : Lex.Pi; return;
            case Lex.Conditional:
                if (c == '%') { BeginParameter(Lex.Conditional, at); return; }
                if (Space(c)) { if (scratch.Length != 0) separator = true; return; }
                if (c == '[')
                {
                    if (Current.Id != lexicalSource) { Reject("Conditional delimiter crosses an entity boundary.", at); return; }
                    if (scratch.ToString() == "INCLUDE") { includes.Push(Current.Id); scratch.Clear(); lex = Lex.Between; return; }
                    if (scratch.ToString() == "IGNORE") { scratch.Clear(); ignoreDepth = 1; ignoreRun = 0; lex = Lex.Ignore; return; }
                    break;
                }
                if (separator && scratch.Length != 0 || c is not (>= 'A' and <= 'Z')) break;
                AddScalar(scratch, c, at); return;
            case Lex.ConditionalEnd:
                if (ignoreRun == 1 && c == ']') { ignoreRun = 2; return; }
                if (ignoreRun == 2 && c == '>' && includes.Peek() == Current.Id && lexicalSource == Current.Id) { includes.Pop(); lex = Lex.Between; return; } break;
            case Lex.Ignore:
                // A three-character recognizer skips arbitrarily long IGNORE bodies, including nested sections.
                if (ignoreRun == 0) { ignoreRun = c == '<' ? 1 : c == ']' ? 3 : 0; return; }
                if (ignoreRun == 1) { ignoreRun = c == '!' ? 2 : c == '<' ? 1 : c == ']' ? 3 : 0; return; }
                if (ignoreRun == 2)
                { if (c == '[') { if (ignoreDepth == int.MaxValue) { Refuse(ValueBudget.Limit(at)); return; } ignoreDepth++; ignoreRun = 0; } else ignoreRun = c == '<' ? 1 : c == ']' ? 3 : 0; return; }
                if (ignoreRun == 3) { ignoreRun = c == ']' ? 4 : c == '<' ? 1 : 0; return; }
                if (c == '>')
                { ignoreRun = 0; if (--ignoreDepth == 0) { if (Current.Id != lexicalSource) { Reject("Ignored section crosses an entity boundary.", at); return; } lex = Lex.Between; } }
                else ignoreRun = c == ']' ? 4 : c == '<' ? 1 : 0;
                return;
        }
        Reject("Invalid DTD syntax.", at);
    }
    private void BeginParameter(Lex resume, XmlLocation at)
    { parameterResume = resume; parameterAt = at; reference.Clear(); referenceScalars = 0; lex = Lex.Parameter; }
    private void ExpandParameter(string name, XmlLocation at, CancellationToken cancellation)
    {
        if (active.Contains(name)) { Reject("Recursive parameter entity.", parameterAt, "DTD.EntityCycle"); return; }
        DtdEntityDeclaration? entity = null;
        foreach (var candidate in parameter) { cancellation.ThrowIfCancellationRequested(); if (candidate.Name == name) { entity = candidate; break; } }
        if (entity is null) { Reject("Undeclared parameter entity.", parameterAt, "DTD.UndeclaredEntity"); return; }
        lex = parameterResume; reference.Clear();
        if (nextId == int.MaxValue) { Refuse(ValueBudget.Limit(parameterAt)); return; }
        if (!Accept(budget.EnterEntity(parameterAt))) return;
        Source? child = null;
        try
        {
            bool literal = parameterResume == Lex.Literal;
            child = new(nextId++, Current.External || entity.SystemId is not null, new(entity.Location.SourceUri, 1, 1, null), name, entity.ReplacementText, true, literal);
            active.Add(name);
            if (!literal) Process(' ', parameterAt, cancellation);
            if (entity.ReplacementText is null)
            { pending = child; request = new(name, entity.SystemId, entity.PublicId, parameterAt); }
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
