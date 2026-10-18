using Xemele.Html;
using Xunit;

namespace Xemele.Tests.HtmlTokenization;

public sealed class TokenizerBehaviorTests
{
    [Fact]
    public void ProcessingInstructionsOwnCaseDataLocationsAndScalarQuotas()
    {
        var tokens = Html5Tokenizer.Tokenize("😀\r\n<?FoO_1- \r\n&amp;\0😀??><b>");
        var pi = tokens[1];
        Assert.Equal(HtmlTokenKind.ProcessingInstruction, pi.Kind);
        Assert.Equal("FoO_1-", pi.Name);
        Assert.Equal("&amp;\0😀?", pi.Data);
        Assert.Equal(new XmlLocation(null, 2, 1, null), pi.Location);
        Assert.Empty(pi.Attributes);
        Assert.Null(pi.Doctype);
        Assert.False(pi.SelfClosing);
        Assert.Equal(HtmlTokenKind.StartTag, tokens[2].Kind); // The PI snapshot survived subsequent delivery.
        Assert.Equal("😀", Html5Tokenizer.Tokenize("<?x 😀?>", new()
        {
            Limits = new()
            {
                TokenCharacters = 1
            }
        })[0].Data);
        Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("<?xx>", new() { Limits = new() { TokenCharacters = 1 } }));
        Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("<?x 😀x>", new() { Limits = new() { TokenCharacters = 1 } }));
        var errors = new List<(string, XmlLocation)>();
        var tokenizer = new HtmlTokenizer(_ => { }, new()) { ParseError = (code, location) => errors.Add((code, location)) };
        tokenizer.Feed("<?\r\n");
        tokenizer.Complete();
        Assert.Equal(("invalid-first-character-of-processing-instruction-target", new XmlLocation(null, 1, 3, null)), Assert.Single(errors));
        var failure = new XmlParseException([new("test", XmlDiagnosticSeverity.Error, "PI callback", default)]);
        Assert.Same(failure, Assert.Throws<XmlParseException>(() => Html5Tokenizer.Tokenize("<?x><b>", _ => throw failure, cancellationToken: TestContext.Current.CancellationToken)));
    }

    [Fact]
    public void OwnedTokensDuplicatesDoctypesAndEof()
    {
        var retained = new List<HtmlToken>();
        Html5Tokenizer.Tokenize("<!doctype HTML PUBLIC '' 'x'><DiV A='first' a=second b=&#x1F600;/><!--a--!>", retained.Add, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("", retained[0].Doctype!.PublicIdentifier);
        Assert.Equal("x", retained[0].Doctype!.SystemIdentifier);
        Assert.False(retained[0].Doctype!.ForceQuirks);
        Assert.Equal("div", retained[1].Name);
        Assert.Equal(["a", "b"], retained[1].Attributes.Select(a => a.LocalName));
        Assert.Equal("first", retained[1].Attributes[0].Value);
        Assert.Equal("😀/", retained[1].Attributes[1].Value);
        Assert.Equal("a", retained[2].Data);
        Assert.Equal(HtmlTokenKind.EndOfFile, retained[^1].Kind);
        Assert.Throws<NotSupportedException>(() => ((IList<HtmlAttribute>)retained[1].Attributes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HtmlToken>)Html5Tokenizer.Tokenize("x")).Clear());
    }

    [Fact]
    public void ScalarLocationsNormalizationAndStateSpecificNuls()
    {
        var tokens = Html5Tokenizer.Tokenize("😀\r\n<x>\0\r<!--\0-->");
        Assert.Equal("😀\n", tokens[0].Data);
        Assert.Equal(new XmlLocation(null, 1, 1, null), tokens[0].Location);
        Assert.Equal(new XmlLocation(null, 2, 1, null), tokens[1].Location);
        Assert.Equal("\0\n", tokens[2].Data);
        Assert.Equal(new XmlLocation(null, 2, 4, null), tokens[2].Location);
        Assert.Equal("�", tokens[3].Data);
        Assert.Equal(new XmlLocation(null, 3, 1, null), tokens[3].Location);
        Assert.Equal(new XmlLocation(null, 3, 9, null), tokens[^1].Location);
        foreach (var state in new[] { HtmlTokenizerState.RcData, HtmlTokenizerState.RawText, HtmlTokenizerState.ScriptData, HtmlTokenizerState.PlainText })
        {
            Assert.Equal("a�b", string.Concat(Html5Tokenizer.Tokenize("a\0b", new()
            {
                InitialState = state,
                LastStartTagName = "x"
            }).Where(t => t.Kind == HtmlTokenKind.Character).Select(t => t.Data)));
        }
    }

    [Fact]
    public void OriginalByteMapsSurrogateAndCrLfSplits()
    {
        var retained = new List<HtmlToken>();
        var tokenizer = new HtmlTokenizer(retained.Add, new()) { SourceUri = new("https://example.test/html") };
        tokenizer.Feed("\ud83d", [0]);
        tokenizer.Feed("\ude00\r", [0, 4]);
        tokenizer.Feed("\n<x>", [5, 6, 7, 8]);
        tokenizer.Complete(9);
        Assert.Equal(0, retained[0].Location.ByteOffset);
        Assert.Equal(new XmlLocation(new("https://example.test/html"), 2, 1, 6), retained[1].Location);
        Assert.Equal(new XmlLocation(new("https://example.test/html"), 2, 4, 9), retained[^1].Location);
        Assert.Throws<InvalidOperationException>(() => tokenizer.Complete());
        Assert.Throws<InvalidOperationException>(() => tokenizer.Feed("x"));
    }

    [Fact]
    public void TreeBuilderChangesStateAndForeignCdataSynchronously()
    {
        var retained = new List<HtmlToken>();
        HtmlTokenizer? tokenizer = null;
        tokenizer = new(t =>
        {
            retained.Add(t);
            if (t.Kind == HtmlTokenKind.StartTag && t.Name == "textarea")
            {
                tokenizer!.SetState(HtmlTokenizerState.RcData);
            }

            if (t.Kind == HtmlTokenKind.StartTag && t.Name == "svg")
            {
                tokenizer!.AllowCdata = true;
            }

            if (t.Kind == HtmlTokenKind.EndTag && t.Name == "svg")
            {
                tokenizer!.AllowCdata = false;
            }
        }, new());
        tokenizer.Feed("<textarea>a<b>&amp;</textarea><svg><![CDATA[<x>\0]]></svg><![CDATA[x]]>");
        tokenizer.Complete();
        Assert.Equal("a<b>&", retained[1].Data);
        Assert.Equal(HtmlTokenKind.EndTag, retained[2].Kind);
        Assert.Equal("<x>\0", retained[4].Data);
        Assert.Equal(HtmlTokenKind.Comment, retained[6].Kind);
        Assert.Equal("[CDATA[x]]", retained[6].Data);
    }

    [Fact]
    public void CallbackFaultsRemainIdenticalAndStopImmediately()
    {
        var expected = new Exception("callback");
        var calls = 0;
        Assert.Same(expected, Assert.Throws<Exception>(() => Html5Tokenizer.Tokenize("<a><b>", _ => { calls++; throw expected; }, cancellationToken: TestContext.Current.CancellationToken)));
        Assert.Equal(1, calls);
        var parseException = new XmlParseException([new("test", XmlDiagnosticSeverity.Error, "callback", default)]);
        Assert.Same(parseException, Assert.Throws<XmlParseException>(() => Html5Tokenizer.Tokenize("x", _ => throw parseException, cancellationToken: TestContext.Current.CancellationToken)));
        var tokenizer = new HtmlTokenizer(_ => throw expected, new());
        Assert.Same(expected, Assert.Throws<Exception>(() => tokenizer.Feed("<a>")));
        Assert.Throws<InvalidOperationException>(() => tokenizer.Feed("<b>"));
    }

    [Fact]
    public void CancellationIsBoundedAndOptionsRejectBeforeDelivery()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => Html5Tokenizer.Tokenize("x", _ => throw new Exception(), cancellationToken: source.Token));
        using var during = new CancellationTokenSource();
        var calls = 0;
        Assert.Throws<OperationCanceledException>(() => Html5Tokenizer.Tokenize("<a>" + new string('x', 5000), _ => { calls++; during.Cancel(); }, cancellationToken: during.Token));
        Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() => Html5Tokenizer.Tokenize(null!));
        Assert.Throws<ArgumentNullException>(() => Html5Tokenizer.Tokenize("", (Action<HtmlToken>)null!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => Html5Tokenizer.Tokenize("", new HtmlTokenizerOptions { InitialState = HtmlTokenizerState.RawText }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Html5Tokenizer.Tokenize("", new HtmlTokenizerOptions { InitialState = (HtmlTokenizerState)99 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Html5Tokenizer.Tokenize("", new HtmlTokenizerOptions { Limits = new() { TokenCharacters = 0 } }));
    }

    [Fact]
    public void QuotasUseScalarsAndAggregateAcceptedAttributeCharacters()
    {
        Assert.Equal("😀", Html5Tokenizer.Tokenize("😀", new()
        {
            Limits = new()
            {
                DocumentCharacters = 1,
                ExpandedCharacters = 1,
                TokenCharacters = 1
            }
        })[0].Data);
        Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("😀x", new() { Limits = new() { DocumentCharacters = 1 } }));
        Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("&NotEqualTilde;", new() { Limits = new() { ExpandedCharacters = 1 } }));
        Assert.Equal("😀", Html5Tokenizer.Tokenize("<!--😀-->", new()
        {
            Limits = new()
            {
                TokenCharacters = 1
            }
        })[0].Data);
        Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("<!--😀x-->", new() { Limits = new() { TokenCharacters = 1 } }));
        Assert.Equal("a", Html5Tokenizer.Tokenize("<a b='😀'>", new()
        {
            Limits = new()
            {
                TokenCharacters = 2,
                AttributesPerElement = 1
            }
        })[0].Name);
        Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("<a b='😀x'>", new() { Limits = new() { TokenCharacters = 2 } }));
        Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("<a b c>", new() { Limits = new() { AttributesPerElement = 1 } }));
        Assert.Equal(2, Html5Tokenizer.Tokenize("<a b B>", new()
        {
            Limits = new()
            {
                AttributesPerElement = 1
            }
        }).Count);
        Assert.Equal("😀", Html5Tokenizer.Tokenize("&#x1F600;", new()
        {
            Limits = new()
            {
                TokenCharacters = 1
            }
        })[0].Data);
    }

    [Fact]
    public void ReferenceOverflowUnknownPrefixesAndAttributeAmbiguity()
    {
        var tokens = Html5Tokenizer.Tokenize("<a x='&ampx &amp= &amp;='>&notit; &#999999999999999999999999999999999999; &#0; &#xD800; &#x110000; &#128; &unknown;");
        Assert.Equal("&ampx &amp= &=", tokens[0].Attributes[0].Value);
        Assert.Equal("¬it; � � � � € &unknown;", tokens[1].Data);
    }

    [Fact]
    public void SupplementaryAndReferenceBatchesKeepFirstSourceScalarLocations()
    {
        var tokens = Html5Tokenizer.Tokenize("😀x😀y", new()
        {
            Limits = new()
            {
                TokenCharacters = 2
            }
        });
        Assert.Equal(["😀", "x😀", "y"], tokens.Where(t => t.Kind == HtmlTokenKind.Character).Select(t => t.Data));
        Assert.Equal([1, 2, 4], tokens.Where(t => t.Kind == HtmlTokenKind.Character).Select(t => t.Location.LinePosition));
        tokens = Html5Tokenizer.Tokenize("a&NotEqualTilde;b", new()
        {
            Limits = new()
            {
                TokenCharacters = 1
            }
        });
        Assert.Equal(["a", "≂", "̸", "b"], tokens.Where(t => t.Kind == HtmlTokenKind.Character).Select(t => t.Data));
        Assert.Equal([1, 2, 2, 17], tokens.Where(t => t.Kind == HtmlTokenKind.Character).Select(t => t.Location.LinePosition));
        Assert.Equal("😀", Html5Tokenizer.Tokenize("&#x1F600;", new()
        {
            Limits = new()
            {
                TokenCharacters = 1
            }
        })[0].Data);
    }

    [Fact]
    public void InvalidSurrogatesArePreservedWithSeparateBoundedErrors()
    {
        var retained = new List<HtmlToken>();
        var errors = new List<string>();
        var tokenizer = new HtmlTokenizer(retained.Add, new()
        {
            Limits = new()
            {
                Diagnostics = 1
            }
        })
        {
            ParseError = (code, _) => errors.Add(code)
        };
        tokenizer.Feed("\ud800");
        tokenizer.Feed("x\udc00");
        tokenizer.Complete();
        Assert.Equal("\ud800x\udc00", retained[0].Data);
        Assert.Equal(new[] { "surrogate-in-input-stream" }, errors);
        for (var c = 0; c <= 0xFFFF; c++)
        {
            var expected = char.IsAsciiHexDigit((char)c) ? c <= '9' ? c - '0' : (c | 32) - 'a' + 10 : -1;
            Assert.Equal(expected, HtmlTokenizer.Digit(c, 16));
            Assert.Equal(c is >= '0' and <= '9' ? c - '0' : -1, HtmlTokenizer.Digit(c, 10));
        }
        Assert.Equal(-1, HtmlTokenizer.Digit(0x1F600, 16));
        Assert.Equal(-1, HtmlTokenizer.Digit(-1, 10));
    }

    [Fact]
    public void ValueStringBuilderSupportsSpanFormattingBufferWriterAndDisposal()
    {
        var builder = new Xemele.Internal.ValueStringBuilder();
        builder.Append('a');
        builder.Append('b', 3);
        builder.Append("c");
        builder.Append("de");
        builder.Append("_fg".AsSpan(1));
        builder.Append(42, provider: System.Globalization.CultureInfo.InvariantCulture);
        builder.AppendScalar(0x1F600);
        var span = builder.GetSpan(2);
        span[0] = '!';
        span[1] = '?';
        builder.Advance(2);
        var mem = builder.GetMemory(1);
        mem.Span[0] = '.';
        builder.Advance(1);

        Assert.Equal("abbbcdefg42😀!?.", builder.AsSpan().ToString());
        Assert.Equal("abbb", builder.AsSpan(0, 4).ToString());
        Assert.Equal('a', builder[0]);
        Assert.Equal("abbbcdefg42😀!?.", builder.ToStringAndClear());
        Assert.Equal(0, builder.Length);
        builder.Append(new string('x', 200));
        Assert.Equal(200, builder.ToStringAndDispose().Length);
        Assert.Equal(0, builder.Length);
        Assert.Equal("", builder.ToString());
    }
    [Fact]
    public void AttributeReferencesMatchThePerScalarPath()
    {
        // The whole-tag fast path resolves error-free terminated references inline. Feeding one character
        // at a time keeps every tag incomplete, so the per-scalar state machine is the oracle for tokens,
        // token locations, diagnostics and quotas.
        string[] references =
        [
            "&amp;", "&lt;", "&gt;", "&quot;", "&nbsp;", "&apos;", "&copy;", "&not;", "&notin;", "&notit;", "&Afr;", "&NotEqualTilde;",
            "&CounterClockwiseContourIntegral;", "&CounterClockwiseContourIntegralx;", "&xyz;", "&amp", "&amp=", "&ampx", "&ampx;", "&AMP;",
            "&am;", "&;", "&", "&&amp;", "&amp;&amp;", "&#39;", "&#x27;", "&#X27;", "&#0039;", "&#x1F600;", "&#128512;", "&#65", "&#;", "&#x;",
            "&#0;", "&#13;", "&#x80;", "&#x9F;", "&#xD800;", "&#xFDD0;", "&#xFFFE;", "&#x10FFFF;", "&#x110000;", "&#99999999;", "&#00000065;",
            "&#x0000041;", "&#1114111;", "&#x7F;", "&#9;", "&#10;", "&#32;", "&# 1;", "&\u0001;", "&amp;\u0001", "&amp;\r", "&amp;\u00A0", "&amp;😀",
        ];
        string[] shapes = ["<a v=\"x{0}y\">", "<a v='{0}'>", "<a v={0}>", "<a v={0} w>", "<a v=\"{0}\" w=\"{0}{0}\"/>", "<a\nv=\"{0}\"\nw='{0}'>", "<a v=\"{0}\"x>", "<a v=\"{0}\" v=\"{0}\">"];
        var cases = 0;
        foreach (var reference in references)
        {
            var value = System.Text.RegularExpressions.Regex.Unescape(reference);
            foreach (var shape in shapes)
            {
                var input = "t" + string.Format(System.Globalization.CultureInfo.InvariantCulture, shape, value) + "u";
                foreach (var limits in (XmlResourceLimits[])[new(), new() { TokenCharacters = 3 }, new() { ExpandedCharacters = 4 }])
                {
                    Assert.True(Run(input, limits, false) == Run(input, limits, true), input);
                    cases++;
                }
            }
        }
        Assert.Equal(references.Length * shapes.Length * 3, cases);

        static string Run(string input, XmlResourceLimits limits, bool perCharacter)
        {
            var tokens = new List<HtmlToken>();
            var log = new System.Text.StringBuilder();
            var tokenizer = new HtmlTokenizer(tokens.Add, new() { Limits = limits }) { ParseError = (code, location) => log.Append(code).Append('@').Append(location).Append(';') };
            try
            {
                if (perCharacter)
                {
                    for (var i = 0; i < input.Length; i++)
                    {
                        tokenizer.Feed(input.AsSpan(i, 1));
                    }
                }
                else
                {
                    tokenizer.Feed(input);
                }

                tokenizer.Complete();
            }
            catch (InvalidOperationException e)
            {
                log.Append("throws ").Append(e.Message);
            }
            foreach (var token in tokens)
            {
                log.Append('|').Append(token.Kind).Append(' ').Append(token.Name).Append(' ').Append(token.Location).Append(' ').Append(token.SelfClosing).Append(' ').Append(token.Data);
                foreach (var attribute in token.Attributes)
                {
                    log.Append(' ').Append(attribute.LocalName).Append('=').Append(attribute.Value);
                }
            }
            return log.ToString();
        }
    }
}
