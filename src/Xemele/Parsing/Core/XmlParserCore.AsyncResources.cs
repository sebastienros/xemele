using System.Runtime.ExceptionServices;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Resolution.Core;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private enum ResourceWork { None, OpenSubset, OpenParameter, OpenGeneral, RefillDtd, FinishDtd, RefillGeneral, FinishGeneral, Cleanup }
    private bool asyncResources;
    private ResourceWork resourceWork;
    private DtdParser? asyncDtdParser;
    private List<ExternalXmlSource>? asyncDtdSources;
    private DtdEntityRequest? asyncDtdPending;
    private ExternalXmlSource? asyncDtdDisposal;
    private bool asyncSubsetRead, asyncFinishDoctype, asyncCleanupCompleted;

    // Append never calls resource code in this mode. The owner retains its input
    // and resumes the exact unconsumed tail after awaiting this single pending
    // resource operation. No event sink is invoked here, even at resource EOF.
    internal async ValueTask AdvanceResourceAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Concurrent or reentrant core use.");
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!asyncResources || resourceWork == ResourceWork.None || terminal)
                throw new InvalidOperationException("No pending async resource work.");
            if (resourceWork == ResourceWork.Cleanup)
            {
                await CleanupAsync(true).ConfigureAwait(false);
                resourceWork = ResourceWork.None; asyncCleanupCompleted = true; return;
            }
            CheckUsable();
            try
            {
                activeCancellation = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                if (resolution is not null)
                    await resolution.BindCancellationTokenAsync(cancellationToken).ConfigureAwait(false);
                var work = resourceWork; resourceWork = ResourceWork.None;
                switch (work)
                {
                    case ResourceWork.OpenSubset:
                    {
                        var source = await OpenExternalSourceAsync(doctypeSystem!, doctypePublic,
                            XmlResourceKind.ExternalSubset, doctypeLocation).ConfigureAwait(false);
                        if (source is null) break;
                        (asyncDtdSources ??= []).Add(source);
                        asyncDtdParser = new(DtdSubsetKind.External, budget.Options.Limits, entityBudget!,
                            new XmlLocation(source.SourceUri, 1, 1, null));
                        asyncDtdParser.ConfigureDeclarationReading(standalone == true, firstUnreadParameter);
                        asyncDtdParser.SeedPriorModel(internalModel!, cancellationToken);
                        asyncDtdParser.SetFramedInitialSource(source.FirstLocation);
                        break;
                    }
                    case ResourceWork.OpenParameter:
                    {
                        var entity = asyncDtdPending!.Value;
                        var source = await OpenExternalSourceAsync(entity.SystemId!, entity.PublicId,
                            XmlResourceKind.ExternalEntity, entity.Location).ConfigureAwait(false);
                        if (source is null) break;
                        (asyncDtdSources ??= []).Add(source);
                        asyncDtdParser!.PushFramedEntity(source.SourceUri, source.FirstLocation);
                        asyncDtdPending = null;
                        break;
                    }
                    case ResourceWork.OpenGeneral:
                    {
                        var frame = entitySources![^1];
                        var source = await OpenExternalSourceAsync(frame.Declaration.SystemId!, frame.Declaration.PublicId,
                            XmlResourceKind.ExternalEntity, frame.Declaration.Location).ConfigureAwait(false);
                        frame.External = source;
                        if (source is not null)
                        { frame.Line = source.FirstLocation.LineNumber; frame.Column = source.FirstLocation.LinePosition; }
                        entitySources[^1] = frame;
                        break;
                    }
                    case ResourceWork.RefillDtd:
                    {
                        var source = asyncDtdSources![^1];
                        await source.EnsureCharactersAsync(cancellationToken).ConfigureAwait(false);
                        diagnostic = source.Diagnostic;
                        break;
                    }
                    case ResourceWork.FinishDtd:
                    {
                        var source = asyncDtdDisposal!; asyncDtdDisposal = null;
                        await source.DisposeAsync().ConfigureAwait(false); break;
                    }
                    case ResourceWork.RefillGeneral:
                    {
                        var source = entitySources![^1].External!;
                        await source.EnsureCharactersAsync(cancellationToken).ConfigureAwait(false);
                        diagnostic = source.Diagnostic;
                        break;
                    }
                    case ResourceWork.FinishGeneral:
                    {
                        var frame = entitySources![^1];
                        if (state != State.Data || frames.Count != frame.ElementDepth)
                        {
                            Reject("XML.Syntax", "General entity crosses a markup or element boundary.",
                                new(frame.External!.SourceUri, frame.Line, frame.Column, frame.External.ByteLength));
                            break;
                        }
                        entitySources.RemoveAt(entitySources.Count - 1);
                        budget.Release(Quota.EntityDepth, 1);
                        await frame.External!.DisposeAsync().ConfigureAwait(false); break;
                    }
                }
            }
            catch
            {
                faulted = true;
                await CleanupAsync(true).ConfigureAwait(false);
                asyncCleanupCompleted = true; resourceWork = ResourceWork.None;
                throw;
            }
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    private async ValueTask<ExternalXmlSource?> OpenExternalSourceAsync(string systemId, string? publicId,
        XmlResourceKind kind, XmlLocation at)
    {
        activeCancellation.ThrowIfCancellationRequested();
        resolution ??= new(budget.Options.Resolver, budget, activeCancellation);
        var request = XmlResolutionSession.CreateRequest(systemId, publicId, at.SourceUri, kind, at);
        if (request.Diagnostic is not null) { diagnostic = request.Diagnostic; return null; }
        var opened = await resolution.OpenAsync(request.Request!, at).ConfigureAwait(false);
        if (opened.Diagnostic is not null) { diagnostic = opened.Diagnostic; return null; }
        ExternalXmlSource? source = null;
        try
        {
            source = new(resolution, opened.Resource!, budget.Options);
            if (await source.FrameAsync(budget.Options.Limits, entityBudget!, activeCancellation).ConfigureAwait(false)) return source;
            diagnostic = source.Diagnostic;
            await resolution.AbortAsync().ConfigureAwait(false);
            await source.DisposeAsync().ConfigureAwait(false); return null;
        }
        catch
        {
            try { await resolution.AbortAsync().ConfigureAwait(false); } catch { }
            try { if (source is not null) await source.DisposeAsync().ConfigureAwait(false); } catch { }
            throw;
        }
    }

    // Grammar work remains inside Append's callback/pause protocol. Each source
    // holds fixed scratch and exact indices while a nested PE suspends it.
    private bool DrainAsyncDtd()
    {
        if (asyncFinishDoctype)
        { asyncFinishDoctype = false; CompleteDoctype(); return true; }
        if (asyncDtdParser is null) return false;
        if (asyncDtdPending is not null)
        { resourceWork = ResourceWork.OpenParameter; return true; }
        if (asyncDtdSources is not { Count: > 0 })
        { asyncDtdParser = null; return false; } // Back to the same internal subset.
        var source = asyncDtdSources[^1];
        DtdParseResult result;
        if (source.HasCharacters)
        {
            result = asyncDtdParser.Append(source.Characters, source.Mappings, false, activeCancellation);
            source.Consume(result.Consumed);
        }
        else if (!source.Completed)
        { resourceWork = ResourceWork.RefillDtd; return true; }
        else
        {
            asyncDtdParser.CompleteByteSource(source.ByteLength);
            result = asyncDtdParser.Append([], [], true, activeCancellation);
            asyncDtdSources.RemoveAt(asyncDtdSources.Count - 1);
            asyncDtdDisposal = source; resourceWork = ResourceWork.FinishDtd;
        }
        if (result.Step == DtdParseStep.Rejected) diagnostic = result.Diagnostic;
        else if (result.Step == DtdParseStep.Completed)
        {
            subsetHasParameterReferences |= asyncDtdParser.HasParameterReferences;
            firstUnreadParameter ??= asyncDtdParser.FirstUnreadParameter;
            internalModel = asyncDtdParser.Model;
            asyncDtdParser.Dispose(); asyncDtdParser = null;
            asyncSubsetRead = true; asyncFinishDoctype = true;
        }
        else if (result.Step == DtdParseStep.NeedEntity)
        {
            asyncDtdPending = result.Entity;
            // A source's EOF can reveal a pending entity in an owned replacement.
            // Finish its disposal before opening that next entity.
            if (resourceWork == ResourceWork.None) resourceWork = ResourceWork.OpenParameter;
        }
        return true;
    }

    private void AbortOrDeferEntities()
    {
        if (!asyncResources) { AbortEntities(); return; }
        if (!asyncCleanupCompleted) resourceWork = ResourceWork.Cleanup;
    }

    private async ValueTask CleanupAsync(bool preservePrimary)
    {
        ExceptionDispatchInfo? first = null;
        if (preservePrimary)
        { try { if (resolution is not null) await resolution.AbortAsync().ConfigureAwait(false); } catch { } }
        if (asyncDtdDisposal is { } pending)
        {
            asyncDtdDisposal = null;
            try { await pending.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
        }
        if (asyncDtdSources is not null)
        {
            while (asyncDtdSources.Count != 0)
            {
                var source = asyncDtdSources[^1]; asyncDtdSources.RemoveAt(asyncDtdSources.Count - 1);
                try { await source.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
            }
            asyncDtdSources = null;
        }
        try { if (!ReferenceEquals(asyncDtdParser, dtdParser)) asyncDtdParser?.Dispose(); }
        catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
        finally { asyncDtdParser = null; asyncDtdPending = null; }
        if (entitySources is not null)
        {
            while (entitySources.Count != 0)
            {
                var source = entitySources[^1]; entitySources.RemoveAt(entitySources.Count - 1);
                try { if (source.External is not null) await source.External.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
                finally { budget.Release(Quota.EntityDepth, 1); }
            }
            entitySources = null;
        }
        try { dtdParser?.Dispose(); }
        catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
        finally { dtdParser = null; }
        try { if (resolution is not null) await resolution.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { first ??= ExceptionDispatchInfo.Capture(error); }
        finally { resolution = null; }
        entities = null; entityBudget = null;
        incompleteDefaults?.Clear(); incompleteDefaultByDeclaration?.Clear();
        attributeEntities = null; dtdAttributes = null; ids = null; internalSubset = null; internalModel = null;
        if (!preservePrimary) first?.Throw();
    }

    // Synchronous Append faults immediately. Its async owner must await this
    // cleanup before propagating a callback/cancellation error; cleanup on that
    // faulted path preserves the already-selected primary exception.
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Concurrent or reentrant core use.");
        try
        {
            if (disposed) return;
            disposed = true;
            try { await CleanupAsync(faulted || diagnostic is not null).ConfigureAwait(false); }
            finally
            {
                ClearUnresolvedReference(); token = []; tokenLocations = []; physicalStarts = [];
                attributePhysicalLocations = []; attributePhysicalOrigins = []; attributePhysicalIdentities = [];
                attributePhysicalIdentity = null; attributePhysicalSpelling = null; physicalTag.Clear(); attributes = [];
                names.Clear(); names = null!; bindings.Clear(); bindings.TrimExcess();
                budget.Release(Quota.Depth, frames.Count); frames.Clear(); frames.TrimExcess(); sinkIdentity = null;
                resourceWork = ResourceWork.None;
            }
        }
        finally { Volatile.Write(ref gate, 0); }
    }
}
