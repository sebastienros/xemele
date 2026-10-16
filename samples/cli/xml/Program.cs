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
        if (registry.Run([command, "--input", input, "--output", output]) != 0 || !File.ReadAllText(output).Contains("item", StringComparison.Ordinal))
            throw new InvalidOperationException(command);
    if (registry.Run(["xpath", "--input", input, "--output", output, "--expression", "count(/r/item)"]) != 0 || File.ReadAllText(output) != "1\n")
        throw new InvalidOperationException("xpath");
    string dtd = Path.Combine(folder, "schema.dtd");
    File.WriteAllText(dtd, "<!ELEMENT r (item)><!ELEMENT item (#PCDATA)>");
    if (registry.Run(["validate-dtd", "--input", input, "--dtd", dtd, "--output", output]) != 0 || new FileInfo(output).Length != 0)
        throw new InvalidOperationException("dtd");
    string catalog = Path.Combine(folder, "catalog.xml");
    File.WriteAllText(catalog, "<catalog xmlns=\"urn:oasis:names:tc:entity:xmlns:xml:catalog\"><uri name=\"urn:sample\" uri=\"input.xml\"/></catalog>");
    if (registry.Run(["catalog", "--catalog", catalog, "--uri", "urn:sample", "--output", output]) != 0 || File.ReadAllText(output).Trim() != new Uri(input).AbsoluteUri)
        throw new InvalidOperationException("catalog");
    string declarations = Path.Combine(folder, "elements.dtd");
    File.WriteAllText(declarations, "<!ELEMENT r (item)><!ELEMENT item (#PCDATA)>");
    File.WriteAllText(dtd, "<!ENTITY % declarations SYSTEM \"elements.dtd\">%declarations;");
    if (registry.Run(["validate-dtd", "--input", input, "--dtd", dtd, "--allow-root", folder, "--output", output]) != 0)
        throw new InvalidOperationException("relative parameter entity");
    File.WriteAllText(Path.Combine(folder, "entity.ent"), "relative entity");
    File.WriteAllText(input, "<!DOCTYPE r [<!ENTITY e SYSTEM \"entity.ent\">]><r>&e;</r>");
    if (registry.Run(["parse", "--input", input, "--allow-dtd", "--allow-root", folder, "--output", output]) != 0 || File.ReadAllText(output) != "<r>relative entity</r>")
        throw new InvalidOperationException("relative general entity");
    using (var canceled = new CancellationTokenSource())
    {
        canceled.Cancel();
        File.WriteAllText(output, "preserve");
        if (XmlCommands.Run("parse", ["--input", input, "--output", output], canceled.Token) != 3 || File.ReadAllText(output) != "preserve")
            throw new InvalidOperationException("cancellation");
    }
    foreach (bool invocation in new[] { true, false })
    {
        var guarded = new GuardedFileInput(new FailingInput(), invocation);
        try
        {
            _ = guarded.Read(new byte[1]);
            throw new InvalidOperationException("read did not reject");
        }
        catch (InvocationException) when (invocation) { }
        catch (RejectionException) when (!invocation) { }
        try
        {
            guarded.Dispose();
            throw new InvalidOperationException("dispose did not reject");
        }
        catch (InvocationException) when (invocation) { }
        catch (RejectionException) when (!invocation) { }
    }
    var unexpected = new InvalidOperationException("unexpected resolver failure");
    var unexpectedInput = new GuardedFileInput(new FailingInput(unexpected), false);
    try
    {
        _ = unexpectedInput.Read(new byte[1]);
        throw new InvalidOperationException("unexpected failure lost");
    }
    catch (InvalidOperationException ex) when (ReferenceEquals(ex, unexpected)) { }
    Console.OpenStandardOutput().Write([]);
    if (!Console.OpenStandardInput().CanRead)
        throw new InvalidOperationException("stdin closed");
    Console.WriteLine("Seven command and cancellation sample passed; " + RuntimeInformation.FrameworkDescription);
}
finally { Directory.Delete(folder, true); }

sealed class FailingInput(Exception? failure = null) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException(); set => throw new NotSupportedException();
    }
    public override int Read(byte[] buffer, int offset, int count) => throw failure ?? new IOException("owned test read");
    public override int Read(Span<byte> buffer) => throw failure ?? new IOException("owned test read");
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            throw failure ?? new IOException("owned test dispose");
        base.Dispose(disposing);
    }
}
