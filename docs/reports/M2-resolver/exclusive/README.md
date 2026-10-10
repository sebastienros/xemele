# Resolver allocation and timing observations

One explicitly granted exclusive run at e7cc0b5fd6480c4395e6dd861c5e28ed97815f2d,
with MemoryDiagnoser/ShortRunJob class attributes and no job override. Generated
build succeeded, eight children exited0, 24 actual measurements were recorded.
CPU/runtime/GC and every measurement are in raw.log.gz and the unchanged CSV.

Warm borrowed direct/bounded reads, including the synchronously completing
ValueTask path, measured0 B/op. New session/resolver/resource/open/read/dispose
measured1416 B/op for both chunk widths. This is owned I/O setup, not a zero-
allocation operation claim. The sample's MemoryStream-backed owner differs:
first observed session/open/dispose2624 B, warm setup1464 B; 1000 retained session
GC heap delta2628984 B and after-disposal residual2256 B are noisy total-process
observations, not an exact per-owner heap census. Sessions own no read buffer;
keys/handles/budget snapshots and caller-resolver stream storage persist through
source lifetimes. Private resolver allocation is outside session control.

Bounded sync reads measured4.5115/44.8278 ns for1/4096 bytes; the direct zero-fill
baseline measured0.3877/33.0506 ns. Async completion measured21.3620/60.0368 ns;
full setup measured125.7113/166.2217 ns. Record errors alongside means; three
samples and ordinary priority make these provisional. Eight high-priority
permission failures were reported. The one-byte baseline is near overhead with
an interval crossing zero, so its ratio is not a robust throughput claim.

No optimization replaces a correctness path: this comparison quantifies ledger/
ownership overhead around identical zero-filled reads. No token scanner/matcher
or SearchValues selection occurs here. Real parser/entity composition belongs
to M2-entities; that owner must validate end-to-end external-input performance.
Suspending async I/O, x64, Windows/Linux and AOT measurements remain release/gate
follow-ups. All43 focused/844 total tests at this source preceded the run;
functional evidence includes genuine async overrides and yielding async cleanup.
