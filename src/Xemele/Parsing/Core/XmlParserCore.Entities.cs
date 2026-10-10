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
        if (!EntityName(name)) { Reject("XML.Syntax", "Invalid general entity name.", reference); return true; }
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
        if (declaration.ReplacementText is null)
        { Reject("XML.ResourceDenied", "External entity composition is not yet available.", reference); return true; }
        if (!Charge(Quota.EntityDepth, 1, reference)) return true;
        try { entitySources.Add(new(declaration, reference, frames.Count)); }
        catch { budget.Release(Quota.EntityDepth, 1); throw; }
        return true;
    }

    private bool DrainEntityScalar()
    {
        if (entitySources is not { Count: > 0 }) return false;
        var source = entitySources[^1];
        activeCancellation.ThrowIfCancellationRequested();
        string text = source.Declaration.ReplacementText!;
        if (source.Index == text.Length)
        {
            if (state != State.Data || frames.Count != source.ElementDepth)
            { Reject("XML.Syntax", "General entity crosses a markup or element boundary.", source.Reference); return false; }
            entitySources.RemoveAt(entitySources.Count - 1);
            budget.Release(Quota.EntityDepth, 1);
            return true;
        }
        int scalar = text[source.Index++];
        if (char.IsHighSurrogate((char)scalar))
        {
            if (source.Index == text.Length || !char.IsLowSurrogate(text[source.Index]))
            { Reject("XML.Syntax", "Unpaired high surrogate in entity.", source.Reference); return true; }
            scalar = char.ConvertToUtf32((char)scalar, text[source.Index++]);
        }
        if (!XmlScalar(scalar)) { Reject("XML.Syntax", "Invalid entity replacement scalar.", source.Reference); return true; }
        if (!Charge(Quota.ExpandedCharacters, 1, source.Reference)) return true;
        // Internal replacement text was newline-normalized while declaring it;
        // character references resolved there must retain their resulting CR.
        Process(scalar, source.Reference);
        return true;
    }

    private void AbortEntities() => DisposeEntities();

    private void DisposeEntities()
    {
        if (entitySources is not null)
        {
            while (entitySources.Count != 0)
            {
                entitySources.RemoveAt(entitySources.Count - 1);
                budget.Release(Quota.EntityDepth, 1);
            }
            entitySources = null;
        }
        entities = null; entityBudget = null;
    }
}
