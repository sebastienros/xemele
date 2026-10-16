# Frozen validating XML profile

`applicability.json` retains every2585 original ID and3386 resource hashes from
the verified20130923 inventory. The deterministic classifier reads source metadata
and original bytes only. There are976 associated-DTD valid/invalid rows;1609 rows
preserve the existing effective XML1.0 grammar profile (1531 source not-wf/error,
plus78 source valid/invalid documents without a DOCTYPE). No success-dependent
filtering exists. Associated valid rows must validate; invalid rows must reject
or produce complete invalidity. Existing mandatory grammar rejections stay
rejections. Quota/incomplete/refused-resource outcomes never satisfy invalidity.

The profile is XML1.0 Fifth Edition plus Namespaces1.0 Third Edition and the
verified M2 effective-version/encoding contract, not an assertion that original
XML1.1 metadata is native XML1.1 validation. All original resources stay in the
immutable external cache; no corpus payload redistribution is added.
