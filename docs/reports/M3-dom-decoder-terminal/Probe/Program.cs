using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele;

byte[] valid = "<r/>"u8.ToArray(), invalid = [0x3c, 0x72, 0xaf, 0x3e];
long coldStart = GC.GetTotalAllocatedBytes(true);
Require(XmlDocument.Parse(valid).RootElement.LocalName == "r");
long coldBytes = GC.GetTotalAllocatedBytes(true) - coldStart;
for (int i = 0; i < 64; i++) { Valid(); Invalid(); await AsyncInvalid(); }
const int iterations = 128;
long before = GC.GetTotalAllocatedBytes(true);
for (int i = 0; i < iterations; i++) Valid();
long validBytes = GC.GetTotalAllocatedBytes(true) - before;
before = GC.GetTotalAllocatedBytes(true);
for (int i = 0; i < iterations; i++) Invalid();
long invalidBytes = GC.GetTotalAllocatedBytes(true) - before;
before = GC.GetTotalAllocatedBytes(true);
for (int i = 0; i < iterations; i++) await AsyncInvalid();
long asyncInvalidBytes = GC.GetTotalAllocatedBytes(true) - before;
before = GC.GetTotalAllocatedBytes(true);
byte[]? positive = null;
for (int i = 0; i < iterations; i++) { positive = new byte[4096]; GC.KeepAlive(positive); }
long positiveBytes = GC.GetTotalAllocatedBytes(true) - before;
Require(validBytes > 0 && invalidBytes > 0 && asyncInvalidBytes > 0 && positiveBytes >= iterations * 4096L);
Console.WriteLine(JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), iterations,
    coldOwnedDomBytes = coldBytes, validOwnedDomBytes = validBytes, invalidOwnedDiagnosticBytes = invalidBytes,
    asyncInvalidOwnedDiagnosticAndSuspensionBytes = asyncInvalidBytes, positiveOwnedArrayBytes = positiveBytes,
    counter = "GC.GetTotalAllocatedBytes(true), process-wide; warm64; fixtures prepared before counters",
    scope = "DOM setup/persistence and diagnostics are owned allocations; no zero-allocation or comparative timing claim. Guard adds no allocation, buffers unchanged." }, new JsonSerializerOptions { WriteIndented = true }));
void Valid() => Require(XmlDocument.Parse(valid).RootElement.LocalName == "r");
void Invalid()
{
    Require(!XmlDocument.TryParse(invalid, out var d, out var diagnostics) && d is null && diagnostics.Count == 1 && diagnostics[0].Code == "INPUT.InvalidEncoding");
}
async Task AsyncInvalid()
{
    using var stream = new Delayed(invalid);
    var result = await XmlDocument.LoadWithDiagnosticsAsync(stream);
    Require(result.Value is null && !result.IsComplete && !result.IsWellFormed && result.Diagnostics.Count == 1 && result.Diagnostics[0].Code == "INPUT.InvalidEncoding" && stream.CanRead);
}
static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Probe oracle failed."); }
sealed class Delayed(byte[] bytes) : MemoryStream(bytes, false)
{
    public override int Read(Span<byte> buffer) => throw new InvalidOperationException("Synchronous input.");
    public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous input.");
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); await Task.Yield();
        int count = 0;
        while (count < buffer.Length)
        { int value = base.ReadByte(); if (value < 0) break; buffer.Span[count++] = (byte)value; }
        return count;
    }
}
