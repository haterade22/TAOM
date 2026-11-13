---
paths:
  - ".claude/settings*.json"
  - ".claude/hooks/**"
  - ".claude/skills/**/*.sh"
  - ".claude/skills/freeze/SKILL.md"
  - ".claude/skills/investigate/SKILL.md"
  - ".claude/statusline.sh"
  - "docs/reference/hooks-catalog.md"
  - "tools/test_hooks.sh"
  - "tools/audit_claude_config.py"
---

<!-- Path-scoped (ADR-011). Every fact cites a doc URL or an empirical context. If Claude Code
     changes, update THIS file first. -->

# Claude Code harness facts: hooks and settings (verified)

What a session, a subagent and a compaction load, the rule loader and MEMORY.md: `harness-loading.md`.
Skill and agent frontmatter, models, effort and spawn limits: `harness-skills-agents.md`.

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

## Settings

| Fact | Source |
|---|---|
| Claude Code asks for a `Co-Authored-By: <model>` commit trailer and a PR footer unless `attribution.commit` / `attribution.pr` are `""`; `sessionUrl: false` drops the cloud session link. TAOM sets all three in `.claude/settings.json`; `check-commit-subject-version.sh` stays as the backstop. `includeCoAuthoredBy` is the deprecated form. | https://code.claude.com/docs/en/settings-reference (DOC, 2026-09-23); EMPIRICAL 2026-09-23: the next harness reminder after the edit said to add no attribution |

## Tool and session behaviour

| Fact | Source | Why we care |
|---|---|---|
| `statusLine` renders only in the terminal CLI and the extension's terminal mode; the VS Code panel shows a context indicator and `/usage` instead. | GitHub issues #55643, #77829, #70497 (community reports, not verified here; the docs are silent) | Mike works in the panel: point him to `/usage` for `statusline.sh`'s usage line (#1012). The extension still runs the command (#70497), so keep it cheap. |

## Gitignore blast radius

`git check-ignore -v <path>` is the authoritative test; grepping `.gitignore` is not. Generic patterns
(`bin/`, `obj/`, `tmp/`, `cache/`) match at any depth, which once shipped `check-freeze.sh` untracked
and dead. Name new `.claude/` folders descriptively (`scripts/`, `state/`). (git docs; 2026-04-26
deep-review)

## What this rule changes about how you work

1. **Harness behaviour:** check the facts here and in the two sibling rules first. If a file disagrees
   with what you need, update it first, with a source.
2. **Writing a fact here:** cite a doc URL (DOC-BACKED) or an observation (EMPIRICAL: where, when).
   Unsourced "verified" claims age into wrong ones.
