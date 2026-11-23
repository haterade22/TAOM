# PR review automation

## Overview

Every pull request to `bannerlord-1.5.x` gets two checks without anyone asking. GitHub Actions builds and tests it
without the game (the PR checks), and a scheduled task on the maintainer's desktop writes a first review of each new
PR head (the watcher). Neither one approves, merges or posts a review; the maintainer still starts the real review
with `/review-outside-pr <N>`, or `/review-outside-pr open` for every open PR (#1543).

## Why This Exists

- **Before:** the GitHub workflows were removed on 2026-10-05 (`c8d69a0dc`), because tests that needed the game
  failed on the hosted runner and nobody read the red runs. A PR showed no checks, and nothing looked at it until
  the maintainer asked a session to.
- **Now:** more people open PRs. The PR checks give each contributor a pass or fail within minutes, and the watcher
  has a report waiting when the maintainer sits down.
- **Without it:** a broken build or a hook change sits unnoticed in a PR until someone happens to review it.

## Architecture

### Layer 1: the PR checks (`.github/workflows/pr-checks.yml`)

Three jobs on `pull_request` to `bannerlord-1.5.x`, and by hand (`workflow_dispatch`):

| Job | Runner | What it runs |
|---|---|---|
| C# build and tests (no game) | `windows-latest` | the three no-game commands in [.ai/verification.md](../../.ai/verification.md): the build against BUTR's reference assemblies (`-p:TaomGameRefs=RefAsm`), the unit tests without `RequiresGame`, `LiveInstall` and `BindingVerification`, and the binding gate without `RequiresGameIL` |
| Python tool tests | `windows-latest` | `python -m unittest discover -s tools/tests -t .` with ruff and ShellCheck 0.11.0 (the download is checked against a pinned SHA-256); the tests that need the game install, Blender or Ghidra skip |
| Doc drift and context budget | `ubuntu-latest` | `python tools/lint_docs.py --fail-on-drift` |

Each test step fails when fewer tests executed than its floor (36,000 unit tests, 740 binding checks, 10,000 tool
tests), because `dotnet test` and `unittest` exit 0 when a filter matches nothing. Skipped tests do not count: the
tool step subtracts unittest's `skipped=` from its `Ran` count. The C# floors sit about 5% under the run of
2026-11-22 (38,012 and 781), the tool floor 7% under it (10,750 executed of 10,884, 134 skipped). The doc job passes `--dash-base HEAD^1`,
the PR's base, because a clean CI checkout gives the default base nothing to compare.

Safety: the jobs run on a GitHub-hosted VM that is thrown away after the run, never on a self-hosted runner, because
`.ai/policy.md` forbids untrusted PR code on the game workstation. The token is read-only, no job uses a secret, and
every action is pinned to a commit SHA. A fork PR from any outside contributor waits for the maintainer's approval
before it runs (the repository's Actions setting `all_external_contributors`).

What it cannot see: the tests that need the game, the live `TAOM_Map` and Armory, the ModuleData validator against
the installed modules, and the game itself. Green here is not `/verify`.

**Keeping it green.** A test that runs engine code fails on the reference assemblies, where every method body
throws. Tag it `[TestCategory("RequiresGame")]` (unit step) or `[TestCategory("RequiresGameIL")]` (binding gate)
with a comment naming the engine call; never catch the exception. The symptoms are listed in
[.ai/verification.md](../../.ai/verification.md). Tag only the tests that run engine code: when a class setup is
what runs it, move the tests that read only metadata to a class with no setup, as `MarshalThrowerBindingTests` did.
After an engine bump the C# job stays red until BUTR publishes the new reference assemblies (the `engine-bump`
skill).

### Layer 2: the watcher (`tools/pr_review_watch.py`)

A Windows scheduled task, `TAOM PR review watcher`, runs `pythonw tools/pr_review_watch.py run` every 30 minutes
while the maintainer is logged on. For each open PR whose head commit it has not reviewed:

1. **Phase 1 in code.** From the GitHub API, with no checkout. Any of these stops the PR:
   - a changed file with no text patch: a binary or an archive (GitHub reports it with no changes), a file too
     large to show, or a rename, which GitHub reports the same way whether its content changed or not;
   - a high-risk path, at any depth and in any case, on the new or the old name of a rename. The list is the
     skill's (`test_the_list_matches_the_review_outside_pr_skill` fails when the two drift apart), plus the
     `tools/` scripts that trunk's hooks, settings and build files name, the `tools/` modules they import, and the
     `tools/` scripts they run or load by a path in a string (`validate_moduledata.py` runs the generators that
     `check_generator_item_refs.py` lists). On 2026-11-22 that made 172 of trunk's 1,385 `tools/` files high
     risk, so a PR that changes one stops; comments and docstrings that name a path count too.
     Such a script runs with `tools/` first on its import path, so a package or another source file in `tools/`
     named like any module it imports, the standard library's included (`tools/json/__init__.py`,
     `tools/_gamedir.pyw`), stops the PR too;
   - two file names that collide on Windows: they differ only in case, or one file has the name of another's
     folder;
   - hidden Unicode (zero-width, direction, invisible-operator, blank-letter, tag and variation-selector
     characters) in the diff, the title, the body or a file name; a byte order mark at the start of a file line is
     not counted. The ranges are the skill's grep (`test_the_ranges_match_the_review_outside_pr_skills_grep`);
   - a file name that is not a plain relative path, or that Windows cannot hold;
   - a file list shorter than the PR's changed-file count (GitHub's compare list stops at 300 files);
   - a base other than `bannerlord-1.5.x`.

   The report lists the reasons and no model is called, since the skill wants those PRs read line by line in a
   session.
