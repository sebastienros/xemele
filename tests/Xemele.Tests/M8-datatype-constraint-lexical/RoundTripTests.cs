using System.Globalization;
using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8ConstraintLexical.ConstraintTests;
namespace Xemele.Tests.M8ConstraintLexical;

public sealed class RoundTripTests
{
    [Fact]
    public void DeterministicFloatingBitPatternsRoundTripAndMeetCanonicalGrammar()
    {
        var r = new XmlDatatypeRegistry(); var e = new XmlFacetEvaluator(); var lib = r.Find(DatatypeTypes.XsdNamespace)!;
        ulong bits = 0xabcdef0123456789;
        for (int i = 0; i < 512; i++)
        {
            bits = unchecked(bits * 6364136223846793005 + 1442695040888963407);
            Check("double", BitConverter.Int64BitsToDouble((long)bits).ToString("R", CultureInfo.InvariantCulture));
            Check("float", BitConverter.Int32BitsToSingle((int)(bits >> 32)).ToString("R", CultureInfo.InvariantCulture));
        }
        void Check(string name, string lexical)
        {
            if (lexical == "Infinity") lexical = "INF"; else if (lexical == "-Infinity") lexical = "-INF";
            var type = r.FindType(DatatypeTypes.Name(name))!;
            Assert.True(DatatypeConstraintRepresentation.TryCreate(type, lexical, Context, r, e, Op(), out var rep, out _));
            Assert.True(lib.TryParse(type.Name, rep!.Lexical, Context, Op(), out var parsed, out _)); Assert.True(lib.ValueEquals(rep.DeclaredValue, parsed!, Op()));
            if (rep.Lexical is "INF" or "-INF" or "NaN") return;
            var canonical = rep.Lexical.AsSpan(); if (canonical[0] == '-') canonical = canonical[1..];
            Assert.Equal('.', canonical[1]); int exponent = canonical.IndexOf('E'); Assert.True(exponent >= 3);
            Assert.DoesNotContain('+', rep.Lexical); Assert.False(canonical[..exponent].EndsWith("00", StringComparison.Ordinal));
            Assert.True(canonical[0] != '0' || rep.Lexical == "0.0E0");
            var ex = canonical[(exponent + 1)..]; if (ex[0] == '-') ex = ex[1..]; Assert.True(ex.Length == 1 || ex[0] != '0');
        }
    }
    [Fact]
    public void EverySupportedOffsetPreservesDateAndCalendarValuesAcrossBceAndLeapBoundaries()
    {
        foreach (string date in new[] { "-0004-02-28", "-0001-12-31", "0001-01-01", "1900-03-01", "2000-03-01" })
        foreach (int offset in new[] { -840, -720, -719, -1, 0, 1, 719, 720, 721, 840 })
        {
            string zone = (offset < 0 ? "-" : "+") + (Math.Abs(offset) / 60).ToString("D2", CultureInfo.InvariantCulture) + ":" + (Math.Abs(offset) % 60).ToString("D2", CultureInfo.InvariantCulture);
            foreach (string type in new[] { "date", "dateTime" })
            {
                string authored = date + (type == "dateTime" ? "T00:00:00.12300" : "") + zone;
                var rep = Create(type, authored, out var r); var lib = r.Find(DatatypeTypes.XsdNamespace)!;
                Assert.True(lib.TryParse(DatatypeTypes.Name(type), rep.Lexical, Context, Op(), out var parsed, out _));
                Assert.True(lib.ValueEquals(rep.DeclaredValue, parsed!, Op()), authored + " => " + rep.Lexical);
            }
        }
    }
    [Fact]
    public void UnionFormattingDoesNotRetryMemberSelectionWhenCanonicalFormChangesPatterns()
    {
        var padded = Restrict("padded", "int", F(XmlFacetKind.Pattern, "0[0-9]"));
        var union = new XmlDatatypeDefinition(new("urn:constraint", "u"), XmlDatatypeVariety.Union, DatatypeTypes.Name("anySimpleType"), null, [padded.Name, DatatypeTypes.Name("decimal")], [], [], false, default);
        var r = new XmlDatatypeRegistry([padded, union], Op()); var e = new XmlFacetEvaluator();
        Assert.True(DatatypeConstraintRepresentation.TryCreate(union, "01", Context, r, e, Op(), out var rep, out _));
        Assert.Equal("1", rep!.Lexical); Assert.Equal(padded.Name, rep.DeclaredValue.SelectedMember!.TypeName);
        Assert.True(e.TryValidate(union, rep.Lexical, rep.Context, r, Op(), out var actual, out _));
        Assert.Equal(DatatypeTypes.Name("decimal"), actual!.SelectedMember!.TypeName);
        Assert.True(r.Find(DatatypeTypes.XsdNamespace)!.ValueEquals(rep.DeclaredValue, actual, Op()));
    }
    [Fact]
    public void EmptyListsAndQNameListsHaveOwnedOutputAndMatchingBorrowedTokens()
    {
        var list = new XmlDatatypeDefinition(new("urn:constraint", "qlist"), XmlDatatypeVariety.List, DatatypeTypes.Name("anySimpleType"), DatatypeTypes.Name("QName"), [], [], [], false, default);
        var r = new XmlDatatypeRegistry([list], Op()); var e = new XmlFacetEvaluator();
        foreach (string lexical in new[] { " \t ", " p:a\r\n b  p:c " })
        {
            Assert.True(DatatypeConstraintRepresentation.TryCreate(list, lexical, Context, r, e, Op(), out var rep, out _));
            Assert.Equal(lexical.Trim().Length == 0 ? "" : "p:a b p:c", rep!.Lexical);
            Assert.True(e.TryValidate(list, rep.Lexical, rep.Context, r, Op(), out var parsed, out _)); Assert.True(r.Find(DatatypeTypes.XsdNamespace)!.ValueEquals(rep.DeclaredValue, parsed!, Op()));
        }
        var refs = Create("IDREFS", " a\t b  c ", out _); Assert.Equal("a b c", refs.Lexical);
    }
}
