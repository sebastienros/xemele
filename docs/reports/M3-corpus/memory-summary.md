# Frozen 1 GiB streaming observations

Tested source: `b5db61f2ed6579e82b13faa8a575dcb0b32c4da5`. Frozen runner SHA-256 `1b0e702c215da392f69f63319943893f49712f99098442ad9f48051dc29d840f`; product SHA-256 `fd38344e0d3cda0f1c4fc01e47abb863363517b1603879379c97546db2fd607d`. All leases verified these identities before launch. No source, options, workload or build changed between modes.

All five completed reports consumed exactly 1,073,741,824 bytes and assert 262,144 starts, 262,144 ends, 1,070,333,958 text characters, one completion and a start callback before input exhaustion/EOF. Each has 64 active parsing live-heap samples (every 16 MiB) plus completion. Depth two, fixed vocabulary r/e/a, maximum token 4,089 characters. Whole collected managed heap includes source/sink buffers and runtime objects, conservatively bounding parser-owned live bytes without subtraction. Each is below 8,388,608 bytes.

| Mode | Maximum whole managed live heap (bytes) | Cumulative allocation (bytes) | Informational seconds | Actual process result |
| --- | ---: | ---: | ---: | --- |
| SAX | 244,912 | 18,930,440 | 36.890 | Outer `/usr/bin/time -l` exited 1; runner exit not independently captured |
| Async SAX | 276,280 | 19,128,456 | 40.227 | Direct dotnet exit 0 |
| Pull | 247,440 | 31,514,560 | 90.879 | Direct dotnet exit 0, 360 s deadline |
| Async pull | 278,192 | 31,902,824 | 110.845 | Direct dotnet exit 0, 600 s deadline |
| Push | 383,776 | 18,934,696 | 37.838 | Direct dotnet exit 0, 90 s deadline |

RSS is unavailable on this Mac: the managed API returns nonpositive PeakWorkingSet64, emitted as null plus rssUnavailable=true. The SAX outer timing tool failed querying `sysctl kern.clockrate` with `Operation not permitted` after the complete semantic/live report was written. Its real/user/sys timing text is preserved, and outer exit remains 1. Coordinator explicitly adjudicated the completed SAX raw semantic/live observations as valid; this is not a claim of runner exit 0 or successful external RSS collection. No SAX rerun was performed.

Historical failures remain explicit. The initial wrapper selected 'sax' as a DLL filename and failed before launching any child. The next lease completed async SAX but sync pull exceeded an inadequate 75 s deadline and was terminated with direct dotnet exit -15; that partial run supplies no memory acceptance result. Coordinator then approved unchanged remaining modes with sized deadlines; sync pull, async pull and push completed once under those deadlines. Exit reports and the empty timeout stdout/stderr are retained under distinct names. All launched processes were reaped and dedicated process groups were confirmed absent before release.

No throughput conclusion is drawn from the shared Arm64 host. Cumulative allocations are not retained memory, and missing RSS is not a measured zero. At the measured revision the malformed UTF-8 DOM regression blocked overall completion; these memory observations did not resolve it.

The later verified DOM-only repair was merged and affected corpus/fuzz checks rerun successfully at `f3e60c5e6227ee8efda010283d4b16191a74810b`. `streaming-closure-proof.json` proves all measured streaming paths, workload and build settings unchanged. This qualifies reuse of the original observations; it does not claim a new measurement or a matching whole-assembly binary hash after the DOM repair.
