# Reference process integration

This separate infrastructure executable requires privately provisioned native
references; ordinary solution tests retain their existing BCL-only behavior.
It calls the production `ReferenceProcess.ExecuteAsync` client for every request,
including real engine responses and adversarial child process failures. It never
substitutes a system parser or alters the protocol validator.

With the pinned SDK/toolchains from the native runner READMEs provisioned:

```sh
python3 -B tools/ReferenceRunners/Libxml2/build.py --fetch # first provisioning only
python3 -B tools/ReferenceRunners/Xmloxide/build.py --offline
dotnet build tools/ReferenceRunners/Integration/ReferenceIntegration.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet run --project tools/ReferenceRunners/Integration/ReferenceIntegration.csproj -c Release --no-build -- "$PWD" "$(command -v dotnet)" "$PWD/artifacts/reference-integration" "$(git rev-parse HEAD)"
```

Run the executable with explicit repository, dotnet, and private native paths
as shown above. It records validated wire responses and process checks in the
supplied output directory.
Missing native binaries/provenance fail, rather than silently skipping checks.
Native builds remain explicit so `--fetch` is never an implicit network action.

The common known fixture has an independent expected event sequence. Shared
capabilities include absolute mapped external general entities, denial, invalid
XML and unsupported requests. BCL/libxml2 additionally cover external DTDs;
xmloxide's explicit capability failure is asserted. Document-level whitespace,
libxml2 namespace IRI rejection and xmloxide literal CRLF attribute/CDATA values
remain visible differences, with BCL and independent expectations retained.
These reference limitations do not narrow Xemele product requirements. Subprocess
checks do not measure parser performance or establish general XML conformance.
