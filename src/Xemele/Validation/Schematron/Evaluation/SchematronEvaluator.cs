using System.Globalization;
using System.Text;
using Xemele.XPath;

namespace Xemele.Validation;

internal interface ISchematronEvaluator
{
    SchematronResult Validate(SchematronCompiledModel schema, XmlDocument document, SchematronValidationOptions options);
}

internal sealed class SchematronEvaluator : ISchematronEvaluator
{
    public SchematronResult Validate(SchematronCompiledModel schema, XmlDocument document, SchematronValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(document);
        var snapshot = SchemaSnapshots.Validation(options);
        var phase = SelectPhase(schema, snapshot.Phase);
        return Evaluate(schema, document, snapshot, phase, new(snapshot.Validation));
    }
    internal static string SelectPhase(SchematronCompiledModel schema, string? phase)
    {
        phase = phase is null or "#DEFAULT" ? schema.DefaultPhase : phase;
        if (phase != "#ALL" && !schema.Phases.ContainsKey(phase))
        {
            throw new ArgumentException("Unknown Schematron phase.", nameof(phase));
        }

        return phase;
    }
    internal static SchematronResult Stopped(SchematronCompiledModel schema, string phase, SchemaValidationOperation operation)
        => new(operation.Complete(), phase, schema.Title, schema.Namespaces, [], [], []);
    internal static SchematronResult Evaluate(SchematronCompiledModel schema, XmlDocument document, SchematronValidationOptions options, string phase, SchemaValidationOperation operation)
        => new Run(schema, document, options, phase, operation).Evaluate();

