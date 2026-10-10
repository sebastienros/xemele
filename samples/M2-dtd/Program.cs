using System.Text.Json;
using Xemele;
using Xemele.DtdParentSample;
using Xemele.Parsing.Dtd;
if (args.Contains("--runtime")) { Console.WriteLine(Environment.Version); return; }
if (args.Contains("--contract")) { Inventory.Print(); return; }
var result = Join.Run();
Console.WriteLine(JsonSerializer.Serialize(new {
    elements = result.Model.Elements.Select(d => new { d.Name, kind = d.Content.Kind.ToString(),
        occurrence = d.Content.Occurrence.ToString(), children = d.Content.Children.Select(c => new { c.Name, occurrence = c.Occurrence.ToString() }), d.IsExternal }),
    attributes = result.Model.Attributes.Select(d => new { d.ElementName, d.Name, type = d.Type.ToString(), kind = d.DefaultKind.ToString(), d.DefaultValue, d.IsExternal }),
    result.Defaults, internalPrecedence = !result.Model.GeneralEntities[0].IsExternal
}));

// This fixed caller grant supplies an external PE; the parser never opens a resource.
const string parent = "<!ENTITY % granted SYSTEM 'approved.dtd'>%granted;<!ELEMENT parent EMPTY>";
const string approved = "<?xml encoding='UTF-8'?><!ELEMENT child EMPTY>";
var parentUri = new Uri("https://sample.invalid/subset.dtd");
var finalUri = new Uri("https://sample.invalid/final-approved.dtd");
using var parser = new DtdParser(DtdSubsetKind.Internal, new(), new XmlLocation(parentUri, 9, 7, null));
var request = parser.Append(parent, [], true, default);
if (request.Step != DtdParseStep.NeedEntity || request.Entity is not { Name: "granted", SystemId: "approved.dtd", PublicId: null } || request.Diagnostic is not null)
    throw new InvalidOperationException("Unexpected resource request.");
int suffix = request.Consumed;
parser.PushEntity(finalUri);
if (parser.Append(approved, [], true, default).Step != DtdParseStep.NeedInput)
    throw new InvalidOperationException("Pushed-source exhaustion must restore the parent.");
if (parser.Append(parent.AsSpan(suffix), [], true, default).Step != DtdParseStep.Completed)
    throw new InvalidOperationException("Saved parent suffix did not complete.");
Console.WriteLine(JsonSerializer.Serialize(new {
    request = request.Entity.Value.Name, systemId = request.Entity.Value.SystemId, consumed = suffix,
    declarations = parser.Model.Elements.Select(d => new { d.Name, d.IsExternal, uri = d.Location.SourceUri!.AbsoluteUri,
        line = d.Location.LineNumber, column = d.Location.LinePosition, byteOffset = d.Location.ByteOffset })
}));
