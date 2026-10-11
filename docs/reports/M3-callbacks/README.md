# Callback infrastructure checks

The runtime implementation lives in `src/Xemele/Streaming/Callbacks/`.
It provides the frozen sixteen-family callback API and borrowed views, independently
of the later parser/pump transports. `AttributeRow` and `NamespaceRow` are internal
ordinary owners; their span-backed collection views construct items directly.
The later pump owns row storage and invalidates delivery views when a callback
returns. View constructors are internal, and no view can be stored on a class or
used across an await. Persisting text/part containers is an explicit consumer cost.

`tests/Xemele.Tests/M3-callbacks/` checks every delegate against subclass dispatch,
optional handlers, exception identity (including callback-thrown parse exceptions),
metadata, checked indices, default collections, and concrete enumerator states.
Its warm allocation check exercises all sixteen notifications and collection
iteration through subclass, delegate, and empty adapter paths. Initialization,
row/part owner allocation and consumer persistence are excluded; the positive
control proves allocation detection. No scanning/token matching optimization is
introduced here.

`benchmarks/Xemele.Benchmarks/M3-callbacks/` compares sixteen-notification groups
against the direct subclass baseline using MemoryDiagnoser. These microbenchmarks
are provisional until composition with a real parser; M3-streaming-pump and its
public transport owners must confirm end-to-end bounded storage and allocation.
Cross-architecture confirmation remains a release requirement.

`Lifetime/Lifetime.csproj` deliberately fails with `-p:LifetimeCase=STORAGE`
(CS8345) or `-p:LifetimeCase=AWAIT` (CS4007). Without a case it builds normally.
`samples/M3-callbacks/` demonstrates callbacks and one-method subclass overrides
against the actual runtime assembly. `docs/api/M3-callbacks.txt` is extracted from
that compiled assembly with the approved ApiCheck emitter.

Check outcomes and exact tested revisions are recorded in the task JSON and
verification report, separately from the implementation commit.
