using System.Text;
using Xemele.Parsing.Dtd;
using Xunit;

namespace Xemele.Tests;

public sealed class ValueTests
{
    private static readonly (string Text, string? Expected)[] Cases =
    [ ("", ""), ("abc<\t\r\n😀", "abc<\t\r\n😀"), ("&#65;&#x1F600;", "A😀"),
      ("&#000000000009;", "\t"), ("&#x00000000000a;", "\n"), ("&lt;&中;&😀;", "&lt;&中;&😀;"),
      ("&#38;lt;", "&lt;"), ("&#13;\n", "\r\n"), ("&", null), ("&#", null), ("&#x", null),
      ("&#;", null), ("&#x;", null), ("&#X41;", null), ("&#0;", null), ("&#xD800;", null),
      ("&#55296;", null), ("&#xFFFE;", null), ("&#1114112;", null), ("&#x110000;", null),
      ("&#9999999999999999999999;", null), ("&#xFFFFFFFFFFFFFFFFFF;", null), ("&#-1;", null),
      ("&1a;", null), ("&a b;", null), ("\uD800", null), ("\uDC00", null), ("\uD800x", null), ("\0", null) ];
    public static IEnumerable<object[]> Splits()
    {
        for (var id = 0; id < Cases.Length; id++)
        {
            for (var split = 0; split <= Cases[id].Text.Length; split++)
            {
                for (var output = 2; output <= 5; output++)
                {
                    yield return [id, split, output];
                }
            }
        }
    }
    [Theory, MemberData(nameof(Splits))]
    public void EveryInputAndOutputSplit(int id, int split, int capacity)
    {
        var (text, expected) = Cases[id];
        using var decoder = new DtdValueDecoder(new());
        var result = new StringBuilder();
        var output = new char[capacity];
        var accepted = 0;
        DtdValueResult Drain(ReadOnlySpan<char> input, bool final, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (true)
            {
                var r = decoder.Decode(input[offset..], output, final, cancellationToken);
                Assert.InRange(r.Consumed, 0, input.Length - offset);
                Assert.InRange(r.Written, 0, capacity);
                result.Append(output.AsSpan(0, r.Written));
                offset += r.Consumed;
                accepted += r.Consumed;
                if (r.Step != DtdValueStep.NeedOutput)
                {
                    if (r.Step == DtdValueStep.NeedInput)
                    {
                        Assert.Equal(input.Length, offset);
                    }

                    return r;
                }
                Assert.True(r.Consumed > 0 || r.Written > 0);
            }
        }
        var first = Drain(text.AsSpan(0, split), false, TestContext.Current.CancellationToken);
        var last = first.Step == DtdValueStep.Rejected ? first : Drain(text.AsSpan(split), true, TestContext.Current.CancellationToken);
        Assert.Equal(expected is null ? DtdValueStep.Rejected : DtdValueStep.Completed, last.Step);
        if (expected is not null)
        {
            Assert.Equal(expected, result.ToString());
            Assert.Equal(text.Length, accepted);
            Assert.Null(last.Diagnostic);
        }
        else
        {
            Assert.Equal("DTD.InvalidValue", last.Diagnostic!.Code);
        }

        Assert.Throws<InvalidOperationException>(() => decoder.Decode([], output, true, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void LongLeadingZerosProgressAndBorrowedLifetime()
    {
        using var d = new DtdValueDecoder(new()
        {
            TokenCharacters = 1
        });
        var output = new char[2];
        var input = new string('0', 8192).ToCharArray();
        Assert.Equal(2, d.Decode("&#", output, false, TestContext.Current.CancellationToken).Consumed);
        for (var i = 0; i < 4; i++)
        {
            var r = d.Decode(input, output, false, TestContext.Current.CancellationToken);
            Assert.Equal(input.Length, r.Consumed);
            Assert.Equal(0, r.Written);
            Assert.Equal(DtdValueStep.NeedInput, r.Step);
        }
        Array.Fill(input, 'x');
        var end = d.Decode("9;", output, true, TestContext.Current.CancellationToken);
        Assert.Equal(DtdValueStep.Completed, end.Step);
        Assert.Equal('\t', output[0]);
        using var pair = new DtdValueDecoder(new());
        input = ['\uD83D'];
        pair.Decode(input, output, false, TestContext.Current.CancellationToken);
        input[0] = 'x';
        Assert.Equal(2, pair.Decode("\uDE00", output, true, TestContext.Current.CancellationToken).Written);
        Assert.Equal("😀", new string(output));
    }
    private static Dictionary<string, DtdEntityDeclaration> Table(params (string Name, string Text)[] entries) => entries.ToDictionary(x => x.Name, x => new DtdEntityDeclaration(x.Name, false, x.Text, null, null, null, false, default), StringComparer.Ordinal);
    private static DtdNormalizedValue Normalize(string text, DtdAttributeType type = DtdAttributeType.CData, Dictionary<string, DtdEntityDeclaration>? table = null, XmlResourceLimits? limits = null, IDtdBudget? ledger = null)
        => DtdValueProcessor.NormalizeAttributeDefault(text, type, table ?? [], limits ?? new(), default, ledger, TestContext.Current.CancellationToken);
    [Theory]
    [InlineData(" \t\n\r a  b ", "     a  b ", "a b")]
    [InlineData(" &#9;&#10;&#13; ", " \t\n\r ", "\t\n\r")]
    [InlineData("&lt;&gt;&amp;&apos;&quot;", "<>&'\"", "<>&'\"")]
    [InlineData("&#x1F600;", "😀", "😀")]
    [InlineData(" \u0085\u00A0 ", " \u0085\u00A0 ", "\u0085\u00A0")]
    public void AttributeWhitespaceAndPredefined(string source, string cdata, string tokenized)
    {
        Assert.Equal(cdata, Normalize(source).Value);
        Assert.Equal(tokenized, Normalize(source, DtdAttributeType.IdRefs).Value);
    }
    [Fact]
    public void DeclarationOriginForwardReferencesAndEffectiveTable()
    {
        var output = new char[64];
        using var d = new DtdValueDecoder(new());
        var decoded = d.Decode("&#9; &#38;#9; &forward;", output, true, TestContext.Current.CancellationToken);
        var table = Table(("outer", new string(output, 0, decoded.Written)), ("forward", "done"));
        Assert.Equal("  \t done", Normalize("&outer;", table: table).Value);
        Assert.Equal("\t done", Normalize("&outer;", DtdAttributeType.NmTokens, table).Value);
        table["forward"] = table["forward"] with
        {
            ReplacementText = "internal",
            IsExternal = false
        };
        Assert.Equal("  \t internal", Normalize("&outer;", table: table).Value);
        // Publication retains the declaration literal; helper cannot mutate its model.
        var declaration = new DtdAttributeDeclaration("e", "a", DtdAttributeType.CData, [], DtdDefaultKind.Value, "&outer;", true, default);
        Assert.Equal("&outer;", declaration.DefaultValue);
    }
    [Fact]
    public void DirectCrTabDifferFromDecodedDeclarationCrTab()
    {
        var output = new char[8];
        using var d = new DtdValueDecoder(new());
        var r = d.Decode("&#xD;&#x9;", output, true, TestContext.Current.CancellationToken);
        var table = Table(("a", new string(output, 0, r.Written)));
        Assert.Equal("\r\t", Normalize("&#xD;&#x9;").Value);
        Assert.Equal("  ", Normalize("&a;", table: table).Value);
        Assert.Equal("\r\t", Normalize("&#xD;&#x9;", DtdAttributeType.NmTokens).Value);
        Assert.Equal("", Normalize("&a;", DtdAttributeType.NmTokens, table).Value);
    }
    [Theory]
    [InlineData("<")]
    [InlineData("&missing;")]
    [InlineData("&;")]
    [InlineData("&#x;")]
    [InlineData("&#xD800;")]
    [InlineData("&#x110000;")]
    [InlineData("&a")]
    public void InvalidAttributes(string source) => Assert.Equal("DTD.InvalidValue", Normalize(source).Diagnostic!.Code);
    [Fact]
    public void InvalidScalarAttributesUseStableAsciiCaseIds()
    {
        foreach (var source in new[] { "\uD800", "\uDC00", "\uD800x", "\0", "&#999999999999999999;" })
        {
            Assert.NotNull(Normalize(source).Diagnostic);
        }
    }
    [Fact]
    public void ExternalUnparsedParameterAndLiteralMarkupRejected()
    {
        foreach (var entity in new[] {
            new DtdEntityDeclaration("a", false, null, "uri", null, null, false, default),
            new DtdEntityDeclaration("a", false, "text", null, null, "n", false, default),
            new DtdEntityDeclaration("a", true, "text", null, null, null, false, default),
            new DtdEntityDeclaration("a", false, "<", null, null, null, false, default) })
        {
            Assert.NotNull(Normalize("&a;", table: new()
            {
                ["a"] = entity
            }).Diagnostic);
        }

        Assert.Equal("<", Normalize("&a;", table: Table(("a", "&#60;"))).Value);
        Assert.Equal("<", Normalize("&lt;").Value);
    }
    [Fact]
    public void CyclesNullDepthAndRepeatedInactiveNames()
    {
        Assert.NotNull(Normalize("&a;", table: Table(("a", "&b;"), ("b", "&a;")), limits: new()
        {
            EntityDepth = null
        }).Diagnostic);
        Assert.Equal("xx", Normalize("&a;&a;", table: Table(("a", "x"))).Value);
        var table = Table(("n0", "x"));
        for (var i = 1; i <= 2000; i++)
        {
            table.Add("n" + i, new("n" + i, false, "&n" + (i - 1) + ";", null, null, null, false, default));
        }

        Assert.Equal("x", Normalize("&n2000;", table: table, limits: new()
        {
            EntityDepth = null
        }).Value);
    }
    [Fact]
    public void ExactAndOneOverExpansionDepthAndNormalizedTokenLimits()
    {
        var table = Table(("a", "&b;"), ("b", "x")); // three outer replacement scalars + one nested scalar.
        Assert.Equal("x", Normalize("&a;", table: table, limits: new()
        {
            EntityDepth = 2,
            ExpandedCharacters = 4
        }).Value);
        Assert.NotNull(Normalize("&a;", table: table, limits: new()
        {
            EntityDepth = 1
        }).Diagnostic);
        Assert.NotNull(Normalize("&a;", table: table, limits: new()
        {
            ExpandedCharacters = 3
        }).Diagnostic);
        Assert.Equal("😀 x", Normalize("  😀  x ", DtdAttributeType.IdRefs, limits: new()
        {
            TokenCharacters = 3
        }).Value);
        Assert.NotNull(Normalize("  😀  x ", DtdAttributeType.IdRefs, limits: new()
        {
            TokenCharacters = 2
        }).Diagnostic);
        Assert.Equal(" ", Normalize("&#32;", limits: new()
        {
            EntityDepth = 1,
            ExpandedCharacters = 1,
            TokenCharacters = 1
        }).Value);
        using var d = new DtdValueDecoder(new()
        {
            ExpandedCharacters = 1,
            TokenCharacters = 1
        });
        Assert.Equal(DtdValueStep.Completed, d.Decode("&#x1F600;", new char[2], true, TestContext.Current.CancellationToken).Step);
        using var over = new DtdValueDecoder(new()
        {
            ExpandedCharacters = 1
        });
        Assert.Equal("XML.ResourceLimit", over.Decode("&#65;&#66;", new char[4], true, TestContext.Current.CancellationToken).Diagnostic!.Code);
        using var token = new DtdValueDecoder(new()
        {
            TokenCharacters = 1
        });
        Assert.Equal("XML.ResourceLimit", token.Decode("ab", new char[4], true, TestContext.Current.CancellationToken).Diagnostic!.Code);
        Assert.True(DtdValueProcessor.CanAppend(int.MaxValue - 2, 2));
        Assert.False(DtdValueProcessor.CanAppend(int.MaxValue - 1, 2));
    }
    [Fact]
    public void SharedLedgerChargesOnlyActualExpansionAndBalancesOnRefusal()
    {
        var ledger = new Ledger(4, 2);
        Assert.Equal("x", Normalize("literal &a;", table: Table(("a", "&b;"), ("b", "x")), ledger: ledger).Value![8..]);
        Assert.Equal(4, ledger.Expanded);
        Assert.Equal(0, ledger.Depth);
        ledger = new(3, 2);
        var rejected = Normalize("&a;", table: Table(("a", "&b;"), ("b", "x")), ledger: ledger);
        Assert.Same(ledger.Refusal, rejected.Diagnostic);
        Assert.Equal(0, ledger.Depth);
        ledger = new(1, 1);
        using var d = new DtdValueDecoder(new(), default, ledger);
        Assert.Equal(DtdValueStep.Completed, d.Decode("literal &name;&#9;", new char[32], true, TestContext.Current.CancellationToken).Step);
        Assert.Equal(1, ledger.Expanded);
        Assert.Equal(0, ledger.Depth);
    }
    [Fact]
    public void CancellationExceptionsMisuseAndLifetime()
    {
        using var d = new DtdValueDecoder(new());
        Assert.Throws<ArgumentException>(() => d.Decode("x", new char[1], false, TestContext.Current.CancellationToken));
        Assert.Throws<OperationCanceledException>(() => d.Decode("x", new char[2], false, new(true)));
        Assert.Throws<InvalidOperationException>(() => d.Decode([], new char[2], true, TestContext.Current.CancellationToken));
        d.Dispose();
        Assert.Throws<ObjectDisposedException>(() => d.Decode([], new char[2], true, TestContext.Current.CancellationToken));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var ledger = new Ledger(10000, 10) { Callback = () => cancel.Cancel() };
        Assert.Throws<OperationCanceledException>(() => DtdValueProcessor.NormalizeAttributeDefault("&a;", DtdAttributeType.CData, Table(("a", new string('x', 10000))), new(), sharedBudget: ledger, cancellationToken: cancel.Token));
        Assert.Equal(0, ledger.Depth);
        var exception = new IOException("ledger");
        ledger = new(10, 10)
        {
            Callback = () => throw exception
        };
        Assert.Same(exception, Assert.Throws<IOException>(() => Normalize("&a;", table: Table(("a", "x")), ledger: ledger)));
        Assert.Equal(0, ledger.Depth);
        var table = Table(("a", "owned"));
        var owned = Normalize("&a;", table: table).Value;
        table.Clear();
        Assert.Equal("owned", owned);
    }
    [Fact]
    public void WarmBorrowedPrimitiveAllocatesNothing()
    {
        using var d = new DtdValueDecoder(new()
        {
            TokenCharacters = null,
            ExpandedCharacters = null
        });
        var output = new char[8];
        for (var i = 0; i < 1000; i++)
        {
            d.Decode("x&n;&#9;", output, false, TestContext.Current.CancellationToken);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            d.Decode("x&n;&#9;", output, false, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void ProgressBeforeEofFinalStickinessAndReentrancy()
    {
        var output = new char[2];
        using var d = new DtdValueDecoder(new());
        var r = d.Decode("a&#9;", output, false, TestContext.Current.CancellationToken);
        Assert.Equal(DtdValueStep.NeedInput, r.Step);
        Assert.Equal(5, r.Consumed);
        Assert.Equal(2, r.Written);
        Assert.Equal("a\t", new string(output));
        using var final = new DtdValueDecoder(new());
        r = final.Decode("abc", output, true, TestContext.Current.CancellationToken);
        Assert.Equal(DtdValueStep.NeedOutput, r.Step);
        Assert.Equal(3, r.Consumed);
        Assert.Throws<InvalidOperationException>(() => final.Decode([], output, false, TestContext.Current.CancellationToken));
        r = final.Decode([], output, true, TestContext.Current.CancellationToken);
        Assert.Equal(DtdValueStep.Completed, r.Step);
        Assert.Equal('c', output[0]);
        var ledger = new Ledger(10, 10);
        using var reentrant = new DtdValueDecoder(new(), default, ledger);
        ledger.Callback = () =>
        {
            Assert.Throws<InvalidOperationException>(() => reentrant.Decode([], output, false, TestContext.Current.CancellationToken));
            Assert.Throws<InvalidOperationException>(() => reentrant.Dispose());
        };
        Assert.Equal(DtdValueStep.Completed, reentrant.Decode("&#9;", output, true, TestContext.Current.CancellationToken).Step);
        Assert.Throws<ArgumentNullException>(() => new DtdValueDecoder(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DtdValueDecoder(new() { TokenCharacters = 0 }));
    }
    [Fact]
    public void DecoderCleanupPreservesPrimaryChargeExceptionAndStack()
    {
        var primary = new IOException("primary charge");
        var secondary = new IOException("secondary exit");
        var ledger = new ThrowingLedger { ChargeException = primary, FailAtCharge = 1, ExitExceptions = [secondary] };
        using var decoder = new DtdValueDecoder(new(), default, ledger);
        Assert.Same(primary, Assert.Throws<IOException>(() => decoder.Decode("&#9;", new char[2], true, TestContext.Current.CancellationToken)));
        Assert.Contains(nameof(ThrowingLedger.ChargeExpandedCharacters), primary.StackTrace);
        Assert.Equal(1, ledger.ExitCalls);
        Assert.Equal(0, ledger.Depth);
        Assert.Throws<InvalidOperationException>(() => decoder.Decode([], new char[2], true, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void DecoderExitExceptionWithoutPrimaryPropagatesOnce()
    {
        var first = new IOException("exit");
        var ledger = new ThrowingLedger { ExitExceptions = [first] };
        using var decoder = new DtdValueDecoder(new(), default, ledger);
        Assert.Same(first, Assert.Throws<IOException>(() => decoder.Decode("&#9;", new char[2], true, TestContext.Current.CancellationToken)));
        Assert.Equal(1, ledger.ExitCalls);
        Assert.Equal(0, ledger.Depth);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedCleanupDrainsAllFramesAndPreservesPrimary(bool numeric)
    {
        var primary = new IOException("primary");
        var ledger = new ThrowingLedger
        {
            ChargeException = primary,
            FailAtCharge = numeric ? 8 : 4,
            ExitExceptions = [new IOException("numeric or child exit"), new IOException("parent exit"), new IOException("outer exit")]
        };
        var table = Table(("a", "&b;"), ("b", numeric ? "&#9;" : "x"));
        Assert.Same(primary, Assert.Throws<IOException>(() => Normalize("&a;", table: table, ledger: ledger)));
        Assert.Contains(nameof(ThrowingLedger.ChargeExpandedCharacters), primary.StackTrace);
        Assert.Equal(numeric ? 3 : 2, ledger.ExitCalls);
        Assert.Equal(0, ledger.Depth);
        Assert.Equal(numeric ? 3 : 2, ledger.AcceptedEntries);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstExitExceptionPropagatesAfterAllParentsDrain(bool diagnostic)
    {
        var first = new IOException("first exit");
        var second = new IOException("second exit");
        var ledger = new ThrowingLedger { ExitExceptions = [first, second] };
        var table = Table(("a", "&b;"), ("b", diagnostic ? "&undeclared;" : "x"));
        Assert.Same(first, Assert.Throws<IOException>(() => Normalize("&a;", table: table, ledger: ledger)));
        Assert.Equal(2, ledger.AcceptedEntries);
        Assert.Equal(2, ledger.ExitCalls);
        Assert.Equal(0, ledger.Depth);
        Assert.Contains(nameof(ThrowingLedger.ExitEntity), first.StackTrace);
    }
    [Fact]
    public void RefusedEntryNeverExitsAndCleanupCancellationStaysPrimary()
    {
        var first = new IOException("parent exit");
        var ledger = new ThrowingLedger { MaxDepth = 1, ExitExceptions = [first] };
        Assert.Same(first, Assert.Throws<IOException>(() => Normalize("&a;", table: Table(("a", "&b;"), ("b", "x")), ledger: ledger)));
        Assert.Equal(2, ledger.EnterCalls);
        Assert.Equal(1, ledger.AcceptedEntries);
        Assert.Equal(1, ledger.ExitCalls);
        Assert.Equal(0, ledger.Depth);
        var canceled = new OperationCanceledException("primary cancellation");
        ledger = new()
        {
            ChargeException = canceled,
            FailAtCharge = 4,
            ExitExceptions = [first, new IOException("outer")]
        };
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Normalize("&a;", table: Table(("a", "&b;"), ("b", "x")), ledger: ledger)));
        Assert.Equal(2, ledger.ExitCalls);
        Assert.Equal(0, ledger.Depth);
    }
    private sealed class ThrowingLedger : IDtdBudget
    {
        public int EnterCalls, AcceptedEntries, Depth, ExitCalls, ChargeCalls;
        public int MaxDepth = int.MaxValue, FailAtCharge = int.MaxValue;
        public Exception? ChargeException;
        public Exception[] ExitExceptions = [];
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation location)
        {
            if (++ChargeCalls == FailAtCharge)
            {
                throw ChargeException!;
            }

            return null;
        }
        public XmlDiagnostic? EnterEntity(XmlLocation location)
        {
            EnterCalls++;
            if (Depth == MaxDepth)
            {
                return new("TEST.Refused", XmlDiagnosticSeverity.Fatal, "Refused", location);
            }

            Depth++;
            AcceptedEntries++;
            return null;
        }
        public void ExitEntity()
        {
            Assert.True(Depth > 0);
            Depth--;
            var index = ExitCalls++;
            if (index < ExitExceptions.Length)
            {
                throw ExitExceptions[index];
            }
        }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation location) => throw new Exception("Caller owns names.");
    }
    private sealed class Ledger(long maximum, int maxDepth) : IDtdBudget
    {
        public long Expanded; public int Depth; public Action? Callback;
        public XmlDiagnostic Refusal = new("TEST.Refused", XmlDiagnosticSeverity.Fatal, "Refused", default);
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation location)
        {
            Callback?.Invoke();
            if (count > maximum - Expanded)
            {
                return Refusal;
            }

            Expanded += count;
            return null;
        }
        public XmlDiagnostic? EnterEntity(XmlLocation location)
        {
            if (Depth == maxDepth)
            {
                return Refusal;
            }

            Depth++;
            return null;
        }
        public void ExitEntity()
        {
            Assert.True(Depth > 0);
            Depth--;
        }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation location) => throw new Exception("Caller owns names.");
    }
}
