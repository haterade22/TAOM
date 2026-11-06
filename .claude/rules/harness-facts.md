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
---

<!-- Path-scoped (ADR-011). Every fact cites a doc URL or an empirical context. If Claude Code
     changes, update THIS file first. -->

# Claude Code harness facts (verified)

## Context loading

| Fact | Source |
|---|---|
| CLAUDE.md should stay under 200 lines ("longer files consume more context and reduce adherence"). An `@path` import loads at launch (up to four hops), so it organises but saves nothing. Claude Code reads AGENTS.md natively (v2.1.277+) only when no CLAUDE.md, `.claude/CLAUDE.md` or CLAUDE.local.md exists at or above the working directory, so CLAUDE.md keeps its `@AGENTS.md` import. Installed version: read it from a transcript's version field; do not record it here. | https://code.claude.com/docs/en/memory "AGENTS.md" (DOC, 2026-10-08) |
| Custom and general-purpose subagents load the CLAUDE.md hierarchy, imports and unscoped rules; Explore and Plan skip them and the git status, but path-scoped rules and nested CLAUDE.md still load when they touch matching files. `omitClaudeMd: true` (v2.1.271+) opts a custom agent out; no TAOM agent sets it. The docs say no subagent loads the main session's auto memory, but the transcripts disagree: `MEMORY.md` is in the `instructions` file list and the rendered text of every sampled subagent spawn that had that record. Cause unknown, but not the teams flag: at 0, at 1 and unset the list names `MEMORY.md` and `SendMessage` is offered, so the flag left the settings ([retro](../../docs/reviews/harness-retro-2026-11-03.md)). Documented inheritor: a fork (the whole parent conversation; default on in VS Code from 2.1.302). | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-23); EMPIRICAL 2026-11-02, subagent transcripts of `e--repos-TAOM` |
| On a native 1M model auto-compaction fires at about 967K. `/autocompact 400k` (saved per model) or `autoCompactWindow` lowers it. A subagent inherits the window; `autoCompactWindow` in its frontmatter can only lower it further. Idle compaction (`idleCompaction`, default true) compacts a conversation of 200K tokens or more after about 54 minutes idle. It reaches the VS Code panel in v2.1.314; `harness_retro.py` section 11 measures the rewrites after an idle hour. | model-config, sub-agents, settings-reference#idlecompaction, changelog 2.1.299 and 2.1.314 (DOC, 2026-11-03); `harness_retro.py` section 11 (EMPIRICAL) |
| Explore runs on the main conversation's model (v2.1.198+; Opus 5.5 here), not Haiku. `CLAUDE_CODE_SUBAGENT_MODEL` alone does not change Explore or Plan; a spawn's `model` parameter does, and so would a project agent named `Explore`, which overrides the built-in. | https://code.claude.com/docs/en/sub-agents "Built-in subagents", "Choose a model" (DOC, 2026-10-08); CHANGELOG 2.1.198 |
| After `/compact`, CLAUDE.md, unscoped rules, auto memory, the plan file and a fresh git status are re-injected; up to five recent files are re-read; each invoked skill's body returns capped at 5,000 tokens (25,000 total, oldest dropped); path rules return on the next matching read; SessionStart hooks matching the `compact` source run and their output is added. The skill-description listing is rebuilt and re-sent from v2.1.304; before that it did not return. A re-read file over 5,000 tokens comes back as a path reference, not its content. | https://code.claude.com/docs/en/context-window#what-survives-compaction (DOC, 2026-11-03); changelog 2.1.304 |
| MCP tool schemas are deferred behind tool search; only the tool names load at startup. | context-window docs (DOC, 2026-09-23); this session's deferred-tool list (EMPIRICAL, 2026-09-23) |

## Skill load semantics

