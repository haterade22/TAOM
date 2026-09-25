---
paths:
  - ".claude/settings*.json"
  - ".claude/hooks/**"
  - ".claude/rules/**"
  - ".claude/agents/**"
  - ".claude/skills/*/SKILL.md"
  - ".claude/skills/**/*.sh"
  - ".claude/statusline.sh"
  - ".mcp.json"
  - "CLAUDE.md"
  - "AGENTS.md"
  - "docs/ai-includes/orientation.md"
  - "docs/adrs/011-knowledge-delivery-tiers.md"
  - "docs/reference/hooks-catalog.md"
  - "docs/reference/rules-catalog.md"
  - "tools/test_hooks.sh"
  - "tools/audit_claude_config.py"
description: Verified Claude Code load semantics, hook lifecycle and frontmatter schema, loaded when harness files are opened so an edit does not recreate an already-fixed bug.
---

<!-- Path-scoped since 2026-09-23 (ADR-011): these facts matter while the harness is being changed,
     not on every turn. The globs name the files that ARE the harness, not all of .claude/: a
     /deep-review lens reading its lens file, or any agent in a .claude/worktrees/ checkout, would
     otherwise pay for this file. Every fact cites a doc URL or an empirical context. If Claude
     Code changes, update THIS file first. -->

# Claude Code harness facts (verified)

## Context loading

| Fact | Source |
|---|---|
| CLAUDE.md should stay under 200 lines ("longer files consume more context and reduce adherence"). An `@path` import loads at launch (up to four hops), so it organises but saves nothing. Claude Code reads AGENTS.md natively only from v2.1.277; the installed 2.1.241 needs CLAUDE.md's `@AGENTS.md`. | https://code.claude.com/docs/en/memory (DOC, 2026-09-23); `claude --version` (EMPIRICAL, 2026-09-23) |
| Custom and general-purpose subagents load the CLAUDE.md hierarchy, its imports and the project rules. The built-in Explore and Plan agents load none of them. No subagent loads the main session's auto memory (a fork inherits the conversation instead). `omitClaudeMd: true` (v2.1.271+) opts a custom agent out. | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-23) |
| After `/compact`, CLAUDE.md, unscoped rules, auto memory, the plan file and a fresh git status are re-injected; up to five recent files are re-read; each invoked skill's body returns capped at 5,000 tokens (25,000 total, oldest dropped); path rules return on the next matching read; SessionStart hooks matching the `compact` source run and their output is added. **The skill-description listing does not return.** | https://code.claude.com/docs/en/context-window (DOC, 2026-09-23) |
| MCP tool schemas are deferred behind tool search; only the tool names load at startup. | context-window docs (DOC, 2026-09-23); this session's deferred-tool list (EMPIRICAL, 2026-09-23) |

## Skill load semantics

| Fact | Source | Why we care |
|---|---|---|
| Skill **descriptions** load eagerly unless the skill sets `disable-model-invocation: true`; **bodies** load only when invoked. | https://code.claude.com/docs/en/skills (DOC, 2026-04-26) | Count frontmatter, not bodies, for the eager total. |
| Consumed frontmatter: `name`, `description`, `when_to_use`, `argument-hint`, `arguments`, `disable-model-invocation`, `user-invocable`, `allowed-tools`, `disallowed-tools`, `model`, `effort`, `context` (`fork`), `agent`, `hooks`, `paths`, `shell`. | skills docs (DOC, 2026-07-18) | `triggers:` is not a field (a gstack preamble relic): move its phrases into `description`. |
| A description stays at most 30 words: skill and agent descriptions load on every Task spawn. | EMPIRICAL (`scan.sh` flag) | Description creep came back twice on `/freeze` and `/investigate`. |
| `disable-model-invocation: true` makes a skill user-only. | skills docs (DOC) | For skills that cost money or publish; TAOM mostly uses CLAUDE.md's "never auto-invoke" row instead. |

## Agent (subagent) load semantics

