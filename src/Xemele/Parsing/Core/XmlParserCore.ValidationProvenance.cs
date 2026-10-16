using Xemele.Parsing.Dtd;
using Xemele.Internal.Operations;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private XmlDiagnostic? validationInstanceDiagnostic;
    private DtdElementDeclaration? validationInstanceDeclaration;

    private DtdElementDeclaration? ValidationElementDeclaration(ReadOnlySpan<char> name)
    {
        if (validationInstanceDiagnostic is not null || entities is null || sinkIdentity is not IXmlValidationProvenanceSink)
        {
            return null;
        }

        var elements = entities.Model.Elements;
        for (var i = 0; i < elements.Count; i++)
        {
            activeCancellation.ThrowIfCancellationRequested();
            if (!Charge(Quota.EvaluationWork, 1, opening))
            {
                return null;
            }

            var declaration = elements[i];
            if (name.Length != declaration.Name.Length)
            {
                continue;
            }

            var equal = true;
            for (var offset = 0; offset < name.Length;)
            {
                activeCancellation.ThrowIfCancellationRequested();
                var count = Math.Min(256, name.Length - offset);
                if (!Charge(Quota.EvaluationWork, count, opening))
                {
                    return null;
                }

                if (!name.Slice(offset, count).SequenceEqual(declaration.Name.AsSpan(offset, count)))
                {
                    equal = false;
                    break;
                }
                offset += count;
            }
            if (equal)
            {
                return declaration;
            }
        }
        return null;
    }

    private void RetainValidationReference(bool characterWhitespace, XmlLocation location)
    {
        if (sinkIdentity is not IXmlValidationProvenanceSink || validationInstanceDiagnostic is not null || frames.Count == 0 ||
            frames[^1].ValidationDeclaration is not { } declaration)
        {
            return;
        }

        var empty = declaration.Content.Kind == DtdParticleKind.Empty;
        if (!empty && !(characterWhitespace && declaration.Content.Kind is
            DtdParticleKind.Name or DtdParticleKind.Sequence or DtdParticleKind.Choice))
        {
            return;
        }

        validationInstanceDiagnostic = new("DTD.Content", XmlDiagnosticSeverity.Error,
            empty ? "EMPTY content contains a reference." : "Element-only content contains a character reference.", location);
        validationInstanceDeclaration = declaration;
    }
}
