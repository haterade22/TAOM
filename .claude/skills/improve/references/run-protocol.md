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
- **Earlier work on the same request.** Read `plans/README.md` and the earlier runs' REPORT.md, and
  ask where any other AI client's run left its record (a session log outside the repo counts: the Kimi
  run of 2026-11-07 was found only in its client's session folder). What it did goes into BRIEF.md
  "Already done"; its open leads go to the lanes as leads.
- **Other sessions' work in flight**, for the BRIEF's skip list: `git worktree list`; for each local
  branch ahead of the trunk, `git diff --name-only <trunk>...<branch>`; for each sibling worktree,
  `git -C "<wt>" status --short`. A session that announced a live game run owns that feature's folders
  for the night.
- A gate proven live this session: an observed deny, or a headless probe ("Live probe" below).
- `gh auth status` when issues are in scope; `codex login status` when the maintainer asked for Codex.
- The run's date (`date +%F`), version (`<Version value=...>` in `Main/_Module/SubModule.xml`) and
  worktree root `<root>` (a folder outside the repo and off C:, holding the worktrees, `<root>\scratch`
  and `<root>\scratch\tmp`), written into the PROGRESS.md header so a resumed run has it. No script or
  prompt carries a typed date, version, hash or drive path: `improve_ctl.py args` derives the date and
  version, and takes the root as `--scratch "<root>\scratch" --tmp "<root>\scratch\tmp"` (or from
  `TAOM_IMPROVE_ROOT`; without either it exits 2). Quote every path in a command: Git Bash drops the
  backslashes of an unquoted one, and `args` refuses a `--run-root`, `--scratch`, `--tmp` or
  `TAOM_IMPROVE_ROOT` that is not absolute (exit 2).

## Launching a workflow and waiting for it

1. Write the items file with the Write tool, then
   `python -B tools/improve_ctl.py args <name> --items "<items.json>" --run-root "<run folder>"
   --scratch "<root>\scratch" --tmp "<root>\scratch\tmp" --out "<root>\scratch\<label>-args.json" --wrap
   "<root>\scratch\wf\<label>.js"`. `<label>` names the launch (`lanes`, `verify`, `plans`, `exec`,
   `review-a`). The wrapper embeds the args and calls the workflow script, because the Workflow tool
   takes args by value and they hold the dispatch rules. The tool reads the dispatch rules, the
   workflow scripts and the version from the checkout it runs in: run it from the checkout whose skill
   files the agents must get (branch mode: `cd "<root>\wt-run" && python -B tools/improve_ctl.py ...`,
   one call, so no directory change persists). Read the args back (pool, models, items) before you
   launch.
2. Launch the wrapper with the Workflow tool (`scriptPath`). Its launch result names the run id and
   the transcript folder: write both into the PROGRESS.md row at once.
3. Wait in the turn: `python -B tools/improve_ctl.py watch --dir "<transcript folder>" [--dir "<second
   folder>"] --wait 570`, as a Bash call with `timeout: 600000` (the tool's maximum; its default of
   120000 kills the wait after two minutes). It blocks until an agent finishes, an agent is newly
   flagged STALLED-ASK or STALE, no agent has been at work for two looks in a row, or the limit
   passes, and says which. `--step N` waits for N more agents. A flag that stood before the wait does
   not end it, so a wait still blocks while an ask waits for the maintainer; the exit code stays 1.
   "No agent is running" is not "the workflow is done": between two waves a workflow has none. Use
   each return for work: the spec check of an executor that finished, the next items file, the
   records.
4. When the workflow's own completion notice arrives, read the result from its task output, not from
   memory of the watch lines.

## Models

The scripts default every role to `claude-opus-5-5`. The maintainer asked on 2026-11-12 for a model
and an effort per task. What worked on that run:

| Role | Model, effort | Set with |
|---|---|---|
| Lanes that need cross-cutting judgment (architecture, performance, harness) | Opus, high | the fanout item's `model` and `effort` |
| Lanes that read widely against clear criteria (tests, tools, docs) | Sonnet, high | the fanout item's `model` and `effort` |
| Refuters (fanout items, role `lane`) | Opus, high | the fanout item's `model` and `effort` |
| Critic (the fanout `checker`) | Opus, xhigh by default | `--model checker=id`; the checker's `effort` |
| Plan writers, cold reviewers, revisers | Opus; effort fixed at high | `--model writer=id reviewer=id reviser=id` |
| Executors of mechanical or test-only plans | Sonnet, medium or high | the execute item's `model` and `effort` |
| Executors of production C# refactors | Opus, high | the same |
| Review lenses | the `deep-reviewer` definition | never passed |
| Lead and fix pass | Opus; effort fixed at high | `--model lead=id fix=id` |
| Convergence reviewer | Opus; effort fixed at max | `--model convergence=id` |

