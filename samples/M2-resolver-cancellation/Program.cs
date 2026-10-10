using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xemele;
using Xemele.Internal.Operations;
using Xemele.Resolution.Core;

var method = typeof(XmlResolutionSession).GetMethod("BindCancellationToken", BindingFlags.Instance | BindingFlags.NonPublic)!;
HookInventory.Validate(method);
Console.WriteLine("internal void Xemele.Resolution.Core.XmlResolutionSession.BindCancellationToken(System.Threading.CancellationToken token)");
HookInventory.VerifyCompiledMutations();
string sourceBuild = typeof(XmlResourceResolver).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
Console.WriteLine("Runtime source build: " + sourceBuild);
using var a = new CancellationTokenSource(); using var b = new CancellationTokenSource();
var resolver = new Resolver();
using var session = new XmlResolutionSession(resolver, new OperationBudget(new(), default), a.Token);
using var first = session.Open(new(new Uri("urn:binding:parent"), null, XmlResourceKind.ExternalEntity), default).Resource!;
session.BindCancellationToken(b.Token); a.Cancel();
using var second = session.Open(new(new Uri("urn:binding:child"), first.SourceUri, XmlResourceKind.ExternalEntity), default).Resource!;
Require(resolver.LastToken == b.Token && session.ResourcesOpened == 2 && first.Read(new byte[1], default).Count == 1);
_ = XmlDocument.Parse("<root/>");
Console.WriteLine("Exact token B and paused parent retained; M1 strict parse preserved.");
if (args is ["--allocation"])
{
    using var independent = new XmlResolutionSession(null, new OperationBudget(new(), default), default);
    for (int i = 0; i < 10000; i++) independent.BindCancellationToken((i & 1) == 0 ? b.Token : default);
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 100000; i++) independent.BindCancellationToken((i & 1) == 0 ? b.Token : default);
    long borrowed = GC.GetAllocatedBytesForCurrentThread() - before;
    before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 1000; i++) Escape(new byte[32]);
    long escaped = GC.GetAllocatedBytesForCurrentThread() - before;
    // Identical thread-local counter controls in the same process, after escaped allocations.
    before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 100000; i++) independent.BindCancellationToken((i & 1) == 0 ? b.Token : default);
    long guard = GC.GetAllocatedBytesForCurrentThread() - before;
    Require(borrowed == 0 && escaped > 0 && guard == 0);
    Console.WriteLine(JsonSerializer.Serialize(new { sourceBuild, processId = Environment.ProcessId, runtime = Environment.Version.ToString(), iterations = 100000, borrowed, escapedIterations = 1000, escaped, guard, scope = "warm sequential binding; setup and error paths excluded" }));
}
else Require(args.Length == 0);
return;
static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Binding sample invariant failed."); }
[MethodImpl(MethodImplOptions.NoInlining)] static void Escape(byte[] value) => EscapeState.Value = value;
static class EscapeState { internal static byte[]? Value; }
sealed class Resolver : XmlResourceResolver
{
    internal CancellationToken LastToken;
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    { LastToken = cancellationToken; return new(new MemoryStream(new byte[] { 1 }), request.Uri); }
}
