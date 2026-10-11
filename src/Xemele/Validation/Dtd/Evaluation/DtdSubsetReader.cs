using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Resolution.Core;

namespace Xemele.Validation;

// String input is already decoded. Nested PE byte sources use M2 framing,
// resolution ownership, aggregate limits and original source mappings unchanged.
internal static class DtdSubsetReader
{
    internal static DtdModel Parse(string text, XmlResourceResolver? resolver, XmlResourceLimits limits, CancellationToken cancellation)
    {
        var budget = new OperationBudget(new() { Limits = limits, Resolver = resolver, DtdProcessing = XmlDtdProcessing.Parse }, cancellation);
        var entities = new XmlEntityBudget(budget, new(StringComparer.Ordinal));
        using var session = new XmlResolutionSession(resolver, budget, cancellation);
        using var parser = new DtdParser(DtdSubsetKind.External, limits, entities, new(null, 1, 1, null));
        var sources = new List<ExternalXmlSource>();
        int offset = 0;
        try
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                DtdParseResult result;
                if (sources.Count == 0)
                {
                    result = parser.Append(text.AsSpan(offset), [], true, cancellation);
                    offset += result.Consumed;
                }
                else
                {
                    var source = sources[^1];
                    if (source.EnsureCharacters(cancellation))
                    {
                        result = parser.Append(source.Characters, source.Mappings, false, cancellation);
                        source.Consume(result.Consumed);
                    }
                    else
                    {
                        if (source.Diagnostic is { } failure) Throw(failure);
                        parser.CompleteByteSource(source.ByteLength);
                        result = parser.Append([], [], true, cancellation);
                        sources.RemoveAt(sources.Count - 1); source.Dispose();
                    }
                }
                if (result.Step == DtdParseStep.Rejected) Throw(result.Diagnostic!);
                if (result.Step == DtdParseStep.Completed) return parser.Model;
                if (result.Step != DtdParseStep.NeedEntity) continue;
                var entity = result.Entity!.Value;
                var request = XmlResolutionSession.CreateRequest(entity.SystemId!, entity.PublicId, entity.Location.SourceUri,
                    XmlResourceKind.ExternalEntity, entity.Location);
                if (request.Diagnostic is { } requestFailure) Throw(requestFailure);
                var opened = session.Open(request.Request!, entity.Location);
                if (opened.Diagnostic is { } openFailure) Throw(openFailure);
                ExternalXmlSource? child = null;
                try
                {
                    child = new(session, opened.Resource!, budget.Options);
                    sources.Add(child);
                    if (!child.Frame(limits, entities, cancellation)) Throw(child.Diagnostic!);
                    parser.PushFramedEntity(child.SourceUri, child.FirstLocation);
                }
                catch
                {
                    session.Abort();
                    try { child?.Dispose(); } catch { }
                    throw;
                }
            }
        }
        catch
        {
            session.Abort();
            for (int i = sources.Count - 1; i >= 0; i--) { try { sources[i].Dispose(); } catch { } }
            sources.Clear();
            throw;
        }
        finally
        {
            for (int i = sources.Count - 1; i >= 0; i--) sources[i].Dispose();
        }
    }
    private static void Throw(XmlDiagnostic diagnostic) => throw new XmlParseException([
        diagnostic.Code.StartsWith("DTD.", StringComparison.Ordinal) ? diagnostic : diagnostic with { Code = "DTD.ExternalInput" }]);
}
