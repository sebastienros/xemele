using Xemele;
const string input = "<report id='first' id='second'><item>ready</report>";
var result = XmlDocument.ParseWithDiagnostics(input, new() { Recover = true });
Console.WriteLine($"complete={result.IsComplete}; wellFormed={result.IsWellFormed}; diagnostics={result.Diagnostics.Count}");
Console.WriteLine(result.Value!.ToXmlString(new() { OmitXmlDeclaration = true }));
foreach (var diagnostic in result.Diagnostics)
    Console.WriteLine($"{diagnostic.Code} at {diagnostic.Location.LineNumber}:{diagnostic.Location.LinePosition}; byteOffset={diagnostic.Location.ByteOffset?.ToString() ?? "absent"}");
// The returned tree is ordinary owned, editable storage.
result.Value.RootElement.SetAttribute("reviewed", "yes");
Console.WriteLine("edited=" + result.Value.RootElement.GetAttribute("reviewed"));
