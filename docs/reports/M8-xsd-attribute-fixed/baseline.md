# Original accepted addB183 baseline

Witness revision: `729025dbbbd7553ee57e512800ae5d9e91d9ce7d` (runtime unchanged from claim).
Pinned .NET SDK 10.0.400 / runtime 10.0.11. Offline restore and Release test build passed, zero warnings/errors.

Actual command (exit 1):
```
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.M8XsdAttributeFixed -parallelMode none -noLogo -noColor -result-xml artifacts/M8-xsd-attribute-fixed/baseline/witness-diagnostic.xml
```

2 discovered/executed rows: independent System.Xml.Schema.XmlSchemaSet accepts unchanged original; public facade fails with `XSD.SchemaRestriction: Derivation changes an inherited fixed attribute use.` No skips/errors. Prior initial run also failed in ContentModels/Derivation.cs:67 and is preserved at ignored `artifacts/M8-xsd-attribute-fixed/baseline/witness.xml`.

Original SHA256 `ba5b1343bc8f015b7f5bcf951ffb64610a21fcd4741ffa3892408bb0d2a28bb6`, 1765 bytes. Tests check this hash on both paths. Provenance/NOTICE in owned fixture directory. Original cache unchanged.

[W3C Structures §3.2.2](https://www.w3.org/TR/xmlschema-1/#declare-attribute) maps constraints to actual datatype values; [§3.5](https://www.w3.org/TR/xmlschema-1/#Attribute_Uses) preserves declaration versus use constraints. These support typed equality with each authored namespace map. Legacy structural-only comparison remains unchanged.

Source review: runtime `78c44c3728170331c824e1de999ceb46a6dbc5f2` approved by coordinator's independent model/contract review. Derivation.cs SHA256 `d7445ff8efb48832c47866d4235e20109090febbedaa31b6b3849d7066bb2926`.

Interpretation caveat from that review: the Second Edition restriction prose's same-string wording conflicts with the actual-value component mapping. [Working Group R-217 disposition](https://lists.w3.org/Archives/Public/www-xml-schema-comments/2007JanMar/0098.html) favors value-based comparison; it was not formally applied as a 1.0 erratum. This repair follows the coherent actual-value interpretation and the unchanged accepted original corpus, with BCL as corroboration only. The archive fetch failed in this worker; the disposition attribution is coordinator-provided independent contract-review evidence.

The two 30-digit decimal witnesses are outside System.Decimal.MaxValue. They are retained as explicit BigInteger coefficient/scale mathematical equality and inequality assertions, with no BCL oracle claim or range-rejection false positive. Representable decimal/list/token/QName/union restriction cases continue to use independent BCL corroboration.
