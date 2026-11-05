# Plan 103: Make the 1.4.x hook gates decide, judge the right worktree, and never deploy from a documented command

> **Executor instructions**: Follow this plan step by step, in order. Run every verification
> command and confirm the expected result before moving on. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. Do not edit `plans/README.md`: report your status
> and the numbers this plan asks you to record to the integrator, who maintains the index.
>
> **Drift check (run first, in the worktree created in Step 0)**:
> `git diff --stat 1b00d1acd..HEAD -- .claude/ .githooks .gitattributes CLAUDE.md AGENTS.md tools/test_hooks.sh tools/lint_docs.py tools/README.md tools/tests/test_lint_docs.py docs/reference/hooks-catalog.md docs/reference/mcp-servers.md docs/features/doc-health-linter.md docs/ai-includes docs/modding docs/reference docs/features`
> Changes from WP101 (troop data, one lessons doc) and WP102 (plan 020: CHANGELOG hooks retired,
> settings, rules, skills, CLAUDE.md, AGENTS.md, `test_hooks.sh`, the hooks catalog, `session-start.sh`)
> are expected. Compare the live files against the excerpts below; STOP only on a real mismatch in a
> line this plan edits.

## Status

- **Priority**: P1
- **Effort**: L (10 commits in three Parts; the largest piece is a hand merge into `tools/test_hooks.sh`)
- **Risk**: MED (after merge the gates really refuse and prompt; every gate stays fail-open on its own faults)
- **Depends on**: WP102 (plan 020 on 1.4.x, which itself follows WP101). STOP if WP102 is not merged (Step 0).
- **Category**: dx / security (harness)
- **Wave / lane**: wave 0, harness lane (WP102 then WP103); runs beside the code lane (WP104, WP105)
- **Executor**: laptop, ordinary Claude Code session started in the main checkout `C:\Users\mikew\source\repos\TAOM`, working in its own worktree
- **Decisions applied**: D1 (no commit stages `CHANGELOG.md`; the CHANGELOG gates are gone), D3 (deny Serena's edit tools, keep its read tools), D8 (subjects carry `v2.0.28`), D10 (plain session executes), D18 (no `merge=union` line here), D19 (git-gate group matches `Bash|PowerShell`), D20 (laptop data: the ModuleData gate result is recorded, not fixed here)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`, 2026-11-04
- **Issue**: port of 1.5.x #647 (output contract), #680 and #690 (anchored registrations), #681 (here-strings). The 1.4.x-only parts (lint `--drift-only`, CLAUDE.md trim, the command sweep, the `bannerlord-1.5.x` push guard) have no issue: orchestrator creates the issue before implementation lands.

## Why this matters

Every later work package in this programme commits through these gates, and today they do not
work. Seven PreToolUse gates and the `/freeze` check print a top-level `permissionDecision`, which
Claude Code does not read, so the `v2.0.28` subject gate, the files-tracked, ModuleData, doc-drift
and native-DLL gates, both confirm prompts and the `/freeze` boundary decide nothing. A command with
one character outside cp1252 empties every Python-parsed gate; a persistent `cd` into a subdirectory
stops every hook (relative registrations); the commit gates judge the main checkout even when the
commit runs in an executor's worktree; the doc-drift gate's full lint takes 25 s on this laptop
against its 20 s inner bound; and `/verify`, `/review-codex` and 77 documented commands build
without `-p:ModuleId=`, which deploys into the game install on this line. After this plan the gates
refuse what they should, read the repository a commit runs in, answer inside their budgets, and
every documented build is non-deploying; a tracked git hook applies the label rule to terminal and
IDE commits too.

## Current state (read this session on `origin/bannerlord-1.4.5` 1b00d1acd unless marked)

**Registrations.** `.claude/settings.json:28-86`: one PreToolUse group, `"matcher": "Bash"`, holding
10 hooks named by relative paths, for example `:34` `"command": ".claude/hooks/block-no-verify.sh"`.
All 28 registrations in the file are relative; none uses `CLAUDE_PROJECT_DIR`. There is no
`permissions` block (the 9 git and filesystem MCP denies live in the tracked
`.claude/settings.local.json`; none is Serena's). After WP102 the group holds 9 hooks and the file
26 registrations (WP102 retires `check-changelog-changed.sh` and `check-changelog-updated.sh`).
The 5 skill-frontmatter registrations (`/freeze`, `/investigate`) already use
`bash ${CLAUDE_PROJECT_DIR}/.claude/skills/freeze/check-freeze.sh`.

**Output contract.** Top-level emitters (`git grep -F '{"permissionDecision'`): `block-broad-git-add.sh:145`,
`block-dangerous-git.sh:113`, `check-claude-files-tracked.sh:87`,
`check-commit-subject-version.sh:73` (`print(json.dumps({"permissionDecision": "deny", "message": msg}, ...))`),
`check-doc-config-drift.sh:97,119`, `check-moduledata-validation.sh:112,130`, `check-native-dll-crt.sh:92,120`,
`.claude/skills/freeze/check-freeze.sh:155`, plus `check-changelog-changed.sh` (gone after WP102).
`.claude/rules/harness-facts.md:33` documents the wrong form. `tools/test_hooks.sh` section 6 greps the
text `"permissionDecision":"deny"`, which matches both forms, so the suite stays green.

**Encoding.** `.claude/hooks/_pybin.sh:146-147` ends with `PYBIN=$(taom_resolve_python || true)` /
`export PYBIN`; nothing sets `PYTHONIOENCODING`.

**Repository.** Every commit gate runs `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"` (for example
`check-commit-subject-version.sh:53`, `check-doc-config-drift.sh:55`, `check-moduledata-validation.sh:55`,
`block-broad-git-add.sh:125`) and then reads that tree's index and `Main/_Module/SubModule.xml`.

**Here-strings.** `block-broad-git-add.sh:118`, `block-dangerous-git.sh:103`, `check-doc-config-drift.sh:78`,
`check-moduledata-validation.sh:73`, `check-native-dll-crt.sh:68`, `check-verification-evidence.sh:42`,
`detect-docs-gaps.sh:43`, `mark-verification-run.sh:74` (`done <<< "$(printf '%s' "$COMMAND" | tr ';&|' '\n')"`),
`validate-push.sh:48` (`read -r -a TOKENS <<< "$CLEAN"`, which reads the first line only). Laptop bash:
GNU bash 5.3.9. The #681 hang itself was not reproduced on this laptop.

**Push guard.** `validate-push.sh:113-118` protects `master|main|bannerlord-1.4.5` only; `:121-124` exits 2
on a force push to one of them.

**Doc drift gate.** `check-doc-config-drift.sh:91` runs `timeout -k 2 20 "$PY" tools/lint_docs.py --fail-on-drift`
(registered at 30 s). Measured this session on the laptop: the full `--fail-on-drift` run takes
25.0 s; its three gating checks (`check_config_example_drift`, `check_version_consistency`,
`check_claude_md_budget`) take about 0.45 s together. `tools/lint_docs.py` has no `--drift-only` flag.

**CLAUDE.md budget.** `tools/lint_docs.py:49` caps CLAUDE.md at 46,000 bytes measured on disk
(`stat().st_size`, `:812`). CLAUDE.md is 46,283 bytes as a blob with 421 lines, 46,704 bytes on disk
with CRLF. `python tools/lint_docs.py --fail-on-drift --summary` prints `claude_budget: 1` and exits 1.

**ModuleData gate.** `check-moduledata-validation.sh:97-106` runs the validator with 19 `--code` flags
under `timeout -k 2 45` (registered 60 s). Measured this session: that command takes 22.1 s here and
exits 1 with 3,423 `BROKEN_ITEM_REF` (the laptop still has the old Armory; WP101's maintainer step,
D20). Once Part B makes the gate's output real, every commit staging ModuleData XML on this laptop
is refused until that changes. WP103 stages no ModuleData.

**Non-deploying commands.** `Main/TAOM.csproj:133-136` says `DisableModuleCopy` does NOT gate
`CopyModule`; only an empty `-p:ModuleId=` skips it. `.claude/skills/verify/SKILL.md:19`
(`dotnet build Main --no-restore 2>&1`) and `:27` (`dotnet test TAOM.Tests --no-build 2>&1`);
`.claude/skills/review-codex/SKILL.md:239-240` (`dotnet build TAOM.Tests`, `dotnet test TAOM.Tests`).
With the 1.5.x linter pattern (35ff005dc `check_unflagged_dotnet`) plus bare `dotnet test` lines,
77 lines in 35 files under `docs/modding`, `docs/reference`, `docs/ai-includes`, `.claude/skills`,
`.claude/rules`, `.claude/agents` lack `-p:ModuleId=`; `.ai/`, `AGENTS.md` and `CLAUDE.md` have none.
`docs/features` has 11 more in 10 files.

**Git hooks.** No `.githooks/` on the ref; `git config --get core.hooksPath` is unset on this laptop.
`AGENTS.md` (3,690 bytes) has no "Git and commits" section; the commit rules live in CLAUDE.md
"Commits". `AGENTS.md:27` reads "Verify evidence before making claims".

**Already on 1.4.x**: none of `hookSpecificOutput`, `PYTHONIOENCODING`, `taom_git_repo_dir`,
`_shellwords.py`, `.githooks`, `check_commit_msg.py`, `prompt-fixed.md`, a `bannerlord-1.5.x` push
guard or a Serena deny (git grep this session).

**Engine signatures.** None. This plan binds no TaleWorlds type or member (files touched: `.claude/`,
`.githooks/`, `tools/`, docs, CLAUDE.md, `.gitattributes`).

**Harness facts this plan relies on** (code.claude.com/docs/en/hooks, read 2026-11-04): a PreToolUse
decision goes under `hookSpecificOutput` (`permissionDecision` allow/deny/ask/defer,
`permissionDecisionReason`); "Handlers run in the current directory"; a hook that fails to start or
prints invalid JSON is a non-blocking error and the action goes ahead; "Claude never sees stderr from
a hook that exits 0"; `cwd` "is the worktree root after Claude enters a worktree, and the new directory
after Claude runs `cd`"; `${CLAUDE_PROJECT_DIR}` "stays put"; `Bash` matches only the Bash tool and
`Bash|PowerShell` covers the PowerShell tool too. The settings reference lists `attribution.commit`
("Change or hide the trailer Claude Code adds to commits"), `attribution.pr` and `attribution.sessionUrl`.
Settings edits to `permissions` and `hooks` reload without a restart. That a top-level
`permissionDecision` is ignored was observed live on the 1.5.x line (#647); it is re-proved here only
by the maintainer's live check after merge.

## Conventions that bind this plan

- `.claude/rules/hook-authoring.md`: a gate fails OPEN on its own fault but never fails SILENT (say so
  on stderr or with an `ask`); bound external work inside the script below the registered timeout and
  treat rc 124 as "not checked", never as a pass; never spell it `python3`; source `_pybin.sh` before
  the first `"$PYBIN"`.
- `.claude/rules/harness-facts.md`: TAOM hooks must fail open; every registration carries an explicit `timeout`.
- Config protection (decisions brief): `.claude/settings.json`, `.claude/settings.local.json`,
  `Directory.Build.props` and `docs/adrs/*.md` are edited by the maintainer only. Part C hands the
  settings change to the maintainer; never apply it yourself by any route (`git apply`, a script, a cherry-pick).
- TDD: each step adds or extends its failing test first, shows it RED, then makes it GREEN.
- ADR-002, ADR-003, ADR-004, ADR-005, ADR-007, the MCM json2 rule and `.claude/rules/harmony-patches.md`
  govern C#; this plan changes no C#, so they are not exercised.
- Prose you write (commit bodies, docs, rules): no em or en dash characters; no AI attribution line.

## Commands you will need

Every Bash call runs from the worktree root and sets two variables. Start each one with:
`cd C:/Users/mikew/source/repos/taom-port/103 && export MSYS_NO_PATHCONV=1 PYTHONIOENCODING=utf-8 && `
(`MSYS_NO_PATHCONV=1` stops Git Bash mangling `git show <ref>:.claude/...`; a `cd` outside the main
checkout does not persist between calls, so repeat it.) Run git only through the Bash tool (D19). Pass
`timeout: 600000` on Bash calls that run the hook suite, the tool tests, `lint_docs.py` without `--drift-only`,
the ModuleData validator or `dotnet`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (BHA0001 Patch88 warning is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed; total equals the Step 0 baseline (no C# changes) |
| Hook suite | `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh` | ends `N passed, 0 failed` (see Step 0 for a baseline that is not clean, and the note below the table) |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no failure the Step 0 baseline lacked; `Ran` = baseline + new tests |
| One tool test file | `python -m unittest discover -s tools/tests -t . -p "<file>.py"` | as stated per step |
| Doc gate | `python tools/lint_docs.py --fail-on-drift --summary` | exit 0 after Step A4 |
| Instruction contract | `python tools/reviewctl.py lint` | exit 0 |
| Config audit | `python tools/audit_claude_config.py --min HIGH` | no HIGH finding the Step 0 baseline lacked |
| Message check (after A1) | `python tools/check_commit_msg.py <msgfile>` | exit 0, no output |

Hook suite notes: "Run the suite" in any step means the Hook suite command above. It clears `MSYS_NO_PATHCONV` because the
sandbox blocks this plan adds call `git -C "$SANDBOX/..."` with a `/tmp` path that `git.exe` can only read with path conversion
on; with the prefix's variable set, 7b reports `did not deny both files ... {}` (reproduced while reviewing this plan). A new FAIL
whose text holds `took` or `HUNG` is machine load, not a defect (a trivial-payload row went over its 3000 ms limit in two of
seven reviewer runs): re-run the suite once before treating it as a failure or a STOP.

Never `./build.ps1`. Never two builds at once in this worktree. This plan changes no C# and nothing the C#
build reads, so run Restore, Build and Test at Step 0 and at the end of each Part rather than after every commit.

## Scope

**In scope** (the only files you modify):
- `.claude/hooks/`: `_pybin.sh`, `_shellwords.py` (new), `block-broad-git-add.sh`, `block-dangerous-git.sh`,
  `block-no-verify.sh`, `check-claude-files-tracked.sh`, `check-commit-subject-version.sh`,
  `check-doc-config-drift.sh`, `check-moduledata-validation.sh`, `check-native-dll-crt.sh`,
  `check-verification-evidence.sh`, `detect-docs-gaps.sh`, `mark-verification-run.sh`, `validate-push.sh`
- `.claude/skills/freeze/check-freeze.sh`, `.claude/skills/freeze/SKILL.md`, `.claude/skills/verify/SKILL.md`,
  `.claude/skills/review-codex/SKILL.md`, `.claude/skills/review-codex/references/prompt-fixed.md` (new)
- `.claude/rules/harness-facts.md`, `.claude/rules/hook-authoring.md`
- Command lines only (Step A3): the `.md` files the new test lists under `.claude/skills`, `.claude/rules`,
  `.claude/agents`, `docs/ai-includes`, `docs/modding`, `docs/reference`, plus the 11 `docs/features` lines named in A3
- `.githooks/commit-msg` (new), `.gitattributes`, `tools/check_commit_msg.py` (new), `tools/lint_docs.py`,
  `tools/test_hooks.sh`, `tools/README.md`
- `tools/tests/test_check_commit_msg.py` (new), `tools/tests/test_shellwords.py` (new),
  `tools/tests/test_dotnet_commands_flagged.py` (new), `tools/tests/test_lint_docs.py`
- `CLAUDE.md`, `docs/reference/hooks-catalog.md`, `docs/reference/mcp-servers.md`, `docs/features/doc-health-linter.md`
- `.claude/settings.json`: changed ONLY by the maintainer in Part C; you stage and commit the maintainer's change

**Out of scope** (do not touch):
- `CHANGELOG.md` (D1), `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/`, `.mcp.json`
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, any C#, any ModuleData
- `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, review records, `docs/INDEX.md`, `docs/reference/feature-map.md` (WP142/143/144 sync these)
- `session-start.sh`, the Stop hooks' reminder channel, `_pushjudge.py`, `taom_hook_command`, the plan 011/013/027 gate rewrites (WP116)
- `docs/ai-includes/git-and-commits.md` (1.5.x-only; do not create it), `plans/` (integrator)
- The main checkout's uncommitted `.serena/project.yml` and `stash@{0}`

## Git workflow

- Worktree (Step 0): `git worktree add C:/Users/mikew/source/repos/taom-port/103 -b port148/103-executor-safety-harness bannerlord-1.4.5`
- Commit on this branch only. Never push, open a PR, tag, merge, or touch another branch.
- Port method: `git cherry-pick -n <sha>` only for A2. Other steps take file states with
  `git show <sha>:<path> > <path>` or apply a named hunk with `git diff <sha>^ <sha> -- <paths> | git apply -`.
  Never `git checkout <sha> -- <path>` (the `--` form is what `block-dangerous-git.sh` prompts on once it works).
- Stage explicit paths with `git add <paths>` in one Bash call; commit in the NEXT sequential call with
  `git commit -F C:/Users/mikew/source/repos/taom-port/scratch/103/msg.txt` (a file outside the repo). In this
  dedicated worktree a commit without a pathspec is safe. Never `git add -A`, `-u` or `.`; never
  `--no-verify`, `-C`, `-c`, `--amend --no-edit` on these commits.
- From A1 on, run `python tools/check_commit_msg.py C:/Users/mikew/source/repos/taom-port/scratch/103/msg.txt`
  before each commit: exit 0. The trunk's own gates do not judge your commits while you work (they are
  the inert pre-WP103 ones), so this check is yours.
- Dash check, after staging and before every commit that touches markdown (must print `0`):
  `git diff --cached -U0 -- '*.md' | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); print(sum(1 for l in t.splitlines() if l[:1]=='+' and ('\u2014' in l or '\u2013' in l)))"`
  A nonzero count means an added or edited markdown line carries an em or en dash: rewrite it (rule S6).
- Run `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh` before you stage, not after: its 4b section times the gates against this
  worktree's index.
- Edit files with the Edit or Write tool, or with a script file, never with a Bash command whose own text holds a
  force push to a trunk (`git ... push --force ... bannerlord-1.4.5`) or `git ... --no-verify`: the trunk's
  `validate-push.sh` and `block-no-verify.sh` are live exit-2 gates today and refuse such a Bash call even when it
  only writes a test row into a file. The 7c, 7h and 7j blocks contain exactly that text.
- Commit message template (CLAUDE.md "Commits"; each step gives the subject and body):

```
<type>(<scope>): v2.0.28 - <description, 72 characters at most for the whole line>

<Body for 1.4.x readers, wrapped at 72, no em or en dashes, no
Co-Authored-By or "Generated with" line.>

Research: <what was checked, when relevant>
Not-tested: <what needs a live check, when relevant>
Ported-from: <40-hex sha>   (one line per included 1.5.x commit)
```

## Part A: commit rules and non-deploying commands (4 commits)

### Step 0: Preflight (no commit)

1. From the main checkout: `cd C:/Users/mikew/source/repos/TAOM && git worktree add C:/Users/mikew/source/repos/taom-port/103 -b port148/103-executor-safety-harness bannerlord-1.4.5 && mkdir -p C:/Users/mikew/source/repos/taom-port/scratch/103`
   (the scratch folder, outside the repo, holds your commit message file and the Part C script).
2. Preconditions, each in the worktree (prefix as in Commands):
   - `git merge-base --is-ancestor 1b00d1acd HEAD && git merge-base --is-ancestor 8df90444c HEAD && echo WP101-OK` prints `WP101-OK`.
   - `test ! -e .claude/hooks/check-changelog-changed.sh && test ! -e .claude/hooks/check-changelog-updated.sh && test -f tools/changelog_from_commits.py && echo WP102-OK` prints `WP102-OK`.
   - `grep -c check-changelog .claude/settings.json` prints `0`.
   - `grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml` prints `<Version value="v2.0.28"`.
   - `python -c "import json; g=json.load(open('.claude/settings.json',encoding='utf-8'))['hooks']['PreToolUse'][0]; print(g['matcher'], [h['command'].rsplit('/',1)[-1] for h in g['hooks']])"`
     prints `Bash` and exactly these 9 names: block-no-verify.sh, validate-push.sh, check-claude-files-tracked.sh,
     check-commit-subject-version.sh, block-dangerous-git.sh, block-broad-git-add.sh,
     check-moduledata-validation.sh, check-native-dll-crt.sh, check-doc-config-drift.sh.
   - `git grep -c -e hookSpecificOutput -e PYTHONIOENCODING -e taom_git_repo_dir -e 'decision_of()' -- .claude tools/test_hooks.sh`
     prints nothing (exit 1): no part of this plan exists yet.
   - `grep -c 'Verify evidence before making claims' AGENTS.md` prints `1`.
3. Baselines. Record each result for your final report (not in a repo file):
   - `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh 2>&1 | tail -4` (the `N passed, M failed` line, and every FAIL name if M > 0)
   - `python -m unittest discover -s tools/tests -t . 2>&1 | tail -4` (`Ran N tests`, OK or the failure names)
   - `python tools/lint_docs.py --fail-on-drift --summary; echo rc=$?` (planning: `claude_budget: 1`, rc=1)
   - `python tools/audit_claude_config.py --min HIGH 2>&1 | tail -6`; `python tools/reviewctl.py lint; echo rc=$?` (rc=0)
   - `time python tools/lint_docs.py --fail-on-drift > /dev/null` (planning: 25.0 s)
   - The ModuleData gate's validator command: copy the `timeout -k 2 45 "$PY" tools/validate_moduledata.py --code ...`
     line from `.claude/hooks/check-moduledata-validation.sh`, replace `"$PY"` with `python`, prefix `time`, and record
     the time, the exit code and the summary count (WP101's baseline records exit 0; planning, before the Armory refresh: 22.1 s, rc 1, 3,423 `BROKEN_ITEM_REF`). A non-zero
     exit is a STOP: every ModuleData commit would be denied once Part B lands.
   - Restore, Build and Test (Commands table): record the totals.

If the hook-suite baseline has failures, keep their names: at the end every check that passed in the
baseline must still pass and every section this plan adds must pass.

### Step A1: a tracked commit-msg hook (port of 0382d5d2a; hook file at its 1.5.x tip state)

Method: file transplant plus two citation edits. RED first.

1. `git show 0382d5d2a:tools/tests/test_check_commit_msg.py > tools/tests/test_check_commit_msg.py`
2. RED: `python -m unittest discover -s tools/tests -t . -p "test_check_commit_msg.py" 2>&1 | tail -3`
   shows an import error for `check_commit_msg`.
3. `git show 0382d5d2a:tools/check_commit_msg.py > tools/check_commit_msg.py`, then edit its two citations:
   line 2 `(AGENTS.md "Git and commits")` becomes `(CLAUDE.md "Commits")`; line 86
   `Commit refused (AGENTS.md 'Git and commits').` becomes `Commit refused (CLAUDE.md 'Commits').`
   Its `AI_TRAILER` regex already equals the one Part B gives the subject gate (do not change it).
4. `mkdir -p .githooks && git show 434d1d795:.githooks/commit-msg > .githooks/commit-msg`. This is the
   1.5.x tip version of the 0382d5d2a hook: it skips a Microsoft Store `python` alias and fails open when
   the checker crashes. Edit line 5: `POSIX sh, and never a Store alias: pre-commit says why.` becomes
   `POSIX sh, and never a Store alias (hook-authoring.md, "Never spell it python3").`
5. `.gitattributes`: after the line `*.bash  text eol=lf` add
   `# Git hooks have no extension, and bash refuses a CRLF shebang line.` and `.githooks/* text eol=lf`.
6. `docs/reference/hooks-catalog.md`: directly below the paragraph that starts `Skill-inline (not in` and above the
   `---` rule that precedes the `<!-- backlinks-start` block (never inside that generated block), add a blank line and:

```
## Git hooks (every committer)

The hooks above run only for the commands Claude runs. The repository's own git hooks live in the
tracked folder `.githooks/`, which `core.hooksPath` names (set once per clone with
`git config core.hooksPath .githooks`; a worktree whose branch predates the folder runs no hook).

| Hook | What it does |
|---|---|
| `commit-msg` | Runs `tools/check_commit_msg.py` on every commit and merge message, terminal and IDE commits included: the `<type>[(scope)]: vX.Y.Z - <description>` subject with the `<Version>` of the tree being committed, at most 72 characters, and no AI attribution line. A `fixup!`, `squash!` or `amend!` subject passes. Refuses with exit 1 and names each problem; fails open, saying so, when Python or the checker is missing, when the only Python is a Microsoft Store alias, or when the checker crashes. A merge therefore needs a labelled message (`git merge --no-ff -m "chore: vX.Y.Z - merge <branch>" <branch>`). Tests: `tools/tests/test_check_commit_msg.py`. |
```

7. `tools/README.md`, section `## Validation`: directly below the `test_hooks.sh` row add
   `` | `check_commit_msg.py` | **Checks a commit message** (pure stdlib), run by the git hook `.githooks/commit-msg` on every commit and merge (`core.hooksPath` = `.githooks`): the version-labelled subject with the `<Version>` of the tree being committed (the staged `SubModule.xml` when staged), at most 72 characters, and no AI attribution line. Exit 1 refuses with the reasons. The same rules as `.claude/hooks/check-commit-subject-version.sh`, for terminal and IDE commits. Tested by `tools/tests/test_check_commit_msg.py`. | `<message file>` | ``
8. GREEN: the command of item 2 prints `Ran 14 tests` and `OK`.
9. Stage, then record the executable bit and line endings:
   `git add .githooks/commit-msg .gitattributes tools/check_commit_msg.py tools/tests/test_check_commit_msg.py docs/reference/hooks-catalog.md tools/README.md && git update-index --chmod=+x .githooks/commit-msg`
   Verify: `git ls-files -s .githooks/commit-msg` starts with `100755`; `git ls-files --eol .githooks/commit-msg` shows `i/lf` and `attr/text eol=lf`.
10. Commit. Subject `feat(git): v2.0.28 - a commit-msg hook checks every committer`. Body: terminal and IDE
    commits now meet the subject-gate rules; what `.githooks/commit-msg` checks (label with the committed
    tree's version, 72 characters, no AI attribution, merges included); it fails open on a missing Python,
    a Store alias or a crashing checker; it runs only after a clone sets `git config core.hooksPath .githooks`
    (a maintainer step), and merges then need a labelled message. Trailers:
    `Not-tested: whether git cherry-pick or git revert run commit-msg`,
    `Ported-from: 0382d5d2a435c35a16c8d4c8da6017fcf258176a`, `Ported-from: 434d1d795465fe9b78c06decd31db5a26558132c`.

**Verify**: `git show --stat HEAD` lists exactly the 6 staged paths; `git log -1 --format=%s` matches the subject.

### Step A2: non-deploying /verify and /review-codex (cherry-pick of 785b2703e)

Method: `git cherry-pick -n 785b2703e`. Simulated this session onto 1b00d1acd: clean (`git merge-tree` exit 0).
WP102 edits `verify/SKILL.md` Step 5 and the Output block, which this commit does not touch. If the
pick conflicts in `verify/SKILL.md`, keep both sides (WP102's Step 5 and Output text, 785b2703e's
Steps 1 and 2) and `git add` it. A conflict in any other path is a STOP.

The pick changes `verify/SKILL.md` Steps 1 and 2 to `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`
and `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (no `--no-build`), changes
`review-codex/SKILL.md` 3f to both flags, moves the ID cheatsheet and lessons blocks into the new
`references/prompt-fixed.md`, rewrites 3h to point at `.ai/review-reference.md` (its "Lessons From
Prior Reviews", "What Codex does especially well" and "Look harder here" headings exist on 1.4.x)
and `docs/reviews/codex-track-record.md`. Then adapt four things for 1.4.x:

1. `.claude/skills/review-codex/SKILL.md`: replace `AGENTS.md's "Evidence, never invention" rule` with
   `AGENTS.md's "Verify evidence before making claims" rule` (the 1.5.x string is absent from 1.4.x AGENTS.md).
2. Same file, Phase 3h: delete item 3, the two lines starting `3. In an `/improve` run the review lead lists these`
   (the 1.4.x `/improve` has no run protocol until WP138).
3. `.claude/skills/review-codex/references/prompt-fixed.md`: replace
   ``Every Codex adversarial prompt carries these two blocks as written. `/review-codex` Phase 2d pastes them,``
   + newline + ``and `python tools/improve_ctl.py codex-prompt` reads them from here; edit them only here. Each block is``
   with ``Every Codex adversarial prompt carries these two blocks as written. `/review-codex` Phase 2d pastes them;``
   + newline + ``edit them only here. Each block is`` (`tools/improve_ctl.py` does not exist on 1.4.x).
4. `.claude/skills/verify/SKILL.md`: replace `(AGENTS.md "Commands")` with `(.ai/verification.md)` (1.4.x AGENTS.md has
   only "Start here", "Always", "Code Review Rules" and "Codex workflow skills"; `.ai/verification.md` holds both flags).

Test: none new (docs). Verify:
- `grep -c 'ModuleId=' .claude/skills/verify/SKILL.md .claude/skills/review-codex/SKILL.md` prints a count of 2 for each file (`<path>:2`).
- `grep -c 'AGENTS.md "Commands"' .claude/skills/verify/SKILL.md` prints 0.
- `grep -c -e 'Evidence, never invention' -e improve_ctl .claude/skills/review-codex/SKILL.md .claude/skills/review-codex/references/prompt-fixed.md` prints 0 for both.
- `python tools/lint_docs.py --quick --summary` shows `dead_links: 0`.

Stage `.claude/skills/verify/SKILL.md .claude/skills/review-codex/SKILL.md .claude/skills/review-codex/references/prompt-fixed.md`.
Commit. Subject `docs(skills): v2.0.28 - non-deploying builds in /verify and Codex`. Body: both skills built and
tested without `-p:ModuleId=`, so each run copied into the game install on this line (the csproj does not gate
the copy on `DisableModuleCopy`) and failed while Bannerlord was open; both flags now; `/verify` no longer tests
a stale DLL with `--no-build`; the Codex prompt's fixed blocks live in `references/prompt-fixed.md`; 3h points at
the 1.4.x lessons homes; the complete-read probe names an AGENTS.md rule that exists here.
`Ported-from: 785b2703e897fe3c3ccf01b8e7d9b847074d7bd1`.

### Step A3: every documented build and test passes both flags (sweep; ModuleId hunk of 1d1abd694)

Method: new test first, then a hand sweep.

1. Create `tools/tests/test_dotnet_commands_flagged.py` with exactly this content:

```python
"""Every documented `dotnet build` or `dotnet test` of TAOM passes -p:ModuleId= (WP103, 2026-11).

On the 1.4.x line Main/TAOM.csproj does not gate the module copy on DisableModuleCopy, so a documented
build or test without -p:ModuleId= deploys into the game install, and fails while Bannerlord runs.
TARGET is the pattern of tools/lint_docs.py check_unflagged_dotnet on the 1.5.x line (35ff005dc), over
the same folders plus .ai/, .agents/, AGENTS.md and CLAUDE.md; BARE adds a command line that names no
project (`dotnet test --filter X`), which builds the solution in the repository root.
"""
import pathlib
import re
import unittest

REPO = pathlib.Path(__file__).resolve().parents[2]
DIRS = ("docs/modding", "docs/reference", "docs/ai-includes", ".claude/skills", ".claude/rules",
        ".claude/agents", ".ai", ".agents")
FILES = ("AGENTS.md", "CLAUDE.md")
TARGET = re.compile(
    r"\bdotnet\s+(?:test|build)\s+(?:-\S+\s+(?:[^-\s]\S*\s+)?)*[\"']?(?:\S*TAOM\S*|Main\b\S*)")
BARE = re.compile(r"^\s*(?:\$\s+)?dotnet\s+(?:test|build)\b")
CONTINUATIONS = ("\\", "`", "^")


def unflagged(text):
    """1-based numbers of the lines holding a TAOM build or test command with no ModuleId; a command
    continued with \\, ` or ^ is read whole."""
    lines = text.splitlines()
    hits = []
    for i, line in enumerate(lines):
        if not (TARGET.search(line) or BARE.match(line)):
            continue
        whole, j = line, i
        while lines[j].rstrip().endswith(CONTINUATIONS) and j + 1 < len(lines):
            j += 1
            whole += " " + lines[j]
        if "ModuleId" not in whole:
            hits.append(i + 1)
    return hits


class UnflaggedTests(unittest.TestCase):
    def test_commands_with_and_without_the_flags(self):
        self.assertEqual(unflagged("dotnet test TAOM.Tests --filter X"), [1])
        self.assertEqual(unflagged("dotnet build Main --no-restore"), [1])
        self.assertEqual(unflagged("dotnet test --filter X"), [1])
        self.assertEqual(unflagged("dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter X"), [])
        self.assertEqual(unflagged("dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId="), [])
        self.assertEqual(unflagged("the dotnet test runner reports it"), [])

    def test_a_continued_command_is_read_whole(self):
        self.assertEqual(unflagged("dotnet test TAOM.Tests \\\n  -p:DisableModuleCopy=true -p:ModuleId="), [])
        self.assertEqual(unflagged("dotnet test TAOM.Tests `\n  --filter X"), [1])

    def test_repository_docs_pass_both_flags(self):
        paths = [p for d in DIRS for p in sorted((REPO / d).rglob("*.md"))]
        paths += [REPO / f for f in FILES]
        found = []
        for p in paths:
            if p.is_file():
                for n in unflagged(p.read_text(encoding="utf-8", errors="replace")):
                    found.append(f"{p.relative_to(REPO).as_posix()}:{n}")
        self.assertEqual(found, [], "add -p:DisableModuleCopy=true -p:ModuleId= to: " + ", ".join(found))


if __name__ == "__main__":
    unittest.main()
```

2. RED: `python -m unittest discover -s tools/tests -t . -p "test_dotnet_commands_flagged.py" 2>&1 | tail -5`:
   two tests pass, `test_repository_docs_pass_both_flags` fails listing the lines (planning: 77 lines in
   35 files on 1b00d1acd; A2 already fixed 4 of them in 2 files, so expect about 73 in 33, give or take WP102's edits).
3. Fix every listed line with these rules:
   - S1. A runnable command (in a code block or backticks): insert ` -p:DisableModuleCopy=true -p:ModuleId=`
     right after the project or solution argument (`TAOM.Tests`, `TAOM.Tests/TAOM.Tests.csproj`, `Main`,
     `Main/TAOM.csproj`, `TAOM.sln`); delete a now-duplicate lone `-p:DisableModuleCopy=true` from the same
     command. Leave `--filter`, `--no-build`, `--no-restore`, `-c` and redirections as they are.
   - S2. A command with no project (`dotnet test`, `dotnet test --filter ...`, `dotnet test --collect:...`):
     make it `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` followed by its options.
   - S3. Prose that names the unflagged form on purpose (for example `docs/ai-includes/new-culture-authoring.md`,
     "`dotnet build Main` may fail with ... UnauthorizedAccessException"): add the words `without -p:ModuleId=`
     after the command instead of changing it.
   - S4. A recorded result (for example `docs/reference/lotrlome-warg-changes.md` "7,716 passed", `module-backup-sweep.md`
     "7,770 passed"): add the flags to the command cell; the count stays.
   - S5. Where text around a command says "the flag" (singular) about `DisableModuleCopy`, make it say both
     flags (for example `.claude/skills/improve/SKILL.md` Hard Rule 2, `docs/ai-includes/agent-operating-manual.md`
     table notes).
   - S6. Never leave an em or en dash on a line you edit: replace it with a colon, a comma or parentheses as the
     sentence needs (this applies to every markdown line this plan touches; see the dash check in Git workflow).
4. Also fix these `docs/features` lines by S1 or S2 (not covered by the test, because the 1.5.x tip still has
   unflagged feature docs that later ports bring in): `battle-scenes.md:9`, `clan-heraldry.md:134`,
   `culture-marketplace.md:120`, `faction-map.md:100`, `localization.md:117`, `lord-party-templates.md:240`,
   `menu-link-colors.md:186`, `player-switcher.md:474` and `:475`, `settlement-nameplate-relation.md:180`,
   `volunteer-recruitment.md:181` (line numbers on 1b00d1acd; leave the prose at `menu-link-colors.md:112`).
5. GREEN: the item 2 command prints `Ran 3 tests` and `OK`;
   `git grep -nE 'dotnet (build|test)' -- docs/features | grep -v ModuleId` prints only `menu-link-colors.md:112`.

Stage the test file and every edited `.md` by explicit path (`git status --porcelain` lists them).
Commit. Subject `docs(harness): v2.0.28 - every documented build and test skips deploy`. Body: the docs, skills,
rules and agent prompts now pass both flags, because without `-p:ModuleId=` the post-build copy deploys into the
game install on this line (an agent following one could overwrite the installed TAOM.dll, or fail while the game
runs); the new tool test keeps the 1.5.x linter's folders clean and adds bare `dotnet test` lines; feature docs
were swept once and are not gated. `Ported-from: 1d1abd6940e7765f3d08247afa3ec68acda12d23`,
`Ported-from: 35ff005dc5bb80870e49e46122b69ae57ac1a916`.

### Step A4: CLAUDE.md under its byte cap, plus the pathspec and commit-msg rules (385b7adf6)

The failing test is the doc gate itself: `python tools/lint_docs.py --fail-on-drift --summary; echo rc=$?`
shows `claude_budget: 1`, rc=1 (RED). Measure CLAUDE.md as the cap does on a CRLF checkout:
`python -c "import pathlib; b=pathlib.Path('CLAUDE.md').read_bytes().replace(b'\r\n', b'\n'); print(len(b)+b.count(b'\n'))"`.

Apply every edit below. Each target is named by a dash-free prefix; if WP102 removed a target, skip that
edit and continue. Planning applied all of them to the 1b00d1acd blob in memory: 46,704 became 45,350.

- C1. Delete the `## Notes` bullet that starts `- Target: Bannerlord v1.4.8 (installed game version;` (the header banner already says it).
- C2. Delete the `## Notes` bullet that starts ``- `E:\Decompiled_Bannerlord\` `` (the header, the MCP lookup order and `docs/reference/bannerlord-engine-and-toolchain.md` hold it).
- C3. Delete the line that starts `> **Haiku 3 deprecation:**` and the blank line after it (the date has passed).
- C4. Delete the `## Status Line` section (heading, blank, the one text line, blank). In `docs/reference/hooks-catalog.md`,
  directly below the paragraph that starts `Skill-inline (not in` (above the `## Git hooks` section A1 added), add a blank line and:
  ``Status line (not a hook): `.claude/statusline.sh` renders `ctx: N% | model | branch | Ns/Nu/Nt` (staged, unstaged and untracked counts, omitted when clean). It is registered under `statusLine` in `settings.json` by a relative path, which is safe because it gates nothing.``
- C5. Delete the `## PowerShell Tool (Windows)` section (heading, blank, its four text lines, blank); under `## Notes`, after
  `- No git actions unless explicitly asked`, add `- Native PowerShell tool (opt-in) and its knobs: [`docs/reference/powershell-tool.md`](docs/reference/powershell-tool.md)` (that doc holds every fact the section had).
- C6. Under `## Hooks`, replace the long line that ends `Full catalog (hook → event → purpose):` with
  `` Registrations live in `.claude/settings.json` and in the `/freeze` and `/investigate` SKILL.md frontmatter, each with an explicit `timeout`. Gate them with `bash tools/test_hooks.sh`. Full catalog, with the current counts (hook → event → purpose): ``
- C7. Under `### Release tags`, replace the four-line paragraph that starts `Five versions players actually ran` with:

```
`/release` runs the full sequence, including the #371 Dependencies pairing check. The contract, the
backfill record and the five phantom versions players ran (in no commit on any branch):
[`docs/reference/release-process.md`](docs/reference/release-process.md).
```

- C8. Replace the Hook Response Contracts row that starts ``| `check-verification-evidence.sh` |`` with
  ``| `check-verification-evidence.sh` | Run `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` and `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` and read the output before claiming done, never `./build.ps1` (it deploys). If the build cannot run, say so (`environment-failures.md`). |``
- C9. Under `## Scoped Rules`, delete the blockquote line that starts `> **Convention:** A rule with a `paths:` array` and the blank line after it (`docs/reference/rules-catalog.md:10-12` holds the same text; the catalog pointer stays).
- C10. Under `## Skills (Slash Commands)`, replace the four-line paragraph that starts `41 of the 43 skill` with:
  `Skill **descriptions load eagerly** (that is the skill listing you see), so they are not tabled here.` + newline +
  `Invoke a skill with `/name`; "Skill Routing" below says *when*; each `SKILL.md` holds its workflow.`
- E1 (385b7adf6). In the `### Multi-session git safety` table, directly below the row that starts ``| **Stage explicitly (`git add <paths>`)``, add:
  ``| **In a shared checkout, commit with a pathspec: `git commit -F <msg> -- <paths>`** | A bare `git commit` takes the whole index, so files another session staged go in too (2026-10-14: a 12-path commit took 10 of them). A pathspec commit takes each named file whole from the working tree |``
- E2. Under `## Commits`, replace
  ``Enforced by `.claude/hooks/check-commit-subject-version.sh` (refuses a subject without the label or`` + newline +
  ``with the wrong version; amend/fixup/squash forms that bring no new subject pass).`` with:

```
Enforced by `.claude/hooks/check-commit-subject-version.sh` (refuses a subject without the label or
with the wrong version, and any AI attribution line; amend/fixup/squash forms that bring no new
subject pass) and, for every committer, by `.githooks/commit-msg` once a clone runs
`git config core.hooksPath .githooks`. That hook also judges merges, so give a merge a labelled
message: `git merge --no-ff -m "chore: vX.Y.Z - merge <branch>" <branch>`.
```

GREEN: the measure prints 45,600 or less; `python tools/lint_docs.py --fail-on-drift --summary; echo rc=$?`
shows `claude_budget: 1` and rc=0 (that one finding is the report-only `size-warn` note, because 44,000 bytes is the warn
threshold; `python tools/lint_docs.py --fail-on-drift | grep -F '[size]'` prints nothing); `python tools/reviewctl.py lint; echo rc=$?` rc=0; after staging, the dash
check from Git workflow prints 0.

Stage `CLAUDE.md docs/reference/hooks-catalog.md`. Commit. Subject
`docs(claude): v2.0.28 - CLAUDE.md under its byte cap; pathspec commits`. Body: CLAUDE.md was 46,704 bytes on disk
against the 46,000 byte cap the doc gate enforces, so that gate would refuse every feature-doc commit once it
works; duplicated and stale text goes (name the cuts); it gains the pathspec-commit rule for shared checkouts,
the commit-msg hook and labelled merges, and a verification contract that names non-deploying commands.
`Ported-from: 385b7adf66bf333ccd6d867e8027081d30716c4b`.

### Part A done criteria

- [ ] 4 commits on the branch with the subjects above; `git diff --name-only bannerlord-1.4.5..HEAD -- CHANGELOG.md .claude/settings.json` prints nothing
- [ ] Tool tests: no failure the baseline lacked; `Ran` = baseline + 17 (14 + 3)
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0; CLAUDE.md measure 45,600 or less
- [ ] Build exit 0; Test 0 failed, same total as Step 0
- [ ] `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh`: no failure the baseline lacked (no hook changed yet)

## Part B: gates that decide, fast, on the right repository (5 commits)

### Step B1: `lint_docs.py --drift-only` keeps the drift gate inside its budget (from c79a58521, without its ADR-011 parts)

1. RED. In `tools/tests/test_lint_docs.py` add `import contextlib`, `import io` and `from unittest import mock` to
   the imports, and append this class above `if __name__ == "__main__":`:

```python
class DriftOnlyTests(unittest.TestCase):
    """--drift-only runs only the checks --fail-on-drift gates on: the doc drift hook's mode. The full
    run took 25 s on the laptop, past the hook's 20 s inner bound (WP103, 2026-11)."""

    def _main(self, budget=()):
        def boom(*args, **kwargs):
            raise AssertionError("a report-only check ran under --drift-only")
        slow = ("check_dead_links", "check_untracked_link_targets", "check_stale_versions",
                "check_orphan_features", "check_backlinks_region_prose", "check_missing_feature_docs",
                "check_model_registry", "check_ai_dashes")
        patches = [mock.patch.object(ld, name, boom) for name in slow] + [
            mock.patch.object(ld, "check_config_example_drift", return_value=[]),
            mock.patch.object(ld, "check_version_consistency", return_value=[]),
            mock.patch.object(ld, "check_claude_md_budget", return_value=list(budget)),
            mock.patch("sys.stdout", new_callable=io.StringIO),
        ]
        with contextlib.ExitStack() as stack:
            out = [stack.enter_context(p) for p in patches][-1]
            rc = ld.main(["--drift-only"])
        return rc, out.getvalue()

    def test_clean_tree_exits_0_and_runs_no_report_only_check(self):
        rc, out = self._main()
        self.assertEqual(rc, 0)
        self.assertIn("--drift-only", out)

    def test_a_budget_violation_exits_1(self):
        rc, _ = self._main([(ld.CLAUDE_MD, 0, "size", "CLAUDE.md is over the budget")])
        self.assertEqual(rc, 1)

    def test_a_size_warning_alone_still_exits_0(self):
        rc, _ = self._main([(ld.CLAUDE_MD, 0, "size-warn", "approaching the budget")])
        self.assertEqual(rc, 0)
```

   Run `python -m unittest discover -s tools/tests -t . -p "test_lint_docs.py" 2>&1 | tail -4`: the three new
   tests error (argparse exits 2 on `--drift-only`).
2. `tools/lint_docs.py`, in `main()`:
   - after the `ap.add_argument("--fail-on-drift", ...)` call add
     `ap.add_argument("--drift-only", action="store_true", help="Run only the checks --fail-on-drift gates on (config-example drift, version mismatches, the CLAUDE.md and AGENTS.md budget) and gate on them: the commit hook's mode. The full run took 25 s on the laptop, past the hook's 20 s bound.")`
   - replace the three lines from `report.dead_links = check_dead_links(files)` through `if not args.quick:` with:

```python
    if args.drift_only:
        args.fail_on_drift = True
        report.config_drift = check_config_example_drift(files)
        report.version_mismatches = check_version_consistency()
        report.budget = check_claude_md_budget()
    else:
        report.dead_links = check_dead_links(files)
        report.untracked_targets = check_untracked_link_targets(files)
    if not args.quick and not args.drift_only:
```

   - in the `if args.summary:` branch, define `mode = ["mode: drift-only (only config_drift, version_mismatch and claude_budget ran)"] if args.drift_only else []`
     just above `summary = "\n".join([`, and insert `*mode,` right after the list's first element `"---",`;
   - change `rendered = format_report(report, quick=args.quick)` to `rendered = format_report(report, quick=args.quick, drift_only=args.drift_only)`.
   In `format_report`, change the signature to `def format_report(report: LintReport, quick: bool, drift_only: bool = False) -> str:`
   and replace the dead-link count line, the two-line untracked count statement and the `if not quick:` line after them with:

```python
    if drift_only:
        out.append("Only the checks `--fail-on-drift` gates on ran (`--drift-only`); run without it for the rest.")
        out.append(f"- Config-example drift (doc JSON != shipped ModuleData config): **{len(report.config_drift)}**")
        out.append(f"- Version mismatches (CLAUDE.md / snapshot != pin): **{len(report.version_mismatches)}**")
        out.append(f"- CLAUDE.md budget (size/row/line caps{'' if CLAUDE_MD_BUDGET_ENFORCE else ', warn-only'}): **{len(report.budget)}**")
    else:
        out.append(f"- Dead links: **{len(report.dead_links)}**")
        out.append(f"- Link targets present but UNTRACKED (dead on the remote): "
                   f"**{len(report.untracked_targets)}**")
    if not quick and not drift_only:
```

   The `## ...` detail sections below stay as they are (they print only non-empty lists, and the hook's awk reads them).
3. `.claude/hooks/check-doc-config-drift.sh`: line 21 `(linter rc=1 on --fail-on-drift)` becomes `(linter rc=1 on --drift-only)`;
   replace the two comment lines starting `# Run the drift gate. --fail-on-drift prints the full report` with
   `# Run the drift gate. --drift-only runs just the checks that can block (config-example drift,` /
   `# version markers, the CLAUDE.md and AGENTS.md budget) and exits 1 iff one fails; 0 otherwise.` /
   `# Only rc=1 blocks. The full --fail-on-drift run took 25 s on the laptop (2026-11-04), past the` /
   `# 20 s bound below; these checks take under a second.`; in line 91 and in the line-97 message change
   `tools/lint_docs.py --fail-on-drift` to `tools/lint_docs.py --drift-only`.
4. Docs: `tools/README.md` `lint_docs.py` row, last cell: add `` `--drift-only` `` to the flag list and, in its text,
   say the hook runs `--drift-only`. `docs/features/doc-health-linter.md`: under "How to run", after the
   `--fail-on-drift` line add `python tools/lint_docs.py --drift-only    # only checks 5/6/7, the commit hook's mode (under a second)`;
   in Key Files change the hook row's `runs --fail-on-drift` to `runs --drift-only`.
5. GREEN: the item 1 command passes all tests; `time python tools/lint_docs.py --drift-only; echo rc=$?` prints
   rc=0 in under 3 s; `python tools/lint_docs.py --fail-on-drift; echo rc=$?` still rc=0.

Stage `tools/lint_docs.py tools/tests/test_lint_docs.py .claude/hooks/check-doc-config-drift.sh tools/README.md docs/features/doc-health-linter.md`.
Commit. Subject `feat(tools): v2.0.28 - lint_docs --drift-only keeps the drift gate fast`. Body: the gate ran the whole
linter under a 20 s bound and that run takes 25 s on the laptop, so once its output is read every gated commit would get a
timeout prompt instead of a verdict; `--drift-only` runs only the three checks the gate blocks on.
`Research: lint_docs check timings measured in-process on the laptop, 2026-11-04`.
`Ported-from: c79a585218ad22841c8db1f273e013385512b332`.

### Step B2: decisions Claude Code reads, UTF-8 hook stdio (harness half of c79a58521)

1. RED: edit `tools/test_hooks.sh`. Take each block with `git show c79a58521:tools/test_hooks.sh | sed -n '<range>p'`:
   - lines 51-73 (`decision_of()` and its comment): insert after the line
     `[[ -z "$HPY" ]] && { echo "test_hooks: no safe python for the harness itself; cannot run."; exit 1; }` and one blank line;
   - lines 331-342 (`PRE_GATES`, `is_pre_gate`): insert right after the line `is_gate() { [[ " $GATE_HOOKS " == *" $1 "* ]]; }`;
   - lines 375-378 (the BADSHAPE check): insert inside the section 4 loop, between the inner `fi` that closes the
     "stdout is not valid JSON" check and the outer `fi`;
   - lines 388-414 (section 4b): insert, followed by one blank line, immediately above the `# ----` line that
     precedes `# 5. Starved environment`;
   - lines 512-576 (sections 5c, 5c2, 5c3): insert, followed by one blank line, immediately above the `# ----`
     line that precedes `head2 "6. check-commit-subject-version`;
   - in section 6: replace `if printf '%s' "$OUT" | grep -q '"permissionDecision":"deny"'; then got=deny; else got=allow; fi`
     with `got=$(decision_of "$OUT")`; after the `CSV_BADFILE=` line insert line 625 (`CSV_AIFILE=`); insert lines
     645-692 (the AI attribution and parser rows) directly above the row `"-F file labelled|allow|git commit -F $CSV_MSGFILE"`;
     insert lines 718-735 (the non-ASCII loop) after the `done` that ends the `CSV_CASES` loop, before the section's closing `fi`;
   - lines 769-797 (section 7b): insert, followed by one blank line, immediately above the `# ----` line that precedes `head2 "Summary"`.
   Run `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh 2>&1 | grep FAIL | head -60`: failures in 5c (the seven gates and `check-freeze.sh`),
   5c2 (4 rows), 5c3, section 6 (deny rows read as BADSHAPE, the AI rows, the non-ASCII rows) and 7b.
2. Hooks. Apply the c79a58521 hunks for ten paths in one go (planning: `git apply --check` passed on 1b00d1acd):
   `git diff c79a58521^ c79a58521 -- .claude/hooks/_pybin.sh .claude/hooks/block-broad-git-add.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/check-claude-files-tracked.sh .claude/hooks/check-commit-subject-version.sh .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-native-dll-crt.sh .claude/hooks/validate-push.sh .claude/skills/freeze/check-freeze.sh .claude/skills/freeze/SKILL.md | git apply -`
   This nests every decision under `hookSpecificOutput`, JSON-encodes the two confirm reasons, adds
   `export PYTHONIOENCODING=utf-8` to `_pybin.sh`, makes `check-claude-files-tracked.sh` ask git twice
   (`ls-files --others [--ignored] --exclude-standard`, bounded at 3 s each) instead of per file, rewrites the subject
   gate's parser (shlex, heredocs, `$'...'`, every commit in the command) and adds its AI attribution refusal,
   makes `/freeze` decode the path before comparing, and changes validate-push's refusal text. Then fix the 1.5.x
   citations: in `check-commit-subject-version.sh` replace `(AGENTS.md "Git and commits")` with `(CLAUDE.md "Commits")`
   and both `(AGENTS.md 'Git and commits'` with `(CLAUDE.md 'Commits'`; in `validate-push.sh` line 3 replace
   `(AGENTS.md "Git and commits")` with `(CLAUDE.md "Commits")`.
   `check-doc-config-drift.sh` by hand (its c79a58521 hunk carries 1.5.x-only budget text): line 23 Returns comment
   becomes ``# Returns: {} to allow, {"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}} to block.``;
   the rc-124 line becomes
   ``    printf '%s\n' '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"[check-doc-config-drift] The doc drift gate exceeded its 20s budget and was stopped. This commit is UNCHECKED for config-example drift, version-marker mismatches and the CLAUDE.md eager-load budget. This is NOT a pass. Run: python tools/lint_docs.py --drift-only"}}'``;
   the last printf becomes `printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":%s}}\n' "$MSG"`.
   Keep its awk heading `CLAUDE\.md budget` and its RELEVANT list.
3. `.claude/rules/harness-facts.md`: replace row 33 (it starts `| Hook scripts read tool-input JSON from stdin`) with:
   ``| **PreToolUse output contract.** Tool-input JSON arrives on stdin. Print `{}` to allow; to block or confirm, print `{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"..."}}` (or `"ask"`), or exit 2 with the reason on stderr. A top-level `permissionDecision` is **ignored**. Other events keep a top-level `decision`. Malformed JSON fails open. | https://code.claude.com/docs/en/hooks "PreToolUse decision control" (DOC, re-read 2026-11-04); EMPIRICAL on the 1.5.x line 2026-09-23 (#647): nine gates printed the top-level form and a commit the label gate had denied ran anyway | `tools/test_hooks.sh` 5c fails on the top-level form; a text match on `"permissionDecision":"deny"` passes both forms, which is how the dead gates stayed green. Encode an interpolated reason with `json.dumps` or `jq -Rs .`: a raw newline makes the JSON invalid, which fails open. |``
4. `.claude/rules/hook-authoring.md`: in the timeout table, the row starting `| **Handle rc 124 explicitly, and never as a pass.**`
   gets the middle text ``Emit an `ask` decision under `hookSpecificOutput` (`harness-facts.md` "PreToolUse output contract"), or write to stderr for an advisory hook``.
   Replace the paragraph below that table (it starts ``bash tools/test_hooks.sh` enforces every line of this``) with
   ``bash tools/test_hooks.sh` enforces this: no registration without a timeout, no external tool`` /
   `without an inner bound below it, and (4b) every PreToolUse gate answers a commit inside 80% of` /
   ``its registration. Run it before committing anything under `.claude/hooks/`.``, then add a blank line and:

```
## Prove a gate live (EMPIRICAL: TAOM 2026-09-23, #647)

A gate is done when a real tool call it must stop has been shown stopped: the harness refused
it, not a test read the hook's output. Nine gates passed every test for months while Claude Code
ignored their top-level `permissionDecision` (`harness-facts.md` "PreToolUse output contract").
Test the inputs that go missing too: a non-ASCII command, a multi-line one.
```

5. `docs/reference/hooks-catalog.md`: append to the end of the Purpose cell (before the closing ` |`) of
   - `block-dangerous-git.sh` and `block-broad-git-add.sh`: ` Its confirm is a `hookSpecificOutput` ask with a JSON-encoded reason (2026-11; a raw newline made the old form invalid JSON, which fails open).`
   - `check-commit-subject-version.sh`: ` Since 2026-11 it reads the command the way bash does (heredoc bodies, `$'...'`, attached `-m"..."`, every commit in the command) and refuses an AI attribution line (a `Co-Authored-By` naming an AI, or "Generated with Claude Code"); a human co-author passes.`
   - `check-claude-files-tracked.sh`: ` Two bulk `git ls-files` queries, each bounded at 3 s, since 2026-11; it asked git two or three times per file before.`
6. GREEN: `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh 2>&1 | tail -4`: no failure the baseline lacked, all of 4b, 5c, 5c2, 5c3, 6 and 7b pass;
   `git grep -n -F -e '{"permissionDecision' -e '{\"permissionDecision' -- .claude/hooks .claude/skills/freeze/check-freeze.sh` prints nothing.

Stage the 11 hook and skill files above, `tools/test_hooks.sh`, `.claude/rules/harness-facts.md`,
`.claude/rules/hook-authoring.md`, `docs/reference/hooks-catalog.md`. Commit. Subject
`fix(hooks): v2.0.28 - gates answer in hookSpecificOutput, read UTF-8`. Body: Claude Code reads a PreToolUse decision only
under `hookSpecificOutput`; seven gates and the `/freeze` check printed a top-level `permissionDecision`, so they decided
nothing; they now nest it and JSON-encode each reason; `_pybin.sh` exports `PYTHONIOENCODING=utf-8` because one character
outside cp1252 emptied the extracted command and every Python-parsed gate allowed it; also the bulk files-tracked query, the
subject gate's parser and AI attribution refusal, the `/freeze` path decode and the validate-push refusal text; the hook suite
reads decisions as Claude Code does (`decision_of`). `Not-tested: a live refusal through the harness (maintainer check after merge)`.
`Ported-from: c79a585218ad22841c8db1f273e013385512b332`.

### Step B3: split hook text without here-strings (143f0fa85; comment wording from fff6d66e9)

1. RED: insert `git show 143f0fa85:tools/test_hooks.sh | sed -n '595,663p'` (section 4e, verbatim), followed by one
   blank line, immediately above the `# ----` line that precedes `# 5. Starved environment` (after 4b). Insert
   `git show 143f0fa85:tools/test_hooks.sh | sed -n '1958,2025p'` (section 7h) immediately above the `# ----` line
   that precedes `head2 "Summary"`, then adapt 7h for 1.4.x:
   - in `hw_payload`, `push --force origin bannerlord-1.5.x` becomes `push --force origin bannerlord-1.4.5`;
   - the `for row in` list keeps only `"validate-push.sh|push|Bash|65536|rc2" "validate-push.sh|push|Bash|65600|rc2" "validate-push.sh|push|Bash|65663|rc2" "block-dangerous-git.sh|reset|Bash|65548|ask" "block-broad-git-add.sh|addall|Bash|65548|ask" "mark-verification-run.sh|test|Bash|65548|mark"`;
   - replace its comment sentence from `Each row sizes its command` through `rows prove the gates still answer, inside 80% of each registration.`
     with `On 1.4.x each gate reads the raw command, so each row sizes its command into that window directly. 4e keeps the construct out; these rows prove the gates still answer, inside 80% of each registration.`
     (wrap it like the lines around it and keep their `#     ` comment prefix).
   Run the suite: 4e fails and lists the 9 sites (7h rows may hang until the 10 s bound or may pass; bash behaviour decides).
2. Fix each site. For a list loop, replace the `while IFS= read -r <var>; do` line with the three comment lines
   `# Split into an array under set -f, never a here-string: Git Bash 5.3 hangs forever on one of` /
   `# 65,536 to 65,663 bytes of text (#681), and a killed gate fails open. An empty line is dropped,` /
   `# which is harmless here.` then `set -f; IFS=$'\n'; <LIST>=($<VAR>); IFS=$' \t\n'; set +f` and
   `for <var> in "${<LIST>[@]}"; do`; replace the matching `done <<< "$<VAR>"` with `done`:

| File (1b00d1acd line of the `done <<<`) | `<var>` | `<VAR>` | `<LIST>` |
|---|---|---|---|
| `block-broad-git-add.sh` (118) | `seg` | `SEGMENTS` | `SEG_LIST` |
| `block-dangerous-git.sh` (103) | `seg` | `SEGMENTS` | `SEG_LIST` |
| `check-doc-config-drift.sh` (78) | `f` | `STAGED` | `STAGED_LIST` |
| `check-moduledata-validation.sh` (73) | `f` | `STAGED` | `STAGED_LIST` |
| `check-native-dll-crt.sh` (68) | `f` | `STAGED` | `STAGED_LIST` |
| `check-verification-evidence.sh` (42) | `f` | `ALL_FILES` | `FILE_LIST` |

   Special cases:
   - `detect-docs-gaps.sh`: put the comment and `set -f; IFS=$'\n'; DOC_LIST=($EXISTING_DOCS); IFS=$' \t\n'; set +f`
     once, right after the `EXISTING_DOCS=$(find ...)` line (globbing is back on before the `Main/Features/*/` loop);
     the inner `while IFS= read -r doc; do` becomes `for doc in "${DOC_LIST[@]}"; do` and `  done <<< "$EXISTING_DOCS"` becomes `  done`.
   - `mark-verification-run.sh`: before the loop add `SEGMENTS=$(printf '%s' "$COMMAND" | tr ';&|' '\n')`, then the
     comment and split with `SEG_LIST`/`SEGMENTS`; `done <<< "$(printf '%s' "$COMMAND" | tr ';&|' '\n')"` becomes `done`.
   - `validate-push.sh` line 48: replace `read -r -a TOKENS <<< "$CLEAN"` with
     `# A word split under set -f, never a here-string (#681). Only the first line is judged, as` /
     `# read -a did; set -f keeps a refspec glob such as refs/heads/* whole.` /
     `CLEAN=${CLEAN%%$'\n'*}` / `set -f; TOKENS=($CLEAN); set +f`.
3. `.claude/rules/hook-authoring.md`: after the "Prove a gate live" section add:

```
## Never feed hook text through a here-string (EMPIRICAL: TAOM 2026-09-26, #681)

Never feed unbounded text through `<<<` or an expanding here-document (Git Bash 5.3 hangs forever on
65,536 to 65,663 bytes of text, and a killed gate fails open): split it under `set -f` with
`set -f; IFS=$'\n'; A=($X); IFS=$' \t\n'; set +f` and a `for` loop, or `set -f; T=($X); set +f` for
words, never `< <(printf ...)`, which forks per call; `tools/test_hooks.sh` 4e enforces it.
```

4. GREEN: the suite passes 4e (`no hook script feeds text through a here-string or an expanding here-document`) and
   all six 7h rows; no failure the baseline lacked. `git grep -n '<<<' -- '.claude/hooks/*.sh'` prints no line outside a comment.

Stage the 9 hooks, `tools/test_hooks.sh`, `.claude/rules/hook-authoring.md`. Commit. Subject
`fix(hooks): v2.0.28 - split hook text without here-strings`. Body: Git Bash 5.3 hangs forever on a here-string of
65,536 to 65,663 bytes of text (measured on the 1.5.x line, #681) and a killed gate allows the command; nine sites now
split into an array under `set -f`; validate-push still judges the first line only; 4e and 7h pin it.
`Not-tested: the hang itself, not reproduced on this laptop`.
`Ported-from: 143f0fa8540032657beb979977e2a52329a6e95d`, `Ported-from: fff6d66e9bd5a2db11c0ef20ab167225efbd1a22`.

### Step B4: git gates judge the repository a commit runs in (12d96adc1, gate halves of 7e26d77fe and 407570ece)

Method: end-state transplant at 407570ece (12d96adc1's subject-gate change was moved into the shared reader by
7e26d77fe and fixed by 407570ece; the end state covers all three). The merge-tree simulation of each commit onto
1b00d1acd conflicts (12d96adc1: the subject gate, hooks catalog, test_hooks; 7e26d77fe and 407570ece: `_pybin.sh`,
`_shellwords.py`, the gates, test files), because 1.4.x lacks plan 027's reader; hence the transplant.

1. RED:
   - `git show 407570ece:tools/tests/test_shellwords.py > tools/tests/test_shellwords.py`; then
     `python -m unittest discover -s tools/tests -t . -p "test_shellwords.py" 2>&1 | tail -3` shows an import error for `_shellwords`.
   - `tools/test_hooks.sh` section 6: replace the `CSV_CASES=(` line with this block (the stub worktree and five rows go first):

```
    # Another worktree carries its own version: a commit run there through `git -C <dir>` or
    # `cd <dir> &&` is judged against that tree's SubModule.xml. It is a git work tree: the gate
    # resolves the commit's directory to its work tree's top level (_pybin.sh taom_git_repo_dir), and
    # a directory in no work tree falls back to the project.
    mkdir -p "$SANDBOX/otherwt/Main/_Module"
    git -C "$SANDBOX/otherwt" init -q 2>/dev/null
    printf '<Module>\n  <Version value="v1.2.3"/>\n</Module>\n' > "$SANDBOX/otherwt/Main/_Module/SubModule.xml"
    CSV_OTHER=$(cygpath -m "$SANDBOX/otherwt" 2>/dev/null || echo "$SANDBOX/otherwt")
    CSV_CASES=(
      "git -C another worktree, its label|allow|git -C \"$CSV_OTHER\" commit -m \"docs: v1.2.3 - x\""
      "git -C another worktree, this repo's label|deny|git -C \"$CSV_OTHER\" commit -m \"docs: $CSV_VER - x\""
      "cd another worktree then commit, its label|allow|cd \"$CSV_OTHER\" && git commit -m \"docs: v1.2.3 - x\""
      "cd another worktree then commit, this repo's label|deny|cd \"$CSV_OTHER\" && git commit -m \"docs: $CSV_VER - x\""
      "git -C a missing directory falls back to this repo|allow|git -C \"$CSV_OTHER/nope\" commit -m \"docs: $CSV_VER - x\""
```

   - Append `git show 407570ece:tools/test_hooks.sh | sed -n '1326,1500p'` (the repository-reader rows of 7b: worktree
     and subdirectory commits, the resolver cases including a PowerShell `Set-Location`, the two-repository deny,
     `taom_is_project_repo`, the drift gate with a stub linter, the subject gate in a worktree cwd, the broad-add list)
     directly after the last line of section 7b (the `fi` after `allows once the file is staged`).
   Run the suite: four of the five new section 6 rows fail (the missing-directory row already passes) and the new
   7b rows fail (`taom_git_repo_dir` does not exist yet).
2. Reader and helpers:
   - `git show 407570ece:.claude/hooks/_shellwords.py > .claude/hooks/_shellwords.py` (verbatim; it also carries plan 027's
     PowerShell reader, which only the `git-repo` mode uses here; WP116 replaces the file with its end state).
   - `git show 407570ece:.claude/hooks/_pybin.sh | sed -n '159,164p;193,248p' >> .claude/hooks/_pybin.sh` (appends
     `TAOM_HOOKS_DIR`, `taom_git_repo_dir`, `taom_deny_multi_repo`, `taom_is_project_repo`; not `taom_hook_command`).
     Verify: `bash -n .claude/hooks/_pybin.sh` exits 0 and `grep -c '^taom_' .claude/hooks/_pybin.sh` prints 7.
3. `check-commit-subject-version.sh`: write it as `git show 407570ece:.claude/hooks/check-commit-subject-version.sh`
   with these edits (line numbers of that blob): delete lines 33-43 (the prefilter comment, the
   `[[ "$INPUT" == *commit* ...` test and the blank after it; plan 013 belongs to WP116); replace lines 50-53 (the
   `taom_hook_command` comment and call) with

```
# Extract the bash command from tool_input. A PowerShell tool command is read as Bash text
# until the _shellwords.py posix reader is wired into the gates (plan 027).
COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
```

   and replace its three citations as in B2 (`AGENTS.md "Git and commits"` and both `AGENTS.md 'Git and commits'`
   become `CLAUDE.md "Commits"` / `CLAUDE.md 'Commits'`). Planning checked this exact construction: `bash -n` exits 0,
   the Python body parses (8,703 bytes), no `AGENTS.md` left.
4. The other four gates (each replaces its one `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }` line):
   - `check-claude-files-tracked.sh`: the three comment lines `# The top level of the repository the commit runs in, not always the project root (_pybin.sh` /
     `# taom_git_repo_dir): another session's untracked skill in the main checkout used to deny every` / `# worktree commit.` then
     `COMMIT_REPO=$(taom_git_repo_dir commit) || { taom_deny_multi_repo check-claude-files-tracked; exit 0; }` and
     `cd "$COMMIT_REPO" 2>/dev/null || { echo '{}'; exit 0; }`; and its `MSG=` first line says `These files exist under .claude/ in $(pwd) but will NOT be committed:`.
   - `check-doc-config-drift.sh` and `check-moduledata-validation.sh`: the comment lines `# The top level of the repository the commit runs in, not always the project root (_pybin.sh` /
     `# taom_git_repo_dir). A commit in another repository is not this gate's: its tools/*.py are not` / `# TAOM's to run.` then
     `COMMIT_REPO=$(taom_git_repo_dir commit) || { taom_deny_multi_repo <this gate's file name without .sh>; exit 0; }`,
     `cd "$COMMIT_REPO" 2>/dev/null || { echo '{}'; exit 0; }` and `taom_is_project_repo "$COMMIT_REPO" || { echo '{}'; exit 0; }`.
   - `block-broad-git-add.sh`: add `    SUBCMD=add` as the first line inside `if [[ "$rest" =~ ^add([[:space:]]|$) ]]; then` and
     `    SUBCMD=commit` inside `elif [[ "$rest" =~ ^commit([[:space:]]|$) ]]; then`; replace the `cd` line with

```
MULTI_NOTE=""
if ! ADD_REPO=$(taom_git_repo_dir "${SUBCMD:-add}"); then
  # The calls reach more than one work tree: list the project root's, and say the list is incomplete.
  ADD_REPO="${CLAUDE_PROJECT_DIR:-$(pwd)}"
  MULTI_NOTE=" The command runs git ${SUBCMD:-add} in more than one repository, so this list covers the project root only: split it, one repository per command."
fi
cd "$ADD_REPO" 2>/dev/null || { echo '{}'; exit 0; }
```

     and in the `MSG=` line change `${FILE_LIST}.${STASH_NOTE}` to `${FILE_LIST}.${MULTI_NOTE}${STASH_NOTE}`; extend the
     comment above with `Listed from the top level of the repository the call runs in (_pybin.sh taom_git_repo_dir).`
5. `.claude/rules/harness-facts.md`: below the contract row add
   ``| Every hook payload carries `cwd`, "the worktree root after Claude enters a worktree, and the new directory after Claude runs `cd`", while `$CLAUDE_PROJECT_DIR` "stays put" at the project root where the session started. A gate that judges a git call reads that call's own repository (`git -C`, a `cd` before it, then `cwd`, resolved to the work tree's top level), never `$CLAUDE_PROJECT_DIR` alone. | https://code.claude.com/docs/en/hooks (DOC, 2026-11-04) | `check-claude-files-tracked.sh`, `check-moduledata-validation.sh`, `check-doc-config-drift.sh`, `check-commit-subject-version.sh` and `block-broad-git-add.sh` take it from `_pybin.sh` `taom_git_repo_dir` over `_shellwords.py git-repo`; a command that commits in two repositories is denied. `tools/test_hooks.sh` 7b. |``
   In the numbered "What this rule changes" list, the item about committing `.claude/` changes: after
   ``.claude/{skills,agents,rules,hooks}/`` add `of the repository the commit runs in`; if WP102 reworded the item so
   that text is absent, append the sentence `check-claude-files-tracked.sh judges the repository the commit runs in.`
6. `docs/reference/hooks-catalog.md`: append ` It reads that <Version> in the repository the commit runs in (git -C <dir>, a cd <dir> before it, else the payload's cwd; _pybin.sh taom_git_repo_dir) and refuses a command that commits in two repositories.`
   to the subject-gate row (format the names as code); ` Judges the repository the commit runs in, not the main checkout.` to
   `check-claude-files-tracked.sh`; ` Runs in the repository the commit runs in, and only when that is a work tree of this project (taom_is_project_repo).`
   to `check-moduledata-validation.sh` and `check-doc-config-drift.sh`; ` Lists the files of the repository the git add runs in.`
   to `block-broad-git-add.sh`; and to the header blockquote: `` `_shellwords.py` (2026-11) is a helper with no registration: the reader `_pybin.sh` `taom_git_repo_dir` calls to find the repository a git call runs in.``
7. GREEN: `test_shellwords.py` prints `Ran 83 tests` and `OK`; the hook suite has no failure the baseline lacked and every
   row added in this step passes.

Stage `.claude/hooks/_shellwords.py .claude/hooks/_pybin.sh` and the five gates, `tools/tests/test_shellwords.py`,
`tools/test_hooks.sh`, `.claude/rules/harness-facts.md`, `docs/reference/hooks-catalog.md`. Commit. Subject
`fix(hooks): v2.0.28 - git gates judge the repository a commit runs in`. Body: the gates cd'd to `CLAUDE_PROJECT_DIR`, which
stays at the checkout a session started in, so a worktree commit was judged on the main checkout's index, version and tools;
this port runs executors in worktrees; the five gates now read the repository the git call runs in (cd or `git -C`, then the
payload cwd, resolved to the top level) through `taom_git_repo_dir` over the 1.5.x reader `_shellwords.py`; a command that commits
in two repositories is refused; the validator gates run a repository's tools only in this project.
`Ported-from: 12d96adc10448a27e9d225b23453f4558d196579`, `Ported-from: 7e26d77fec30e1ea2b18521c247cfcd3eb59db3e`,
`Ported-from: 407570ecea85fb22762075eb1ff5c665830afe2e`.

### Step B5: validate-push guards bannerlord-1.5.x too (new work; block-no-verify comment from d01bddf10)

1. RED: insert this block immediately above the `# ----` line that precedes `head2 "7h.` (or `head2 "Summary"`):

```
# ---------------------------------------------------------------------------
head2 "7c. validate-push refuses a force push to either trunk, master or main"
for row in "2|git push --force origin bannerlord-1.4.5" "2|git push --force origin HEAD:bannerlord-1.5.x" \
           "2|git push -f origin master" "2|git push origin +bannerlord-1.5.x" \
           "0|git push origin port148/103-x" "0|git push --force origin port148/103-x"; do
    want="${row%%|*}"; cmd="${row#*|}"
    payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":"Bash","tool_input":{"command":sys.argv[1]},"hook_event_name":"PreToolUse"}))' "$cmd")
    rc=0
    printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash .claude/hooks/validate-push.sh >/dev/null 2>&1 || rc=$?
    if [[ "$rc" == "$want" ]]; then
        ok "validate-push rc=$rc for: $cmd"
    else
        bad "validate-push expected rc=$want, got rc=$rc for: $cmd"
    fi
done
```

   Run the suite: the two `bannerlord-1.5.x` rows fail (rc=0).
2. `validate-push.sh` `is_protected`: `master|main|bannerlord-1.4.5) return 0 ;;` becomes
   `master|main|bannerlord-1.4.5|bannerlord-1.5.x) return 0 ;;`; add to the comment above it
   `# bannerlord-1.5.x added 2026-11: a session on this line must not force-push the other trunk either.`
3. `block-no-verify.sh`: replace the two comment lines `# here for two measured reasons. Hooks run with cwd set to the MAIN project directory` /
   `# regardless of where the command executes (proven 2026-08-20: a `dotnet build` run from` with
   `# here for two measured reasons. Hooks run in the session's directory (the MAIN project` /
   `# directory unless an earlier Bash `cd` moved it, #690), not where the command's own `cd`` /
   `# leads (proven 2026-08-20: a `dotnet build` run from`.
4. `docs/reference/hooks-catalog.md`: the `validate-push.sh` row's Purpose becomes
   `Warns on a push to a protected branch; hard-blocks (exit 2) a force push (--force, -f in a short cluster, --force-with-lease, --force-if-includes, a + refspec) to master, main, bannerlord-1.4.5 or bannerlord-1.5.x. Judges the first line of the command only.`
   (format names as code); in the `block-no-verify.sh` row replace `hooks run with cwd = the MAIN tree regardless of where the command runs`
   with `hooks run in the session's directory (the main tree unless an earlier Bash cd moved it, #690), not where the command's own cd leads`.
5. GREEN: all six 7c rows pass; no failure the baseline lacked.

Stage `.claude/hooks/validate-push.sh .claude/hooks/block-no-verify.sh tools/test_hooks.sh docs/reference/hooks-catalog.md`.
Commit. Subject `fix(hooks): v2.0.28 - validate-push guards bannerlord-1.5.x too`. Body: validate-push protected master, main and
bannerlord-1.4.5 only, so a session on this line could force-push the 1.5.x trunk; it refuses that now; block-no-verify's
comment no longer claims hooks always run in the main tree (#690).
`Ported-from: d01bddf104e9271f600e5ad5895b71faedcaa888`.

### Part B done criteria

- [ ] 5 more commits (9 in total) with the subjects above
- [ ] `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh`: no failure the baseline lacked; 4b, 4e, 5c, 5c2, 5c3, 6, 7b, 7c and 7h all pass
- [ ] Tool tests: `Ran` = baseline + 103 (14 + 3 + 3 + 83); no failure the baseline lacked
- [ ] `python tools/lint_docs.py --drift-only` exits 0 in under 3 s; `--fail-on-drift` exits 0
- [ ] `git grep -n -F '{"permissionDecision' -- .claude` prints nothing (the old row in `harness-facts.md` and the freeze skill line were replaced in B2)
- [ ] Build exit 0; Test 0 failed, same total as Step 0

## Part C: anchored registrations, PowerShell coverage, Serena denies (maintainer applies settings.json)

### Step C1: the settings change and its tests (7b342ed59, d01bddf10 7j, D19, D3 from 7d13adc36, attribution from c79a58521)

1. RED (executor): insert `git show d01bddf10:tools/test_hooks.sh | sed -n '2343,2386p'` (section 7j: every registration in
   settings.json and in skill or agent frontmatter anchored on `CLAUDE_PROJECT_DIR`, and validate-push run as registered from
   `tools/` and `.claude/hooks/` must still refuse) immediately above the `# ----` line that precedes `head2 "Summary"`, and change
   its payload text `HEAD:bannerlord-1.5.x` to `HEAD:bannerlord-1.4.5`. Below it add section 7k:

```
# ---------------------------------------------------------------------------
head2 "7k. the git gates run for the Bash and PowerShell tools; Serena's editors are denied (D19, D3)"
K7=$("$HPY" - <<'PY' | tr -d '\r'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
gates = {"block-no-verify.sh", "validate-push.sh", "check-claude-files-tracked.sh",
         "check-commit-subject-version.sh", "block-dangerous-git.sh", "block-broad-git-add.sh",
         "check-moduledata-validation.sh", "check-native-dll-crt.sh", "check-doc-config-drift.sh"}
for g in d["hooks"]["PreToolUse"]:
    names = {h["command"].rsplit("/", 1)[-1] for h in g["hooks"]}
    if names & gates and g.get("matcher") != "Bash|PowerShell":
        print("matcher %r for %s" % (g.get("matcher"), ",".join(sorted(names & gates))))
    gates -= names
for n in sorted(gates):
    print("unregistered " + n)
deny = set(d.get("permissions", {}).get("deny", []))
for n in ["create_text_file", "replace_content", "replace_in_files", "delete_lines", "replace_lines",
          "insert_at_line", "replace_symbol_body", "insert_after_symbol", "insert_before_symbol",
          "rename_symbol", "safe_delete_symbol", "execute_shell_command", "jet_brains_move",
          "jet_brains_safe_delete", "jet_brains_rename", "jet_brains_inline_symbol"]:
    if "mcp__serena__" + n not in deny:
        print("not denied mcp__serena__" + n)
for n in ["find_symbol", "find_referencing_symbols", "get_symbols_overview", "read_memory", "write_memory"]:
    if "mcp__serena__" + n in deny:
        print("read or memory tool denied mcp__serena__" + n)
if d.get("attribution", {}).get("commit") != "" or d.get("attribution", {}).get("pr") != "":
    print("attribution.commit and attribution.pr are not both empty")
PY
)
if [[ -z "$K7" ]]; then
    ok "git-gate group matches Bash|PowerShell; 16 Serena editors denied, read and memory tools kept; attribution off"
else
    bad "settings.json: $(printf '%s' "$K7" | tr '\n' ';')"
fi
```

   Run the suite: 7j reports the unanchored registrations and both payload runs answer rc 127; 7k reports the matcher,
   the 16 Serena tools and the attribution.
2. Docs (executor, same commit):
   - `.claude/rules/harness-facts.md`, below the cwd row:
     ``| The PowerShell tool is its own tool: `tool_name` `"PowerShell"`, the command in `tool_input.command`. A `Bash` matcher never sees it; `Bash\|PowerShell` covers both. | https://code.claude.com/docs/en/hooks (DOC, 2026-11-04); payload shape EMPIRICAL on the 1.5.x line 2026-09-25 | The git-gate group matches `Bash\|PowerShell` (maintainer decision 2026-11-04). The gates still read a PowerShell command as Bash text, so run git through the Bash tool until `_shellwords.py posix` is wired into them (plan 027). |``
   - `.claude/rules/hook-authoring.md`: the paragraph from B2 becomes
     ``bash tools/test_hooks.sh` enforces this: no registration without a timeout, no external tool`` /
     `without an inner bound below it, (4b) every PreToolUse gate answers a commit inside 80% of its` /
     ``registration, and (7j) every registration is anchored on `"$CLAUDE_PROJECT_DIR"` (a relative one`` /
     ``dies after a `cd`, #690). Run it before committing `.claude/hooks/`.``
   - `docs/reference/hooks-catalog.md`: every Event cell `PreToolUse (Bash)` becomes `PreToolUse (Bash, PowerShell)`; append to the
     header blockquote `` Since 2026-11 every registration names its script as `"$CLAUDE_PROJECT_DIR"/.claude/hooks/<name>.sh` (a relative path stopped every gate after a Bash `cd`, #690), and the git-gate group matches `Bash|PowerShell`.``
   - `docs/reference/mcp-servers.md`, section `## Denied write tools`: after the paragraph listing the nine git and filesystem tools add
     `` Sixteen Serena editing tools are denied in `.claude/settings.json` (`permissions.deny`, maintainer decision 2026-11-04): `mcp__serena__` followed by `create_text_file`, `replace_content`, `replace_in_files`, `delete_lines`, `replace_lines`, `insert_at_line`, `replace_symbol_body`, `insert_after_symbol`, `insert_before_symbol`, `rename_symbol`, `safe_delete_symbol`, `execute_shell_command`, `jet_brains_move`, `jet_brains_safe_delete`, `jet_brains_rename`, `jet_brains_inline_symbol`. Serena's read tools and its four memory tools stay allowed. The list is the can-edit set the 1.5.x line derived for Serena 7a296833 (7d13adc36); this line runs Serena unpinned, so re-derive it when Serena adds an editing tool. ``
     and in its **Why:** paragraph change `registered against `matcher: "Bash"`` to `registered against `matcher: "Bash|PowerShell"``.
   - `.claude/skills/freeze/SKILL.md`: the table row that starts `| MCP server tools (Serena's` becomes
     ``| MCP server tools | Not checked by freeze; the git, filesystem and Serena editing tools are denied outright (`docs/reference/mcp-servers.md`), Serena's memory tools excepted. Use Edit/Write for changes you want freeze-protected. |``
   - `CLAUDE.md` `## Hooks`: after `each with an explicit `timeout`.` insert
     `` Each names its script as `"$CLAUDE_PROJECT_DIR"/.claude/hooks/<name>.sh` (a relative path stopped every gate after a `cd`, #690).``
     Re-run the A4 measure: it must stay under 46,000 (planning expects about 45,480) and the doc gate must still exit 0.
3. Write this maintainer script to `C:/Users/mikew/source/repos/taom-port/scratch/103/settings.py` (outside the repo). Do NOT run it.

```python
"""WP103 Part C: the .claude/settings.json change, applied by the maintainer (config-protection keeps
agents out of this file). Run from the WP103 worktree root: python ../scratch/103/settings.py
Idempotent: a second run changes nothing."""
import json
import pathlib

p = pathlib.Path(".claude/settings.json")
d = json.loads(p.read_text(encoding="utf-8"))
SERENA_EDIT = ["create_text_file", "replace_content", "replace_in_files", "delete_lines", "replace_lines",
               "insert_at_line", "replace_symbol_body", "insert_after_symbol", "insert_before_symbol",
               "rename_symbol", "safe_delete_symbol", "execute_shell_command", "jet_brains_move",
               "jet_brains_safe_delete", "jet_brains_rename", "jet_brains_inline_symbol"]
perms = dict(d.get("permissions", {}))
deny = list(perms.get("deny", []))
deny += ["mcp__serena__" + n for n in SERENA_EDIT if "mcp__serena__" + n not in deny]
perms["deny"] = deny
out = {"attribution": {"commit": "", "pr": "", "sessionUrl": False}, "permissions": perms}
out.update({k: v for k, v in d.items() if k not in out})
for groups in out["hooks"].values():
    for group in groups:
        for h in group["hooks"]:
            if h["command"].startswith(".claude/hooks/"):
                h["command"] = '"$CLAUDE_PROJECT_DIR"/' + h["command"]
gates = [g for g in out["hooks"]["PreToolUse"]
         if any(h["command"].endswith("/validate-push.sh") for h in g["hooks"])]
assert len(gates) == 1, "expected one PreToolUse group holding validate-push.sh"
gates[0]["matcher"] = "Bash|PowerShell"
p.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
print("settings.json updated")
```

   The exact change it makes to the post-WP102 file: a new `attribution` block (5 lines) and `permissions.deny` block
   (20 lines) at the top; each of the 26 `"command": ".claude/hooks/<name>.sh"` values becomes
   `"command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/<name>.sh"`; the git-gate group's `"matcher": "Bash"` becomes
   `"matcher": "Bash|PowerShell"`. Nothing else (planning checked that `json.dumps(indent=2)` reproduces today's file byte for byte).
4. STOP and hand over to the maintainer with this request: "WP103 Part C: please review
   `C:/Users/mikew/source/repos/taom-port/scratch/103/settings.py`, then run
   `cd C:/Users/mikew/source/repos/taom-port/103 && python ../scratch/103/settings.py && git diff --stat -- .claude/settings.json`
   (expect one file, about 52 insertions and 27 deletions), and tell me when it is done." Leave the uncommitted test and doc
   edits in the worktree.
5. Resume after the maintainer confirms (a new session may resume here: `git status --porcelain` must show
   `.claude/settings.json` plus the files of items 1 and 2 modified, nothing else). GREEN:
   `python -m json.tool .claude/settings.json > /dev/null && echo JSON-OK`; `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh 2>&1 | tail -4`
   shows 7j and 7k passing and no failure the baseline lacked; `python tools/audit_claude_config.py --min HIGH` shows no
   HIGH the baseline lacked; `python tools/lint_docs.py --fail-on-drift` exits 0.
6. Stage `.claude/settings.json tools/test_hooks.sh .claude/rules/harness-facts.md .claude/rules/hook-authoring.md docs/reference/hooks-catalog.md docs/reference/mcp-servers.md .claude/skills/freeze/SKILL.md CLAUDE.md`.
   Commit. Subject `fix(hooks): v2.0.28 - anchor hook paths, gate PowerShell, deny Serena`. Body: relative registrations failed
   to start after a persistent `cd`, so every call ran ungated (#690); each now names `"$CLAUDE_PROJECT_DIR"/.claude/hooks/<name>.sh`;
   the git-gate group matches `Bash|PowerShell` (read as Bash text for now); Serena's sixteen edit tools are denied because they
   bypass every Edit, Write and Bash hook, its read and memory tools stay; `attribution` is empty to match the no-attribution
   rule; the maintainer applied the settings change; 7j and 7k pin it. Trailers: `Ported-from:` 7b342ed59ffeb3b1a856717b65ef32f1bd8e1c94,
   d01bddf104e9271f600e5ad5895b71faedcaa888, 7d13adc369490cf6d7e866c6a44a4dcc76c85f68, c79a585218ad22841c8db1f273e013385512b332 (one line each).
7. Delete `C:/Users/mikew/source/repos/taom-port/scratch/103/settings.py` and the message file.

### Part C done criteria

- [ ] 10 commits on the branch; `git log --format=%s bannerlord-1.4.5..HEAD` lists the 10 subjects of this plan
- [ ] `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh` ends with 0 failed, or with only failures that were in the Step 0 baseline
- [ ] Build exit 0; Test 0 failed, same total as Step 0

## Disposition table

| Commit | Subject (1.5.x) | Disposition | Step |
|---|---|---|---|
| `143f0fa85` | split hook text without here-strings | adapted: re-expressed per 1.4.x site; validate-push keeps first-line semantics; 4e verbatim; 7h rewritten (Bash rows, bannerlord-1.4.5); lessons hunks dropped (WP142) | B3 |
| `7b342ed59` | anchor hook registrations on the project dir | adapted: settings.json by the maintainer (26 registrations after WP102); 7j with trunk bannerlord-1.4.5; hook-authoring, catalog and CLAUDE.md lines hand-written; lessons dropped | C1 |
| `d01bddf10` | convergence fixes for the push guard work | partial: 7j frontmatter check (C1) and the block-no-verify comment (B5); the `_pushjudge.py`, validate-push, `test_shellwords` rename and catalog hunks go with the push-judge transplant (WP116) | B5, C1 |
| `785b2703e` | non-deploying builds in /verify and Codex | ported: `cherry-pick -n` (simulated clean) plus four 1.4.x adaptations | A2 |
| `12d96adc1` | commit-subject gate reads the target repo's version | transplanted: behaviour and its five test cases arrive through the 407570ece end state | B4 |
| `385b7adf6` | commit with a pathspec, never a bare commit | adapted: a row in the CLAUDE.md git-safety table (`git-and-commits.md` is 1.5.x-only) | A4 |
| `7e26d77fe` | upstream review; gates judge the right repo | partial: gate half via the 407570ece end state. Not ported: upstream-watch tooling (`tools/upstream_watch.py` and tests, `/upstream-review`, its feature doc, the session-start block, feature-map, tools/README and CLAUDE.md lines, the adopt-external line), `tools/tests/test_graphify_taom.py` (graphify is 1.5.x-only) | B4 |
| `407570ece` | upstream check in its own hook; Codex fixes | partial: reader fixes, the two-repository deny, the broad-add note, their tests. Not ported: `upstream-watch.sh` and its registration, the session-start shared deadline, the review record, RCA and lessons | B4 |
| `0382d5d2a` | a commit-msg hook checks every committer | ported: checker and tests verbatim but two citations; `.githooks/commit-msg` at its tip state (434d1d795); catalog and tools/README rows; the three 1.5.x review-record edits skipped | A1 |
| `c79a58521` (shared, harness half) | (mislabelled umbrella) | ported: output contract in seven gates and `/freeze`, `PYTHONIOENCODING`, bulk files-tracked query, subject-gate parser and AI-attribution refusal, validate-push refusal text, freeze path decode, test_hooks `decision_of`/4b/5c/5c2/5c3/6/7b, harness-facts contract row, hook-authoring rows, the drift-only idea (B1, without ADR-011), `attribution` block (C1). Crash half to WP107; CLAUDE.md, AGENTS.md, rules and ADR-011 rewrites to WP142/WP143; 5d, 8 and the 3b rules extension are 1.5.x-only; detect-docs-gaps and session-start text left | A1-C1 |
| `1d1abd694` (shared, ModuleId hunk) | rebuild the skill around the run pipeline | adapted: the ModuleId part as a repository-wide sweep; the pipeline rebuild stays with WP138 | A3 |
| `7d13adc36` (D3) | newest Serena pin, deny its edit tools | adapted: the 16-entry deny list (maintainer), mcp-servers and freeze lines; the `.mcp.json` Serena pin not ported | C1 |
| `35ff005dc` (pattern only) | the doc linter judges what a commit records | the `check_unflagged_dotnet` pattern lifted into a tool test; the linter rework stays with its owner WP | A3 |
| `434d1d795` (one file state) | commit gates for every committer | `.githooks/commit-msg` only; `pre-commit`, `pre-merge-commit` and the rest stay with their owner WP | A1 |
| `fff6d66e9` (comment wording) | review fixes for the push guard work | the corrected byte-window wording only; the rest is WP116's | B3 |
| `4b7e36250` | union-merge lessons | not in this WP: D18 defers the `merge=union` line to WP142 | none |

## Test plan

- New Python tests: `tools/tests/test_check_commit_msg.py` (14: label, version, 72-character limit, AI trailer, human co-author,
  comments, autosquash prefixes, unknown version, two end-to-end commits through the real hook in a scratch repository);
  `tools/tests/test_dotnet_commands_flagged.py` (3); `tools/tests/test_lint_docs.py` `DriftOnlyTests` (3); `tools/tests/test_shellwords.py` (83, verbatim from 407570ece).
- New hook-suite checks in `tools/test_hooks.sh`: BADSHAPE in section 4; 4b (each PreToolUse gate answers a commit within 80% of its
  registration); 4e (no here-string or expanding here-document in any registered script, with a self-test); 5c (no top-level emitter);
  5c2 (ask gates emit valid JSON for multi-line commands); 5c3 (`/freeze` decodes escaped and raw paths); section 6 (decisions read by
  `decision_of`, the AI-attribution and parser rows, non-ASCII raw and escaped payloads, five worktree rows); 7b (files-tracked deny and
  allow, the repository reader in worktrees, subdirectories, two repositories, `taom_is_project_repo`, the drift gate with a stub linter,
  the subject gate in a worktree cwd, the broad-add list); 7c (push guard rows); 7h (payloads in the here-string window); 7j (anchoring);
  7k (matcher, Serena denies, attribution).
- Structural patterns followed: the existing section 6 loop, the c79a58521 and 407570ece blocks as written.
- Structurally untestable here (for `Not-tested:` lines): whether Claude Code honours the nested decision in a live session (maintainer
  check after merge); whether `git cherry-pick` or `git revert` run `commit-msg`; the #681 hang on this laptop.

## Done criteria (whole plan)

- [ ] 10 commits on `port148/103-executor-safety-harness`, subjects exactly as in this plan, each passing `python tools/check_commit_msg.py`
      (for each: `git log -1 --format=%B <sha> > C:/Users/mikew/source/repos/taom-port/scratch/103/msg.txt` then the check)
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD` lists only in-scope paths; it includes no `CHANGELOG.md`, `Main/`, ModuleData, `docs/adrs/`, `.mcp.json` or `.claude/settings.local.json`
- [ ] `env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh`: every baseline-passing check passes and every section this plan adds passes
- [ ] `python -m unittest discover -s tools/tests -t .`: `Ran` = baseline + 103, no failure the baseline lacked
- [ ] `python tools/lint_docs.py --fail-on-drift` exit 0; `python tools/lint_docs.py --drift-only` exit 0 in under 3 s; the CLAUDE.md measure was 45,600 or less after A4 and is under 46,000 now
- [ ] `python tools/reviewctl.py lint` exit 0; `python tools/audit_claude_config.py --min HIGH` no new HIGH
- [ ] `git grep -n -F -e '{"permissionDecision' -e '{\"permissionDecision' -- .claude/hooks .claude/skills` prints nothing
- [ ] `git grep -c -F 'CLAUDE_PROJECT_DIR\"/.claude/hooks/' -- .claude/settings.json` prints `.claude/settings.json:26` (one per registration after WP102; the file stores the quotes escaped as `\"`)
- [ ] Build exit 0; Test 0 failed with the Step 0 total
- [ ] Final report (hand-off to the integrator, who records it in `plans/README.md`) lists: status, the 10 commit hashes, Step 0 baselines and timings,
      the ModuleData validator result, final suite numbers, any deviation. No old-plan row is closed by this plan.

## STOP conditions

Stop and report (do not improvise) if:

- A Step 0 precondition fails (WP101 or WP102 not merged, a `check-changelog` registration remains, the version is not `v2.0.28`,
  the git-gate group is not the 9 named hooks, or `decision_of`, `hookSpecificOutput`, `PYTHONIOENCODING` or `taom_git_repo_dir` already exist).
- The B2 `git apply` fails. Do not apply part of it; report which path failed and the WP102 change you see in it.
- `git cherry-pick -n 785b2703e` conflicts in any path other than `.claude/skills/verify/SKILL.md`.
- A line-number reference into a 1.5.x blob does not hold the content this plan names (for example `sed -n '51,73p'` of the
  c79a58521 test file does not start with the `decision_of` comment, or lines 33-43 and 50-53 of the 407570ece subject gate are not
  the prefilter and the `taom_hook_command` call).
- After all ten cuts and both additions, CLAUDE.md measures more than 45,600.
- The same verification fails twice after a reasonable fix, or a check that passed in the baseline starts failing.
- A step seems to need an edit to `.claude/settings.json` (staging the maintainer's change in C1 item 6 is the one allowed touch),
  `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*.md`, `CHANGELOG.md`, `Main/IoC.cs`, `Main/SubModule.cs` or `.mcp.json`.
- A hook running in your own session denies or asks about something this plan did not do (your session should run the trunk's gates;
  a live prompt means you started the session in the worktree or the trunk moved).
- `python tools/lint_docs.py --drift-only` takes 10 s or more, or the Step 0 ModuleData validator command takes 40 s or more (its gate
  would then be near its 45 s bound; report it, it is a follow-up, not a fix here).
- The maintainer declines or edits the Part C script. Parts A and B stay landable; report and stop.

## In-game checks owed

None in the game. **Live harness checks owed (maintainer, a fresh Claude Code session in the main checkout after the integrator merges
WP103 and the Part C settings are in):**

1. Make a scratch worktree: `git worktree add C:/Users/mikew/source/repos/taom-port/gate-proof -b scratch/gate-proof bannerlord-1.4.5`.
2. Through the Bash tool: `git -C C:/Users/mikew/source/repos/taom-port/gate-proof commit --allow-empty -m "docs: no label"` must be refused
   by the harness with the `[check-commit-subject-version]` reason (no commit appears in `git -C ... log -1`).
3. The same through the PowerShell tool: refused.
4. Run `cd tools` in the Bash tool, then repeat step 2: still refused (anchoring).
5. `git -C ... commit --allow-empty -m "docs: no label → probe"`: refused (UTF-8 stdio).
6. `git -C ... commit --allow-empty -m "docs: v2.0.28 - gate proof"`: allowed.
7. From a terminal outside Claude, after step 1 of Maintainer steps: `git -C ... commit --allow-empty -m "Add things"` is refused by
   `.githooks/commit-msg`; a labelled one passes.
8. Ask Claude to use `mcp__serena__replace_content` on any file: denied; `mcp__serena__find_symbol` still works.
9. `git -C ... add -A` through Claude: a confirm prompt listing the scratch worktree's files (decline it).
10. Clean up: `git worktree remove C:/Users/mikew/source/repos/taom-port/gate-proof` and `git branch -D scratch/gate-proof`
    (the second one prompts through `block-dangerous-git.sh`; approve it).

## Maintainer steps

1. Part C: review and run `C:/Users/mikew/source/repos/taom-port/scratch/103/settings.py` in the WP103 worktree (the only settings change in this plan).
2. After WP103 merges, once: `git config core.hooksPath .githooks` in the main checkout (repository-wide; every worktree whose branch has
   `.githooks/` then runs it). From then on every merge needs a labelled message, for example
   `git merge --no-ff -m "chore: v2.0.28 - merge WP104 plan 009" port148/104-...`.
3. Merge WP103 when no executor is mid-commit: from that moment their commits face real gates.
4. Create the tracking issue for the 1.4.x-only parts (outward-facing; not an executor step).
5. Run `/security-scan` after the merge (CLAUDE.md routes hook and settings changes there) and the live checks above.

## Maintenance notes

- After this lands the gates refuse and prompt. Forms that prompt (`block-dangerous-git.sh`, `block-broad-git-add.sh`) and stall an
  unattended run: `git reset --hard`, `git clean -f`, `git branch -D`, `git stash drop|clear`, `git checkout` with ` -- `, `-f` or `.`,
  `git restore` without `--staged`, `git add -A|-u|.`, `git commit -a`. The decisions brief's procedure (`git checkout --ours <path>`,
  `git checkout HEAD <path>`, `git cherry-pick --abort`, explicit `git add <paths>`) avoids them. An ask prompts even in bypass mode
  (observed on the 1.5.x line).
- The ModuleData gate refuses every ModuleData commit on this laptop while the validator reports errors (3,423 `BROKEN_ITEM_REF` measured
  2026-11-04, before WP101's Armory refresh; D20 already makes such commits a STOP). Its validator took 22.1 s against the 45 s inner bound.
- CLAUDE.md headroom after A4 and C1 is a few hundred bytes under 46,000; WP142, WP143 and WP144 must trim as they add.
- `tools/tests/test_dotnet_commands_flagged.py` fails when a later port brings an unflagged `dotnet build|test` into its folders; the fix
  is to add both flags in that port. Feature docs are swept but not gated (the 1.5.x tip still has 14 unflagged feature-doc lines).
- WP116 (wave 1) replaces `_shellwords.py` with its end state, adds `taom_hook_command` so the gates read PowerShell properly, ports the
  per-line push judge (closing the line-2 force-push gap validate-push still has), the plan 011 Stop-reminder channel, the plan 013
  prefilters and `mark-verification-run.sh`'s fix for commands carrying `-p:ModuleId=` (its env-prefix strip drops them today, so the
  verification marker is not set; the Stop reminder is invisible anyway). Until then run git through the Bash tool (D19).
- Serena is unpinned on 1.4.x (`.mcp.json` names `git+https://github.com/oraios/serena` with no commit), so a new Serena editing tool is
  not denied until someone re-derives the list.
- The later 1.5.x checker refinements (35ff005dc: crash exit 3, the closing-keyword rule) and the `pre-commit` hook (434d1d795) are not
  here; the tip `commit-msg` hook already treats a checker traceback as "not checked".
- Reviewer focus for this branch: the B2 `git apply` result against WP102's edits, the B4 subject-gate construction, the 7b block
  boundaries, and the maintainer script's output against the description in C1 item 3.
