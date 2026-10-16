using System.Runtime.CompilerServices;

namespace Xemele.Tests.Tree;

internal static class TreeAllocationProbe
{
    // Both reads and all traversal, namespace, attribute and borrowed intern work
    // share this synchronous helper. Setup, result checks and output are outside.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static (long Bytes, long Consumed) Walk(XmlDocument d, XmlNode e, bool positive, ref object? escaped)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (var i = 0; i < 1000; i++)
        {
            foreach (var n in e.Children)
            {
                sum += n.Value.Length;
            }

            foreach (var x in e.Attributes)
            {
                sum += x.Value.Length;
            }

            foreach (var ns in e.NamespaceDeclarations)
            {
                sum += ns.Value.Length;
            }

            sum += d.Intern("urn:e".AsSpan()).Length;
            if (positive)
            {
                escaped = new byte[37];
            }
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return (bytes, sum);
    }
}
