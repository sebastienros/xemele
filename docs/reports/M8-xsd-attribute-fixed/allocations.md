# Typed inherited fixed attribute allocations

Actual root-only prebuilt run at frozen revision `139b9cf98a66f4a6a42f859a7a1b5360482ed6eb`; manifest SHA256 `b164cb10b3ce774f1e9472eb7a89d97372ef4a438589bfd4c08597a898429238` (454 identities). Runtime/test acceptance `5dfc443663069e7b25fc1817363da50a07bd1d21`; no runtime changes since reviewed repair78c44c372. Task remains branch work, not integration verification.

Coordinator invoked the owned supervisor's exact `--granted` manifest/latest-start/absolute-expiry sequence for M8-xsd-attribute-fixed-139b9cf9-alloc-only. Latest-start `2026-10-12T07:18:39.776454+00:00`, absolute-expiry `2026-10-12T07:19:09.776454+00:00`;30s maximum/20work/10cleanup. Disabled control then measure; both prebuilt managed children exited0. PIDs90360/90362 reaped, process groups absent, logs closed.454before/after identities matched. Durable cleanup release `2026-10-12T07:17:46.729893+00:00` had rawResultsInspected=false; root committed/relayed RELEASE before this worker inspected results. Supervisor elapsed1.651952625s is lifecycle evidence only.

Pinned SDK10.0.400/.NET10.0.11, Arm64/macOS15.8.1, workstation GC, tiered compilation/server GC/concurrent GC disabled. Runtime DLL SHA256 `FF8D190B3E7EBC8EF91AF1A3E22788DA89EB1354635191E4F589C64454D1C436`; probe SHA256 `1966D96993F8EC793283FD2B357C1AB19B76332805D7D6DB90E3765174C5F9EE`. Same in control and measurement.

Each route has16warmups followed by64 synchronous operations. Consume every accepted program/schema or rejected-schema diagnostic,1456total consumed including16retained results. Internal routes compile the prepared graph through complete typed composition; public routes compile prepared XML DOMs through the facade. Per-operation setup, typed values, dictionaries/content programs, diagnostics and facade publication are included. Fixture generation and output JSON are outside per-route counters. Control executed identical routes with EVERY allocation/GC/timing reading disabled (reported0 by construction).

| Route | Operations | Total managed bytes | Bytes/op |
| --- | ---: | ---: | ---: |
| typed-list-short-equivalent | 64 | 1730560 | 27040.0 |
| typed-list-short-unequal | 64 | 1814528 | 28352.0 |
| typed-list-long-equivalent | 64 | 2146864 | 33544.75 |
| typed-list-long-unequal | 64 | 2237440 | 34960.0 |
| typed-token-multilingual-equivalent | 64 | 1686528 | 26352.0 |
| typed-token-multilingual-unequal | 64 | 1770496 | 27664.0 |
| typed-qname-equivalent-contexts | 64 | 1693232 | 26456.75 |
| typed-qname-unequal-contexts | 64 | 1776640 | 27760.0 |
| public-short-equivalent | 64 | 98611872 | 1540810.5 |
| public-short-unequal | 64 | 17771120 | 277673.75 |
| public-long-multilingual-equivalent | 64 | 113757144 | 1777455.375 |
| public-long-multilingual-unequal | 64 | 18031728 | 281745.75 |

Cold fixture setup: `1685048`B, including authored XML, graphs/registries and retained source inputs. Positive new1024-byte-array control: `1048`B. Separate full-GC heap readings: `1521616`B before versus `2276672`B with16retained owned outputs (12contentprograms+4schemas), aggregate delta `755056`B. This approximate whole-heap delta includes output-array/compilation caches and is not an object-size or per-result guarantee. Fixture and returned objects remain live through the readings; transient datatype values belong only to each compilation call.

All twelve owned routes allocate. No zero-allocation, throughput, optimization-benefit, or cross-architecture claim. This is bounded single-host allocation evidence for the functional repair, not a baseline comparison: the original behavior rejected valid constraints. Public success completes publication of every builtin/user schema type; rejection terminates before full publication, so successful and failed totals are different workloads. Warmup does not hide cold setup, and fractional bytes/op may include occasional pool/cache growth.

Raw outputs/logs remain ignored under `artifacts/M8-xsd-attribute-fixed/measurement-1/`; the concise summary and frozen source/identity proposal are committed. No retries/extensions or worker measurement occurred. Future measurements require a new explicit fresh host lease.
