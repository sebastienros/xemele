using System.Text;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Input;
using Xunit;

namespace Xemele.Tests.XmlPerformance;

public sealed class PerformanceTests
{
    [Fact]
    public void ShortValueCacheRequiresExactEqualityAndOwnsInput()
    {
        var cache = new Xemele.Parsing.Dom.DomSink.ShortValueCache();
        var input = "axcze".ToCharArray();
        var first = cache.Own(input);
        Assert.Same(first, cache.Own("axcze"));
        // Same length and sampled characters, different unsampled character.
        var collision = cache.Own("aycze");
        Assert.Equal("aycze", collision);
        Assert.NotSame(first, collision);
        input.AsSpan().Fill('!');
        Assert.Equal("axcze", first);
        Assert.Equal("axcze", cache.Own("axcze"));
        var unicode = cache.Own("α日本語😀");
        Assert.Same(unicode, cache.Own("α日本語😀"));
        string owned = new('x', 32);
        Assert.Same(owned, cache.Own(owned, owned));
        Assert.Same(owned, cache.Own(new string('x', 32)));
        string longOwned = new('x', 33);
        Assert.Same(longOwned, cache.Own(longOwned, longOwned));
        Assert.NotSame(cache.Own(longOwned), cache.Own(longOwned));
        Assert.Same(string.Empty, cache.Own([]));
    }
    [Fact]
    public void RepeatedDomValuesRemainOwnedAndQuotasCountEveryOccurrence()
    {
        var xml = "<r>" + string.Concat(Enumerable.Repeat("<n a='USD'>USD</n>", 20)) + "</r>";
        var input = Encoding.UTF8.GetBytes(xml);
        var document = XmlDocument.Parse(input, cancellationToken: TestContext.Current.CancellationToken);
        input.AsSpan().Fill((byte)'!');
        var first = document.RootElement.Children[0];
        var last = document.RootElement.Children[19];
        Assert.Same(first.GetAttribute("a"), last.GetAttribute("a"));
        Assert.Equal("USD", last.GetAttribute("a"));
        Assert.Equal("USD", last.GetTextContent());
        var separate = XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotSame(first.GetAttribute("a"), separate.RootElement.Children[0].GetAttribute("a"));
        Assert.False(XmlDocument.TryParse(xml, out _, out var diagnostics,
            new()
            {
                Limits = new()
                {
                    DocumentCharacters = xml.Length - 1
                }
            }, TestContext.Current.CancellationToken));
        Assert.Equal("XML.ResourceLimit", diagnostics[0].Code);
    }
    [Fact]
    public void WarmShortValueCacheHitsAllocateZero()
    {
        var cache = new Xemele.Parsing.Dom.DomSink.ShortValueCache();
        var owned = cache.Own("USD");
        for (var i = 0; i < 100; i++)
        {
            cache.Own("USD");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            if (!ReferenceEquals(owned, cache.Own("USD")))
            {
                throw new InvalidOperationException();
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(8193)]
    public void BulkTextPreservesUnicodeNormalizationAcrossDecoderPages(int length)
    {
        var xml = "<r>" + new string('x', length) + "α日本語😀&amp;\r\n<![CDATA[tail]]>\u007f</r>";
        var expected = new System.Xml.XmlDocument { PreserveWhitespace = true };
        using var reader = System.Xml.XmlReader.Create(new StringReader(xml), new System.Xml.XmlReaderSettings { IgnoreWhitespace = false });
        expected.Load(reader);
        var actual = XmlDocument.Parse(Encoding.UTF8.GetBytes(xml), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected.DocumentElement!.InnerText, actual.RootElement.GetTextContent());
        Assert.Equal(actual.RootElement.GetTextContent(), XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken).RootElement.GetTextContent());
    }
    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(4096)]
    public void BulkQuotaStopsAtTheFirstExcessScalar(int limit)
    {
        var xml = "<r>" + new string('x', limit) + "y</r>";
        Assert.False(XmlDocument.TryParse(xml, out _, out var diagnostics, new()
        {
            Limits = new()
            {
                TokenCharacters = limit
            }
        }, TestContext.Current.CancellationToken));
        if (limit >= 3)
        {
            Assert.Equal(limit + 4, diagnostics[0].Location.LinePosition);
        }

        Assert.Equal("XML.ResourceLimit", diagnostics[0].Code);
    }
    [Fact]
    public void PausedBorrowedRunConsumesOnlyItsAcceptedPrefixAndRevalidatesTheTail()
    {
        using var core = new XmlParserCore(new());
        var sink = new BulkSink { Capture = true, PauseRun = true };
        var input = "<r>abcdef</r>".ToCharArray();
        long[] offsets = [.. Enumerable.Range(0, input.Length).Select(x => (long)x)];
        var cancel = TestContext.Current.CancellationToken;
        var paused = core.Append(input, offsets, false, sink, cancel);
        Assert.Equal(ScanStep.Paused, paused.Step);
        Assert.Equal(9, paused.Consumed);
        Assert.Equal("abcdef", sink.Captured.ToString());
        input.AsSpan(0, paused.Consumed).Fill('#');
        offsets[^1] = -1;
        core.CompleteByteSource(input.Length);
        Assert.Throws<ArgumentException>(() => core.Append(input.AsSpan(paused.Consumed), offsets.AsSpan(paused.Consumed), true, sink, cancel));
        Assert.Equal("abcdef", sink.Captured.ToString());
    }
    [Fact]
    public void PreparedBorrowedTextDispatchAllocatesZero()
    {
        using var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null
            }
        });
        var sink = new BulkSink();
        var cancel = TestContext.Current.CancellationToken;
        string run = new('x', 4096);
        core.Append("<r>", [], false, sink, cancel);
        for (var i = 0; i < 20; i++)
        {
            core.Append(run, [], false, sink, cancel);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            var result = core.Append(run, [], false, sink, cancel);
            if (result.Step != ScanStep.NeedInput || result.Consumed != run.Length)
            {
                throw new InvalidOperationException("Unexpected borrowed dispatch state");
            }
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(1020L * 4096, sink.Units);
        Assert.True(sink.Calls < 1100);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancel).Step);
    }
    [Fact]
    public void PreparedRepeatedTagAndNameDispatchAllocatesZero()
    {
        using var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null
            }
        });
        var sink = new BulkSink();
        var cancel = TestContext.Current.CancellationToken;
        const string tag = "<n a='value'/>";
        core.Append("<r>", [], false, sink, cancel);
        for (var i = 0; i < 20; i++)
        {
            core.Append(tag, [], false, sink, cancel);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            if (core.Append(tag, [], false, sink, cancel).Consumed != tag.Length)
            {
                throw new InvalidOperationException("Incomplete tag dispatch");
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancel).Step);
    }
    [Fact]
    public void PageBoundariesKeepAllSlotsAndBoundsStable()
    {
        var arena = new SlotArena<int>();
        arena.EnsureCapacity(4097);
        for (var i = 0; i < 5000; i++)
        {
            arena.Add(i * 7);
        }

        foreach (var i in new[] { 0, 3, 1023, 1024, 2047, 2048, 4095, 4096, 4999 })
        {
            Assert.Equal(i * 7, arena[i]);
        }

        Assert.Equal(5000, arena.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = arena[5000]);
    }
    [Theory]
    [InlineData(4294967294L)]
    [InlineData(4294967295L)]
    [InlineData(4294967296L)]
    [InlineData(long.MaxValue)]
    public void CompactCoordinatesAndSparseOverflowRemainLossless(long offset)
    {
        var document = new XmlDocument();
        var root = document.CreateElement("r");
        var origin = new Uri("urn:source");
        document.RecordSourceOrigin(new(origin, 0, 0, null));
        var expected = new XmlLocation(origin, int.MaxValue, int.MaxValue, offset);
        document.RecordSourceLocation(root, expected);
        document.MarkValidationSourceComplete();
        Assert.Equal(expected, document.OriginalLocation(root));
        Assert.Equal(new(origin, 0, 0, null), document.OriginalLocation(document.DocumentNode));
    }
    [Fact]
    public void OwnedDomSurvivesInputMutationAndScratchReuse()
    {
        var input = Encoding.UTF8.GetBytes("<r a='v'>α😀&amp;</r>");
        var first = XmlDocument.Parse(input, cancellationToken: TestContext.Current.CancellationToken);
        input.AsSpan().Fill((byte)'!');
        for (var i = 0; i < 32; i++)
        {
            _ = XmlDocument.Parse("<r>" + new string('z', 5000) + "</r>", cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Equal("α😀&", first.RootElement.GetTextContent());
        Assert.Equal("v", first.RootElement.GetAttribute("a"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrustedUtf8AndUtf16TagRunsKeepExactMemberOffsets(bool utf16)
    {
        var xml = "<r>α😀&amp;<n first='ordinary' second='x&amp;z'\n third='v'/></r>";
        var encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        var preamble = utf16 ? encoding.GetPreamble() : [];
        byte[] bytes = [.. preamble, .. encoding.GetBytes(xml)];
        var doc = XmlDocument.Parse(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var node = doc.RootElement.Children[1];
        foreach (var attribute in node.Attributes)
        {
            var index = xml.IndexOf(attribute.LocalName + "=", StringComparison.Ordinal);
            var line = xml.AsSpan(0, index).Count('\n') + 1;
            int beginning = xml.LastIndexOf('\n', index) + 1, column = 1;
            foreach (var rune in xml.AsSpan(beginning, index - beginning).EnumerateRunes())
            {
                column++;
            }

            Assert.Equal(new XmlLocation(null, line, column, preamble.Length + encoding.GetByteCount(xml.AsSpan(0, index))), doc.OriginalLocation(attribute));
        }
        Assert.Equal("x&z", node.GetAttribute("second"));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PredefinedReferenceExpansionQuotaKeepsTheFailingReferenceLocation(int expansions)
    {
        const string xml = "<r>x&amp;&lt;&quot;&apos;</r>";
        Assert.False(XmlDocument.TryParse(xml, out _, out var diagnostics,
            new()
            {
                Limits = new()
                {
                    ExpandedCharacters = expansions
                }
            }, TestContext.Current.CancellationToken));
        int[] columns = [10, 14, 20];
        Assert.Equal(columns[expansions - 1], diagnostics[0].Location.LinePosition);
    }
    [Fact]
    public void RepeatedOwnedNamesAndEqualDistinctCallerStringsRespectInternQuota()
    {
        var doc = new XmlDocument(new()
        {
            InternedNames = 3
        });
        var first = doc.CreateElement(new string("n".AsSpan()), new string("urn:n".AsSpan()), new string("p".AsSpan()));
        var repeated = doc.CreateElement(new string("n".AsSpan()), new string("urn:n".AsSpan()), new string("p".AsSpan()));
        Assert.Same(first.LocalName, repeated.LocalName);
        Assert.Same(first.NamespaceUri, repeated.NamespaceUri);
        Assert.Throws<InvalidOperationException>(() => doc.CreateElement("fourth"));
    }
    [Fact]
    public void CollidingNameCachesPreserveExactNamesAndDistinctNameQuota()
    {
        string[] names = [.. Enumerable.Range(0, 32).Select(i => "item" + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture)), "名前α", "名前β", "a", "A"];
        var xml = "<r>" + string.Concat(Enumerable.Range(0, 4).SelectMany(_ => names.Select(n => "<" + n + "/>"))) + "</r>";
        var document = XmlDocument.Parse(xml, new()
        {
            Limits = new()
            {
                InternedNames = names.Length + 1
            }
        }, TestContext.Current.CancellationToken);
        for (var i = 0; i < names.Length * 4; i++)
        {
            Assert.Equal(names[i % names.Length], document.RootElement.Children[i].LocalName);
            if (i >= names.Length)
            {
                Assert.Same(document.RootElement.Children[i % names.Length].LocalName, document.RootElement.Children[i].LocalName);
            }
        }
        Assert.False(XmlDocument.TryParse(xml, out _, out var diagnostics, new()
        {
            Limits = new()
            {
                InternedNames = names.Length
            }
        }, TestContext.Current.CancellationToken));
        Assert.Equal("XML.ResourceLimit", diagnostics[0].Code);
    }
    [Fact]
    public void PreparedCollidingNamesDispatchAllocatesZero()
    {
        string[] names = [.. Enumerable.Range(0, 32).Select(i => "item" + i.ToString("D8", System.Globalization.CultureInfo.InvariantCulture))];
        var tags = string.Concat(names.Select(n => "<" + n + "/>"));
        using var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null
            }
        });
        var sink = new BulkSink();
        var cancel = TestContext.Current.CancellationToken;
        core.Append("<r>", [], false, sink, cancel);
        sink.ExpectedNames = names;
        for (var i = 0; i < 20; i++)
        {
            core.Append(tags, [], false, sink, cancel);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            if (core.Append(tags, [], false, sink, cancel).Consumed != tags.Length)
            {
                throw new InvalidOperationException("Incomplete colliding-name dispatch");
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancel).Step);
    }
    [Fact]
    public void CompletePrimaryTagPreflightKeepsFirstExcessByteAndScalarLocations()
    {
        var bytes = "<r><n a='v'/></r>"u8.ToArray();
        for (var maximum = 1; maximum < bytes.Length; maximum++)
        {
            Assert.False(XmlDocument.TryParse(bytes.AsSpan(), out _, out var diagnostics,
                new()
                {
                    Limits = new()
                    {
                        DocumentCharacters = maximum
                    }
                }, TestContext.Current.CancellationToken));
            Assert.Equal("XML.ResourceLimit", diagnostics[0].Code);
            Assert.Equal(maximum + 1, diagnostics[0].Location.LinePosition);
            Assert.Equal(maximum, diagnostics[0].Location.ByteOffset);
        }
        var document = XmlDocument.Parse(bytes.AsSpan(), new()
        {
            Limits = new()
            {
                DocumentCharacters = bytes.Length
            }
        }, TestContext.Current.CancellationToken);
        Assert.Equal(new XmlLocation(null, 1, 4, 3), document.OriginalLocation(document.RootElement.Children[0]));
    }
    [Fact]
    public void InternalPredefinedReferencesKeepScalarQuotaAccountingAndFailures()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY e \"prefix&amp;&lt;&gt;&apos;&quot;suffix\">]><r>&e;</r>";
        for (var maximum = 1; maximum <= 60; maximum++)
        {
            var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Limits = new() { ExpandedCharacters = maximum } };
            var handler = new TextCapture();
            var scalar = Xemele.Streaming.XmlSaxParser.ParseWithDiagnostics(xml, handler, options, TestContext.Current.CancellationToken);
            var parsed = XmlDocument.TryParse(Encoding.UTF8.GetBytes(xml).AsSpan(), out var document, out var diagnostics, options, TestContext.Current.CancellationToken);
            Assert.Equal(scalar.IsWellFormed, parsed);
            if (parsed)
            {
                Assert.Equal(handler.Value.ToString(), document!.RootElement.GetTextContent());
            }
            else
            {
                Assert.Equal(scalar.Diagnostics[0].Code, diagnostics[0].Code);
                Assert.Equal(scalar.Diagnostics[0].Location.LineNumber, diagnostics[0].Location.LineNumber);
                Assert.Equal(scalar.Diagnostics[0].Location.LinePosition, diagnostics[0].Location.LinePosition);
            }
        }
    }
    [Fact]
    public void InternalPredefinedReferenceKeepsEmptyContentValidationProvenance()
    {
        const string xml = "<!DOCTYPE r [<!ELEMENT r EMPTY><!ENTITY e \"prefix&amp;suffix\">]><r>&e;</r>";
        var document = XmlDocument.Parse(xml, new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, TestContext.Current.CancellationToken);
        var result = Xemele.Validation.DtdSchema.FromDocument(document).Validate(document);
        Assert.False(result.IsValid);
        Assert.True(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DTD.Content" && diagnostic.Location.LinePosition == xml.IndexOf("&e;", StringComparison.Ordinal) + 1);
    }
    [Fact]
    public void PrimaryScalarReferenceCandidatesMatchFullScalarParsing()
    {
        string[] references = ["&#945;", "&#x3B1;", "&#32;", "&#13;", "&#x1F600;", "&#000000000000000000000945;", "&#xD800;", "&#0;", "&#xZZ;", "&#X41;", "&#;", "&#123", "&amp;", "&bogus;"];
        foreach (var dtd in new[] { false, true })
        {
            foreach (var reference in references)
            {
                foreach (var maximum in new[] { 1, 2, 6 })
                {
                    var xml = (dtd ? "<!DOCTYPE r [<!ELEMENT r ANY>]>" : "") + "<r>prefix" + reference + "suffix</r>";
                    var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Limits = new() { ExpandedCharacters = maximum } };
                    var handler = new TextCapture();
                    var scalar = Xemele.Streaming.XmlSaxParser.ParseWithDiagnostics(xml, handler, options, TestContext.Current.CancellationToken);
                    var parsed = XmlDocument.TryParse(Encoding.UTF8.GetBytes(xml).AsSpan(), out var document, out var diagnostics, options, TestContext.Current.CancellationToken);
                    Assert.Equal(scalar.IsWellFormed, parsed);
                    if (parsed)
                    {
                        Assert.Equal(handler.Value.ToString(), document!.RootElement.GetTextContent());
                    }
                    else
                    {
                        Assert.Equal(scalar.Diagnostics[0].Code, diagnostics[0].Code);
                        Assert.Equal(scalar.Diagnostics[0].Location.LineNumber, diagnostics[0].Location.LineNumber);
                        Assert.Equal(scalar.Diagnostics[0].Location.LinePosition, diagnostics[0].Location.LinePosition);
                    }
                }
            }
        }
    }
    [Fact]
    public void PreparedNumericReferenceDispatchAllocatesZero()
    {
        using var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null,
                ExpandedCharacters = null
            }
        });
        var sink = new BulkSink();
        var cancel = TestContext.Current.CancellationToken;
        core.Append("<r>prefix", [], false, sink, cancel);
        for (var i = 0; i < 20; i++)
        {
            core.Append("&#945;", [], false, sink, cancel);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            if (core.Append("&#945;", [], false, sink, cancel).Consumed != 6)
            {
                throw new InvalidOperationException("Incomplete reference dispatch");
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(1026, sink.Units);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancel).Step);
    }
    [Theory]
    [InlineData("a")]
    [InlineData("α")]
    [InlineData("😀")]
    [InlineData(">")]
    [InlineData("&amp;")]
    [InlineData("&#13;")]
    [InlineData("x&amp;y")]
    public void SingleOwnedTextFragmentsPreserveValueAndSourceAnchor(string body)
    {
        var xml = "<r>" + body + "</r>";
        var reference = new System.Xml.XmlDocument { PreserveWhitespace = true };
        using var reader = System.Xml.XmlReader.Create(new StringReader(xml), new System.Xml.XmlReaderSettings { IgnoreWhitespace = false });
        reference.Load(reader);
        var actual = XmlDocument.Parse(Encoding.UTF8.GetBytes(xml).AsSpan(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(reference.DocumentElement!.InnerText, actual.RootElement.GetTextContent());
        Assert.Equal(new XmlLocation(null, 1, 4, 3), actual.OriginalLocation(actual.RootElement.FirstChild));
    }
    [Theory]
    [InlineData("abc\r\ndef\rghi\nj", 1, 1)]
    [InlineData("a\r\nb", 2147483646, 2147483646)]
    [InlineData("ab", 1, 2147483646)]
    [InlineData("\r\nx", 2147483646, 1)]
    [InlineData("\n\n", 2147483647, 1)]
    public void AffineAsciiDecoderPreservesNewlinesAndLocationLimits(string body, int line, int column)
    {
        var cancel = TestContext.Current.CancellationToken;
        using var ordinary = new XmlInputDecoder(new());
        using var affine = new XmlInputDecoder(new());
        var chars = new char[32];
        var mappings = new long[32];
        foreach (var decoder in new[] { ordinary, affine })
        {
            Assert.Equal(4, decoder.Decode("<r>!"u8, chars, mappings, false, cancel).ConsumedBytes);
            Assert.Null(decoder.ValidateDeclaration([], new(null, 1, 1, 0)));
            var location = typeof(XmlInputDecoder).GetField("location", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(decoder)!;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            location.GetType().GetField("line", flags)!.SetValue(location, line);
            location.GetType().GetField("column", flags)!.SetValue(location, column);
        }
        var bytes = Encoding.ASCII.GetBytes(body);
        var expected = ordinary.Decode(bytes, chars, mappings, false, cancel);
        string expectedText = new(chars, 0, expected.WrittenCharacters);
        var firstCount = affine.DecodeAscii(bytes, chars, out var first, cancel);
        Assert.Equal(4, first);
        string actualText = new(chars, 0, firstCount);
        DecodeResult tail = default;
        if (firstCount < bytes.Length)
        {
            tail = affine.Decode(bytes.AsSpan(firstCount), chars, mappings, false, cancel);
            actualText += new string(chars, 0, tail.WrittenCharacters);
        }
        Assert.Equal(expectedText, actualText);
        Assert.Equal(expected.ConsumedBytes, firstCount + tail.ConsumedBytes);
        Assert.Equal(expected.Diagnostic, tail.Diagnostic);
        if (expected.Step != DecodeStep.InvalidInput)
        {
            Assert.Equal(ordinary.Decode([0xff], chars, mappings, false, cancel).Diagnostic,
                affine.Decode([0xff], chars, mappings, false, cancel).Diagnostic);
        }
    }
    [Fact]
    public void PreparedAffineAsciiDispatchAllocatesZero()
    {
        using var core = new XmlParserCore(new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null
            }
        }, trustedByteMappings: true);
        var sink = new BulkSink();
        var cancel = TestContext.Current.CancellationToken;
        core.AppendAscii("<r>", 0, sink, cancel);
        string run = new('x', 4096);
        long offset = 3;
        for (var i = 0; i < 20; i++)
        {
            core.AppendAscii(run, offset, sink, cancel);
            offset += run.Length;
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            var result = core.AppendAscii(run, offset, sink, cancel);
            offset += run.Length;
            if (result.Step != ScanStep.NeedInput || result.Consumed != run.Length)
            {
                throw new InvalidOperationException("Incomplete affine run.");
            }
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        core.AppendAscii("</r>", offset, sink, cancel);
        core.CompleteByteSource(offset + 4);
        Assert.Equal(ScanStep.Completed, core.Append([], [], true, sink, cancel).Step);
    }
    [Fact]
    public void PreparedAffineAsciiDecodingAllocatesZero()
    {
        var cancel = TestContext.Current.CancellationToken;
        using var decoder = new XmlInputDecoder(new());
        var chars = new char[4096];
        var mappings = new long[4096];
        Assert.Equal(4, decoder.Decode("<r>!"u8, chars, mappings, false, cancel).ConsumedBytes);
        Assert.Null(decoder.ValidateDeclaration([], new(null, 1, 1, 0)));
        var run = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("abc\r\n", 819)));
        for (var i = 0; i < 20; i++)
        {
            decoder.DecodeAscii(run, chars, out _, cancel);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            if (decoder.DecodeAscii(run, chars, out _, cancel) != run.Length)
            {
                throw new InvalidOperationException("Incomplete ASCII decode.");
            }
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4095)]
    [InlineData(4096)]
    public void AffineAsciiDomPreservesByteAnchorsAcrossNewlineAndUnicodeTransitions(int padding)
    {
        var xml = "<r>α😀" + new string('x', padding) + "\r\n<n a='v'>α😀</n>\r<t/>\n</r>";
        var input = Encoding.UTF8.GetBytes(xml);
        var actual = XmlDocument.Parse(input.AsSpan(), cancellationToken: TestContext.Current.CancellationToken);
        var n = actual.RootElement.FirstChild.NextSibling;
        var start = xml.IndexOf("<n", StringComparison.Ordinal);
        Assert.Equal(new XmlLocation(null, 2, 1, Encoding.UTF8.GetByteCount(xml.AsSpan(0, start))), actual.OriginalLocation(n));
        Assert.Equal("α😀", n.GetTextContent());
        Assert.Equal(new XmlLocation(null, 2, 4, Encoding.UTF8.GetByteCount(xml.AsSpan(0, start + 3))), actual.OriginalLocation(n.Attributes[0]));
    }
    private sealed class TextCapture : Xemele.Streaming.XmlSaxHandler
    {
        internal readonly StringBuilder Value = new();
        public override void Text(ReadOnlySpan<char> text) => Value.Append(text);
    }
    private sealed class BulkSink : IXmlBulkTextSink
    {
        internal string[]? ExpectedNames; private int nameIndex;
        internal bool Capture, PauseRun; internal long Units, Calls; internal readonly StringBuilder Captured = new();
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView value) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
        {
            if (ExpectedNames is { } expected && !name.LocalName.SequenceEqual(expected[nameIndex++ % expected.Length]))
            {
                throw new InvalidOperationException("Incorrect interned name.");
            }

            return true;
        }
        public bool NamespaceDeclaration(XmlNamespaceView value) => true;
        public bool Attribute(XmlAttributeView value) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location)
        {
            Units += text.Length;
            Calls++;
            if (Capture)
            {
                Captured.Append(text);
            }

            if (PauseRun && text.Length >= 2)
            {
                PauseRun = false;
                return false;
            }
            return true;
        }
        public bool EndDocument(XmlLocation location) => true;
    }
}
