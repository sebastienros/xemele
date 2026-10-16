using Xemele;
using Xemele.Parsing.Dtd;
if (args.Contains("--runtime")) { Console.WriteLine(Environment.Version); return; }
if (args.Contains("--hooks")) { Environment.ExitCode = HookInventory.Print() ? 0 : 1; return; }
if (args.Contains("--contract")) { Inventory.Print(); return; }
var uri = new Uri("https://fixture.invalid/effective.dtd");
var ledger = new OperationLedger();
using var prior = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(null, 1, 1, null));
if (prior.Append("<!ENTITY % x '<!ELEMENT child EMPTY>'><!ENTITY g 'inside'>", [], true, default).Step != DtdParseStep.Completed) throw new Exception("prior");
using var parser = new DtdParser(DtdSubsetKind.External, new(), ledger, new(uri, 1, 1, null));
parser.SeedPriorModel(prior.Model, default);
using var framer = new XmlTextDeclarationFramer(new(), ledger, uri);
const string text = "<?xml\r\nencoding='UTF-8'?>%x;<!ATTLIST child a CDATA '&g;'>";
var frame = framer.Append(text, [], true, default);
if (frame.Step != TextDeclarationStep.Declared) throw new Exception("frame");
parser.SetFramedInitialSource(frame.NextLocation);
if (parser.Append(text.AsSpan(frame.Consumed), [], true, default).Step != DtdParseStep.Completed) throw new Exception("grammar");
var attribute = parser.Model.Attributes.Single();
var value = DtdValueProcessor.NormalizeAttributeDefault(attribute.DefaultValue!, attribute.Type, parser.Model.GeneralEntities.ToDictionary(e => e.Name), new());
if (value.Diagnostic is not null) throw new Exception("value");
Console.WriteLine($"elements={string.Join(',', parser.Model.Elements.Select(e => e.Name))};default={value.Value};priorIdentity={ReferenceEquals(prior.Model.GeneralEntities[0], parser.Model.GeneralEntities[0])};source={attribute.Location.SourceUri};line={attribute.Location.LineNumber}");
using var second = new DtdParser(DtdSubsetKind.External, new(), uri);
second.SetFramedInitialSource(frame.NextLocation);
var rejected = second.Append("<?xml encoding='UTF-8'?>", [], true, default);
Console.WriteLine($"secondDeclaration={rejected.Step};code={rejected.Diagnostic?.Code};line={rejected.Diagnostic?.Location.LineNumber}");
