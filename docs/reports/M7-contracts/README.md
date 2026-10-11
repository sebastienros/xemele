# M7 contract evidence

Tested implementation revision: 854f3ccabe01b1076e44f89b7e19d69449444d81.

`python3 tests/Contracts/Fixtures/M7/check.py --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'`
passed: three independent Engine/Tokenizer/Selector projects compiled with only
completed Xemele and local declaration files; real XML tree/streaming probes passed
in each on explicitly selected runtime 10.0.11 with SDK 10.0.400. Exact emitted
API baselines matched 173, 116 and 131 members respectively. Three missing-member
baseline mutation controls rejected with API mismatch. Owned diff/untracked checks
passed; no runtime or shared build source was modified. Local restore used local
source and no package downloads. Compiler-server reuse disabled.

`python3 tools/planning/tasks.py validate` passed all 164 task records.
`git diff --check` passed. These checks ran against the revision above before
this report/task metadata commit. Initial exploratory build failed because its
probe referenced Xemele.XmlReader; corrected to actual Xemele.Streaming.XmlReader
and rebuilt successfully. No failed result is represented as passing.

Nonpackaged declaration methods throw by design and are never executed. These
results freeze signatures and prove compatibility with earlier dependencies;
they do not prove runtime HTML/CSS correctness, corpus coverage or allocations.
Full WHATWG/tokenizer/entity corpus pinning belongs to implementation corpus
owners; CSS owner must pin/classify xmloxide overlap against the exact matrix.
No shared repair is requested. No runtime hot path was implemented and no
allocation test, throughput measurement or cumulative suite was run.

Independent coordinator source-only review approved the tested revision after
state-specific NUL, frameset and ScriptingEnabled metadata corrections. This is
review evidence, not an additional test run.

## Ownership-base harness repair

Tested repair revision: 10bbc38672a7ee92304d770a923a35de2cafb452.
Optional --ownership-base selects the known preintegration revision for combined
tree checking; the default remains original claim
9e530764937150f85998d5db7807fd6581d9d425. The owned-path predicate is unchanged.
Coordinator must separately inspect the complete worker diff against the claim.
The same pinned check.py command, task validation and diff check reran successfully
at this revision: three consumers/XML probes, 173/116/131 API members and three
rejecting API controls; 164 task records valid. Earlier source review remains
recorded against its actual reviewed contract revision, not this harness repair.
