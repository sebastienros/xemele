using Xemele;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
if (args.Contains("--contract")) { Inventory.Print(); return; }
if (args.Contains("--runtime")) { Console.WriteLine(Environment.Version); return; }
var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
var clone = new OperationBudget(options, default).Options;
var metadata = new XmlDocumentType("root", null, "relative.dtd", "<!ENTITY e 'owned'>");
var model = new DtdModel([], [], [new("e", false, "owned", null, null, null, false, default)], [], []);
var sink = new DomSink(clone);
Console.WriteLine($"defaults={new XmlParseOptions().DtdProcessing};resolver={new XmlParseOptions().Resolver is null};clone={clone.DtdProcessing};limitsOwned={!ReferenceEquals(options.Limits, clone.Limits)}");
Console.WriteLine($"manualCallback={((IXmlDtdSink)sink).DocumentType(metadata, model, default)};name={sink.Document.DocumentType!.Name};entity={sink.Document.Dtd!.GeneralEntities[0].ReplacementText};rootAbsent={sink.Document.RootElement.IsDefault}");
// Allocation boundaries are measured separately, after setup; no timing claim.
if (args.Contains("--allocations"))
{
    object? escaped = null;
    for (int i = 0; i < 10000; i++)
        escaped = new XmlDocumentType("root", null, null, null);
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++)
    {
        escaped = new XmlDocumentType("root", null, null, null);
        GC.KeepAlive(escaped);
    }
    long recordBytes = GC.GetAllocatedBytesForCurrentThread() - before;
    for (int i = 0; i < 10000; i++)
        escaped = options with
        {
            Limits = options.Limits with
            {
            }
        };
    before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++)
    {
        escaped = options with
        {
            Limits = options.Limits with
            {
            }
        };
        GC.KeepAlive(escaped);
    }
    long snapshotBytes = GC.GetAllocatedBytesForCurrentThread() - before;
    for (int i = 0; i < 10000; i++)
        ((IXmlDtdSink)sink).DocumentType(metadata, model, default);
    before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++)
        ((IXmlDtdSink)sink).DocumentType(metadata, model, default);
    long callbackBytes = GC.GetAllocatedBytesForCurrentThread() - before;
    Console.WriteLine($"allocations:iterations=10000;record={recordBytes};optionAndLimitsSnapshot={snapshotBytes};callback={callbackBytes}");
    if (recordBytes <= 0 || snapshotBytes <= 0 || callbackBytes != 0)
        throw new InvalidOperationException("Allocation boundary regression");
    GC.KeepAlive(escaped);
}
