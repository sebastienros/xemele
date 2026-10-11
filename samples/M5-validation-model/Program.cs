using System.Runtime.InteropServices;
using Xemele;
using Xemele.Validation;

if (RuntimeInformation.FrameworkDescription != ".NET 10.0.11") throw new Exception("Pinned runtime required.");
var options = new ValidationOptions { MaxOperations = 42, Limits = new() { Diagnostics = 2 } };
var valid = new ValidationResult(true, []);
var invalid = new ValidationResult(true, [new("DTD.Content", XmlDiagnosticSeverity.Error, "Content mismatch.", default)]);
var incomplete = new ValidationResult(false, [new("VALIDATION.Limit", XmlDiagnosticSeverity.Fatal, "Work exhausted.", default)]);
Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; cap={options.MaxOperations}; valid={valid.IsValid}; invalid={invalid.IsComplete}/{invalid.IsValid}; incomplete={incomplete.IsComplete}/{incomplete.IsValid}");
