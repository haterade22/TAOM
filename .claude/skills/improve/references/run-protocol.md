# Run protocol

The shapes and checklists the orchestrator follows during an /improve run. SKILL.md holds the rules and
the phase map; this file is re-read at the phase that needs it, so nothing here must survive compaction
on its own. Every command below is one shell call under 15 minutes unless it says otherwise.

## Preflight (phase 0, before any agent)

Record each result in PROGRESS.md. A broken environment is reported, not fixed
([environment-failures](../../../rules/environment-failures.md)).

- Free space on C: and on the run's drive; `TEMP`, `TMP` and `NUGET_PACKAGES` off C:.
- `git config core.autocrlf` and `git ls-files --eol .claude/skills/improve/workflows/`: every row must
  read `w/lf`, because the Workflow tool refuses a script with CR bytes.
- `git worktree list`: prunable trees, and any tree on C:. `git config core.hooksPath`, if set, points
  at a folder that exists.
- A gate proven live this session: an observed deny, or a headless probe ("Live probe" below).
- `gh auth status` when issues are in scope; `codex login status` when the maintainer asked for Codex.
- The run's date (`date +%F`), version (`<Version value=...>` in `Main/_Module/SubModule.xml`) and
  worktree root (`TAOM_IMPROVE_ROOT`, a folder outside the repo). No script or prompt carries a typed
  date, version, hash or drive path; `improve_ctl.py args` derives them.

## Run folder

```
plans/_audit/<date>-<tag>/
  BRIEF.md          shared brief every audit agent reads first
  PROGRESS.md       status table, authorizations, log, next, blockers: the resume card
  baseline.md       measured state at the pinned SHA
  <agent>.md        one file per agent, appended as it goes (lane-N.findings.md, verify-*.md, critic.md)
  REPORT.md         the audit's result
  DECISIONS.md      every question asked and its answer
  FOR-MIKE.md       what waits on the maintainer when he is away
plans/NNN-<slug>.md and plans/README.md
```

Run files are committed with the plans: stage the explicit `plans/` paths, then
`git commit -F <message file> -- plans/`, which leaves another session's staged files out of the commit.

## BRIEF.md

