---
name: upstream-review
description: Use on the UPSTREAM AHEAD banner, or when asked to review yotthani's repos again, check what the watched upstream repos changed, or run the daily upstream review.
argument-hint: [--report-only]
---

# Upstream review (watched repos, delta since the last review)

The repeatable form of the 2026-10-15/16 reviews of yotthani's repos (records
[adopt-yotthani-2026-10-15.md](../../../docs/reviews/adopt-yotthani-2026-10-15.md) and
[adopt-yotthani-2026-10-16.md](../../../docs/reviews/adopt-yotthani-2026-10-16.md)). The watched repos and their last
reviewed SHAs are in [`docs/reviews/upstream-watch.json`](../../../docs/reviews/upstream-watch.json); the tool is
`tools/upstream_watch.py` ([tools/README.md](../../../tools/README.md)). A one-off source goes through `/adopt-external`,
whose security pass applies here too.

**Gates:** AGENTS.md "Paid AI dispatch", "Untrusted input" and "Git and commits" apply unchanged; push, merge and public
issues need the maintainer's word for this run; the main checkout is never touched (other sessions work there).

**`--report-only`** (the unattended daily run): P1 and P2 only, then write the report to
`E:\Temp\taom-upstream-watch\reports\<date>.md` and stop. No worktree, no repo edit.

## P1. Detect

1. `python tools/upstream_watch.py status --max-age-hours 0` (a live check, never the banner's cache). Exit 0: nothing new,
   report that and stop. Exit 3: UNCHECKED (gh missing, logged out, offline): report the reason and stop. Exit 2: the
   state file or the arguments are bad: report the message and stop. Exit 1: a repo row shows `ahead` above 0, or a
   release row shows a `latest` other than its `seen` (handle that by the paragraph on `UPSTREAM RELEASE` at the end;
   it is not a review). Report any repo the NOTE line names as unchecked (ahead wins over unchecked in the exit code).
   Step 2 and P2 run only for repos that are ahead.
2. Per repo that is ahead: `python tools/upstream_watch.py areas --repo <r> --clone-dir <scratch>/up/<name> --out
   <scratch>/areas-<name>.md`. The brief's Range line holds the **pinned head** (full 40-hex SHA): every reader cites it,
   and P7's `advance` takes it. Nothing in the blobless clone is ever checked out, built or run. A WARNING that a
   `feature_paths` entry is missing at the head means the author renamed a feature: fix the state file in P7.
3. Read the last record's "What TAOM verified", "Not taken" and "For <author>" sections: what TAOM already has, and what is
   still asked of the author.

## P2. Read-only review

- **Readers:** one per area with real change (CLAUDE.md "Subagents" for the briefing; Sonnet `implementer`, told it is
  read-only and must never `git checkout`), at most four in flight. A `feature` area gives engine facts only, never the
  feature: the maintainer leaves the author's features with the author. Each item: what, evidence (path@pinned-head:lines,
  quoted), kind (engine fact, TAOM bug or risk, tool or method, feature), TAOM relevance with a TAOM path, recommendation.
- **Verifier:** one Opus `architect` re-reads every load-bearing claim at the pinned head, the engine source
  (`pwsh tools/taom-src.ps1 path <Type>`) or the native client (`python tools/native_decompile.py`), and TAOM's code, and
  tries to refute it.
- **Completeness critic** when the delta is large: which area, claim or source went unread.
- **Report:** `<scratch>/report.md`: what TAOM has, what it could use (code items, knowledge items), TAOM bugs or risks the
  author's measurements show, not taken (with reasons), asks still open with the author.

## P3. Plan, then stop for approval

Code items (each with its issue text, files, tests first, tier), knowledge items (owning doc per
[ADR-011](../../../docs/adrs/011-knowledge-delivery-tiers.md)), and what needs the maintainer's word. Wait for the go.

## P4. Issues

On the go: one `/issue` per code item and per confirmed TAOM bug; a suspected bug that needs a game run carries its check
and the `triage-needs-ingame` label.

## P5. Build and review

A worktree from `origin/bannerlord-1.5.x` (`git worktree add -b <branch> E:/repos/taom-<slug> origin/bannerlord-1.5.x`),
then `/ship` on it with every phase as written there (its Codex phases need their own yes). The upstream-specific parts
of each item's commit body: credit the author with the pinned head.

## P6. Knowledge base

- Chunks by area, disjoint files, each fact in its owning doc with one trap line at most (`python tools/lint_docs.py
  --fail-on-drift` gates the trap index).
- Credit and hedge rule ([lesson](../../../docs/reviews/lessons/misc.md), "Restate an upstream fact with its hedge"): the
  author, repo, path@pinned-head and lines, the evidence kind and the engine version, and the source's own qualifier in the
  same sentence; TAOM's own reading is tagged as such.
- The record `docs/reviews/adopt-<author>-<date>.md` in the shape of the last one; the provenance register's pass and
  Covers columns; the adoption history line in `docs/ai-includes/external-repo-adoption.md`.
- Doc review: three finders (fidelity to source, consistency with TAOM code and docs, routing), then one Opus verifier;
  fix every confirmed item and record it in the record's "The doc review". Before merge, grep the batch for every name the
  code review renamed or deleted.

## P7. Advance the watch

`python tools/upstream_watch.py advance --repo <r> --sha <pinned head> --record <record> --date <date>` for each repo,
committed in the same commit as the record (the state file must never name an uncommitted record).

## P8. Land

1. `git fetch`, rebase on `origin/bannerlord-1.5.x`, then `/verify` on the merged tree.
2. On the maintainer's word: `git push origin HEAD:bannerlord-1.5.x` (a fast-forward), then confirm each commit with
   `git merge-base --is-ancestor <sha> origin/bannerlord-1.5.x`; close or update the issues.
3. Cleanup on the maintainer's word: the worktrees, the local branches, `E:/graphify/TAOM-wt-<worktree>`; the memory card
   moves to the shipped follow-ups.

## Rules learned in the first runs

- Reviewer prompts say: never `git checkout`, never edit; check `git branch --show-current` before each commit.
- Never run `build_backlinks.py` for a batch: it rewrote 254 footers, other sessions' files included; the lint passes
  without footers.
- A backslash path goes through the Edit tool, never a non-raw script string (a `\f` became a form feed).
- A copied figure takes its source tag in the same edit; the author's pronouns are not stated: use the name.
- Trunk moves during a review: rebase and re-verify on the merged tree before the push.

## Unattended daily run (UNVERIFIED; the maintainer sets it up)

OS configuration is the maintainer's, so this skill never creates the task. A headless run of this skill has not been
proven. Before scheduling one, run it once by hand with `claude -p "/upstream-review --report-only"`
from the TAOM checkout and confirm that it invokes the skill and writes the report. Pick the permission mode, a
`timeout` and `--no-session-persistence` as in the repo's headless recipe
(`.claude/skills/improve/references/run-protocol.md`). Then schedule that exact command with Windows Task Scheduler.
Without it, the `UPSTREAM AHEAD` session-start banner (checked at most once a day) is the trigger.

**An `UPSTREAM RELEASE:` line (or, in full `status`, a release row whose `latest` differs from `seen`)** is not a review: a repo under `releases` in the state file published a new release. Update the record the line names (for Bannerlord-Coop-Team/BannerlordCoop, the version table in `docs/features/coop-interop.md`: the release's branch, its `<GameVersion>`, and whether current TAOM can run it), then run the `seen` command the line prints, and commit both together.
