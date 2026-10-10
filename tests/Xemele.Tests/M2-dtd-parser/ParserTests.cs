using Xemele.Parsing.Dtd;
using Xunit;
namespace Xemele.Tests;
public sealed class DtdParserTests
{
    // Authored from the XML productions, independently marked accept/reject.
    private static readonly (DtdSubsetKind Kind, string Text, bool Valid)[] Cases =
    [ (DtdSubsetKind.Internal, "", true),
      (DtdSubsetKind.Internal, " \r\n<!-- hello --><?p data?>", true),
      (DtdSubsetKind.Internal, "<!ELEMENT r EMPTY><!ELEMENT x ANY>", true),
      (DtdSubsetKind.Internal, "<!ELEMENT r (a,(b|c)+,d?)>", true),
      (DtdSubsetKind.Internal, "<!ELEMENT r (#PCDATA|a|b)*>", true),
      (DtdSubsetKind.Internal, "<!ELEMENT r (#PCDATA)>", true),
      (DtdSubsetKind.Internal, "<!ATTLIST r a CDATA #IMPLIED b ID #REQUIRED c IDREF #FIXED 'x' d IDREFS 'x y' e ENTITY 'x' f ENTITIES 'x y' g NMTOKEN '2' h NMTOKENS 'x y' i (a|2b|中) 'a' j NOTATION (x|y) #IMPLIED>", true),
      (DtdSubsetKind.Internal, "<!ENTITY e 'a&#13;&later;'><!ENTITY later 'b'><!ENTITY % p '<!ELEMENT r EMPTY>'>%p;", true),
      (DtdSubsetKind.Internal, "<!ENTITY x SYSTEM 'x'><!ENTITY y PUBLIC 'some id' 'y' NDATA n><!NOTATION n PUBLIC 'pub'>", true),
      (DtdSubsetKind.Internal, "<!ENTITY amp '&#38;#38;'><!ENTITY lt '&#38;#60;'><!ENTITY gt '&#62;'><!ENTITY apos '&#39;'><!ENTITY quot '&#34;'>", true),
      (DtdSubsetKind.Internal, "<!ENTITY % x '<!ENTITY same &quot;no&quot;>'>", true),
      (DtdSubsetKind.External, "<?xml version='1.0' encoding='UTF-8'?><!ELEMENT r EMPTY>", true),
      (DtdSubsetKind.External, "<?xml encoding='UTF-8'?><![INCLUDE[<!ELEMENT r ANY><![INCLUDE[<!ENTITY e 'x'>]]>]]>", true),
      (DtdSubsetKind.External, "<![IGNORE[ garbage %missing; <!' \uD83D\uDE00 <![ x ]]> ]]> <!ELEMENT r ANY>", true),
      (DtdSubsetKind.External, "<!ENTITY % model '(a|b)*'><!ELEMENT r %model;>", true),
      (DtdSubsetKind.External, "<!ENTITY % p 'xy'><!ENTITY e 'a%p;b'>", true),
      (DtdSubsetKind.External, "<!ENTITY % mode 'INCLUDE'><![%mode;[<!ELEMENT r EMPTY>]]>", true),
      (DtdSubsetKind.Internal, "<!ELEMENT r (a|b,c)>", false),
      (DtdSubsetKind.Internal, "<!ELEMENT r ()>", false),
      (DtdSubsetKind.Internal, "<!ELEMENT r (a,)>", false),
      (DtdSubsetKind.Internal, "<!ELEMENT r (#PCDATA|a)>", false),
      (DtdSubsetKind.Internal, "<!ELEMENT r (#PCDATA)+>", false),
      (DtdSubsetKind.Internal, "<!ELEMENT 1r EMPTY>", false),
      (DtdSubsetKind.Internal, "<!ATTLIST r a () #IMPLIED>", false),
      (DtdSubsetKind.Internal, "<!ATTLIST r a NOTATION (2) #IMPLIED>", false),
      (DtdSubsetKind.Internal, "<!ATTLIST r a CDATA 'a<z'>", false),
      (DtdSubsetKind.Internal, "<!ATTLIST r a CDATA '&#0;'>", false),
      (DtdSubsetKind.Internal, "<!ENTITY e '&bad'>", false),
      (DtdSubsetKind.Internal, "<!ENTITY e '&#x110000;'>", false),
      (DtdSubsetKind.Internal, "<!ENTITY % p 'x'><!ENTITY e '%p;'>", false),
      (DtdSubsetKind.Internal, "<!ENTITY % p 'EMPTY'><!ELEMENT r %p;>", false),
      (DtdSubsetKind.Internal, "<!ENTITY % p '%p;'>%p;", false),
      (DtdSubsetKind.Internal, "<!ENTITY % a '&#37;b;'><!ENTITY % b '&#37;a;'>%a;", false),
      (DtdSubsetKind.Internal, "<!ENTITY % p '<!ELEMENT r'>%p; EMPTY>", false),
      (DtdSubsetKind.External, "<!ENTITY % p '(a'><!ELEMENT r %p;,b)>", true),
      (DtdSubsetKind.Internal, "<!ENTITY amp '&amp;'>", false),
      (DtdSubsetKind.Internal, "<!ENTITY lt '&#60;'>", false),
      (DtdSubsetKind.Internal, "<!ENTITY gt SYSTEM 'x'>", false),
      (DtdSubsetKind.Internal, "<!ENTITY % p SYSTEM 'x' NDATA n>", false),
      (DtdSubsetKind.Internal, "<!ENTITY e PUBLIC 'pub'>", false),
      (DtdSubsetKind.Internal, "<!NOTATION n PUBLIC 'a\t'>", false),
      (DtdSubsetKind.Internal, "<![IGNORE[x]]>", false),
      (DtdSubsetKind.External, "<![INCLUDE[x]]>", false),
      (DtdSubsetKind.External, "<![IGNORE[unfinished", false),
      (DtdSubsetKind.External, "<?xml version='1.0'?>", false),
      (DtdSubsetKind.External, "<?xml encoding='UTF-8' standalone='yes'?>", false),
      (DtdSubsetKind.Internal, "<?XML?>", false),
      (DtdSubsetKind.Internal, "<!--a--b-->", false),
      (DtdSubsetKind.Internal, "<?p", false),
      (DtdSubsetKind.Internal, "<!ELEMENT r EMPTY", false),
      (DtdSubsetKind.Internal, "\0", false),
      (DtdSubsetKind.Internal, "\uD800x", false),
      (DtdSubsetKind.Internal, "\uDC00", false),
      (DtdSubsetKind.Internal, "<!--\uD800", false) ];
    public static IEnumerable<object[]> Splits()
    { for (int id = 0; id < Cases.Length; id++) for (int split = 0; split <= Cases[id].Text.Length; split++) yield return [id, split]; }
    [Theory, MemberData(nameof(Splits))]
    public void EveryShortSplit(int id, int split)
    {
        var (kind, text, valid) = Cases[id];
        using var p = new DtdParser(kind, new() { EntityDepth = null });
        var first = p.Append(text.AsSpan(0, split), [], false, TestContext.Current.CancellationToken);
        Assert.InRange(first.Consumed, 0, split);
        if (first.Step == DtdParseStep.NeedInput) Assert.Equal(split, first.Consumed);
        var last = first.Step == DtdParseStep.Rejected ? first : p.Append(text.AsSpan(split), [], true, TestContext.Current.CancellationToken);
        Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, last.Step);
        if (valid)
        {
            Assert.NotNull(p.Model); Assert.Null(last.Diagnostic); Assert.Equal(text.Length - split, last.Consumed);
            if (text == "<!ENTITY % p '(a'><!ELEMENT r %p;,b)>")
            {
                var model = p.Model; Assert.Single(model.Elements); Assert.Single(model.ParameterEntities);
                Assert.Empty(model.Attributes); Assert.Empty(model.GeneralEntities); Assert.Empty(model.Notations);
                var element = model.Elements[0]; Assert.Equal("r", element.Name); Assert.True(element.IsExternal);
                Assert.Equal(new XmlLocation(null, 1, text.IndexOf("<!ELEMENT", StringComparison.Ordinal) + 1, null), element.Location);
                Assert.Equal(DtdParticleKind.Sequence, element.Content.Kind); Assert.Equal(DtdOccurrence.Once, element.Content.Occurrence); Assert.Null(element.Content.Name);
                Assert.Equal(new[] { "a", "b" }, element.Content.Children.Select(c => c.Name));
                Assert.All(element.Content.Children, c => { Assert.Equal(DtdParticleKind.Name, c.Kind); Assert.Equal(DtdOccurrence.Once, c.Occurrence); Assert.Empty(c.Children); });
                var pe = model.ParameterEntities[0]; Assert.Equal("p", pe.Name); Assert.True(pe.IsParameter); Assert.True(pe.IsExternal);
                Assert.Equal("(a", pe.ReplacementText); Assert.Null(pe.SystemId); Assert.Null(pe.PublicId); Assert.Null(pe.NotationName);
                Assert.Equal(new XmlLocation(null, 1, 1, null), pe.Location);
            }
        }
        else Assert.NotNull(last.Diagnostic);
        Assert.Throws<InvalidOperationException>(() => p.Append([], [], true, TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("<?a:b?>", false)]
    [InlineData("<?p?x?>", false)]
    [InlineData("<?p??>", false)]
    [InlineData("<?p ?x?>", true)]
    [InlineData("<!ELEMENT e (a ?)>", false)]
    [InlineData("<!ELEMENT e ((a) *)>", false)]
    [InlineData("<!ELEMENT e (# PCDATA)>", false)]
    [InlineData("<!ATTLIST e a CDATA # IMPLIED>", false)]
    [InlineData("<!ATTLIST e a CDATA # FIXED 'x'>", false)]
    [InlineData("<!ENTITY lt '&#38;#00060;'>", true)]
    [InlineData("<!ENTITY amp '&#38;#x00026;'>", true)]
    [InlineData("<!ENTITY gt '&#38;#00062;'>", true)]
    public void AdjacencyPiPolicyAndPredefinedSemanticSpellings(string text, bool valid)
    {
        for (int split = 0; split <= text.Length; split++)
        { using var p = new DtdParser(DtdSubsetKind.Internal, new()); var a = p.Append(text.AsSpan(0, split), [], false, TestContext.Current.CancellationToken); var b = a.Step == DtdParseStep.Rejected ? a : p.Append(text.AsSpan(split), [], true, TestContext.Current.CancellationToken); Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, b.Step); }
    }
    [Fact]
    public void ModelValuesPrecedenceAndNamespaces()
    {
        using var p = new DtdParser(DtdSubsetKind.Internal, new());
        const string text = "<!ELEMENT r (a,b?)><!ELEMENT r ANY><!ATTLIST r a CDATA ' &e;&#9; '><!ATTLIST r a ID #REQUIRED><!ENTITY e 'a&#13;&f;'><!ENTITY e 'ignored'><!ENTITY f 'z'><!ENTITY % e '<!NOTATION n SYSTEM &quot;x&quot;>'><!NOTATION n SYSTEM 'first'><!NOTATION n SYSTEM 'second'>";
        Assert.Equal(DtdParseStep.Completed, p.Append(text, [], true, TestContext.Current.CancellationToken).Step);
        var m = p.Model; Assert.Single(m.Elements); Assert.Single(m.Attributes); Assert.Single(m.ParameterEntities); Assert.Single(m.Notations);
        Assert.Equal("first", m.Notations[0].SystemId); Assert.Equal(" &e;&#9; ", m.Attributes[0].DefaultValue); Assert.Equal("a\r&f;", m.GeneralEntities[0].ReplacementText);
        var effective = m.GeneralEntities.ToDictionary(e => e.Name, StringComparer.Ordinal);
        var applied = DtdValueProcessor.NormalizeAttributeDefault(m.Attributes[0].DefaultValue!, DtdAttributeType.CData, effective, new(), default, null, TestContext.Current.CancellationToken);
        Assert.Equal(" a z\t ", applied.Value);
        Assert.Equal(DtdParticleKind.Sequence, m.Elements[0].Content.Kind); Assert.Equal(DtdOccurrence.Optional, m.Elements[0].Content.Children[1].Occurrence);
    }
    [Theory]
    [InlineData("<!ENTITY e SYSTEM 'x'><!ATTLIST r a CDATA '&e;'>", false)]
    [InlineData("<!ATTLIST r a CDATA '&e;'><!ENTITY e SYSTEM 'x'>", false)]
    [InlineData("<!ENTITY e 'a&e;'><!ATTLIST r a CDATA '&e;'>", false)]
    [InlineData("<!ENTITY e '&#60;'><!ATTLIST r a CDATA '&e;'>", false)]
    [InlineData("<!ENTITY e '&#38;#60;'><!ATTLIST r a CDATA '&e;'>", true)]
    [InlineData("<!ENTITY e '&later;'><!ATTLIST r a CDATA '&e;'>", true)]
    [InlineData("<!ENTITY e '&f;'><!ENTITY f '<'><!ATTLIST r a CDATA '&e;'>", false)]
    public void KnownDefaultReferencesWfAndUnresolvedEffectiveTableBoundary(string text, bool valid)
    {
        for (int split = 0; split <= text.Length; split++)
        { using var p = new DtdParser(DtdSubsetKind.Internal, new()); var a = p.Append(text.AsSpan(0, split), [], false, TestContext.Current.CancellationToken); var b = a.Step == DtdParseStep.Rejected ? a : p.Append(text.AsSpan(split), [], true, TestContext.Current.CancellationToken); Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, b.Step); }
    }
    [Theory]
    [InlineData("<!ENTITY e '&#38;#0;'><!ATTLIST r a CDATA '&e;'>")]
    [InlineData("<!ENTITY e '&#38;1bad;'><!ATTLIST r a CDATA '&e;'>")]
    [InlineData("<!ENTITY e SYSTEM 'external'><!ATTLIST r a CDATA '&e;'>")]
    [InlineData("<!ENTITY e '<'><!ATTLIST r a CDATA '&e;'>")]
    [InlineData("<!ENTITY e '&e;'><!ATTLIST r a CDATA '&e;'>")]
    public void InternalKnownTableRejectsExternalLocalTableDefersToEffectiveOverride(string text)
    {
        using var internalParser = new DtdParser(DtdSubsetKind.Internal, new()); Assert.Equal(DtdParseStep.Rejected, internalParser.Append(text, [], true, TestContext.Current.CancellationToken).Step);
        using var externalParser = new DtdParser(DtdSubsetKind.External, new()); Assert.Equal(DtdParseStep.Completed, externalParser.Append(text, [], true, TestContext.Current.CancellationToken).Step);
        var effective = new Dictionary<string, DtdEntityDeclaration>(StringComparer.Ordinal) { ["e"] = new("e", false, "safe", null, null, null, false, default) };
        var raw = externalParser.Model.Attributes[0].DefaultValue!;
        Assert.Equal("safe", DtdValueProcessor.NormalizeAttributeDefault(raw, DtdAttributeType.CData, effective, new(), default, null, TestContext.Current.CancellationToken).Value);
    }
    [Fact]
    public void ProcessedParameterReplacementPreservesReferencedCrAndLf()
    {
        const string source = "<!ENTITY % p '&#13;'><!ENTITY % q '&#10;'><!ENTITY e '%p;%q;'><!ENTITY raw 'a\r\nb'>";
        for (int split = 0; split <= source.Length; split++)
        { using var p = new DtdParser(DtdSubsetKind.External, new()); p.Append(source.AsSpan(0, split), [], false, TestContext.Current.CancellationToken); Assert.Equal(DtdParseStep.Completed, p.Append(source.AsSpan(split), [], true, TestContext.Current.CancellationToken).Step); Assert.Equal("\r\n", p.Model.GeneralEntities[0].ReplacementText); Assert.Equal("a\nb", p.Model.GeneralEntities[1].ReplacementText); }
    }
    [Fact]
    public void SupplementaryParameterNameCountsScalarsAtReference()
    {
        string name = string.Concat(Enumerable.Repeat("😀", 100)); string declaration = "<!ENTITY % " + name + " ' '>";
        int count = declaration.EnumerateRunes().Count(); using var p = new DtdParser(DtdSubsetKind.Internal, new() { TokenCharacters = count });
        Assert.Equal(DtdParseStep.Completed, p.Append(declaration + "%" + name + ";", [], true, TestContext.Current.CancellationToken).Step);
        using var over = new DtdParser(DtdSubsetKind.Internal, new() { TokenCharacters = count - 1 }); Assert.Equal(DtdParseStep.Rejected, over.Append(declaration, [], true, TestContext.Current.CancellationToken).Step);
    }
    [Fact]
    public void OneUnitChunksAndBorrowedRelease()
    {
        const string text = "<!ENTITY % p '<!ELEMENT 😀 (#PCDATA)>'>%p;\r\n<!--😀-->";
        using var p = new DtdParser(DtdSubsetKind.Internal, new()); char[] owner = new char[1];
        foreach (char c in text) { owner[0] = c; var r = p.Append(owner, [], false, TestContext.Current.CancellationToken); Assert.Equal(1, r.Consumed); Assert.Equal(DtdParseStep.NeedInput, r.Step); owner[0] = '\0'; }
        Assert.Equal(DtdParseStep.Completed, p.Append([], [], true, TestContext.Current.CancellationToken).Step); Assert.Equal("😀", p.Model.Elements[0].Name);
    }
}