| Fact | Source | Why we care |
|---|---|---|
| Skill **descriptions** load eagerly unless the skill sets `disable-model-invocation: true`; **bodies** load only when invoked. | https://code.claude.com/docs/en/skills (DOC, 2026-04-26) | Count frontmatter, not bodies, for the eager total. |
| Consumed frontmatter (the skill allowlist): `name`, `description`, `when_to_use`, `argument-hint`, `arguments`, `disable-model-invocation`, `user-invocable`, `allowed-tools`, `disallowed-tools`, `model`, `effort`, `context` (`fork`), `agent`, `background`, `hooks`, `paths`, `shell`, `metadata`, `license`, `compatibility`. An unrecognised key is silently ignored, so a typo turns a setting off with no error. | https://code.claude.com/docs/en/claude-directory#frontmatter-fields-by-file (DOC, 2026-11-03, complete) | `triggers:` is not a field (a gstack preamble relic): move its phrases into `description`. `tools/test_hooks.sh` 3b fails a skill key off this list and a `name` that differs from the directory; a documented key missing here goes into 3b's `SKILL_KEYS` and this row. |
| A TAOM skill description stays at most 30 words: our own cap (the harness cap is 1,536 characters). | `tools/context_budget_scan.sh` flag (report only); settings-reference skillListing keys (DOC, 2026-11-03) | Description creep came back twice on `/freeze` and `/investigate`. |
| `disable-model-invocation: true` makes a skill user-only; when Claude tries to invoke one, it is told to ask the user to run it (v2.1.222). | skills docs (DOC); CHANGELOG 2.1.222 (DOC, 2026-10-08) | No TAOM skill sets it; the "never auto-invoke" row in CLAUDE.md is text only. |
| A `context: fork` skill runs in the background by default (v2.1.218; `background: false` opts out). A project skill named `verify` is offered to Claude to run right before each commit except docs-only and tests-only ones (v2.1.286). | CHANGELOG 2.1.218, 2.1.286 (DOC, 2026-10-08) | `/verify` is TAOM's; the harness nudge and `check-verification-evidence.sh` point the same way. |

## Agent (subagent) load semantics

| Fact | Source | Why we care |
|---|---|---|
| Agent descriptions take context wherever Claude chooses an agent (warning past 15,000 tokens combined, every agent still loads); an agent's **body** loads only when it runs. | sub-agents docs, "Understand automatic delegation" (DOC, 2026-11-03) | Same eager/lazy split as skills. |
| `model:` takes an alias (`sonnet`, `opus`, `haiku`, `fable`), a full id or `inherit`; `effort:` takes `low` to `max`, per model. Resolution: the spawn's `model` parameter, then the definition's `model:`, then `CLAUDE_CODE_SUBAGENT_MODEL`, then the main model. | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-18) | `deep-reviewer` pins Opus 5.5 (`claude-opus-5-5`, Mike 2026-09-23) at max effort; passing `model` on the spawn silently overrides that, so `/deep-review` never passes one. |
| Agent frontmatter (the agent allowlist): `name`, `description`, `tools`, `disallowedTools`, `model`, `permissionMode`, `maxTurns`, `autoCompactWindow`, `skills`, `mcpServers`, `hooks`, `memory`, `background`, `omitClaudeMd`, `effort`, `isolation`, `color`, `initialPrompt`, `experimental`. An unrecognised key is silently ignored; `claude --debug` names an unrecognised agent field (v2.1.296). | https://code.claude.com/docs/en/claude-directory#frontmatter-fields-by-file (DOC, 2026-11-03, complete) | `tools/test_hooks.sh` 3b fails an agent key off this list and a `name` that differs from the file stem; a documented key missing here goes into 3b's `AGENT_KEYS` and this row. |
| The Agent tool takes an `effort` parameter (v2.1.292). It overrides a definition's `effort:`, which overrides the session level; `CLAUDE_CODE_EFFORT_LEVEL` beats both. | CHANGELOG 2.1.292; sub-agents docs, "Choose an effort level" (DOC, 2026-11-02); EMPIRICAL 2026-11-02: `deep-reviewer` spawns with `effort: high` ran at `high` in their transcripts | `/deep-review` passes `high` on tier 2 and second-wave lenses. |
| Subagents start in the background by default (terminal; VS Code and desktop always from v2.1.302). Fork mode (`subagent_type` `fork`, `CLAUDE_CODE_FORK_SUBAGENT`; on in the terminal from v2.1.232 and in VS Code from v2.1.302) lets Claude start a subagent that inherits the whole parent conversation at the main model, so it escapes the tier rule; `Agent(fork)` in `permissions.deny` or `CLAUDE_CODE_FORK_SUBAGENT=0` turns it off. A subagent may spawn its own, up to `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` layers below the main conversation (default 3 since v2.1.219; TAOM sets 1, so nesting is off). At most `CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS` run at once (default 20, v2.1.217); past it the Agent tool **refuses** the spawn rather than queueing it. | https://code.claude.com/docs/en/env-vars, sub-agents "Let subagents spawn their own subagents" (DOC, 2026-10-08) | A cap below `/deep-review`'s seven to nine parallel lenses would make it fail, so TAOM sets none. |

