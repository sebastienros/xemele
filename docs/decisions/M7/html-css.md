# M7 resolved contract decisions

The exact public declaration families are Model, Engine, Tokenizer and Selector
under tests/Contracts/Fixtures/M7. Engine/Tokenizer/Selector compile separately
against Model plus the completed Xemele assembly; no peer implementation or future
milestone source is referenced. Declaration methods intentionally throw and are
never executed, linked into Xemele or packaged. Compilation is signature evidence,
not runtime HTML evidence.

The provisional Document/Node return shapes cannot preserve WHATWG data through
XmlDocument factories. Actual Tree/Validation.cs rejects XML-invalid names,
control characters, comments containing '--' and namespace attribute forms.
M7 therefore freezes a separate HtmlDocument/HtmlNode owned by M7-html-model.
HTML storage/navigation/copy/mutation remain under HtmlCommon, with no weakening
of XML validation, no conversion API and no breaking shared-tree repair.

html.md resolves fragment ancestor/form/template context, three quirks modes,
lexical owned-token callbacks, byte decoding, serializers and exhaustive CSS
support/rejection boundaries. Scripting flag affects parsing only. The full
WHATWG algorithm is required; browser-execution dependencies are the only declared
HTML5 exclusions. Upstream corpus pinning and engine behavior remain implementation
owners' work and cannot be claimed from declaration fixtures.

Earlier deliverables are preserved by an owned-path diff check and executable
probes against the real XmlDocument/XmlReader in each independent consumer. No
hot path is implemented here; allocation/throughput claims are inapplicable.
