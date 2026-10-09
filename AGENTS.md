# Xemele

Managed C# XML/HTML toolkit, `net10.0` only. No implementation exists until task evidence says otherwise.

- Start with `docs/tasks/README.md` and `python3 tools/planning/tasks.py ready`.
- Read only your task JSON, its listed specs, and relevant source/tests. Do not load all design/task files.
- Before runtime hot-path work, read [string/byte performance rules](docs/performance-coding.md): borrowed spans, static literals, benchmark-backed SearchValues/token matching, and explicit allocation evidence.
- Claim one ready task through the coordination workflow before implementation; use an isolated branch/worktree.
- Edit only its owned paths and task record. Shared contracts/build files require their owning task; record requests in your handoff.
- Implement the listed acceptance criteria. Preserve earlier milestones; no dependency on a later milestone or unmerged peer branch.
- Update only your task's status, criteria, and evidence. `implemented` means branch work; `verified` requires integrated code and passing checks.
- Run relevant checks plus `python3 tools/planning/tasks.py validate`. Never mark unrun checks as passing.
- Keep handoffs concise: changes, checks, unresolved issues, and required follow-ups. See the task workflow for details.
