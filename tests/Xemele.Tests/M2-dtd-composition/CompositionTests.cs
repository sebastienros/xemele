using System.Text;
using Xemele.Parsing.Dtd;
using Xunit;
using Model = Xemele.Parsing.Dtd.DtdModel;
namespace Xemele.Tests;

public sealed class DtdCompositionTests
{
    private static readonly Uri Parent = new("https://fixture.invalid/parent.dtd"), Child = new("https://fixture.invalid/child.dtd"), Nested = new("https://fixture.invalid/nested.dtd");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly XmlLocation Origin = new(Parent, 1, 1, null);
    private static Model Prior(string text, Ledger? ledger = null)
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger ?? new(), Origin);
        Assert.Equal(DtdParseStep.Completed, p.Append(text, [], true, Ct).Step);
        return p.Model;
    }

    [Fact]
    public void SeedVisibilityIdentityOrderFlagsAndFirstWins()
    {
        var ledger = new Ledger();
        var prior = Prior("<!ENTITY % x '<!ELEMENT via EMPTY>'><!ENTITY g 'inside'><!ELEMENT root ANY><!ATTLIST root a (one|two) 'one'><!NOTATION n SYSTEM 'old'>", ledger);
        long before = ledger.Expanded; int names = ledger.Names;
        using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, Origin);
        p.SeedPriorModel(prior, Ct);
        Assert.Equal(before, ledger.Expanded); Assert.Equal(names, ledger.Names); Assert.Equal(0, ledger.Enters);
        p.SetFramedInitialSource(Origin);
        const string external = "%x;<!ENTITY % x 'wrong'><!ENTITY g 'outside'><!ELEMENT root EMPTY><!ATTLIST root a CDATA 'other'><!NOTATION n SYSTEM 'new'><!ATTLIST via b CDATA '&g;'>";
        Assert.Equal(DtdParseStep.Completed, p.Append(external, [], true, Ct).Step);
        Assert.Same(prior.Elements[0], p.Model.Elements[0]); Assert.Same(prior.Attributes[0], p.Model.Attributes[0]);
        Assert.Same(prior.GeneralEntities[0], p.Model.GeneralEntities[0]); Assert.Same(prior.ParameterEntities[0], p.Model.ParameterEntities[0]); Assert.Same(prior.Notations[0], p.Model.Notations[0]);
        Assert.Equal(new[] { "root", "via" }, p.Model.Elements.Select(e => e.Name));
        Assert.False(p.Model.Elements[0].IsExternal); Assert.True(p.Model.Elements[1].IsExternal);
        Assert.Equal(Origin, p.Model.Elements[0].Location);
        var applied = DtdValueProcessor.NormalizeAttributeDefault(p.Model.Attributes[1].DefaultValue!, DtdAttributeType.CData, p.Model.GeneralEntities.ToDictionary(e => e.Name), new(), Origin, cancellationToken: Ct);
        Assert.Null(applied.Diagnostic); Assert.Equal("inside", applied.Value);
        Assert.Equal(1, ledger.Enters); Assert.Equal(1, ledger.Exits);
    }

    [Fact]
    public void SeedDoesNotRechargeRetainedNamesOrReplacementWork()
    {
        var ledger = new Ledger(); var model = Prior("<!ELEMENT root (child)><!ATTLIST root a (one|two) #IMPLIED><!ENTITY e 'huge'><!NOTATION n SYSTEM 'n'>", ledger);
        int names = ledger.Names; long expanded = ledger.Expanded;
        ledger.MaxNames = names; ledger.MaxExpanded = expanded;
        using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, Origin);
        p.SeedPriorModel(model, Ct); p.SetFramedInitialSource(Origin);
        Assert.Equal(DtdParseStep.Completed, p.Append([], [], true, Ct).Step);
        Assert.Equal(names, ledger.Names); Assert.Equal(expanded, ledger.Expanded);
        ledger.MaxExpanded = long.MaxValue;
        using var duplicate = new DtdParser(DtdSubsetKind.External, new(), ledger, Origin);
        duplicate.SeedPriorModel(model, Ct); duplicate.SetFramedInitialSource(Origin);
        Assert.Equal(DtdParseStep.Completed, duplicate.Append("<!ELEMENT root (child)><!ATTLIST root a (one|two) #IMPLIED><!NOTATION n SYSTEM 'n'>", [], true, Ct).Step);
        Assert.Equal(names, ledger.Names);
    }

    [Fact]
    public void SeedAndInitialHooksRejectIllegalTimingAndRemainTransactional()
    {
        var prior = Prior("<!ENTITY % x '<!ELEMENT ok EMPTY>'>");
        using var p = new DtdParser(DtdSubsetKind.External, new(), Origin);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => p.SeedPriorModel(prior, cancel.Token));
        Assert.Throws<ArgumentNullException>(() => p.SeedPriorModel(null!, Ct));
        Assert.Throws<ArgumentException>(() => p.SetFramedInitialSource(new(Child, 1, 1, null)));
        Assert.Throws<ArgumentException>(() => p.SetFramedInitialSource(new(Parent, 1, 0, null)));
        p.SetFramedInitialSource(new(Parent, 4, 8, null)); p.SeedPriorModel(prior, Ct);
        Assert.Throws<InvalidOperationException>(() => p.SeedPriorModel(prior, Ct));
        Assert.Throws<InvalidOperationException>(() => p.SetFramedInitialSource(Origin));
        Assert.Equal(DtdParseStep.Completed, p.Append("%x;", [], true, Ct).Step);
        Assert.Equal("ok", p.Model.Elements[0].Name);
        using var internalParser = new DtdParser(DtdSubsetKind.Internal, new());
        Assert.Throws<InvalidOperationException>(() => internalParser.SeedPriorModel(prior, Ct));
        Assert.Throws<InvalidOperationException>(() => internalParser.SetFramedInitialSource(new(null, 1, 1, null)));
        using var empty = new DtdParser(DtdSubsetKind.External, new()); empty.Append([], [], false, Ct);
        Assert.Throws<InvalidOperationException>(() => empty.SeedPriorModel(prior, Ct));
        Assert.Throws<InvalidOperationException>(() => empty.SetFramedInitialSource(new(null, 1, 1, null)));
        using var invalid = new DtdParser(DtdSubsetKind.External, new());
        Assert.Throws<ArgumentException>(() => invalid.Append(" ", new long[] { -1 }, false, Ct));
        Assert.Throws<InvalidOperationException>(() => invalid.SeedPriorModel(prior, Ct));
        using var eof = new DtdParser(DtdSubsetKind.External, new()); eof.CompleteByteSource(3);
        Assert.Throws<InvalidOperationException>(() => eof.SeedPriorModel(prior, Ct));
        using var disposed = new DtdParser(DtdSubsetKind.External, new()); disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposed.SeedPriorModel(prior, Ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<?xml\r\nencoding='UTF-8'?>")]
    public void InitialFramingEverySplitHasActualCursorAbsoluteMappingsAndSinglePrefix(string prolog)
    {
        const string body = "<!--😀-->\r\n<!ELEMENT 中 EMPTY>";
        string source = prolog + body;
        for (int frameSplit = 0; frameSplit <= source.Length; frameSplit++)
        {
            var ledger = new Ledger(); var framed = Frame(source, frameSplit, Parent, ledger);
            Assert.Equal(body, framed.Body);
            for (int split = 0; split <= body.Length; split++)
            {
                using var p = new DtdParser(DtdSubsetKind.External, new(), Parent);
                p.SetFramedInitialSource(framed.At);
                var a = p.Append(body.AsSpan(0, split), framed.Maps.AsSpan(0, split), false, Ct);
                Assert.Equal(DtdParseStep.NeedInput, a.Step); Assert.Equal(split, a.Consumed);
                p.CompleteByteSource(framed.Eof);
                var b = p.Append(body.AsSpan(split), framed.Maps.AsSpan(split), true, Ct);
                Assert.Equal(DtdParseStep.Completed, b.Step); Assert.Equal(body.Length - split, b.Consumed);
                Assert.Equal(new XmlLocation(Parent, prolog.Length == 0 ? 2 : 3, 1, 3 + Encoding.UTF8.GetByteCount(prolog + "<!--😀-->\r\n")), p.Model.Elements[0].Location);
            }
            Assert.Equal(prolog.Length, ledger.Expanded); // CR and LF both source scalars.
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("<?xml\r\nencoding='UTF-8'?>")]
    public void NestedFramingEverySplitRestoresParentSuffixFinalMappingAndExactlyOneEntry(string prolog)
    {
        const string parent = "<!ENTITY % a SYSTEM 'a'>%a;\r\n<!ELEMENT parent ANY>";
        const string child = "<!--😀-->\r\n<!ENTITY % b SYSTEM 'b'>%b;\r\n<!ELEMENT child EMPTY>";
        string source = prolog + child;
        for (int split = 0; split <= source.Length; split++)
        {
            var ledger = new Ledger(); using var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(Parent, 7, 9, 100));
            long[] maps = Maps(parent, 100); p.CompleteByteSource(100 + Encoding.UTF8.GetByteCount(parent));
            var a = p.Append(parent, maps, true, Ct); Assert.Equal(DtdParseStep.NeedEntity, a.Step);
            var framed = Frame(source, split, Child, ledger);
            p.PushFramedEntity(Child, framed.At); p.CompleteByteSource(framed.Eof);
            var b = p.Append(framed.Body, framed.Maps, true, Ct); Assert.Equal(DtdParseStep.NeedEntity, b.Step);
            var nested = Frame(prolog + "<!--😀-->\r\n<!ELEMENT nested EMPTY>", split % (prolog.Length + 10), Nested, ledger);
            p.PushFramedEntity(Nested, nested.At); p.CompleteByteSource(nested.Eof);
            Assert.Equal(DtdParseStep.NeedInput, p.Append(nested.Body, nested.Maps, true, Ct).Step);
            Assert.Equal(DtdParseStep.NeedInput, p.Append(framed.Body.AsSpan(b.Consumed), framed.Maps.AsSpan(b.Consumed), true, Ct).Step);
            Assert.Equal(DtdParseStep.Completed, p.Append(parent.AsSpan(a.Consumed), maps.AsSpan(a.Consumed), true, Ct).Step);
            Assert.Equal(new[] { "nested", "child", "parent" }, p.Model.Elements.Select(e => e.Name));
            Assert.Equal(new XmlLocation(Parent, 8, 1, 100 + parent.IndexOf("<!ELEMENT parent", StringComparison.Ordinal)), p.Model.Elements[2].Location);
            Assert.Equal(Child, p.Model.Elements[1].Location.SourceUri); Assert.Equal(Nested, p.Model.Elements[0].Location.SourceUri);
            Assert.Equal(2, ledger.Enters); Assert.Equal(2, ledger.Exits); Assert.Equal(0, ledger.Depth);
            Assert.Equal(ScalarCount(source) + ScalarCount(prolog + "<!--😀-->\r\n<!ELEMENT nested EMPTY>"), ledger.Expanded);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SecondDeclarationIsRejectedAtActualBodyCursor(bool pushed)
    {
        var ledger = new Ledger(); using var p = new DtdParser(pushed ? DtdSubsetKind.Internal : DtdSubsetKind.External, new(), ledger, Origin);
        if (pushed) Assert.Equal(DtdParseStep.NeedEntity, p.Append("<!ENTITY % a SYSTEM 'a'>%a;", [], true, Ct).Step);
        Uri uri = pushed ? Child : Parent;
        var framed = Frame("<?xml\r\nencoding='UTF-8'?><?xml encoding='UTF-8'?>", 24, uri, ledger);
        if (pushed) p.PushFramedEntity(uri, framed.At); else p.SetFramedInitialSource(framed.At);
        p.CompleteByteSource(framed.Eof);
        var r = p.Append(framed.Body, framed.Maps, true, Ct);
        Assert.Equal(DtdParseStep.Rejected, r.Step); Assert.Equal("DTD.Syntax", r.Diagnostic!.Code);
        Assert.Equal(uri, r.Diagnostic.Location.SourceUri); Assert.Equal(2, r.Diagnostic.Location.LineNumber);
        Assert.Equal(pushed ? 1 : 0, ledger.Exits);
    }

    [Fact]
    public void InvalidPushDoesNotLosePendingRequestOrEnterAgainAndByteLowerBoundIsPreserved()
    {
        var ledger = new Ledger(); using var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger, Origin);
        const string parent = "<!ENTITY % a SYSTEM 'a'>%a;";
        Assert.Throws<InvalidOperationException>(() => p.PushFramedEntity(Child, new(Child, 1, 1, null)));
        Assert.Equal(DtdParseStep.NeedEntity, p.Append(parent, [], true, Ct).Step);
        Assert.Throws<ArgumentNullException>(() => p.PushFramedEntity(null!, new(Child, 1, 1, null)));
        Assert.Throws<ArgumentException>(() => p.PushFramedEntity(new("relative", UriKind.Relative), new(Child, 1, 1, null)));
        Assert.Throws<ArgumentException>(() => p.PushFramedEntity(Child, new(Nested, 1, 1, null)));
        Assert.Throws<ArgumentException>(() => p.PushFramedEntity(Child, new(Child, 0, 1, null)));
        Assert.Throws<ArgumentException>(() => p.PushFramedEntity(Child, new(Child, 1, 1, -1)));
        p.PushFramedEntity(Child, new(Child, 3, 5, 40));
        Assert.Equal(1, ledger.Enters); Assert.Equal(0, ledger.Exits);
        Assert.Throws<InvalidOperationException>(() => p.PushFramedEntity(Child, new(Child, 3, 5, 40)));
        Assert.Throws<ArgumentException>(() => p.Append(" ", new long[] { 39 }, false, Ct));
        p.CompleteByteSource(44); Assert.Equal(DtdParseStep.NeedInput, p.Append("<!", new long[] { 40, 41 }, false, Ct).Step);
        var failure = p.Append([], [], true, Ct); Assert.Equal(new XmlLocation(Child, 3, 7, 44), failure.Diagnostic!.Location);
        Assert.Equal(1, ledger.Exits);
        using var initial = new DtdParser(DtdSubsetKind.External, new(), new XmlLocation(Parent, 1, 1, 20));
        Assert.Throws<ArgumentException>(() => initial.SetFramedInitialSource(new(Parent, 2, 1, 19)));
        initial.SetFramedInitialSource(new(Parent, 2, 1, null));
        Assert.Throws<ArgumentException>(() => initial.Append(" ", new long[] { 19 }, false, Ct));
        initial.CompleteByteSource(20); Assert.Equal(DtdParseStep.Completed, initial.Append([], [], true, Ct).Step);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(40)]
    public void EmptyAndBomOnlySourcesUseComposerEof(int eof)
    {
        using var p = new DtdParser(DtdSubsetKind.External, new(), Parent);
        p.SetFramedInitialSource(new(Parent, 3, 4, null)); p.CompleteByteSource(eof);
        Assert.Equal(DtdParseStep.Completed, p.Append([], [], true, Ct).Step);
        using var bad = new DtdParser(DtdSubsetKind.External, new(), Parent);
        bad.SetFramedInitialSource(new(Parent, 3, 4, null)); bad.Append("<!", new long[] { eof, eof + 1 }, false, Ct); bad.CompleteByteSource(eof + 9);
        Assert.Equal(new XmlLocation(Parent, 3, 6, eof + 9), bad.Append([], [], true, Ct).Diagnostic!.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<?xml encoding='UTF-8'?>")]
    public void SharedAggregateExactAndOneOverAcrossFramerBodyAndSeed(string prolog)
    {
        string source = prolog + "<!ELEMENT child EMPTY>";
        for (int delta = 0; delta <= 1; delta++)
        {
            var ledger = new Ledger(); var prior = Prior("<!ELEMENT seed EMPTY>", ledger);
            ledger.MaxExpanded = ScalarCount(source) - delta;
            using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, Origin); p.SeedPriorModel(prior, Ct);
            var framed = Frame(source, source.Length / 2, Parent, ledger); p.SetFramedInitialSource(framed.At); p.CompleteByteSource(framed.Eof);
            var r = p.Append(framed.Body, framed.Maps, true, Ct);
            Assert.Equal(delta == 0 ? DtdParseStep.Completed : DtdParseStep.Rejected, r.Step);
            Assert.Equal(ledger.MaxExpanded, ledger.Expanded);
            if (delta == 1) Assert.Equal("XML.ResourceLimit", r.Diagnostic!.Code);
        }
    }

    [Fact]
    public void FramedCancellationAndCallbackFailureBalanceDepthAndPreservePrimaryException()
    {
        foreach (bool cancel in new[] { false, true })
        {
            var ledger = new Ledger(); using var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger, Origin);
            p.Append("<!ENTITY % a SYSTEM 'a'>%a;", [], true, Ct); p.PushFramedEntity(Child, new(Child, 3, 1, null));
            var primary = new IOException("primary"); ledger.Failure = cancel ? null : primary; ledger.ExitFailure = new IOException("cleanup");
            using var cts = new CancellationTokenSource(); if (cancel) cts.Cancel();
            if (cancel) Assert.Throws<OperationCanceledException>(() => p.Append(" ", [], true, cts.Token));
            else Assert.Same(primary, Assert.Throws<IOException>(() => p.Append(" ", [], true, Ct)));
            Assert.Equal(1, ledger.Enters); Assert.Equal(1, ledger.Exits); Assert.Equal(0, ledger.Depth);
            Assert.Throws<InvalidOperationException>(() => p.Append([], [], true, Ct));
        }
        var pendingLedger = new Ledger(); var pending = new DtdParser(DtdSubsetKind.Internal, new(), pendingLedger, Origin);
        pending.Append("<!ENTITY % a SYSTEM 'a'>%a;", [], true, Ct); pending.Dispose(); pending.Dispose();
        Assert.Equal(1, pendingLedger.Enters); Assert.Equal(1, pendingLedger.Exits);
    }

    [Fact]
    public void SeedDeepParticlesAndNotationTokensAndFailureLeavePriorImmutable()
    {
        DtdParticle particle = new(DtdParticleKind.Name, "leaf", DtdOccurrence.Once, []);
        for (int i = 0; i < 12000; i++) particle = new(DtdParticleKind.Sequence, null, DtdOccurrence.Once, [particle]);
        var element = new DtdElementDeclaration("root", particle, false, Origin);
        var attribute = new DtdAttributeDeclaration("root", "a", DtdAttributeType.Notation, ["n"], DtdDefaultKind.Implied, null, false, Origin);
        var prior = new Model([element], [attribute], [], [], []);
        var ledger = new Ledger { MaxNames = 0 };
        using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, Origin);
        var broken = new Model([element, null!], [], [], [], []);
        Assert.Throws<NullReferenceException>(() => p.SeedPriorModel(broken, Ct));
        p.SeedPriorModel(prior, Ct); p.SetFramedInitialSource(Origin);
        Assert.Equal(DtdParseStep.Completed, p.Append("<!ELEMENT root (leaf)><!ATTLIST root a NOTATION (n) #IMPLIED>", [], true, Ct).Step);
        Assert.Same(element, Assert.Single(p.Model.Elements)); Assert.Same(attribute, Assert.Single(p.Model.Attributes));
        Assert.Same(element, Assert.Single(prior.Elements)); Assert.Equal(0, ledger.Names);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<?xml encoding='UTF-8'?>")]
    public void FramedLiteralPeHasNoPaddingAndQuotesRemainLiteral(string prolog)
    {
        var ledger = new Ledger(); using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, Origin);
        p.SetFramedInitialSource(Origin);
        const string parent = "<!ENTITY % a SYSTEM 'a'><!ENTITY e 'x%a;y'>";
        var r = p.Append(parent, [], true, Ct); Assert.Equal(DtdParseStep.NeedEntity, r.Step);
        var framed = Frame(prolog + "'&#9;", 3, Child, ledger);
        p.PushFramedEntity(Child, framed.At); p.CompleteByteSource(framed.Eof);
        Assert.Equal(DtdParseStep.NeedInput, p.Append(framed.Body, framed.Maps, true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, p.Append(parent.AsSpan(r.Consumed), [], true, Ct).Step);
        Assert.Equal("x'\ty", Assert.Single(p.Model.GeneralEntities).ReplacementText);
        Assert.Equal(1, ledger.Enters); Assert.Equal(1, ledger.Exits);
    }

    [Fact]
    public void BorrowedChunksAreReleasedBeforePendingAndCarryResumption()
    {
        var ledger = new Ledger(); using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, Origin);
        p.SetFramedInitialSource(new(Parent, 3, 7, 20));
        char[] source = "<!ENTITY % a SYSTEM 'a'>%a;<!ELEMENT parent EMPTY>".ToCharArray();
        long[] maps = Maps(new string(source), 20); p.CompleteByteSource(20 + source.Length);
        var r = p.Append(source, maps, true, Ct); Assert.Equal(DtdParseStep.NeedEntity, r.Step);
        string saved = new(source[r.Consumed..]); long[] suffixMaps = maps[r.Consumed..];
        Array.Fill(source, 'X'); Array.Fill(maps, -1L);
        p.PushFramedEntity(Child, new(Child, 8, 1, 30));
        char[] chunk = "<!--\uD83D".ToCharArray(); long[] chunkMaps = [30, 31, 32, 33, 34];
        Assert.Equal(DtdParseStep.NeedInput, p.Append(chunk, chunkMaps, false, Ct).Step);
        Array.Fill(chunk, 'X'); Array.Fill(chunkMaps, -1L);
        p.CompleteByteSource(50);
        Assert.Equal(DtdParseStep.NeedInput, p.Append("\uDE00-->", new long[] { 34, 38, 39, 40 }, true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, p.Append(saved, suffixMaps, true, Ct).Step);
        Assert.Equal("parent", Assert.Single(p.Model.Elements).Name); Assert.Equal(Parent, p.Model.Elements[0].Location.SourceUri);
        Assert.Equal(1, ledger.Exits);
    }

    internal static long[] Maps(string text, long start = 3)
    {
        long[] result = new long[text.Length]; long offset = start;
        for (int i = 0; i < text.Length; i++)
        { result[i] = offset; int length = char.IsHighSurrogate(text[i]) ? 2 : 1; if (length == 2) result[++i] = offset; offset += Encoding.UTF8.GetByteCount(text.AsSpan(i - length + 1, length)); }
        return result;
    }
    private static int ScalarCount(string text) => text.EnumerateRunes().Count();
    internal readonly record struct Framed(string Body, long[] Maps, XmlLocation At, long Eof);
    internal static Framed Frame(string source, int split, Uri uri, Ledger ledger)
    {
        using var f = new XmlTextDeclarationFramer(new(), ledger, uri);
        long[] maps = Maps(source); int consumed = 0;
        var r = f.Append(source.AsSpan(0, split), maps.AsSpan(0, split), false, Ct); consumed += r.Consumed;
        if (r.Step == TextDeclarationStep.NeedInput) { r = f.Append(source.AsSpan(split), maps.AsSpan(split), true, Ct); consumed += r.Consumed; }
        Assert.NotEqual(TextDeclarationStep.Rejected, r.Step); Assert.NotEqual(TextDeclarationStep.NeedInput, r.Step);
        XmlLocation at; string body; long[] bodyMaps;
        if (r.Step == TextDeclarationStep.Declared) { at = r.NextLocation; body = source[consumed..]; bodyMaps = maps[consumed..]; }
        else
        {
            at = f.PrefixLocation; Span<char> prefix = stackalloc char[7]; Span<long> offsets = stackalloc long[7];
            int count = f.DrainPrefix(prefix, offsets); Assert.Equal(source.AsSpan(0, consumed).ToString(), prefix[..count].ToString());
            Assert.Equal(maps.AsSpan(0, consumed).ToArray(), offsets[..count].ToArray());
            Assert.Throws<InvalidOperationException>(() => f.DrainPrefix(new char[7], new long[7]));
            body = prefix[..count].ToString() + source[consumed..]; bodyMaps = maps;
        }
        return new(body, bodyMaps, at, 3 + Encoding.UTF8.GetByteCount(source));
    }
    internal sealed class Ledger : IDtdBudget
    {
        internal long Expanded, MaxExpanded = long.MaxValue; internal int Names, MaxNames = int.MaxValue, Enters, Exits, Depth;
        internal Exception? Failure, ExitFailure;
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation at)
        { if (Failure is not null) throw Failure; if (count > MaxExpanded - Expanded) return ValueBudget.Limit(at); Expanded += count; return null; }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation at)
        { if (Names >= MaxNames) return ValueBudget.Limit(at); Names++; return null; }
        public XmlDiagnostic? EnterEntity(XmlLocation at) { Enters++; Depth++; return null; }
        public void ExitEntity() { Exits++; Depth--; if (ExitFailure is not null) throw ExitFailure; }
    }
}
