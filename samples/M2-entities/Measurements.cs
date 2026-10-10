using System.Runtime.CompilerServices;
using Xemele;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;

// Explicit mode only. Initialization, ownership and retention are reported
// separately; the coordinator grants exclusive host measurements before use.
internal static class Measurements
{
    private const int Iterations = 10000;
    private static object?[] escaped = new object?[Iterations];
    internal static void Run()
    {
        ContentOracle(false); ContentOracle(true);
        var sink = new CountSink();
        using var ordinary = new XmlParserCore(new());
        Expect(ordinary.Append("<r>", [], false, sink, default), ScanStep.NeedInput);
        for (int i = 0; i < Iterations; i++) Expect(ordinary.Append("abcdefghijklmnop", [], false, sink, default), ScanStep.NeedInput);
        long ordinaryBytes = Allocated(Iterations, _ => Expect(ordinary.Append("abcdefghijklmnop", [], false, sink, default), ScanStep.NeedInput));
        if (sink.Characters != 32L * Iterations) throw new InvalidOperationException("Warm ordinary full-character oracle failed.");
        Expect(ordinary.Append("</r>", [], true, sink, default), ScanStep.Completed);

        sink = new(); using var entities = new XmlParserCore(new() { DtdProcessing = XmlDtdProcessing.Parse });
        Expect(entities.Append("<!DOCTYPE r [<!ENTITY e 'abcdefghijklmnop'>]><r>", [], false, sink, default), ScanStep.NeedInput);
        for (int i = 0; i < Iterations; i++) Expect(entities.Append("&e;", [], false, sink, default), ScanStep.NeedInput);
        long entityBytes = Allocated(Iterations, _ => Expect(entities.Append("&e;", [], false, sink, default), ScanStep.NeedInput));
        if (sink.Characters != 32L * Iterations) throw new InvalidOperationException("Warm entity full-character oracle failed.");
        Expect(entities.Append("</r>", [], true, sink, default), ScanStep.Completed);

        var model = new DtdModel([], [new("r", "a", DtdAttributeType.CData, [], DtdDefaultKind.Value, "v", false, default)],
            [new DtdEntityDeclaration("e", false, "value", null, null, null, false, default)], [], []);
        var context = new XmlEntityContext(model);
        for (int i = 0; i < Iterations; i++) { context.TryGetGeneralEntity("e", out _); context.GetAttributes("r"); }
        long lookupBytes = Allocated(Iterations, _ =>
        {
            if (!context.TryGetGeneralEntity("e", out var entity) || entity!.ReplacementText != "value" || context.GetAttributes("r").Count != 1)
                throw new InvalidOperationException("Borrowed effective lookup oracle failed.");
        });
        long coldCore = Allocated(Iterations, i => { var core = new XmlParserCore(new()); core.Dispose(); escaped[i] = core; });
        long coldContext = Allocated(Iterations, i => escaped[i] = new XmlEntityContext(model));
        long positive = Allocated(Iterations, i => escaped[i] = new byte[1]);
        if (positive == 0 || ordinaryBytes != 0 || entityBytes != 0 || lookupBytes != 0)
            throw new InvalidOperationException("Promised warm zero-allocation or escaped positive control failed.");
        var retained = Retention();
        // One controlled collection outside all allocation/timing intervals;
        // no GC/runtime configuration change or retry hides a failed lifetime.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
        if (retained.Input.IsAlive || retained.Document.RootElement.GetTextContent() != "owned" ||
            retained.Document.DocumentType?.Name != "r" || retained.Document.DocumentType.InternalSubset != "<!ENTITY e 'owned'>" ||
            retained.Document.Dtd?.GeneralEntities.Count != 1 || retained.Document.Dtd.GeneralEntities[0].Name != "e" ||
            retained.Document.Dtd.GeneralEntities[0].ReplacementText != "owned")
            throw new InvalidOperationException("Borrowed input retention/owned result oracle failed.");
        GC.KeepAlive(retained.Document); GC.KeepAlive(escaped);
        Console.WriteLine($"iterations={Iterations};ordinaryWarm={ordinaryBytes};entityWarm={entityBytes};borrowedLookup={lookupBytes};coldCore={coldCore};coldContext={coldContext};escapedPositive={positive}");
        Console.WriteLine("retention=callerCharBufferReleased;ownedDtdAndDomPreserved;oneCollectionOutsideIntervals");
    }

    private static void ContentOracle(bool entity)
    {
        var sink = new SampleSink();
        using var core = new XmlParserCore(new() { DtdProcessing = XmlDtdProcessing.Parse });
        string input = entity ? "<!DOCTYPE r [<!ENTITY e 'abcdefghijklmnop'>]><r>&e;&e;</r>" : "<r>abcdefghijklmnopqrstuvwxyz</r>";
        string expected = entity ? "abcdefghijklmnopabcdefghijklmnop" : "abcdefghijklmnopqrstuvwxyz";
        Expect(core.Append(input, [], true, sink, default), ScanStep.Completed);
        if (sink.Elements != "r" || sink.Text.ToString() != expected || sink.Metadata != (entity ? 1 : 0))
            throw new InvalidOperationException("Outside-interval actual callback content oracle failed.");
    }

    private static long Allocated(int count, Action<int> operation)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < count; i++) operation(i);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
    private static void Expect(ScanResult result, ScanStep expected)
    { if (result.Step != expected) throw new InvalidOperationException("Actual parser result oracle failed."); }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (XmlDocument Document, WeakReference Input) Retention()
    {
        char[] input = "<!DOCTYPE r [<!ENTITY e 'owned'>]><r>&e;</r>".ToCharArray();
        var document = XmlDocument.Parse(input.AsSpan(), new() { DtdProcessing = XmlDtdProcessing.Parse });
        Array.Fill(input, '!'); return (document, new WeakReference(input));
    }
    private sealed class CountSink : IXmlEventSink
    {
        internal long Characters;
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView declaration) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) => true;
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
        public bool Attribute(XmlAttributeView attribute) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) { Characters += text.Length; return true; }
        public bool EndDocument(XmlLocation location) => true;
    }
}
