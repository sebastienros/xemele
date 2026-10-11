using System.Runtime.InteropServices;
using Xemele.Tool;
string folder = Path.Combine(Path.GetTempPath(), "xemele-cli-sample-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    string input = Path.Combine(folder, "input.xml"), output = Path.Combine(folder, "output.xml");
    File.WriteAllText(input, "<r><item>one</item></r>");
    var registry = new CommandRegistry();
    foreach (string command in new[] { "parse", "format", "canonicalize", "xinclude" })
        if (registry.Run([command, "--input", input, "--output", output]) != 0 || !File.ReadAllText(output).Contains("item", StringComparison.Ordinal)) throw new InvalidOperationException(command);
    if (registry.Run(["xpath", "--input", input, "--output", output, "--expression", "count(/r/item)"]) != 0 || File.ReadAllText(output) != "1\n") throw new InvalidOperationException("xpath");
    string dtd = Path.Combine(folder, "schema.dtd"); File.WriteAllText(dtd, "<!ELEMENT r (item)><!ELEMENT item (#PCDATA)>");
    if (registry.Run(["validate-dtd", "--input", input, "--dtd", dtd, "--output", output]) != 0 || new FileInfo(output).Length != 0) throw new InvalidOperationException("dtd");
    string catalog = Path.Combine(folder, "catalog.xml");
    File.WriteAllText(catalog, "<catalog xmlns=\"urn:oasis:names:tc:entity:xmlns:xml:catalog\"><uri name=\"urn:sample\" uri=\"input.xml\"/></catalog>");
    if (registry.Run(["catalog", "--catalog", catalog, "--uri", "urn:sample", "--output", output]) != 0 || File.ReadAllText(output).Trim() != new Uri(input).AbsoluteUri) throw new InvalidOperationException("catalog");
    using (var canceled = new CancellationTokenSource())
    {
        canceled.Cancel();
        File.WriteAllText(output, "preserve");
        if (XmlCommands.Run("parse", ["--input", input, "--output", output], canceled.Token) != 3 || File.ReadAllText(output) != "preserve") throw new InvalidOperationException("cancellation");
    }
    Console.OpenStandardOutput().Write([]);
    if (!Console.OpenStandardInput().CanRead) throw new InvalidOperationException("stdin closed");
    Console.WriteLine("Seven command and cancellation sample passed; " + RuntimeInformation.FrameworkDescription);
}
finally { Directory.Delete(folder, true); }
