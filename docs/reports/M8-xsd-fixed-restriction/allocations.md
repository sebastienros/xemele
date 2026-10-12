# Owned allocation evidence

Prepared source/harness: `a68d7330fef65d4c72353cd189f5673d375944ff`.
Accepted runtime/tests: `0c54888ba0a4c2c010996eaabfda63c7f6ce8a50`.
Manifest SHA256:
`eda25ac70e83beb757379d7ad05b17b40dbcbb84fcf6d77c4a960a78b8d01c98`.
Root grant `9707173cdaa6ea8720b3831d7b0aa2acefcc01f7` and durable root release
`d1ff39c3b9fb6d53400aae10e7398249ec66b1b6` authorized the one serial measurement
sequence. The worker launched no measurement process. Raw results were opened
only after the durable cleanup release. No retry, BDN or benchmark timing ran.

Actual runtime is .NET 10.0.11, SDK 10.0.400, Arm64, macOS 15.8.1, workstation
GC; tiered compilation and concurrent GC disabled. Each route consumes its
result, observes a separate first call, warms 16 calls, then records three
64-call aggregates using synchronous per-thread allocation counters. There are
no asynchronous paths in this probe. Fixtures are prepared outside warm
measurement, except the explicit end-to-end XML composition route.

| Owned route | First call B | Three aggregate B numerators, each /64 operations | Warm B/op range | Warm median B/op |
| --- | ---: | --- | ---: | ---: |
| Existing structural compilation | 29,152 | 1,843,200 / 1,843,200 / 1,843,200 | 28,800 | 28,800 |
| Complete compilation with fresh facet plans | 33,000 | 1,986,112 / 1,985,024 / 1,985,024 | 31,016–31,033 | 31,016 |
| Complete compilation with prepared decimal facets | 30,624 | 1,858,560 / 1,859,120 / 1,858,560 | 29,040–29,048.75 | 29,040 |
| Complete QName union/list compilation with prepared facets | 33,904 | 1,953,792 / 1,953,792 / 1,954,352 | 30,528–30,536.75 | 30,528 |
| Unequal fixed constraint and owned diagnostic | 32,344 | 1,923,584 / 1,923,584 / 1,923,584 | 30,056 | 30,056 |
| Real XML compiler/datatype/facet/content composition | 172,192 | 11,017,280 / 11,017,280 / 11,018,368 | 172,145–172,162 | 172,145 |

The setup/fixture observation is 474,152 B, kept separate from the first and warm
calls. The 1,024-byte positive control allocates 1,048 B. The prepared decimal
join's warm median is 240 B above the structural lane, consistent with two owned
typed parse results. The structural lane provides a cost reference; it does not
perform the new typed checks and is not an equivalent-semantics optimization
baseline. The measured join is an owned schema compilation boundary, not a
borrowed allocation-free primitive. No throughput or zero-allocation claim is
made. No new scanning/token matching technique needs comparison.

Sixteen complete programs were kept alive for a retained-heap observation.
`GC.GetTotalMemory(true)` changed from 651,544 to 549,648 B (−101,896 B). This
whole-heap observation is inconclusive for per-program retained cost: collection
can reclaim earlier transient objects and weak facet-plan cache entries. The
negative delta is not a memory saving or retained-size bound. Source review
confirms that the new typed values and caller operation remain local to the
relation evaluation; returned content programs do not retain them.

The fixed supervisor's control/allocation children 53265 and 53267 both exited
0; both were reaped, process groups absent, and logs closed. All 442 prepared
identities matched before and after, with no mismatches. Runtime assembly SHA256
`31b406c9da194cade44e999e45c2ca34c285bbb6b0495b6a0fcbc51a8d889706`,
probe SHA256 `8a279a0cc0f49bf970044a53c59056c11658af7d717ecacad3e543000a211280`,
input SHA256 `bb7f94fb9792ac4306015e51d0fa7a86e09f6b4f699e6c1f0a98f3fc1b3c3980`.
The accepted raw allocation output SHA256 is
`415ecf2921c0513865dbeef68eb62a4493bc0c6b51d32e5cb7e42f0b9f0a03bc`.
Raw numeric results and logs stay in ignored task artifacts and are not committed.
This is one Arm64 allocation run; it makes no cross-architecture release claim.
