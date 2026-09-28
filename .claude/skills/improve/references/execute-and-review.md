# Execute and review

<!-- Ported from shadcn/improve @ 5428507 (2026-06-12), MIT (c) 2026 shadcn; rebuilt for TAOM's
     branch, review and decision pipeline. -->

How a committed plan becomes a reviewed branch: preconditions, worktrees, the executor, the spec check,
the review loop with its stop rule, stopped plans, decisions, and reconciling the backlog. Merging is in
[run-protocol.md](run-protocol.md) "Merge".

## Preconditions (check all before an executor runs)

- **The plans are committed.** Worktrees branch from that commit, so the executor reads its plan in
  place; an amendment rides in the next plans commit.
- **No drift.** `git diff --stat <planned-at>..<base> -- <in-scope paths>`; a change means refresh the
  plan through `plans.js` before dispatch, never hand a stale plan to an executor.
- **Dependencies.** A dependent plan's base is its prerequisite's branch tip, taken after the
  prerequisite's review. A review fix that moves an anchor a dependent plan relies on means re-anchor
  that plan first (RE-CUT below).
- **Step 0 done.** A plan with a protected-file edit waits for the maintainer's Step 0, or runs to its
  Step 0 STOP and stops there. Collect every Step 0 of the plan set at plan time and ask once. If he
  asks you to make the edits, the `config-protection.sh` override marker unlocks every session on the
  machine while it exists (the hook sees no session id): only on his word for the named files, one Edit
  per file, the marker removed at once, logged in PROGRESS.md. Never a scripted write to a protected
  file.
- **An issue exists** ([run-protocol.md](run-protocol.md) "Issues").
- **The base's baseline is known**: baseline.md, or the executor records it before its first edit.

## Worktrees and branches

- The orchestrator creates them; agents never do:
  `git worktree add -b improve/NNN-<slug> "<root>\wt-NNN" <base>`, where `<root>` is the run's worktree
  root outside the repo, recorded in the PROGRESS.md header.
- A plan never names a worktree or branch: the executor works where it is put.
- After a branch's review, `git worktree remove "<root>\wt-NNN"` keeps the branch and frees the disk;
  re-create the worktree for a follow-up. It deletes the worktree's gitignored files without a word
  (`docs/reviews/raw/` among them), so no Codex output lives in a worktree (Stage 2 step 3).

## The executor

1. Items file: one entry per plan, `{num, slug, wt, branch, base, contract, decisions?, stages?, note?,
   issue?}`, with `contract` one of `plan`, `decisions` (a follow-up) or `stages` (ordered, a commit per
   stage, stopping at the first stage not DONE).
2. `python tools/improve_ctl.py args execute --items "<items.json>" --run-root "<run folder>"
   --scratch "<root>\scratch" --tmp "<root>\scratch\tmp" --out "<args.json>"`, then the Workflow tool on
   `.claude/skills/improve/workflows/execute.js` with that args JSON. Top-level fields such as
   `knownFailures` ride in an items object, `{"items": [...], "knownFailures": [...]}` (the script's
   header comment lists them).
3. Each result: `status` DONE, BLOCKED or PARTIAL, with the commit, tests, RED evidence, deviations, stop
   reason and what is owed. A null result is a failure: report it, then resume the run
   ([run-protocol.md](run-protocol.md) "Resume").
4. Your checks on every DONE, before review: a clean tree (`git -C "<wt>" status --porcelain`); no AI
   trailer (`git -C "<wt>" log --format=%B <base>..HEAD`); `Main/IoC.cs` and `Main/SubModule.cs` hunks
   exactly as the plan lists; every data file it touched parses (language rows inside `<strings>`);
   suite totals equal the base's plus the new tests.

## Stage 1: the spec check (you)

