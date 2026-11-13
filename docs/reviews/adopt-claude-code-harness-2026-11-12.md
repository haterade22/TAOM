# claude-code-harness adoption review (2026-11-12)

Review of [Chachamaru127/claude-code-harness](https://github.com/Chachamaru127/claude-code-harness)
against TAOM's AI harness, through `/adopt-external`, in the same session as the Harness² delta
review ([record](adopt-harness2-2026-10-21.md#delta-review-2026-11-12)). Mike asked how the harness
could be more effective, efficient, looped and automatic, then on 2026-11-12 approved every Tier 1
and Tier 2 item on a branch to merge. Issue [#1138](https://github.com/haterade22/TAOM/issues/1138).

## Coverage

- **Repository:** `main` at `aeb665e8` (2026-11-11), plugin version 5.16.0, MIT. Read through a
  blobless clone in the session scratchpad; nothing was installed or run. Read in full: `README.md`,
  `.claude-plugin/plugin.json`, `.claude-plugin/settings.json`, every hook registration in
  `.claude-plugin/hooks.json` (by a parse, plus the full text of the dispatch command and the three
  agent hooks), `skills/harness-loop/SKILL.md`, `skills/failure-codifier/SKILL.md`,
  `agents/advisor.md`, `go/internal/hookhandler/file_lease_hook.go`, the head of
  `scripts/detect-review-plateau.sh`, `scripts/hook-handlers/webhook-notify.sh` (head),
  `monitors/monitors.json`, and the rules `autonomous-confirmation-scope.md`, `self-audit.md` and
  `claude-5-prompt-standard.md`. `benchmarks/breezing-bench/agent-eval/REPORT.md` was read by its
  headings and conclusions. The Go sources were searched for outbound hosts, secret reads and shell
  calls, not read whole.
- **TAOM:** `.claude/settings.json`, `check-verification-evidence.sh`, `mark-verification-run.sh`,
  `config-protection.sh`, the `/improve` review workflow, `tools/loop_progress.py`, the three retro
  records, and the installed Claude Code binary (2.1.322) for one environment variable.

## What it is

A plan, work, review and release workflow for solo developers, in Japanese and English. It keeps a
`spec.md` and a `Plans.md`, runs workers in worktrees with a separate reviewer, and runs long work
through `/loop` and `ScheduleWakeup` (`harness-loop`, eight cycles by default). Its 71 hook handlers
on 27 events are 68 command hooks, each calling a prebuilt Go binary (`bin/harness`), and 3 agent
hooks that start Haiku 5.5 on every Write and Edit (before and after) and before a compaction. It
also targets Codex, Cursor, Grok and OpenCode.

## Security

- **Safe to learn from:** yes. `python tools/audit_claude_config.py --root <clone> --external`
  scanned 1,785 files: CRITICAL 3, HIGH 155, MED 170, INFO 7. The three CRITICAL rows are one
  "bad pattern" example in a docs table (`skills/harness-review/references/security-profile.md:257`
  and its two mirrors), read at source. Most HIGH rows are tests and docs that quote attack strings
  (`tests/test-3cli-hook-floor.sh`), archived plans that name the permission-skip flag, and
  workspace cleanup. The Go code names no outbound host but `localhost` (its optional memory
  service, port 37888 by default) and a JSON schema URL. The webhook script posts only when
  `HARNESS_WEBHOOK_URL` is set, and no registration calls it.
- **Safe to install:** no. The four prebuilt 13 to 14 MB binaries run on every hook event and cannot
  be reviewed as text. The installers pipe `curl` into `bash`. The Codex setup replaces user-level
  `worker.toml` and `reviewer.toml`. Its settings deny edits to `.claude/settings*`, which conflicts
  with TAOM's own setup.
- **A trap in our own procedure:** Git Bash rewrote the sparse-checkout pattern `!/bin/` as
  `!C:/Program Files/Git/usr/bin/`, so the exclusion matched nothing and the binaries landed in the
  scratch clone. They were not run and were deleted. `MSYS_NO_PATHCONV=1` keeps the patterns as
  written (re-run on the same clone; `git sparse-checkout list` then shows `!/bin/`). The gotcha is
  in [external-repo-adoption.md](../ai-includes/external-repo-adoption.md).
- **Secrets in the session environment:** `ELEVENLABS_API_KEY` is in Mike's shell environment, so
  every hook, Bash call, MCP server and subagent can read it. It must stay there: `.mcp.json` passes
  it to the ElevenLabs MCP server. Claude Code 2.1.322 holds a `CLAUDE_CODE_SUBPROCESS_ENV_SCRUB`
  variable; one string in its binary ties it to a subprocess sandbox pinned to the launch
  directory, and which variables it removes is not verified. Options were a launcher that reads the
  key from a file outside the repo, or a measured one-session trial of the variable. Mike chose to
  record the exposure only (2026-11-12): a small win against a speculative case.

## Already in TAOM

| claude-code-harness | TAOM |
|---|---|
| Plan, work, review, release | `/new-feature`, `/improve`, `/deep-review`, `/ship`, `/release` |
| `Plans.md` and `--resume latest` | `plans/`, `PROGRESS.md`, memory resume cards |
| Worker in a worktree, lead review, bounded fix loop | `/improve` execute and review workflows (`MAX_ROUNDS` 2) |
| Review plateau (Jaccard over the last three rounds of one task) | Not needed: `/improve` stops after two convergence rounds, and the check needs three |
| `failure-codifier` (count 3 or more is medium, 5 is high; proposals only) | `/harness-retro` (signals in three or more sessions, proposals, one-at-a-time approval) |
| `autonomous-confirmation-scope.md` | `think-before-coding.md`, the standing word of 2026-11-12 |
| `self-audit.md` (hooks injected into `settings.local.json`) | `/security-scan` reads `settings.local.json` |
| The breezing benchmark: an explicit validate step, 3/15 to 14/15 passes on one model | `/verify` and the Stop reminder already mandate it |
| PreCompact warning about work in progress | `session-start.sh` re-surfaces recent dead ends after a compaction (`tools/test_hooks.sh` 7n) |

## What TAOM took

- **A per-session edit ledger** (`file_lease_hook.go`): a PostToolUse Edit and Write hook records, by
  the payload's `session_id`, which files a session changed. TAOM uses it to make
  `check-verification-evidence.sh` judge the session instead of the tree (retro proposal P1 of
  [2026-11-10](harness-retro-2026-11-10.md)), and to tell a session once when another session in
  the same checkout edited the same file in the last 24 hours. The lease's PreToolUse half and its
  deny were not taken: TAOM only informs.
- **An advisor consult on a fixed trigger** (`agents/advisor.md`, `harness-loop` "Advisor
  Strategy"): in an unattended run, a second failure with the same cause gets one read-only
  `architect` consult that answers PLAN, CORRECTION or STOP. The fixed trigger replaces a judgement
  of low confidence, and the advice grants no authorization. The trigger is one clause in
  `working-discipline.md`, and the consult contract is in the body of `.claude/agents/architect.md`,
  which loads only when the agent runs: the always-on rules had 14 B of room under their 16,384 B
  cap, so three filler words and a `--help` pointer were cut, with no rule lost. No new agent.
- **Two measurements in `tools/harness_retro.py`**, so the next retro can judge two ideas before
  anything is built for them: files that two sessions edited (the collision rate behind the lease's
  deny), and tool errors in the 20 results after a compaction against the 20 before it (the case
  for a PostCompact hook).

## Skipped

| Item | Why |
|---|---|
| Install the plugin, its Go guardrail engine or its binaries | Not reviewable, while TAOM's hooks are shell and Python text |
| Haiku agent hooks on every Write and Edit | A model call per edit; `/deep-review` gates at commit, and `deep-reviewer` was already 33.9% of spend in the 11-10 retro |
| Sandbox network deny list (paste sites, metadata endpoints) | TAOM runs no sandbox on Windows; tied to the declined secrets item |
| `harness-accept`, `harness-sync`, `breezing`, Cursor, Grok and OpenCode routes | Covered by `/verify` and `/improve`, or out of TAOM's tools |
| Formal models (`formal/quint`) | No TAOM need |
| `claude-5-prompt-standard.md` | Its kept rules (fixed schema names, numeric limits) are TAOM practice; no gap found |

## Build and review (2026-11-12 to 2026-11-13)

Two `implementer` agents built the hooks and the retro measures from one pinned design. Two review
rounds followed, each a set of `deep-reviewer` lenses with independent `architect` skeptics on every
finding.

- **First round:** 34 findings (20 on the hooks, 14 on the miner). Skeptics refuted 2 and split on 2.
  The two that changed the design: git 2.55 matches no `:(icase)` pathspec two or more folders inside
  an untracked folder, so the session's own new feature files went unseen (the hook now passes a
  `:(literal)` pathspec as well); and C# changed through Bash or PowerShell never reaches a ledger,
  which the tree-wide check had caught (the transcripts of the last 14 days hold `sed -i` edits of
  TAOM C#, such as `DefenderSplit.cs` on 2026-11-12, and a `git checkout --theirs` of C# tests). The Stop hook now also reminds for dirty C# in
  the payload's work tree that no session's ledger holds. That check is TAOM's own; the lease has no
  counterpart.
- **Re-review of the fixes:** 15 findings, all upheld. The largest: the new check shared the own-edit
  streak marker, so one dismissed reminder muted the session's own later edits (it now has
  `<id>.tree.reminded`); and a session that never built counted unclaimed C# of any age (its baseline
  is now the last build of any session, the rule before #1138).
- **Kept as they were:** the no-session-id fallback (it also runs when the helper fails to load) and
  the two note templates (the skeptics split on both). Stated as limits instead of code: a claim has
  no age, so a file another session edited in the last 7 days stays exempt; and the ledger folder is
  per launch checkout, so the cross-session note does not see a session launched in a sibling
  worktree.
- **Tests:** every new hook row was run against the first-round hooks and against 17 mutants, one per
  guarded behaviour, and each mutant fails at least one row. The retro miner's 133 tests pass.

## Decisions

Mike approved every Tier 1 and Tier 2 item on 2026-11-12, the one `settings.json` registration
through `config-protection.sh`'s session override, and recording the key exposure without code.
