using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private bool doctypeSeen, subsetHasParameterReferences;
    private XmlLocation? firstUnreadParameter;
    private XmlLocation doctypeLocation;
    private string? doctypeName, doctypeSystem, doctypePublic;
    private XmlDocumentType? documentType;
    private DtdParser? dtdParser;
    private StringBuilder? internalSubset;
    private DtdModel? internalModel;
    private bool subsetMarkup, subsetComment, subsetPi;
    private char subsetQuote;
    private int subsetPrefix, subsetDash;
    private bool subsetQuestion;

    private bool ParseDoctypeHeader()
    {
        var i = 0;
        if (tokenLength == 0 || !White(token[0]))
        {
            Reject("XML.Syntax", "DOCTYPE requires separating whitespace.", opening);
            return false;
        }
        SkipWhite(ref i);
        var start = i;
        while (i < tokenLength && !White(token[i]))
        {
            Work(i);
            i++;
        }
        var name = token.AsSpan(start, i - start);
        if (!EntityName(name))
        {
            Reject("XML.Syntax", "Invalid DOCTYPE name.", opening);
            return false;
        }
        doctypeName = Intern(name, opening);
        doctypeLocation = opening;
        var separator = i < tokenLength;
        SkipWhite(ref i);
        doctypePublic = doctypeSystem = null;
        if (i != tokenLength)
        {
            if (!separator)
            {
                Reject("XML.Syntax", "External identifier requires whitespace.", opening);
                return false;
            }
            start = i;
            while (i < tokenLength && !White(token[i]))
            {
                Work(i);
                i++;
            }
            var keyword = token.AsSpan(start, i - start);
            var isPublic = keyword.SequenceEqual("PUBLIC");
            if (!isPublic && !keyword.SequenceEqual("SYSTEM"))
            {
                Reject("XML.Syntax", "Invalid external identifier keyword.", opening);
                return false;
            }
            if (i == tokenLength || !White(token[i]))
            {
                Reject("XML.Syntax", "External identifier requires whitespace.", opening);
                return false;
            }
            SkipWhite(ref i);
            if (isPublic)
            {
                doctypePublic = DoctypeLiteral(ref i, true);
                if (diagnostic is not null)
                {
                    return false;
                }

                if (i == tokenLength || !White(token[i]))
                {
                    Reject("XML.Syntax", "PUBLIC requires a system identifier.", opening);
                    return false;
                }
                SkipWhite(ref i);
            }
            doctypeSystem = DoctypeLiteral(ref i, false);
            if (diagnostic is not null)
            {
                return false;
            }

            SkipWhite(ref i);
        }
        if (i != tokenLength)
        {
            Reject("XML.Syntax", "Unexpected DOCTYPE header content.", opening);
            return false;
        }
        return diagnostic is null;
    }

    private string? DoctypeLiteral(ref int i, bool publicId)
    {
        if (i == tokenLength || token[i] is not ('\'' or '"'))
        {
            Reject("XML.Syntax", "Expected quoted external identifier.", opening);
            return null;
        }
        var delimiter = token[i++];
        var start = i;
        while (i < tokenLength && token[i] != delimiter)
        {
            Work(i);
            if (publicId && !(token[i] is ' ' or '\r' or '\n' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' ||
                "-'()+,./:=?;!*#@$_%".AsSpan().Contains(token[i])))
            {
                Reject("XML.Syntax", "Invalid public identifier character.", tokenLocations[i]);
                return null;
            }
            i++;
        }
        if (i == tokenLength)
        {
            Reject("XML.Syntax", "Unfinished external identifier.", opening);
            return null;
        }
        string value = new(token.AsSpan(start, i++ - start));
        if (publicId)
        {
            var normalized = new StringBuilder();
            var space = false;
            foreach (var c in value)
            {
                activeCancellation.ThrowIfCancellationRequested();
                if (White(c))
                {
                    space = normalized.Length != 0;
                    continue;
                }
                if (space)
                {
                    normalized.Append(' ');
                    space = false;
                }
                normalized.Append(c);
            }
            value = normalized.ToString();
        }
        return value;
    }

    private void BeginDtdSubset()
    {
        state = State.DtdSubset;
        internalSubset = new();
        dtdParser = new(DtdSubsetKind.Internal, budget.Options.Limits, entityBudget!, position.Current());
        dtdParser.ConfigureDeclarationReading(standalone == true, hasExternalSubset: doctypeSystem is not null);
        subsetMarkup = subsetComment = subsetPi = subsetQuestion = false;
        subsetQuote = '\0';
        subsetPrefix = subsetDash = 0;
        ResetToken();
    }

    private void ProcessDtdScalar(int scalar, XmlLocation location)
    {
        if (!subsetMarkup && scalar == ']')
        {
            if (sourceMode == 2)
            {
                dtdParser!.CompleteByteSource(location.ByteOffset!.Value);
            }

            var completed = dtdParser!.Append([], [], true, activeCancellation);
            if (completed.Step == DtdParseStep.Rejected)
            {
                diagnostic = completed.Diagnostic;
                return;
            }
            if (completed.Step != DtdParseStep.Completed)
            {
                Reject("XML.Syntax", "Internal subset did not complete.", location);
                return;
            }
            subsetHasParameterReferences |= dtdParser.HasParameterReferences;
            firstUnreadParameter ??= dtdParser.FirstUnreadParameter;
            internalModel = dtdParser.Model;
            dtdParser.Dispose();
            dtdParser = null;
            state = State.DtdClose;
            return;
        }
        Span<char> chars = stackalloc char[2];
        Span<long> offsets = stackalloc long[2];
        var count = new Rune(scalar).EncodeToUtf16(chars);
        offsets.Fill(location.ByteOffset ?? 0);
        if (internalSubset!.Length > int.MaxValue - count)
        {
            Reject("XML.ResourceLimit", "Internal subset metadata capacity exceeded.", location);
            return;
        }
        internalSubset.Append(chars[..count]);
        var result = dtdParser!.Append(chars[..count], sourceMode == 2 ? offsets[..count] : [], false, activeCancellation);
        if (result.Step == DtdParseStep.Rejected)
        {
            diagnostic = result.Diagnostic;
            return;
        }
        if (result.Step == DtdParseStep.NeedEntity)
        {
            if (asyncResources)
            {
                asyncDtdParser = dtdParser;
                asyncDtdPending = result.Entity;
                resourceWork = ResourceWork.OpenParameter;
            }
            else
            {
                RunDtdSources(dtdParser, null, result.Entity);
                if (diagnostic is not null)
                {
                    return;
                }
            }
        }
        TrackSubsetDelimiter(scalar);
    }

    private void TrackSubsetDelimiter(int c)
    {
        if (!subsetMarkup)
        {
            if (c == '<')
            {
                subsetMarkup = true;
                subsetPrefix = 1;
            }
            return;
        }
        if (subsetComment)
        {
            if (c == '>' && subsetDash == 2)
            {
                subsetMarkup = subsetComment = false;
                subsetDash = 0;
            }
            else
            {
                subsetDash = c == '-' ? Math.Min(2, subsetDash + 1) : 0;
            }

            return;
        }
        if (subsetPi)
        {
            if (c == '>' && subsetQuestion)
            {
                subsetMarkup = subsetPi = false;
            }

            subsetQuestion = c == '?';
            return;
        }
        if (subsetQuote != 0)
        {
            if (c == subsetQuote)
            {
                subsetQuote = '\0';
            }

            return;
        }
        if (subsetPrefix == 1 && c == '?')
        {
            subsetPi = true;
            subsetPrefix = 0;
            return;
        }
        if (subsetPrefix == 1 && c == '!' || subsetPrefix == 2 && c == '-')
        {
            subsetPrefix++;
            return;
        }
        if (subsetPrefix == 3 && c == '-')
        {
            subsetComment = true;
            subsetPrefix = 0;
            subsetDash = 0;
            return;
        }
        subsetPrefix = 0;
        if (c is '\'' or '"')
        {
            subsetQuote = (char)c;
        }
        else if (c == '>')
        {
            subsetMarkup = false;
        }
    }

    private void CompleteDoctype()
    {
        var model = internalModel ?? new([], [], [], [], []);
        if (doctypeSystem is not null && !asyncSubsetRead)
        {
            if (asyncResources)
            {
                internalModel = model;
                resourceWork = ResourceWork.OpenSubset;
                return;
            }
            model = ParseExternalSubset(model);
            if (model is null)
            {
                return;
            }
        }
        entities = new XmlEntityContext(model, activeCancellation);
        if (!ValidateEffectiveDefaults())
        {
            return;
        }

        documentType = new(doctypeName!, doctypePublic, doctypeSystem, internalSubset?.ToString());
        internalSubset = null;
        internalModel = null;
        state = State.Data;
        delivery = Delivery.DocumentType;
        ResetToken();
    }
}