    private sealed class StoppedRun : Exception;
    private sealed class Run(SchematronCompiledModel schema, XmlDocument document, SchematronValidationOptions options, string phase, SchemaValidationOperation operation)
    {
        private readonly long revision = document.ContentRevision;
        private readonly List<SchematronActivePattern> active = [];
        private readonly List<SchematronFiredRule> fired = [];
        private readonly List<SchematronFinding> findings = [];
        private void Check()
        {
            operation.CancellationToken.ThrowIfCancellationRequested();
            if (revision < 0 || document.ContentRevision != revision)
            {
                throw new InvalidOperationException("Instance changed during Schematron validation.");
            }
        }
        private void Charge(long amount = 1, XmlLocation location = default)
        {
            Check();
            if (!operation.Charge(amount, location))
            {
                throw new StoppedRun();
            }
        }
        private void Refuse(string code, string message, XmlLocation location = default)
        {
            operation.Refuse(code, message, location);
            throw new StoppedRun();
        }
        internal SchematronResult Evaluate()
        {
            Check();
            try
            {
                if (!document.SourceInputComplete)
                {
                    Refuse("SCHEMA.IncompleteInput", "Instance input is incomplete.");
                }

                var variables = new Dictionary<XPathExpandedName, XPathValue>();
                // Document focus is created by M4 inside the accounted operation.
                Lets(schema.Lets, document.DocumentNode, variables);
                var selected = phase == "#ALL" ? null : schema.Phases[phase];
                if (selected is not null)
                {
                    Lets(selected.Lets, document.DocumentNode, variables);
                }

                foreach (var pattern in schema.Patterns)
                {
                    Charge();
                    if (selected is not null && !Active(selected, pattern.Key))
                    {
                        continue;
                    }

                    Charge();
                    active.Add(new(pattern.Key, pattern.Id, pattern.Name));
                    var scoped = Copy(variables);
                    Lets(pattern.Lets, document.DocumentNode, scoped);
                    Pattern(pattern, scoped);
                }
            }
            catch (StoppedRun) { }
            finally { Check(); }
            var validation = operation.Complete();
            if (!validation.IsComplete && findings.Count == operation.Limits.Diagnostics)
            {
                findings.RemoveAt(findings.Count - 1);
            }

            return new(validation, phase, schema.Title, schema.Namespaces, active, fired, findings);
        }
        private bool Active(SchematronCompiledPhase selected, int key)
        {
            foreach (var candidate in selected.ActivePatternKeys)
            {
                Charge();
                if (candidate == key)
                {
                    return true;
                }
            }
            return false;
        }
        private Dictionary<XPathExpandedName, XPathValue> Copy(Dictionary<XPathExpandedName, XPathValue> source)
        {
            var result = new Dictionary<XPathExpandedName, XPathValue>();
            foreach (var pair in source)
            {
                Charge();
                result.Add(pair.Key, pair.Value);
            }
            return result;
        }
        private XPathContext Context(Dictionary<XPathExpandedName, XPathValue> variables) => new() { Variables = variables, Limits = options.XPathLimits, CancellationToken = operation.CancellationToken };
        private XPathValue Eval(XPathExpression expression, XmlNode node, Dictionary<XPathExpandedName, XPathValue> variables, XmlLocation location = default)
        {
            Check();
            try
            {
                return SchemaXPath.Evaluate(expression, node, Context(variables), operation, location);
            }
            catch (XPathEvaluationException error) { Refuse("SCH.XPath", error.Diagnostic.Message, location); return default; }
            catch (InvalidOperationException) when (!operation.IsComplete) { throw new StoppedRun(); }
            finally { Check(); }
        }
        private XPathValue Eval(XPathExpression expression, XPathItem item, Dictionary<XPathExpandedName, XPathValue> variables, XmlLocation location = default)
        {
            Check();
            try
            {
                return SchemaXPath.Evaluate(expression, item, Context(variables), operation, location);
            }
            catch (XPathEvaluationException error) { Refuse("SCH.XPath", error.Diagnostic.Message, location); return default; }
            catch (InvalidOperationException) when (!operation.IsComplete) { throw new StoppedRun(); }
            finally { Check(); }
        }
        private void Lets(IReadOnlyList<SchematronLet> lets, XmlNode node, Dictionary<XPathExpandedName, XPathValue> variables)
        {
            foreach (var let in lets)
            {
                Charge();
                var value = Eval(let.Value, node, variables);
                Charge();
                variables[let.Name] = value;
            }
        }
        private void Lets(IReadOnlyList<SchematronLet> lets, XPathItem item, Dictionary<XPathExpandedName, XPathValue> variables)
        {
            foreach (var let in lets)
            {
                Charge();
                var value = Eval(let.Value, item, variables);
                Charge();
                variables[let.Name] = value;
            }
        }
        private void Pattern(SchematronCompiledPattern pattern, Dictionary<XPathExpandedName, XPathValue> variables)
        {
            // The table belongs only to this pattern/run. TryAdd preserves the first authored rule.
            var matches = new Dictionary<ItemIdentity, (XPathItem Item, SchematronCompiledRule Rule)>();
            foreach (var rule in pattern.Rules)
            {
                Charge();
                var selected = Eval(rule.ContextSelector, document.DocumentNode, variables, rule.Location);
                foreach (var item in selected.AsNodeSet())
                {
                    Charge();
                    matches.TryAdd(item.Identity, (item, rule));
                }
            }
            // Walk live topology with revision guards instead of sorting with an unaccounted comparator.
            for (var node = document.DocumentNode; !node.IsDefault;)
            {
                Charge();
                var kind = node.Kind switch
                {
                    XmlNodeKind.Document => XPathItemKind.Root,
                    XmlNodeKind.Element => XPathItemKind.Element,
                    XmlNodeKind.Text or XmlNodeKind.CData => XPathItemKind.Text,
                    XmlNodeKind.Comment => XPathItemKind.Comment,
                    XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction,
                    _ => (XPathItemKind)(-1)
                };
                if (matches.TryGetValue(new(kind, node, default, null), out var match))
                {
                    Fire(pattern, match.Rule, match.Item, variables);
                }

                if (node.Kind == XmlNodeKind.Element)
                {
                    var attributes = new List<XmlAttribute>();
                    foreach (var attribute in node.Attributes)
                    {
                        Charge();
                        attributes.Add(attribute);
                    }
                    for (var i = 1; i < attributes.Count; i++)
                    {
                        var value = attributes[i];
                        var j = i;
                        while (j > 0 && Compare(attributes[j - 1], value) > 0)
                        {
                            Charge();
                            attributes[j] = attributes[j - 1];
                            j--;
                        }
                        attributes[j] = value;
                    }
                    foreach (var attribute in attributes)
                    {
                        Charge();
                        if (matches.TryGetValue(new(XPathItemKind.Attribute, node, attribute, null), out match))
                        {
                            Fire(pattern, match.Rule, match.Item, variables);
                        }
                    }
                }
                if (!node.FirstChild.IsDefault)
                {
                    node = node.FirstChild;
                    continue;
                }
                while (node != document.DocumentNode && node.NextSibling.IsDefault)
                {
                    Charge();
                    node = node.Parent;
                }
                node = node == document.DocumentNode ? default : node.NextSibling;
            }
        }
        private int Compare(XmlAttribute left, XmlAttribute right)
        {
            var value = Compare(left.NamespaceUri, right.NamespaceUri);
            return value == 0 ? Compare(left.LocalName, right.LocalName) : value;
        }
        private int Compare(string left, string right)
        {
            for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
            {
                Charge();
                if (left[i] != right[i])
                {
                    return left[i].CompareTo(right[i]);
                }
            }
            return left.Length.CompareTo(right.Length);
        }
        private void Fire(SchematronCompiledPattern pattern, SchematronCompiledRule rule, XPathItem item, Dictionary<XPathExpandedName, XPathValue> variables)
        {
            var scoped = Copy(variables);
            Lets(rule.Lets, item, scoped);
            var contextPath = Path(item);
            var contextSource = Source(item);
            Charge();
            var key = fired.Count;
            fired.Add(new(key, pattern.Key, rule.Key, rule.ContextSource, contextPath));
            foreach (var check in rule.Checks)
            {
                Charge();
                var test = Eval(check.Test, item, scoped, check.Location).AsBoolean();
                if (test == (check.Kind == SchematronFindingKind.FailedAssert))
                {
                    continue;
                }

                var subject = item;
                if ((check.Subject ?? rule.Subject) is { } expression)
                {
                    var value = Eval(expression, item, scoped, check.Location);
                    if (value.Kind != XPathValueKind.NodeSet)
                    {
                        Refuse("SCH.XPath", "A Schematron subject must be a node-set.", check.Location);
                    }

                    var nodes = value.AsNodeSet();
                    subject = nodes.Count == 0 ? default : nodes[0];
                }
                var location = subject.IsDefault ? "" : Path(subject);
                var source = subject.IsDefault ? default : Source(subject);
                var text = Text(check.Text, item, scoped, check.Location);
                var diagnostics = new List<SchematronDiagnosticReference>();
                foreach (var id in check.Diagnostics)
                {
                    Charge();
                    diagnostics.Add(new(id, Text(schema.Diagnostics[id], item, scoped, check.Location)));
                }
                var properties = new List<SchematronPropertyReference>();
                foreach (var id in check.Properties)
                {
                    Charge();
                    properties.Add(new(id, Text(schema.Properties[id], item, scoped, check.Location)));
                }
                Charge();
                var failed = check.Kind == SchematronFindingKind.FailedAssert;
                if (!operation.AddDiagnostic(new(failed ? "SCH.Assert" : "SCH.Report", failed ? XmlDiagnosticSeverity.Error : XmlDiagnosticSeverity.Warning,
                    failed ? "Schematron assertion failed." : "Schematron report succeeded.", source)))
                {
                    // The terminal entry replaces the final diagnostic slot. Findings share that cap.
                    if (findings.Count == operation.Limits.Diagnostics)
                    {
                        findings.RemoveAt(findings.Count - 1);
                    }

                    throw new StoppedRun();
                }
                findings.Add(new(check.Kind, check.Id, check.Role, check.Flag, pattern.Key, key, pattern.Id, rule.ContextSource, check.TestSource,
                    location, source, contextPath, contextSource, text, diagnostics, properties));
            }
        }
        private string Text(IReadOnlyList<SchematronTextPart> parts, XPathItem item, Dictionary<XPathExpandedName, XPathValue> variables, XmlLocation location)
        {
            var result = new StringBuilder();
            foreach (var part in parts)
            {
                Charge();
                var text = part switch
                {
                    SchematronLiteral literal => literal.Text,
                    SchematronValueOf value => Eval(value.Select, item, variables, location).AsString(),
                    SchematronNameOf { Path: null } => item.Name,
                    SchematronNameOf name => Name(Eval(name.Path!, item, variables, location), location),
                    _ => throw new InvalidOperationException("Unknown compiled text part.")
                };
                Append(result, text);
            }
            Charge();
            return result.ToString();
        }
        private string Name(XPathValue value, XmlLocation location)
        {
            if (value.Kind != XPathValueKind.NodeSet)
            {
                Refuse("SCH.XPath", "A Schematron name path must be a node-set.", location);
            }

            var nodes = value.AsNodeSet();
            return nodes.Count == 0 ? "" : nodes[0].Name;
        }
        private void Append(StringBuilder result, ReadOnlySpan<char> text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                Charge();
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }
            }
            if (text.Length > int.MaxValue - result.Length || options.XPathLimits.MaxStringCharacters is int cap && text.Length > cap - result.Length)
            {
                Refuse("SCH.XPath", "Schematron expanded text limit exceeded.");
            }

