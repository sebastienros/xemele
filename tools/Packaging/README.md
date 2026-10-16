# Xemele packages and CLI releases

## CLI distributions

`Xemele.Tool` installs the `xemele` command using the .NET 10 SDK:

```sh
dotnet tool install --global Xemele.Tool
xemele --help
```

Continuous previews from `main` use `--prerelease` and the Feedz source
`https://f.feedz.io/sebastienros/xemele/nuget/index.json`. Version tags, including
prerelease tags, publish to NuGet.org. Installation is available after publication.

For tagged releases, the .NET 10 tool router selects one of four implementation
packages:

| Target | Implementation | Standalone archive |
| --- | --- | --- |
| `linux-x64` | Native AOT | `xemele-VERSION-linux-x64.tar.gz` |
| `osx-arm64` (macOS ARM64) | Native AOT | `xemele-VERSION-osx-arm64.tar.gz` |
| `win-x64` | Native AOT | `xemele-VERSION-win-x64.zip` |
| Other .NET 10-supported targets | Framework-dependent, portable `any` | `xemele-VERSION-any.zip` |

Native archives contain `xemele` (`xemele.exe` on Windows), require no .NET
installation, and retain the operating system dependencies of Native AOT.
The Linux executable is built on Ubuntu 24.04 and requires a compatible glibc-based
system; it is not an Alpine/musl executable. The portable archive runs with
`dotnet Xemele.Tool.dll` and requires the .NET 10 runtime. All archives include
the license, notices, and this guide. Download them from the matching GitHub release.
The NuGet tool always requires the SDK for installation, even on native targets.
Continuous Feedz previews ship a classic portable tool requiring .NET 10 on all
platforms; the native tool packages and standalone archives are produced for tags.

## Build and validate locally

Use the pinned SDK and Python 3.13 or newer. Native builds require the platform's
Native AOT toolchain (Clang and development libraries on Linux, Xcode command-line
tools on macOS, or the Visual Studio C++ build tools on Windows). Build each native
RID on its matching OS and architecture; do not substitute cross-compilation for
actual execution.

```sh
python -B tools/Packaging/tool.py build --rid osx-arm64 --version 0.9.0-preview.1 --output artifacts/tool-release
python -B tools/Packaging/tool.py build --rid any --version 0.9.0-preview.1 --output artifacts/tool-release
```

Use a fresh output root for each attempt; existing RID directories are rejected.
Each build packs the implementation, executes representative XML, XPath, HTML,
CSS, schema/JSON diagnostic, SVRL and XInclude commands, checks failure exits, and
creates an archive. The `any` build also creates the tool router and library package.
Release metadata comes from `Release.targets`; normal source builds do not enable
AOT, and ordinary `dotnet pack` still produces a classic portable tool.

Collect the four builds' `packages/` and `archives/` directories into one directory,
then verify installation from a fresh package store and execute the extracted archive:

```sh
python -B tools/Packaging/tool.py verify --rid osx-arm64 --version 0.9.0-preview.1 --feed artifacts/tool-release/packages --archives artifacts/tool-release/archives
```

The verifier requires exactly six NuGet packages: the library, router, and four
tool implementations. Xemele packages are resolved only from this local feed;
NuGet.org supplies SDK apphost dependencies when needed. CI verifies all three
native hosts and the automatic portable fallback on Linux ARM64.

## Continuous preview publication

The **Build** workflow follows Parlot's main/release-branch and pull-request
structure, with Xemele's Linux, Windows, and macOS checks retained. It provisions
pinned corpora and independent engines, compares the compiled public API, and
executes the MTP runner, corpus, and offline CLI checks.

On pushes to `main`, the Linux job packs the runtime library and portable CLI
after its checks, then publishes them to Feedz using `FEEDZ_IO_API_KEY`.
Pull requests and maintenance branches do not publish. Versions use
`VersionPrefix` from `Directory.Build.props` and `VersionSuffix=preview-RUN_NUMBER`,
for example `0.9.0-preview-123`. The default local compatibility version remains
`0.9.0-rc.1`. Like Parlot, SDK installation and checkout actions are pinned, with
isolated NuGet paths and checkout credentials not persisted.

## Tag-triggered publication

