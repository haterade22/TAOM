# Project Manager

## Overview

One AI session, the manager, runs the project between the maintainer's decisions. It reads the
goals, takes in new notes, checks returned work, runs owed in-game checks, selects the next issues,
dispatches them to `/improve` executors, opens pull requests and writes one digest. The decision
and its ladder are [ADR-015](../adrs/015-self-managing-project.md). The procedure is the
`/manage` skill.

The stage in force is the Status line of ADR-015, and nowhere else. At stage 0 the manager opens
pull requests and the maintainer merges them.

## Why This Exists

- **Before:** the repo had workers (`/improve`, `/verify`, `/game-run`) and no manager. The
  maintainer chose each task, started each session and gave each word to push or merge. The
  priority list was his machine-local memory file.
- **The limit on going further now:** a self-merge gate on the desktop can be passed without real
  evidence (ADR-015 "Context" lists the five reasons). So the manager first earns a record with
  the maintainer as the only merger.

## The maintainer's part

| What | Where | How often |
|---|---|---|
| Order the goals, write "Done when" | [docs/goals.md](../goals.md) | when priorities change |
| Drop player feedback and play notes, raw | a comment on the pinned issue "Manager inbox" | any time |
| Answer decisions by issue number ("#1234 A, #1250 B") | a comment on the pinned issue "Manager digest" | weekly |
| Reject a manager pull request | close it without a merge; its issue is then blocked until you say otherwise | any time |
| Merge what is ready | the digest's "Ready to merge" list | weekly, or when it suits |
| Play what a log cannot prove | the digest's "What a person must play" list | when it suits |
| Stop the manager | create the file `taom-manager/STOP` in the git common folder | when needed |

Find the two pinned issues with `gh issue list --search "Manager in:title" --state open`.

## Setup, once

None of this exists on 2026-11-18. A tick stops at its first step until all of it does. The
manager creates it after the maintainer has ordered `docs/goals.md` and answered the backlog sort:

1. The `manager` label: `gh label create manager --description "Opened by the manager session"`.
2. One milestone per row of `docs/goals.md`, named as the row's "Milestone" cell.
3. Every open issue assigned to its milestone from the sort answer.
4. Two issues, "Manager inbox" and "Manager digest", pinned (`gh issue pin <n>`).
5. The DRAFT status line removed from `docs/goals.md` (the maintainer does this).

## State on the desktop

The folder `taom-manager` in the git common folder (`git rev-parse --git-common-dir`), shared by
every worktree: `STOP` (its presence stops every tick) and `ticks.log` (one line per worker run,
`<YYYY-MM-DD> <issue> dispatched` or `<YYYY-MM-DD> <issue> fix`, which counts the daily limit).

Everything else the manager must remember lives on GitHub, so a new session can read it: a
`rocket` reaction from the maintainer's login marks an inbox note or a digest answer as handled;
`Fix round <n>` comments count a pull request's fix rounds; the label `triage-blocked-decision`
on an issue keeps a rejected or stopped item out of the selection.

## How it works

| Piece | What it does |
|---|---|
| `/manage tick` | One pass: breakers, intake, returned work, owed in-game checks, select, dispatch, pull requests, digest. `--dry` prints the selection and changes nothing. |
| `/manage digest` | Rewrites the digest issue's body; once a week also posts it as a comment. |
| [docs/goals.md](../goals.md) | The priority order. One row is one GitHub milestone. An issue with no milestone is never selected. |
| `/improve` | Plans, executes in worktrees, spec-checks and reviews each selected issue (its phases 5 to 9). |
| `/game-run`, `tools/ingame_verdict.py` | Run and judge owed in-game checks on the trunk build when the PC is idle. |
| `tools/work_claims.py` | The `manager-tick` claim keeps two ticks apart; each dispatched issue is claimed. |
| The `manager` label | Marks a pull request the manager opened. |
| `check-subagent-verification.sh` | Blocks a builder subagent once when it ends with edited C# and no build or test since ([hooks catalog](../reference/hooks-catalog.md)). |

A manager pull request's body carries the line `Ready at: <sha>` when its checks passed. A merge
at that commit counts as "merged unchanged".

### The ladder counts

The manager never reports its own record. These queries do:

```bash
# Manager pull requests merged, with the commit each was merged at and its body
gh pr list --state merged --label manager --limit 200 --json number,mergedAt,headRefOid,body
# Reverts of merged pull requests; one counts when its title or body names a manager pull request
gh pr list --state merged --search "Revert in:title" --limit 50 --json number,title,body,mergedAt
```

A merged pull request counts as unchanged when its `headRefOid` equals the `Ready at:` commit in
its body. The days of a stage count from the first manager pull request merged in it.

## Limits

- **Stage 0 only.** The manager never merges, tags, releases or pushes to a trunk.
- **One login.** The manager posts as the maintainer, so its comments start with
  `<!-- manager -->` and it never comments on the inbox. A separate identity comes with the
  self-merge amendment.
- **Started by hand.** Nothing starts a tick yet, and the maintainer's start is the word
  `.ai/policy.md` requires for issues, pushes and game runs. An unattended start at idle is the
  next phase (ADR-015 "Migration Strategy"); it needs that policy text amended first.
- **Game-bound.** The build, most tests and every in-game check need the desktop. A check with no
  manifest or no driver stays a person's.
- **The checkpoint hook is inert until registered** in `.claude/settings.json`, which is the
  maintainer's file. It sees C# edited through Edit and Write only.
- **Cost.** A dispatched issue runs a plan writer, an executor and its reviewers. The limits are
  four agents in flight, six open manager pull requests and eight worker runs a day.

## Key Files

| File | Purpose |
|------|---------|
| `docs/adrs/015-self-managing-project.md` | The decision, the ladder, the self-merge conditions |
| `docs/goals.md` | Goals in priority order (the maintainer's file) |
| `.claude/skills/manage/SKILL.md` | The tick and the digest |
| `.claude/hooks/check-subagent-verification.sh` | The SubagentStop checkpoint |
| `.claude/skills/improve/SKILL.md` | The execution engine the tick dispatches to |

## GitHub Issue

#1449

## Testing

- `bash tools/test_hooks.sh` (the checkpoint hook's cases; section 3b parses the skill's frontmatter)
- `python tools/lint_docs.py --fail-on-drift`
- `/manage tick --dry`: the printed selection follows the order of `docs/goals.md` and skips every
  claimed, blocked and assigned issue.

## How to verify in game

Not applicable: this is harness tooling for AI sessions and changes nothing the game loads.
