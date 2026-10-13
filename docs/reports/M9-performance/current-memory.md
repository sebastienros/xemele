# Current streaming memory evidence

Five exact 1 GiB runs at a9cd65d8ab179e434007816530cc75d18b259723 on .NET 10.0.11 / Arm64. The unchanged generated oracle checks 262,144 starts and ends, 1,070,333,958 text characters, one completion, early callbacks and 65 collected live samples per mode. Source/sink/runtime bytes remain included in the conservative bound.

| Mode | Maximum sampled whole managed heap (bytes) | External peak process RSS (bytes) |
| --- | ---: | ---: |
| sax | 253248 | 60702720 |
| sax-async | 289504 | 60538880 |
| pull | 255392 | 59981824 |
| pull-async | 291304 | 61194240 |
| push | 261528 | 59555840 |

All five are below 8,388,608 bytes. The retained 9 MiB control measured 9,627,776 live bytes and correctly refused that bound. All outer commands exited zero with child reaping, absent owned groups and closed logs.

RSS and cumulative allocations remain separate from managed live memory. Local durations are informational. Fuzzing was not executed. Historical observations and failed receipts remain unchanged; these new results supply the current requalification.
