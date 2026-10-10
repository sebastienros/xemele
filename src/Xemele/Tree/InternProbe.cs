using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Xemele.TreeBenchmarks")]
namespace Xemele;
public sealed partial class XmlDocument
{
    // Borrowed probes never persist caller memory. Only Intern after PreflightNames
    // creates a durable string; empty names use the shared literal and no quota.
    internal string? FindInternedName(ReadOnlySpan<char> value) =>
        value.IsEmpty ? "" : names.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(value, out string? found) ? found : null;
}
