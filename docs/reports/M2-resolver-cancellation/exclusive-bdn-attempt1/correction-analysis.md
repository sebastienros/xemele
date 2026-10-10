# Failed generator invocation and proposed correction

The first exclusive BDN invocation ran from the repository root at source
`d44c269a2391b680df6439eba36273dc6b5d6759`. The parent returned zero but
project generation failed before any generated build, child, or measured case.
Both the standalone benchmark and the sample have the project filename
`Xemele.ResolverSample.csproj`; the original raw failure and empty result set are
retained. The allocation control succeeded separately and must not be repeated.

A read-only exact-project inventory found one matching file below the standalone
project directory. However, changing only the invocation working directory would
still fail: pinned BenchmarkDotNet 0.15.8 searches ancestors for a solution or
`global.json` before using the project root, then recursively scans that root for
the assembly-matching filename. This repository has both `global.json` and
`Xemele.slnx`. The official pinned implementations are
[DotNetCliGenerator.cs](https://raw.githubusercontent.com/dotnet/BenchmarkDotNet/v0.15.8/src/BenchmarkDotNet/Toolchains/DotNetCli/DotNetCliGenerator.cs)
and [CsProjGenerator.cs](https://raw.githubusercontent.com/dotnet/BenchmarkDotNet/v0.15.8/src/BenchmarkDotNet/Toolchains/CsProj/CsProjGenerator.cs).

The proposed scoped correction is rename only the owned sample project to
`CancellationBindingSample.csproj`, retain its existing friend assembly name,
and keep the standalone benchmark project filename matching that assembly name.
No runtime, authored benchmark method/job, threshold, or GC/JIT setting changes.
Root was informed of the ancestor-search evidence before modifying anything.
A new source checkpoint and scoped source/package/oracle/discovery checks precede
any renewed BDN-only grant. No measurements run under the released first grant.
