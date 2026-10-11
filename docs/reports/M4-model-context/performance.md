# Context performance evidence

Prepared a9cac03 ran once on SDK10.0.400, actual .NET10.0.11, macOS15.8.1 Arm64 Apple M4 Pro. Two classes declare [MemoryDiagnoser, ShortRunJob]; no CLI job flags. Eight successful cases completed in65.68seconds. Initial/final147 source/generated,43 binary and3852 SDK/runtime/host hashes matched manifest SHA25655075b34367c5472e2ac48fe2400dfa4bfaf149de9c1aa16188a55c83fcda61d. Allocation/timing/wrapper/identity exits were all0; no children remained. Lease released before actual raw inspection.

Counters: all32 warm and32 measured batches0B; 8192 groups per phase with three comparisons and one owned-map lookup each; consumed147456. Combined first static/dynamic context creation (including core metadata)6944B. Positive byte[1024] control1048B.128 owned static copies251904B=1968B/op;128 dynamic copies93184B=728B/op. Fixture setup lies outside counters; no retained-heap claim.

|Case|Framework mean ns|Bounded charged mean ns|BDN inclusive managed totals, framework / bounded|
|---|---:|---:|---|
|4096-char multilingual hit|165.153|1467.999|0/4194304 ops;1968/524288 ops|
|4096-char last-character miss|156.941|1529.577|64/4194304 ops;1968/524288 ops|
|7-char hit|1.485|3.906|1968/268435456 ops;1968/134217728 ops|

BDN displays allocation dashes for all comparisons, but the tiny positive inclusive totals above remain evidence and are not silently rounded into an exact-zero claim. Independent warmed counters establish the borrowed regression result; no attribution is claimed for BDN totals. Owned static/dynamic means948.584ns/144.921ns; BDN1968B/728B per operation agrees with explicit owned counters. Dynamic and static copies own sealed entry objects and comparison delegates; cold setup remains separate.

The charged/polled comparator is slower than the framework ordinal baseline. These cases compare equal ordinal results, while the bounded candidate additionally guarantees cancellation and work accounting. Timing is informational, not an optimization/regression threshold or x64 claim. Whole-query confirmation belongs to M4-evaluate; broader hardware release evidence belongs to M9. Only this summary/table is committed; raw results are removed after actual inspection.