Push a semantic-version tag prefixed with `v` to start the **Publish**
workflow. The package version comes directly from the tag without its
`v` prefix; there is no manual version or publication input.

```sh
# Tag a commit contained in main.
git tag v0.9.0
git push origin v0.9.0
```

The workflow reuses the complete **Build** validation matrix, builds and executes
all three native targets and the portable build,
verifies router metadata, installs the tool on all three native hosts and Linux
ARM64, and executes extracted archives. Publication follows only after every
verification job succeeds and the `release` environment permits deployment.
Use the local build/verification commands above to validate without publishing;
pushing a valid release tag requests publication.

- All tags, such as `v1.2.3` or `v1.2.3-preview.1`, must point to a commit contained
  in `origin/main` and publish to NuGet.org. Lightweight and annotated tags are
  both accepted, matching Parlot's release policy.
- Publication uses the `release` GitHub environment, which can enforce approval rules.
- Tagged publication exchanges the job's GitHub OIDC token through `NuGet/login@v1`
  for a short-lived NuGet API key for the `sebastienros` account. No long-lived
  `NUGET_API_KEY` secret is required.
- Implementation packages are uploaded before the router so a new tool version
  never deliberately points at implementations that have not been uploaded.
- NuGet packages and CLI archives receive GitHub build-provenance attestations.
- All four archives are attached to a GitHub release tagged `vVERSION`; previews
  are marked as prereleases.

Before enabling tagged publication, configure a NuGet trusted-publishing policy
for the `sebastienros/xemele` repository, workflow filename `publish.yml`, and
environment `release`. Authorize the library, tool router, and all four tool
implementation package IDs. Continuous preview publication requires `FEEDZ_IO_API_KEY`.
No upload occurs in local build/verification commands. Feed and
GitHub publication are separate operations, not an atomic transaction; inspect any
partial failure before retrying. Package uploads skip already-published versions,
but creating an existing GitHub release fails explicitly instead of replacing assets.
These packaging checks do not replace the repository's full correctness,
compatibility, performance, and robustness release requirements.

## Runtime package profile

Managed net10.0 XML/HTML toolkit: owned XML trees, strict XML1.0/namespaces and
explicit recovery, SAX callbacks, bounded pull/push/async transports and forward
writer, XPath1.0, DTD validation, canonicalization, catalogs/XInclude, BCL adapters,
legacy and WHATWG HTML, CSS selectors, RELAX NG XML syntax, XSD1.0 Second Edition,
and ISO2016Schematron with direct XPath1 and SVRL. The `Xemele.Tool` package
provides the `xemele` command. Safe defaults prohibit ambient external access.

Use XmlDocument.Parse/Load/LoadAsync for an owned tree, XmlSaxParser for callbacks,
XPathExpression.Compile/Evaluate for reusable queries, and the named schema
Compile/Validate APIs. Caller streams stay open. Borrowed callback views last
only during that callback. TryParse's false/null/diagnostics describe syntax
rejection; invalid arguments, cancellation, I/O and caller exceptions propagate.
Validation IsComplete=false is incomplete work, not an invalidity verdict.

See the repository's docs/migration/README.md, runnable examples and module
specifications at the package's exact repository commit for detailed contracts.
The first preview's222public signatures remain unchanged. Nullable/default-handle,
recovery, resolver, finite-budget and ownership behavior are documented there.

Excluded: XML1.1, XPath2+, XSD1.1, RNG compact syntax/custom datatype registration,
XSLT engines, browser rendering/scripts/DOM, CLR-object serialization, native C ABI
and broader XPointer profiles. Schema profiles and exact conformance exceptions
are documented in the release inputs/reports; no blanket native-library parity.
The155approved original XSD nonpasses remain144false/11null and receive zero pass
credit. Separate application-profile evidence is not original standards credit.

The compatibility verifier uses local `0.9.0-rc.1` packages without publishing them.
A successful local smoke test does not establish unexecuted platform,
dedicated timing or sustained-fuzz gates.
Source Link records the exact local repository commit; remote source retrieval
is unavailable until that commit is deliberately pushed. Public XML documentation
is supplemented by this guide; full per-member XML-comment coverage is not claimed.
