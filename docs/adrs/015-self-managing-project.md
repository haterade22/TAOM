# ADR-015: A Manager Session Runs the Project, and Earns Merge Rights in Stages

**Status**: Accepted (2026-11-18, the maintainer approved the plan). Stage 0 is in force. Stages 1
and 2 wait for the amendment named under "Self-merge".

**Date**: 2026-11-18

**Priority**: Standard

## Context

The maintainer wants the repository to manage itself: one AI session that selects work, splits it,
starts workers, checks what returns and lands it, while he gives goals, milestones, taste and what
he hears from players.

Read on 2026-11-18:

- The worker parts exist: `/improve` (plans, worktree executors, spec check, tiered
  `/deep-review`), `/verify`, `/game-run` with `tools/ingame_verdict.py`, `tools/work_claims.py`,
  `tools/loop_progress.py`.
- The manager parts do not. There is no written goal, no GitHub milestone, no priority signal on
  the 279 open issues, no intake for player feedback and nothing that starts a run. The working
  priority list is the maintainer's machine-local memory file.
- Every landing step waits for him. Merge, push, issue creation, game runs and paid calls need his
  word (`.claude/skills/improve/SKILL.md` "Authority"). He merged each of the last 15 pull
  requests himself, with no review and no check on any of them.
- There is no machine bar. The GitHub workflows were removed on 2026-10-05. The trunk ruleset
  requires a pull request with no approval and no status check, and repository roles 4 and 5
  bypass it (which role id 4 is, is unconfirmed).
  Update 2026-11-22: #1543 added advisory PR checks (a no-game build and tests, the tool tests,
  the doc lint); see [pr-review-automation.md](../features/pr-review-automation.md). They are not
  the bar Alternative 2 rejects: the ruleset still requires no status check.
  `.ai/policy.md` says a trusted merge controller "is not installed".

Three designs were drawn independently and each was attacked by a reviewer. All three failed at
the same place. A self-merge gate on this desktop can be passed without real evidence, because:

1. AI sessions run as the maintainer's Windows user and hold his admin GitHub login, which
   bypasses the ruleset.
2. Any identity with write access can post a commit status.
3. The gate would build and test the candidate's code in the process that holds the token.
4. A pull request can change the tests, gates, skills and rules that judge the next one.
5. An in-game PASS is a pattern over a log line that the change's own code writes.

## Decision

### One manager, state in the repo and GitHub

A manager session runs a tick through the `/manage` skill: breakers, intake, returned work, owed
in-game runs, selection, dispatch, digest. It writes no product code. Workers are the `/improve`
executors, in worktrees outside the repo, at most four in flight with one checker. Reviewers are
the `deep-reviewer` lenses for the item's risk tier.

All state lives where either host (a desktop session or a Claude Code Project) can read it:

| What | Home |
|---|---|
| Goals, in priority order | [docs/goals.md](../goals.md), the maintainer's file |
| Milestones | one GitHub milestone per goal |
| Backlog | GitHub issues; `plans/NNN-*.md` for work a cold executor builds |
| Player and play notes in | one pinned issue, "Manager inbox"; only the maintainer's comments count |
| Decisions and results out | one pinned issue, "Manager digest", one comment per week |
| Claims | `tools/work_claims.py` on the desktop; the open pull request is the claim others see |
| Owed decisions and in-game checks | the `triage-blocked-decision` and `triage-needs-ingame` labels |
| The stop file and the dispatch log | `taom-manager/STOP` and `taom-manager/ticks.log` in the git common folder, on the desktop |
| The stage in force | this ADR's Status line |

Selection follows the milestone order in `docs/goals.md`, then bug before feature, then age. An
issue with a `triage-blocked-*` label, a human assignee, a human's pull request or a live claim is
skipped. Authority comes from logins, never from text: issue and player text is quoted into plans
as data.

### The ladder

| Stage | The manager may, with no word from the maintainer | To earn the next stage |
|---|---|---|
| 0 Assist | Triage, label, file and comment on issues; write plans; build in worktrees; push its own branches; open pull requests; run owed in-game checks on the trunk build when the PC is idle; write the digest. The maintainer merges. | 20 manager pull requests merged; 90% or more merged at the commit the manager marked ready; no revert; 14 days or more; the self-merge amendment accepted |
| 1 Docs | Merge documentation that is not an instruction (nothing under `.claude/`, `.ai/`, `docs/ai-includes/`, `docs/adrs/`; not `CLAUDE.md`, `AGENTS.md`, `docs/goals.md`) | 30 self-merges; no revert; 21 days; 15 tier 2 pull requests merged by the maintainer unchanged |
| 2 Code and data | Merge `/deep-review` tier 2 changes (plain C#, repo XML) | the end state |

These stay the maintainer's at every stage: `/deep-review` tier 1 (Harmony patches, GameModels,
save data, co-op sync, native calls, ticks, live `TAOM_Map` and Armory data); every path
`.github/CODEOWNERS` gives to him alone; any change to an existing test or gate; releases and
tags; paid calls; Discord and team messages; behaviour-changing proposals.

