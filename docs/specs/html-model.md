# HTML model boundary

The provisional Document/Node return shapes cannot preserve WHATWG data through
XmlDocument factories. Actual Tree/Validation.cs rejects XML-invalid names,
control characters, comments containing '--' and namespace attribute forms.
HTML uses a separate HtmlDocument/HtmlNode representation.
HTML storage/navigation/copy/mutation remain under HtmlCommon, with no weakening
of XML validation, no conversion API and no breaking shared-tree repair.

html.md resolves fragment ancestor/form/template context, three quirks modes,
lexical owned-token callbacks, byte decoding, serializers and exhaustive CSS
support/rejection boundaries. Scripting flag affects parsing only. The full
WHATWG algorithm is required; browser-execution dependencies are the only declared
HTML5 exclusions. Upstream corpus pinning and engine behavior remain implementation
requirements, not claims established by declaration fixtures.
