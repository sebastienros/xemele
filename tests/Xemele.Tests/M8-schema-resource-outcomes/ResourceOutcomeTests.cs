using System.Text;
using Xemele.Validation;
using Xemele.Internal.Operations;
using Xemele.Resolution.Core;
using Xunit;

namespace Xemele.Tests.M8SchemaResourceOutcomes;

public sealed class ResourceOutcomeTests
{
    private static readonly Uri Source = new("https://source.test/main.xsd");
    private static readonly Uri Basis = new("https://base.test/elsewhere/");
    private static readonly XmlLocation At = new(Source, 7, 3, 41);
    private static SchemaResourceReference Reference(string href = "child.xsd") => new(href, Source, SchemaResourcePurpose.XsdInclude, At);
    private static bool Optional(SchemaCompileContext context, out XmlDocument? document) => context.Resources.TryLoadOptional(Reference(), Basis, context.Operation, out document);

    [Fact]
    public void DefaultFactoriesAndCopiesDoNotAcquireOrDisposeOwnership()
    {
        XmlResourceResolution empty = default;
        Assert.Equal(XmlResourceResolutionStatus.Denied, empty.Status);
        Assert.Null(empty.Resource);
        Assert.Equal(empty, XmlResourceResolution.Denied);
        Assert.Equal(XmlResourceResolutionStatus.Unavailable, XmlResourceResolution.Unavailable.Status);
        Assert.Null(XmlResourceResolution.Unavailable.Resource);
        Assert.Equal([0, 1, 2], Enum.GetValues<XmlResourceResolutionStatus>().Select(s => (int)s));
        Assert.Empty(typeof(XmlResourceResolution).GetConstructors());
        Assert.All(typeof(XmlResourceResolution).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(XmlResourceResolution)));
        Assert.False(typeof(IAsyncDisposable).IsAssignableFrom(typeof(XmlResourceResolution)));
        Assert.Equal("resource", Assert.Throws<ArgumentNullException>(() => XmlResourceResolution.FromResource(null!)).ParamName);
        var stream = new OwnedStream("<r/>");
        using var resource = new XmlResolvedResource(stream, Source);
        var result = XmlResourceResolution.FromResource(resource);
        var copy = result;
        Assert.Equal(XmlResourceResolutionStatus.Resolved, copy.Status);
        Assert.Same(resource, copy.Resource);
        Assert.Equal(0, stream.Disposals);
        resource.Dispose();
        resource.Dispose();
        Assert.Equal(1, stream.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultAdapterUsesIdenticalRequestTokenAndOneLegacyCallback(bool resolved)
    {
        using var cancellation = new CancellationTokenSource();
        using var resource = new XmlResolvedResource(new MemoryStream(), Source);
        var request = new XmlResourceRequest(Basis, Source, XmlResourceKind.Schema) { SystemId = "child.xsd", PublicId = "authored" };
        var resolver = new Legacy((seen, token) => { Assert.Same(request, seen); Assert.Equal(cancellation.Token, token); return resolved ? resource : null; });
        var result = resolver.ResolveResult(request, cancellation.Token);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(resolved ? XmlResourceResolutionStatus.Resolved : XmlResourceResolutionStatus.Denied, result.Status);
        Assert.Same(resolved ? resource : null, result.Resource);
    }

    [Fact]
    public void AdapterCancellationBeforeCallbackAndAfterAcquisitionLeaveOwnershipWithReceiver()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var resolver = new Legacy((_, _) => throw new Exception("not called"));
        Assert.Throws<OperationCanceledException>(() => resolver.ResolveResult(new(Source, null, XmlResourceKind.Schema), canceled.Token));
        Assert.Equal(0, resolver.Calls);
        using var later = new CancellationTokenSource();
        var stream = new OwnedStream("<r/>");
        using var resource = new XmlResolvedResource(stream, Source);
        var acquired = new Legacy((_, _) => { later.Cancel(); return resource; });
        Assert.Same(resource, acquired.ResolveResult(new(Source, null, XmlResourceKind.Schema), later.Token).Resource);
        Assert.Equal(0, stream.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyNullRefusesBothLoadSites(bool optional)
    {
        var resolver = new Legacy((_, _) => null);
        var context = new SchemaCompileContext(new()
        {
            Resolver = resolver
        });
        var error = Assert.Throws<SchemaCompileException>(() => Load(context, optional));
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Single(error.Diagnostics).Code);
        Assert.False(context.Operation.IsComplete);
        Assert.Equal(1, resolver.Calls);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void ExplicitOutcomesAtOptionalAndMandatorySites(int outcome, bool optional)
    {
        var stream = new OwnedStream("<r/>");
        var resource = new XmlResolvedResource(stream, Source);
        var resolver = new Explicit((_, _) => outcome switch { 1 => XmlResourceResolution.Unavailable, 2 => XmlResourceResolution.FromResource(resource), _ => default });
        var context = new SchemaCompileContext(new()
        {
            Resolver = resolver
        });
        if (outcome == 2)
        {
            Assert.NotNull(Load(context, optional));
            Assert.True(context.Operation.IsComplete);
            Assert.Empty(context.Operation.Diagnostics);
            Assert.Equal(1, stream.Disposals);
        }
        else if (optional && outcome == 1)
        {
            Assert.False(Optional(context, out var document));
            Assert.Null(document);
            Assert.True(context.Operation.IsComplete);
            Assert.Empty(context.Operation.Diagnostics);
            Assert.Equal(0, stream.Disposals);
        }
        else
        {
            var error = Assert.Throws<SchemaCompileException>(() => Load(context, optional));
            var diagnostic = Assert.Single(error.Diagnostics);
            Assert.Equal("SCHEMA.ResourceDenied", diagnostic.Code);
            Assert.Equal(At, diagnostic.Location);
            Assert.False(context.Operation.IsComplete);
            Assert.Equal(0, stream.Disposals);
        }
        Assert.Equal(1, resolver.Calls);
        resource.Dispose();
        Assert.Equal(1, stream.Disposals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingResolverInvalidBaseAndDifferentOperationCannotBecomeAbsence(int fault)
    {
        var resolver = new Explicit((_, _) => XmlResourceResolution.Unavailable);
        var context = new SchemaCompileContext(new()
        {
            Resolver = fault == 0 ? null : resolver
        });
        if (fault == 0)
        {
            Assert.Throws<SchemaCompileException>(() => Optional(context, out _));
        }

        if (fault == 1)
        {
            Assert.Throws<SchemaCompileException>(() => context.Resources.TryLoadOptional(Reference(), null, context.Operation, out _));
        }

        if (fault == 2)
        {
            Assert.Throws<ArgumentException>(() => context.Resources.TryLoadOptional(Reference(), Basis, new SchemaCompileOperation(), out _));
        }

        Assert.Equal(0, resolver.Calls);
        if (fault != 2)
        {
            Assert.False(context.Operation.IsComplete);
        }

        Assert.Throws<ArgumentException>(() => context.Resources.TryLoadOptional(Reference(), new("relative", UriKind.Relative), context.Operation, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallerExceptionsIncludingParseExceptionsPropagateThroughAdapterAndOptionalLoader(bool parse)
    {
        Exception sentinel = parse ? new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Error, "caller", At)]) : new IOException("caller");
        var legacy = new Legacy((_, _) => throw sentinel);
        Assert.Same(sentinel, Assert.ThrowsAny<Exception>(() => legacy.ResolveResult(new(Source, null, XmlResourceKind.Schema), TestContext.Current.CancellationToken)));
        foreach (var resolver in new XmlResourceResolver[] { legacy, new Explicit((_, _) => throw sentinel) })
        {
            var context = new SchemaCompileContext(new()
            {
                Resolver = resolver
            });
            Assert.Same(sentinel, Assert.ThrowsAny<Exception>(() => Optional(context, out _)));
            Assert.True(context.Operation.IsComplete);
            Assert.Empty(context.Operation.Diagnostics);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OptionalCancellationBeforeCallbackAfterUnavailableAndAfterAcquisition(int when)
    {
        using var cancel = new CancellationTokenSource();
        var stream = new OwnedStream("<r/>");
        var resolver = new Explicit((_, token) => { Assert.Equal(cancel.Token, token); cancel.Cancel(); return when == 1 ? XmlResourceResolution.Unavailable : XmlResourceResolution.FromResource(new(stream, Source)); });
        var context = new SchemaCompileContext(new()
        {
            Resolver = resolver,
            CancellationToken = cancel.Token
        });
        if (when == 0)
        {
            cancel.Cancel();
        }

        var error = Assert.Throws<OperationCanceledException>(() => Optional(context, out _));
        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(when == 0 ? 0 : 1, resolver.Calls);
        Assert.Equal(when == 2 ? 1 : 0, stream.Disposals);
    }

    [Fact]
    public void UnavailableCannotReturnAfterOperationRefusal()
    {
        var operation = new SchemaCompileOperation();
        var loader = new SchemaResourceLoader(new()
        {
            Resolver = new Explicit((_, _) => { operation.Refuse("CALLER.Stop", "stop", At); return XmlResourceResolution.Unavailable; })
        }, operation);
        var error = Assert.Throws<SchemaCompileException>(() => loader.TryLoadOptional(Reference(), Basis, operation, out _));
        Assert.Equal("CALLER.Stop", Assert.Single(error.Diagnostics).Code);
        Assert.False(operation.IsComplete);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ParseReadQuotaAndCancellationFailuresKeepPrimaryExceptionAndDisposeOnce(int failure)
    {
        using var cancel = new CancellationTokenSource();
        var sentinel = new XmlParseException([new("CALLER.Read", XmlDiagnosticSeverity.Error, "read", At)]);
        var stream = new OwnedStream(failure == 0 ? "<r>" : "<r/>") { DisposeFailure = new IOException("cleanup"), ReadFailure = failure == 1 ? sentinel : null, AfterRead = failure == 3 ? cancel.Cancel : null };
        var resource = new XmlResolvedResource(stream, Source);
        var context = new SchemaCompileContext(new()
        {
            Resolver = new Explicit((_, _) => XmlResourceResolution.FromResource(resource)),
            CancellationToken = cancel.Token,
            Limits = new()
            {
                ExternalBytes = failure == 2 ? 3 : 100
            }
        });
        var error = Assert.ThrowsAny<Exception>(() => Optional(context, out _));
        if (failure == 1)
        {
            Assert.Same(sentinel, error);
        }
        else if (failure == 3)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        }
        else
        {
            Assert.Equal(failure == 0 ? "SCHEMA.Syntax" : "SCHEMA.Limit", Assert.Single(Assert.IsType<SchemaCompileException>(error).Diagnostics).Code);
        }

        Assert.Equal(1, stream.Disposals);
        resource.Dispose();
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public void SuccessfulParsePropagatesDisposalFailureExactlyOnce()
    {
        var sentinel = new IOException("dispose");
        var stream = new OwnedStream("<r/>") { DisposeFailure = sentinel };
        var resource = new XmlResolvedResource(stream, Source);
        var context = new SchemaCompileContext(new()
        {
            Resolver = new Explicit((_, _) => XmlResourceResolution.FromResource(resource))
        });
        Assert.Same(sentinel, Assert.Throws<IOException>(() => Optional(context, out _)));
        Assert.Equal(1, stream.Disposals);
        resource.Dispose();
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public void UnreadableReturnRefusesAndDisposesOnce()
    {
        var stream = new OwnedStream("<r/>");
        var resource = new XmlResolvedResource(stream, Source);
        stream.Unreadable = true;
        var context = new SchemaCompileContext(new()
        {
            Resolver = new Explicit((_, _) => XmlResourceResolution.FromResource(resource))
        });
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Single(Assert.Throws<SchemaCompileException>(() => Optional(context, out _)).Diagnostics).Code);
        Assert.Equal(1, stream.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableAttemptsHaveExactSharedWorkBoundaryAndNoFreshBudget(bool evaluationLimit)
    {
        var resolver = new Explicit((_, _) => XmlResourceResolution.Unavailable);
        var context = new SchemaCompileContext(new()
        {
            Resolver = resolver,
            MaxOperations = evaluationLimit ? 100 : 2,
            Limits = new()
            {
                EvaluationWork = evaluationLimit ? 2 : 100,
                InclusionDepth = 1
            }
        });
        Assert.False(Optional(context, out _));
        Assert.Null(Load(context, true));
        Assert.Equal(0, context.Operation.RemainingWork);
        Assert.True(context.Operation.IsComplete);
        Assert.Equal("SCHEMA.Limit", Assert.Single(Assert.Throws<SchemaCompileException>(() => Optional(context, out _)).Diagnostics).Code);
        Assert.Equal(2, resolver.Calls);
        Assert.False(context.Operation.IsComplete);
        Assert.Throws<SchemaCompileException>(() => Optional(context, out _));
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public void AbsenceHasNoCacheDocumentResourceOrBytesAndAvailableFollowupUsesSameBudgets()
    {
        var calls = 0;
        var stream = new OwnedStream("<r/>");
        var resolver = new Explicit((_, _) => ++calls < 3 ? XmlResourceResolution.Unavailable : XmlResourceResolution.FromResource(new(stream, Source)));
        var context = new SchemaCompileContext(new()
        {
            Resolver = resolver,
            MaxOperations = 11,
            Limits = new()
            {
                ExternalResources = 1,
                ExternalBytes = 4,
                DocumentCharacters = 8,
                InclusionDepth = 1
            }
        });
        context.Parse("<p/>");
        Assert.False(Optional(context, out var absent));
        Assert.Null(absent);
        Assert.False(Optional(context, out absent));
        Assert.Null(absent);
        Assert.True(Optional(context, out var document));
        Assert.NotNull(document);
        Assert.Equal("r", document.RootElement.LocalName);
        Assert.Equal(1, stream.Disposals);
        Assert.Equal(0, context.Operation.RemainingWork);
        Assert.True(context.Operation.IsComplete);
        Assert.Equal(3, calls);
        Assert.Throws<SchemaCompileException>(() => Optional(context, out _));
        Assert.Equal(3, calls);
    }

    [Fact]
    public void OptionalAttemptAndParsingSucceedExactlyAtFiveWorkUnits()
    {
        foreach (var cap in new long[] { 5, 4 })
        {
            var stream = new OwnedStream("<r/>");
            var context = new SchemaCompileContext(new()
            {
                MaxOperations = cap,
                Resolver = new Explicit((_, _) => XmlResourceResolution.FromResource(new(stream, Source)))
            });
            if (cap == 5)
            {
                Assert.True(Optional(context, out _));
                Assert.True(context.Operation.IsComplete);
            }
            else
            {
                Assert.Equal("SCHEMA.Limit", Assert.Single(Assert.Throws<SchemaCompileException>(() => Optional(context, out _)).Diagnostics).Code);
            }

            Assert.Equal(0, context.Operation.RemainingWork);
            Assert.Equal(1, stream.Disposals);
        }
    }

    [Fact]
    public void ExhaustedResourceCapacityGuardsUnavailableCallback()
    {
        var resolver = new Explicit((_, _) => XmlResourceResolution.FromResource(new(new OwnedStream("<r/>"), Source)));
        var context = new SchemaCompileContext(new()
        {
            Resolver = resolver,
            Limits = new()
            {
                ExternalResources = 1
            }
        });
        Assert.True(Optional(context, out _));
        resolver.Callback = (_, _) => XmlResourceResolution.Unavailable;
        Assert.Equal("SCHEMA.Limit", Assert.Single(Assert.Throws<SchemaCompileException>(() => Optional(context, out _)).Diagnostics).Code);
        Assert.Equal(1, resolver.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AbsenceNeverResetsPrincipalExternalParsingAllowances(int quota)
    {
        const string xml = "<r>&amp;</r>";
        var limits = new XmlResourceLimits { DocumentCharacters = quota == 0 ? xml.Length * 2 : 100, ExpandedCharacters = quota == 1 ? 2 : 100, ExternalBytes = quota == 2 ? Encoding.UTF8.GetByteCount(xml) : 100 };
        var calls = 0;
        var streams = new List<OwnedStream>();
        var context = new SchemaCompileContext(new()
        {
            Limits = limits,
            Resolver = new Explicit((_, _) =>
        {
            if (++calls == 1)
            {
                return XmlResourceResolution.Unavailable;
            }

            var stream = new OwnedStream(xml);
            streams.Add(stream);
            return XmlResourceResolution.FromResource(new(stream, Source));
        })
        });
        context.Parse(xml);
        Assert.False(Optional(context, out _));
        Assert.True(Optional(context, out _));
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => Optional(context, out _)).Diagnostics[^1].Code);
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
        Assert.False(context.Operation.IsComplete);
    }

    [Fact]
    public void RedirectedSourceEffectiveBaseAndPrincipalExternalParsingRemainShared()
    {
        var final = new Uri("https://redirect.test/path/final.xsd");
        XmlResourceRequest? seen = null;
        var stream = new OwnedStream("<r xml:base='nested/'>根😀</r>");
        var context = new SchemaCompileContext(new()
        {
            BaseUri = Source,
            Resolver = new Explicit((request, _) => { seen = request; return XmlResourceResolution.FromResource(new(stream, final)); })
        });
        var principal = context.Parse("<p/>");
        Assert.Equal(Source, principal.SourceUri);
        Assert.True(Optional(context, out var document));
        Assert.NotNull(document);
        Assert.Equal(new Uri(Basis, "child.xsd"), seen!.Uri);
        Assert.Equal(Source, seen.Referrer);
        Assert.Equal(XmlResourceKind.Schema, seen.Kind);
        Assert.Equal("child.xsd", seen.SystemId);
        Assert.Null(seen.PublicId);
        Assert.Equal(final, context.Resources.SourceUri(document));
        Assert.Equal(final, context.Open(document).SourceUri);
        Assert.Equal(new Uri(final, "nested/"), context.Open(document).Context(document.RootElement).BaseUri);
        Assert.Equal("根😀", document.RootElement.GetTextContent());
        Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public void ReentrancyAndDepthGuardsRemainStrictAndDisposeNestedReturn()
    {
        SchemaCompileContext context = null!;
        var streams = new List<OwnedStream>();
        var resolver = new Explicit((_, _) =>
        {
            var stream = new OwnedStream("<r/>");
            streams.Add(stream);
            if (streams.Count == 1)
            {
                stream.AfterRead = () => Optional(context, out _);
            }

            return XmlResourceResolution.FromResource(new(stream, Source));
        });
        context = new(new()
        {
            Resolver = resolver
        });
        Assert.Equal("SCHEMA.ResourceCycle", Assert.Single(Assert.Throws<SchemaCompileException>(() => Optional(context, out _)).Diagnostics).Code);
        Assert.Equal(2, streams.Count);
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
        var depthResolver = new Explicit((_, _) => XmlResourceResolution.Unavailable);
        var depth = new SchemaCompileContext(new()
        {
            Resolver = depthResolver,
            Limits = new()
            {
                InclusionDepth = 1
            }
        });
        using (depth.Resources.EnterComposition(At))
        {
            Assert.Throws<SchemaCompileException>(() => Optional(depth, out _));
        }

        Assert.Equal(0, depthResolver.Calls);
    }

    [Fact]
    public void NonSchemaResolutionSessionStillCallsLegacyResolve()
    {
        var legacy = new LegacyOnly();
        using var session = new XmlResolutionSession(legacy, new OperationBudget(new(), default), default);
        var refusal = session.Open(new(Source, null, XmlResourceKind.ExternalEntity), At);
        Assert.Equal("XML.ResourceDenied", refusal.Diagnostic!.Code);
        Assert.Equal(1, legacy.Calls);
    }

    private static XmlDocument? Load(SchemaCompileContext context, bool optional)
    {
        if (optional)
        {
            Optional(context, out var document);
            return document;
        }
        return context.Resources.Load(Reference(), Basis, context.Operation);
    }
    private sealed class Legacy(Func<XmlResourceRequest, CancellationToken, XmlResolvedResource?> callback) : XmlResourceResolver
    {
        internal int Calls; public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            Calls++;
            return callback(request, token);
        }
    }
    private sealed class Explicit(Func<XmlResourceRequest, CancellationToken, XmlResourceResolution> callback) : XmlResourceResolver
    {
        internal int Calls; internal Func<XmlResourceRequest, CancellationToken, XmlResourceResolution> Callback = callback;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) => throw new Exception("Legacy callback unexpectedly used.");
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken token)
        {
            Calls++;
            return Callback(request, token);
        }
    }
    private sealed class LegacyOnly : XmlResourceResolver
    {
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            Calls++;
            return null;
        }
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken token) => throw new Exception("Non-schema consumer used new API.");
    }
    private sealed class OwnedStream(string text) : MemoryStream(Encoding.UTF8.GetBytes(text))
    {
        internal int Disposals; internal bool Unreadable; internal Exception? ReadFailure, DisposeFailure; internal Action? AfterRead;
        public override bool CanRead => !Unreadable && base.CanRead;
        public override int Read(Span<byte> buffer)
        {
            if (ReadFailure is not null)
            {
                throw ReadFailure;
            }

            var count = base.Read(buffer);
            var after = AfterRead;
            AfterRead = null;
            after?.Invoke();
            return count;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
                base.Dispose(disposing);
                if (DisposeFailure is not null)
                {
                    throw DisposeFailure;
                }
            }
        }
    }
}