2. **The PR as data.** The diff, the PR body, the title and author, and the PR's version of each changed file (as
   text from the contents API) go into `E:/TAOM-PR-Reviews/pr-<N>-<head>/`, named with the whole head SHA. Every PR
   read names the head that the PR list gave: the file list and the diff come from the compare API
   (`compare/<base>...<head>`), the files from the contents API at that head, so a push during a review cannot pair
   one head's diff with another head's files. A new export empties `files/` first. Nothing is checked out, so no
   git hook, build file or test of the PR runs.
3. **A read-only review.** `claude -p` runs from the main checkout (the task script refuses a linked worktree) with:
   - `--tools Read Grep Glob`, so no other tool exists in the session, and every tool that runs, writes, fetches
     or delegates also in `--disallowedTools`;
   - no `--allowedTools`: a bare `Read` there approves a read of any path, while without it only reads inside the
     checkout, the PR folder and the engine source cache need no approval, and `-p` mode refuses the rest;
   - no MCP server (`--strict-mcp-config`), the project settings only (`--setting-sources project`, which approve
     no reads) and `--no-session-persistence`.

   A probe on 2026-11-22 asked such a run to use Bash and Write and to read a file in the PR folder and one outside
   it. It had only Glob, Grep and Read; Bash and Write left no file; the PR folder read worked and the outside read
   was refused. The prompt tells the model that the PR folder is untrusted data and asks for a verdict (LOOKS
   RIGHT, CONCERNS or STOP) on the first line, then findings, questions and what it could not check. Only the
   first line counts, so a fake verdict that the review quotes from the PR further down is never read as the
   verdict. The model is `opus` (`--model` changes it).
4. **Report and notice.** `report.md` in the PR folder, with the model's text under a heading that marks it as
   untrusted, a line in `state.json`, and a Windows notification that names the PR number and the verdict, never
   PR text.

`state.json` keeps the last head reviewed for each PR, and a head that is still the PR's head is not reviewed
again. A new push to the PR is a new head and gets a new review. A failed fetch, a failed model run or an answer
that does not start with a verdict line counts as one attempt; the third attempt for one head writes a STOP report
that names the last error and the `run --pr <N> --force` command, sends a notice, and the head is not tried again.
A failed model run ends the run, so a usage limit or a lapsed login costs one attempt, not one per open PR. The
lock file is created and written in one step (`O_EXCL`), so of a manual run and a scheduled one only one gets
it; the other does nothing, and a lock it cannot read yet is judged by its age on disk. A new attempt removes the
last failed answer (`claude-error.txt`) before the model reads the folder. Each run stamps the lock before each PR, removes only its own lock, and takes over a lock older than
two hours. The PR list asks gh for up to 1,000 open PRs.

### Component Diagram

