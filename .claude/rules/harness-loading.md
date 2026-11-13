---
paths:
  - "CLAUDE.md"
  - "AGENTS.md"
  - "docs/ai-includes/orientation.md"
  - ".claude/rules/*.md"
  - ".mcp.json"
  - "docs/adrs/011-knowledge-delivery-tiers.md"
  - "docs/reference/rules-catalog.md"
---

<!-- Path-scoped (ADR-011). Every fact cites a doc URL or an empirical context. If Claude Code
     changes, update THIS file first. -->

# Claude Code harness facts: what loads when (verified)

Hooks and settings: `harness-facts.md`. Skill and agent frontmatter, models and spawns:
`harness-skills-agents.md`. A trap sheet (`.claude/rules/traps/`) does not load this rule.

## Context loading

| Fact | Source |
|---|---|
| CLAUDE.md should stay under 200 lines ("longer files consume more context and reduce adherence"). An `@path` import loads at launch (up to four hops), so it organises but saves nothing. Claude Code reads AGENTS.md natively (v2.1.277+) only when no CLAUDE.md, `.claude/CLAUDE.md` or CLAUDE.local.md exists at or above the working directory, so CLAUDE.md keeps its `@AGENTS.md` import. Installed version: read it from a transcript's version field; do not record it here. | https://code.claude.com/docs/en/memory "AGENTS.md" (DOC, 2026-10-08) |
| Custom and general-purpose subagents load the CLAUDE.md hierarchy, imports and unscoped rules; Explore and Plan skip them and the git status, but path-scoped rules and nested CLAUDE.md still load when they touch matching files. `omitClaudeMd: true` (v2.1.271+) opts a custom agent out; no TAOM agent sets it. The docs say no subagent loads the main session's auto memory, but the transcripts disagree: `MEMORY.md` is in the `instructions` file list and the rendered text of every sampled subagent spawn that had that record. Cause unknown, but not the teams flag: at 0, at 1 and unset the list names `MEMORY.md` and `SendMessage` is offered, so the flag left the settings ([retro](../../docs/reviews/harness-retro-2026-11-03.md)). Documented inheritor: a fork (the whole parent conversation; default on in VS Code from 2.1.302). | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-23); EMPIRICAL 2026-11-02, subagent transcripts of `e--repos-TAOM` |
| On a native 1M model auto-compaction fires at about 967K. `/autocompact 400k` (saved per model) or `autoCompactWindow` lowers it. A subagent inherits the window; `autoCompactWindow` in its frontmatter can only lower it further. Idle compaction (`idleCompaction`, default true) compacts a conversation of 200K tokens or more after about 54 minutes idle. It reaches the VS Code panel in v2.1.314; `harness_retro.py` section 11 measures the rewrites after an idle hour. | model-config, sub-agents, settings-reference#idlecompaction, changelog 2.1.299 and 2.1.314 (DOC, 2026-11-03); `harness_retro.py` section 11 (EMPIRICAL) |
| Explore runs on the main conversation's model (v2.1.198+; Opus 5.5 here), not Haiku. `CLAUDE_CODE_SUBAGENT_MODEL` alone does not change Explore or Plan; a spawn's `model` parameter does, and so would a project agent named `Explore`, which overrides the built-in. | https://code.claude.com/docs/en/sub-agents "Built-in subagents", "Choose a model" (DOC, 2026-10-08); CHANGELOG 2.1.198 |
| After `/compact`, CLAUDE.md, unscoped rules, auto memory, the plan file and a fresh git status are re-injected; up to five recent files are re-read; each invoked skill's body returns capped at 5,000 tokens (25,000 total, oldest dropped); path rules return on the next matching read; SessionStart hooks matching the `compact` source run and their output is added. The skill-description listing is rebuilt and re-sent from v2.1.304; before that it did not return. A re-read file over 5,000 tokens comes back as a path reference, not its content. | https://code.claude.com/docs/en/context-window#what-survives-compaction (DOC, 2026-11-03); changelog 2.1.304 |
| MCP tool schemas are deferred behind tool search; only the tool names load at startup. | context-window docs (DOC, 2026-09-23); this session's deferred-tool list (EMPIRICAL, 2026-09-23) |

## Rule loader (memory) semantics

| Fact | Source |
|---|---|
| A rule with no `paths:` loads at session start. A rule with any `paths:` (even `["**/*"]`) loads when Claude uses Read, Write or Edit on a matching file (Write and Edit since v2.1.288), not on every tool use; from v2.1.293 a single-file `cat`, `head`, `tail`, `sed -n` or `grep` in Bash also loads it. `paths` is the only frontmatter field Claude Code reads; other fields are ignored and stripped. A rule whose YAML fails to parse loads **unconditionally**. Patterns are relative to the directory holding the rule's `.claude` folder, so a rule in this checkout does not load for files in a sibling worktree such as `E:/repos/taom-*` unless Claude starts there or runs `/cd`. | https://code.claude.com/docs/en/memory "Path-specific rules" (DOC, 2026-11-03); CHANGELOG 2.1.288, 2.1.293 |
| A `paths:` rule does **not** fire for a file outside the project directory, even through a `**/` glob. | EMPIRICAL 2026-09-23: reading the live `TAOM_Map/ModuleData/settlements.xml` loaded no rule; reading the repo's shadow copy loaded three |
| A single `*` stays inside one directory: `src/components/*.tsx` matches files "directly in" that folder, "not in subfolders", so `.claude/rules/*.md` leaves out `.claude/rules/traps/`. A pattern with no slash (`*.md`, `CLAUDE.md`) matches at any depth; `/*.md` matches the base directory only. | memory docs, the glob table under "Path-specific rules" (DOC, 2026-11-13) |

## Memory file (MEMORY.md) semantics

| Fact | Source |
|---|---|
| Auto memory is machine-local. MEMORY.md's first 200 lines or 25 KB load in each main session; other memory files load only when read. | memory docs (DOC, 2026-09-23) |
| MEMORY.md lives at `~/.claude/projects/<slug>/memory/`; the docs say only that the slug "is derived from the git repository". The Windows derivation (drive letter lowercased, `--`, separators to `-`) is EMPIRICAL (2026-04-26): derive it, then fall back to substring matching. | memory docs + EMPIRICAL |
| `autoMemoryDirectory` accepts only an absolute or `~/` path; a relative value is silently ignored (TAOM shipped `.claude/memory` that way until 2026-08-05). | https://code.claude.com/docs/en/settings (DOC, 2026-08-05) |
