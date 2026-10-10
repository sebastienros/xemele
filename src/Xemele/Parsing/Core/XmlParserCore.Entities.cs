using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private sealed class EntitySource(DtdEntityDeclaration declaration, XmlLocation reference, int elementDepth)
    {
        internal readonly DtdEntityDeclaration Declaration = declaration;
        internal readonly XmlLocation Reference = reference;
        internal readonly int ElementDepth = elementDepth;
        internal int Index;
        internal ExternalXmlSource? External;
        internal int Line, Column;
        internal bool Cr;
    }

    // These remain null on the no-DTD path. A source retains declaration-owned
    // replacement text, never a view of primary input or a callback buffer.
    private IXmlEntityContext? entities;
    private XmlEntityBudget? entityBudget;
    private List<EntitySource>? entitySources;

    internal XmlParserCore(XmlParseOptions options, IXmlEntityContext entities, Uri? sourceUri = null)
        : this(options, sourceUri)
    {
        ArgumentNullException.ThrowIfNull(entities);
        this.entities = entities;
        entityBudget = new(budget, names);
    }

    private static bool ScalarReference(ReadOnlySpan<char> name) =>
        !name.IsEmpty && name[0] == '#' || name.SequenceEqual("lt") || name.SequenceEqual("gt") ||
        name.SequenceEqual("amp") || name.SequenceEqual("apos") || name.SequenceEqual("quot");

    private bool EntityName(ReadOnlySpan<char> name)
    {
        bool first = true;
        foreach (var scalar in name.EnumerateRunes())
        {
            activeCancellation.ThrowIfCancellationRequested();
            if (first ? !NameStart(scalar.Value) : !NameChar(scalar.Value)) return false;
            first = false;
        }
        return !first;
    }

    // True means this is a declared general reference, including a rejected one.
    // Scalar references keep the earlier path and are never reparsed as markup.
    private bool PushGeneralEntity(ReadOnlySpan<char> name, XmlLocation reference)
    {
        if (ScalarReference(name) || entities is null || !entities.TryGetGeneralEntity(name, out var declaration)) return false;
        if (declaration is null) return false;
        if (!ValidName(name, false)) { Reject("XML.Syntax", "Invalid general entity name.", reference); return true; }
        if (declaration.NotationName is not null)
        { Reject("XML.Syntax", "Unparsed entity cannot be referenced in content.", reference); return true; }
        if (standalone == true && declaration.IsExternal)
        { Reject("XML.Standalone", "Standalone document depends on an external entity declaration.", reference); return true; }
        entitySources ??= [];
        foreach (var source in entitySources)
        {
            activeCancellation.ThrowIfCancellationRequested();
            if (source.Declaration.Name == declaration.Name)
            { Reject("XML.EntityCycle", "Recursive general entity reference.", reference); return true; }
        }
        if (!Charge(Quota.EntityDepth, 1, reference)) return true;
        EntitySource sourceFrame;
        try { entitySources.Add(sourceFrame = new(declaration, reference, frames.Count)); }
        catch { budget.Release(Quota.EntityDepth, 1); throw; }
        if (declaration.ReplacementText is null)
        {
            if (declaration.SystemId is null)
            { Reject("XML.Syntax", "General entity has no replacement or external identifier.", reference); return true; }
            sourceFrame.External = OpenExternalSource(declaration.SystemId, declaration.PublicId,
                XmlResourceKind.ExternalEntity, declaration.Location);
            if (sourceFrame.External is not null)
            {
                var start = sourceFrame.External.FirstLocation;
                sourceFrame.Line = start.LineNumber; sourceFrame.Column = start.LinePosition;
            }
        }
        return true;
    }

    private bool DrainEntityScalar()
    {
        if (entitySources is not { Count: > 0 }) return false;
        var source = entitySources[^1];
        activeCancellation.ThrowIfCancellationRequested();
        int scalar;
        XmlLocation at = source.Reference;
        if (source.External is { } external)
        {
            if (!external.EnsureCharacters(activeCancellation))
            {
                if (external.Diagnostic is not null) { diagnostic = external.Diagnostic; return true; }
                return FinishEntitySource(source);
            }
            var chars = external.Characters;
            at = new(external.SourceUri, source.Line, source.Column, external.Mappings[0]);
            scalar = chars[0]; int width = 1;
            if (char.IsHighSurrogate((char)scalar))
            { scalar = char.ConvertToUtf32((char)scalar, chars[1]); width = 2; }
            external.Consume(width);
        }
        else
        {
            string text = source.Declaration.ReplacementText!;
            if (source.Index == text.Length) return FinishEntitySource(source);
            scalar = text[source.Index++];
            if (char.IsHighSurrogate((char)scalar))
            {
                if (source.Index == text.Length || !char.IsLowSurrogate(text[source.Index]))
                { Reject("XML.Syntax", "Unpaired high surrogate in entity.", at); return true; }
                scalar = char.ConvertToUtf32((char)scalar, text[source.Index++]);
            }
        }
        if (!XmlScalar(scalar)) { Reject("XML.Syntax", "Invalid entity replacement scalar.", at); return true; }
        if (!Charge(Quota.ExpandedCharacters, 1, at)) return true;
        if (source.External is not null)
        {
            bool skip = source.Cr && scalar == '\n';
            if (scalar is '\r' or '\n')
            {
                if (!skip)
                {
                    if (source.Line == int.MaxValue) { Reject("XML.LocationLimit", "Entity location capacity exceeded.", at); return true; }
                    if (source.Line != 0) { source.Line++; source.Column = 1; }
                }
            }
            else if (source.Column != 0)
            {
                if (source.Column == int.MaxValue) { Reject("XML.LocationLimit", "Entity location capacity exceeded.", at); return true; }
                source.Column++;
            }
            source.Cr = scalar == '\r';
            if (skip) return true;
            if (scalar == '\r') scalar = '\n';
        }
        // Internal replacement text was normalized while declaring it; resolved
        // character references retain CR. External source newlines normalize once.
        Process(scalar, at);
        return true;
    }

    private bool FinishEntitySource(EntitySource source)
    {
        if (state != State.Data || frames.Count != source.ElementDepth)
        { Reject("XML.Syntax", "General entity crosses a markup or element boundary.", source.Reference); return true; }
        source.External?.Dispose();
        entitySources!.RemoveAt(entitySources.Count - 1);
        budget.Release(Quota.EntityDepth, 1);
        return true;
    }

    private void AbortEntities()
    {
        resolution?.Abort();
        DisposeEntities();
    }

    private void DisposeEntities()
    {
        if (entitySources is not null)
        {
            while (entitySources.Count != 0)
            {
                var source = entitySources[^1];
                entitySources.RemoveAt(entitySources.Count - 1);
                source.External?.Dispose();
                budget.Release(Quota.EntityDepth, 1);
            }
            entitySources = null;
        }
        dtdParser?.Dispose(); dtdParser = null;
        resolution?.Dispose(); resolution = null;
        entities = null; entityBudget = null;
        attributeEntities = null; dtdAttributes = null; ids = null; internalSubset = null; internalModel = null;
    }
}
