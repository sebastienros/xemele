# Xemele

A planned managed C# XML and HTML toolkit inspired by libxml2 and xmloxide, targeting **.NET 10 (`net10.0`)**.

The goal is recognizable upstream concepts and behavior with idiomatic .NET APIs, strong conformance coverage, predictable resource use, and competitive performance.

**Status:** design only; no library implementation exists yet. The documents below are proposals, and performance figures are targets, not measured results.

- [Task tracking and session workflow](docs/tasks/README.md)
- [Implementation orchestrator and worker prompts](docs/prompts/README.md)
- [Milestone dependencies and parallel lanes](docs/milestones/README.md)
- [Implementation contracts and API shapes](docs/specs/README.md)
- [Roadmap and feature goals](docs/roadmap.md)
- [Architecture, API design, and repository layout](docs/design.md)
- [Testing and conformance strategy](docs/testing.md)
- [Performance goals and measurement protocol](docs/performance.md)

Start with `python3 tools/planning/tasks.py ready`. Each task has its own status record, acceptance criteria, dependencies, file ownership, and evidence. No library implementation is currently verified.

For an overview, run `python3 tools/planning/tasks.py status`. For a session, read only the selected task and its listed specifications; see [AGENTS.md](AGENTS.md).
