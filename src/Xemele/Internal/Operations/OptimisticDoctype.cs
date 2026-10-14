using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Syntax;
using Xemele.Streaming;

namespace Xemele.Internal.Operations;

// A declared internal general entity reduced to what the optimistic parsers can reproduce exactly: replacement text made of plain
// characters and the five predefined references. Length is the replacement text in UTF-16 units (an upper bound on both the
// scalars and the spelled characters the streaming parser charges) and InnerRefs the predefined references inside it.
internal sealed class OptimisticEntity
{
    internal string Content = "", Attribute = "";
    internal int Length, InnerRefs;
}

// The DOCTYPE of a whole-buffer parse. The real streaming parser reads the declaration (so DTD semantics, quotas and
// diagnostics come from the same code); only documents whose DOCTYPE is nothing but internal general entities are kept.
internal sealed class OptimisticDoctype
{
    internal int End;
    internal XmlDocumentType Declaration = null!;
    internal DtdModel Model = null!;
    // Expanded characters and interned names the streaming parser has charged once the DOCTYPE is behind it.
    internal long Expanded, Names;
    internal XmlLocation Location;
    // A null value is an entity the fast path cannot expand; using it declines the whole document.
    internal Dictionary<string, OptimisticEntity?> Entities = null!;

    internal static bool TryRead(ReadOnlySpan<byte> input, int start, int line, int column, bool charInput,
        XmlParseOptions options, CancellationToken cancellationToken, out OptimisticDoctype? doctype)
    {
        doctype = null;
        if (options.DtdProcessing != XmlDtdProcessing.Parse) return false;
        if (!XmlParserCore.TryScanDoctype(input, start, line, column, charInput, options, cancellationToken, out var scan)) return false;
        var model = scan.Model;
        if (scan.Declaration.PublicId is not null || scan.Declaration.SystemId is not null ||
            model.Elements.Count != 0 || model.Attributes.Count != 0 || model.Notations.Count != 0 || model.ParameterEntities.Count != 0 ||
            model.DeclarationValidityDiagnostic is not null || model.UnreadParameterLocation is not null) return false;
        var table = new Dictionary<string, OptimisticEntity?>(model.GeneralEntities.Count, StringComparer.Ordinal);
        foreach (var declaration in model.GeneralEntities)
        {
            // Redeclaring a predefined entity is left to the streaming parser.
            if (declaration.Name is "lt" or "gt" or "amp" or "apos" or "quot") return false;
            table.TryAdd(declaration.Name, Analyze(declaration));
        }
        doctype = new() { End = scan.End, Declaration = scan.Declaration, Model = model, Expanded = scan.Expanded, Names = scan.Names, Location = scan.Location, Entities = table };
        return true;
    }

    internal bool TryFind(ReadOnlySpan<byte> name, out OptimisticEntity? entity)
    {
        Span<char> chars = stackalloc char[name.Length];
        for (int k = 0; k < chars.Length; k++) chars[k] = (char)name[k];
        return Entities.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(chars, out entity);
    }

    // Delivers the DTD events exactly as the streaming pump does: StartDtd, one declaration per general entity, EndDtd,
    // with a cancellation check before each callback and after the last.
    internal void Deliver(IXmlSaxHandler handler, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        handler.StartDtd(new(Declaration.Name, Declaration.PublicId, Declaration.SystemId,
            Declaration.PublicId is not null, Declaration.SystemId is not null, Location));
        foreach (var entity in Model.GeneralEntities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            handler.DtdDeclaration(new(XmlDtdDeclarationKind.Entity, entity.Name, [], [], [], XmlDtdDefaultKind.None,
                entity.ReplacementText, entity.ReplacementText is not null, entity.SystemId, entity.SystemId is not null, entity.PublicId,
                entity.PublicId is not null, entity.NotationName, entity.IsParameter, entity.IsExternal, entity.Location));
        }
        cancellationToken.ThrowIfCancellationRequested();
        handler.EndDtd();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static OptimisticEntity? Analyze(DtdEntityDeclaration declaration)
    {
        if (declaration.IsParameter || declaration.IsExternal || declaration.ReplacementText is not { } text ||
            declaration.SystemId is not null || declaration.PublicId is not null || declaration.NotationName is not null) return null;
        var content = new StringBuilder(text.Length);
        var attribute = new StringBuilder(text.Length);
        int references = 0;
        for (int k = 0; k < text.Length; k++)
        {
            char c = text[k];
            if (c == '&')
            {
                var rest = text.AsSpan(k);
                char value; int length;
                if (rest.StartsWith("&lt;")) { value = '<'; length = 4; }
                else if (rest.StartsWith("&gt;")) { value = '>'; length = 4; }
                else if (rest.StartsWith("&amp;")) { value = '&'; length = 5; }
                else if (rest.StartsWith("&apos;")) { value = '\''; length = 6; }
                else if (rest.StartsWith("&quot;")) { value = '"'; length = 6; }
                else return null;
                content.Append(value); attribute.Append(value);
                k += length - 1; references++;
            }
            else if (c is '<' or ']' or '>' or '\r' or '\uFFFE' or '\uFFFF' || (c < ' ' && c is not ('\t' or '\n'))) return null;
            else if (char.IsHighSurrogate(c))
            {
                if (k + 1 >= text.Length || !char.IsLowSurrogate(text[k + 1])) return null;
                content.Append(c).Append(text[k + 1]); attribute.Append(c).Append(text[k + 1]); k++;
            }
            else if (char.IsLowSurrogate(c)) return null;
            else { content.Append(c); attribute.Append(c is '\t' or '\n' ? ' ' : c); }
        }
        return new() { Content = content.ToString(), Attribute = attribute.ToString(), Length = text.Length, InnerRefs = references };
    }
}
