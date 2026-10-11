using System.Globalization;
using DeclarationModel = Xemele.Parsing.Dtd.DtdModel;
using Xemele.Parsing.Dtd;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M5DtdModel;

public sealed class CompilationTests
{
    private static readonly XmlLocation At = new(new Uri("urn:model"), 3, 7, 12);
    internal static DeclarationModel Parse(string body)
    {
        using var parser = new DtdParser(DtdSubsetKind.External, new(), new Uri("urn:model"));
        var parsed = parser.Append(body, [], true, default);
        Assert.Equal(DtdParseStep.Completed, parsed.Step);
        return parser.Model;
    }
    internal static CompiledDtdModel Compile(string body) => DtdCompiler.Compile(Parse(body), new());
    private static bool Accepts(DtdContentModel content, params string[] children)
    {
        var operation = new ValidationOperation(); int state = content.InitialState;
        foreach (string child in children) if (!content.TryAdvance(ref state, child, operation)) return false;
        Assert.True(operation.IsComplete); return content.IsAccepting(state);
    }
    [Theory]
    [InlineData("(a,b)", "a b", true)]
    [InlineData("(a,b)", "b a", false)]
    [InlineData("(a,b)", "a", false)]
    [InlineData("(a|b)", "b", true)]
    [InlineData("(a|b)", "a b", false)]
    [InlineData("(a?,b*)", "", true)]
    [InlineData("(a?,b*)", "a b b", true)]
    [InlineData("(a?,b*)", "b a", false)]
    [InlineData("((a,b)+,c?)", "a b a b c", true)]
    [InlineData("((a,b)+,c?)", "c", false)]
    [InlineData("((a,b)+,c?)", "a b a", false)]
    [InlineData("((a|b)*,c)", "b a c", true)]
    [InlineData("((a|b)*,c)", "", false)]
    [InlineData("(a?,(b?,c?),d)", "a c d", true)]
    [InlineData("(a?,(b?,c?),d)", "d", true)]
    [InlineData("(a?,(b?,c?),d)", "c b d", false)]
    [InlineData("(a,a)", "a a", true)]
    public void SequenceChoiceNestedAndAllOccurrences(string model, string children, bool expected)
    {
        var compiled = Compile("<!ELEMENT r " + model + ">");
        Assert.Equal(expected, Accepts(compiled.Elements[0].Content, children.Length == 0 ? [] : children.Split(' ')));
    }
    [Theory]
    [InlineData("EMPTY", "", true)]
    [InlineData("EMPTY", "a", false)]
    [InlineData("ANY", "a b", true)]
    [InlineData("(#PCDATA)", "", true)]
    [InlineData("(#PCDATA)", "a", false)]
    [InlineData("(#PCDATA|a|b)*", "a b a", true)]
    [InlineData("(#PCDATA|a|b)*", "x", false)]
    public void EmptyAnyAndMixed(string model, string children, bool expected) =>
        Assert.Equal(expected, Accepts(Compile("<!ELEMENT r " + model + ">").Elements[0].Content, children.Length == 0 ? [] : children.Split(' ')));

    [Theory]
    [InlineData("(a|a)")]
    [InlineData("(a*,a)")]
    [InlineData("(a?,a)")]
    [InlineData("((a,b)|(a,c))")]
    [InlineData("((a,b?)*,b)")]
    public void OneUnambiguousConstraint(string content)
    {
        var error = Assert.Throws<XmlParseException>(() => Compile("<!ELEMENT r " + content + ">"));
        Assert.Equal("DTD.AmbiguousContent", Assert.Single(error.Diagnostics).Code);
        Assert.Equal(new Uri("urn:model"), error.Diagnostics[0].Location.SourceUri);
        Assert.True(error.Diagnostics[0].Location.LineNumber > 0);
    }
    [Theory]
    [InlineData("<!ELEMENT r (#PCDATA|a|a)*>", "DTD.DuplicateMixedName")]
    [InlineData("<!ELEMENT r ANY><!ATTLIST r a ID #IMPLIED b ID #REQUIRED>", "DTD.MultipleIdAttributes")]
    [InlineData("<!ATTLIST r a ID 'x'>", "DTD.IdDefault")]
    [InlineData("<!ATTLIST r a (one|one) #IMPLIED>", "DTD.DuplicateToken")]
    [InlineData("<!ATTLIST r a (one|two) 'three'>", "DTD.InvalidDefault")]
    [InlineData("<!ATTLIST r a NMTOKEN 'two words'>", "DTD.InvalidDefault")]
    [InlineData("<!ATTLIST r a IDREFS ''>", "DTD.InvalidDefault")]
    [InlineData("<!ATTLIST r a ENTITY 'no'>", "DTD.InvalidDefault")]
    [InlineData("<!ATTLIST r a NOTATION (n) #IMPLIED>", "DTD.UndeclaredNotation")]
    [InlineData("<!NOTATION n SYSTEM 'n'><!ELEMENT r EMPTY><!ATTLIST r a NOTATION (n) #IMPLIED>", "DTD.NotationOnEmpty")]
    [InlineData("<!NOTATION n SYSTEM 'n'><!ATTLIST r a NOTATION (n) #IMPLIED b NOTATION (n) #IMPLIED>", "DTD.MultipleNotationAttributes")]
    [InlineData("<!ENTITY e SYSTEM 'e' NDATA no>", "DTD.UndeclaredNotation")]
    public void DeclarationValidity(string body, string code) =>
        Assert.Equal(code, Assert.Single(Assert.Throws<XmlParseException>(() => Compile(body)).Diagnostics).Code);

