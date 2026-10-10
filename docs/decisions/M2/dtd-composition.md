# DTD composition repair

The existing constructor inventory and public methods remain unchanged. Three
assembly-internal instance hooks initialize independently framed external sources:

- `void SeedPriorModel(DtdModel model, CancellationToken cancellationToken)`:
  once on a fresh initial external parser with a shared operation ledger, before any Append (including empty or
  invalid submissions) and before CompleteByteSource. Stages ordered first-wins
  declaration collections and retained-name keys, reusing immutable declaration
  references, flags and locations. It performs no budget calls, expansion, or
  default application. Cancellation checks cover each declaration, particle,
  child and bounded name scan. Cancellation/allocation failure before publication
  leaves the seed retryable without partial tables or ledger changes. Standalone local-budget
  parsers reject seeding: they cannot inherit previously charged operation work.
  The caller must use the same live ledger for prior internal parsing, framing
  and external grammar; model objects do not carry ledger provenance. The live shared ledger must
  deduplicate retained names ordinally across parser instances, including names
  charged in declarations discarded by first-wins model output. Seeding itself
  makes no ChargeName calls; later lookup misses can call the ledger for a name
  absent from the model without recharging its existing distinct-name entry.
- `void SetFramedInitialSource(XmlLocation firstLocation)`: once on the same
  fresh external parser; may run before or after seeding. URI must equal the
  constructor source URI (null permitted); line/column are both positive or both
  zero, offset null/nonnegative and no smaller than the initial lower bound.
  Disposes local prolog framing and sets the actual normalized scalar cursor.
  A null offset keeps the existing mapping lower bound. CompleteByteSource still
  provides the exact absolute EOF, including empty/BOM-only sources.
- `void PushFramedEntity(Uri finalUri, XmlLocation firstLocation)`: exactly one
  pending external PE request; absolute final URI equals firstLocation.SourceUri,
  cursor/offset validation as above. The accepted depth entry belongs to the
  request. Capacity reservation precedes cursor/stack publication; no new entry,
  source identity, local prolog or public PushEntity reentrancy occurs. Parent
  suffix, final flag, cursor, byte scope and owned replacement state stay saved.

Invalid hook timing/arguments do not mutate parser state. Append cancellation
and budget callback failures retain the existing terminal cleanup semantics.
Legacy construction/PushEntity continue to frame locally exactly as before.

The caller validates its external declaration with an outer common-only framer,
then uses Declared.NextLocation or Absent.PrefixLocation. It drains the absent
prefix once and forwards it before the unconsumed suffix, preserving absolute
mappings. Declaration source scalars are charged once by the shared framer;
absent-prefix/body scalars once by grammar; seeded tables/names never again.
Framed grammar treats a second text declaration as reserved PI syntax rather
than stripping it. No decoder, resolver, core or production XML behavior is
claimed by this repair.

Allocations belong to seed staging/index persistence, source/setup and immutable
model output. Input remains borrowed. There is no new delimiter scanner or fixed
vocabulary optimization; existing verified literal matching remains unchanged.
New scoped setup/composition allocations and warm scan boundaries require actual
measurement, separate from preserved predecessor timing.
