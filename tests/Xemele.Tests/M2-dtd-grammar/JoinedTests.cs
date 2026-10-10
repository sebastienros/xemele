using Xemele.Parsing.Dtd;
using Xunit;
namespace Xemele.Tests;
public sealed class DtdGrammarJoinedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("<!ENTITY e SYSTEM 'denied'>")]
    [InlineData("<!ENTITY e '<'>")]
    [InlineData("<!ENTITY e '&e;'>")]
    [InlineData("<!ENTITY e '&#38;#0;'>")]
    [InlineData("<!ENTITY e '&#38;1bad;'>")]
    public void EffectiveInternalOverrideAndNegativeEffectiveTableAtEverySplit(string declaration)
    {
        string source = declaration + "<!ATTLIST r a NMTOKENS ' &e;  z '><!ATTLIST r a CDATA 'ignored'>";
        for (int split = 0; split <= source.Length; split++)
        {
            using var external = new DtdParser(DtdSubsetKind.External, new());
            Assert.Equal(DtdParseStep.NeedInput, external.Append(source.AsSpan(0, split), [], false, Ct).Step);
            Assert.Equal(DtdParseStep.Completed, external.Append(source.AsSpan(split), [], true, Ct).Step);
            using var internalParser = new DtdParser(DtdSubsetKind.Internal, new());
            Assert.Equal(DtdParseStep.Completed, internalParser.Append("<!ENTITY e '中😀'><!ENTITY e 'ignored'>", [], true, Ct).Step);
            var table = internalParser.Model.GeneralEntities.Concat(external.Model.GeneralEntities).DistinctBy(e => e.Name, StringComparer.Ordinal).ToDictionary(e => e.Name, StringComparer.Ordinal);
            var attribute = Assert.Single(external.Model.Attributes); Assert.Equal(" &e;  z ", attribute.DefaultValue);
            var value = DtdValueProcessor.NormalizeAttributeDefault(attribute.DefaultValue!, attribute.Type, table, new(), attribute.Location, null, Ct);
            Assert.Null(value.Diagnostic); Assert.Equal("中😀 z", value.Value); Assert.False(table["e"].IsExternal);
            var negative = DtdValueProcessor.NormalizeAttributeDefault(attribute.DefaultValue!, attribute.Type,
                external.Model.GeneralEntities.ToDictionary(e => e.Name, StringComparer.Ordinal), new(), attribute.Location, null, Ct);
            Assert.Null(negative.Value); Assert.NotNull(negative.Diagnostic);
        }
    }
    [Fact]
    public void FramerDeclaredAndAbsentHandoffEverySplitAbsoluteScalarMappingsAndQuota()
    {
        foreach (string prolog in new[] { "", "<?xml\r\nencoding='UTF-8'?>" })
        {
            const string body = "<!--😀-->\r\n<!ELEMENT 中 (a,b?)><!ATTLIST 中 x CDATA 'raw'>";
            string text = prolog + body; long[] maps = Maps(text, 3); long eof = maps[^1] + 1;
            int scalars = text.EnumerateRunes().Count();
            for (int split = 0; split <= text.Length; split++)
            {
                var ledger = new Ledger { Maximum = scalars };
                var model = Framed(text, maps, eof, split, ledger);
                var element = Assert.Single(model.Elements); Assert.Equal("中", element.Name);
                Assert.Equal(new XmlLocation(new Uri("https://join.invalid/external.dtd"), prolog.Length == 0 ? 2 : 3, 1,
                    maps[text.IndexOf("<!ELEMENT", StringComparison.Ordinal)]), element.Location);
                Assert.Equal(DtdParticleKind.Sequence, element.Content.Kind); Assert.Equal(DtdOccurrence.Optional, element.Content.Children[1].Occurrence);
                Assert.Equal(scalars, ledger.Expanded); Assert.Equal(0, ledger.Enters); Assert.Equal(0, ledger.Exits);
                // One aggregate ledger across framing and grammar refuses the final scalar.
                var over = new Ledger { Maximum = scalars - 1 };
                Assert.Throws<QuotaRejected>(() => Framed(text, maps, eof, split, over)); Assert.Equal(scalars - 1, over.Expanded);
            }
        }
    }
    private static Xemele.Parsing.Dtd.DtdModel Framed(string text, long[] maps, long eof, int split, Ledger ledger)
    {
        var uri = new Uri("https://join.invalid/external.dtd");
        using var f = new XmlTextDeclarationFramer(new(), ledger, uri);
        DtdParser? parser = null; int offset = 0;
        try
        {
            foreach (int end in new[] { split, text.Length })
            {
                if (parser is null)
                {
                    var r = f.Append(text.AsSpan(offset, end - offset), maps.AsSpan(offset, end - offset), end == text.Length, Ct); offset += r.Consumed;
                    if (r.Step == TextDeclarationStep.Rejected) throw new QuotaRejected();
                    if (r.Step == TextDeclarationStep.NeedInput) continue;
                    parser = new(DtdSubsetKind.External, new(), ledger, r.Step == TextDeclarationStep.Declared ? r.NextLocation : f.PrefixLocation);
                    if (r.Step == TextDeclarationStep.Absent)
                    {
                        char[] prefix = new char[7]; long[] prefixMaps = new long[7]; int n = f.DrainPrefix(prefix, prefixMaps);
                        var accepted = parser.Append(prefix.AsSpan(0, n), prefixMaps.AsSpan(0, n), false, Ct);
                        Array.Fill(prefix, '\0'); Array.Fill(prefixMaps, -1);
                        if (accepted.Step == DtdParseStep.Rejected) throw new QuotaRejected();
                    }
                }
                if (end == text.Length) parser.CompleteByteSource(eof);
                var done = parser.Append(text.AsSpan(offset, end - offset), maps.AsSpan(offset, end - offset), end == text.Length, Ct); offset += done.Consumed;
                if (done.Step == DtdParseStep.Rejected) throw new QuotaRejected();
                Assert.Equal(end == text.Length ? DtdParseStep.Completed : DtdParseStep.NeedInput, done.Step);
                if (done.Step == DtdParseStep.Completed) return parser.Model;
            }
            throw new Exception("Incomplete handoff.");
        }
        finally { parser?.Dispose(); }
    }
    private sealed class QuotaRejected : Exception;
    internal static long[] Maps(string text, long start)
    {
        var maps = new long[text.Length]; long at = start;
        for (int i = 0; i < text.Length; i++)
        {
            maps[i] = at;
            if (char.IsHighSurrogate(text[i])) { maps[++i] = at; at += 4; }
            else at += text[i] <= 127 ? 1 : text[i] <= 2047 ? 2 : 3;
        }
        return maps;
    }
    [Fact]
    public void PushedSourcesAndDefaultsShareExactAggregateLedgerAndBorrowedRelease()
    {
        const string parent = "<!ENTITY % p SYSTEM 'p'>%p;<!ELEMENT parent EMPTY>";
        const string child = "<?xml encoding='UTF-8'?><!ENTITY e '😀'><!ATTLIST parent a CDATA '&e;'>";
        int total = child.EnumerateRunes().Count() + 1;
        foreach (int maximum in new[] { total, total - 1 })
        for (int split = 0; split <= child.Length; split++)
        {
            var ledger = new Ledger { Maximum = maximum };
            using var p = new DtdParser(DtdSubsetKind.Internal, new() { DocumentCharacters = 1 }, ledger, new(null, 9, 7, 100));
            long[] parentMaps = Maps(parent, 100); p.CompleteByteSource(parentMaps[^1] + 1);
            var request = p.Append(parent, parentMaps, true, Ct); Assert.Equal(DtdParseStep.NeedEntity, request.Step);
            Assert.Equal(parent.IndexOf(';') + 1, request.Consumed); Assert.Equal(1, ledger.Enters);
            p.PushEntity(new("https://join.invalid/child"));
            char[] borrowed = child.ToCharArray(); long[] childMaps = Maps(child, 3); p.CompleteByteSource(childMaps[^1] + 1);
            Assert.Equal(DtdParseStep.NeedInput, p.Append(borrowed.AsSpan(0, split), childMaps.AsSpan(0, split), false, Ct).Step);
            Array.Fill(borrowed, '\0', 0, split); Array.Fill(childMaps, -1, 0, split);
            Assert.Equal(DtdParseStep.NeedInput, p.Append(borrowed.AsSpan(split), childMaps.AsSpan(split), true, Ct).Step);
            Array.Fill(borrowed, '\0'); Array.Fill(childMaps, -1);
            Assert.Equal(DtdParseStep.Completed, p.Append(parent.AsSpan(request.Consumed), parentMaps.AsSpan(request.Consumed), true, Ct).Step);
            Assert.Equal(child.EnumerateRunes().Count(), ledger.Expanded); Assert.Equal(ledger.Enters, ledger.Exits);
            var attribute = Assert.Single(p.Model.Attributes);
            var value = DtdValueProcessor.NormalizeAttributeDefault(attribute.DefaultValue!, attribute.Type,
                p.Model.GeneralEntities.ToDictionary(e => e.Name, StringComparer.Ordinal), new(), attribute.Location, ledger, Ct);
            Assert.Equal(maximum == total ? "😀" : null, value.Value); Assert.Equal(maximum != total, value.Diagnostic is not null);
            Assert.Equal(ledger.Enters, ledger.Exits); Assert.Equal(maximum, ledger.Expanded);
            Assert.Equal(new XmlLocation(null, 9, 7 + request.Consumed, parentMaps[request.Consumed]), Assert.Single(p.Model.Elements).Location);
        }
    }
    [Theory]
    [InlineData("cancel")]
    [InlineData("fault")]
    [InlineData("refuse")]
    public void NormalizerLedgerFailuresAfterParserCompositionBalanceAndPreservePrimary(string action)
    {
        var ledger = new Ledger();
        using var parser = new DtdParser(DtdSubsetKind.External, new(), ledger, new(null, 1, 1, null));
        Assert.Equal(DtdParseStep.Completed, parser.Append("<!ENTITY a '&b;'><!ENTITY b 'ok'><!ATTLIST r x CDATA '&a;'>", [], true, Ct).Step);
        var original = new InvalidOperationException("primary"); using var cts = new CancellationTokenSource();
        int priorEnters = ledger.Enters, priorExits = ledger.Exits;
        ledger.OnCharge = action == "cancel" ? () => cts.Cancel() : null;
        ledger.ChargeFault = action == "fault" ? original : null;
        ledger.ExitFault = action == "refuse" ? null : new InvalidOperationException("cleanup");
        ledger.Refuse = action == "refuse";
        void Apply() => DtdValueProcessor.NormalizeAttributeDefault(parser.Model.Attributes[0].DefaultValue!, DtdAttributeType.CData,
            parser.Model.GeneralEntities.ToDictionary(e => e.Name, StringComparer.Ordinal), new(), default, ledger, action == "cancel" ? cts.Token : Ct);
        if (action == "fault") Assert.Same(original, Assert.Throws<InvalidOperationException>(Apply));
        else if (action == "cancel") Assert.Throws<OperationCanceledException>(Apply);
        else Apply();
        Assert.Equal(ledger.Enters - priorEnters, ledger.Exits - priorExits);
        if (action == "refuse") Assert.Equal(priorEnters, ledger.Enters);
    }
    internal sealed class Ledger : IDtdBudget
    {
        internal long Maximum = long.MaxValue, Expanded; internal int Enters, Exits;
        internal bool Refuse; internal Exception? ChargeFault, ExitFault; internal Action? OnCharge;
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation at)
        {
            OnCharge?.Invoke(); if (ChargeFault is not null) throw ChargeFault;
            if (count > Maximum - Expanded) return Limit(at); Expanded += count; return null;
        }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation at) => null;
        public XmlDiagnostic? EnterEntity(XmlLocation at) { if (Refuse) return Limit(at); Enters++; return null; }
        public void ExitEntity() { Exits++; if (ExitFault is not null) throw ExitFault; }
        private static XmlDiagnostic Limit(XmlLocation at) => new("XML.ResourceLimit", XmlDiagnosticSeverity.Fatal, "Authored shared ledger refusal.", at);
    }
}
