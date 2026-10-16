using System.Collections;
using System.Text;
using Xemele.Validation;
using Xemele.XPath;
using Xunit;
namespace Xemele.Tests.M8Model;

public sealed class ModelTests
{
    private static readonly XmlLocation At = new(new Uri("urn:test:source"), 2, 3, 17);
    private static XmlDiagnostic Error(string code = "TEST.Error") => new(code, XmlDiagnosticSeverity.Error, "error", At);

    [Fact]
    public void OptionsSnapshotNestedLimitsAndRejectInvalidPolicyBeforeWork()
    {
        var options = new SchemaCompileOptions();
        var operation = new SchemaCompileOperation(options);
        Assert.NotSame(options.Limits, operation.Limits);
        Assert.Equal(10_000_000, operation.RemainingWork);
        var compile = SchemaSnapshots.Compile(new SchematronCompileOptions());
        var original = new SchematronCompileOptions();
        var copy = SchemaSnapshots.Compile(original);
        Assert.NotSame(original.Schema, copy.Schema);
        Assert.NotSame(original.Schema.Limits, copy.Schema.Limits);
        Assert.NotSame(original.XPathLimits, copy.XPathLimits);
        var validation = new SchematronValidationOptions();
        var run = SchemaSnapshots.Validation(validation);
        Assert.NotSame(validation.Validation, run.Validation);
        Assert.NotSame(validation.Validation.Limits, run.Validation.Limits);
        Assert.NotSame(validation.XPathLimits, run.XPathLimits);
        Assert.Equal(SchematronEdition.Iso2016, compile.Edition);
        Assert.Null(run.Phase);
        foreach (var bad in new[] { new SchemaCompileOptions { Limits = null! }, new() { MaxOperations = 0 }, new() { MaxComponents = -1 }, new() { BaseUri = new("relative", UriKind.Relative) }, new() { Limits = new() { Diagnostics = 0 } } })
        {
            Assert.ThrowsAny<ArgumentException>(() => new SchemaCompileOperation(bad));
        }

        Assert.Throws<ArgumentNullException>(() => SchemaSnapshots.Compile(new SchematronCompileOptions { Schema = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => SchemaSnapshots.Compile(new SchematronCompileOptions { Edition = (SchematronEdition)42 }));
        Assert.Throws<ArgumentNullException>(() => SchemaSnapshots.Validation(new SchematronValidationOptions { XPathLimits = null! }));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new SchemaCompileOperation(new() { CancellationToken = canceled.Token }));
    }
    [Fact]
    public void CompileCountersAggregateExactlyAtLimitsAndBoundTerminalDiagnostics()
    {
        var op = new SchemaCompileOperation(new()
        {
            MaxOperations = 7,
            MaxComponents = 2,
            Limits = new()
            {
                EvaluationWork = 5,
                Diagnostics = 2
            }
        });
        Assert.True(op.Charge(2, At));
        Assert.True(op.Charge(3, At));
        Assert.Equal(0, op.RemainingWork);
        Assert.True(op.Charge(0, At));
        Assert.True(op.ChargeComponents(2, At));
        Assert.True(op.AddDiagnostic(Error()));
        Assert.True(op.AddDiagnostic(Error("TEST.Second")));
        Assert.False(op.Charge(1, At));
        Assert.False(op.IsComplete);
        Assert.False(op.ChargeComponents(0, At));
        Assert.Equal(2, op.Diagnostics.Count);
        Assert.Equal("SCHEMA.Limit", op.Diagnostics[^1].Code);
        Assert.Equal(At, op.Diagnostics[^1].Location);
        Assert.Equal(XmlDiagnosticSeverity.Fatal, op.Diagnostics[^1].Severity);
        var components = new SchemaCompileOperation(new()
        {
            MaxComponents = 1
        });
        Assert.True(components.ChargeComponents(1, At));
        Assert.False(components.ChargeComponents(1, At));
        var diagnostics = new SchemaCompileOperation(new()
        {
            Limits = new()
            {
                Diagnostics = 1
            }
        });
        Assert.True(diagnostics.AddDiagnostic(Error()));
        Assert.False(diagnostics.AddDiagnostic(Error()));
        Assert.Single(diagnostics.Diagnostics);
        var unlimited = new SchemaCompileOperation(new()
        {
            MaxOperations = null,
            MaxComponents = null,
            Limits = new()
            {
                EvaluationWork = null
            }
        });
        Assert.True(unlimited.Charge(long.MaxValue, At));
        Assert.Null(unlimited.RemainingWork);
        Assert.False(unlimited.Charge(1, At));
        Assert.Throws<ArgumentOutOfRangeException>(() => components.Charge(-1, At));
    }
    [Fact]
    public void RunAdaptersOwnIndependentM5CountersAndResults()
    {
        var options = new ValidationOptions { MaxOperations = 3 };
        var left = new SchemaValidationOperation(options);
        var right = new SchemaValidationOperation(options);
        Assert.True(left.Charge(3, At));
        Assert.Equal(0, left.RemainingWork);
        Assert.Equal(3, right.RemainingWork);
        Assert.False(left.Charge(1, At));
        Assert.True(right.Complete().IsValid);
        var result = left.Complete();
        Assert.False(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal("VALIDATION.Limit", Assert.Single(result.Diagnostics).Code);
        Assert.NotSame(options.Limits, right.Limits);
    }
    [Fact]
    public void DefinitionsAndWithCopiesOwnCollectionsAndNamespaceBindings()
    {
        var prefixes = new Dictionary<string, string> { ["p"] = "urn:old" };
        var context = new SchemaNamespaceContext(prefixes, "urn:default", At.SourceUri);
        prefixes["p"] = "urn:new";
        Assert.Equal("urn:old", context.Prefixes["p"]);
        var replacement = context with
        {
            Prefixes = prefixes
        };
        prefixes.Clear();
        Assert.Equal("urn:new", replacement.Prefixes["p"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)context.Prefixes).Add("x", "urn:x"));
        var names = new List<SchemaName> { new("urn:n", "one") };
        var facets = new List<XmlFacet> { new(XmlFacetKind.Enumeration, "p:n", false, context, At) };
        var final = new List<XmlDatatypeDerivation> { XmlDatatypeDerivation.Restriction };
        var definition = new XmlDatatypeDefinition(new("urn:n", "type"), XmlDatatypeVariety.Union, null, null, names, facets, final, false, At);
        names.Clear();
        facets.Clear();
        final.Clear();
        Assert.Single(definition.MemberTypes);
        Assert.Single(definition.Facets);
        Assert.Single(definition.Final);
        Assert.Equal(At, definition.Facets[0].Location);
        Assert.Equal("urn:old", definition.Facets[0].Context.Prefixes["p"]);
        var names2 = new List<SchemaName> { new("", "two") };
        var changed = definition with
        {
            MemberTypes = names2
        };
        names2.Clear();
        Assert.Single(changed.MemberTypes);
        Assert.Throws<NotSupportedException>(() => ((IList<SchemaName>)definition.MemberTypes).Clear());
    }
    [Fact]
    public void CompileExceptionRequiresOwnedNonNullErrorDiagnostics()
    {
        var input = new List<XmlDiagnostic> { Error() };
        var error = new SchemaCompileException(input);
        input.Clear();
        Assert.Single(error.Diagnostics);
        Assert.Throws<ArgumentException>(() => new SchemaCompileException([]));
        Assert.Throws<ArgumentException>(() => new SchemaCompileException([new("WARN", XmlDiagnosticSeverity.Warning, "warning", default)]));
        Assert.Throws<ArgumentException>(() => new SchemaCompileException([null!]));
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDiagnostic>)error.Diagnostics).Clear());
    }
    [Fact]
    public void DetachedReportSnapshotsKeepOptionalIdsEventKeysAndSubjectSeparateFromFocus()
    {
        var diagnostics = new List<SchematronDiagnosticReference> { new("d", "diagnostic") };
        var properties = new List<SchematronPropertyReference> { new("p", "property") };
        var subject = At with
        {
            LineNumber = 8
        };
        var finding = new SchematronFinding(SchematronFindingKind.SuccessfulReport, null, null, null, 1, 3, null, "/r", "true()", "/r/a", subject, "/r", At, "message", diagnostics, properties);
        diagnostics.Clear();
        properties.Clear();
        var patterns = new List<SchematronActivePattern> { new(1, null, null) };
        var fired = new List<SchematronFiredRule> { new(3, 1, 2, "/r", "/r") };
        var findings = new List<SchematronFinding> { finding };
        var namespaces = new Dictionary<string, string> { ["p"] = "urn:p" };
        var report = new SchematronResult(new(true, []), "#ALL", null, namespaces, patterns, fired, findings);
        patterns.Clear();
        fired.Clear();
        findings.Clear();
        namespaces.Clear();
        Assert.Null(report.ActivePatterns[0].Id);
        Assert.Equal(3, report.Findings[0].FiredRuleKey);
        Assert.Equal("urn:p", report.Namespaces["p"]);
        Assert.Equal(subject, finding.SourceLocation);
        Assert.Equal(At, finding.ContextSourceLocation);
        Assert.Single(finding.Properties);
        Assert.Single(finding.Diagnostics);
        Assert.True(report.Validation.IsValid);
        Assert.Null(report.Title);
    }
    [Fact]
    public void ReportConstructorsRejectNullStringsInvalidKeysAndInconsistentJoins()
    {
        var valid = new ValidationResult(true, []);
        var invalid = new ValidationResult(true, [new("SCH.Assert", XmlDiagnosticSeverity.Error, "assert", default)]);
        var incomplete = new ValidationResult(false, [new("SCH.XPath", XmlDiagnosticSeverity.Fatal, "stopped", default)]);
        SchematronFinding Finding(int pattern = 1, int fired = 3, string? id = null, SchematronFindingKind kind = SchematronFindingKind.SuccessfulReport, string context = "/r", string contextLocation = "/r") =>
            new(kind, null, "", "", pattern, fired, id, context, "true()", "", default, contextLocation, At, "", [], []);
        SchematronResult Result(ValidationResult validation, IEnumerable<SchematronActivePattern> patterns, IEnumerable<SchematronFiredRule> rules, IEnumerable<SchematronFinding> findings) =>
            new(validation, "#ALL", null, new Dictionary<string, string>(), patterns, rules, findings);
        SchematronActivePattern[] patterns = [new(1, null, null), new(2, "two", null)];
        SchematronFiredRule[] rules = [new(3, 1, 7, "/r", "/r")];
        Assert.Throws<ArgumentNullException>(() => new SchematronDiagnosticReference(null!, ""));
        Assert.Throws<ArgumentNullException>(() => new SchematronPropertyReference("id", null!));
        Assert.Throws<ArgumentNullException>(() => new SchematronDiagnosticReference("id", "") with { Text = null! });
        Assert.Throws<ArgumentNullException>(() => new SchematronFiredRule(0, 0, 0, null!, "/"));
        Assert.Throws<ArgumentNullException>(() => new SchematronFiredRule(0, 0, 0, "/", null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SchematronActivePattern(-1, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SchematronFiredRule(-1, 0, 0, "/", "/"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SchematronFiredRule(0, -1, 0, "/", "/"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SchematronFiredRule(0, 0, -1, "/", "/"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Finding(kind: (SchematronFindingKind)123));
        Assert.Throws<ArgumentException>(() => Result(valid, [patterns[0], patterns[0]], rules, []));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, [rules[0], rules[0]], []));
        Assert.Throws<ArgumentException>(() => Result(valid, [], rules, []));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, rules, [Finding(fired: 99)]));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, rules, [Finding(pattern: 2)]));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, rules, [Finding(id: "invented")]));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, rules, [Finding(context: "/other")]));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, rules, [Finding(contextLocation: "/other")]));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, rules, [Finding(kind: SchematronFindingKind.FailedAssert)]));
        Assert.Throws<ArgumentException>(() => Result(invalid, patterns, rules, []));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, rules, [null!]));
        Assert.Throws<ArgumentException>(() => Result(valid, [null!], [], []));
        Assert.Throws<ArgumentException>(() => Result(valid, patterns, [null!], []));
        Assert.True(Result(valid, patterns, rules, [Finding()]).Validation.IsValid); // empty subject, independent known focus
        Assert.False(Result(invalid, patterns, rules, [Finding(kind: SchematronFindingKind.FailedAssert)]).Validation.IsValid);
        Assert.False(Result(incomplete, patterns, rules, []).Validation.IsComplete);
    }

    [Fact]
    public void NamespaceAndBaseSnapshotsDetachFromLiveTreeAndTraversalDetectsMutation()
    {
        var context = new SchemaCompileContext(new()
        {
            BaseUri = new("https://example.test/root/")
        });
        var input = context.Parse("<r xmlns='urn:r' xmlns:p='urn:p' xml:base='one/'><a xmlns:p='urn:inner' xml:base='../two/'/></r>");
        var child = input.Document.RootElement.FirstChild;
        var snapshot = input.Context(child);
        Assert.Equal("urn:r", snapshot.DefaultNamespace);
        Assert.Equal("urn:inner", snapshot.Prefixes["p"]);
        Assert.Equal(new Uri("https://example.test/root/two/"), snapshot.BaseUri);
        Assert.Equal("http://www.w3.org/XML/1998/namespace", snapshot.Prefixes["xml"]);
        child.SetNamespaceDeclaration("p", "urn:changed");
        Assert.Equal("urn:inner", snapshot.Prefixes["p"]);
        Assert.Throws<InvalidOperationException>(input.CheckUnchanged);
        var incomplete = new XmlDocument();
        incomplete.MarkSourceIncomplete();
        Assert.Equal("SCHEMA.IncompleteInput", Assert.Single(Assert.Throws<SchemaCompileException>(() => new SchemaCompileContext().Open(incomplete)).Diagnostics).Code);
    }
    [Fact]
    public void InputParserPreservesCallerStreamsAndExceptionIdentityAndProhibitsDtd()
    {
        using var content = new CountStream("skip<r>根😀</r>");
        content.Position = 4;
        var context = new SchemaCompileContext(new()
        {
            BaseUri = At.SourceUri
        });
        var input = context.Parse(content);
        Assert.Equal("根😀", input.Document.RootElement.GetTextContent());
        Assert.Equal(0, content.Disposals);
        Assert.Equal(At.SourceUri, input.SourceUri);
        var error = Assert.Throws<SchemaCompileException>(() => new SchemaCompileContext().Parse("<!DOCTYPE r><r/>"));
        Assert.Equal("SCHEMA.Syntax", error.Diagnostics[0].Code);
        var callerError = new XmlParseException([Error()]);
        using var failing = new FailingStream(callerError);
        Assert.Same(callerError, Assert.Throws<XmlParseException>(() => new SchemaCompileContext().Parse(failing)));
        Assert.Equal(0, failing.Disposals);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void XPathJoinDebitsActualWorkSharesCapAndPreservesCallbackErrors(bool item)
    {
        var root = XmlDocument.Parse("<r><a/><a/></r>", cancellationToken: TestContext.Current.CancellationToken).RootElement;
        var expression = XPathExpression.Compile("count(/r/a)", cancellationToken: TestContext.Current.CancellationToken);
        long used;
        if (item)
        {
            expression.EvaluateWithAccounting(XPathItem.FromNode(root), null, out used);
        }
        else
        {
            expression.EvaluateWithAccounting(root, null, out used);
        }

        var op = new SchemaValidationOperation(new()
        {
            MaxOperations = used * 2
        });
        XPathValue Evaluate(ISchemaOperation operation, XPathContext? options = null) => item ? SchemaXPath.Evaluate(expression, XPathItem.FromNode(root), options, operation) : SchemaXPath.Evaluate(expression, root, options, operation);
        Assert.Equal(2, Evaluate(op).AsNumber());
        Assert.Equal(used, op.RemainingWork);
        Assert.Equal(2, Evaluate(op).AsNumber());
        Assert.Equal(0, op.RemainingWork);
        Assert.True(op.IsComplete);
        Assert.Throws<InvalidOperationException>(() => Evaluate(op));
        Assert.False(op.IsComplete);
        var shortRun = new SchemaValidationOperation(new()
        {
            MaxOperations = used - 1
        });
        Assert.Throws<XPathEvaluationException>(() => Evaluate(shortRun));
        Assert.InRange(shortRun.RemainingWork!.Value, 0, used - 2);
        var name = new XPathExpandedName("urn:e", "f");
        var callback = XPathExpression.Compile("e:f()", new()
        {
            Namespaces = new Dictionary<string, string> { ["e"] = "urn:e" },
            Functions = [new(name, 0, 0)]
        }, TestContext.Current.CancellationToken);
        using var canceled = new CancellationTokenSource();
        var failure = new InvalidOperationException("caller");
        var callbackOptions = new XPathContext { Functions = new Dictionary<XPathExpandedName, XPathExtensionFunction> { [name] = (ctx, _) => { ctx.ChargeOperations(7); canceled.Cancel(); throw failure; } } };
        var canceledOp = new SchemaValidationOperation(new()
        {
            CancellationToken = canceled.Token
        });
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => item ? SchemaXPath.Evaluate(callback, XPathItem.FromNode(root), callbackOptions, canceledOp) : SchemaXPath.Evaluate(callback, root, callbackOptions, canceledOp)));
        Assert.Throws<OperationCanceledException>(() => canceledOp.Complete());
        var callbackOp = new SchemaCompileOperation();
        var throwingOptions = callbackOptions with
        {
            Functions = new Dictionary<XPathExpandedName, XPathExtensionFunction> { [name] = (ctx, _) => { ctx.ChargeOperations(7); throw failure; } }
        };
        var before = callbackOp.RemainingWork!.Value;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => SchemaXPath.Evaluate(callback, root, throwingOptions, callbackOp)));
        Assert.True(before - callbackOp.RemainingWork > 7);
    }

    [Fact]
    public void ParsingDebitsAcceptedScalarsAcrossPrincipalAndExternalInputs()
    {
        // 9 XML scalars, 10 UTF-16 units, 14 UTF-8 bytes. Both input routes charge 9.
        const string xml = "<r>根😀</r>";
        var chars = new SchemaCompileContext(new()
        {
            MaxOperations = 9
        });
        chars.Parse(xml);
        Assert.Equal(0, chars.Operation.RemainingWork);
        using var stream = new CountStream(xml);
        var bytes = new SchemaCompileContext(new()
        {
            MaxOperations = 9
        });
        bytes.Parse(stream);
        Assert.Equal(0, bytes.Operation.RemainingWork);
        var shortRun = new SchemaCompileContext(new()
        {
            MaxOperations = 8
        });
        Assert.Equal("SCHEMA.Limit", Assert.Single(Assert.Throws<SchemaCompileException>(() => shortRun.Parse(xml)).Diagnostics).Code);
        Assert.False(shortRun.Operation.IsComplete);
        Assert.Equal(0, shortRun.Operation.RemainingWork);
        var aggregate = new SchemaCompileContext(new()
        {
            MaxOperations = 8,
            Resolver = new ResourceTests.Resolver((_, _) => new(new CountStream("<r/>"), new Uri("urn:external")))
        });
        aggregate.Parse("<r/>");
        aggregate.Resources.Load(new("urn:external", null, SchemaResourcePurpose.XsdInclude, default), aggregate.Operation);
        Assert.Equal(0, aggregate.Operation.RemainingWork);
        Assert.Throws<SchemaCompileException>(() => aggregate.Parse("<r/>"));
        var documentCap = new SchemaCompileContext(new()
        {
            Limits = new()
            {
                DocumentCharacters = 7
            }
        });
        documentCap.Parse("<r/>");
        Assert.Throws<SchemaCompileException>(() => documentCap.Parse("<r/>"));
    }

    internal sealed class CountStream(string xml) : MemoryStream(Encoding.UTF8.GetBytes(xml))
    {
        internal int Disposals
        {
            get; private set;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }
    internal sealed class FailingStream(Exception error) : MemoryStream
    {
        internal int Disposals
        {
            get; private set;
        }
        public override int Read(Span<byte> buffer) => throw error;
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }
}
