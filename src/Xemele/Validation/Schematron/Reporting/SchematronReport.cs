using System.Buffers;
using System.Text;

namespace Xemele.Validation;

/// <summary>Serializes detached Schematron results using the frozen SVRL profile.</summary>
public static class SchematronReport
{
    /// <summary>Serializes a detached result as SVRL XML text using the supported reporting profile.</summary>
    /// <param name="result">The result whose active patterns, rule firings, and findings are serialized.</param>
    /// <param name="cancellationToken">The token used to cancel serialization.</param>
    /// <returns>The SVRL XML text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentException">A result string contains an illegal XML scalar.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static string SerializeSvrl(SchematronResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        using var output = new Output(null, cancellationToken);
        Write(result, output, cancellationToken);
        return output.Result();
    }

    /// <summary>Writes a detached result as UTF-8 SVRL XML using the supported reporting profile.</summary>
    /// <param name="result">The result to serialize.</param>
    /// <param name="output">The writable destination stream, which is left open.</param>
    /// <param name="cancellationToken">The token used to cancel serialization.</param>
    /// <remarks>Writing is not transactional; a failure or cancellation can leave partial output.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is not writable or a result string contains an illegal XML scalar.</exception>
    /// <exception cref="IOException">Writing to the stream fails.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    public static void SaveSvrl(SchematronResult result, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("Stream must be writable.", nameof(output));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var sink = new Output(output, cancellationToken);
        Write(result, sink, cancellationToken);
        sink.Complete();
    }

    private static void Write(SchematronResult result, Output output, CancellationToken token)
    {
        // Operation-owned indices preserve collection order without quadratic joins.
        var events = new Dictionary<int, List<SchematronFiredRule>>();
        foreach (var fired in result.FiredRules)
        {
            token.ThrowIfCancellationRequested();
            if (!events.TryGetValue(fired.PatternKey, out var rows))
            {
                events.Add(fired.PatternKey, rows = []);
            }

            rows.Add(fired);
        }
        var findings = new Dictionary<int, List<SchematronFinding>>();
        foreach (var finding in result.Findings)
        {
            token.ThrowIfCancellationRequested();
            if (!findings.TryGetValue(finding.FiredRuleKey, out var rows))
            {
                findings.Add(finding.FiredRuleKey, rows = []);
            }

            rows.Add(finding);
        }
        var namespaces = new SortedDictionary<string, string>(new PrefixComparer(token));
        foreach (var pair in result.Namespaces)
        {
            token.ThrowIfCancellationRequested();
            namespaces.Add(pair.Key, pair.Value);
        }

        output.Raw("<svrl:schematron-output xmlns:svrl=\"http://purl.oclc.org/dsdl/svrl\"");
        output.Attribute("phase", result.Phase);
        output.Attribute("title", result.Title);
        output.Raw(">");
        foreach (var pair in namespaces)
        {
            output.Raw("<svrl:ns-prefix-in-attribute-values");
            output.Attribute("prefix", pair.Key);
            output.Attribute("uri", pair.Value);
            output.Raw("/>");
        }
        foreach (var pattern in result.ActivePatterns)
        {
            token.ThrowIfCancellationRequested();
            output.Raw("<svrl:active-pattern");
            output.Attribute("id", pattern.Id);
            output.Attribute("name", pattern.Name);
            output.Raw("/>");
            if (!events.TryGetValue(pattern.Key, out var firedRows))
            {
                continue;
            }

            foreach (var fired in firedRows)
            {
                token.ThrowIfCancellationRequested();
                output.Raw("<svrl:fired-rule");
                output.Attribute("context", fired.Context);
                output.Raw("/>");
                if (!findings.TryGetValue(fired.Key, out var findingRows))
                {
                    continue;
                }

                foreach (var finding in findingRows)
                {
                    token.ThrowIfCancellationRequested();
                    var assertion = finding.Kind == SchematronFindingKind.FailedAssert;
                    output.Raw(assertion ? "<svrl:failed-assert" : "<svrl:successful-report");
                    output.Attribute("test", finding.Test);
                    output.Attribute("location", finding.Location);
                    output.Attribute("id", finding.Id);
                    output.Attribute("role", finding.Role);
                    output.Attribute("flag", finding.Flag);
                    output.Raw(">");
                    output.Raw("<svrl:text>");
                    output.Escape(finding.Text, false);
                    output.Raw("</svrl:text>");
                    foreach (var diagnostic in finding.Diagnostics)
                    {
                        output.Raw("<svrl:diagnostic-reference");
                        output.Attribute("diagnostic", diagnostic.Id);
                        output.Raw(">");
                        output.Escape(diagnostic.Text, false);
                        output.Raw("</svrl:diagnostic-reference>");
                    }
                    foreach (var property in finding.Properties)
                    {
                        output.Raw("<svrl:property-reference");
                        output.Attribute("property", property.Id);
                        output.Raw(">");
                        output.Escape(property.Text, false);
                        output.Raw("</svrl:property-reference>");
                    }
                    output.Raw(assertion ? "</svrl:failed-assert>" : "</svrl:successful-report>");
                }
            }
        }
        output.Raw("</svrl:schematron-output>");
        token.ThrowIfCancellationRequested();
    }

    private sealed class PrefixComparer(CancellationToken token) : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            ReadOnlySpan<char> left = x, right = y;
            var length = Math.Min(left.Length, right.Length);
            for (var offset = 0; offset < length;)
            {
                token.ThrowIfCancellationRequested();
                var count = Math.Min(2048, length - offset);
                var order = left.Slice(offset, count).SequenceCompareTo(right.Slice(offset, count));
                if (order != 0)
                {
                    return order;
                }

                offset += count;
            }
            token.ThrowIfCancellationRequested();
            return left.Length.CompareTo(right.Length);
        }
    }

    // Fixed profile markup is static. All caller fields cross the XML scalar/escape
    // boundary. The scalar loop is a correctness baseline, with no speed claim.
    private sealed class Output : IDisposable
    {
        private readonly StringBuilder? builder;
        private readonly WriterOutput? bytes;
        private readonly CancellationToken token;
        internal Output(Stream? stream, CancellationToken token)
        {
            this.token = token;
            if (stream is null)
            {
                builder = new();
            }
            else
            {
                bytes = new(stream, 65001, token);
            }
        }
        internal void Raw(ReadOnlySpan<char> text)
        {
            token.ThrowIfCancellationRequested();
            if (bytes is not null)
            {
                bytes.Write(text);
                return;
            }
            while (!text.IsEmpty)
            {
                token.ThrowIfCancellationRequested();
                var count = Math.Min(text.Length, 2048);
                builder!.Append(text[..count]);
                text = text[count..];
            }
        }
        internal void Attribute(string name, string? value)
        {
            if (value is null)
            {
                return;
            }

            Raw(" ");
            Raw(name);
            Raw("=\"");
            Escape(value, true);
            Raw("\"");
        }
        internal void Escape(ReadOnlySpan<char> text, bool attribute)
        {
            int run = 0, brackets = 0, polled = 0;
            for (var offset = 0; offset < text.Length;)
            {
                if ((polled++ & 1023) == 0)
                {
                    token.ThrowIfCancellationRequested();
                }

                if (Rune.DecodeFromUtf16(text[offset..], out var rune, out var width) != OperationStatus.Done ||
                    !(rune.Value is 9 or 10 or 13 or >= 0x20 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000 and <= 0x10ffff))
                {
                    throw new ArgumentException("Illegal XML scalar.", nameof(text));
                }

                var c = rune.Value;
                ReadOnlySpan<char> replacement = c switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '"' when attribute => "&quot;",
                    9 when attribute => "&#x9;",
                    10 when attribute => "&#xA;",
                    13 => "&#xD;",
                    '>' when !attribute && brackets >= 2 => "&gt;",
                    _ => default
                };
                if (!replacement.IsEmpty)
                {
                    Raw(text[run..offset]);
                    Raw(replacement);
                    run = offset + width;
                }
                if (!attribute)
                {
                    brackets = c == ']' ? Math.Min(2, brackets + 1) : 0;
                }

                offset += width;
                if (offset - run >= 2048)
                {
                    Raw(text[run..offset]);
                    run = offset;
                }
            }
            Raw(text[run..]);
        }
        internal string Result()
        {
            token.ThrowIfCancellationRequested();
            var result = builder!.ToString();
            token.ThrowIfCancellationRequested();
            return result;
        }
        internal void Complete()
        {
            bytes!.Complete();
            token.ThrowIfCancellationRequested();
        }
        public void Dispose() => bytes?.Dispose();
    }
}
