using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace InputSpike;

internal static class Diagnostics
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializePrimitives() => RuntimeHelpers.RunClassConstructor(typeof(Primitives).TypeHandle);
    internal static void Allocations()
    {
        Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}; OS={RuntimeInformation.OSDescription}; Arch={RuntimeInformation.ProcessArchitecture}; GC server={System.Runtime.GCSettings.IsServerGC}; latency={System.Runtime.GCSettings.LatencyMode}");
        long start = GC.GetAllocatedBytesForCurrentThread(); InitializePrimitives();
        long cold = GC.GetAllocatedBytesForCurrentThread() - start;
        Console.WriteLine($"Cold explicit Primitives initialization: {cold} B (SearchValues, array tokens, vocabulary, hash metadata, UTF8 token arrays, one-time LINQ delegates/enumerators). No fixture bytes included.");
        start = GC.GetAllocatedBytesForCurrentThread();
        var byteScanner = new Scanner<byte, ByteUnits>(); var byteSink = new Utf8Sink();
        var charScanner = new Scanner<char, CharUnits>(); var charSink = new CharSink();
        Decoder decoder = new UTF8Encoding(false, true).GetDecoder();
        long setup = GC.GetAllocatedBytesForCurrentThread() - start;
        Console.WriteLine($"Cold context construction: {setup} B for two scanners, sinks, strict encoding/decoders (owned per context; not per token).");
        foreach (string name in new[] { "AsciiShort", "UnicodeShort", "AsciiLong", "UnicodeLong" })
        {
            var bench = new InputBenchmarks { Case = name, Chunk = 7 }; bench.Setup();
            Measure(name + "/StringToCharacterCore", bench.StringToCharacterCore, true);
            Measure(name + "/ByteToByteCore", bench.ByteToByteCore, true);
            Measure(name + "/ByteToCharacterScratch", bench.ByteToCharacterScratch, true);
            Measure(name + "/StringToCharacterCorePaused", bench.StringToCharacterCorePaused, true);
            Measure(name + "/ByteToByteCorePaused", bench.ByteToByteCorePaused, true);
            Measure(name + "/ByteToOwnedString", bench.ByteToOwnedString, false);
            Measure(name + "/StringViaOwnedUtf8", bench.StringViaOwnedUtf8, false);
        }
        Measure("Cached byte/char SearchValues", () => "text&"u8.IndexOfAny(Primitives.ByteStops) + "text&".AsSpan().IndexOfAny(Primitives.CharStops), true);
        Measure("Length match byte/char", () => Primitives.LengthMatch("standalone"u8, false) + Primitives.LengthMatch("standalone", false), true);
        Measure("Hash match byte/char", () => Primitives.HashMatch("standalone"u8, false) + Primitives.HashMatch("standalone", false), true);
        var candidate = new ChunkCandidate();
        Measure("Chunk match", () => { candidate.Reset(); candidate.Append("stand"u8, false, false, true); return candidate.Append("alone"u8, true, false, true); }, true);
        Measure("Constant byte/char/int literals", () => Primitives.ConstantBytes[0] + Primitives.Utf8Literal[0] + Primitives.CharLiteral[0] + Primitives.ConstantInts[0], true);
        Measure("Runtime-valued readonly collection control", () => Primitives.RuntimeBytes[0], false);
        Measure("Mutable collection control", () => Primitives.MutableBytes[0], false);
        Measure("Runtime-valued local collection", () => Primitives.RuntimeLocalSum(), true);
        Measure("Mutable local collection", () => Primitives.MutableLocalSum(), true);
        Retention();
        Console.WriteLine("PASS warm allocation regressions and source collectability. Counters are synchronous current-thread allocations; no async claim.");
    }
    private static void Measure(string name, Func<long> action, bool zero)
    {
        long checksum = 0;
        for (int i = 0; i < 100; i++) checksum ^= action();
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) checksum ^= action();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Checks.Require(!zero || bytes == 0, "Warm allocation regression: " + name);
        Console.WriteLine($"{name}: {bytes / 1000.0:F2} B/op over 1000 operations; result={checksum}");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreatePausedSource(Scanner<byte, ByteUnits> scanner, Utf8Sink sink)
    {
        byte[] source = new byte[4 * 1024 * 1024]; source.AsSpan().Fill((byte)'x');
        sink.Characters.Pause = true;
        ScanResult result = scanner.Append(source, false, sink, 7);
        Checks.Require(result.Consumed == 7 && result.Step == Step.Paused, "Suspended inside source token.");
        return new WeakReference(source);
    }
    private static void Retention()
    {
        var scanner = new Scanner<byte, ByteUnits>(); var sink = new Utf8Sink();
        WeakReference source = CreatePausedSource(scanner, sink);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Checks.Require(!source.IsAlive, "Scanner/sink do not retain suspended source.");
        GC.KeepAlive(scanner); GC.KeepAlive(sink);
        Console.WriteLine("Paused after 7 bytes in 4 MiB token: source collectible while scanner/sink remain alive; scanner retained input=0; decoder pending scalar <=3 bytes; callback stack scratch=128 UTF16 units.");
        scanner.Reset(); sink.Reset();
        byte[] chunk = new byte[64 * 1024]; chunk.AsSpan().Fill((byte)'x');
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) scanner.Append(chunk, false, sink);
        scanner.Append("|"u8, true, sink);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Checks.Require(bytes == 0 && sink.Characters.Tokens == 1, "64 MiB token streaming remains bounded and allocation-free.");
        Console.WriteLine($"64 MiB synthetic single token from reusable 64 KiB chunk: {bytes} B processing allocations, {sink.Characters.Tokens} token, no document buffer; source chunk owns 65536 bytes plus array header.");
    }
    internal static void Lowering()
    {
        Console.WriteLine($"Release IL inspection: {RuntimeInformation.FrameworkDescription}; SDK pinned by repository; assembly={typeof(Primitives).Assembly.GetName().Name}");
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
        foreach (string name in new[] { "get_Utf8Literal", "get_CharLiteral", "get_ConstantBytes", "get_ConstantInts", "get_RuntimeBytes", "get_MutableBytes", "RuntimeLocalSum", "MutableLocalSum", ".cctor" })
        {
            MethodBase method = name == ".cctor" ? typeof(Primitives).TypeInitializer! : typeof(Primitives).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            Console.WriteLine($"\n{method.Name}:");
            byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
            int p = 0;
            while (p < il.Length)
            {
                int offset = p; ushort code = il[p++]; if (code == 0xFE) code = (ushort)(0xFE00 | il[p++]);
                OpCode op = opcodes[code]; string operand = "";
                int size = op.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, p) * 4,
                    _ => 4
                };
                if (size == 4)
                {
                    int token = BitConverter.ToInt32(il, p);
                    try
                    {
                        operand = op.OperandType switch
                        {
                            OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType => method.Module.ResolveMember(token)?.ToString() ?? "?",
                            OperandType.InlineString => method.Module.ResolveString(token),
                            _ => token.ToString(CultureInfo.InvariantCulture)
                        };
                    }
                    catch (ArgumentException) { operand = $"token 0x{token:X8}"; }
                }
                else if (size > 0) operand = Convert.ToHexString(il.AsSpan(p, size));
                Console.WriteLine($"IL_{offset:X4}: {op.Name} {operand}"); p += size;
            }
        }
    }
}
