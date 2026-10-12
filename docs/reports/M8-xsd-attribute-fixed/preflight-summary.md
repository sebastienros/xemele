# Disabled control preflight

Actual elevated `python3 docs/reports/M8-xsd-attribute-fixed/measure.py --verify-only`: exit0,454frozen identities and ps permission passed.
Actual elevated `python3 docs/reports/M8-xsd-attribute-fixed/measure.py --preflight`: exit0, disabled child PID88642 exit0; direct child reaped, owned group absent, log closed, groupMembers empty. 20s work/30s hard bound; actual execution under1second.454identities unchanged before/after. All allocation/GC/timing counters disabled.
Preparation restore/build passed with pinned10.0.400/10.0.11 and zero warnings/errors. Preparation control:12routes x64loopops plus16warmups,1456consumed,16retained; every allocation/GCreading0 because disabled. These are lifecycle/correctness facts, not allocation results.

Root alone may launch proposal under fresh host lease. Ignored raw artifacts: artifacts/M8-xsd-attribute-fixed/preflight/{control.json,lifecycle.json,command-0.log}, preparation/control.json, prepare-1.log.
