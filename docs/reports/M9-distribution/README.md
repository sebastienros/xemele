# Local distribution result

Local macOSarm64/.NET10.0.11checks passed. Two clean builds produce byte-identical
unsigned runtime and dotnet-tool packages and symbol archives after versioned ZIP
metadata canonicalization, with every payload byte preserved. Guides, MIT license,
WHATWG data attribution and notices are included. Embedded portable-PDB Source Link
maps to the exact build commit387925626763f5339d31abd2bfd448474a8efbff.
Actual fresh package-only consumers execute XML/XPath/SAX ownership/HTML/CSS and
RNG/XSD/Schematron/SVRL in managed, trimmed and NativeAOT modes. Native output is
Mach-O64arm64. Isolated local dotnet-tool install and help also pass.

`local-checks.json` records all actual command outcomes and clean finite process
cleanup. The source-equivalent reporting continuation retains completed clean
builds and managed/trim workloads instead of replaying them after reporting-only
fixes. Earlier failed preparation/reporting attempts remain in attempt*.json.
No public/runtime source or shared build configuration changed.

Retained local artifacts: artifacts/M9-distribution/checkpoint-387925626 in the
primary checkout; two nupkg, two snupkg and manifest. No package is published.
Source Link URL metadata is verified; remote source retrieval is not possible
before an explicitly authorized push. Only this host's two clean rebuilds are
claimed; relocated or other-platform determinism is untested. The package includes
user documentation; full per-member XML-comment coverage is not claimed.

M9-distribution remains blocked: the human confirmed no Windows/Linux/x64 runners
or prior reports. The owned workflow is a concrete no-publication execution path
for those checks when runners become available. Unexecuted hosted checks, including
Windows-specific linker/tool behavior, have no passing credit. A1/A2 stay pending;
A3 has the actual local deliverable evidence. Do not verify the release gate from
these local-only results.
