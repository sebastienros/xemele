# Prepared canonicalization measurement

Runtime source reviewed at 86d201877172af32c0afee7a511bb82b281bf8dc. Release
runtime/tests build passed with no warnings/errors; 63 focused tests passed on
actual .NET10.0.11. API checker matched944 union members. Nonmeasuring controls
passed exact encoder bytes against materialized Replace/UTF8 output and all six
caller-stream/owned-output byte counts. Discovery confirmed12 cases, one
ShortRun InProcessNoEmit job, three warmups/three iterations/one launch.
The initial fixture setup control hit XPath's default operation limit; the
prepared fixture now uses an explicit50,000,000-operation setup budget. No
allocation counters or benchmark timing have run for this task.

The copied verified M5-canonical-provenance freeze/runner covers product sources,
owned fixture/test sources, generated obj/configuration, probe binaries,
SDK10.0.400/full10.0.11runtime/fxr closure, stable effective runtime environment,
and host CPU/memory/architecture controls. Scoped own-group cleanup and final
identity verification run in finally. Build/restore/control/discovery stay
outside measurement. The exact plan requires an exclusive grant, maximum1000s:
allocation90s, twelve ShortRun cases820s, initial/final identity30s each plus
cleanup reserve. Explicit TieredCompilation/TieredPGO/ReadyToRun=0 is frozen;
all timing on Arm64 is informational. No CLI job flags are used.

Allocation scopes remain distinct: cold fixture setup;32 warm and32 measured
512-group borrowed-encoding batches (three ASCII/dense/Unicode encodes per
group, inclusive whole counter plus each batch distribution); six caller-stream
cases128 operations each, whose preflight plans allocate; six owned-output
cases128 operations each including byte arrays; positive1048B-class control;
invalid UTF16 errors;64 retained owned outputs/whole forced-GC heap delta.
No subtraction is used. Every BDN inclusive GC numerator/operation count must
be reported even when the displayed allocation is a dash. Materialized versus
borrowed encoding has different ownership/result shape; it is an explicit
baseline comparison, not a claim that whole canonicalization allocates zero.

The scalar encoder uses borrowed spans and static UTF8 replacements; it checks
and encodes every scalar, so the straightforward loop is the measured baseline.
No unmeasured SearchValues/token shortcut was introduced. Namespace/base plans
and dictionaries are owned temporary preflight objects; synthesized base values
are owned strings. Caller stream remains borrowed and never closed/flushed.
