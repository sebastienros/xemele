namespace Xemele.Html;

internal sealed partial class HtmlTokenizer
{
    // true requests reconsumption in the new state.
    private bool DoctypeStep(int c)
    {
        switch (state)
        {
            case State.Doctype:
                if (Space(c))
                {
                    state = State.BeforeDoctypeName;
                    return false;
                }
                if (c != '>' && c >= 0)
                {
                    Error("missing-whitespace-before-doctype-name");
                }

                state = State.BeforeDoctypeName;
                return true;
            case State.BeforeDoctypeName:
                if (Space(c))
                {
                    return false;
                }

                if (c == '>' || c < 0)
                {
                    Error(c < 0 ? "eof-in-doctype" : "missing-doctype-name");
                    forceQuirks = true;
                    EmitDoctype();
                    return c < 0;
                }
                Append(ref name, ref nameScalars, Fold(ReplaceNull(c)), false);
                state = State.DoctypeName;
                return false;
            case State.DoctypeName:
                if (Space(c))
                {
                    state = State.AfterDoctypeName;
                    return false;
                }
                if (c == '>')
                {
                    EmitDoctype();
                    return false;
                }
                if (c < 0)
                {
                    Error("eof-in-doctype");
                    forceQuirks = true;
                    EmitDoctype();
                    return true;
                }
                Append(ref name, ref nameScalars, Fold(ReplaceNull(c)), false);
                return false;
            case State.AfterDoctypeName:
                if (Space(c))
                {
                    return false;
                }

                if (c == '>')
                {
                    EmitDoctype();
                    return false;
                }
                if (c < 0)
                {
                    Error("eof-in-doctype");
                    forceQuirks = true;
                    EmitDoctype();
                    return true;
                }
                ClearTemporary();
                lookaheadOpening = current;
                state = State.DoctypeKeyword;
                return true;
            case State.DoctypeKeyword:
                if (c >= 0)
                {
                    temporary.AppendScalar(c);
                }

                if (BuilderEquals(ref temporary, "public", true))
                {
                    state = State.AfterPublicKeyword;
                    return false;
                }
                if (BuilderEquals(ref temporary, "system", true))
                {
                    state = State.AfterSystemKeyword;
                    return false;
                }
                if (c >= 0 && (BuilderPrefix(ref temporary, "public", true) || BuilderPrefix(ref temporary, "system", true)))
                {
                    return false;
                }

                Error("invalid-character-sequence-after-doctype-name", lookaheadOpening);
                forceQuirks = true;
                state = State.BogusDoctype;
                return true;
            case State.AfterPublicKeyword:
            case State.AfterSystemKeyword:
                var isPublic = state == State.AfterPublicKeyword;
                if (Space(c))
                {
                    state = isPublic ? State.BeforePublic : State.BeforeSystem;
                    return false;
                }
                if (c is '"' or '\'')
                {
                    Error(isPublic ? "missing-whitespace-after-doctype-public-keyword" : "missing-whitespace-after-doctype-system-keyword");
                    BeginIdentifier(isPublic, c);
                    return false;
                }
                if (c == '>' || c < 0)
                {
                    Error(c < 0 ? "eof-in-doctype" : isPublic ? "missing-doctype-public-identifier" : "missing-doctype-system-identifier");
                    forceQuirks = true;
                    EmitDoctype();
                    return c < 0;
                }
                Error(isPublic ? "missing-quote-before-doctype-public-identifier" : "missing-quote-before-doctype-system-identifier");
                forceQuirks = true;
                state = State.BogusDoctype;
                return true;
            case State.BeforePublic:
            case State.BeforeSystem:
                isPublic = state == State.BeforePublic;
                if (Space(c))
                {
                    return false;
                }

                if (c is '"' or '\'')
                {
                    BeginIdentifier(isPublic, c);
                    return false;
                }
                if (c == '>' || c < 0)
                {
                    Error(c < 0 ? "eof-in-doctype" : isPublic ? "missing-doctype-public-identifier" : "missing-doctype-system-identifier");
                    forceQuirks = true;
                    EmitDoctype();
                    return c < 0;
                }
                Error(isPublic ? "missing-quote-before-doctype-public-identifier" : "missing-quote-before-doctype-system-identifier");
                forceQuirks = true;
                state = State.BogusDoctype;
                return true;
            case State.PublicDouble:
            case State.PublicSingle:
            case State.SystemDouble:
            case State.SystemSingle:
                isPublic = state is State.PublicDouble or State.PublicSingle;
                int quote = state is State.PublicDouble or State.SystemDouble ? '"' : '\'';
                if (c == quote)
                {
                    if (isPublic)
                    {
                        publicId = data.ToString();
                    }
                    else
                    {
                        systemId = data.ToString();
                    }

                    state = isPublic ? State.AfterPublic : State.AfterSystem;
                    return false;
                }
                if (c == '>' || c < 0)
                {
                    Error(c < 0 ? "eof-in-doctype" : isPublic ? "abrupt-doctype-public-identifier" : "abrupt-doctype-system-identifier");
                    if (isPublic)
                    {
                        publicId = data.ToString();
                    }
                    else
                    {
                        systemId = data.ToString();
                    }

                    forceQuirks = true;
                    EmitDoctype();
                    return c < 0;
                }
                Append(ref data, ref dataScalars, ReplaceNull(c));
                return false;
            case State.AfterPublic:
                if (Space(c))
                {
                    state = State.BetweenIdentifiers;
                    return false;
                }
                if (c == '>')
                {
                    EmitDoctype();
                    return false;
                }
                if (c is '"' or '\'')
                {
                    Error("missing-whitespace-between-doctype-public-and-system-identifiers");
                    BeginIdentifier(false, c);
                    return false;
                }
                if (c < 0)
                {
                    Error("eof-in-doctype");
                    forceQuirks = true;
                    EmitDoctype();
                    return true;
                }
                Error("missing-quote-before-doctype-system-identifier");
                forceQuirks = true;
                state = State.BogusDoctype;
                return true;
            case State.BetweenIdentifiers:
                if (Space(c))
                {
                    return false;
                }

                if (c == '>')
                {
                    EmitDoctype();
                    return false;
                }
                if (c is '"' or '\'')
                {
                    BeginIdentifier(false, c);
                    return false;
                }
                if (c < 0)
                {
                    Error("eof-in-doctype");
                    forceQuirks = true;
                    EmitDoctype();
                    return true;
                }
                Error("missing-quote-before-doctype-system-identifier");
                forceQuirks = true;
                state = State.BogusDoctype;
                return true;
            case State.AfterSystem:
                if (Space(c))
                {
                    return false;
                }

                if (c == '>')
                {
                    EmitDoctype();
                    return false;
                }
                if (c < 0)
                {
                    Error("eof-in-doctype");
                    forceQuirks = true;
                    EmitDoctype();
                    return true;
                }
                Error("unexpected-character-after-doctype-system-identifier");
                state = State.BogusDoctype;
                return true;
            case State.BogusDoctype:
                if (c == '>' || c < 0)
                {
                    EmitDoctype();
                    return c < 0;
                }
                if (c == 0)
                {
                    Error("unexpected-null-character");
                }

                return false;
            default:
                throw new InvalidOperationException();
        }
    }

    private void BeginIdentifier(bool isPublic, int quote)
    {
        ClearData();
        if (isPublic)
        {
            publicId = "";
            state = quote == '"' ? State.PublicDouble : State.PublicSingle;
        }
        else
        {
            systemId = "";
            state = quote == '"' ? State.SystemDouble : State.SystemSingle;
        }
    }
}