| Fact | Source | Why we care |
|---|---|---|
| Agent **descriptions** load with every Task spawn; an agent's **body** loads only when that agent is spawned. | docs (skills + Task tool) | Same eager/lazy split as skills. |
| `model:` takes an alias (`sonnet`, `opus`, `haiku`, `fable`), a full id or `inherit`; `effort:` takes `low` to `max`, per model. Resolution: the spawn's `model` parameter, then the definition's `model:`, then `CLAUDE_CODE_SUBAGENT_MODEL`, then the main model. | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-18) | `deep-reviewer` pins Opus 5.5 (`claude-opus-5-5`, Mike 2026-09-23) at max effort, which Opus 5.5 supports (all five levels, https://platform.claude.com/docs/en/build-with-claude/effort, verified 2026-09-23); passing `model` on the spawn silently overrides that, so `/deep-review` never passes one. |

## Hook lifecycle

| Fact | Source | Why we care |
|---|---|---|
| Hooks in `settings.json` are global. Hooks in a skill's `hooks:` frontmatter fire only while that skill is invoked; a state file written from elsewhere is inert. `/investigate` deliberately re-declares `/freeze`'s hook. | https://code.claude.com/docs/en/hooks (DOC, 2026-04-26) | Copy an inline hook to another skill only with a stated reason. |
| **Visibility.** Plain stdout reaches Claude only for `SessionStart`, `UserPromptSubmit`, `UserPromptExpansion` and `PostModelSwitch`; for every other event it goes to the debug log. Stderr from a hook that exits 0 never reaches Claude. A PreToolUse `deny` reason and exit-2 stderr do; an `ask` reason is shown to the user, not to Claude. A Stop hook reaches Claude through JSON `{"decision":"block","reason":"..."}` on stdout, or exit 2: Claude answers the reason in one more response, the next Stop payload carries `stop_hook_active: true`, and the harness ends the turn after 8 consecutive blocks. Stop's `additionalContext` is documented, unproven here. `systemMessage` goes to the user. | hooks docs, "Exit code 0" and "Stop decision control" (DOC, 2026-09-23); EMPIRICAL 2026-09-23: `suggest-compact.sh` printed about 18 times in one session and none arrived | A reminder printed anywhere else is silent: make it a gate, or move it to a visible channel. The four Stop reminders use `.claude/hooks/_stop_reminder.sh` (`tools/test_hooks.sh` 7a). |
| **PreToolUse output contract.** Tool-input JSON arrives on stdin. Print `{}` to allow; to block or confirm, print `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}}` (or `"ask"`), or exit 2 with the reason on stderr. A top-level `permissionDecision` is **ignored**. Other events keep a top-level `decision`. Malformed JSON fails open. | hooks docs, "PreToolUse decision control" (DOC, 2026-09-23); EMPIRICAL 2026-09-23: nine TAOM gates printed the top-level form, and a live commit the label gate had denied ran anyway, until #647 | `tools/test_hooks.sh` 5c fails on the top-level form; matching the text `"permissionDecision":"deny"` passes both forms, which is how the dead gates stayed green. Escape `\` and `"` in any interpolated path. |
| **TAOM hooks fail open.** A hook's own bug never blocks: swallow internal errors, and non-deny hooks exit 0. A deny is only ever a deliberate gate decision. | this repo's design | `/security-scan` deliberately does not flag `\|\| true`, `2>/dev/null` or `exit 0`. |
| All hooks matching an event run in **parallel**. An omitted `timeout` defaults to **600 s** (`UserPromptSubmit` 30 s, `SessionEnd` a 1.5 s shared budget). A timed-out hook is **killed and its output discarded**, so a PreToolUse gate then fails open. `async: true` hooks are exempt from timeouts and cannot gate. | hooks docs (DOC, 2026-08-31) | The 2026-08-31 twenty-minute stalls. Every registration carries an explicit `timeout`, and slow work is bounded inside the script (`hook-authoring.md`). |
| 30 hook events exist, including `PostToolUseFailure`, `SubagentStart`, `PreCompact`, `PostCompact` and `WorktreeCreate`. Handler fields beyond `matcher` and `command`: `type`, `if` (a permission-rule gate such as `"Bash(git commit*)"`), `timeout`, `statusMessage`, `once`, and for command hooks `args`, `async`, `asyncRewake`, `shell`. | hooks docs (DOC, 2026-07-18) | Don't flag a real event as undocumented. The `if:` migration is deferred: a mis-scoped `if:` silently disables a gate, so it needs a per-gate proof. `WorktreeCreate` must print the worktree path, so no passive logger belongs there. |
| Not documented, so assume neither way: whether `settings.json`'s `env` block reaches hook processes (`_pybin.sh` probes `TAOM_PYBIN` rather than trusting it), and any `timeout` for `statusLine` (keep `.claude/statusline.sh` cheap). | hooks docs | A per-session logger belongs on `SessionEnd`, not `Stop`, which fires every turn. |

Authoring conventions (mirror a sibling's full convention set, the two-stage git-commit matcher, amend
handling, log rotation, timing the slow path, never `python3`) are in `.claude/rules/hook-authoring.md`,
which loads with any hook file.

## Rule loader (memory) semantics

| Fact | Source |
|---|---|
| A rule with no `paths:` loads at session start. A rule with any `paths:` (even `["**/*"]`) loads when Claude **reads** a matching file, not on every tool use. `paths` is the only frontmatter field Claude Code reads; other fields are ignored and stripped. A rule whose YAML fails to parse loads **unconditionally**. | https://code.claude.com/docs/en/memory (DOC, 2026-09-23) |
| A `paths:` rule does **not** fire for a file outside the project directory, even through a `**/` glob. | EMPIRICAL 2026-09-23: reading the live `TAOM_Map/ModuleData/settlements.xml` loaded no rule; reading the repo's shadow copy loaded three |

## Memory file (MEMORY.md) semantics

| Fact | Source |
|---|---|
| Auto memory is machine-local. MEMORY.md's first 200 lines or 25 KB load in each main session; other memory files load only when read. | memory docs (DOC, 2026-09-23) |
| MEMORY.md lives at `~/.claude/projects/<slug>/memory/`; the docs say only that the slug "is derived from the git repository". The Windows derivation (drive letter lowercased, `--`, separators to `-`) is EMPIRICAL (2026-04-26): derive it, then fall back to substring matching. | memory docs + EMPIRICAL |
| `autoMemoryDirectory` accepts only an absolute or `~/` path; a relative value is silently ignored (TAOM shipped `.claude/memory` that way until 2026-08-05). | https://code.claude.com/docs/en/settings (DOC, 2026-08-05) |

## Settings

| Fact | Source |
|---|---|
| Claude Code asks for a `Co-Authored-By: <model>` commit trailer and a PR footer unless `attribution.commit` / `attribution.pr` are `""`; `sessionUrl: false` drops the cloud session link. TAOM sets all three in `.claude/settings.json`; `check-commit-subject-version.sh` stays as the backstop. `includeCoAuthoredBy` is the deprecated form. | https://code.claude.com/docs/en/settings-reference (DOC, 2026-09-23); EMPIRICAL 2026-09-23: the next harness reminder after the edit said to add no attribution |

## Gitignore blast radius

`git check-ignore -v <path>` is the authoritative test; grepping `.gitignore` is not. Generic patterns
(`bin/`, `obj/`, `tmp/`, `cache/`) match at any depth, which once shipped `check-freeze.sh` untracked
and dead. Name new `.claude/` folders descriptively (`scripts/`, `state/`). (git docs; 2026-04-26
deep-review)

## What this rule changes about how you work

1. **Load behaviour:** check the facts here first. If this file disagrees with what you need, update
   it first, with a source.
2. **Porting from an external suite:** follow `external-skill-ports.md`, which starts with
   `python tools/audit_claude_config.py --root <dir> --external`.
3. **Committing `.claude/` changes:** `check-claude-files-tracked.sh` blocks if a file under
   `.claude/{skills,agents,rules,hooks}/` is untracked or ignored. It fires only on Claude-driven
   commits.
4. **Review skills:** Phase 3e root-cause analysis applies to every confirmed bug, not only HIGH ones.
5. **Writing a fact here:** cite a doc URL (DOC-BACKED) or an observation (EMPIRICAL: where, when).
   Unsourced "verified" claims age into wrong ones.

Parallel agents that may edit single-owner files (csproj, `IoC.cs`, `SubModule.cs`,
`Directory.Build.props`) pass `isolation: "worktree"`; case studies are in
`docs/ai-includes/agent-teams.md`.

## Last verified: 2026-09-23
