# Milestone map

Live progress: `python3 tools/planning/tasks.py status`. Dependencies and ownership are authoritative in individual [task records](../tasks/README.md); these pages describe the static schedule.

| Milestone | Standalone additive deliverable | Plan |
| --- | --- | --- |
| M0 | Build, contracts, test/reference/benchmark infrastructure | [M0](M0.md) |
| M1 | Strict XML subset, mutable tree, basic serialization | [M1](M1.md) |
| M2 | XML 1.0/namespaces, DTD parsing, resource policy, recovery | [M2](M2.md) |
| M3 | SAX, pull, push, async I/O, streaming writer | [M3](M3.md) |
| M4 | XPath 1.0 | [M4](M4.md) |
| M5 | DTD validation and canonicalization | [M5](M5.md) |
| M6 | Catalogs, XInclude, adapters, XML CLI | [M6](M6.md) |
| M7 | Legacy HTML, HTML5, CSS | [M7](M7.md) |
| M8 | RELAX NG, XSD 1.0, Schematron | [M8](M8.md) |
| M9 | Reviewed stable API, performance and packaging release | [M9](M9.md) |

All milestones are cumulative release checkpoints. Every implementation task depends only on the same or an earlier numbered milestone. Module work may begin once its actual prerequisites are verified; it need not wait for unrelated intervening modules. Gate tasks additionally depend on the preceding gate, producing an ordered sequence of release checkpoints.

Examples: M7 HTML work can begin after the M3 foundation while M4–M6 progress; M8 Schematron begins after M4 plus its own contract, independent of HTML. M7/M8 release gates still follow the earlier release gates. This distinguishes implementation concurrency from release order.

Within every milestone: settle the contract, run the independent lanes listed in its plan, integrate dependent composition tasks, then run the milestone gate. Parallel readiness is computed from task dependencies, never inferred from milestone numbers alone.
