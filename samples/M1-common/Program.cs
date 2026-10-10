using Xemele;
var options = new XmlParseOptions { Limits = new() { DocumentCharacters = 1024, Depth = 8 } };
options.Limits.Validate();
var diagnostic = new XmlDiagnostic("sample.syntax", XmlDiagnosticSeverity.Fatal,
    "Example rejection", new(null, 1, 2, null));
var result = new XmlParseResult<string>(null, false, false, [diagnostic]);
Console.WriteLine($"complete={result.IsComplete}; wellFormed={result.IsWellFormed}; code={result.Diagnostics[0].Code}");
Console.WriteLine(new XmlParseException(result.Diagnostics).Message);