            result.Append(text);
        }
        private XmlLocation Source(XPathItem item)
        {
            Charge();
            if (item.Attribute is { } attribute)
            {
                return document.OriginalLocation(attribute);
            }

            if (item.Kind == XPathItemKind.Namespace)
            {
                for (var owner = item.Identity.Node; !owner.IsDefault; owner = owner.Parent)
                {
                    Charge();
                    foreach (var declaration in owner.NamespaceDeclarations)
                    {
                        Charge();
                        if (Compare(declaration.Prefix, item.LocalName) == 0)
                        {
                            return document.OriginalLocation(declaration);
                        }
                    }
                }
                return new(document.ParsedSourceUri, 0, 0, null);
            }
            return document.OriginalLocation(item.Identity.Node);
        }
        private string Path(XPathItem item)
        {
            if (item.Kind == XPathItemKind.Root)
            {
                return "/";
            }

            var steps = new List<string>();
            if (item.Kind == XPathItemKind.Attribute)
            {
                steps.Add("@*[local-name()=" + Literal(item.LocalName) + " and namespace-uri()=" + Literal(item.NamespaceUri) + "]");
            }
            else if (item.Kind == XPathItemKind.Namespace)
            {
                steps.Add("namespace::*[name()=" + Literal(item.LocalName) + "]");
            }

            var node = item.Identity.Node;
            for (; !node.IsDefault && node.Kind != XmlNodeKind.Document; node = node.Parent)
            {
                Charge();
                var position = 1;
                var precedingTextHasCharacters = false;
                for (var prior = node.PreviousSibling; !prior.IsDefault; prior = prior.PreviousSibling)
                {
                    Charge();
                    if (XPathItem.IsText(node))
                    {
                        // XPath exposes nonempty maximal text/CDATA runs only. A
                        // physical empty run separated by a comment consumes no position.
                        if (!XPathItem.IsText(prior))
                        {
                            continue;
                        }

                        precedingTextHasCharacters |= prior.Value.Length != 0;
                        if (!XPathItem.IsText(prior.PreviousSibling))
                        {
                            if (precedingTextHasCharacters)
                            {
                                position++;
                            }

                            precedingTextHasCharacters = false;
                        }
                    }
                    else if (node.Kind == XmlNodeKind.Element ? prior.Kind == XmlNodeKind.Element && Compare(prior.LocalName, node.LocalName) == 0 && Compare(prior.NamespaceUri, node.NamespaceUri) == 0 : prior.Kind == node.Kind)
                    {
                        position++;
                    }
                }
                var step = node.Kind switch
                {
                    XmlNodeKind.Element => "*[local-name()=" + Literal(node.LocalName) + " and namespace-uri()=" + Literal(node.NamespaceUri) + "]",
                    XmlNodeKind.Text or XmlNodeKind.CData => "text()",
                    XmlNodeKind.Comment => "comment()",
                    XmlNodeKind.ProcessingInstruction => "processing-instruction()",
                    _ => throw new InvalidOperationException("Unknown location node.")
                };
                steps.Add(step + "[" + position.ToString(CultureInfo.InvariantCulture) + "]");
            }
            var result = new StringBuilder();
            for (var i = steps.Count - 1; i >= 0; i--)
            {
                Append(result, "/");
                Append(result, steps[i]);
            }
            Charge();
            return result.ToString();
        }
        private string Literal(string text)
        {
            // Owned XPath location literals; mixed quotes use concat and retain all characters.
            var result = new StringBuilder();
            bool single = false, doubleQuote = false;
            foreach (var c in text)
            {
                Charge();
                single |= c == '\'';
                doubleQuote |= c == '"';
            }
            if (!single)
            {
                Append(result, "'");
                Append(result, text);
                Append(result, "'");
            }
            else if (!doubleQuote)
            {
                Append(result, "\"");
                Append(result, text);
                Append(result, "\"");
            }
            else
            {
                Append(result, "concat(");
                var start = 0;
                for (var i = 0; i < text.Length; i++)
                {
                    Charge();
                    if (text[i] != '\'')
                    {
                        continue;
                    }

                    if (start != 0)
                    {
                        Append(result, ",");
                    }

                    Append(result, "'");
                    Append(result, text.AsSpan(start, i - start));
                    Append(result, "',\"'\"");
                    start = i + 1;
                }
                if (start != 0)
                {
                    Append(result, ",");
                }

                Append(result, "'");
                Append(result, text.AsSpan(start));
                Append(result, "')");
            }
            Charge();
            return result.ToString();
        }
    }
}
