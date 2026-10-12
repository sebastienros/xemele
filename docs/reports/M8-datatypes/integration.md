# Datatype composition integration

Actual tested combined revision: c6da4a5425cbdcd7104330d351f62b59c72fdf2b.
Root ran `python3 docs/reports/M8-datatypes/checks.py`, exit0:354 tests
(77 composition+277 children/model), no failures/errors/skips/not-run, public1393,
frozen316, normal and genuine XML-only consumers, Release0warnings/errors,
177task validation and diff check.

Final worker172581919607789cca470da1b0004d584a8e8702 was merged afterward;
only allocations.md, measurement-summary.json and own task JSON changed.
Git comparison confirmed source, tests, benchmarks, samples, API and shared build
inputs equal the actual tested combination. No later rerun is claimed.

Root independently inspected successful measurement2 raw distributions and
all six exact BDN GC counters after durable RELEASE2fb89c1d. See allocations.md
for all owned/cold costs, original failed measurement, residuals and timing limits.
The PARK acknowledgments covered all then-known Xemele/GregsEx actors. A separate
parser research chat was discovered afterward; parser confirms builds/tests occurred but exact overlap is
unknown; treat timing as potentially contended. This does not change per-thread observed allocation counts; timings
remain informational and no speedup/repeatability claim is made.