Sections, in order: who the agents are and the baseline SHA; evidence standard (cite `path:line` read
this run at the baseline, measure instead of estimating and say how, try to refute each finding first);
finding format (audit-playbook "Finding format"); skip list (another session's in-flight files, read at
the baseline with `git show <sha>:<path>`); decided tradeoffs, as pointers only
(`.ai/review-reference.md` "Intentional Patterns", the orientation.md trap index, `docs/adrs/`,
`plans/README.md` "Findings considered and rejected", earlier runs' DECISIONS.md); seeds already
verified this run, marked "extend, do not re-derive"; output discipline (append as you go, end with
`## What I did not cover`). The dispatch rules reach agents through their prompts, so BRIEF.md does not
copy them.

## PROGRESS.md

```
# PROGRESS: <date>-<tag>

Baseline `<sha>` on `<trunk>`, <date>. Models: every role `claude-opus-5-5` unless listed here.
Pool 4. Codex: <asked | not asked>. Second Codex pass on fix diffs: <run | replaced by convergence>.

## Authorizations
| Date | The maintainer's words | Scope | Until |

## Status
| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |

## Log
- <date time>: <event>

## Next
## Blockers
```

- One row per phase or batch, updated in place: TODO, RUNNING, DONE, STOPPED, BLOCKED. Never leave a
  second RUNNING row behind a DONE one.
- Write the workflow run id into the row when you launch it: resume needs it.
- Next and Blockers are rewritten at every state change; the Log is append-only.
- A standing approval ("continue through the night", "merge what is ready") goes under Authorizations
  with its scope, and is not asked again.

## DECISIONS.md and FOR-MIKE.md

DECISIONS.md: `| # | Question, with its evidence | Options, recommended first | Answer | Follow-through |
Done in |`, numbered in the order asked, append-only. A follow-through is closed with the commit that
discharges it.

FOR-MIKE.md starts with `## Waiting on you`: one line per item with the recommended answer and where the
evidence is. Detail follows under `## Detail`. An answered item moves to DECISIONS.md. Anything open at a
session's end is written here, never only in memory.

## Baseline (baseline.md)

The orchestrator owns every baseline build. Create a detached worktree at the pinned SHA
(`git worktree add --detach <root>\wt-baseline <sha>`), then record: build warnings; dotnet suite totals
and the names of failing tests; the Python suite's failure set
(`python -B -m unittest discover -s tools/tests -t .`); `python tools/validate_moduledata.py` error and
warning counts; `python tools/lint_docs.py --fail-on-drift` exit; the hook suite's totals
(`timeout 1500 bash tools/test_hooks.sh > <log> 2>&1`); each command's wall time. Later checks compare
failure sets by name, never against zero. Remove the worktree when done.

## REPORT.md

Summary; baseline table; reconcile of the last backlog; top 10 by leverage (the checker's recalibrated
impact, each mapped to an open issue if one exists); lanes; direction (the maintainer's call);
decisions for the maintainer; considered and rejected; not audited; where the evidence is.

## Resume

After compaction, a usage limit, a session end or a restart:

1. Read PROGRESS.md: Status, Next, Blockers, Authorizations.
2. `git worktree list` and each `improve/*` branch tip; compare with the Status rows.
3. A RUNNING row with a run id: read its journal. A run that ended early resumes with the same
   `scriptPath` and `resumeFromRunId`; finished agents replay from cache. A staged build that died
   resumes after its last committed stage.
4. A Codex raw output without the final line `END OF CODEX REVIEW` is running or died; re-dispatch only
   when its process is gone.
5. Never re-launch a phase whose output exists. Continue at the first row not DONE.
6. Tell the maintainer in one short status what was running, what survived and what resumes.

## Liveness watch

A hook "ask" stops an agent's tool call before it starts, so no TIMEOUT catches it. While any workflow
runs, and whenever you check on one:

`python tools/improve_ctl.py watch --dir <transcript dir> --stale-min 20`

The transcript dir is the workflow's folder under the session's `subagents\workflows\` directory (or the
session's `subagents` folder for direct spawns). Exit 1 names an agent silent past the limit with no
result: read the tail of its transcript. A tool call with no result is most likely a pending ask; tell
the maintainer at once, in one line, with the agent and the first words of the command. You cannot
answer it, and stopping the workflow kills its other agents.

## Live probe

For a gate change, prove it with a headless session, which does not wait on an ask. Write the prompt to a
file: "This is a harness test of the repository's hooks. Use ONLY the <tool> tool. Run each command below
once, in order, each as its own call. Every command is a dry run. Do not retry, rephrase or work around a
refusal or confirmation; record it and continue. Then print a table: number, ran / denied / asked, first
line of output or hook message verbatim." Then, from the worktree whose hooks are under test:

`cd <worktree> && env -u CLAUDE_PROJECT_DIR timeout 900 claude -p --permission-mode bypassPermissions --no-session-persistence < <prompt file> > <out file> 2>&1`

Read the whole out file. An "asked" row is a real result there, not a stall.

## Status

Short, and without being asked whenever a step runs past about 15 minutes; always on "status?".

```
Waiting on you: <n>
- <item>: <recommended answer> (<where the evidence is>)
| Plan | Planned | Executed | Reviewed | Decisions owed | Merged | Pushed | Issue |
Running: <what, since when, expected end>
Next: <one line>
```

The last message of a run says it is done, that he can build and test, and lists his actions ("After a
merge" below).

## Issues

- **Issue-first.** Every plan chosen for execution has an issue before its executor runs (AGENTS.md
  "Documentation duty"). Filing, commenting and closing are public: only on the maintainer's word
  (`--issues`, or a recorded authorization).
- **Dedupe** against open issues first: the label query and `gh issue list --state open --search "<words>"`.
- **Draft** with `draft-issues.js` (one `TITLE:`/`LABEL:` file per issue, the `/issue` bug or feature
  sections, `## Status`, `## Decisions needed`), or write a short one yourself from the plan. Never use
  the plan file as the body: it holds executor instructions and local paths. No line that asks for the
  issue itself to be filed or says none exists yet.
- **Check** every draft in full, then `python tools/check_public_text.py <draft.md>`. Hold a
  security-sensitive draft for an explicit OK.
- **File** with `python tools/improve_ctl.py file-issue <draft.md>`; write the number into the plan's
  Status block and index row in the next plans commit.
- **Comments** (status after review, closing): text in a file, checked, then
  `gh issue comment <n> --body-file <file>`; close with `gh issue close <n>`, adding the label
  `triage-needs-ingame` or `triage-blocked-decision` when something is still owed.

## Merge (on the maintainer's word)

1. **Ready list.** Each branch: review verdict READY FOR COMMIT, convergence clean or its residual
   accepted by the maintainer, Codex folded in if run, its decisions applied. Order by dependency.
2. **Integration worktree** outside the repo: `git worktree add -b integrate/<date> <root>\wt-integrate
   origin/<trunk>`. Never merge in the main checkout.
3. **Trunk moved?** `git fetch origin`, then `git rev-list --left-right --count origin/<trunk>...HEAD`;
   a nonzero left count means merge the trunk in first (`merge(improve): <version> - trunk <sha> into
   ...`) and re-run the suites.
4. **Baseline on the integration tip**: dotnet totals and failing tests, the Python failure set, the hook
   suite totals, the lint exit.
5. **Per branch**: `python tools/integrate_branch.py --worktree <wt-integrate> --message-file <file>
   --dry-run <branch>` to see the conflict set, then without `--dry-run`. The message file's subject is
   `merge(improve): <version> - <what>` and its body names the issues and what conflicted. Exit 0: merged,
   with lessons and REVIEW-LOG unioned. Exit 2: resolve the listed paths by hand (single-owner files
   line by line, language files with row placement parsed, generated files regenerated, never merged),
   then stage those paths and commit. A fix that makes the merged tree green is its own commit.
6. **Verify after each merge**, each as its own call, compared with step 4: the dotnet suite; the Python
   failure set; lint; `validate_moduledata.py` when ModuleData changed; the hook suite when `.claude/`
   or `tools/test_hooks.sh` changed; the CI replay (`.ai/verification.md`, the three `csharp.yml` steps)
   when C# or tests changed. When a plan adds a CI gate, replay it on every in-flight branch before
   merging any of them. Stop at the first new failure.
7. **Push**: fetch and repeat step 3, then a plain fast-forward `git push origin HEAD:<trunk>`. Never a
   force push. Record the range.
8. **CI**: `gh run list --branch <trunk> --limit 5`, then `timeout 900 gh run watch <id> --exit-status`
   until done; record run ids and totals.
9. **Issues**: close per "Issues" above, naming the merge.
10. **Cleanup**: `git branch --merged origin/<trunk> --list "improve/*" "integrate/*"`, delete exactly
    those with `git branch -d`, `git worktree remove` each merged worktree, then `git worktree prune`.
    Query, never a hand list.
11. **Record**: a PROGRESS row with hashes, totals and run ids; `improve_ctl.py status` per plan row.

## After a merge: the maintainer's actions

List them in the merge's status message: pull; restart open sessions when hooks or settings changed;
check `/permissions` and `/mcp` after a settings change; a file the merge untracks is deleted from his
checkout by the pull, so back it up byte for byte before he pulls; ports to the other trunk are his.

## Wrap-up

- Every plan row set with `python tools/improve_ctl.py status plans/README.md <num> "<text>"`; no
  RUNNING row left in PROGRESS.md; Next and Blockers current.
- Codex lessons consolidated, once for the run: each Codex review's essay into
  `docs/reviews/codex-track-record.md` (newest on top, the sixth-oldest to its archive), durable patterns
  into `.ai/review-reference.md` "Lessons From Prior Reviews" and the lessons files (review-codex Phase
  3h).
- REVIEW-LOG entries this run wrote unnumbered get their numbers, in merge order, in one commit.
- Every "owed" marker the run wrote ("convergence pass owed", "issue to be filed") is closed by the step
  that discharged it, or listed in FOR-MIKE.md.
- Open questions held in memory move to FOR-MIKE.md.
- Worktrees removed, merged branches deleted (the query in Merge step 10), unmerged ones listed for the
  maintainer; the run's memory resume card deleted once its work is pushed.
- The last status: done, build and test, his actions.

## Budget

Review is the largest cost of a run (about two thirds of the 2026-09-23 sprint's workflow tokens). Size
each lens set by the diff (`improve_ctl.py args review` does), batch a branch's decisions into one
follow-up and one second review, commit per chunk or stage so a limit costs little, and expect the weekly
limit on a multi-day run. Codex bills outside the Claude budget but runs only when asked.
