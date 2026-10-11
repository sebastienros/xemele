using Xemele.StreamingAcceptance;
namespace Xemele.Fuzz.Streaming;
// Linked by the report-owned runner; the bootstrap executable remains untouched.
public static class StreamingFuzz
{
    public static Task Exercise(byte[] bytes, int seed = 1771, CancellationToken cancellationToken = default) =>
        Equivalence.Verify(bytes, new() { DtdProcessing = XmlDtdProcessing.Parse, Limits = new() { DocumentCharacters = 1_048_576, Depth = 64, TokenCharacters = 65536 } }, seed, cancellationToken);
}