```
pull request opened or pushed
     |                                   \
GitHub Actions: pr-checks.yml             scheduled task (desktop, every 30 min)
  C# no-game build and tests                pr_review_watch.py run
  Python tool tests                           Phase 1 in code --STOP--> report, no model
  doc drift and budget                        PR files as text -> claude -p (read only) -> report.md
     |                                         notification: "#N: verdict"
  pass or fail on the PR                           |
                     \                            /
                maintainer: /review-outside-pr <N> or open
                (build with the game, deep review, fix, merge)
```

## Configuration

| Setting | Where | Default |
|---|---|---|
| Report folder | `--out`, or `TAOM_PR_REVIEW_DIR` | `E:/TAOM-PR-Reviews` |
| Model | `--model` | `opus` |
| Interval | `register_pr_review_task.ps1 -EveryMinutes` | 30 |
| Test floors | `pr-checks.yml` | 36,000 / 740 / 10,000 |

## Key Files

| File | Purpose |
|------|---------|
| `.github/workflows/pr-checks.yml` | Layer 1: the three no-game jobs on each pull request |
| `tools/pr_review_watch.py` | Layer 2: Phase 1 in code, the read-only model review, the report and state |
| `tools/register_pr_review_task.ps1` | Registers or removes (`-Unregister`) the scheduled task |
| `tools/tests/test_pr_review_watch.py` | The watcher's checks, state, lock, command and runs, with fakes for gh and claude |
| `.claude/skills/review-outside-pr/SKILL.md` | The review that starts from both reports |

## How to use it

- See the watcher's verdicts: `python tools/pr_review_watch.py status`.
- Review one PR again: `python tools/pr_review_watch.py run --pr <N> --force`.
- See what a run would do: `python tools/pr_review_watch.py run --dry-run`.
- Install or remove the task: `pwsh tools/register_pr_review_task.ps1` or `pwsh tools/register_pr_review_task.ps1 -Unregister`.
- The task log: `E:/TAOM-PR-Reviews/watch.log`.

## Known limits

- The PR checks show a trunk failure on every PR until trunk is fixed: a red run on a PR that did not cause it
  still needs a look at trunk.
- The watcher runs only while the desktop is on and the maintainer is logged on; a PR that arrives overnight is
  reviewed within one interval after the next logon.
- Each model review uses the maintainer's Claude usage: one Opus review per new head, so a contributor who pushes
  often costs one review per push. There is no daily cap: the maintainer decided against one on 2026-11-22.
- The watcher polls, so it reviews the head it finds, not every push in between.
- The watcher does not check which branch the main checkout is on. The model is told that its working folder is
  trunk, and the guarded-script list comes from that folder: keep the main checkout on `bannerlord-1.5.x`.
- A fork PR's head is read through the base repository's compare API by SHA. TAOM had no fork PR to test on
  2026-11-22; on a public repository's fork PR the compare and contents reads by SHA gave the PR's files. If
  GitHub refuses one, the third failed attempt writes a STOP report.
- The hidden Unicode check reads the compare diff. Whether GitHub cuts that diff short on a very large
  comparison is not tested.
- Long paths are not enabled on the desktop, and the review folder name holds the whole head SHA, so a changed
  file whose repo path is longer than about 185 characters cannot be written. That fails the attempt, and the
  third failure writes a STOP report. The longest tracked path on 2026-11-22 was 116 characters.
- The project settings load in each model review, so trunk's SessionStart hooks may run there too (not
  measured); none of trunk's tool hooks match Read, Grep or Glob.
- The watcher reads a PR only through its own diff and files; it does not run the deep-review lenses or build
  anything. Those stay in `/review-outside-pr`.

## How to verify in game

Not applicable: nothing here runs in the game. To verify it, open a pull request to `bannerlord-1.5.x` and check
that `gh pr checks <N>` lists the three jobs; within 30 minutes, `python tools/pr_review_watch.py status` lists the
PR and its report exists.

## Changelog

- 2026-11-22: added the PR checks workflow and the watcher (#1543); tagged the tests that need the game. A Codex
  review the same day led to the pinned compare reads, the import-shadow guard, the first-line verdict, the
  collision check, the whole-SHA folder, the 1,000-PR list, the atomic lock and the executed-test floor.