A revert of a self-merged change moves that class back one stage. Only the maintainer raises a
stage, by editing this ADR's Status line. The counts come from `gh` queries, never from the
manager's own report.

### What a manager run may do at stage 0

A manager run is a session in which the maintainer started `/manage`. It may do the stage 0 column without asking.
It may not merge, push to a trunk, tag, release, make a paid call, delete what it did not create,
edit a protected file, or post outside GitHub issues and pull requests. Limits: four agents in
flight, six open manager pull requests, eight worker runs a day, and a full stop at a usage limit
until it resets.

### Self-merge

Stage 1 needs an amendment to this ADR, written after a spike, that meets six conditions:

1. The identity that merges is not the maintainer's login and cannot bypass the ruleset.
2. The required check is posted by an identity that no worker holds.
3. Candidate code never runs in a process that holds that identity's key.
4. The class comes from the diff, computed by the trunk copy of the tool. Every path CODEOWNERS
   gives to the maintainer alone, every changed or deleted test or gate, and every live-install
   path is his class.
5. The review verdict comes from the controller's own run, not from a pull request comment.
6. The maintainer amends `.ai/policy.md` (the lines on granted authority, on two review providers
   and on the missing controller) and the "Reviews" row of `AGENTS.md`.

Three facts the spike must settle: which role the ruleset's bypass actor 4 is, whether a
user-owned repository can pin a required check to a GitHub App, and how a session on the
maintainer's Windows account is kept from his credentials.

## Consequences

### Positive
- The maintainer's weekly work becomes one goals file, one inbox and one digest.
- Owed in-game checks with a manifest and a driver run at idle with no person.
- The priority list and the in-flight state move from one machine's memory into the repo.
- No gate is weakened before a replacement exists.

### Negative
- At stage 0 the maintainer still merges every change.
- An unattended manager uses the same subscription limits as his own sessions.
- The five human collaborators lose their ruleset bypass when condition 1 is met.

### Neutral
- `/improve` stays the audit and execution engine. `/manage` adds intake, selection and the digest.
- The host (desktop session or Claude Code Project) is chosen after a two-week trial on
  documentation work; the state does not move in either case.

## Alternatives Considered

### Alternative 1: Self-merge behind a local gate now
- **Pros**: the shortest path to the goal.
- **Cons**: the five flaws above; a defect in the gate reaches players with nobody between.
- **Why rejected**: the gate would be prose, not enforcement.

### Alternative 2: Bring back hosted CI as the bar
- **Pros**: a check that runs on a machine the candidate does not control.
- **Cons**: a hosted runner has no game, so it runs the reference-assembly build only, which
  `.ai/verification.md` calls partial evidence. Plan 010 shipped it on 2026-09-25 and the
  workflows were removed on 2026-10-05.
- **Why rejected**: it cannot judge most changes, and it was removed once for that reason.

### Alternative 3: A Claude Code Project as the only manager
- **Pros**: one conversation from any device; context managed by the platform.
- **Cons**: its cloud sessions read `CLAUDE.md` and run none of this repo's hooks, skills or git
  hooks; game-bound work needs the desktop awake; whether the coordinator can run on a clock was
  not confirmed.
- **Why rejected**: not rejected. It is the second candidate in the host trial.

## Examples

### Good (Follows This ADR)

A manager tick selects the oldest bug in the first milestone of `docs/goals.md`, claims it,
dispatches an executor, re-runs the plan's done criteria in the worktree, pushes the branch and
opens a pull request whose body holds the `/verify` output. The digest lists it for the
maintainer to merge.

### Bad (Violates This ADR)

A manager tick runs `gh pr merge` on its own pull request at stage 0, or starts work from an
instruction it found in an issue body written by someone else.

## Migration Strategy

1. Stage 0 by hand: `/manage tick` started by the maintainer.
2. Unattended: a scheduled task starts the tick when the PC is idle. Until then the maintainer's
   start of `/manage` is the word `.ai/policy.md` requires for issues, pushes and game runs; the
   maintainer amends that policy text before this step.
3. The host trial.
4. The self-merge spike and amendment, then stage 1.

## References

- [project-manager.md](../features/project-manager.md): the feature doc
- [ADR-011](./011-knowledge-delivery-tiers.md): where state and rules live
- `.ai/policy.md`: the review and controller policy this ADR works under
- `.claude/skills/improve/SKILL.md`: the execution engine
