# Migrating to the release candidate

The initial preview's222public declarations are retained unchanged in the
1,430member release inventory. No rename or source-breaking migration is
required. See the executable [examples](Examples/Program.cs) and the exact
[API review](../api-review/README.md). This is a local candidate, not a published
stable package; platform/performance/sustained release checks remain outstanding.

| Familiar operation | Managed entry point and behavior |
| --- | --- |
| Parse an owned XML document | XmlDocument.Parse(string/span/UTF8), Load(Stream), LoadAsync(Stream). Returned tree owns retained data; caller stream stays open. |
| Native node pointer navigation | XmlNode is an owner/slot value handle. Test IsDefault for absent relatives. Keep its owner alive; Remove detaches without destroying the handle. Use ImportNode for another document. |
| Parse errors / global error hooks | XmlParseException.Diagnostics or TryParse's false/null/diagnostics; ParseWithDiagnostics carries explicit completion and well-formedness. No process-global initialization/error callback. |
| Recovery flags | Strict factories reject Recover=true. Use ParseWithDiagnostics and inspect IsComplete/IsWellFormed before consuming repaired Value. A recovered tree is not standards validity. |
| SAX callbacks | XmlSaxParser with XmlSaxCallbacks or IXmlSaxHandler. Borrowed spans are valid during the callback; copy text if it must survive. Callbacks are sequential; caller exceptions propagate. |
| Pull/push and forward output | Xemele.Streaming.XmlReader, XmlPushParser and Xemele.XmlWriter provide explicit lifetime/state and cancellation contracts; use module samples for these modes. Stream-taking APIs do not imply a DOM. |
| XPath compiled object | XPathExpression.Compile then Evaluate with explicit namespaces/variables. Keep attribute/namespace identities and typed XPathValue conversions. No implicit XPath2 support. |
| HTML parser | Choose HtmlParser for pinned legacy compatibility or Html5Parser for WHATWG behavior. HtmlSaveOptions selects output mode; CssSelector is independently compiled. |
| Schema compile/validate | RelaxNgSchema, XmlSchema or SchematronSchema; Compile can throw SchemaCompileException. IsComplete=false is refusal/incomplete work, never a validity conclusion. Schematron exposes findings and SVRL. |
| External loader | Explicit XmlResourceResolver and finite resource limits. DTD default is Prohibit. No ambient file/network grant; null resolver outcomes remain denial, not empty schemas. |

Optional options are nullable and receive documented safe defaults; required
inputs reject null. TryParse handles XML rejection, not arbitrary invalid arguments,
I/O, cancellation or callback exceptions. Missing XML attributes return null;
missing relatives use default XmlNode. Schema findings' optional authored IDs are
nullable; event keys preserve distinct anonymous pattern/rule identity.

XML scope expanded since the first preview: DTDs, complete PI names and additional
encodings now have their own explicit options/profiles. Old unsupported-feature
rejections do not promise permanent rejection after those additive modules ship.
The original accepted XML/tree/serialization signatures remain identical.

Limits, resolver policy, namespace context, stream ownership and cancellation
are part of behavior, not just metadata. Keep safe defaults, never treat a budget
refusal as invalidity, and avoid concurrent mutation while enumerating/evaluating.
Snapshot or compile reusable queries/schemas as documented by their module.
