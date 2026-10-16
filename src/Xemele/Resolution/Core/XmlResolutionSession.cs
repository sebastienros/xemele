using System.Runtime.CompilerServices;
using Xemele.Internal.Operations;
[assembly: InternalsVisibleTo("Xemele.ResolverSample")]
namespace Xemele.Resolution.Core;

internal readonly record struct ResourceOpenResult(XmlResourceHandle? Resource, XmlDiagnostic? Diagnostic);
internal readonly record struct ResourceReadResult(int Count, XmlDiagnostic? Diagnostic);
internal readonly record struct ResourceRequestResult(XmlResourceRequest? Request, XmlDiagnostic? Diagnostic);

// One sequential operation owns one session, including all nested sources. The
// parser retains parent handles/cursors and passes the final SourceUri as the
// next referrer. No current URI, decoder or source buffer is stored globally.
internal sealed class XmlResolutionSession : IDisposable, IAsyncDisposable
{
    private readonly XmlResourceResolver? resolver;
    private readonly OperationBudget budget;
    private CancellationToken cancellation;
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private readonly HashSet<XmlResourceHandle> handles = [];
    private bool disposed, busy, faulted, draining;
    private XmlDiagnostic? terminal;
    internal long BytesRead
    {
        get; private set;
    }
    internal long ResourcesOpened => budget.Count(Quota.ExternalResources);
    internal XmlResolutionSession(XmlResourceResolver? resolver, XmlResourceLimits limits, CancellationToken cancellation)
        : this(resolver, new OperationBudget(new() { Limits = limits }, cancellation), cancellation) { }
    internal XmlResolutionSession(XmlResourceResolver? resolver, OperationBudget budget, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(budget);
        this.resolver = resolver;
        this.budget = budget;
        this.cancellation = cancellation;
        CheckCancellation();
    }

    // A paused owner binds its next call's exact token before resource work.
    // The independently constructed budget retains its own cancellation token.
    internal void BindCancellationToken(CancellationToken token)
    {
        Enter();
        try
        {
            cancellation = token;
            CheckCancellation();
        }
        catch { FaultAndDrain(); throw; }
        finally { busy = false; }
    }

    // Async owners must not synchronously dispose their live resource stack
    // when cancellation is observed while rebinding a paused operation.
    internal async ValueTask BindCancellationTokenAsync(CancellationToken token)
    {
        Enter();
        try
        {
            cancellation = token;
            CheckCancellation();
        }
        catch { await FaultAndDrainAsync().ConfigureAwait(false); throw; }
        finally { busy = false; }
    }

    internal static ResourceRequestResult CreateRequest(string systemId, string? publicId, Uri? referrer,
        XmlResourceKind kind, XmlLocation location)
    {
        ArgumentNullException.ThrowIfNull(systemId);
        if (referrer is { IsAbsoluteUri: false })
        {
            throw new ArgumentException("Absolute referrer required.", nameof(referrer));
        }

        if (!Uri.TryCreate(systemId, UriKind.RelativeOrAbsolute, out var uri) ||
            (!uri.IsAbsoluteUri && (referrer is null || !Uri.TryCreate(referrer, uri, out uri))))
        {
            return new(null, Denied(location, "External identifier has no absolute base URI."));
        }

        return new(new(uri, referrer, kind)
        {
            SystemId = systemId,
            PublicId = publicId
        }, null);
    }

    internal ResourceOpenResult Open(XmlResourceRequest request, XmlLocation location)
    {
        Enter();
        XmlResolvedResource? acquired = null;
        try
        {
            var rejection = BeforeOpen(request, location);
            if (rejection is not null)
            {
                return new(null, rejection);
            }

            acquired = resolver!.Resolve(request, cancellation);
            var result = Accept(acquired, location);
            if (result.Resource is not null)
            {
                acquired = null; // Ownership transferred to registered handle.
            }

            CheckCancellation();
            if (acquired is not null)
            {
                var rejected = acquired;
                acquired = null;
                rejected.Dispose();
            }
            return result;
        }
        catch
        {
            // Drain the entire source stack before unwinding the primary error.
            // Otherwise outer Dispose could replace it with a parent cleanup error.
            FaultAndDrain();
            try
            {
                acquired?.Dispose();
            }
            catch { }
            acquired = null;
            throw;
        }
        finally { busy = false; }
    }

