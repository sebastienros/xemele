# Independent integration verification

Integrated revision84dfa585215e87f1ccea65a97db414ee4752a7ab.
Root fresh Release builds pinned SDK10.0.400 and RuntimeFrameworkVersion10.0.11,
shared compilation disabled: zero warnings/errors. Explicit --fx-version10.0.11
executions passed all25 PI/prior model tests and the HTML/XML preservation sample.
Root parsed all25 actual XML identities as Pass. Official discovery/compose and
unchanged approved M0 checker matched1203 compiled API members,3388 original
manifest fixtures, bootstrapFalse.3386 cache resources copied with source/dest
hash checks; original cache untouched. Planning167 and whitespace checks passed.

Root independently inspected22 actual allocation rows after durable release:
16 batches×4096 existing-value mutations0B, positive1048B, cold1112B,
existingtarget256B/node, newtarget87768B/256 including first dictionary backing
storage and growth, graph2020800B/100. Retained3934000B inclusive whole-heap delta
for100 documents includes document array/runtime roots; no per-node retention
precision, throughput or x64 claim. No measurement rerun.

Git comparison proves runtime, tests, probe and sample inputs exactly unchanged
from measuredab5b8d through integrated84dfa585. Parser/builder/serializer owners
must confirm composition using these verified additive PI kinds/factory.
