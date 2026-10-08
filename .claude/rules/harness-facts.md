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
| CLAUDE.md should stay under 200 lines ("longer files consume more context and reduce adherence"). An `@path` import loads at launch (up to four hops), so it organises but saves nothing. Claude Code reads AGENTS.md natively (v2.1.277+) only when no CLAUDE.md, `.claude/CLAUDE.md` or CLAUDE.local.md exists at or above the working directory, so CLAUDE.md keeps its `@AGENTS.md` import. Installed: CLI 2.1.288, VS Code extension 2.1.292. | https://code.claude.com/docs/en/memory "AGENTS.md" (DOC, 2026-10-08); `claude --version` and `code --list-extensions --show-versions` (EMPIRICAL, 2026-10-08) |
| Custom and general-purpose subagents load the CLAUDE.md hierarchy, its imports and the project rules. The built-in Explore and Plan agents load none of them. No subagent loads the main session's auto memory (a fork inherits the conversation instead). `omitClaudeMd: true` (v2.1.271+) opts a custom agent out. | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-23) |
| Explore runs on the main conversation's model (v2.1.198+; Opus 5.5 here), not Haiku. `CLAUDE_CODE_SUBAGENT_MODEL` alone does not change Explore or Plan; a spawn's `model` parameter does, and so would a project agent named `Explore`, which overrides the built-in. | https://code.claude.com/docs/en/sub-agents "Built-in subagents", "Choose a model" (DOC, 2026-10-08); CHANGELOG 2.1.198 |
| After `/compact`, CLAUDE.md, unscoped rules, auto memory, the plan file and a fresh git status are re-injected; up to five recent files are re-read; each invoked skill's body returns capped at 5,000 tokens (25,000 total, oldest dropped); path rules return on the next matching read; SessionStart hooks matching the `compact` source run and their output is added. **The skill-description listing does not return.** | https://code.claude.com/docs/en/context-window (DOC, 2026-09-23) |
| MCP tool schemas are deferred behind tool search; only the tool names load at startup. | context-window docs (DOC, 2026-09-23); this session's deferred-tool list (EMPIRICAL, 2026-09-23) |

## Skill load semantics

| Fact | Source | Why we care |
|---|---|---|
| Skill **descriptions** load eagerly unless the skill sets `disable-model-invocation: true`; **bodies** load only when invoked. | https://code.claude.com/docs/en/skills (DOC, 2026-04-26) | Count frontmatter, not bodies, for the eager total. |
| Consumed frontmatter: `name`, `description`, `when_to_use`, `argument-hint`, `arguments`, `disable-model-invocation`, `user-invocable`, `allowed-tools`, `disallowed-tools`, `model`, `effort`, `context` (`fork`), `agent`, `hooks`, `paths`, `shell`. | skills docs (DOC, 2026-07-18) | `triggers:` is not a field (a gstack preamble relic): move its phrases into `description`. |
| A description stays at most 30 words: skill and agent descriptions load on every Task spawn. | EMPIRICAL (`scan.sh` flag) | Description creep came back twice on `/freeze` and `/investigate`. |
| `disable-model-invocation: true` makes a skill user-only; when Claude tries to invoke one, it is told to ask the user to run it (v2.1.222). | skills docs (DOC); CHANGELOG 2.1.222 (DOC, 2026-10-08) | For skills that cost money or publish; TAOM mostly uses CLAUDE.md's "never auto-invoke" row instead. |
| A `context: fork` skill runs in the background by default (v2.1.218; `background: false` opts out). A project skill named `verify` is offered to Claude to run right before each commit except docs-only and tests-only ones (v2.1.286). | CHANGELOG 2.1.218, 2.1.286 (DOC, 2026-10-08) | `/verify` is TAOM's; the harness nudge and `check-verification-evidence.sh` point the same way. |

## Agent (subagent) load semantics