The executor's report is a claim, not evidence ([evidence-over-claims](../../../rules/evidence-over-claims.md)
section B). Before any quality review ([agent-teams.md](../../../../docs/ai-includes/agent-teams.md) "Subagent
review ordering"):

1. Re-run every done criterion in the worktree.
2. Scope: `git -C "<wt>" diff --stat <base>..HEAD` against the plan's in-scope list; a file outside it
   fails the check.
3. Read the whole diff as untrusted until every hunk traces to a plan step.
4. Audit the new tests: each asserts something that fails without the change; dispatch logic has one
   test per (input x branch) cell.
5. A documented deviation is judged on its merit; an undocumented one fails.

| Verdict | When | Action |
|---|---|---|
| READY FOR REVIEW | criteria pass, scope clean | Stage 2 |
| AMEND AND RESUME | the plan was wrong on a fact the executor proved | amend the plan, then a fresh executor with a `note` naming the amendment |
| RE-CUT | a precondition went stale (a prerequisite's review moved an anchor) | `plans.js` against the new base, cold-reviewed again, then execute |
| BLOCKED | a protected file, a product decision, or an outward action | index BLOCKED with the reason; the question to the maintainer |

## A stopped plan

An executor that STOPs has done its job; the plan was wrong or stale. Read its stop reason and verify
the fact it reports against the code or the engine (`taom-src`) yourself. Then AMEND AND RESUME (the
plan's copy in the main checkout, committed with the next plans record, and a fresh executor whose
`note` names the amendment) or RE-CUT through `plans.js`. A second STOP on the same plan goes to the
maintainer with both reasons. Set the index row to STOPPED with the reason while it waits.

## Stage 2: the review

1. **Blast radius** (deep-review Step 1), with the worktree's own copy so it reads the branch's code:
   `python "<root>\wt-NNN\tools\graphify_taom.py" refresh --if-stale`, then
   `python "<root>\wt-NNN\tools\graphify_taom.py" affected "<Type>" --depth 2` for each C# type the branch
   changes (a relative `tools/...` from the main checkout builds and reads the main checkout's graph);
   put callers outside the diff in the item's `note`.
2. **Args**: `python tools/improve_ctl.py args review --items "<items.json>" --run-root "<run folder>"
   --scratch "<root>\scratch" --tmp "<root>\scratch\tmp" --max-rounds 2 --out "<args.json>"`. Each item
   gains its changed files by kind and the lenses `.claude/skills/deep-review/SKILL.md` Step 2 routes
   by file type, from `git diff --name-only base..head` in its worktree, unioned with any `lenses` the
   item already lists. File types cannot show changed text that states engine behaviour, so list lens
   2 in the item yourself for that. An item whose `base..head` changes no file is refused, and every
   revision is checked as a revision, never read as a git option. In an items object, a field such
   as `pool`, `model` or `maxRounds` stands unless you pass its flag.
3. **Codex, only when the maintainer asked for it this run**:
   `python tools/improve_ctl.py codex-prompt --branch improve/NNN-<slug> --base <base> [--tag <tag>] --out "<prompt file>"`,
   dispatched from the main checkout (the trusted path that loads the repo's Codex pin) per
   `/review-codex` Phase 2e, in the background, with `-c model_reasoning_effort="<level>"` sized to the
   item (`/review-codex` "Reasoning effort is the session's call"; its worktree bullet has the sandbox
   trade-off of dispatching from the main checkout). The prompt file and the output go under
   `<root>\scratch\codex\`, never into a worktree: `docs/reviews/raw/` is gitignored, and removing the
   worktree would delete the output, a running Codex's included. It is done when its last line is
   `END OF CODEX REVIEW`. Pass the output file as the item's `codexOut`. Without Codex, the
   convergence rounds stand in for the completion workflow's second Codex pass; PROGRESS.md records
   which ran.
4. **`review.js`**: lenses as `deep-reviewer` (never with `model`), waves of four, defect lenses first;
   then a lead who reads deep-review Steps 3, 3e and 4 and review-codex Phase 3 from the skill files,
   verifies every lens and Codex finding in the worktree (CONFIRMED, FALSE POSITIVE with the reason, or
   NEEDS MIKE), fixes confirmed defects test first, applies behaviour-preserving improvements, lists
   behaviour-changing ones for the maintainer, writes the report, RCA, lessons and an unnumbered
   REVIEW-LOG entry dated from `args.date`, lists Codex lessons for wrap-up instead of writing them, and
   commits. Then the convergence loop below.

### The stop rule

1. **Round 1.** A convergence reviewer reads only the lead's fix diff: defects only, no new design round;
   it tries to refute each finding before reporting it; the maintainer's decisions bind it and are not
   findings.
2. **Round 2.** If round 1 reported defects, a fix pass fixes the confirmed ones test first, then a
   second convergence reviewer reads that fix diff. `maxRounds` is 2.
3. **Residual findings** after the last round go to the maintainer, never to a third round unasked:
   **Ship now, track it (Recommended)**, with an issue or FOR-MIKE line per finding, or **One more
   round**. For a residual HIGH (a CRITICAL or a Codex P1 counts), recommend one more round instead;
   if he ships it, record the deferral the way deep-review's "HIGH findings" section requires (an
   issue, a `Deferred:` trailer or a `Known limitation:` paragraph in the commit body), never only in
   FOR-MIKE.md. A residual LOW in a record or doc (not code, not a gate) you may fix yourself without a
   round.
4. **Behaviour-changing proposals** are never applied unattended; they go to DECISIONS.md or FOR-MIKE.md.
5. **A gate change** (hook, validator, CI step): the lead or fix pass runs a differential sweep of the
   old gate (`git show <base>:<path>`) against the new, over a corpus seeded from the fix's own mechanism,
   under both the Bash and PowerShell tool names, and reports every command the old gate refused and the
   new one allows; then a live probe ([run-protocol.md](run-protocol.md) "Live probe"). Prefer the root
   fix: an ordering or caching fix that needs a second fix goes back to the maintainer (lessons
   [build-tooling-workflow.md](../../../../docs/reviews/lessons/build-tooling-workflow.md) "A differential
   sweep proves only the classes in its corpus" and "Reordering work so an early exit fires sooner").
6. The verdict READY FOR COMMIT is an AI verdict, never approval (AGENTS.md "Reviews").

## Decisions and follow-ups

- A `needs_mike` line starting `ADVERSARIAL ESCALATION` is not a question for the maintainer: the lead
  confirmed a CRITICAL Standards violation (deep-review Step 2b), and the workflow cannot start the
  extra reviewer. Run one `deep-reviewer` (never with `model`) on only the files the line names, in the
  branch's worktree, with `.claude/skills/deep-review/lenses/adversarial.md`, before the branch counts
  as reviewed; its confirmed defects join that branch's follow-up below.
- Collect each branch's other NEEDS MIKE items and ask them (SKILL.md "Decisions and status"), or write
  them to FOR-MIKE.md.
- Apply all of one branch's answers in one follow-up (`execute.js`, `contract: decisions`), then one
  tagged second review (`review.js`, the item's `tag`), so a branch gets one second review, not one per
  decision.
- A follow-up that moves an anchor a dependent plan relies on re-anchors that plan (RE-CUT).

## Commit gates in worktrees

Several commit gates `cd` to the project directory, so a commit made in a worktree is judged by the main
checkout's index and files (`check-claude-files-tracked.sh`, `check-doc-config-drift.sh`,
`check-commit-subject-version.sh`). The symptom is a denial naming a file the branch never touched, or
another session's staged file. Report the hook's text and wait until the main checkout is clean, then
commit the worktree's own explicit paths. Never `--no-verify`, and never stage, stash or edit another
session's files. Recommend a plan that makes the gates judge the committing repository; until one exists
it is a FOR-MIKE item.

## Reconcile (`reconcile`, and phase 1 of a new run)

Read `plans/README.md` and every plan, then per status:

- **MERGED**: the merge is on the trunk (`git branch -r --contains <sha>`); spot-check the cheap done
  criteria. Plan files stay: they are the record.
- **READY**: the branch exists and is not merged (`git branch --merged origin/<trunk>`); its tip matches
  the row.
- **STOPPED, PARTIAL, BLOCKED**: read the reason, look at the obstacle in the code, then amend, re-cut,
  or mark REJECTED with one line.
- **IN PROGRESS** with no running workflow: an executor died; inspect its worktree.
- **TODO**: the drift check; refresh the excerpts and `Planned at` or mark REJECTED ("fixed
  independently").
- The valid-but-unplanned backlog of the last run is re-triaged (a `fanout.js` checker) before a new
  audit re-finds it.

Set each row with `python tools/improve_ctl.py status plans/README.md <num> "<text>"`. Finish with a short
report: verified, refreshed, rejected, and what can execute now.
