# Source-only helper / direct-call IL review

Inspected prebuilt diagnostic artifact at source revision
`824470cb8320b73c0cd134bc7a785fd2ff913531` using cached native
`dotnet-inspect` 0.26.0 (`member ... --library <local probe DLL> --all -m ... -v:d -S IL`).
The inspector reads local metadata; it does not invoke writer methods or run
allocation/timing measurements. Raw IL is adjacent in `primitive-il.txt`,
`complete-il.txt`, and `diagnose-il.txt`; the local getter appears in
`diagnose-decompiled.txt`.

* `Primitive`, IL_0000..IL_009B: static/constant strings, `AsMemory`, default token
  `initobj`, the five writer calls, and `Complete`. No `box`, `newarr`, or `newobj`.
* `Complete`, successful IL_0014..IL_0023: `ValueTask.GetAwaiter` and `GetResult`.
  Its only reference `newobj` is the invalid-completion exception on the guarded
  failure branch, which the measured completed-success invariant excludes.
* `Diagnose` direct switch, IL_0269..IL_030F: identical writer arguments and the
  same `Complete`, within the counter bracket starting IL_0230/IL_023E and ending
  IL_0317/IL_0326. No allocation opcode or reflection/getter invocation there.
* Reflection and expression/getter construction, IL_0061..IL_00E8, and arrays
  beginning IL_00F4 precede the batch loops. Compiled getters load fields/Count
  and return `int`; their invocations occur IL_01C8..IL_01E0 before call/control
  brackets and IL_0361..IL_0379 afterwards. `WriterState` constructor `newobj`
  produces a value type stored directly into the preallocated record array,
  with no boxing. Batch totals intentionally include this diagnostic overhead.
* Empty controls, IL_01E9..IL_01FF, execute the same four counter reads in the
  same order, without a writer, getter, delegate, or reflection call inside.
  JSON/LINQ projection and formatting occur after all batch loops.

The review found no explicit managed factory in the helper's successful path
that differs from the direct switch. IL alone does not prove the historical
128 B came from tiering/OSR or another runtime initialization. That ambiguity
remains explicit; a coordinator-selected process-local tiering differential
control is queued separately, without altering default-runtime acceptance.