## Hook lifecycle

| Fact | Source | Why we care |
|---|---|---|
| Hooks in `settings.json` are global. A skill's hooks register when the skill is invoked and keep running for the rest of the session, so `/investigate`'s freeze hook outlives the investigation until `/unfreeze`; nothing registers before the first invocation, so a state file written from elsewhere is inert. `/investigate` deliberately re-declares `/freeze`'s hook. | https://code.claude.com/docs/en/hooks (DOC, 2026-11-03) | Copy an inline hook to another skill only with a stated reason. |
| **Visibility.** Plain stdout reaches Claude only for `SessionStart`, `UserPromptSubmit`, `UserPromptExpansion` and `PostModelSwitch`; for every other event it goes to the debug log. Stderr from a hook that exits 0 never reaches Claude. A PreToolUse `deny` reason and exit-2 stderr do; an `ask` reason is shown to the user, not to Claude. A Stop hook reaches Claude through JSON `{"decision":"block","reason":"..."}` on stdout, or exit 2: Claude answers the reason in one more response, the next Stop payload carries `stop_hook_active: true`, and the harness ends the turn after 8 consecutive continuations (the count resets when Claude calls a tool; `CLAUDE_CODE_STOP_HOOK_BLOCK_CAP` raises it). PreToolUse, PostToolUse and PostToolUseFailure take `{"hookSpecificOutput":{"hookEventName":"<event>","additionalContext":"..."}}`, which lands next to the tool result; PostToolUse exit 2 also shows stderr to Claude. `systemMessage` goes to the user. | hooks docs, "Exit code 0", "Stop decision control" (DOC, 2026-09-23) and "Add context for Claude" (DOC, 2026-09-29); EMPIRICAL 2026-09-23 and 2026-09-29 (`tools/test_hooks.sh` 7k) | A reminder printed anywhere else is silent: make it a gate, or move it to a visible channel. The Stop reminders use `.claude/hooks/_stop_reminder.sh` (`tools/test_hooks.sh` 7a). |
| **PreToolUse output contract.** Tool-input JSON arrives on stdin. Print `{}` to allow; to block or confirm, print `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}}` (or `"ask"`), or exit 2 with the reason on stderr. A top-level `permissionDecision` is **ignored**. Other events keep a top-level `decision`. Malformed JSON fails open. | hooks docs, "PreToolUse decision control" (DOC, 2026-09-23); EMPIRICAL 2026-09-23 (#647) | `tools/test_hooks.sh` 5c fails on the top-level form; matching the text `"permissionDecision":"deny"` passes both forms, which is how the dead gates stayed green. Escape `\` and `"` in any interpolated path. |
| **An `ask` prompts even in bypass mode.** A PreToolUse `ask` shows the user an approval prompt although the session runs with `bypassPermissions`, and the tool call waits until someone answers. A headless `claude -p` run did not wait: the agent saw the ask's reason and the command did not run. | EMPIRICAL 2026-09-26; hooks docs "PreToolUse decision control" (DOC, 2026-10-08) | The confirm gates match dangerous git text anywhere in a command, quoted text included, so an unattended agent keeps such text out of its shell commands: payloads go in files, scripts run by path. |
| **TAOM hooks fail open.** A hook's own bug never blocks: swallow internal errors, and non-deny hooks exit 0. A deny is only ever a deliberate gate decision. The harness is stricter on its own side: since v2.1.288 a PreToolUse or PermissionRequest hook whose matching fails, or whose tool input will not serialize to JSON, blocks the call. | this repo's design; CHANGELOG 2.1.288 (DOC, 2026-10-08) | `/security-scan` deliberately does not flag `\|\| true`, `2>/dev/null` or `exit 0`. |
| All hooks matching an event run in **parallel**. An omitted `timeout` defaults to **600 s** (`UserPromptSubmit` 30 s, `SessionEnd` a 1.5 s shared budget). A timed-out hook is **killed and its output discarded**, so a PreToolUse gate then fails open. `async: true` hooks are exempt from timeouts and cannot gate. | hooks docs (DOC, 2026-08-31) | The 2026-08-31 twenty-minute stalls. Every registration carries an explicit `timeout`, and slow work is bounded inside the script (`hook-authoring.md`). |
| **A deny stops only its own call.** When one message carries several tool calls, each is gated alone: a PreToolUse deny on one leaves its parallel siblings to run. | EMPIRICAL 2026-09-29: `git status --no-verify` was refused by `block-no-verify.sh` while the sibling Bash call in the same message wrote its marker file | Put dependent steps in one command (`git add <paths> && git commit`), never in sibling calls: a refused `git add` does not stop a sibling `git commit` of whatever was already staged. |
| The hooks docs event table lists every event, including `PostToolUseFailure`, `SubagentStart`, `PreCompact`, `PostCompact` and `WorktreeCreate`. Handler fields beyond `matcher` and `command`: `type`, `if` (a permission-rule gate such as `"Bash(git commit*)"`), `timeout`, `statusMessage`, `once` (skill frontmatter only), `onFailure` (v2.1.295: block on a crash, timeout or odd exit), and for command hooks `args`, `async`, `asyncRewake`, `shell`. | hooks docs (DOC, 2026-11-03) | Don't flag a real event as undocumented. The `if:` migration is deferred: a mis-scoped `if:` silently disables a gate, so it needs a per-gate proof. `WorktreeCreate` must print the worktree path, so no passive logger belongs there. |
| The PowerShell tool is its own tool: `tool_name` `"PowerShell"`, the command in `tool_input.command`. A hook matched on `Bash` never sees it, so every git gate registers in one `Bash\|PowerShell` group. Shell commands started by Monitor reach a Bash matcher from v2.1.314; such hooks can block or ask, but not approve or rewrite. | EMPIRICAL 2026-09-25: `validate-push.sh` refused PowerShell calls through that matcher; changelog 2.1.314 (DOC, 2026-11-03) | The gates read either shell through `_shellwords.py` (`hook-authoring.md`, plan 027). |
| Every hook payload carries `cwd`, "the worktree root after Claude enters a worktree, and the new directory after Claude runs `cd`", while `$CLAUDE_PROJECT_DIR` "stays put" at the directory the session started in. So for a session that started in the main checkout and entered a worktree, a registration anchored on `$CLAUDE_PROJECT_DIR` still runs the main checkout's copy of a hook, and a hook that judges a git call must read that call's own repository (`git -C`, a `cd`, then `cwd`, resolved to the work tree's top level), not `$CLAUDE_PROJECT_DIR`. | https://code.claude.com/docs/en/hooks "Common input fields" (DOC, 2026-10-16); EMPIRICAL 2026-10-16: after `EnterWorktree`, `check-claude-files-tracked.sh` still judged the main checkout | Every git gate that reads the repository takes it from `_pybin.sh` `taom_git_repo_dir` over `_shellwords.py git-repo`; `tools/test_hooks.sh` 7b. |
| PostToolUseFailure input: `tool_name`, `tool_input`, `tool_use_id`, `error`, and optional `is_interrupt` and `duration_ms`. `is_interrupt` is true when the tool call was aborted. 2.1.241 sends no `is_timeout` or `error_type`, though the event's description names them; whether a timeout sets `is_interrupt` is UNVERIFIED. Cancelling a running tool does not fire the hook. | hooks docs, PostToolUseFailure input (DOC, 2026-11-03); the 2.1.241 fact: EMPIRICAL 2026-09-25, its binary | `mark-verification-run.sh` leaves an interrupted build or test unmarked. |
| The `env` block should reach hooks, which inherit the session's environment; `_pybin.sh` still probes `TAOM_PYBIN`. The status line has no documented timeout, so keep `.claude/statusline.sh` cheap. | hooks docs, settings-reference, statusline docs (DOC, 2026-11-03) | A per-session logger belongs on `SessionEnd`, not `Stop`, which fires every turn. |

Authoring conventions are in `.claude/rules/hook-authoring.md`.

## Rule loader (memory) semantics

| Fact | Source |
|---|---|
| A rule with no `paths:` loads at session start. A rule with any `paths:` (even `["**/*"]`) loads when Claude uses Read, Write or Edit on a matching file (Write and Edit since v2.1.288), not on every tool use; from v2.1.293 a single-file `cat`, `head`, `tail`, `sed -n` or `grep` in Bash also loads it. `paths` is the only frontmatter field Claude Code reads; other fields are ignored and stripped. A rule whose YAML fails to parse loads **unconditionally**. Patterns are relative to the directory holding the rule's `.claude` folder, so a rule in this checkout does not load for files in a sibling worktree such as `E:/repos/taom-*` unless Claude starts there or runs `/cd`. | https://code.claude.com/docs/en/memory "Path-specific rules" (DOC, 2026-11-03); CHANGELOG 2.1.288, 2.1.293 |
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
| `statusLine` renders only in the terminal CLI and the extension's terminal mode; the VS Code panel shows a context indicator and `/usage` instead. | GitHub issues #55643, #77829, #70497 (community reports, not verified here; the docs are silent) | Mike works in the panel: point him to `/usage` for `statusline.sh`'s usage line (#1012). The extension still runs the command (#70497), so keep it cheap. |
| In an unattended session (`claude -p`, the Agent SDK, CI, a cloud session), a background Bash or PowerShell command stops at its `timeout` (default 30 min, max 2 h) and Claude is told why. An interactive session in the terminal, the desktop app or VS Code has no limit (v2.1.288+; from v2.1.285 to v2.1.287 the limit applied to every session). | https://code.claude.com/docs/en/tools-reference "Time limit for background commands" (DOC, 2026-10-08) | `/review-codex` and `/deep-review` Step 0 pass 7200000 so an unattended Codex run is not cut at 30 min. |
| In the VS Code panel, Stop and Escape end only the current turn; background agents keep running and are stopped one by one from the agent map (v2.1.286). The terminal CLI was not checked. | CHANGELOG 2.1.286 `[VSCode]` (DOC, 2026-10-08); live probe owed | CLAUDE.md "Subagents". |

## Gitignore blast radius

`git check-ignore -v <path>` is the authoritative test; grepping `.gitignore` is not. Generic patterns
(`bin/`, `obj/`, `tmp/`, `cache/`) match at any depth, which once shipped `check-freeze.sh` untracked
and dead. Name new `.claude/` folders descriptively (`scripts/`, `state/`). (git docs; 2026-04-26
deep-review)

## What this rule changes about how you work

1. **Load behaviour:** check the facts here first. If this file disagrees with what you need, update
   it first, with a source.
2. **Writing a fact here:** cite a doc URL (DOC-BACKED) or an observation (EMPIRICAL: where, when).
   Unsourced "verified" claims age into wrong ones.

Parallel agents that may edit single-owner files: see `docs/ai-includes/agent-teams.md` "Worktree
isolation for parallel agent runs", which also says when the harness refuses `isolation: "worktree"`.
