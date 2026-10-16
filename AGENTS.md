# Xemele

Managed C# XML/HTML toolkit, `net10.0` only.

- Read the relevant [specifications](docs/specs/README.md), source, and tests before changing behavior.
- Before runtime hot-path work, read [string/byte performance rules](docs/performance-coding.md): borrowed spans, static literals, benchmark-backed SearchValues/token matching, and explicit allocation evidence.
- Preserve public API compatibility and keep [API baselines](tools/ApiBaseline/README.md) consistent with intentional API changes. Never regenerate a baseline merely to hide a mismatch.
- Run the relevant checks from [CI](.github/workflows/build.yml). Never describe unrun checks as passing.
- Keep corpus, encoding, canonicalization, and frozen benchmark fixture bytes unchanged unless the corresponding expectations and provenance are intentionally updated.
- Keep changes focused and summarize unresolved issues plainly.
