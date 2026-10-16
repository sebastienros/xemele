using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8SchValidate;

public sealed class BudgetTests
{
    [Fact]
    public void AggregateWorkCannotResetAcrossChecksLetsAndMessages()
    {
        var document = XmlDocument.Parse("<item/>", cancellationToken: TestContext.Current.CancellationToken);
        var one = EvaluationTests.Compile(EvaluationTests.Pattern("<assert test='true()'/>"));
        var many = EvaluationTests.Compile(EvaluationTests.Pattern(string.Concat(Enumerable.Repeat("<assert test='true()'/>", 16))));
        var limits = new Xemele.XPath.XPathEvaluationLimits { MaxOperations = null };
        long low = 1, high = 100_000;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (one.Validate(document, new()
            {
                Validation = new()
                {
                    MaxOperations = mid
                },
                XPathLimits = limits
            }).Validation.IsComplete)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }
        Assert.True(one.Validate(document, new()
        {
            Validation = new()
            {
                MaxOperations = low
            },
            XPathLimits = limits
        }).Validation.IsValid);
        Assert.False(one.Validate(document, new()
        {
            Validation = new()
            {
                MaxOperations = low - 1
            },
            XPathLimits = limits
        }).Validation.IsComplete);
        var repeated = many.Validate(document, new()
        {
            Validation = new()
            {
                MaxOperations = low
            },
            XPathLimits = limits
        });
        Assert.False(repeated.Validation.IsComplete);
        Assert.Empty(repeated.Findings);
        Assert.Contains(repeated.Validation.Diagnostics, d => d.Code is "SCH.XPath" or "VALIDATION.Limit");
        Assert.True(many.Validate(document, new()
        {
            Validation = new()
            {
                MaxOperations = null,
                Limits = new()
                {
                    EvaluationWork = null
                }
            },
            XPathLimits = limits
        }).Validation.IsValid);
        Assert.False(one.Validate(document, new()
        {
            Validation = new()
            {
                MaxOperations = null,
                Limits = new()
                {
                    EvaluationWork = 1
                }
            }
        }).Validation.IsComplete);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void FindingAndTerminalDiagnosticsShareTheCap(int cap)
    {
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<report test='true()'>x</report>"));
        var exact = "<root>" + string.Concat(Enumerable.Repeat("<item/>", cap)) + "</root>";
        var options = new SchematronValidationOptions { Validation = new() { Limits = new() { Diagnostics = cap } } };
        var result = schema.Validate(exact, options);
        Assert.True(result.Validation.IsValid);
        Assert.Equal(cap, result.Findings.Count);
        Assert.Equal(cap, result.Validation.Diagnostics.Count);
        var over = schema.Validate(exact.Replace("</root>", "<item/></root>", StringComparison.Ordinal), options);
        Assert.False(over.Validation.IsComplete);
        Assert.Equal(cap, over.Validation.Diagnostics.Count);
        Assert.Equal("VALIDATION.Limit", over.Validation.Diagnostics[^1].Code);
        Assert.Equal(cap - 1, over.Findings.Count);
    }

    [Fact]
    public void PerExpressionAndAggregateParsingLimitsStopInsteadOfFailingAssertions()
    {
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<assert test='false()'>bad</assert>"));
        var result = schema.Validate("<item/>", new()
        {
            XPathLimits = new()
            {
                MaxOperations = 1
            }
        });
        Assert.False(result.Validation.IsComplete);
        Assert.Empty(result.Findings);
        Assert.Equal("SCH.XPath", Assert.Single(result.Validation.Diagnostics).Code);
        var parsed = schema.Validate("<item/>", new()
        {
            Validation = new()
            {
                MaxOperations = 1
            }
        });
        Assert.False(parsed.Validation.IsComplete);
        Assert.Equal("VALIDATION.Limit", Assert.Single(parsed.Validation.Diagnostics).Code);
        var text = EvaluationTests.Compile(EvaluationTests.Pattern("<report test='true()'>longer</report>"));
        Assert.False(text.Validate("<item/>", new()
        {
            XPathLimits = new()
            {
                MaxStringCharacters = 2
            }
        }).Validation.IsComplete);
    }
}

public sealed class TraversalGuardTests
{
    [Fact]
    public async Task MutationDuringValidationFailsInsteadOfPublishingAResult()
    {
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<assert test='true()'/>"));
        var document = XmlDocument.Parse("<root>" + string.Concat(Enumerable.Repeat("<item/>", 3000)) + "</root>", cancellationToken: TestContext.Current.CancellationToken);
        using var started = new ManualResetEventSlim();
        var validation = Task.Run(() =>
        {
            started.Set();
            return Record.Exception(() => schema.Validate(document, new() { Validation = new() { MaxOperations = null, Limits = new() { EvaluationWork = null } }, XPathLimits = new() { MaxOperations = null } }));
        }, TestContext.Current.CancellationToken);
        started.Wait(TestContext.Current.CancellationToken);
        var changes = 0;
        while (!validation.IsCompleted)
        {
            document.RootElement.SetAttribute("changing", (++changes).ToString());
            await Task.Yield();
        }
        Assert.IsType<InvalidOperationException>(await validation);
        Assert.True(changes > 0);
    }

    [Fact]
    public async Task UnlimitedWorkStillPollsCancellationDuringEvaluation()
    {
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<assert test='true()'/>"));
        var document = XmlDocument.Parse("<root>" + string.Concat(Enumerable.Repeat("<item/>", 3000)) + "</root>", cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        using var started = new ManualResetEventSlim();
        var validation = Task.Run(() =>
        {
            started.Set();
            return Record.Exception(() => schema.Validate(document, new() { Validation = new() { CancellationToken = cancellation.Token, MaxOperations = null, Limits = new() { EvaluationWork = null } }, XPathLimits = new() { MaxOperations = null } }));
        }, TestContext.Current.CancellationToken);
        started.Wait(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        Assert.IsAssignableFrom<OperationCanceledException>(await validation);
    }

    [Fact]
    public void InstanceScalarQuotaMatchesAcrossStringAndByteInput()
    {
        const string input = "<item>😀</item>";
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<assert test='true()'/>"));
        foreach (var cap in new[] { 14, 13 })
        {
            var options = new SchematronValidationOptions { Validation = new() { Limits = new() { DocumentCharacters = cap } } };
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input));
            Assert.Equal(cap == 14, schema.Validate(input, options).Validation.IsComplete);
            Assert.Equal(cap == 14, schema.Validate(stream, options).Validation.IsComplete);
            Assert.True(stream.CanRead);
        }
    }
}
