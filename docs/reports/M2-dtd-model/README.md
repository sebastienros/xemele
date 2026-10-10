# Independent immutable DTD model

The internal runtime model is the exact model section of the verified M2 DTD
fixture, ending before `DtdParseStep`. It contains no parser, budget, framer,
resolver, decoder, core or DOM dependency. Strings, locations, Uri references and
immutable child/declaration objects are retained. Each collection constructor
copies into a fresh array wrapped by a read-only collection. Snapshotting is an
owned persistence boundary, not a transient parsing primitive. Supplied order,
duplicates, names, replacement text, identifiers, origins and default values are
preserved. Grammar performs normalization, lexical validation and declaration
precedence later.

`Approved/ApprovedModel.cs` is the unchanged extracted approved fixture section.
The verifier checks its exact text before compiling it independently with actual
M1 common sources. The standalone sample compiles actual model/common source only,
compares all 11 exact types and 154 reflection inventory rows (constructor/method
parameters, nullable generic/property/return metadata, record init modifiers,
enum constants and type/member flags), and rejects extra DTD types. No declaration
parser placeholder is compiled or invoked.

Eight focused Facts cover caller array/list mutation and generic/non-generic
IList mutation rejection, nested particles, occurrence/kind values, ordered tokens
and duplicate declarations, all five model tables and separate entity namespaces,
one-shot enumerables, empty snapshots, default/type/value combinations, internal /
external / unparsed entities, identifiers, location/Uri/origin retention and all
seven required null collection parameters. SDK source globs include production and
test files without changing shared build files or public API fragments.

The standalone sample reports cold fixture/setup allocation separately from 1000
warm synchronous constructor operations. It also demonstrates that a caller List
with capacity 200000 and mutable character source can be collected while retained
model content remains usable. These are bounded informational measurements, not
a throughput comparison, zero-allocation promise or retained-byte measurement.

Run the task-owned verifier with pinned SDK/runtime settings:

```sh
export DOTNET_ROOT='/Users/sebastienros/Library/Application Support/dotnetup/dotnet'
export PATH="$DOTNET_ROOT:$PATH"
python3 -B docs/reports/M2-dtd-model/verify.py --report-prefix fresh --runtime-count 306
```

The report prefix must be new. Reports preserve commands, outcomes and the tested
source revision. Runtime defaults and test parallelism remain unchanged. The
fresh BCL build output is copied in full before historical solution builds;
242 records must match its unchanged hash and current source version. Checks
expect API251, discovery3388, direct306+255+299 (860 total, baseline852+8), planning14
and task validation118. Full MTP execution is the final command in the historical
M1 verifier. Suite failures are retained; no retry or unrelated edits mask them.

Initial preparation: source revision 2b86b44 built the actual common-only sample,
then the approved project failed CS2001 because its relative common-source paths
went up five levels rather than four. No focused or cumulative suite ran in that
preparation command. Commit b902b16 corrected only those paths before the complete
source-revision check. The first complete report is `first/`.