One retry at the next tier up when a result is wrong or confused, never at the same tier.

## Branch mode

When the maintainer wants every change on a branch that he merges later:

- The baseline is the SHA of `origin/<trunk>` after `git fetch origin`; local trunk commits that
  are not pushed are other sessions' work and are not in it.
- Make the run branch and its worktree before the first run file:
  `git worktree add -b improve/<date>-run "<root>\wt-run" <baseline sha>`. The run folder, the plans,
  the index and your own small items are committed there, never in the main checkout.
- Plan worktrees branch from the plans commit on the run branch. Each reviewed branch merges into
  the run branch with `integrate_branch.py --worktree "<root>\wt-run"`; that merge is part of the
  run and needs no further word. The tool refuses a dirty tree, so commit the run folder's files
  (explicit paths) before each call.
- Of "Merge" below, steps 1, 5, 6 and 11 apply as written, with `<root>\wt-run` as the integration
  worktree. Steps 2 and 3 are replaced by the two bullets around this one. Steps 7 to 10 apply only
  after the maintainer's word for the trunk. For the cleanup, list the merged plan branches with
  `git -C "<root>\wt-run" branch --merged HEAD --list "improve/0*"` and delete them from there.
- The trunk moves during a run. Before the final verification, fetch and merge `origin/<trunk>` into
  the run branch; repeat it before a push when the left count of
  `git rev-list --left-right --count origin/<trunk>...HEAD` is not 0.
- On the maintainer's word the trunk takes the run branch as one fast-forward:
  `git push origin HEAD:<trunk>`. Without that word, push nothing: the branch is local, and the final
  message gives him the commands to merge it.
- An in-game smoke (`/game-run`) deploys from `<root>\wt-run` and needs his word; a co-op session
  goes first. Say in the report that the game folder now holds that build.
- Wrap-up without a trunk merge: leave `<root>\wt-run` and the run branch, write the memory resume
  card (the work is not pushed), and list his commands in the final message.

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
`git commit -F "<message file>" -- <each plans/ path this run wrote>`. Naming each path leaves another
session's staged files out of the commit; a bare `-- plans/` would sweep in its unstaged edits under
`plans/`.

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

Baseline `<sha>` on `<trunk>`, <date>. Root `<root>` (worktrees; scratch `<root>\scratch`, temp
`<root>\scratch\tmp`). Models: every role `claude-opus-5-5` unless listed here. Pool 4.
Codex: <asked | not asked>. Second Codex pass on fix diffs: <run | replaced by convergence>.

## Authorizations
| Date | The maintainer's words | Scope | Until |

## Status
| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |

## Log
- <date time>: <event>

