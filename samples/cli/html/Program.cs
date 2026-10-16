using System.Runtime.InteropServices;
using System.Text;
using Xemele.Tool;
if (RuntimeInformation.FrameworkDescription != ".NET 10.0.11") throw new InvalidOperationException("Unexpected runtime: " + RuntimeInformation.FrameworkDescription);
string folder = Path.Combine(Path.GetTempPath(), "xemele-m7-cli-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
int controls = 0;
try
{
    string input = Path.Combine(folder, "input.html"), output = Path.Combine(folder, "output.html");
    File.WriteAllText(input, "<!doctype html><p id='a'>sample<br>");
    var registry = new CommandRegistry();
    foreach (string command in new[] { "html-legacy", "html5", "html-select", "html-fragment" })
    {
        string[] extra = command switch
        {
            "html-select" => ["--selector", "#a"],
            "html-fragment" => ["--context", "div"],
            _ => []
        };
        Check(registry.Run([command, "--input", input, "--output", output, .. extra]) == 0 && File.ReadAllText(output).Contains("sample", StringComparison.Ordinal), command);
    }
    File.WriteAllText(input, "<r><a>XML</a></r>");
    Check(registry.Run(["parse", "--input", input, "--output", output]) == 0 && File.ReadAllText(output) == "<r><a>XML</a></r>", "XML preserved");
    foreach (string command in new[] { "html5", "html-legacy", "html-select", "html-fragment" })
    {
        string[] extra = command switch
        {
            "html-select" => ["--selector", "p"],
            "html-fragment" => ["--context", "div"],
            _ => []
        };
        using var borrowedInput = new MemoryStream(Encoding.UTF8.GetBytes("<p>borrowed"));
        using var borrowedOutput = new MemoryStream();
        Check(HtmlCommands.Run(command, extra, borrowedInput: borrowedInput, borrowedOutput: borrowedOutput) == 0, "borrowed command");
        Check(borrowedInput.CanRead && borrowedOutput.CanWrite, "success lifetime");
        using var faultOutput = new FailingStream(write: true);
        using var freshInput = new MemoryStream(Encoding.UTF8.GetBytes("<p>fault"));
        Check(HtmlCommands.Run(command, extra, borrowedInput: freshInput, borrowedOutput: faultOutput) == 3 && freshInput.CanRead && !faultOutput.Disposed, "output I/O lifetime");
        using var faultInput = new FailingStream();
        Check(HtmlCommands.Run(command, extra, borrowedInput: faultInput, borrowedOutput: borrowedOutput) == 3 && !faultInput.Disposed, "input I/O lifetime");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        File.WriteAllText(output, "preserve");
        Check(HtmlCommands.Run(command, [.. extra, "--output", output], canceled.Token, borrowedInput) == 3 && borrowedInput.CanRead && File.ReadAllText(output) == "preserve", "canceled lifetime/result");
    }
    using (var quotaInput = new MemoryStream(Encoding.UTF8.GetBytes("<p>quota")))
    using (var quotaOutput = new MemoryStream())
        Check(HtmlCommands.Run("html5", ["--max-characters", "1"], borrowedInput: quotaInput, borrowedOutput: quotaOutput) == 1 && quotaInput.CanRead && quotaOutput.Length == 0, "rejected lifetime/staging");
    using (var guarded = new HtmlFileInput(new FailingStream()))
    {
        try
        {
            _ = guarded.Read(new byte[1]);
            throw new InvalidOperationException("missing I/O mapping");
        }
        catch (HtmlInvocationException) { controls++; }
    }
    Console.OpenStandardOutput().Write([]);
    Check(Console.OpenStandardInput().CanRead, "standard input remains open");
    Console.WriteLine($"M7 CLI usage and ownership sample passed: {controls} controls; {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSArchitecture}");
}
finally { Directory.Delete(folder, true); }
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); controls++; }
sealed class FailingStream(bool write = false) : Stream
{
    internal bool Disposed;
    public override bool CanRead => !write;
    public override bool CanWrite => write;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException(); set => throw new NotSupportedException();
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new IOException("owned input I/O failure");
    public override int Read(Span<byte> buffer) => throw new IOException("owned input I/O failure");
    public override void Write(byte[] buffer, int offset, int count) => throw new IOException("owned output I/O failure");
    public override void Flush()
    {
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