    [Fact]
    public void DefaultNormalizationUsesExistingEntitySemanticsAndExternalProvenance()
    {
        var model = Compile("<!ENTITY e '  one  two  '><!ELEMENT r ANY><!ATTLIST r names NMTOKENS '&e;' text CDATA 'x&#x9;y' fixed (one|two) #FIXED 'one'>");
        Assert.Equal("one two", model.Attributes[0].NormalizedDefault);
        Assert.Equal("x\ty", model.Attributes[1].NormalizedDefault);
        Assert.Equal("one", model.Attributes[2].NormalizedDefault);
        Assert.Equal(DtdDefaultKind.Fixed, model.Attributes[2].Declaration.DefaultKind);
        Assert.True(model.Attributes[0].Declaration.IsExternal);
        Assert.Equal(new Uri("urn:model"), model.Attributes[0].Declaration.Location.SourceUri);
        Assert.Equal("&e;", model.Attributes[0].Declaration.DefaultValue);
        Assert.Throws<XmlParseException>(() => Compile("<!ATTLIST r a CDATA '&no;'>"));
        Assert.Throws<XmlParseException>(() => Compile("<!ENTITY e '&e;'><!ATTLIST r a CDATA '&e;'>"));
    }
    [Theory]
    [InlineData("CDATA", "", true)]
    [InlineData("CDATA", "\u0001", false)]
    [InlineData("ID", "abc", true)]
    [InlineData("ID", "1abc", false)]
    [InlineData("IDREF", "p:名", true)]
    [InlineData("IDREFS", "one two", true)]
    [InlineData("IDREFS", "one  two", false)]
    [InlineData("IDREFS", "one\ttwo", false)]
    [InlineData("NMTOKEN", "1:a", true)]
    [InlineData("NMTOKENS", "1:a 名", true)]
    [InlineData("NMTOKENS", "x ", false)]
    [InlineData("(one|two)", "one", true)]
    [InlineData("(one|two)", "ONE", false)]
    [InlineData("NOTATION (n)", "n", true)]
    [InlineData("NOTATION (n)", "no", false)]
    [InlineData("ENTITY", "e", true)]
    [InlineData("ENTITY", "parsed", false)]
    [InlineData("ENTITIES", "e e", true)]
    [InlineData("ENTITIES", "e unknown", false)]
    public void AttributeConstraints(string type, string value, bool expected)
    {
        var model = Compile("<!NOTATION n SYSTEM 'n'><!ENTITY e SYSTEM 'e' NDATA n><!ENTITY parsed 'text'><!ELEMENT r ANY><!ATTLIST r a " + type + " #IMPLIED>");
        Assert.Equal(expected, model.Attributes[0].IsValueValid(value, model, new()));
    }
    [Fact]
    public void InvalidUtf16CannotBecomeReplacementName()
    {
        var model = Compile("<!ATTLIST r a IDREF #IMPLIED>");
        Assert.False(model.Attributes[0].IsValueValid(new string(['x', (char)0xD800]), model, new()));
        Assert.False(model.Attributes[0].IsValueValid(new string([(char)0xDC00]), model, new()));
    }
    [Fact]
    public void BorrowedLookupsAndStickyFailureAreOrdinal()
    {
        var model = Compile("<!ELEMENT I (名)><!ATTLIST I id ID #IMPLIED>");
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.NotNull(model.FindElement("_I_".AsSpan(1, 1), new()));
            Assert.Null(model.FindElement("ı", new()));
            Assert.NotNull(model.FindAttribute("I", "id", new()));
            Assert.Null(model.FindAttribute("I", "ID", new()));
            int cursor = 0; Assert.False(model.Elements[0].Content.TryAdvance(ref cursor, "wrong", new()));
            Assert.Equal(-1, cursor); Assert.False(model.Elements[0].Content.TryAdvance(ref cursor, "名", new()));
            Assert.False(model.Elements[0].Content.IsAccepting(cursor));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public async Task ImmutableConcurrentReuseAndSnapshots()
    {
        var source = Parse("<!ELEMENT r (a*,b)><!ATTLIST r n NMTOKENS 'one two'>");
        var model = DtdCompiler.Compile(source, new());
        Assert.Same(source, model.Source);
        Assert.Throws<NotSupportedException>(() => ((IList<CompiledDtdElement>)model.Elements).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CompiledDtdAttribute>)model.Attributes).Clear());
        var runs = await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() =>
            Accepts(model.Elements[0].Content, i % 2 == 0 ? ["a", "a", "b"] : ["b"]))));
        Assert.All(runs, Assert.True);
    }
    [Fact]
    public void WorkDepthTokenExpansionAndCancellationBoundaries()
    {
        var source = Parse("<!ELEMENT r ((a,b)*)>");
        var baseline = new ValidationOperation(); DtdCompiler.Compile(source, baseline);
        var exact = new ValidationOperation(new() { MaxOperations = baseline.Operations, Limits = new() { EvaluationWork = null } });
        DtdCompiler.Compile(source, exact); Assert.True(exact.IsComplete);
        var shortRun = new ValidationOperation(new() { MaxOperations = baseline.Operations - 1, Limits = new() { EvaluationWork = null } });
        Assert.Equal("VALIDATION.Limit", Assert.Single(Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(source, shortRun)).Diagnostics).Code);
        Assert.False(shortRun.IsComplete);
        Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(source, new(new() { Limits = new() { Depth = 2 } })));
        var tokenSource = Parse("<!ELEMENT long EMPTY>");
        DtdCompiler.Compile(tokenSource, new(new() { Limits = new() { TokenCharacters = 4 } }));
        Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(tokenSource, new(new() { Limits = new() { TokenCharacters = 3 } })));
        var defaults = Parse("<!ATTLIST r a CDATA '&#65;' b CDATA '&#66;'>");
        DtdCompiler.Compile(defaults, new(new() { Limits = new() { ExpandedCharacters = 2 } }));
        Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(defaults, new(new() { Limits = new() { ExpandedCharacters = 1 } })));
        using var cancellation = new CancellationTokenSource();
        var run = new ValidationOperation(new() { CancellationToken = cancellation.Token }); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => DtdCompiler.Compile(source, run)).CancellationToken);
    }
    [Fact]
    public void EmptyModelStillChecksCancellationAtCompilerEntry()
    {
        var source = new DeclarationModel([], [], [], [], []);
        var empty = DtdCompiler.Compile(source, new()); Assert.Empty(empty.Elements); Assert.Empty(empty.Attributes);
        using var cancellation = new CancellationTokenSource();
        var operation = new ValidationOperation(new() { CancellationToken = cancellation.Token });
        cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => DtdCompiler.Compile(source, operation)).CancellationToken);
    }
    [Fact]
    public void TransitionFailureStopsAtWorkBoundaryAndPreservesCursorOnQuota()
    {
        var content = Compile("<!ELEMENT r (a*)>").Elements[0].Content;
        var exact = new ValidationOperation(new() { MaxOperations = 3 }); int state = 0;
        Assert.True(content.TryAdvance(ref state, "a", exact));
        Assert.Equal(3, exact.Operations); Assert.True(exact.IsComplete); Assert.Equal(1, state);
        Assert.False(content.TryAdvance(ref state, "a", exact));
        Assert.False(exact.IsComplete); Assert.Equal(1, state);
        using var cancellation = new CancellationTokenSource();
        var run = new ValidationOperation(new() { CancellationToken = cancellation.Token }); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => content.TryAdvance(ref state, "a", run)).CancellationToken);
    }
    [Fact]
    public void SyntheticModelCannotBypassLexicalOrShapeChecks()
    {
        DtdParticle leaf = new(DtdParticleKind.Name, "a", DtdOccurrence.Once, []);
        var cases = new DtdParticle[]
        {
            new(DtdParticleKind.Name, "bad name", DtdOccurrence.Once, []),
            new(DtdParticleKind.Sequence, null, DtdOccurrence.Once, []),
            new(DtdParticleKind.Any, null, DtdOccurrence.Optional, []),
            new(DtdParticleKind.Name, "a", DtdOccurrence.Once, [leaf]),
            new(DtdParticleKind.Mixed, null, DtdOccurrence.Once, [leaf]),
            new(DtdParticleKind.Sequence, null, (DtdOccurrence)42, [leaf])
        };
        foreach (var particle in cases)
        {
            var model = new DeclarationModel([new("r", particle, false, At)], [], [], [], []);
            Assert.Equal(At, Assert.Single(Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(model, new())).Diagnostics).Location);
        }
        var duplicate = new DeclarationModel([new("r", leaf, false, At), new("r", leaf, false, At)], [], [], [], []);
        Assert.Equal("DTD.DuplicateElement", Assert.Single(Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(duplicate, new())).Diagnostics).Code);
        var notations = new DeclarationModel([], [], [], [], [new("n", null, "p", At), new("n", null, "p", At)]);
        Assert.Equal("DTD.DuplicateNotation", Assert.Single(Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(notations, new())).Diagnostics).Code);
    }
    [Fact]
    public void DeepModelsAreIterativeAndFiniteUnderUnlimitedInputQuotas()
    {
        DtdParticle particle = new(DtdParticleKind.Name, "a", DtdOccurrence.Once, []);
        for (int i = 0; i < 4100; i++) particle = new(DtdParticleKind.Sequence, null, DtdOccurrence.Once, [particle]);
        var source = new DeclarationModel([new("r", particle, false, At)], [], [], [], []);
        var error = Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(source, new(new() { MaxOperations = null, Limits = new() { Depth = null, EvaluationWork = null } })));
        Assert.Equal("VALIDATION.Limit", Assert.Single(error.Diagnostics).Code);
    }
}