## Next
## Blockers
## Dead ends
```

- One row per phase or batch, updated in place: TODO, RUNNING, DONE, STOPPED, BLOCKED. Never leave a
  second RUNNING row behind a DONE one.
- Write the workflow run id into the row when you launch it: resume needs it.
- Next and Blockers are rewritten at every state change; the Log is append-only. Take each Log time
  from `date "+%F %H:%M"` when you write the line, never from memory: a run crosses midnight.
- Dead ends is append-only: one line per approach that failed, with why and the evidence path, so a
  resumed session does not try it again.
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
(`git worktree add --detach "<root>\wt-baseline" <sha>`), then record: build warnings; dotnet suite totals
and the names of failing tests; the Python suite's failure set
(`python -B -m unittest discover -s tools/tests -t .`); `python tools/validate_moduledata.py` error and
warning counts; `python tools/lint_docs.py --fail-on-drift` exit; the hook suite's totals, run with
`run_in_background` because it outlasts a foreground Bash call (the suite runs in the checkout that
holds it), then the Summary line read from the log when it finishes:

`CLAUDE_PROJECT_DIR="<root>\wt-baseline" timeout 1500 bash "<root>\wt-baseline\tools\test_hooks.sh" > "<log>" 2>&1`

and each command's wall time. Later checks compare failure sets by name, never against zero. Remove the
worktree when done.

## REPORT.md

Summary; baseline table; reconcile of the last backlog; top 10 by leverage (the checker's recalibrated
impact, each mapped to an open issue if one exists); lanes; direction (the maintainer's call);
decisions for the maintainer; considered and rejected; not audited; where the evidence is.

## Resume

After compaction, a usage limit, a session end or a restart:

1. Read PROGRESS.md: Status, Next, Blockers, Dead ends, Authorizations. After a compaction the
   SessionStart hook prints the Dead ends lines edited in the last two days.
2. `git worktree list` and each `improve/*` branch tip; compare with the Status rows.
3. A RUNNING row with a run id: read its journal. A run that ended early resumes with the same
   `scriptPath` and `resumeFromRunId`; finished agents replay from cache. A staged build that died
   resumes after its last committed stage.
4. A Codex output under `<root>\scratch\codex\` without the final line `END OF CODEX REVIEW` is running
   or died; re-dispatch only when its process is gone.
5. Never re-launch a phase whose output exists. Continue at the first row not DONE.
6. Tell the maintainer in one short status what was running, what survived and what resumes.

## Liveness watch

A hook "ask" stops an agent's tool call before it starts, so no TIMEOUT catches it. While any agent
runs, and whenever you check on one:

`python tools/improve_ctl.py watch --dir "<transcript dir>" [--stale-min N]` (N defaults to 30)

Repeat `--dir` to watch concurrent workflows as one. With `--wait <seconds>` the same call blocks
until there is something to read ("Launching a workflow and waiting for it"), so the watch runs
every time you wait and needs no loop of its own.

Point it at the running workflow's own folder, `<session>\subagents\workflows\<run id>` (no trailing
backslash, which would escape the closing quote), or for direct
spawns at the session's `subagents` folder. It never looks inside a folder named `workflows` below
`--dir`: a killed workflow's unfinished agents stay "running" in its journal and would read as stale
forever, so the session's folder shows only its direct spawns, and each workflow is watched through its
own folder. Exit 1 on either flag:

- **STALLED-ASK**: the agent's latest PreToolUse hook attachment with permissionDecision "ask" has no
  later tool result for that tool call, so it waits on the maintainer. This works in both folders
  (direct spawns have no journal). Tell him at once, in one line, with the agent and the first words
  of the command. You cannot answer it, and stopping the workflow kills its other agents.
- **STALE**: a running agent's transcript is older than N minutes; read its tail. Only a workflow's
  `journal.jsonl` says which agents are running (an agent whose journal key was started again later is
  "superseded", not running), so in a direct-spawn folder only STALLED-ASK and the age column tell you
  anything.

## Live probe

For a gate change, prove it with a headless session, which does not wait on an ask. Write the prompt to a
file: "This is a harness test of the repository's hooks. Use ONLY the <tool> tool. Run each command below
once, in order, each as its own call. Every command is a dry run. Do not retry, rephrase or work around a
refusal or confirmation; record it and continue. Then print a table: number, ran / denied / asked, first
line of output or hook message verbatim." Then, from the worktree whose hooks are under test:

`cd "<worktree>" && env -u CLAUDE_PROJECT_DIR timeout 900 claude -p --permission-mode bypassPermissions --no-session-persistence < "<prompt file>" > "<out file>" 2>&1`

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
  For a failure that the baseline shows on the trunk, fetch and read
  `git log --oneline <baseline>..origin/<trunk>` first: another session may have fixed it since (#1135
  was filed and closed within two hours for that reason).
- **One issue for one run's refactor items** is enough when they are behaviour-preserving clean-ups
  with a shared record; a bug lead gets its own issue.
- **Draft** with `draft-issues.js` (one `TITLE:`/`LABEL:` file per issue, the `/issue` bug or feature
  sections, `## Status`, `## Decisions needed`), or write a short one yourself from the plan. Never use
  the plan file as the body: it holds executor instructions and local paths. No line that asks for the
  issue itself to be filed or says none exists yet.
- **Check** every draft in full, then `python tools/check_public_text.py "<draft.md>"`. Hold a
  security-sensitive draft for an explicit OK.
- **File** with `python tools/improve_ctl.py file-issue "<draft.md>"`; write the number into the plan's
  Status block and index row in the next plans commit.
- **Comments** (status after review, closing): text in a file, checked, then
  `gh issue comment <n> --body-file "<file>"`; close with `gh issue close <n>`, adding the label
  `triage-needs-ingame` or `triage-blocked-decision` when something is still owed.

## Merge (on the maintainer's word)

1. **Ready list.** Each branch: review verdict READY FOR COMMIT, convergence clean or its residual
   accepted by the maintainer, Codex folded in if run, its decisions applied. Order by dependency.
2. **Integration worktree** outside the repo: `git worktree add -b integrate/<date>
   "<root>\wt-integrate" origin/<trunk>`, which by git's default sets the branch to track
   `origin/<trunk>` (step 10 relies on that). Never merge in the main checkout.
3. **Trunk moved?** `git fetch origin`, then `git rev-list --left-right --count origin/<trunk>...HEAD`;
   a nonzero left count means merge the trunk in first (`merge(improve): <version> - trunk <sha> into
   ...`) and re-run the suites.
4. **Baseline on the integration tip**: record its SHA, dotnet totals and failing tests, the Python
   failure set, the hook suite totals (run as in "Baseline" above, `CLAUDE_PROJECT_DIR` set to the
   integration worktree), the lint exit and the `ai_dashes:` count of the step 6 lint command.
5. **Per branch**: write the message file first. Its subject is `merge(improve): <version> - <what>`,
   at most 72 characters; its body names the issues and what conflicted; check it with
   `python tools/check_public_text.py "<file>"`. Then `python tools/integrate_branch.py --worktree
   "<wt-integrate>" --message-file "<file>" --dry-run <branch>` shows the conflict set and which
   append-only paths the union would take (it applies the same rule to the merged blobs, so a path
   the merge would send to a hand counts as one there too; the seam, duplicate and marker checks run
   only in the real merge), and the same without `--dry-run` merges. The tool never commits, and its
   leftover-marker check runs on every file of every merge. A fix that makes the merged tree green is
   its own commit.
   - **Verify before the commit.** The tool stages and never commits, so run the build and the full
     C# suite on the staged merge (and the touched tool tests when `tools/` changed) and commit only
     on green: a red merge then never becomes a commit. Expect the total to be the last total plus
     the branch's new tests.
   - **Exit 0**: merged and staged, with lessons and REVIEW-LOG unioned. The union takes a conflict
     block only when both sides start with the common base lines (it keeps ours, then theirs' lines
     after the base); any other block sends its path to exit 2. It prints
     `ready to commit: git commit -F "<message file>"`: run that commit yourself through Bash, as
     `git -C "<wt-integrate>" commit -F "<message file>"`, so every PreToolUse commit gate judges it.
   - **Exit 1**: refused before this run started a merge, for example the main checkout, a dirty tree,
     a message file that fails `check_public_text.py`, or a merge already in progress (refused and left
     as it was). Read the reason.
   - **Exit 2**: the merge is left in progress with the listed paths for you. Resolve them by hand
     (single-owner files line by line, language files with row placement parsed, generated files
     regenerated, never merged) and stage those paths. Before the commit, run
     `git -C "<wt-integrate>" diff --cached --check`: a line naming a `leftover conflict marker` is a
     path you have not finished (its whitespace lines are not merge problems). Then commit the same
     way.
   - **Exit 3**, or any other exit: git failed after the merge started, or an unexpected error. Stop,
     report its message, and read `git status` first, since a merge may be in progress.
6. **Verify after each merge**, each as its own call, compared with step 4: the dotnet suite; the Python
   failure set; the worktree's own lint copy (each copy lints the checkout it sits in),
   `python "<wt-integrate>/tools/lint_docs.py" --fail-on-drift --summary --dash-base <step 4 SHA>`
   (the default base, HEAD, sees nothing once the merge is committed), whose exit gates drift only
   (a bare `FOLLOW-UP` line in a merged RCA is a gated finding too, #984, and so is a diff that fails on a resolvable base,
   counted apart as `git_diff_failed:`; a mistyped SHA gives exit 2 with no summary block) and ignores dashes, so also compare its `ai_dashes:` count with step 4's: a higher count is new
   prose with an em or en dash; `validate_moduledata.py` when ModuleData changed; the hook suite when
   `.claude/` or `tools/test_hooks.sh` changed; the no-game replay (`.ai/verification.md`, its three
   RefAsm commands) when C# or tests changed. When a plan adds a gate, replay it on every
   in-flight branch before merging any of them. Stop at the first new failure.
7. **Push**: fetch and repeat step 3, then a plain fast-forward `git push origin HEAD:<trunk>`. Never a
   force push. Record the range.
8. **Trunk check**: no CI runs on the trunk (GitHub CI was removed 2026-10-05). The proof is the step 6
   verification on the integration tip; record its totals.
9. **Issues**: close per "Issues" above, naming the merge.
10. **Cleanup**, in this order, from a query, never a hand list. `git branch -d` refuses a branch
    checked out in a worktree, and judges a branch with no upstream (every `improve/*`) against the
    HEAD of the checkout it runs in; the main checkout's HEAD is the maintainer's unpulled trunk, so
    there it refuses them all and git's hint offers the forced form, a HOOK-ASK.
    1. List the merged branches:
       `git branch --merged origin/<trunk> --format="%(refname:short)" --list "improve/*" "integrate/*"`.
    2. `git worktree remove` each plan worktree that has one of them checked out
       (`git worktree list`), keeping the integration worktree.
       Run `python tools/worktree_audit.py` first and treat its bucket as advice, never permission.
    3. Delete the listed branches other than this run's `integrate/<date>` from the integration
       worktree, whose HEAD is the pushed trunk: `git -C "<wt-integrate>" branch -d <branch> ...`.
    4. `git worktree remove "<wt-integrate>"`.
    5. `git branch -d integrate/<date>`: it tracks `origin/<trunk>` (step 2), which holds it once
       pushed, so git deletes it with a warning that HEAD lacks it.
    6. `git worktree prune`.

    A branch that `-d` still refuses stays, listed for the maintainer; never force a deletion. A
    removed worktree takes its gitignored files with it, so nothing a run still needs (a Codex output
    above all) may live in one.
11. **Record**: a PROGRESS row with hashes, totals and run ids; `improve_ctl.py status` per plan row.

## After a merge: the maintainer's actions

List them in the merge's status message: pull; restart open sessions when hooks or settings changed;
check `/permissions` and `/mcp` after a settings change; a file the merge untracks is deleted from his
checkout by the pull, so back it up byte for byte before he pulls; ports to the other trunk are his.

## Wrap-up

- Every plan row set with `python tools/improve_ctl.py status plans/README.md <num> "<text>"`; no
  RUNNING row left in PROGRESS.md; Next and Blockers current.
- Codex lessons consolidated, once for the run: only a new false-positive pattern or a missed
  category, into `.ai/review-reference.md` "Lessons From Prior Reviews"; durable patterns into the
  lessons files (review-codex Phase 3h). The track record is frozen, so no essay.
- REVIEW-LOG entries this run wrote unnumbered get their numbers, in merge order, in one commit.
- Every "owed" marker the run wrote ("convergence pass owed", "issue to be filed") is closed by the step
  that discharged it, or listed in FOR-MIKE.md.
- Open questions held in memory move to FOR-MIKE.md.
- Worktrees removed, merged branches deleted (the query in Merge step 10), unmerged ones listed for the
  maintainer; the run's memory resume card deleted once its work is pushed.
- The last status: done, build and test, his actions.

## Budget

**A reasonable run** (the shape to use when the maintainer asks to hold the agent count down): one
wave of lanes, six at most; one batch refuter per lane pair and no critic; ten items at most; a
`tier` on every review item, `--stop-on-low`, two convergence rounds at most. The 2026-11-12 run had
that shape, except that it ran without `--stop-on-low`: 67 agents and about 12.5 M subagent tokens
(lanes 1.5 M, refuters 0.8 M, plans 3.3 M, executors 0.9 M, reviews 6.0 M; the figures are in
`plans/_audit/2026-11-12-fable/PROGRESS.md`, "Log"). Say in PROGRESS.md which shape you chose.

Review is the largest cost of a run: about two thirds of the 2026-09-23 sprint's workflow tokens, and
nearly half of the 2026-11-12 run's. To hold it down:

- Give every review item its deep-review risk `tier`; `improve_ctl.py args review` then routes one or
  two lenses, not six.
- Pass `--stop-on-low` unless the maintainer asked for a full review. On 2026-11-12 every round 1
  finding on six branches was LOW, most of them wording in the review's own records, and each cost a
  fix pass and a second round at max effort.
- Make small text items yourself (SKILL.md "Small items are yours").
- Batch a branch's decisions into one follow-up and one second review, and commit per chunk or stage
  so a limit costs little.

The refuters are the cheapest agents of a run for what they prevent: on 2026-11-12 three of them
checked 27 findings, narrowed 21 and refuted part of one before any plan was written
(`plans/_audit/2026-11-12-fable/REPORT.md`, "Summary"). Expect the weekly limit on a multi-day run. Codex bills
outside the Claude budget but runs only when asked.