    internal async ValueTask<ResourceOpenResult> OpenAsync(XmlResourceRequest request, XmlLocation location)
    {
        Enter();
        XmlResolvedResource? acquired = null;
        try
        {
            var rejection = BeforeOpen(request, location);
            if (rejection is not null)
            {
                return new(null, rejection);
            }

            acquired = await resolver!.ResolveAsync(request, cancellation).ConfigureAwait(false);
            var result = Accept(acquired, location);
            if (result.Resource is not null)
            {
                acquired = null;
            }

            CheckCancellation();
            if (acquired is not null)
            {
                var rejected = acquired;
                acquired = null;
                await rejected.DisposeAsync().ConfigureAwait(false);
            }
            return result;
        }
        catch
        {
            await FaultAndDrainAsync().ConfigureAwait(false);
            try
            {
                if (acquired is not null)
                {
                    await acquired.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch { }
            acquired = null;
            throw;
        }
        finally { busy = false; }
    }

    private XmlDiagnostic? BeforeOpen(XmlResourceRequest request, XmlLocation location)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Uri);
        if (!request.Uri.IsAbsoluteUri || request.Referrer is { IsAbsoluteUri: false } || !Enum.IsDefined(request.Kind))
        {
            throw new ArgumentException("Absolute request/referrer URI and valid resource kind required.", nameof(request));
        }

        CheckCancellation();
        if (terminal is not null)
        {
            return terminal;
        }

        long maximum = budget.Options.Limits.ExternalResources ?? int.MaxValue;
        if (ResourcesOpened >= maximum)
        {
            return Limit("XML.ExternalResourcesLimit", location, "External resource count exhausted.");
        }

        return resolver is null ? Denied(location, "No external resource resolver was supplied.") : null;
    }

    private ResourceOpenResult Accept(XmlResolvedResource? resource, XmlLocation location)
    {
        if (resource is null)
        {
            return new(null, Denied(location, "The external resource resolver refused the request."));
        }
        // Charge every returned success, including redirects subsequently denied.
        if (!budget.TryCharge(Quota.ExternalResources, 1))
        {
            return new(null, Limit("XML.ExternalResourcesLimit", location, "External resource count exhausted."));
        }

        if (!resource.SourceUri.IsAbsoluteUri || !resource.Content.CanRead)
        {
            return new(null, Denied(location, "The returned resource is not readable at an absolute source URI."));
        }

        var key = CycleKey(resource.SourceUri);
        if (!active.Add(key))
        {
            return new(null, Denied(location with
            {
                SourceUri = resource.SourceUri
            }, "An active external source URI forms a cycle.", "XML.ResourceCycle"));
        }

        var handle = new XmlResourceHandle(this, resource, key);
        handles.Add(handle);
        return new(handle, null);
    }

