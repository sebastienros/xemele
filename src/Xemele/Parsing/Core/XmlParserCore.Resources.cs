using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Resolution.Core;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private XmlResolutionSession? resolution;

    private ExternalXmlSource? OpenExternalSource(string systemId, string? publicId, XmlResourceKind kind, XmlLocation at)
    {
        activeCancellation.ThrowIfCancellationRequested();
        resolution ??= new(budget.Options.Resolver, budget, activeCancellation);
        var request = XmlResolutionSession.CreateRequest(systemId, publicId, at.SourceUri, kind, at);
        if (request.Diagnostic is not null) { diagnostic = request.Diagnostic; return null; }
        var opened = resolution.Open(request.Request!, at);
        if (opened.Diagnostic is not null) { diagnostic = opened.Diagnostic; return null; }
        ExternalXmlSource? source = null;
        try
        {
            source = new(resolution, opened.Resource!, budget.Options);
            if (source.Frame(budget.Options.Limits, entityBudget!, activeCancellation)) return source;
            diagnostic = source.Diagnostic;
            resolution.Abort(); source.Dispose(); return null;
        }
        catch
        {
            resolution.Abort();
            try { source?.Dispose(); } catch { }
            throw;
        }
    }

    private DtdModel? ParseExternalSubset(DtdModel prior)
    {
        var source = OpenExternalSource(doctypeSystem!, doctypePublic, XmlResourceKind.ExternalSubset, doctypeLocation);
        if (source is null) return null;
        DtdParser? parser = null;
        try
        {
            parser = new(DtdSubsetKind.External, budget.Options.Limits, entityBudget!,
                new XmlLocation(source.SourceUri, 1, 1, null));
            parser.ConfigureDeclarationReading(standalone == true, firstUnreadParameter);
            parser.SeedPriorModel(prior, activeCancellation);
            parser.SetFramedInitialSource(source.FirstLocation);
            return RunDtdSources(parser, source, null);
        }
        catch
        {
            resolution!.Abort();
            try { source.Dispose(); } catch { }
            throw;
        }
        finally { parser?.Dispose(); }
    }

    // For an internal-subset NeedEntity the initial source is null: exhausting
    // the last external PE returns to the primary caller rather than completing
    // the internal subset. Each parent retains its exact unconsumed scratch.
    private DtdModel? RunDtdSources(DtdParser parser, ExternalXmlSource? initial, DtdEntityRequest? pending)
    {
        var stack = new List<ExternalXmlSource>();
        if (initial is not null) stack.Add(initial);
        try
        {
            while (diagnostic is null)
            {
                activeCancellation.ThrowIfCancellationRequested();
                if (pending is { } entity)
                {
                    var source = OpenExternalSource(entity.SystemId!, entity.PublicId,
                        XmlResourceKind.ExternalEntity, entity.Location);
                    if (source is null) return null;
                    // Reserve ownership before invoking parser callbacks/hooks.
                    stack.Add(source);
                    parser.PushFramedEntity(source.SourceUri, source.FirstLocation);
                    pending = null;
                }
                if (stack.Count == 0) return null;
                var current = stack[^1];
                DtdParseResult result;
                if (current.EnsureCharacters(activeCancellation))
                {
                    result = parser.Append(current.Characters, current.Mappings, false, activeCancellation);
                    current.Consume(result.Consumed);
                }
                else
                {
                    if (current.Diagnostic is not null) { diagnostic = current.Diagnostic; return null; }
                    parser.CompleteByteSource(current.ByteLength);
                    result = parser.Append([], [], true, activeCancellation);
                    stack.RemoveAt(stack.Count - 1); current.Dispose();
                }
                if (result.Step == DtdParseStep.Rejected) { diagnostic = result.Diagnostic; return null; }
                if (result.Step == DtdParseStep.Completed)
                {
                    subsetHasParameterReferences |= parser.HasParameterReferences;
                    firstUnreadParameter ??= parser.FirstUnreadParameter;
                    return parser.Model;
                }
                if (result.Step == DtdParseStep.NeedEntity) pending = result.Entity;
            }
            return null;
        }
        catch
        {
            resolution!.Abort();
            while (stack.Count != 0)
            {
                var source = stack[^1]; stack.RemoveAt(stack.Count - 1);
                try { source.Dispose(); } catch { }
            }
            throw;
        }
        finally
        {
            if (diagnostic is not null) resolution!.Abort();
            while (stack.Count != 0)
            { var source = stack[^1]; stack.RemoveAt(stack.Count - 1); source.Dispose(); }
        }
    }
}
