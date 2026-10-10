using Xemele.Parsing.Dtd;
using Xunit;
namespace Xemele.Tests;
public sealed class DtdParserSourceTests
{
    private static readonly Uri Parent = new("https://fixture.invalid/document.xml"), Child = new("https://fixture.invalid/redirect.dtd"), Nested = new("https://fixture.invalid/nested.dtd");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("", "")]
    [InlineData("<!ELEMENT child EMPTY>", "<?xml encoding='UTF-8'?>")]
    [InlineData("<!ELEMENT child (#PCDATA)>", "")]
    public void EveryPushedSourceSplitAndParentFinalRestoration(string body, string prolog)
    {
        string child = prolog + body;
        for (int split = 0; split <= child.Length; split++)
        {
            using var p = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(Parent, 7, 11, null));
            const string parent = "<!ENTITY % ext SYSTEM 'external'>%ext;\r\n<!ELEMENT parent ANY>";
            var requested = p.Append(parent, [], true, Ct);
            Assert.Equal(DtdParseStep.NeedEntity, requested.Step); Assert.Equal(parent.IndexOf(';') + 1, requested.Consumed); Assert.Equal("ext", requested.Entity!.Value.Name);
            Assert.Equal(Parent, requested.Entity.Value.Location.SourceUri);
            Assert.Throws<InvalidOperationException>(() => p.Append([], [], true, Ct));
            p.PushEntity(Child);
            Assert.Throws<InvalidOperationException>(() => p.PushEntity(Nested));
            var first = p.Append(child.AsSpan(0, split), [], false, Ct); Assert.Equal(split, first.Consumed); Assert.Equal(DtdParseStep.NeedInput, first.Step);
            var last = p.Append(child.AsSpan(split), [], true, Ct); Assert.Equal(child.Length - split, last.Consumed); Assert.Equal(DtdParseStep.NeedInput, last.Step); Assert.Throws<InvalidOperationException>(() => _ = p.Model);
            Assert.Throws<InvalidOperationException>(() => p.Append([], [], false, Ct));
            var done = p.Append(parent.AsSpan(requested.Consumed), [], true, Ct); Assert.Equal(DtdParseStep.Completed, done.Step);
            var final = p.Model.Elements[^1]; Assert.Equal(Parent, final.Location.SourceUri); Assert.Equal(8, final.Location.LineNumber); Assert.Equal(1, final.Location.LinePosition); Assert.False(final.IsExternal);
            if (body.Length > 0) { Assert.Equal(Child, p.Model.Elements[0].Location.SourceUri); Assert.True(p.Model.Elements[0].IsExternal); }
        }
    }
    [Fact]
    public void NestedRequestAtFinalEofAndEmptyChildren()
    {
        var ledger = new Ledger(); using var p = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(Parent, 1, 1, null));
        const string parent = "<!ENTITY % a SYSTEM 'a'>%a;";
        Assert.Equal(DtdParseStep.NeedEntity, p.Append(parent, [], true, Ct).Step); p.PushEntity(Child);
        const string child = "<!ENTITY % b SYSTEM 'b'>%b;";
        Assert.Equal(DtdParseStep.NeedEntity, p.Append(child, [], true, Ct).Step); p.PushEntity(Nested);
        Assert.Equal(DtdParseStep.NeedInput, p.Append([], [], true, Ct).Step); Assert.Equal(1, ledger.Exits);
        Assert.Equal(DtdParseStep.NeedInput, p.Append([], [], true, Ct).Step); Assert.Equal(2, ledger.Exits);
        Assert.Equal(DtdParseStep.Completed, p.Append([], [], true, Ct).Step); Assert.Equal(2, ledger.Enters); Assert.Equal(2, ledger.Exits);
    }
    [Fact]
    public void ExternalRequestInsideInternalReplacementResumesWithoutCountingOwnedCharacters()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new());
        const string source = "<!ENTITY % a SYSTEM 'a'><!ENTITY % b '&#37;a;<!ELEMENT after EMPTY>'>%b;<!ELEMENT parent ANY>";
        var r = p.Append(source, [], true, Ct); Assert.Equal(DtdParseStep.NeedEntity, r.Step); Assert.Equal(source.IndexOf("%b;", StringComparison.Ordinal) + 3, r.Consumed);
        p.PushEntity(Child); Assert.Equal(DtdParseStep.NeedInput, p.Append("<!ELEMENT child EMPTY>", [], true, Ct).Step);
        var done = p.Append(source.AsSpan(r.Consumed), [], true, Ct); Assert.Equal(DtdParseStep.Completed, done.Step); Assert.Equal(source.Length - r.Consumed, done.Consumed);
        Assert.Equal(new[] { "child", "after", "parent" }, p.Model.Elements.Select(e => e.Name));
    }
    [Fact]
    public void ParentByteScopeAndInteriorEofRestore()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(Parent, 4, 9, 100));
        const string source = "<!ENTITY % a SYSTEM 'a'>%a;\r\n<!ELEMENT p EMPTY>";
        long[] maps = Enumerable.Range(100, source.Length).Select(i => (long)i).ToArray(); p.CompleteByteSource(100 + source.Length);
        var r = p.Append(source, maps, true, Ct); p.PushEntity(Child);
        const string body = "<!ELEMENT x EMPTY>"; long[] childMaps = Enumerable.Range(3, body.Length).Select(i => (long)i).ToArray(); p.CompleteByteSource(3 + body.Length);
        Assert.Equal(DtdParseStep.NeedInput, p.Append(body, childMaps, true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, p.Append(source.AsSpan(r.Consumed), maps.AsSpan(r.Consumed), true, Ct).Step);
        Assert.Equal(3, p.Model.Elements[0].Location.ByteOffset); Assert.Equal(Child, p.Model.Elements[0].Location.SourceUri);
        Assert.Equal(100 + source.IndexOf("<!ELEMENT p", StringComparison.Ordinal), p.Model.Elements[1].Location.ByteOffset);
        Assert.Equal(5, p.Model.Elements[1].Location.LineNumber); Assert.Equal(1, p.Model.Elements[1].Location.LinePosition);
    }
    [Fact]
    public void BomOnlyByteEntityAndUnknownInitialCursor()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(Parent, 0, 0, null));
        const string parent = "<!ENTITY % a SYSTEM 'a'>%a;<!ELEMENT r EMPTY>";
        var r = p.Append(parent, [], true, Ct); p.PushEntity(Child); p.CompleteByteSource(3);
        Assert.Equal(DtdParseStep.NeedInput, p.Append([], [], true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, p.Append(parent.AsSpan(r.Consumed), [], true, Ct).Step); Assert.Equal(0, p.Model.Elements[0].Location.LineNumber); Assert.Null(p.Model.Elements[0].Location.ByteOffset);
    }
    [Fact]
    public void MappingMisuseDoesNotConsumeAndSourceFaultIsTerminal()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(Parent, 2, 3, 40));
        Assert.Throws<ArgumentException>(() => p.Append(" ", new long[] { 39 }, false, Ct));
        Assert.Throws<ArgumentException>(() => p.Append("  ", new long[] { 40 }, false, Ct));
        Assert.Equal(DtdParseStep.NeedInput, p.Append(" ", new long[] { 40 }, false, Ct).Step);
        Assert.Throws<ArgumentException>(() => p.Append(" ", [], false, Ct));
        Assert.Throws<InvalidOperationException>(() => p.Append([], [], true, Ct));
        p.CompleteByteSource(55); Assert.Throws<InvalidOperationException>(() => p.CompleteByteSource(55));
        Assert.Equal(DtdParseStep.Completed, p.Append([], [], true, Ct).Step);
        using var pair = new DtdParser(DtdSubsetKind.Internal, new());
        pair.Append("<!--\uD83D", new long[] { 0, 1, 2, 3, 4 }, false, Ct);
        Assert.Throws<ArgumentException>(() => pair.Append("\uDE00", new long[] { 5 }, false, Ct));
        pair.CompleteByteSource(10); Assert.Equal(DtdParseStep.Completed, pair.Append("\uDE00-->", new long[] { 4, 6, 7, 8 }, true, Ct).Step);
    }
    [Theory]
    [InlineData("<!ELEMENT child", " EMPTY>")]
    [InlineData("<!--", "-->")]
    [InlineData("<?p", "?>")]
    [InlineData("<![INCLUDE[", "]]>")]
    [InlineData("<![IGNORE[", "]]>")]
    public void ChildCannotLeaveOpenMarkup(string child, string suffix)
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new());
        var r = p.Append("<!ENTITY % a SYSTEM 'a'>%a;" + suffix, [], true, Ct); Assert.Equal(DtdParseStep.NeedEntity, r.Step); p.PushEntity(Child);
        Assert.Equal(DtdParseStep.Rejected, p.Append(child, [], true, Ct).Step);
    }
    [Theory]
    [InlineData("<!ELEMENT r ", "EMPTY>")]
    [InlineData("<!ELEMENT r (", "a)>")]
    public void ChildCannotCloseParentMarkup(string prefix, string child)
    {
        for (int split = 0; split <= child.Length; split++)
        {
            var ledger = new Ledger(); using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, new(Parent, 1, 1, null));
            const string entity = "<!ENTITY % a SYSTEM 'a'>";
            var r = p.Append(entity + prefix + "%a;", [], true, Ct); Assert.Equal(DtdParseStep.NeedEntity, r.Step); p.PushEntity(Child);
            Assert.Equal(DtdParseStep.NeedInput, p.Append(child.AsSpan(0, split), [], false, Ct).Step);
            Assert.Equal(DtdParseStep.NeedInput, p.Append(child.AsSpan(split), [], true, Ct).Step);
            Assert.Equal(DtdParseStep.Completed, p.Append([], [], true, Ct).Step);
            Assert.Equal(1, ledger.Enters); Assert.Equal(1, ledger.Exits);
            var model = p.Model; Assert.Single(model.Elements); Assert.Single(model.ParameterEntities);
            Assert.Empty(model.GeneralEntities); Assert.Empty(model.Attributes); Assert.Empty(model.Notations);
            var element = model.Elements[0]; Assert.Equal("r", element.Name); Assert.True(element.IsExternal);
            Assert.Equal(new XmlLocation(Parent, 1, entity.Length + 1, null), element.Location);
            Assert.Equal(DtdOccurrence.Once, element.Content.Occurrence); Assert.Null(element.Content.Name);
            Assert.Equal(child == "EMPTY>" ? DtdParticleKind.Empty : DtdParticleKind.Sequence, element.Content.Kind);
            if (child == "EMPTY>") Assert.Empty(element.Content.Children);
            else { var name = Assert.Single(element.Content.Children); Assert.Equal(DtdParticleKind.Name, name.Kind); Assert.Equal("a", name.Name); Assert.Equal(DtdOccurrence.Once, name.Occurrence); Assert.Empty(name.Children); }
            var pe = model.ParameterEntities[0]; Assert.Equal("a", pe.Name); Assert.True(pe.IsParameter); Assert.True(pe.IsExternal);
            Assert.Null(pe.ReplacementText); Assert.Equal("a", pe.SystemId); Assert.Null(pe.PublicId); Assert.Null(pe.NotationName);
            Assert.Equal(new XmlLocation(Parent, 1, 1, null), pe.Location);
        }
    }
    [Fact]
    public void ExternalPeInReplacementLiteralUsesNoPaddingAndOwnQuotesAreData()
    {
        using var p = new DtdParser(DtdSubsetKind.External, new());
        const string source = "<!ENTITY % a SYSTEM 'a'><!ENTITY e 'x%a;y'>";
        var r = p.Append(source, [], true, Ct); Assert.Equal(DtdParseStep.NeedEntity, r.Step); p.PushEntity(Child);
        Assert.Equal(DtdParseStep.NeedInput, p.Append("<?xml encoding='UTF-8'?>'&#9;", [], true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, p.Append(source.AsSpan(r.Consumed), [], true, Ct).Step); Assert.Equal("x'\ty", p.Model.GeneralEntities[0].ReplacementText);
    }
    [Fact]
    public void ScalarLocationsCrlfAcrossChunksAndEof()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(Parent, 12, 8, null));
        Assert.Equal(DtdParseStep.NeedInput, p.Append("<!--😀-->\r", [], false, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, p.Append("\n<!ELEMENT 中 EMPTY>", [], true, Ct).Step);
        Assert.Equal(new XmlLocation(Parent, 13, 1, null), p.Model.Elements[0].Location);
        using var overflow = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(Parent, int.MaxValue, 1, null));
        Assert.Equal("XML.LocationLimit", overflow.Append("\n", [], true, Ct).Diagnostic!.Code);
        using var eof = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(Parent, 3, 9, 200)); eof.CompleteByteSource(220);
        var failure = eof.Append("<!", new long[] { 200, 201 }, true, Ct); Assert.Equal(new XmlLocation(Parent, 3, 11, 220), failure.Diagnostic!.Location);
    }
    internal sealed class Ledger : IDtdBudget
    {
        internal int Enters, Exits, Names; internal long Expanded;
        internal int RefuseEnterAt = int.MaxValue; internal long MaxExpanded = long.MaxValue;
        internal Exception? ChargeException, ExitException;
        internal Action? OnCharge, OnName;
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation at)
        { OnCharge?.Invoke(); if (ChargeException is not null) throw ChargeException; if (count > MaxExpanded - Expanded) return ValueBudget.Limit(at); Expanded += count; return null; }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation at) { OnName?.Invoke(); Names++; return null; }
        public XmlDiagnostic? EnterEntity(XmlLocation at) { if (Enters == RefuseEnterAt) return ValueBudget.Limit(at); Enters++; return null; }
        public void ExitEntity() { Exits++; if (ExitException is not null) throw ExitException; }
    }
}
