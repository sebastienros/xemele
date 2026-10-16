# Complete strict profile composition

Run `dotnet run --project samples/xml/conformance/Sample.csproj -c Release` with the repository SDK. The sample uses existing public APIs only; it adds no runtime API or new baseline fragment.

The first document joins expanded namespace names, a tokenized external DTD default, an external parsed entity, compatible lexical document/text declaration versions and exactly-once resource disposal. Its `EMPTY` declaration deliberately disagrees with content: this processor is nonvalidating. The resolver explicitly grants two in-memory URNs and refuses everything else; enabling DTD parsing grants no ambient access.

The second document joins an incomplete tokenized default with an unresolved content occurrence under the nonvalidating EntityDeclared policy. Owned sidecars retain symbolic parts, shared declaration/application identity and a child-boundary anchor. Attribute/text reads and XML output refuse loss.

The executable asserts an authored model before printing a compact JSON result. It demonstrates composition, not another full corpus run. Corpus manifests and independent expectations are under `tests/corpora/xml/`.
