using SharpFuzz;
using Xemele;

// This initial qualification target is the XML byte decoder/parser with internal
// DTD processing. Engine coverage and sustained duration require a real engine run.
Fuzzer.LibFuzzer.Run(Exercise);

static void Exercise(ReadOnlySpan<byte> bytes)
{
    if (bytes.Length > 65536) return;
    var options = new XmlParseOptions
    {
        DtdProcessing = XmlDtdProcessing.Parse,
        Limits = new()
        {
            DocumentCharacters = 65536, Depth = 64, AttributesPerElement = 64,
            TokenCharacters = 65536, InternedNames = 4096, EntityDepth = 8,
            ExpandedCharacters = 131072, ExternalBytes = 1, ExternalResources = 1,
            InclusionDepth = 1, Diagnostics = 16, EvaluationWork = 100000
        }
    };
    bool accepted = XmlDocument.TryParse(bytes, out var document, out var diagnostics, options);
    if (accepted && document is null) throw new InvalidOperationException("Accepted XML lacks its document.");
    if (!accepted && diagnostics.Count == 0) throw new InvalidOperationException("Rejected XML lacks its diagnostic.");
}