    private static string CycleKey(Uri uri)
    {
        // AbsoluteUri has URI escaping/canonicalization; fragment never identifies
        // a separate stream. Avoid allocating a substring for the common case.
        var absolute = uri.AbsoluteUri;
        var fragment = absolute.IndexOf('#');
        return fragment < 0 ? absolute : absolute[..fragment];
    }
    private void Enter()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted)
        {
            throw new InvalidOperationException("A faulted resolution session cannot resume.");
        }

        if (busy)
        {
            throw new InvalidOperationException("Resolution sessions are sequential and cannot be reentered.");
        }

        busy = true;
    }
    private void CheckCancellation()
    {
        cancellation.ThrowIfCancellationRequested();
        budget.CheckCancellation();
    }
    private static XmlDiagnostic Denied(XmlLocation location, string message, string code = "XML.ResourceDenied") =>
        new(code, XmlDiagnosticSeverity.Fatal, message, location);
    private XmlDiagnostic Limit(string code, XmlLocation location, string message) => terminal ??= Denied(location, message, code);

    internal ResourceReadResult Read(XmlResourceHandle handle, Span<byte> destination, XmlLocation location)
    {
        Enter();
        try
        {
            handle.EnsureOpen();
            CheckCancellation();
            if (terminal is not null)
            {
                return new(0, terminal);
            }

            if (destination.IsEmpty)
            {
                return default;
            }

            var allowance = ReadAllowance(destination.Length);
            var count = handle.Content.Read(destination[..allowance]);
            var result = Account(count, allowance, location with
            {
                SourceUri = handle.SourceUri
            });
            CheckCancellation();
            return result;
        }
        catch { FaultAndDrain(); throw; }
        finally { busy = false; }
    }
    internal async ValueTask<ResourceReadResult> ReadAsync(XmlResourceHandle handle, Memory<byte> destination, XmlLocation location)
    {
        Enter();
        try
        {
            handle.EnsureOpen();
            CheckCancellation();
            if (terminal is not null)
            {
                return new(0, terminal);
            }

            if (destination.IsEmpty)
            {
                return default;
            }

            var allowance = ReadAllowance(destination.Length);
            var count = await handle.Content.ReadAsync(destination[..allowance], cancellation).ConfigureAwait(false);
            var result = Account(count, allowance, location with
            {
                SourceUri = handle.SourceUri
            });
            CheckCancellation();
            return result;
        }
        catch { await FaultAndDrainAsync().ConfigureAwait(false); throw; }
        finally { busy = false; }
    }
    private int ReadAllowance(int requested)
    {
        var maximum = budget.Options.Limits.ExternalBytes ?? long.MaxValue;
        var remaining = maximum - budget.Count(Quota.ExternalBytes);
        return remaining == 0 ? 1 : (int)Math.Min(Math.Min(requested, 4096), remaining);
    }
    private ResourceReadResult Account(int count, int allowance, XmlLocation location)
    {
        if ((uint)count > (uint)allowance)
        {
            throw new IOException("Stream returned an invalid byte count.");
        }
        // Track even the single-byte EOF probe that makes this operation terminal;
        // OperationBudget itself deliberately never mutates rejected counters.
        if (count > long.MaxValue - BytesRead)
        {
            return new(0, Limit("XML.ExternalBytesLimit", location, "External byte counter capacity exhausted."));
        }

        BytesRead += count;
        if (!budget.TryCharge(Quota.ExternalBytes, count))
        {
            return new(0, Limit("XML.ExternalBytesLimit", location, "External byte allowance exhausted."));
        }

        return new(count, null);
    }
    internal void Release(XmlResourceHandle handle, string key)
    {
        handles.Remove(handle);
        active.Remove(key);
    }

    // Composition catches parser/callback errors outside resource methods and
    // calls Abort/AbortAsync before rethrowing, so outer cleanup is harmless.
    internal void Abort()
    {
        if (busy)
        {
            throw new InvalidOperationException("Cannot abort during resource work.");
        }

        FaultAndDrain();
    }
    internal ValueTask AbortAsync()
    {
        if (busy)
        {
            throw new InvalidOperationException("Cannot abort during resource work.");
        }

        return FaultAndDrainAsync();
    }
    internal void HandleDisposalFailed() => FaultAndDrain();
    internal ValueTask HandleDisposalFailedAsync() => FaultAndDrainAsync();
    private void FaultAndDrain()
    {
        faulted = true;
        // A failing parent's Dispose calls this hook too. The outer drain owns
        // the remaining set; do not recurse through a malicious source stack.
        if (draining)
        {
            return;
        }

        draining = true;
        try
        {
            while (handles.Count != 0)
            {
                try
                {
                    FirstHandle().Dispose();
                }
                catch { }
            }
        }
        finally { draining = false; }
    }
    private async ValueTask FaultAndDrainAsync()
    {
        faulted = true;
        if (draining)
        {
            return;
        }

        draining = true;
        try
        {
            while (handles.Count != 0)
            {
                try
                {
                    await FirstHandle().DisposeAsync().ConfigureAwait(false);
                }
                catch { }
            }
        }
        finally { draining = false; }
    }

    private XmlResourceHandle FirstHandle()
    {
        using var iterator = handles.GetEnumerator();
        iterator.MoveNext();
        return iterator.Current;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (busy)
        {
            throw new InvalidOperationException("Cannot dispose a session during resource work.");
        }

        disposed = true;
        Exception? failure = null;
        // Release removes each handle; continue cleanup even if a stream throws.
        while (handles.Count != 0)
        {
            var handle = FirstHandle();
            try
            {
                handle.Dispose();
            }
            catch (Exception error) { failure ??= error; }
        }
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        if (busy)
        {
            throw new InvalidOperationException("Cannot dispose a session during resource work.");
        }

        disposed = true;
        Exception? failure = null;
        while (handles.Count != 0)
        {
            var handle = FirstHandle();
            try
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception error) { failure ??= error; }
        }
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

internal sealed class XmlResourceHandle : IDisposable, IAsyncDisposable
{
    private readonly XmlResolutionSession session;
    private readonly XmlResolvedResource resource;
    private readonly string key;
    private bool disposed;
    internal XmlResourceHandle(XmlResolutionSession session, XmlResolvedResource resource, string key)
    {
        this.session = session;
        this.resource = resource;
        this.key = key;
    }
    internal Uri SourceUri => resource.SourceUri;
    internal Stream Content => resource.Content;
    internal ResourceReadResult Read(Span<byte> destination, XmlLocation location) => session.Read(this, destination, location);
    internal ValueTask<ResourceReadResult> ReadAsync(Memory<byte> destination, XmlLocation location) => session.ReadAsync(this, destination, location);
    internal void EnsureOpen() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        session.Release(this, key);
        try
        {
            resource.Dispose();
        }
        catch { session.HandleDisposalFailed(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        session.Release(this, key);
        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch { await session.HandleDisposalFailedAsync().ConfigureAwait(false); throw; }
    }
}