| Fact | Source | Why we care |
|---|---|---|
| Agent **descriptions** load with every Task spawn; an agent's **body** loads only when that agent is spawned. | docs (skills + Task tool) | Same eager/lazy split as skills. |
| `model:` takes an alias (`sonnet`, `opus`, `haiku`, `fable`), a full id or `inherit`; `effort:` takes `low` to `max`, per model. Resolution: the spawn's `model` parameter, then the definition's `model:`, then `CLAUDE_CODE_SUBAGENT_MODEL`, then the main model. | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-18) | `deep-reviewer` pins Opus 5.5 (`claude-opus-5-5`, Mike 2026-09-23) at max effort, which Opus 5.5 supports (all five levels, https://platform.claude.com/docs/en/build-with-claude/effort, verified 2026-09-23); passing `model` on the spawn silently overrides that, so `/deep-review` never passes one. |
| The Agent tool takes an `effort` parameter (v2.1.292). Its precedence against a definition's `effort:` is undocumented; the definition's `effort:` overrides the session level but not `CLAUDE_CODE_EFFORT_LEVEL`. | CHANGELOG 2.1.292; sub-agents docs, frontmatter table (DOC, 2026-10-08) | `model-effort-selection.md` passes both on a spawn. |
| Subagents run in the background by default (v2.1.198). A subagent may spawn its own, up to `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` layers below the main conversation (default 3 since v2.1.219; TAOM sets 1, so nesting is off). At most `CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS` run at once (default 20, v2.1.217); past it the Agent tool **refuses** the spawn rather than queueing it. | https://code.claude.com/docs/en/env-vars, sub-agents "Let subagents spawn their own subagents" (DOC, 2026-10-08) | A cap below `/deep-review`'s seven to nine parallel lenses would make it fail, so TAOM sets none. |

## Hook lifecycle

| Fact | Source | Why we care |
|---|---|---|
| Hooks in `settings.json` are global. Hooks in a skill's `hooks:` frontmatter fire only while that skill is invoked; a state file written from elsewhere is inert. `/investigate` deliberately re-declares `/freeze`'s hook. | https://code.claude.com/docs/en/hooks (DOC, 2026-04-26) | Copy an inline hook to another skill only with a stated reason. |
| **Visibility.** Plain stdout reaches Claude only for `SessionStart`, `UserPromptSubmit`, `UserPromptExpansion` and `PostModelSwitch`; for every other event it goes to the debug log. Stderr from a hook that exits 0 never reaches Claude. A PreToolUse `deny` reason and exit-2 stderr do; an `ask` reason is shown to the user, not to Claude. A Stop hook reaches Claude through JSON `{"decision":"block","reason":"..."}` on stdout, or exit 2: Claude answers the reason in one more response, the next Stop payload carries `stop_hook_active: true`, and the harness ends the turn after 8 consecutive blocks. Stop's `additionalContext` is documented, unproven here. PreToolUse, PostToolUse and PostToolUseFailure take `{"hookSpecificOutput":{"hookEventName":"<event>","additionalContext":"..."}}`, which lands next to the tool result; PostToolUse exit 2 also shows stderr to Claude. `systemMessage` goes to the user. | hooks docs, "Exit code 0", "Stop decision control" (DOC, 2026-09-23) and "Add context for Claude" (DOC, 2026-09-29); EMPIRICAL 2026-09-23: `suggest-compact.sh` printed about 18 times in one session and none arrived; 2026-09-29: `check-polearm-shield-parity.sh` had reported to stderr on exit 0 since 2026-08-20 (`tools/test_hooks.sh` 7k) | A reminder printed anywhere else is silent: make it a gate, or move it to a visible channel. The four Stop reminders use `.claude/hooks/_stop_reminder.sh` (`tools/test_hooks.sh` 7a). |
| **PreToolUse output contract.** Tool-input JSON arrives on stdin. Print `{}` to allow; to block or confirm, print `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}}` (or `"ask"`), or exit 2 with the reason on stderr. A top-level `permissionDecision` is **ignored**. Other events keep a top-level `decision`. Malformed JSON fails open. | hooks docs, "PreToolUse decision control" (DOC, 2026-09-23); EMPIRICAL 2026-09-23: nine TAOM gates printed the top-level form, and a live commit the label gate had denied ran anyway, until #647 | `tools/test_hooks.sh` 5c fails on the top-level form; matching the text `"permissionDecision":"deny"` passes both forms, which is how the dead gates stayed green. Escape `\` and `"` in any interpolated path. |
| **An `ask` prompts even in bypass mode.** A PreToolUse `ask` shows the user an approval prompt although the session runs with `bypassPermissions`, and the tool call waits until someone answers. A headless `claude -p` run did not wait: the agent saw the ask's reason and the command did not run. | EMPIRICAL 2026-09-26: the maintainer saw a confirm-gate prompt from a plan 027 review agent in a bypass session (a reviewer the night before stalled for nine hours, most likely the same way); headless probe of `GIT add --dry-run -A` from the PowerShell tool. Hooks docs "PreToolUse decision control" (DOC, 2026-10-08): `"ask"` prompts the user; only a `-p` run, where no one can answer, denies the call and shows Claude the reason. | The confirm gates match dangerous git text anywhere in a command, quoted text included, so an unattended agent keeps such text out of its shell commands: payloads go in files, scripts run by path. |
| **TAOM hooks fail open.** A hook's own bug never blocks: swallow internal errors, and non-deny hooks exit 0. A deny is only ever a deliberate gate decision. The harness is stricter on its own side: since v2.1.288 a PreToolUse or PermissionRequest hook whose matching fails, or whose tool input will not serialize to JSON, blocks the call. | this repo's design; CHANGELOG 2.1.288 (DOC, 2026-10-08) | `/security-scan` deliberately does not flag `\|\| true`, `2>/dev/null` or `exit 0`. |
| All hooks matching an event run in **parallel**. An omitted `timeout` defaults to **600 s** (`UserPromptSubmit` 30 s, `SessionEnd` a 1.5 s shared budget). A timed-out hook is **killed and its output discarded**, so a PreToolUse gate then fails open. `async: true` hooks are exempt from timeouts and cannot gate. | hooks docs (DOC, 2026-08-31) | The 2026-08-31 twenty-minute stalls. Every registration carries an explicit `timeout`, and slow work is bounded inside the script (`hook-authoring.md`). |
| **A deny stops only its own call.** When one message carries several tool calls, each is gated alone: a PreToolUse deny on one leaves its parallel siblings to run. | EMPIRICAL 2026-09-29: `git status --no-verify` was refused by `block-no-verify.sh` while the sibling Bash call in the same message wrote its marker file; ECC gateguard #3136 reports the same | Put dependent steps in one command (`git add <paths> && git commit`), never in sibling calls: a refused `git add` does not stop a sibling `git commit` of whatever was already staged. |
| 30 hook events exist, including `PostToolUseFailure`, `SubagentStart`, `PreCompact`, `PostCompact` and `WorktreeCreate`. Handler fields beyond `matcher` and `command`: `type`, `if` (a permission-rule gate such as `"Bash(git commit*)"`), `timeout`, `statusMessage`, `once`, and for command hooks `args`, `async`, `asyncRewake`, `shell`. | hooks docs (DOC, 2026-07-18) | Don't flag a real event as undocumented. The `if:` migration is deferred: a mis-scoped `if:` silently disables a gate, so it needs a per-gate proof. `WorktreeCreate` must print the worktree path, so no passive logger belongs there. |
| The PowerShell tool is its own tool: `tool_name` `"PowerShell"`, the command in `tool_input.command`. A hook matched on `Bash` never sees it, so every git gate registers in one `Bash\|PowerShell` group. | EMPIRICAL 2026-09-25: PowerShell tool calls were refused by `validate-push.sh` through the `PowerShell` matcher | The gates read either shell through `_shellwords.py` (`hook-authoring.md`, plan 027). |
| PostToolUseFailure input: `tool_name`, `tool_input`, `tool_use_id`, `error`, and optional `is_interrupt` and `duration_ms`. `is_interrupt` is true when the tool call was aborted. 2.1.241 sends no `is_timeout` or `error_type`, though the event's description names them; whether a timeout sets `is_interrupt` is UNVERIFIED. | EMPIRICAL 2026-09-25: the PostToolUseFailure schema and builder in the 2.1.241 binary | `mark-verification-run.sh` leaves an interrupted build or test unmarked. |
| Not documented, so assume neither way: whether `settings.json`'s `env` block reaches hook processes (`_pybin.sh` probes `TAOM_PYBIN` rather than trusting it), and any `timeout` for `statusLine` (keep `.claude/statusline.sh` cheap). | hooks docs | A per-session logger belongs on `SessionEnd`, not `Stop`, which fires every turn. |

Authoring conventions (mirror a sibling's full convention set, the two-stage git-commit matcher, amend
handling, log rotation, timing the slow path, never `python3`) are in `.claude/rules/hook-authoring.md`,
which loads with any hook file.

## Rule loader (memory) semantics

| Fact | Source |
|---|---|
| A rule with no `paths:` loads at session start. A rule with any `paths:` (even `["**/*"]`) loads when Claude uses Read, Write or Edit on a matching file (Write and Edit since v2.1.288), not on every tool use; from v2.1.293 a single-file `cat`, `head`, `tail`, `sed -n` or `grep` in Bash also loads it. `paths` is the only frontmatter field Claude Code reads; other fields are ignored and stripped. A rule whose YAML fails to parse loads **unconditionally**. | https://code.claude.com/docs/en/memory "Path-specific rules" (DOC, 2026-10-08); CHANGELOG 2.1.288, 2.1.293 |
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

## Tool and session behaviour

| Fact | Source | Why we care |
|---|---|---|
| In an unattended session (`claude -p`, the Agent SDK, CI, a cloud session), a background Bash or PowerShell command stops at its `timeout` (default 30 min, max 2 h) and Claude is told why. An interactive session in the terminal, the desktop app or VS Code has no limit (v2.1.288+; from v2.1.285 to v2.1.287 the limit applied to every session). | https://code.claude.com/docs/en/tools-reference "Time limit for background commands" (DOC, 2026-10-08) | `/review-codex` and `/deep-review` Step 0 pass 7200000 so an unattended Codex run is not cut at 30 min. |
| In the VS Code panel, Stop and Escape end only the current turn; background agents keep running and are stopped one by one from the agent map (v2.1.286). The terminal CLI was not checked. | CHANGELOG 2.1.286 `[VSCode]` (DOC, 2026-10-08); live probe owed | CLAUDE.md "Subagents". |
| The Todo/Task tracking tools are not offered on Opus 5.5 and other newer models (v2.1.268); `CLAUDE_CODE_ENABLE_TODO_TOOLS=1` restores them. | CHANGELOG 2.1.268 (DOC, 2026-10-08) | A skill step that says "use TodoWrite" finds no tool. |

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

## Last verified: 2026-10-08