public sealed class OracleTests
{
    [Fact]
    public void ExhaustiveSequencesMatchIndependentRegularExpressionOracle()
    {
        // Independently specified language oracles, not compiled position tables.
        (string Model, string Regex)[] cases = [
            ("(a,b)", "ab"), ("(a|b)", "[ab]"), ("(a?,b*)", "a?b*"),
            ("((a,b)+,c?)", "(ab)+c?"), ("((a|b)*,c)", "[ab]*c"),
            ("(a?,(b?,c?),d)", "a?b?c?d"), ("((a*,b?)+,c)", "(a*b?)+c"),
            ("((a,b)|(c,d))", "ab|cd"), ("(a,a)", "aa"),
            ("((a,b)*,a?)", "(ab)*a?"), ("((a,b)?,c*)", "(ab)?c*"),
            ("(a+,b?,c*)", "a+b?c*"), ("((a|b)?,c+)", "[ab]?c+")
        ];
        int comparisons = 0, deterministic = 0;
        foreach (var item in cases)
        {
            CompiledDtdModel model;
            try { model = CompilationTests.Compile("<!ELEMENT r " + item.Model + ">"); }
            catch (XmlParseException exception) when (exception.Diagnostics[0].Code == "DTD.AmbiguousContent") { continue; }
            deterministic++;
            var oracle = new System.Text.RegularExpressions.Regex("\\A(?:" + item.Regex + ")\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            for (int length = 0; length <= 5; length++)
            {
                int combinations = (int)Math.Pow(4, length);
                for (int number = 0; number < combinations; number++)
                {
                    int remaining = number; var letters = new char[length];
                    for (int i = 0; i < letters.Length; i++) { letters[i] = (char)('a' + remaining % 4); remaining /= 4; }
                    var operation = new ValidationOperation(); int state = 0; bool accepted = true;
                    foreach (char letter in letters)
                    {
                        ReadOnlySpan<char> token = new ReadOnlySpan<char>(in letter);
                        if (!model.Elements[0].Content.TryAdvance(ref state, token, operation)) { accepted = false; break; }
                    }
                    accepted &= model.Elements[0].Content.IsAccepting(state);
                    Assert.Equal(oracle.IsMatch(new string(letters)), accepted); comparisons++;
                }
            }
        }
        Assert.True(deterministic >= 10); Assert.True(comparisons >= 13650);
    }
    [Fact]
    public void EntityProbesCannotBypassWorkBudgetAndUnicodeNamesUseScalarLimit()
    {
        string entities = string.Concat(Enumerable.Range(0, 100).Select(i => "<!ENTITY n" + i + " 'x'>"));
        var source = CompilationTests.Parse(entities + "<!ATTLIST r a CDATA '&n99;&n99;&n99;'>");
        var full = new ValidationOperation(); DtdCompiler.Compile(source, full);
        Assert.True(full.Operations > 1000);
        var operation = new ValidationOperation(new() { MaxOperations = full.Operations - 1 });
        Assert.Throws<XmlParseException>(() => DtdCompiler.Compile(source, operation));
        Assert.False(operation.IsComplete);
        string unicodeName = char.ConvertFromUtf32(0x10000);
        var unicode = CompilationTests.Parse("<!ELEMENT " + unicodeName + " EMPTY>");
        var compiled = DtdCompiler.Compile(unicode, new(new() { Limits = new() { TokenCharacters = 1 } }));
        Assert.NotNull(compiled.FindElement(unicodeName, new()));
    }
}
