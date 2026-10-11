using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xemele.Html;
using Xunit;

namespace Xemele.Tests.HtmlTokenization;

public sealed class TokenizerCorpusTests
{
    internal static string FixtureRoot()
    {
        string? path = Directory.GetCurrentDirectory();
        while (path is not null && !Directory.Exists(Path.Combine(path, "tests/fixtures/M7-tokenizer"))) path = Directory.GetParent(path)?.FullName;
        return Path.Combine(path ?? throw new InvalidOperationException("Fixtures unavailable"), "tests/fixtures/M7-tokenizer");
    }

    [Fact]
    public void AllPinnedTokenizerVectorsAndEveryChunkBoundary()
    {
        int vectors = 0, executions = 0;
        var failures = new List<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(FixtureRoot(), "tokenizer"), "*.test").Order(StringComparer.Ordinal))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            // XML infoset coercion is a separate output profile expressly absent from HTML tokens.
            if (!json.RootElement.TryGetProperty("tests", out var tests)) continue;
            int index = 0;
            foreach (var test in tests.EnumerateArray())
            {
                string input = test.GetProperty("input").GetString()!;
                bool escaped = test.TryGetProperty("doubleEscaped", out var e) && e.GetBoolean();
                if (escaped) input = Unescape(input);
                var expected = Expected(test.GetProperty("output"), escaped);
                string lastTag = test.TryGetProperty("lastStartTag", out var last) ? last.GetString()! : "";
                string[] states = test.TryGetProperty("initialStates", out var initial) ? initial.EnumerateArray().Select(v => v.GetString()!).ToArray() : ["Data state"];
                foreach (string state in states)
                {
                    vectors++;
                    for (int split = -2; split <= input.Length; split++)
                    {
                        executions++;
                        var actual = new List<HtmlToken>();
                        var tokenizer = NewTokenizer(state, lastTag, actual.Add);
                        if (split == -2) tokenizer.Feed(input);
                        else if (split == -1) { for (int i = 0; i < input.Length; i++) tokenizer.Feed(input.AsSpan(i, 1)); }
                        else { tokenizer.Feed(input.AsSpan(0, split)); tokenizer.Feed(input.AsSpan(split)); }
                        tokenizer.Complete();
                        Assert.Single(actual, t => t.Kind == HtmlTokenKind.EndOfFile);
                        string output = Actual(actual);
                        if (expected != output)
                        {
                            failures.Add($"{Path.GetFileName(file)}:{index} {state} split={split} {test.GetProperty("description").GetString()}\nExpected {expected}\nActual   {output}");
                            break;
                        }
                    }
                }
                index++;
            }
        }
        Assert.True(failures.Count == 0, $"{vectors} state vectors / {executions} executions; failures:\n" + string.Join("\n", failures.Take(80)) + "\nFailure count=" + failures.Count + "\n" + string.Join("\n", failures.GroupBy(f => f.Split(':')[0]).Select(g => g.Key + ": " + g.Count())));
        Assert.True(vectors > 6000);
    }

    internal static HtmlTokenizer NewTokenizer(string state, string lastTag, Action<HtmlToken> callback)
    {
        var tokenizer = new HtmlTokenizer(callback, new());
        if (state == "CDATA section state") tokenizer.StartCdata();
        else tokenizer.SetState(state switch
        {
            "Data state" => HtmlTokenizerState.Data, "RCDATA state" => HtmlTokenizerState.RcData,
            "RAWTEXT state" => HtmlTokenizerState.RawText, "Script data state" => HtmlTokenizerState.ScriptData,
            "PLAINTEXT state" => HtmlTokenizerState.PlainText,
            _ => throw new InvalidOperationException("Unknown corpus state: " + state),
        }, lastTag);
        return tokenizer;
    }

    [Fact]
    public void PinnedCorpusHashesAndOutputProfilesAreExplicit()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot(), "manifest.json")));
        Assert.Equal("c777c408b61078ea2eb4acefc2535f54dbc8b28a", manifest.RootElement.GetProperty("revision").GetString());
        int resources = 0;
        foreach (var entry in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(FixtureRoot(), entry.GetProperty("path").GetString()!));
            Assert.Equal(entry.GetProperty("bytes").GetInt32(), bytes.Length);
            Assert.Equal(entry.GetProperty("sha256").GetString(), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
            resources++;
        }
        Assert.Equal(21, resources);
        using var xmlProfile = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot(), "tokenizer/xmlViolation.test")));
        Assert.Equal(4, xmlProfile.RootElement.GetProperty("xmlViolationTests").GetArrayLength());
        // Each remains classified as XML-infoset coercion; the HTML API deliberately
        // preserves these names/comments/noncharacters, rather than claiming that profile.
        Assert.Equal("a\uffffb", Html5Tokenizer.Tokenize("a\uffffb")[0].Data);
        Assert.Equal(" --", Html5Tokenizer.Tokenize("<!-- ---->")[0].Data);
    }

    [Fact]
    public void AllDeclaredParseErrorCodesRemainSeparateFromTokens()
    {
        int vectors = 0;
        var failures = new List<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(FixtureRoot(), "tokenizer"), "*.test").Order(StringComparer.Ordinal))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (!json.RootElement.TryGetProperty("tests", out var tests)) continue;
            int index = 0;
            foreach (var test in tests.EnumerateArray())
            {
                string input = test.GetProperty("input").GetString()!;
                if (test.TryGetProperty("doubleEscaped", out var e) && e.GetBoolean()) input = Unescape(input);
                string lastTag = test.TryGetProperty("lastStartTag", out var last) ? last.GetString()! : "";
                string[] states = test.TryGetProperty("initialStates", out var initial) ? initial.EnumerateArray().Select(v => v.GetString()!).ToArray() : ["Data state"];
                foreach (string state in states)
                {
                    vectors++;
                    var actual = new List<string>();
                    var tokenizer = NewTokenizer(state, lastTag, _ => { });
                    tokenizer.ParseError = (code, _) => actual.Add(code);
                    tokenizer.Feed(input); tokenizer.Complete();
                    string[] expected = test.TryGetProperty("errors", out var errors) ? errors.EnumerateArray().Select(v => v.GetProperty("code").GetString()!).ToArray() : [];
                    if (!expected.SequenceEqual(actual))
                        failures.Add($"{Path.GetFileName(file)}:{index} {state} expected={string.Join(',', expected)} actual={string.Join(',', actual)}");
                }
                index++;
            }
        }
        Assert.Equal(7049, vectors);
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(80)) + "\nFailure count=" + failures.Count + "\n" + string.Join("\n", failures.GroupBy(f => f.Split(':')[0]).Select(g => g.Key + ": " + g.Count())));
    }

    [Fact]
    public void IndependentCurrentPiWitnessesAtEveryBoundary()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot(), "pi-witnesses.json")));
        int cases = 0;
        foreach (var test in json.RootElement.GetProperty("tests").EnumerateArray())
        {
            string input = test.GetProperty("input").GetString()!;
            string expected = Expected(test.GetProperty("output"), false);
            for (int split = -1; split <= input.Length; split++)
            {
                var tokens = new List<HtmlToken>(); var tokenizer = NewTokenizer("Data state", "", tokens.Add);
                if (split < 0) { for (int i = 0; i < input.Length; i++) tokenizer.Feed(input.AsSpan(i, 1)); }
                else { tokenizer.Feed(input.AsSpan(0, split)); tokenizer.Feed(input.AsSpan(split)); }
                tokenizer.Complete();
                Assert.True(expected == Actual(tokens), test.GetProperty("description").GetString());
                Assert.Single(tokens, t => t.Kind == HtmlTokenKind.EndOfFile);
            }
            cases++;
        }
        Assert.Equal(18, cases);
    }

    private static string Unescape(string value) => Regex.Replace(value, @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
    private static string Expected(JsonElement output, bool escaped)
    {
        var normalized = new List<object?>();
        foreach (var token in output.EnumerateArray())
        {
            var parts = token.EnumerateArray().ToArray();
            string kind = parts[0].GetString()!;
            string? S(int i) { string? s = parts[i].ValueKind == JsonValueKind.Null ? null : parts[i].GetString(); return s is null || !escaped ? s : Unescape(s); }
            object? value = kind switch
            {
                "DOCTYPE" => new object?[] { kind, S(1), S(2), S(3), parts[4].GetBoolean() },
                "StartTag" => new object?[] { kind, S(1), parts[2].EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new[] { escaped ? Unescape(p.Name) : p.Name, escaped ? Unescape(p.Value.GetString()!) : p.Value.GetString()! }).ToArray(), parts.Length > 3 && parts[3].GetBoolean() },
                "EndTag" => new object?[] { kind, S(1) },
                "Comment" or "Character" => new object?[] { kind, S(1) },
                "ProcessingInstruction" => new object?[] { kind, S(1), S(2) },
                _ => throw new InvalidOperationException("Unknown upstream token: " + kind),
            };
            normalized.Add(value);
        }
        return JsonSerializer.Serialize(normalized);
    }

    internal static string Actual(IEnumerable<HtmlToken> tokens)
    {
        var normalized = new List<object?>();
        var text = new StringBuilder();
        void Flush() { if (text.Length > 0) { normalized.Add(new object[] { "Character", text.ToString() }); text.Clear(); } }
        foreach (var token in tokens)
        {
            if (token.Kind == HtmlTokenKind.Character) { text.Append(token.Data); continue; }
            Flush();
            if (token.Kind == HtmlTokenKind.EndOfFile) continue;
            normalized.Add(token.Kind switch
            {
                HtmlTokenKind.Doctype => new object?[] { "DOCTYPE", token.Name.Length == 0 ? null : token.Name, token.Doctype!.PublicIdentifier, token.Doctype.SystemIdentifier, !token.Doctype.ForceQuirks },
                HtmlTokenKind.StartTag => new object?[] { "StartTag", token.Name, token.Attributes.OrderBy(a => a.LocalName, StringComparer.Ordinal).Select(a => new[] { a.LocalName, a.Value }).ToArray(), token.SelfClosing },
                HtmlTokenKind.EndTag => new object?[] { "EndTag", token.Name },
                HtmlTokenKind.Comment => new object?[] { "Comment", token.Data },
                _ => throw new InvalidOperationException("Unexpected actual token"),
            });
        }
        Flush(); return JsonSerializer.Serialize(normalized);
    }
}
