using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xemele.Html;
using Xunit;

namespace Xemele.Tests.HtmlCallbacks;

public sealed class CallbackIntegrationTests
{
    [Fact]
    public void AllSevenKindsAreSynchronousOwnedLexicalSnapshots()
    {
        var retained = new List<HtmlToken>();
        var returned = false;
        var thread = Environment.CurrentManagedThreadId;
        Html5Tokenizer.Tokenize("<!doctype html PUBLIC '' 'sys'><TABLE A='first' a=second />outside<!--note--><?Build 😀?></table>", token =>
        {
            Assert.False(returned);
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            retained.Add(token);
        }, cancellationToken: TestContext.Current.CancellationToken);
        returned = true;
        Assert.Equal([ HtmlTokenKind.Doctype, HtmlTokenKind.StartTag, HtmlTokenKind.Character,
            HtmlTokenKind.Comment, HtmlTokenKind.ProcessingInstruction, HtmlTokenKind.EndTag, HtmlTokenKind.EndOfFile ], retained.Select(t => t.Kind));
        Assert.Equal("outside", retained[2].Data); // Lexical position inside table: no foster parenting.
        Assert.Equal("", retained[0].Doctype!.PublicIdentifier);
        Assert.Equal("sys", retained[0].Doctype!.SystemIdentifier);
        Assert.True(retained[1].SelfClosing);
        Assert.Equal("first", Assert.Single(retained[1].Attributes).Value);
        Assert.Throws<NotSupportedException>(() => ((IList<HtmlAttribute>)retained[1].Attributes)[0] = new("a", "changed"));
        Html5Tokenizer.Tokenize("<other a='changed'><?Other changed?>", _ => { }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("table", retained[1].Name);
        Assert.Equal("first", retained[1].Attributes[0].Value);
        Assert.Equal("Build", retained[4].Name);
        Assert.Equal("😀", retained[4].Data);
        Assert.Null(retained[4].Location.ByteOffset);
        Assert.Single(retained, t => t.Kind == HtmlTokenKind.EndOfFile);
    }

    [Theory]
    [InlineData(HtmlTokenizerState.Data)]
    [InlineData(HtmlTokenizerState.RcData)]
    [InlineData(HtmlTokenizerState.RawText)]
    [InlineData(HtmlTokenizerState.ScriptData)]
    [InlineData(HtmlTokenizerState.PlainText)]
    public void PublicStatesMatchAtEveryInternalChunkBoundary(HtmlTokenizerState state)
    {
        const string input = "😀\r\n&amp;<!--x--><b a='&amp;'>z</b><?Pi data?></x>tail";
        var options = new HtmlTokenizerOptions { InitialState = state, LastStartTagName = "x" };
        var expected = Normalize(Html5Tokenizer.Tokenize(input, options));
        for (var split = -1; split <= input.Length; split++)
        {
            var actual = new List<HtmlToken>();
            var tokenizer = new HtmlTokenizer(actual.Add, options, TestContext.Current.CancellationToken);
            tokenizer.Feed("");
            if (split < 0)
            {
                for (var i = 0; i < input.Length; i++)
                {
                    tokenizer.Feed(input.AsSpan(i, 1));
                }
            }
            else
            {
                tokenizer.Feed(input.AsSpan(0, split));
                tokenizer.Feed(input.AsSpan(split));
            }
            tokenizer.Complete();
            Assert.Equal(expected, Normalize(actual));
            Assert.Single(actual, t => t.Kind == HtmlTokenKind.EndOfFile);
        }
    }

    [Fact]
    public void InternalForeignCdataDeliversBeforeCompleteAndOwnsCallerChunks()
    {
        var retained = new List<HtmlToken>();
        var tokenizer = new HtmlTokenizer(retained.Add, new());
        tokenizer.StartCdata();
        var first = "😀\r".ToCharArray();
        tokenizer.Feed(first);
        Array.Fill(first, 'x');
        tokenizer.Feed("\n<x>]]><p a='one'>");
        Assert.Equal("😀\n<x>", retained[0].Data);
        Assert.Equal(HtmlTokenKind.StartTag, retained[1].Kind);
        Assert.DoesNotContain(retained, t => t.Kind == HtmlTokenKind.EndOfFile);
        tokenizer.Feed("<q a='two'><?Target original?>");
        tokenizer.Complete();
        Assert.Equal("one", retained[1].Attributes[0].Value);
        Assert.Equal("original", retained[^2].Data);
        Assert.Single(retained, t => t.Kind == HtmlTokenKind.EndOfFile);
        Assert.Throws<InvalidOperationException>(() => tokenizer.Complete());
    }

    [Fact]
    public void PrefixArrivesBeforeQuotaFailureWithoutEof()
    {
        var delivered = new List<HtmlToken>();
        var failure = Assert.Throws<InvalidOperationException>(() => Html5Tokenizer.Tokenize("<p>" + new string('x', 10000), delivered.Add,
            new()
            {
                Limits = new()
                {
                    DocumentCharacters = 300,
                    TokenCharacters = 64
                }
            }, TestContext.Current.CancellationToken));
        Assert.Contains("DocumentCharacters", failure.Message);
        Assert.Equal(HtmlTokenKind.StartTag, delivered[0].Kind);
        Assert.Contains(delivered, t => t.Kind == HtmlTokenKind.Character);
        Assert.All(delivered.Where(t => t.Kind == HtmlTokenKind.Character), t => Assert.InRange(t.Data.Length, 1, 64));
        Assert.DoesNotContain(delivered, t => t.Kind == HtmlTokenKind.EndOfFile);
    }

    [Fact]
    public void FaultIdentityIsPreservedAtEveryDeliveryIncludingEof()
    {
        const string input = "<!doctype html><p>x<!--c--><?Pi d?></p>";
        var total = Html5Tokenizer.Tokenize(input).Count;
        foreach (var fault in new Exception[] { new ApplicationException("consumer"), new XmlParseException([new("consumer", XmlDiagnosticSeverity.Error, "consumer", default)]) })
        {
            for (var stop = 1; stop <= total; stop++)
            {
                var calls = 0;
                var observed = Record.Exception(() => Html5Tokenizer.Tokenize(input, _ => { if (++calls == stop) { throw fault; } }, cancellationToken: TestContext.Current.CancellationToken));
                Assert.Same(fault, observed);
                Assert.Equal(stop, calls);
            }
        }
    }

    [Fact]
    public void CancellationAtEntryAndFromCallbackStopsWithoutEof()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var failure = Assert.Throws<OperationCanceledException>(() => Html5Tokenizer.Tokenize("<p>" + new string('x', 100000), _ => { calls++; cancellation.Cancel(); }, cancellationToken: cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(1, calls);
        Assert.Throws<OperationCanceledException>(() => Html5Tokenizer.Tokenize("<p>", _ => calls++, cancellationToken: cancellation.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DiscardedTokensAreCollectibleWhileOperationRemainsActive()
    {
        var references = new List<WeakReference<HtmlToken>>();
        var tokenizer = new HtmlTokenizer(token => references.Add(new(token)), new()
        {
            Limits = new()
            {
                TokenCharacters = 16
            }
        });
        FeedDiscarding(tokenizer);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(512, references.Count);
        Assert.All(references, reference => Assert.False(reference.TryGetTarget(out _)));
        // Keep the tokenizer and consumer alive: this tests parser retention, not disposal.
        tokenizer.Feed("<after>");
        tokenizer.Complete();
        GC.KeepAlive(tokenizer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FeedDiscarding(HtmlTokenizer tokenizer)
    {
        for (var i = 0; i < 256; i++)
        {
            tokenizer.Feed("<p a='v'>abcdefghijklmnop");
        }
    }

    private static string Normalize(IEnumerable<HtmlToken> tokens)
    {
        var result = new List<object>();
        var characters = new StringBuilder();
        void Flush()
        {
            if (characters.Length > 0)
            {
                result.Add(new object[] { HtmlTokenKind.Character, characters.ToString() });
                characters.Clear();
            }
        }
        foreach (var token in tokens)
        {
            if (token.Kind == HtmlTokenKind.Character)
            {
                characters.Append(token.Data);
                continue;
            }
            Flush();
            result.Add(new object?[] { token.Kind, token.Name, token.Data, token.Attributes, token.SelfClosing, token.Doctype, token.Location });
        }
        Flush();
        return JsonSerializer.Serialize(result);
    }
}
