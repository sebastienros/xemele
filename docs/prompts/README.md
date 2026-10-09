# Implementation session prompts

- [Orchestrator](orchestrator.md): coordinates the complete implementation through M9, with bounded worker assignments and serialized integration.
- [Worker template](worker.md): the coordinator fills this for one task in one isolated worktree.

Start a new session in this repository with:

> Execute `docs/prompts/orchestrator.md` as my instruction. Orchestrate isolated worker chats to complete Xemele through verified `M9-gate`, following the prompt's scope and authorization.

This authorizes the orchestration described in that file when you submit it; editing these documents alone does not launch work. The prompt authorizes local commits/integration. Add an explicit instruction if you also want remote pushes or publication.

The design follows the official guidance to assign independent subagents clear bounded outcomes and coordinate shared writes, combined with this repository's task ownership and evidence rules. Separate contexts do not by themselves establish isolated checkouts. [OpenAI multi-agent guidance](https://developers.openai.com/api/docs/guides/agents-api/multi-agent), [Codex workspace/worktree guidance](https://developers.openai.com/blog/mastering-codex-remote-for-engineering)
