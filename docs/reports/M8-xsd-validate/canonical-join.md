# Verified canonical constraint join

Runtime join `75d037bcddf3f1515a3ecf48ac524f4a2c6ae74c` follows the exact
verified helper checkpoint `2f58db049ba0ebf41ef56ef09ca468395934dd30`.
The task record preserves all earlier source-review and 851-case/two-failure
history and adds the verified helper prerequisite.

The runtime delta is limited to `Evaluator.cs` and `Values.cs`. An applied
empty-simple default/fixed constraint is prepared through
`DatatypeConstraintRepresentation.TryCreate` using the declaration's simple type
and captured namespace context. Actual-type assessment consumes the resulting
owned `Lexical` and `Context`, retaining its newly parsed actual selected value.
Fixed equality compares against `DeclaredValue`. Failed preparation reaches the
single existing identity leave hook with invalid validity; there is no value-only
facet shortcut or DOM augmentation. Prior derivation, nil, wildcard ID, identity,
and charged text behavior is unchanged.

The preserved independent witnesses now pass: declared integer `01` becomes
canonical `1` for a selected one-digit pattern restriction, while declared
decimal `1` becomes canonical `1.0` and is rejected by selected integer lexical
assessment. Declaration/binding-local QName contexts and selected list/union
behavior remain covered by earlier core and verified helper tests.

Actual `python3 docs/reports/M8-xsd-validate/check.py` at that source passed all
937 selected cases: 156 owned, 86 helper, and 695 other affected cases. Both
canonical regressions and all 22 assessment-route cases are included. There were
zero failures/errors/skips/not-run on pinned .NET 10.0.11 Arm64. The same command
passed the exact 42 safe resolver cases, 1,410 public and two frozen evaluator
member comparisons, normal and true XML-only consumers, planning 182 and diff
checks. Release builds had zero warnings/errors. WarmBorrowed methods are
explicitly excluded, and no allocation counters, BDN or timing workload ran.
Separate frozen datatype and facet comparisons each matched 316 members.

Exact final runtime identities for bounded review:

| File | SHA-256 |
|---|---|
| Evaluator.cs | ca7c7f31f93dc841ee081daa99139ae25eb2e7b36f56c66b8ec8a3fa547f6af8 |
| Values.cs | 02f23a19d01f9c8438386a9b3ee1abdf4e0cef1bcab16441435bbad97677c49a |

All raw check outputs remain ignored/transient. Final source-review acceptance
and separately granted allocation evidence remain pending. The task is still
in progress and exposes no public XSD facade or cumulative conformance claim.
