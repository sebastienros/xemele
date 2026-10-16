using Xemele.Parsing.Dtd;
using Xunit;
namespace Xemele.Tests;

public sealed class DtdParserBudgetTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public void RawPrimaryScalarQuotaAndSharedNoDoubleCharge()
    {
        const string source = "<!--😀\r\n-->";
        var scalars = source.EnumerateRunes().Count();
        using var exact = new DtdParser(DtdSubsetKind.Internal, new()
        {
            DocumentCharacters = scalars
        });
        Assert.Equal(DtdParseStep.Completed, exact.Append(source, [], true, Ct).Step);
        using var over = new DtdParser(DtdSubsetKind.Internal, new()
        {
            DocumentCharacters = scalars - 1
        });
        Assert.Equal("XML.ResourceLimit", over.Append(source, [], true, Ct).Diagnostic!.Code);
        var ledger = new DtdParserSourceTests.Ledger();
        using var shared = new DtdParser(DtdSubsetKind.Internal, new()
        {
            DocumentCharacters = 1
        }, ledger, new(null, 1, 1, null));
        Assert.Equal(DtdParseStep.Completed, shared.Append(source, [], true, Ct).Step);
        Assert.Equal(0, ledger.Expanded);
    }
    [Fact]
    public void NameQuotaDistinctAndRepeatedNamesChargedOnce()
    {
        const string source = "<!ELEMENT r (a)><!ATTLIST r a CDATA #IMPLIED><!ENTITY a 'x'><!ELEMENT r (a)>";
        using var exact = new DtdParser(DtdSubsetKind.Internal, new()
        {
            InternedNames = 2
        });
        Assert.Equal(DtdParseStep.Completed, exact.Append(source, [], true, Ct).Step);
        using var over = new DtdParser(DtdSubsetKind.Internal, new()
        {
            InternedNames = 1
        });
        Assert.Equal("XML.ResourceLimit", over.Append(source, [], true, Ct).Diagnostic!.Code);
        var ledger = new DtdParserSourceTests.Ledger();
        using var shared = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(null, 1, 1, null));
        shared.Append(source, [], true, Ct);
        Assert.Equal(2, ledger.Names);
    }
    [Fact]
    public void DeclarationNormalizedTokenQuotaExactAndOneOver()
    {
        const string source = "<!ENTITY e '😀\r\nx'>";
        var normalized = source.EnumerateRunes().Count() - 1;
        using var exact = new DtdParser(DtdSubsetKind.Internal, new()
        {
            TokenCharacters = normalized
        });
        Assert.Equal(DtdParseStep.Completed, exact.Append(source, [], true, Ct).Step);
        using var over = new DtdParser(DtdSubsetKind.Internal, new()
        {
            TokenCharacters = normalized - 1
        });
        Assert.Equal("XML.ResourceLimit", over.Append(source, [], true, Ct).Diagnostic!.Code);
    }
    [Fact]
    public void ExpansionQuotaRepeatedUsesAndDepthExactOneOver()
    {
        const string source = "<!ENTITY % p ' '>%p;%p;";
        using var exact = new DtdParser(DtdSubsetKind.Internal, new()
        {
            ExpandedCharacters = 2,
            EntityDepth = 1
        });
        Assert.Equal(DtdParseStep.Completed, exact.Append(source, [], true, Ct).Step);
        using var over = new DtdParser(DtdSubsetKind.Internal, new()
        {
            ExpandedCharacters = 1
        });
        Assert.Equal("XML.ResourceLimit", over.Append(source, [], true, Ct).Diagnostic!.Code);
        const string nested = "<!ENTITY % a '&#37;b;'><!ENTITY % b ' '>%a;";
        using var depthExact = new DtdParser(DtdSubsetKind.Internal, new()
        {
            EntityDepth = 2
        });
        Assert.Equal(DtdParseStep.Completed, depthExact.Append(nested, [], true, Ct).Step);
        using var depthOver = new DtdParser(DtdSubsetKind.Internal, new()
        {
            EntityDepth = 1
        });
        Assert.Equal("XML.ResourceLimit", depthOver.Append(nested, [], true, Ct).Diagnostic!.Code);
    }
    [Fact]
    public void ExternalAndTextDeclarationRawExpansionChargedOnceBeforeWork()
    {
        const string source = "<?xml encoding='UTF-8'?>\r\n<!ELEMENT r EMPTY>";
        var scalars = source.Length;
        var exactLedger = new DtdParserSourceTests.Ledger { MaxExpanded = scalars };
        using var exact = new DtdParser(DtdSubsetKind.External, new(), exactLedger, new(null, 1, 1, null));
        Assert.Equal(DtdParseStep.Completed, exact.Append(source, [], true, Ct).Step);
        Assert.Equal(scalars, exactLedger.Expanded);
        var overLedger = new DtdParserSourceTests.Ledger { MaxExpanded = scalars - 1 };
        using var over = new DtdParser(DtdSubsetKind.External, new(), overLedger, new(null, 1, 1, null));
        var refused = over.Append(source, [], true, Ct);
        Assert.Equal(DtdParseStep.Rejected, refused.Step);
        Assert.Equal(scalars - 1, refused.Consumed);
        Assert.Equal(scalars - 1, overLedger.Expanded);
        var early = new DtdParserSourceTests.Ledger { MaxExpanded = 6 };
        using var prolog = new DtdParser(DtdSubsetKind.External, new(), early, new(null, 1, 1, null));
        Assert.Equal(DtdParseStep.Rejected, prolog.Append("<?xml " + new string(' ', 10000), [], false, Ct).Step);
        Assert.Equal(6, early.Expanded);
    }
    [Theory]
    [InlineData("reject")]
    [InlineData("cancel")]
    [InlineData("dispose")]
    [InlineData("fault")]
    public void DrainEveryAcceptedEntryPreservesPrimaryException(string action)
    {
        var ledger = new DtdParserSourceTests.Ledger();
        var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(null, 1, 1, null));
        Assert.Equal(DtdParseStep.NeedEntity, p.Append("<!ENTITY % a SYSTEM 'a'>%a;", [], true, Ct).Step);
        p.PushEntity(new("https://fixture.invalid/a"));
        Assert.Equal(DtdParseStep.NeedEntity, p.Append("<!ENTITY % b SYSTEM 'b'>%b;", [], true, Ct).Step);
        p.PushEntity(new("https://fixture.invalid/b"));
        Assert.Equal(2, ledger.Enters);
        var cleanup = new InvalidOperationException("cleanup");
        ledger.ExitException = cleanup;
        if (action == "cancel")
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<OperationCanceledException>(() => p.Append([], [], true, cts.Token));
        }
        else if (action == "fault")
        {
            var original = new XmlParseException([new("caller", XmlDiagnosticSeverity.Fatal, "caller", default)]);
            ledger.ChargeException = original;
            Assert.Same(original, Assert.Throws<XmlParseException>(() => p.Append(" ", [], false, Ct)));
        }
        else if (action == "dispose")
        {
            Assert.Same(cleanup, Assert.Throws<InvalidOperationException>(() => p.Dispose()));
        }
        else
        {
            Assert.Same(cleanup, Assert.Throws<InvalidOperationException>(() => p.Append("!", [], true, Ct)));
        }

        Assert.Equal(2, ledger.Exits);
        p.Dispose();
        Assert.Equal(2, ledger.Exits);
        Assert.ThrowsAny<Exception>(() => p.Append([], [], true, Ct));
    }
    [Fact]
    public void RefusedEntryNoExitAndPendingDisposeBalance()
    {
        var refused = new DtdParserSourceTests.Ledger { RefuseEnterAt = 0 };
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), refused, new(null, 1, 1, null));
        Assert.Equal(DtdParseStep.Rejected, p.Append("<!ENTITY % p ' '>%p;", [], true, Ct).Step);
        Assert.Equal(0, refused.Exits);
        var ledger = new DtdParserSourceTests.Ledger();
        var pending = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(null, 1, 1, null));
        Assert.Equal(DtdParseStep.NeedEntity, pending.Append("<!ENTITY % p SYSTEM 'p'>%p;", [], true, Ct).Step);
        pending.Dispose();
        pending.Dispose();
        Assert.Equal(1, ledger.Exits);
    }
    [Fact]
    public void ReentrantLedgerFaultAndSnapshotLimits()
    {
        var ledger = new DtdParserSourceTests.Ledger();
        using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, new(null, 1, 1, null));
        ledger.OnCharge = () => p.Append([], [], false, Ct);
        Assert.Throws<InvalidOperationException>(() => p.Append(" ", [], false, Ct));
        Assert.Throws<InvalidOperationException>(() => p.Append([], [], true, Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DtdParser(DtdSubsetKind.Internal, new() { EntityDepth = 0 }));
        Assert.Throws<ArgumentException>(() => new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(null, 0, 1, null)));
    }
    [Theory]
    [InlineData("<!NOTATION n PUBLIC '", "'>")]
    [InlineData("<!ELEMENT r (#PCDATA|", ")*>")]
    public void CancellationDuringDeclarationValidationRemainsPrimary(string start, string end)
    {
        using var cts = new CancellationTokenSource();
        var ledger = new DtdParserSourceTests.Ledger();
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(null, 1, 1, null));
        // ChargeName is reached after the '>' starts declaration processing. Cancel
        // there to require the subsequent custom scan to observe cancellation.
        ledger.OnName = () => cts.Cancel();
        Assert.Equal(DtdParseStep.NeedInput, p.Append(start + new string('x', 10000) + end[..^1], [], false, cts.Token).Step);
        Assert.Throws<OperationCanceledException>(() => p.Append(end.AsSpan(end.Length - 1), [], true, cts.Token));
        Assert.Throws<InvalidOperationException>(() => p.Append([], [], true, Ct));
    }
    [Fact]
    public void DeepUnlimitedInternalStackSuspensionCancellationAndProvenance()
    {
        const int depth = 4100;
        var text = new System.Text.StringBuilder();
        text.Append("<!ENTITY % leaf SYSTEM 'leaf'>");
        for (var i = 0; i < depth; i++)
        {
            text.Append("<!ENTITY % p").Append(i).Append(" '&#37;").Append(i + 1 == depth ? "leaf" : "p" + (i + 1)).Append(";'>");
        }

        text.Append("%p0;");
        var ledger = new DtdParserSourceTests.Ledger();
        using var p = new DtdParser(DtdSubsetKind.Internal, new()
        {
            EntityDepth = null
        }, ledger, new(new Uri("https://fixture.invalid/parent"), 2, 8, null));
        Assert.Equal(DtdParseStep.NeedEntity, p.Append(text.ToString(), [], true, Ct).Step);
        // Numeric references in declarations are transient entries, already exited.
        Assert.Equal(depth + 1, ledger.Enters - ledger.Exits);
        p.PushEntity(new("https://fixture.invalid/final"));
        Assert.Equal(DtdParseStep.NeedInput, p.Append("<!ELEMENT child EMPTY>", [], true, Ct).Step);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => p.Append([], [], true, cts.Token));
        Assert.Equal(ledger.Enters, ledger.Exits);
        Assert.Throws<InvalidOperationException>(() => p.Append([], [], true, Ct));
    }
    [Fact]
    public void IgnoredBodyHasConstantScanStateAndCancellationBeforeInput()
    {
        using var p = new DtdParser(DtdSubsetKind.External, new()
        {
            DocumentCharacters = null,
            ExpandedCharacters = null,
            TokenCharacters = 8
        });
        Assert.Equal(DtdParseStep.NeedInput, p.Append("<![IGNORE[", [], false, Ct).Step);
        string body = new('x', 10000);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(body.Length, p.Append(body, [], false, Ct).Consumed);
        }

        Assert.Equal(DtdParseStep.Completed, p.Append("]]>", [], true, Ct).Step);
        using var cancel = new DtdParser(DtdSubsetKind.Internal, new());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => cancel.Append(" ", [], false, cts.Token));
        Assert.Throws<InvalidOperationException>(() => cancel.Append([], [], true, Ct));
    }
}
