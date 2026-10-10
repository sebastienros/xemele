using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dtd;
using Xemele.Tests;
using Xunit;

if (args.Length != 1 || args[0] is not ("original" or "equivalent" or "helper")) throw new ArgumentException("Known diagnostic mode required.");
using var framer = new XmlTextDeclarationFramer(new() { TokenCharacters = null, ExpandedCharacters = null });
var cancellation = TestContext.Current.CancellationToken;
var prefix = framer.Append("<?xml ", [], false, cancellation);
if (prefix.Consumed != 6 || prefix.Step != TextDeclarationStep.NeedInput || prefix.Diagnostic is not null) throw new InvalidOperationException("Prefix mismatch.");
object? escaped = null;
(long Bytes, long Consumed, XmlLocation Cursor) warm, measured;
if (args[0] == "original")
{
    warm = Original(framer);
    measured = Original(framer);
}
else if (args[0] == "equivalent")
{
    warm = Equivalent(framer, cancellation);
    measured = Equivalent(framer, cancellation);
}
else
{
    warm = FramingAllocationProbe.AppendChecked(framer, cancellation, false, ref escaped);
    measured = FramingAllocationProbe.AppendChecked(framer, cancellation, false, ref escaped);
}
var completion = framer.Append(FramingAllocationProbe.Completion, [], true, cancellation);
if (warm.Consumed != 32000 || measured.Consumed != 32000 || warm.Cursor != new XmlLocation(null,1,32007,null)
    || measured.Cursor != new XmlLocation(null,1,64007,null) || completion.Step != TextDeclarationStep.Declared
    || completion.Consumed != FramingAllocationProbe.Completion.Length || completion.Diagnostic is not null
    || completion.NextLocation != new XmlLocation(null,1,64007+FramingAllocationProbe.Completion.Length,null))
    throw new InvalidOperationException("Diagnostic workload/cursor mismatch.");
Console.WriteLine(JsonSerializer.Serialize(new { mode=args[0], runtime=Environment.Version.ToString(),
    productVersion=typeof(XmlTextDeclarationFramer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    warmIterations=1000, measuredIterations=1000, warmConsumed=warm.Consumed, measuredConsumed=measured.Consumed,
    warmBytes=warm.Bytes, primitiveBytes=measured.Bytes, warmColumn=warm.Cursor.LinePosition, measuredColumn=measured.Cursor.LinePosition,
    finalColumn=completion.NextLocation.LinePosition, completion=completion.Step.ToString(),
    scope="Bounded diagnostic once per fresh process; original reproduces TestContext lookup outside a running Fact, so cannot attribute historical test failure." }));

// Preserve the original per-iteration context lookup and counter placement.
// Result consumption/checks prevent an incomplete primitive from appearing successful.
[MethodImpl(MethodImplOptions.NoInlining)]
static (long Bytes, long Consumed, XmlLocation Cursor) Original(XmlTextDeclarationFramer framer)
{
    long before=GC.GetAllocatedBytesForCurrentThread(); long consumed=0; XmlLocation cursor=default;
    for(int i=0;i<1000;i++)
    {
        var result=framer.Append(FramingAllocationProbe.Spaces,[],false,TestContext.Current.CancellationToken);
        if(result.Consumed!=32 || result.Step!=TextDeclarationStep.NeedInput || result.Diagnostic is not null)throw new InvalidOperationException("Original work mismatch.");
        consumed+=result.Consumed;cursor=result.NextLocation;
    }
    return(GC.GetAllocatedBytesForCurrentThread()-before,consumed,cursor);
}
[MethodImpl(MethodImplOptions.NoInlining)]
static (long Bytes, long Consumed, XmlLocation Cursor) Equivalent(XmlTextDeclarationFramer framer,CancellationToken cancellation)
{
    long before=GC.GetAllocatedBytesForCurrentThread(); long consumed=0; XmlLocation cursor=default;
    for(int i=0;i<1000;i++)
    {
        var result=framer.Append(FramingAllocationProbe.Spaces,[],false,cancellation);
        if(result.Consumed!=32 || result.Step!=TextDeclarationStep.NeedInput || result.Diagnostic is not null || result.Declaration is not null)throw new InvalidOperationException("Equivalent work mismatch.");
        consumed+=result.Consumed;cursor=result.NextLocation;
    }
    return(GC.GetAllocatedBytesForCurrentThread()-before,consumed,cursor);
}
