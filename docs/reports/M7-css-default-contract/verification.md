# Constructor contract verification

Integrated source162c677 preserves the existing implicit public constructor by
spelling it explicitly and defines universal-selector behavior. Selector.golden.txt
is unchanged. Pinned SDK10.0.400/runtime10.0.11 offline Release Selector declaration
and existing contract checker builds passed; exact136member baseline matches.
The typed constructor consumer compiles and existing actual XML/tree/streaming
preservation probe passes. A baseline removing only CssSelector.#ctor rejects1.
Declaration lane alone uses NoWarn=CS0436 for intentional duplicate model types;
product runtime remains unmodified. No stub is shipped. All changed implementation
paths owned; planning168 and whitespace checks pass. Actual default equivalence
belongs to M7-css after this verified prerequisite, not declaration execution.
