using SharpFuzz;
using System.Text.Json;

if (args.Length != 1 || !File.Exists(args[0]) || Path.GetFileName(args[0]) != "Xemele.dll")
    throw new ArgumentException("Pass the copied campaign Xemele.dll, never the production output.");
string path = Path.GetFullPath(args[0]);
if (!path.Contains(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    throw new ArgumentException("Instrumentation is confined to a disposable artifact copy.");
string[] instrumented = Fuzzer.Instrument(path, static name => name.StartsWith("Xemele.", StringComparison.Ordinal), Options.Value).ToArray();
if (instrumented.Length == 0) throw new InvalidOperationException("No candidate methods instrumented.");
Console.WriteLine(JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), methods = instrumented.Length, instrumented }));
