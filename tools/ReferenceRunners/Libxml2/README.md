# Pinned libxml2 XML reference

This task-private net10.0 executable consumes the integrated v1 JSONL contract.
Only reference infrastructure loads native code. Xemele runtime remains managed.

Provision CMake 3.31.6 in an ignored task-local Python virtual environment (or set
`CMAKE` to that version). The build checks Apple Clang 17.0.0 build
clang-1700.6.3.2, GNU Make 3.81, macOS SDK 26.2 and arm64; deployment target is
15.0. Other toolchains/platforms are unverified and fail explicitly. The source
is immutable GNOME libxml2 2.15.4, commit
`96498992efa48d52b0e8b83058bd88dbdaf153c1`, fetched from the upstream Git repository.

```sh
python3 -m venv artifacts/M0-references-libxml2/cmake-env
artifacts/M0-references-libxml2/cmake-env/bin/pip install cmake==3.31.6
python3 -B tools/ReferenceRunners/Libxml2/build.py --fetch
# With repository-pinned dotnet 10.0.400 on PATH and DOTNET_ROOT:
dotnet build tools/ReferenceRunners/Libxml2/Libxml2Reference.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
XEMELE_LIBXML2_BRIDGE="$PWD/artifacts/M0-references-libxml2/native-build/libxemele_libxml2.dylib" dotnet tools/ReferenceRunners/Libxml2/bin/Release/net10.0/Libxml2Reference.dll
python3 -B docs/reports/M0-references-libxml2/verify.py
```

Subsequent offline builds omit `--fetch`. All upstream/generated code is under
ignored `artifacts/M0-references-libxml2/`; no vendored parser is tracked. Build
provenance records source archive, license, static library and bridge SHA256,
compiler/CMake/make/SDK, explicit configuration and preprocessed enabled features.
The bridge statically links this exact engine, and the process verifies version,
commit and bridge checksum before serving requests. Engine metadata reports this
manifest plus actual .NET framework/runner informational version. It never probes
or substitutes a system-installed libxml2. Original upstream and component notices
are preserved under the task report directory. Publication is not performed.

The native bridge owns one reader and error/resource loader per request. Resource
URLs are canonicalized with System.Uri and resolved only from that request's map;
no fallback loader is called. Native input copies and managed pinned/unmanaged
scratch remain alive until reader destruction. Callback exceptions are contained.
The reader emits real expanded names, normalized values, empty start/end pairs,
DTD defaults and coalesced text; attributes sort by .NET UTF-16 ordinal comparison.
Namespace errors captured by libxml2 are rejections even if its reader returns a
positive result. Partial events are discarded for rejection/failure.

Wire limits/UTF8 rules match v1. Safety limits are explicitly engine specific:
4 MiB input bytes, 4 MiB mapped bytes and 4 MiB cumulative resource loads per
request; 1,048,576 emitted text/attribute UTF-16 characters and 131,072 events.
Native default entity amplification/depth/name/text safeguards remain enabled
(no XML_PARSE_HUGE). XML_ERR_RESOURCE_LIMIT and adapter limits return
runner-failure/limit. Cyclic entities are parse rejection. Nonbuilt-in encodings
fail explicitly as unsupported protocol requests (iconv/ICU disabled); absent
hint uses the native BOM/declaration decoder, utf-8 hint also validates strictly.
Input XML 1.1 is rejected under the XML 1.0 profile. No benchmark/allocation or
cross-platform support is claimed by these subprocess tests.

Pending contract issue before completion: native xmlParseMisc consumes document
whitespace outside the root without exposing reader or SAX text events. Current
wrapper cannot faithfully normalize these nodes to BCL v1. This is reported to
the coordinator; no fabricated whitespace reconstruction has been added. Do not
use this pending implementation as a fully conforming differential reference.
