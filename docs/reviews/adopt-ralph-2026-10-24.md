# ralph-claude-code adoption review (2026-10-24)

Review of [frankbria/ralph-claude-code](https://github.com/frankbria/ralph-claude-code) against
TAOM's AI harness, through `/adopt-external`. Mike asked for a thorough review and said every
recommendation would be implemented; he approved the plan on 2026-10-24. Issue
[#996](https://github.com/haterade22/TAOM/issues/996).

## Coverage

- **Repository:** branch `main` at `4fd2fe94` (MIT, about 165 files, pushed 2026-10-24). Read through
  `gh api` only; nothing was cloned, installed or run. Two read-only agents read the runtime
  (`ralph_loop.sh` in chunks, `lib/circuit_breaker.sh`, `lib/response_analyzer.sh`,
  `lib/file_protection.sh`, `lib/ralphrc.sh`, the sandbox scripts, `install.sh`, the workflows and
  the most transferable tests) and the docs and templates (`templates/*`, `CLAUDE.md`, `README.md`,
  `docs/vision.md`, the user guide, the ADRs, the spec workshop). The main session checked the
  security claims below in `ralph_loop.sh` and `lib/circuit_breaker.sh`.
- **TAOM:** `/improve` (`SKILL.md`, `run-protocol.md`, `execute-and-review.md`, `dispatch-rules.md`,
  `plan-template.md`, `workflows/execute.js`), `working-discipline.md`, the hooks in
  `.claude/settings.json`, `hook-authoring.md`, `tools/test_hooks.sh`, and the memory resume cards.
  Not read: `lib/sandbox_e2b.sh` in full, `ralph_import.sh`, `ralph_enable*.sh`, the generated
  90 KB triage `.lock.yml`.

## What Ralph is

A bash loop that re-runs Claude Code headless. Each loop reads `.ralph/PROMPT.md` (vision and
rules), `fix_plan.md` (a checkbox backlog) and `AGENT.md` (build commands and learnings), does one
task, and ends with a `RALPH_STATUS` block (status, tasks done, files changed, tests, work type,
`EXIT_SIGNAL`, one-line next step). Around the loop:

- **Circuit breaker:** CLOSED, HALF_OPEN, OPEN. It opens after 3 loops with no progress, 5 with the
  same error or 2 with permission denials, and half-opens after a 30 minute cooldown
  (`lib/circuit_breaker.sh:22-26`). Progress is a git change; the model's own `FILES_MODIFIED` or
  `PROGRESS` claim counts only outside a git work tree, "so a stuck model can't hold the breaker
  open by claiming progress".
- **Exit gate:** at least 2 completion indicators and an explicit `EXIT_SIGNAL: true`. Natural
  language "done" alone never exits, after generated docs containing the word ended runs early.
- **Other parts:** a call-per-hour limit, a 15 minute call timeout that counts as progress when git
  shows changes, session resume by id, a 10 MiB log rotation, a GitHub issue queue with a
  deterministic completeness score, Docker and E2B sandboxes, and a tmux monitor.

## Security

- **Safe to learn from:** yes. No obfuscation, decode-then-run, telemetry or piped installer.
  `install.sh` only copies files into `~/.local/bin` and `~/.ralph`; `package.json` has no lifecycle
  scripts.
- **Safe to install:** no, and not needed.
  - `CLAUDE_AUTO_UPDATE` is `true` by default (`ralph_loop.sh:123`), so each start runs a global
    `npm update -g @anthropic-ai/claude-code` of an unpinned package (`:1192`).
  - The default allowlist (`:119`) includes `Bash(npm *)`, which runs any npm script, plus
    `Bash(git push *)` and `Bash(git merge *)`.
  - It does not bypass permissions (`:1646` says so and the command builder agrees).
  - Opt-in only, agent-reported and not re-checked here: the Docker sandbox copies
    `~/.claude/.credentials.json` into the container, the E2B sandbox uploads it to a cloud VM, and
    `glm-review.yml` calls a reusable workflow at `@main` with an API key.
- **Hardening worth knowing:** `.ralphrc` is parsed as data and never sourced; command-bearing keys
  accept only stock values from the file; numeric values pass through `to_int` because bash
  arithmetic evaluates its operands. The last point became A3.

## TAOM's hooks against A3

Every arithmetic operand in `.claude/hooks/*.sh` was traced to its source on 2026-10-24: counts from
`grep -c`, `wc -l` and `find | wc -l`, `date +%s`, exit codes, string lengths, `EPOCHREALTIME`
stripped with `${X//[!0-9]/}`, the length `check-bash-length.sh` gets from Python and strips the same
way, and the MCP health marker, which `mcp-health-check.sh` strips with `tr -cd '0-9'`. None takes raw
input. A3 keeps the
next hook that way; it fixes no defect today.

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | No-progress breaker across iterations | Adopted: `working-discipline.md` (a work-driving loop stops after three ticks with no commit, no change in the working tree and no change to the out-of-repo files it names; the files decide, not the agent's report) and `execute-and-review.md` "Run breaker" (an execute batch with three or more results not DONE, Step 0 checks and maintainer decisions aside, stops the run, recorded as a STOPPED row so a resumed session sees it). TAOM stopped per error and per plan, never across iterations |
| A2 | Dead ends in the resume record (`docs/vision.md` handoff template) | Adopted: `## Dead ends` in the PROGRESS.md template, read at resume, and CLAUDE.md item 8 names dead ends in the memory resume card |
| A3 | Digits only into bash arithmetic (`to_int`) | Adopted: a section in `hook-authoring.md` |
| X1 | Install, auto-update, sandboxes, rate limiting, tmux monitor | Declined: see Security; TAOM runs interactive sessions and `/improve` workflows, not a headless bash loop |
| X2 | `RALPH_STATUS` block, exit scenarios, dual exit gate | Declined: `workflows/execute.js` already returns a schema-checked status (DONE, BLOCKED, PARTIAL) and the plan template carries STOP conditions |
| X3 | `fix_plan.md`, optional sections, `queue.json` with P0 to P9 | Declined: `plans/README.md` and `plan-template.md` are richer (status, depends-on, baseline SHA, issue) |
| X4 | `AGENT.md` learnings file written by the loop | Declined: lessons go to `docs/reviews/lessons/` and commands to `.ai/verification.md`, routed by ADR-011 |
| X5 | GitHub issue import and completeness score | Declined: TAOM's backlog is issue-first through `/improve`, and most open issues wait on in-game checks |
| X6 | SHA-pinned actions, `persist-credentials` test, Dependabot, AI review CI | Declined: TAOM has no `.github/workflows` since 2026-10-05; revisit if CI returns |
| X7 | Compound-command detection for permission denials | Declined: the gates already read each shell segment past wrappers (#985) |
| X8 | Issue numbers on every rule in CLAUDE.md | Declined: `docs/reference/rule-provenance.md` keeps the incidents out of always-loaded text |
| X9 | Specification workshop (Given/When/Then) | Declined: the plan template, `/new-feature` and TDD cover it |
| X10 | "Limit testing to about 20% of effort" | Declined: conflicts with AGENTS.md TDD |
| X11 | A test against bare `!` lines in a `set -e` test script | Declined: `tools/test_hooks.sh` runs `set -uo pipefail` and has no such line |
