using System.Text;
using Xunit;
namespace Xemele.Tests.M6XInclude;

public sealed class LedgerTests
{
    private const string Xi = "http://www.w3.org/2001/XInclude";
    private static XmlDocument Input(int count, bool fallback = false)
    {
        var b = new StringBuilder("<r xmlns:xi='" + Xi + "'>");
        for (var i = 0; i < count; i++)
        {
            b.Append(fallback ? "<xi:include href='a'><xi:fallback><ok/></xi:fallback></xi:include>" : "<xi:include href='a'/>");
        }

        return XmlDocument.Parse(b.Append("</r>").ToString(), cancellationToken: TestContext.Current.CancellationToken);
    }
    private sealed class Resolver(Dictionary<string, byte[]> sources, bool asyncOnly = false) : XmlResourceResolver
    {
        internal int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            if (asyncOnly)
            {
                throw new InvalidOperationException("sync entity acquisition");
            }

            return Acquire(request, cancellationToken);
        }
        private XmlResolvedResource? Acquire(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Calls++;
            return sources.TryGetValue(request.Uri.AbsolutePath, out var bytes) ? new(new MemoryStream(bytes, false), request.Uri) : null;
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            return Acquire(request, cancellationToken);
        }
    }
    private static XIncludeOptions Options(Resolver include, XmlResourceLimits limits, Resolver? entity = null) => new()
    {
        BaseUri = new("https://test/main"),
        Resolver = include,
        Limits = limits,
        ParseOptions = new()
        {
            DtdProcessing = XmlDtdProcessing.Parse,
            Resolver = entity
        },
        FixupBaseUris = false,
        FixupLanguage = false
    };
    [Theory]
    [InlineData(false, 16, true)]
    [InlineData(false, 15, false)]
    [InlineData(true, 16, true)]
    [InlineData(true, 15, false)]
    public async Task AggregateDocumentCharactersCountScalarsAcrossSiblingParses(bool asynchronous, int maximum, bool success)
    {
        var resolver = new Resolver(new()
        {
            ["/a"] = Encoding.UTF8.GetBytes("<a>😀</a>")
        }, asynchronous);
        var options = Options(resolver, new()
        {
            DocumentCharacters = maximum
        });
        var input = Input(2, true);
        var result = asynchronous ? await XIncludeProcessor.ProcessAsync(input, options, TestContext.Current.CancellationToken) : XIncludeProcessor.Process(input, options, TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        Assert.Equal(2, resolver.Calls);
        if (!success)
        {
            Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        }
    }
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void OwnedExpansionCountsUnicodeScalars(int includes, bool success)
    {
        var resolver = new Resolver(new()
        {
            ["/a"] = Encoding.UTF8.GetBytes("<a>😀</a>")
        });
        var result = XIncludeProcessor.Process(Input(includes), Options(resolver, new()
        {
            ExpandedCharacters = 1
        }), TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        if (!success)
        {
            Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        }
    }
    [Theory]
    [InlineData(false, 4, true)]
    [InlineData(false, 3, false)]
    [InlineData(true, 4, true)]
    [InlineData(true, 3, false)]
    public async Task IncludeAndEntityAcquisitionShareOneAggregateCount(bool asynchronous, int maximum, bool success)
    {
        var xml = "<!DOCTYPE a [<!ENTITY e SYSTEM 'entity'>]><a>&e;</a>"u8.ToArray();
        var inclusion = new Resolver(new()
        {
            ["/a"] = xml
        }, asynchronous);
        var entity = new Resolver(new()
        {
            ["/entity"] = "text"u8.ToArray()
        }, asynchronous);
        var options = Options(inclusion, new()
        {
            ExternalResources = maximum
        }, entity);
        var result = asynchronous ? await XIncludeProcessor.ProcessAsync(Input(2, true), options, TestContext.Current.CancellationToken) : XIncludeProcessor.Process(Input(2, true), options, TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        Assert.Equal(2, inclusion.Calls);
        Assert.Equal(success ? 2 : 1, entity.Calls);
        if (!success)
        {
            Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        }
    }
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    public async Task ExternalBytesAggregateEntityAndIncludeWithExactEofProbe(bool asynchronous, int delta)
    {
        var xml = "<!DOCTYPE a [<!ENTITY e SYSTEM 'entity'>]><a>&e;</a>"u8.ToArray();
        var inclusion = new Resolver(new()
        {
            ["/a"] = xml
        }, asynchronous);
        var entity = new Resolver(new()
        {
            ["/entity"] = "text"u8.ToArray()
        }, asynchronous);
        var options = Options(inclusion, new()
        {
            ExternalBytes = xml.Length + 4 + delta
        }, entity);
        var result = asynchronous ? await XIncludeProcessor.ProcessAsync(Input(1, true), options, TestContext.Current.CancellationToken) : XIncludeProcessor.Process(Input(1, true), options, TestContext.Current.CancellationToken);
        Assert.Equal(delta == 0, result.Success);
        if (delta != 0)
        {
            Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        }
    }
    [Theory]
    [InlineData(false, 16, true)]
    [InlineData(false, 15, false)]
    [InlineData(true, 16, true)]
    [InlineData(true, 15, false)]
    public async Task ExpandedEntityAndOwnedOutputShareLedgerAcrossSiblingParses(bool asynchronous, int maximum, bool success)
    {
        var include = new Resolver(new()
        {
            ["/a"] = "<!DOCTYPE a [<!ENTITY e SYSTEM 'entity'>]><a>&e;</a>"u8.ToArray()
        }, asynchronous);
        var entity = new Resolver(new()
        {
            ["/entity"] = "text"u8.ToArray()
        }, asynchronous);
        var options = Options(include, new()
        {
            ExpandedCharacters = maximum
        }, entity);
        var result = asynchronous ? await XIncludeProcessor.ProcessAsync(Input(2, true), options, TestContext.Current.CancellationToken) : XIncludeProcessor.Process(Input(2, true), options, TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        if (!success)
        {
            Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        }
    }
    [Theory]
    [InlineData(17, true)]
    [InlineData(16, false)]
    public void ExternalDtdAndEntityExpansionDoNotResetLedger(int maximum, bool success)
    {
        var include = new Resolver(new()
        {
            ["/a"] = "<!DOCTYPE a SYSTEM 'dtd'><a>&e;</a>"u8.ToArray()
        });
        var entity = new Resolver(new()
        {
            ["/dtd"] = "<!ENTITY e 'x'>"u8.ToArray()
        });
        var result = XIncludeProcessor.Process(Input(1, true), Options(include, new()
        {
            ExpandedCharacters = maximum
        }, entity), TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        if (!success)
        {
            Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        }
    }
    [Fact]
    public void SiblingParserDepthAndEntityDepthAreReleased()
    {
        var include = new Resolver(new()
        {
            ["/a"] = "<!DOCTYPE a [<!ENTITY e 'x'>]><a>&e;</a>"u8.ToArray()
        });
        var result = XIncludeProcessor.Process(Input(3), Options(include, new()
        {
            Depth = 2,
            EntityDepth = 1
        }), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal("xxx", result.Document!.RootElement.GetTextContent());
    }
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void ResultFixupAttributesApplyQuotaIncludingNamespaceDeclarations(int maximum, bool success)
    {
        var resolver = new Resolver(new()
        {
            ["/a"] = "<a x='1' y='2'/>"u8.ToArray()
        });
        var options = Options(resolver, new()
        {
            AttributesPerElement = maximum
        }) with
        {
            FixupBaseUris = true
        };
        var result = XIncludeProcessor.Process(Input(1), options, TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
        if (!success)
        {
            Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        }
    }
}
