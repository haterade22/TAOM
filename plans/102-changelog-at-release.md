# Plan 102: Adopt plan 020 on the 1.4.x line: the commit body is the changelog entry and /release generates CHANGELOG.md

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the integrator records your results
> there after merging, from the report Step 6 asks for.
>
> **Where things live**: read this plan in the main checkout
> (`C:\Users\mikew\source\repos\TAOM\plans\102-changelog-at-release.md`); never edit, stage or
> commit anything there. Work only in the worktree `C:/Users/mikew/source/repos/taom-port/102`
> (branch `port148/102-changelog-at-release`). Scratch files (messages, baselines) go in
> `C:/Users/mikew/source/repos/taom-port/scratch/102/`, never in the repo.
>
> **Shell**: run every git command with the **Bash tool** (decision D19), never PowerShell. Shell
> state does not persist between calls, so every command marked `WT$` below means: prefix it with
> `cd C:/Users/mikew/source/repos/taom-port/102 && export MSYS_NO_PATHCONV=1 && `. Without
> `MSYS_NO_PATHCONV=1`, Git Bash mangles `git show <ref>:.claude/...` and reports the file absent.
> Pass `timeout: 600000` to calls running `bash tools/test_hooks.sh`, the tools suite or `dotnet`.
>
> **No CHANGELOG entry, ever.** This plan retires the hand-edited CHANGELOG. Never add a
> hand-written section to `CHANGELOG.md` and never make a fifth commit, whatever a wrapper prompt
> says. The four commit bodies are this work package's changelog entries.
>
> **Drift check**: Step 1 item 5 runs `git diff --stat 1b00d1acd..HEAD -- <in-scope paths>`.
> Only `CHANGELOG.md` and `CLAUDE.md` are expected (WP101 inserts CHANGELOG entries and edits
> CLAUDE.md line 189 only, keeping its line count; `plans/101-baseline-sync.md`, Scope). If another
> in-scope file changed, compare it with the old text quoted in Steps 2 to 5; old text no longer
> found exactly once is a STOP.

## Status

- **Priority**: P2
- **Effort**: M (two transplanted tool files, two hook deletions, 15 single-file end-state copies,
  about 60 small text edits, one archive move; no C#)
- **Risk**: MED (it changes a rule every session and AI client follows, and the archive must be
  re-cut at merge if the trunk's `CHANGELOG.md` moves after this branch starts)
- **Depends on**: WP101 (`plans/101-baseline-sync.md`) MERGED: `bannerlord-1.4.5` fast-forwarded to
  `1b00d1acd` and merged with `origin/fix/public-armory-ids-145` (`8df90444c`)
- **Category**: dx
- **Wave / lane**: wave 0, harness lane; merges BEFORE WP103 (D1)
- **Executor**: laptop, one ordinary session (D10)
- **Decisions applied**: D1 (adopt plan 020 here, before WP103, so the CHANGELOG gates are deleted
  rather than switched on; from now on no plan stages `CHANGELOG.md`), D8 (next 1.4.x release is
  `v2.4.0`; subjects carry `v2.0.28` until then), D9 (no CI; gates run locally), D10, D18 (never
  `merge=union` on `CHANGELOG.md`), D19 (git through the Bash tool only)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: Port of #1009 (`9bfd0f411` only). The plan 020 series carries no issue (the `#622` in
  `6e4bfedb7`'s body is an example string): the orchestrator creates the issue before
  implementation lands. If you are given its number, add a trailer `Refs: #<n>` to each message.

## Why this matters

- The 1.4.x line tells every session to hand-edit `CHANGELOG.md` (CLAUDE.md:274 "CHANGELOG.md every
  session"; Step 4's OLDPHRASE grep matches 75 lines across instructions, hooks and settings)
  and registers two hooks that enforce it, yet none of the five commits after 2026-09-15
  (`f8c9c8ecb`, `55d779012`, `d906fc921`, `ccef172ad`, `1b00d1acd`) touched it:
  `git log c5b84fb41..origin/bannerlord-1.4.5 -- CHANGELOG.md` is empty.
- WP103 fixes the hook output contract (decisions brief, evidence "Harness (D2)": nine PreToolUse
  gates print a top-level `permissionDecision` Claude Code ignores). Once it lands,
  `check-changelog-changed.sh` would deny every commit staging `.claude/`, `CLAUDE.md` or
  `AGENTS.md` without `CHANGELOG.md` (D2 measured 246 of 655 port-ish commits). D1 deletes it first.
- `CHANGELOG.md` is 1,472,650 bytes and 19,381 lines on `origin/bannerlord-1.4.5`, the most
  contended file for parallel executors. 1.5.x made it a generated file on 2026-09-25 (plan 020,
  merge `55a729152`).
- The `v2.4.0` release (D8) needs the generator. It lists every non-merge commit since `v2.0.28`:
  118 on `origin/bannerlord-1.4.5` (14 labelled, 104 not; measured while planning), 121 once WP101
  merges the three #697 commits, plus the ports.

## Current state

Read with `git show origin/bannerlord-1.4.5:<path>` at `1b00d1acd`. WP101 changes none of these
paths except `CHANGELOG.md` (the #697 merge and two inserted entries) and `CLAUDE.md` line 189 (a
trap row, one byte shorter; no line moves). The #697 commits touch troop data, one test,
`.serena/project.yml`, two lessons files, an RCA and `CHANGELOG.md`.

### Facts the steps rely on

- Nothing from plan 020 is on 1.4.x: `git grep -l changelog_from_commits` and
  `git log -S changelog_from_commits` on the ref print nothing; `docs/changelog-archive/` holds only
  `CHANGELOG-2026-H1.md`.
- `.claude/hooks/` has 29 `*.sh` (incl. the sourced helper `_pybin.sh`); `.claude/settings.json`
  has 28 registrations across 9 events (Stop 4, PreToolUse 13), plus 5 skill-frontmatter ones.
  After this plan: 27 scripts, 26 registrations across 9 events, 31 with frontmatter.
- `.claude/settings.json` is blob `6e17738dd` at `1b00d1acd`, `c5b84fb41` and on the public
  branch. The registrations to delete are lines 42-46 (`check-changelog-changed.sh`, PreToolUse
  group `"matcher": "Bash"`) and 203-207 (`check-changelog-updated.sh`, first entry of `"Stop"`).
- `CHANGELOG.md` begins with a doubled BOM and the old `# CHANGELOG` title; newest section
  `## 2026-09-15` (line 5). The public branch adds `## 2026-09-30` (+42 lines); WP101 adds its own
  entry. No program reads the file: `git grep -n -i changelog` over `tools`, `TAOM.Tests`, `*.ps1`,
  `*.props`, `*.csproj` finds comments and the lint archive exemption only.
- `docs/reference/release-process.md:18` is the only doc link to the root `CHANGELOG.md`; it points
  at the `v2.0.12` crash reports, which sit in that file (lines 17277, 17583) and move to the archive.
- `tools/lint_docs.py` exempts `docs/changelog-archive/` from stale-version and dead-link checks
  (lines 69, 113, 122). Its dash check reads every changed `*.md` line, so DASHSCAN below excludes
  the archive.

### The generator (1.5.x end state, new on 1.4.x)

- `tools/changelog_from_commits.py` (blob `01614b4e485179544a441e45d9a3c19003e514ed`) and
  `tools/tests/test_changelog_from_commits.py` (blob `767f5b3db4858068238e69c7465408674efdac8d`)
  are identical at `9bfd0f411` and at the 1.5.x tip. Both are LF without BOM, so
  `git show 9bfd0f411:<path> > <path>` reproduces them exactly.
- `VERSION_ARG_RE = re.compile(r"^v\d+\.\d+\.\d+$")` (line 34) accepts `v2.4.0`. `--since` defaults
  to `git describe --tags --abbrev=0 --match v[0-9]* <until>` (lines 177-178): `v2.0.28` on the
  ref and on the public branch. `insert_section` (125-145) refuses a version already present and
  any hand-written `##`/`###`/`####` heading above the releases. The docstring (line 4) cites
  AGENTS.md "Documentation duty", which E1 creates.
- 35 tests: ParseLog 5, RenderSection 11, LabelParity 1, InsertSection 8, EndToEnd 10.
  `LabelParityTests` reads the line `^pat = re\.compile\(r"(.+)"\)$` from
  `.claude/hooks/check-commit-subject-version.sh`; on 1.4.x that is line 158, byte-identical to
  1.5.x's. Verified in memory while planning: the 25 non-EndToEnd tests pass on Python 3.14.7
  against the 1.4.x hook (the EndToEnd ones write a temp repo and were not run).

### Hooks and tests this plan touches

- `session-start.sh:33-42` is the degraded banner: it names the retired gate and omits
  `check-commit-subject-version`. Lines 160-167 print the newest dated CHANGELOG section
  (`awk '/^## [0-9]/...'`), which prints nothing once sections start `## v`.
- Gates with no jq path (a `taom_pybin_degraded` call without `jq` as third argument):
  `check-changelog-changed`, `check-claude-files-tracked`, `check-commit-subject-version`,
  `check-doc-config-drift`, `check-moduledata-validation`, `check-native-dll-crt`. None of these
  names appears in a non-comment line of `session-start.sh`, so test 5b2 starts RED with 6
  failures and ends GREEN with 5.
- `tools/test_hooks.sh` (509 lines) names neither CHANGELOG hook; it discovers hooks from
  `.claude/hooks/*.sh` and `settings.json`. Section 5b ends at line 439; line 441 is the separator
  before `head2 "6. check-commit-subject-version..."`; `ok`, `bad`, `head2` are at lines 34-36.

### Version scheme (D8)

`v2.0.28` is tag `8ce42fe85`, the newest tag reachable from `origin/bannerlord-1.4.5`. `v2.0.29`
(`a12fec9b8`) to `v2.0.36` (`aa708f620`) are reachable only from `origin/bannerlord-1.5.x`.
`v2.1.0` (`5472dd339`) and `v2.1.1` (`d91192a83`), 2026-08-19, are on no local or remote branch.
`Main/_Module/SubModule.xml` says `v2.0.28` on the ref and the public branch.

### Engine members

None. No C#, ModuleData, XSLT or Harmony target changes, so no TaleWorlds signature binds.

### Conventions that bind this change

- **TDD**: the generator tests land and run RED before the generator; test 5b2 lands and runs RED
  before the banner fix.
- **No em or en dash** (U+2014, U+2013) in any line you write or change in a `.md` file, outside
  code spans and fences (`.claude/rules/output-style.md`). Every new text below is already
  dash-free; old text is quoted verbatim, so some of it still shows the dashes being removed.
- **CLAUDE.md budget** (`tools/lint_docs.py:49`: 46,000 bytes on disk; table rows 400 chars, prose
  lines 600). It is 46,283 bytes as a blob, about 46,704 on disk, already over; WP103 trims it.
  This plan must not grow it (simulated on `1b00d1acd`: 46,283 to 46,282).
- **AGENTS.md** stays under 8,192 bytes (`tools/reviewctl.py:367`; simulated 3,902).
- **Skill frontmatter** must parse as YAML (`tools/test_hooks.sh` 3b); an unquoted `description:`
  holds no ` #` and no `: `. The four edited descriptions were parsed while planning.
- **Hooks fail open**; run `bash tools/test_hooks.sh` before committing any hook or
  `settings.json` change (`.claude/rules/hook-authoring.md`).
- **Untouched single-owner and protected files**: `Main/IoC.cs`, `Main/SubModule.cs`,
  `Main/TAOM.csproj`, `Directory.Build.props`, `docs/adrs/*.md`, `.claude/settings.local.json`.
  `.claude/settings.json` is edited only by the maintainer (Step 0); you stage and commit it.
- ADR-002/003/004/005/007, the MCM json2 rule and the MovementOrder rule do not apply (no C#).

## Commands you will need

| Purpose | Command (`WT$`) | Expected |
|---|---|---|
| Generator tests | `python tools/tests/test_changelog_from_commits.py` | `Ran 35 tests`, `OK` |
| Tools suite | `python -m unittest discover -s tools/tests -t .` | Step 1 count plus 35, no new failure or error |
| Hook suite | `bash tools/test_hooks.sh` | no `FAIL` line the Step 1 baseline lacks |
| Docs | `python tools/lint_docs.py --summary` | each count except `ai_dashes` no higher than baseline |
| Docs gate | `python tools/lint_docs.py --fail-on-drift > /dev/null 2>&1; echo "exit $?"` | same exit as baseline (1 expected: CLAUDE.md size) |
| AGENTS.md contract | `python tools/reviewctl.py lint` | `Shared contract OK`, exit 0 |
| Config audit | `python tools/audit_claude_config.py --min HIGH` | no HIGH or CRITICAL finding the baseline lacks |
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (BHA0001 Patch88 warning is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed; total equals the count the previous merged WP recorded in `plans/README.md` (read it in the main checkout; 9154 when only WP101 is merged) |

Both `-p:` flags are mandatory: on 1.4.x `Main/TAOM.csproj:133-136` does not gate CopyModule on
`DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game install. Never
`./build.ps1`. No commit touches C#, so dotnet runs once, in Step 6, as insurance.

## Scope

**In scope** (52 paths; nothing else may change):

- Commit 1: new `tools/changelog_from_commits.py`, new `tools/tests/test_changelog_from_commits.py`,
  `tools/README.md`.
- Commit 2: delete `.claude/hooks/check-changelog-changed.sh`, `.claude/hooks/check-changelog-updated.sh`;
  stage `.claude/settings.json` (Step 0); edit `.claude/hooks/{session-start,block-broad-git-add,check-moduledata-validation,check-version-tagged}.sh`,
  `.claude/rules/{harness-facts,hook-authoring}.md`, `docs/reference/hooks-catalog.md`, `CLAUDE.md`,
  `tools/test_hooks.sh`.
- Commit 3 (36 paths, listed in full in Step 4 item 4): `AGENTS.md`, `CLAUDE.md`, `README.md`,
  `.ai/roles/builder.md`, `.agents/skills/taom-build/SKILL.md`,
  `.serena/memories/task_completion_checklist.md`, five `.claude/rules/*.md`, thirteen
  `.claude/skills/*/SKILL.md`, five `docs/ai-includes/*.md`, five `docs/features/*.md`,
  `docs/modding/module-taom.md`, `docs/reference/release-process.md`.
- Commit 4: `CHANGELOG.md` (becomes a header), new
  `docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md` (by `git mv`),
  `docs/reference/release-process.md` (one link).

**Out of scope** (do NOT touch):

- `.claude/settings.json` through any tool or shell command, and the protected or single-owner
  files under "Conventions". Any `.cs`, `.csproj`, `.props`, ModuleData `.xml`/`.xslt`, `Main/**`,
  `TAOM.Tests/**`, `Dependencies/**`.
- `plans/**`, `docs/reviews/**` (lessons, REVIEW-LOG, RCAs), `docs/INDEX.md`,
  `docs/reference/feature-map.md`, `docs/adrs/**`, `docs/archive/**`, `docs/audits/**`,
  `docs/changelog-archive/CHANGELOG-2026-H1.md`.
- Lines that name CHANGELOG as a kind of prose or record history, for example
  `CLAUDE.md:35,36,329,330,332,334,335`, `.claude/rules/hook-authoring.md:59,65`,
  `.claude/rules/output-style.md`, `.claude/rules/evidence-over-claims.md:59`,
  `.claude/rules/provenance.md`, `.claude/agents/refactoring-specialist.md:75`,
  `.claude/skills/humanizer/SKILL.md`, `.claude/skills/engine-bump/SKILL.md:143`,
  `.claude/skills/improve/SKILL.md:9`, `.claude/skills/improve/references/audit-playbook.md:101`,
  `.claude/skills/lint-docs/SKILL.md:16`, `.claude/hooks/block-broad-git-add.sh:19,21`,
  `docs/ai-includes/codex-operating-guide.md:40`, `docs/reference/mcp-servers.md:30`,
  `docs/features/bandit-management.md:287`, `docs/modding/module-taom.md:234`, and every code or
  test comment citing "CHANGELOG <date>".
- The plan 020 review records and lessons from 1.5.x, and everything WP103 owns (hook output
  contract, `PYTHONIOENCODING`, anchored registrations, CLAUDE.md trim, Serena denies, PowerShell
  matchers, `-p:ModuleId=` sweeps).

## Git workflow

- Worktree `C:/Users/mikew/source/repos/taom-port/102`, branch `port148/102-changelog-at-release`,
  from local `bannerlord-1.4.5` after WP101. Commit on this branch only; never push, open a PR,
  tag, merge or rebase; never touch the main checkout or any stash.
- Four commits, in order (subjects measured at 68, 70, 64 and 62 characters):
  1. `feat(tools): v2.0.28 - CHANGELOG release section from the commit log`
  2. `chore(hooks): v2.0.28 - retire the two CHANGELOG hooks and their rules`
  3. `docs(workflow): v2.0.28 - the commit body is the changelog entry`
  4. `chore(changelog): v2.0.28 - archive the hand-written CHANGELOG`
- Stage explicit paths only (`git add <paths>`, `git rm`, `git mv`); never `git add -A`, `.`, `-u`
  or `git commit -a`. Stage in one Bash call, then run `git commit -F <file>` in the NEXT
  sequential call, never in the same command and never in parallel. `core.autocrlf` is true, so
  `warning: in the working copy of '<path>', LF will be replaced by CRLF` on files copied with
  `git show` is expected and harmless.
- Write each message with the **Write tool** to
  `C:\Users\mikew\source\repos\taom-port\scratch\102\msg-<n>.txt`, using the drafts in the steps
  (change a fact only if your measurement differs, and say so in the report). Lines at most 72
  characters, no em or en dash, no `Co-Authored-By` or "Generated with" line (CLAUDE.md "Commits"
  forbids AI attribution), trailers last.
- Never `cherry-pick`, `--continue`, `-C`, `-c`, `--amend --no-edit` or `--no-verify`; never
  `git checkout <path>` or `git restore`. If a committed message is wrong and
  `git log -1 --format=%s` shows your own subject, fix it with `git commit --amend -F <file>`
  changing nothing else.
- **MSGCHECK** (on each message file before its commit, with `msg-1.txt` changed to the file you
  are checking; expected `MSGCHECK OK`):

```bash
WT$ python - C:/Users/mikew/source/repos/taom-port/scratch/102/msg-1.txt <<'PY'
import re, sys
b = open(sys.argv[1], 'rb').read()
t = b.decode('utf-8'); L = t.splitlines(); bad = []
if b[:3] == b'\xef\xbb\xbf': bad.append('BOM')
if not re.match(r'^[a-z][a-z0-9]*(?:\([^)]+\))?!?: v2\.0\.28 - \S', L[0]): bad.append('subject label')
if len(L[0]) > 72: bad.append('subject over 72')
if len(L) < 3 or L[1] != '': bad.append('line 2 must be blank')
bad += ['line %d over 72' % (i + 1) for i, l in enumerate(L) if len(l) > 72]
if '\u2014' in t or '\u2013' in t: bad.append('em or en dash')
if re.search(r'(?im)^(co-authored-by|.*generated with)', t): bad.append('AI attribution')
if re.search(r'(?m)^ {0,3}#', t): bad.append('line starting with #')
print('MSGCHECK', 'OK' if not bad else '; '.join(bad))
PY
```

- **STATUS** is `WT$ git status --porcelain`; expected output is given per step. A
  ` M .serena/project.yml` line (Serena rewrites that file) is never yours: leave it unstaged.
- `BASE` below means `$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE)`.

## Steps

### Step 0: Maintainer, before dispatch (not the executor)

`.claude/hooks/config-protection.sh` blocks agents from `settings.json`, so the maintainer makes
this edit in an editor inside the worktree and leaves it uncommitted.

1. If the worktree does not exist:
   `git -C C:/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/102 -b port148/102-changelog-at-release bannerlord-1.4.5`
2. In `C:\Users\mikew\source\repos\taom-port\102\.claude\settings.json` delete the two five-line
   objects; nothing else changes:

~~~diff
--- a/.claude/settings.json
+++ b/.claude/settings.json
@@ -39,11 +39,6 @@
             "command": ".claude/hooks/validate-push.sh",
             "timeout": 5
           },
-          {
-            "type": "command",
-            "command": ".claude/hooks/check-changelog-changed.sh",
-            "timeout": 5
-          },
           {
             "type": "command",
             "command": ".claude/hooks/check-claude-files-tracked.sh",
@@ -200,11 +195,6 @@
       {
         "matcher": "",
         "hooks": [
-          {
-            "type": "command",
-            "command": ".claude/hooks/check-changelog-updated.sh",
-            "timeout": 10
-          },
           {
             "type": "command",
             "command": ".claude/hooks/check-deep-review.sh",
~~~

3. Check: `git -C C:/Users/mikew/source/repos/taom-port/102 diff --numstat -- .claude/settings.json`
   prints `0	10	.claude/settings.json`. (Simulated while planning: the result parses as JSON and
   holds 26 registrations across 9 events.)

### Step 1: Verify the worktree, the prerequisites and the baselines

1. If the worktree is missing, create it with Step 0 item 1. `WT$ git rev-parse --abbrev-ref HEAD`
   prints `port148/102-changelog-at-release`.
2. `WT$ git merge-base --is-ancestor 1b00d1acd HEAD && git merge-base --is-ancestor 8df90444c HEAD && echo WP101-OK`
   prints `WP101-OK`.
3. `WT$ mkdir -p C:/Users/mikew/source/repos/taom-port/scratch/102 && git merge-base HEAD bannerlord-1.4.5 > C:/Users/mikew/source/repos/taom-port/scratch/102/BASE && cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE`
   prints a 40-hex SHA.
4. `WT$ grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1; git describe --tags --abbrev=0 --match 'v[0-9]*' HEAD`
   prints `<Version value="v2.0.28"` then `v2.0.28`.
5. Drift check (expected: a `CHANGELOG.md` line, a `CLAUDE.md` line showing `2 +-` or less, and
   the summary line):

```bash
WT$ git diff --stat 1b00d1acd..HEAD -- CHANGELOG.md AGENTS.md CLAUDE.md README.md .ai/roles/builder.md .agents/skills/taom-build/SKILL.md .serena/memories/task_completion_checklist.md .claude/settings.json .claude/hooks .claude/rules .claude/skills docs/ai-includes docs/features/TEMPLATE.md docs/features/gondor-ithilien-ranger.md docs/features/gui-sprite-system.md docs/features/multi-culture-armor-revamp.md docs/features/troop-progression.md docs/modding/module-taom.md docs/reference/hooks-catalog.md docs/reference/release-process.md docs/changelog-archive tools/README.md tools/test_hooks.sh tools/changelog_from_commits.py tools/tests/test_changelog_from_commits.py
```

6. End-state copies are safe only while these 15 files still equal the plan 020 pre-image
   (`bec0389da`, the parent of `ae85381dc`). Expected: 15 `SAME` lines, then `generator absent`.

```bash
WT$ B=$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE); for f in .claude/hooks/check-version-tagged.sh .agents/skills/taom-build/SKILL.md .ai/roles/builder.md .claude/rules/gui-ui.md .claude/skills/author-armor/SKILL.md .claude/skills/commit-split/SKILL.md .claude/skills/finish-branch/SKILL.md .claude/skills/lint-cleanup-loop/SKILL.md .claude/skills/new-adr/SKILL.md .claude/skills/scope-check/SKILL.md .serena/memories/task_completion_checklist.md docs/ai-includes/codex-operating-guide.md docs/features/TEMPLATE.md docs/features/gondor-ithilien-ranger.md docs/features/gui-sprite-system.md; do [ "$(git rev-parse "$B:$f")" = "$(git rev-parse "bec0389da:$f")" ] && echo "SAME $f" || echo "DIFF $f"; done; git cat-file -e "$B:tools/changelog_from_commits.py" 2>/dev/null && echo "generator PRESENT" || echo "generator absent"
```

7. Step 0 is in place: `WT$ git diff --numstat -- .claude/settings.json` prints
   `0	10	.claude/settings.json`, `WT$ grep -c check-changelog .claude/settings.json` prints `0`, and
   `WT$ python -c "import json; json.load(open('.claude/settings.json', encoding='utf-8')); print('JSON OK')"`
   prints `JSON OK`. If any fails, you may still do Step 2; then STOP before Step 3.
8. Baselines:

```bash
WT$ S=C:/Users/mikew/source/repos/taom-port/scratch/102; python -m unittest discover -s tools/tests -t . > $S/base-tools.txt 2>&1; echo "tools exit $?"; tail -4 $S/base-tools.txt
WT$ S=C:/Users/mikew/source/repos/taom-port/scratch/102; bash tools/test_hooks.sh > $S/base-hooks.txt 2>&1; echo "hooks exit $?"; grep 'FAIL' $S/base-hooks.txt | sort > $S/base-hooks-fail.txt; tail -3 $S/base-hooks.txt
WT$ S=C:/Users/mikew/source/repos/taom-port/scratch/102; python tools/lint_docs.py --summary > $S/base-lint.txt 2>&1; python tools/lint_docs.py --fail-on-drift > /dev/null 2>&1; echo "drift exit $?" >> $S/base-lint.txt; cat $S/base-lint.txt
WT$ S=C:/Users/mikew/source/repos/taom-port/scratch/102; python tools/audit_claude_config.py --min HIGH > $S/base-audit.txt 2>&1; echo "audit exit $?"; git cat-file -s HEAD:CLAUDE.md | tee $S/base-claude-size.txt
```

**Verify**: items 1 to 7 print what they say; item 8 wrote six files (`base-tools.txt`,
`base-hooks.txt`, `base-hooks-fail.txt`, `base-lint.txt`, `base-audit.txt`, `base-claude-size.txt`).
Non-zero exits in item 8 are not a STOP, because the baselines record them: on a simulated post-WP101
tree the tools suite ran 1501 tests with 4 failures and 1 error (all data or install checks), and
drift exited 1. STATUS prints ` M .claude/settings.json` only.

### Step 2: The generator and its tests (commit 1)

Method: end-state transplant. Both files are new on 1.4.x, and their `9bfd0f411` state equals the
1.5.x tip, so `ae85381dc`, `6e4bfedb7`, `971d8e975`, `5b242cee6` and `9bfd0f411` arrive as one.
Replaying them conflicts on every pick (simulated this session with `git merge-tree`: `ae85381dc`
in `tools/README.md`; the follow-ups because the tool file does not exist on 1.4.x).

1. RED: `WT$ git show 9bfd0f411:tools/tests/test_changelog_from_commits.py > tools/tests/test_changelog_from_commits.py && python tools/tests/test_changelog_from_commits.py; echo "exit $?"`
   ends with `ModuleNotFoundError: No module named 'changelog_from_commits'` and `exit 1`.
2. GREEN: `WT$ git show 9bfd0f411:tools/changelog_from_commits.py > tools/changelog_from_commits.py && python tools/tests/test_changelog_from_commits.py 2>&1 | tail -4; echo "exit ${PIPESTATUS[0]}"`
   shows `Ran 35 tests`, `OK`, `exit 0`.
3. `WT$ git hash-object tools/changelog_from_commits.py tools/tests/test_changelog_from_commits.py`
   prints `01614b4e485179544a441e45d9a3c19003e514ed` then `767f5b3db4858068238e69c7465408674efdac8d`.
4. Read `tools/README.md`; Edit it to add one row directly after the `build_backlinks.py` row
   (line 257, section "Docs & knowledge base"). Old (unique):
   ``| `build_backlinks.py` | Regenerate the `<!-- backlinks-start -->` "Referenced by" footers in `docs/`. | (see `--help`) |``
   New: that same line, then a line break, then:

~~~text
| `changelog_from_commits.py` | **Writes one `CHANGELOG.md` release section** (pure stdlib), run by `/release` Phase 4: every non-merge commit since the previous `v*` tag, the labelled ones (`<type>[(scope)]: vX.Y.Z - <description>`) grouped by type with subject, short SHA and body verbatim (a body line that would be a Markdown heading is escaped), the rest in a last "Commits without the version label" group. Refuses (exit 2) a version already present or a hand-written heading above the releases, and names the commit the range ends at, which `/release` requires to be its release commit's parent before it tags the release commit; the commit body is the changelog entry, so nobody else edits the file. Tested by `tools/tests/test_changelog_from_commits.py`. | `--version` (required), `--write`, `--since`, `--until`, `--date`, `--repo` |
~~~

5. `WT$ python -m unittest discover -s tools/tests -t . 2>&1 | tail -4`: run count is the
   `base-tools.txt` count plus 35; no failure or error that `base-tools.txt` lacks.
6. `WT$ git add tools/changelog_from_commits.py tools/tests/test_changelog_from_commits.py tools/README.md && git diff --cached --name-status`
   prints `A` for the two tool files and `M tools/README.md`, nothing else.
7. Write `msg-1.txt`, MSGCHECK it, then `WT$ git commit -F C:/Users/mikew/source/repos/taom-port/scratch/102/msg-1.txt`:

~~~text
feat(tools): v2.0.28 - CHANGELOG release section from the commit log

Adds tools/changelog_from_commits.py, the generator /release now uses
to write CHANGELOG.md. It reads every non-merge commit since the
previous v* release tag and groups the commits whose subject carries
the version label by type (Features, Fixes, Balance, Data,
Performance, Refactoring, Tests, Documentation, Chores, then any other
type alphabetically). Commits without the label get a last group of
their own, so nothing is lost. Each entry is the subject, the short
SHA and the body. A body line that Markdown would read as a heading is
escaped, and a commit that landed twice with the same subject and body
is listed once.

Without --write it prints the section; with --write it inserts it
above the newest release section of CHANGELOG.md. It refuses (exit 2)
a malformed --version, an empty range, a version that already has a
section and any hand-written heading above the release sections. Its
summary names the commit the range ends at, which /release checks
before it tags.

On this line the range starts at v2.0.28, the newest release tag
reachable from bannerlord-1.4.5, and the next release is v2.4.0.

tools/tests/test_changelog_from_commits.py holds 35 tests, including a
check that the label pattern matches the subject gate in
.claude/hooks/check-commit-subject-version.sh in both directions.

Not-tested: a live /release run; v2.4.0 will be the first.
Ported-from: ae85381dc3666b9869692930c1f57a9df0b6d0b5
Ported-from: 6e4bfedb7769bdc1d45d3cf8f90f1564851b9015
Ported-from: 971d8e97581cf6a43d7f9671eb9b19b25cb07c76
Ported-from: 5b242cee660b1675f2c3f582f91130bd9a85cd35
Ported-from: 9bfd0f4117f52de59a259df04a029ebd9c9f1590
~~~

**Verify**: `WT$ git show --stat --format=%s HEAD` shows the commit 1 subject and three paths;
STATUS prints ` M .claude/settings.json` only.

### Step 3: Retire the two CHANGELOG hooks (commit 2)

Method: hand re-implementation of `c91bb95f8` plus the banner and 5b2 parts of `6e4bfedb7`,
`971d8e975` and `5b242cee6`. Simulated picks conflict (`c91bb95f8`: `check-changelog-changed.sh`,
`harness-facts.md`, `hooks-catalog.md`, `tools/test_hooks.sh`), and the 1.4.x files differ from
the 1.5.x pre-images. `c91bb95f8`'s `PF_NARROWED_LIST` and prefilter-count hunks are skipped:
plan 013's prefilter is not on 1.4.x. Start only if Step 1 item 7 passed. Read each file first.
To delete a line with the Edit tool, use the line before it plus the line itself as old text and
the line before it alone as new text (this applies in Steps 3 and 4). Line numbers in Steps 3 to
5 are those of the unedited file at `1b00d1acd`; once an earlier edit in the same file has shifted
them, locate the quoted text instead.

1. RED. Edit `tools/test_hooks.sh`: immediately before the separator line that precedes
   `head2 "6. check-commit-subject-version: subject cases against the real SubModule.xml"`, insert
   this block and one empty line. Use as old text the two lines
   `# ---------------------------------------------------------------------------` and that
   `head2 "6. ...` line (unique together); new text is the block, an empty line, then those two
   lines. The block is `5b242cee6`'s 5b2 with one comment line made true for 1.4.x:

~~~text
# ---------------------------------------------------------------------------
# 5b2. The degraded-toolchain banner names every python-only gate. A gate with no jq
#      path dies whenever python is missing, and its exit-0 stderr never reaches Claude
#      (harness-facts.md), so session-start.sh's banner is the only signal. A hand-kept
#      list there omitted check-commit-subject-version until this check was added.
# ---------------------------------------------------------------------------
head2 "5b2. the degraded banner names every python-only gate"
# Only the call site's third argument marks a jq path (_pybin.sh), and only non-comment lines
# of session-start.sh print anything: a name in a comment there is not in the banner. Any
# non-comment call counts, whatever precedes it (`if`, `&&`), and a run that selects no gate
# fails, so the check cannot pass having checked nothing.
SS_CODE=$(grep -v '^[[:space:]]*#' .claude/hooks/session-start.sh)
n5b2=0
for f in .claude/hooks/*.sh; do
    name=$(basename "$f" .sh)
    call=$(grep -v '^[[:space:]]*#' "$f" | grep 'taom_pybin_degraded[[:space:]]') || continue
    grep -qE 'taom_pybin_degraded +"[^"]*" +"[^"]*" +jq( |$)' <<<"$call" && continue
    n5b2=$((n5b2+1))
    if grep -q -- "$name" <<<"$SS_CODE"; then
        ok "$name is named in the session-start degraded banner"
    else
        bad "$name has no jq path, so it fails open without python, but session-start.sh does not name it"
    fi
done
(( n5b2 > 0 )) || bad "5b2 found no python-only gate call site, so it checked nothing"
~~~

   **Verify RED**: `WT$ bash tools/test_hooks.sh 2>&1 | grep 'FAIL' | grep -c 'session-start.sh does not name it'`
   prints `6` (the `FAIL` filter matters: the suite's closing "Failures:" list repeats each message
   without that word, so an unfiltered count is 12).
2. `WT$ git rm .claude/hooks/check-changelog-changed.sh .claude/hooks/check-changelog-updated.sh`
   prints two `rm '...'` lines.
3. `.claude/hooks/session-start.sh` edit A: replace lines 33-42, from the line
   `# Two separate conditions, deliberately NOT ANDed. Five gates` through the line
   `    echo "    claude-files-tracked, doc-config-drift, moduledata-refs, native-DLL-CRT."`, with:

~~~text
# Two separate conditions, deliberately NOT ANDed. The gates with no jq path
# (check-claude-files-tracked, check-commit-subject-version, check-doc-config-drift,
# check-moduledata-validation, check-native-dll-crt) call "$PYBIN" unconditionally, so they
# die on a missing python whether jq is present or not. ANDing the two conditions hid
# exactly that case. tools/test_hooks.sh 5b2 fails when a python-only gate's file name is
# missing from the echo lines below (it ignores comments, this one included).
if [[ -z "${PYBIN:-}" ]]; then
    echo ""
    echo "!!! HOOK TOOLCHAIN DEGRADED: no safe python resolved. !!!"
    echo "    The python-only gates are failing OPEN right now: check-claude-files-tracked,"
    echo "    check-commit-subject-version, check-doc-config-drift,"
    echo "    check-moduledata-validation, check-native-dll-crt."
~~~

4. `.claude/hooks/session-start.sh` edit B: delete lines 160-168, from
   `# Latest CHANGELOG entry (date + feature titles only)` through the empty line after its `fi`,
   so that `git log --oneline -5 ...`, one empty line and `# Uncommitted changes count` follow
   each other.
5. `.claude/hooks/block-broad-git-add.sh:111`: old
   `    # different thing entirely and is gated by check-changelog-changed.sh.`, new
   `    # different thing entirely.` (keep the four leading spaces).
6. `.claude/hooks/check-moduledata-validation.sh:35`: old
   `# Extract the bash command from tool_input (mirrors check-changelog-changed.sh).`, new
   `# Extract the bash command from tool_input.`
7. `WT$ git show 5b242cee6:.claude/hooks/check-version-tagged.sh > .claude/hooks/check-version-tagged.sh && git hash-object .claude/hooks/check-version-tagged.sh`
   prints `da75f69734fd9c9e4aca1b4e84c54b19dabeac6b` (`c91bb95f8`'s one comment change).
8. `.claude/rules/harness-facts.md:93`: replace the whole line (it starts
   ``3. **If you're committing changes touching `.claude/`**`` and names
   `check-changelog-changed.sh`) with:

~~~text
3. **If you're committing changes touching `.claude/`**: the hook `check-claude-files-tracked.sh` will **hard-block** the commit if any file under `.claude/{skills,agents,rules,hooks}/` exists on disk but is gitignored or untracked. It fires on amends too; there is no blanket `--amend` exemption (a Codex review on 2026-04-26 caught this as a recursion-risk; amend is commonly used as "oops forgot a file" workflow, exactly the case the gate must catch). NOTE: this hook fires only when Claude Code invokes Bash via the tool dispatch; it does NOT fire when a user types `git commit` directly in a shell outside Claude. It is prevention for Claude-driven commits, not a global git pre-commit hook. No hook gates `CHANGELOG.md` any more: `/release` generates it and the commit body is the changelog entry.
~~~

   Also line 57, two fragments (it names the retired Stop reminder, and DASHSCAN reads every changed
   line, so its dash goes too): old ``NOT `Stop` — `Stop` fires every turn``, new
   ``NOT `Stop`: `Stop` fires every turn``; old `(changelog/deep-review/verification-evidence)`, new
   `(deep-review/verification-evidence)`.
9. `.claude/rules/hook-authoring.md:41`: old
   ``**Reference pattern** (used by `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, and `suggest-compact.sh`):``,
   new ``**Reference pattern** (used by `check-claude-files-tracked.sh` and `suggest-compact.sh`):``.
   Line 59 keeps its historical mention.
10. `docs/reference/hooks-catalog.md`, four edits:
    - Line 3, old fragment
      ``**Recounted 2026-09-13 (after `check-commit-subject-version.sh`): 29 scripts on disk, 28 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 33 total.**``,
      new fragment
      ``**Recounted when plan 020 retired the two CHANGELOG hooks: 27 scripts on disk, 26 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 31 total.**``.
    - Delete line 24, the row starting ``| `check-changelog-updated.sh` | Stop |``.
    - Line 26 (`session-start.sh` row), two fragments: old
      `Prints branch, recent commits, CHANGELOG summary on startup.`, new
      `Prints branch and recent commits on startup.`; old
      `when neither resolves — it produced total silence`, new
      `when neither resolves; it produced total silence`.
    - Delete line 38, the row starting ``| `check-changelog-changed.sh` | PreToolUse (Bash) |``.
11. `CLAUDE.md:370` (Hooks section), two fragments: old
    ``33 hook registrations: 28 in `settings.json` across 9 events``, new
    ``31 hook registrations: 26 in `settings.json` across 9 events``; old
    ``29 scripts on disk, one of which (`_pybin.sh`) is a sourced helper, not a registration (recounted 2026-09-13 after `check-commit-subject-version.sh`).``,
    new
    ``27 scripts on disk, one of which (`_pybin.sh`) is a sourced helper, not a registration (recounted when plan 020 retired the two CHANGELOG hooks).``
12. GREEN:

```bash
WT$ S=C:/Users/mikew/source/repos/taom-port/scratch/102; bash tools/test_hooks.sh --verbose > $S/s3-hooks.txt 2>&1; echo "hooks exit $?"; grep -c 'is named in the session-start degraded banner' $S/s3-hooks.txt; grep -c 'does not name it' $S/s3-hooks.txt; grep 'FAIL' $S/s3-hooks.txt | sort | comm -13 $S/base-hooks-fail.txt -
```

    **Expected**: `5`, then `0`, then no output from `comm` (if the only new FAIL is a timing line
    containing `took` or `HUNG`, re-run once before treating it as a failure).
13. `WT$ git ls-files .claude/hooks | grep -c '\.sh$'` prints `27`;
    `WT$ python -c "import json; d=json.load(open('.claude/settings.json',encoding='utf-8')); print(sum(len(g.get('hooks',[])) for gs in d['hooks'].values() for g in gs), len(d['hooks']))"`
    prints `26 9`.
14. `WT$ git add .claude/settings.json .claude/hooks/session-start.sh .claude/hooks/block-broad-git-add.sh .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-version-tagged.sh .claude/rules/harness-facts.md .claude/rules/hook-authoring.md docs/reference/hooks-catalog.md CLAUDE.md tools/test_hooks.sh && git diff --cached --name-status | wc -l`
    prints `12` (two `D`, ten `M`).
15. Write `msg-2.txt`, MSGCHECK it, then commit in the next call:

~~~text
chore(hooks): v2.0.28 - retire the two CHANGELOG hooks and their rules

/release now generates CHANGELOG.md from commit subjects and bodies,
so the rule that every session hand-edits the file is gone, and the
two hooks that enforced it go with it.

check-changelog-changed.sh (PreToolUse on Bash) denied a git commit
that staged anything under .claude/, CLAUDE.md or AGENTS.md without
CHANGELOG.md. check-changelog-updated.sh (Stop) printed a reminder
when source files were dirty and CHANGELOG.md was not. Both scripts
are deleted, and their two registrations are gone from
.claude/settings.json; the maintainer made that edit, because
config-protection keeps agents out of the file.

session-start.sh no longer prints the newest dated CHANGELOG section.
Its degraded-toolchain banner now names all five python-only gates by
file name: it used to name the retired gate and leave out
check-commit-subject-version. tools/test_hooks.sh 5b2 fails when a
gate with no jq path is missing from that banner. Comments that named
a retired hook are trimmed, hooks-catalog.md and CLAUDE.md are
recounted (27 scripts, 26 settings.json registrations across 9
events, 31 with the five skill frontmatter registrations), and
harness-facts.md and hook-authoring.md stop citing the retired gate.

Not-tested: a session started on the new settings.json. Sessions that
  are already running keep calling the two deleted scripts until they
  restart.
Rejected: keeping check-changelog-changed.sh as a gate against hand
  edits. The generator already refuses a hand-written section, so a
  rewritten commit gate adds code for no new protection.
Ported-from: c91bb95f8d3d1d1008df0569b12cd23ff49d7775
Ported-from: 6e4bfedb7769bdc1d45d3cf8f90f1564851b9015
Ported-from: 971d8e97581cf6a43d7f9671eb9b19b25cb07c76
Ported-from: 5b242cee660b1675f2c3f582f91130bd9a85cd35
~~~

**Verify**: `WT$ git show --stat --format=%s HEAD` shows the commit 2 subject and 12 paths;
STATUS prints nothing.

### Step 4: The commit body is the changelog entry (commit 3)

Method: 14 files still equal the plan 020 pre-image (Step 1 item 6), so they take the plan 020
end state (`5b242cee6`) whole, which is exactly `30b54df75`'s and `6e4bfedb7`'s changes to them.
The other 22 differ from their 1.5.x pre-images (simulated picks: `30b54df75` conflicts in 10
paths, `6e4bfedb7` in 19) and are hand-edited. Dropped hunks: `30b54df75`'s
`armory-audit/SKILL.md` and `deep-review/lenses/4-completeness.md` (absent on 1.4.x, where the
completeness check lives in `deep-review/SKILL.md` Agent 4: E13 to E14); `6e4bfedb7`'s
`lenses/6-design.md`, `lenses/7-xml.md`, `docs/ai-includes/git-and-commits.md` (absent; its
"inverse tell" change goes to CLAUDE.md in E6) and `docs/migration/v1.5.2-impact.md`.

1. End-state copies (expected: 14 lines starting `OK`):

```bash
WT$ for f in .agents/skills/taom-build/SKILL.md .ai/roles/builder.md .claude/rules/gui-ui.md .claude/skills/author-armor/SKILL.md .claude/skills/commit-split/SKILL.md .claude/skills/finish-branch/SKILL.md .claude/skills/lint-cleanup-loop/SKILL.md .claude/skills/new-adr/SKILL.md .claude/skills/scope-check/SKILL.md .serena/memories/task_completion_checklist.md docs/ai-includes/codex-operating-guide.md docs/features/TEMPLATE.md docs/features/gondor-ithilien-ranger.md docs/features/gui-sprite-system.md; do git show "5b242cee6:$f" > "$f" && [ "$(git hash-object "$f")" = "$(git rev-parse "5b242cee6:$f")" ] && echo "OK $f" || echo "BAD $f"; done
```

2. Hand edits. Read each file, then Edit. Every old text was checked to occur exactly once at
   `1b00d1acd`; "replace lines X-Y, from A through B" means old text is those whole lines, and
   line numbers are those of the unedited file (E25 and E26 shift the later release-skill lines).

**E1** `AGENTS.md`: after line 41 (`  build, documentation or local-review request. See the shared policy.`)
and its empty line, before `## Code Review Rules`, insert this section and one empty line:

~~~text
## Documentation duty

- The commit body is the changelog entry: write it for a reader of the release
  note. `/release` generates `CHANGELOG.md` from commit subjects and bodies;
  never edit that file by hand.
~~~

**E2** `CLAUDE.md:64` (the line's em dash goes too), old fragment, which ends the line:
``or single commands — descriptions load eagerly into every conversation, a permanent context tax. New skills: description <=30 words, follow `.claude/rules/external-skill-ports.md`, register in the routing table below, update CHANGELOG.``
New fragment:
``or single commands: descriptions load eagerly into every conversation, a permanent context tax. New skills: description <=30 words, follow `.claude/rules/external-skill-ports.md`, register in the routing table below.``

**E3** `CLAUDE.md:106`: old `→ close issue → update CHANGELOG.`, new `→ close issue.`

**E4** `CLAUDE.md:274`: old `- **CHANGELOG.md every session.** ADRs`, new
``- **The commit body is the changelog entry** (`/release` writes `CHANGELOG.md`). ADRs``

**E5** `CLAUDE.md:277`: old `-> issue + feature doc + CHANGELOG.`, new `-> issue + feature doc.`

**E6** `CLAUDE.md:340`: old ``a CHANGELOG entry in `HEAD` whose code is absent``, new
``a feature doc or commit body in `HEAD` whose code is absent``

**E7** `README.md:166`: old `  closeout (issue, feature doc, CHANGELOG).`, new
`  closeout (issue, feature doc, commit body as the changelog entry).`

**E8** `.claude/rules/external-skill-ports.md:121`: replace the whole line (starts
`2. **Update CHANGELOG.md** in the same commit.`) with
``2. **Describe the port in the commit body**: it is the changelog entry (`/release` generates `CHANGELOG.md` from commit bodies).``

**E9** `.claude/rules/external-skill-ports.md:130`: replace the whole line (starts
`- **3 process violations**`) with
`- **3 process violations**: CHANGELOG missed twice; counter math off by one. The pre-commit hook that caught the CHANGELOG half retired when the commit body became the changelog entry.`

**E10** `.claude/rules/simplicity-criterion.md:16`: old `flag the trade-off in PR / CHANGELOG |`,
new `flag the trade-off in the PR or commit body |`

**E11** `.claude/rules/troops.md:22`: old ``| 7. CHANGELOG | `CHANGELOG.md` | Document the changes |``,
new ``| 7. Commit body | the commit message | Describe the changes; `/release` builds `CHANGELOG.md` from it |``

**E12** `.claude/rules/working-discipline.md:29`: old `(commit message, CHANGELOG, log)`, new `(commit body, log)`

**E13** `.claude/skills/deep-review/SKILL.md:236` (inside the Agent 4 prompt): replace the whole
line (starts `5. **CHANGELOG Updated:**`) with:

~~~text
5. **CHANGELOG.md untouched:** only `/release` writes it, from commit bodies. Diff against the review's base, not `HEAD`, so a committed edit shows too: `git diff --name-only <base> -- CHANGELOG.md` (the base is `HEAD` for uncommitted work, else the start of the range) must print nothing. A hand edit is a defect; a `/release` run or an archive roll into `docs/changelog-archive/` is not.
~~~

**E14** `.claude/skills/deep-review/SKILL.md:244`: old `CHANGELOG: [updated / NOT UPDATED]`, new
`CHANGELOG.md: [untouched / HAND-EDITED]` (the two emoji before it stay).

**E15** `.claude/skills/deep-review/SKILL.md:556`: old `A CHANGELOG "Known limitation:" bullet`,
new ``A `Known limitation:` paragraph in the commit body (it becomes the release's CHANGELOG entry)``

**E16** `.claude/skills/improve/SKILL.md:36`: old ``recent `CHANGELOG.md` +``, new
`recent commit bodies +`

**E17** `.claude/skills/new-creature-mount/SKILL.md:82`: old `shape; CHANGELOG; GitHub issue;`,
new `shape; the commit body (the changelog entry); GitHub issue;`

**E18** `.claude/skills/ship/SKILL.md:3`: replace the whole line with
`description: Orchestrate the mandatory completion sequence before merging a C# feature (/verify, /deep-review, fix findings, /review-codex), then close the issue and update the docs.`

**E19** `.claude/skills/ship/SKILL.md:34`: old ``10. Update `CHANGELOG.md`.``, new
``10. Write the commit body as the changelog entry; `/release` generates `CHANGELOG.md` from it.``

**E20** `.claude/skills/verify-bindings/SKILL.md:74`: old
``Update `docs/migration/TRACKING.md` and `CHANGELOG.md` if bindings were fixed.``, new
``Update `docs/migration/TRACKING.md` if bindings were fixed, and say what changed in the commit body.``

**E21** `.claude/skills/verify/SKILL.md`: replace lines 66-75, from `## Step 5: CHANGELOG Check`
through `If C# or XML files changed but CHANGELOG not updated, flag it.`, with:

~~~text
## Step 5: CHANGELOG.md untouched

Only `/release` writes `CHANGELOG.md` (generated from commit bodies). Check that this work did
not edit it by hand:

```bash
git diff --name-only HEAD -- CHANGELOG.md
```

If it prints `CHANGELOG.md` outside a `/release` run, flag it: the entry belongs in the commit body.
~~~

**E22** `.claude/skills/verify/SKILL.md:89`: old `CHANGELOG:  [Updated/NOT UPDATED]`, new
`CHANGELOG:  [Untouched/HAND-EDITED]` (two spaces after the colon in both).

**E23 to E31** `.claude/skills/release/SKILL.md`, in this order:

- **E23** line 3: old `bump the version fields, write the release note,`, new
  `bump the version fields, generate the CHANGELOG section, write the release note,`
- **E24** line 4: old `argument-hint: [version, e.g. 2.0.19]`, new `argument-hint: [version, e.g. 2.4.0]`
- **E25** after line 30 (Phase 1 item 4, starts ``4. `git tag -l 'v*'` ``) insert:

~~~text
5. The target version follows this line's scheme: `v2.4.0`, then `v2.4.1` and so on. Never
   `v2.0.29` to `v2.0.36` (1.5.x release tags) or `v2.1.x` (tags on no branch); see
   `docs/reference/release-process.md`, "Version numbers on the 1.4.x line".
~~~

- **E26** replace line 63 (`## Phase 4 — Release note`) with:

~~~text
## Phase 4: generate the CHANGELOG section

`CHANGELOG.md` is written here and nowhere else; the commit body is the changelog entry (AGENTS.md
"Documentation duty"). With the version fields bumped but not yet committed, run:

```bash
python tools/changelog_from_commits.py --version vX.Y.Z --write
```

It reads every non-merge commit since the previous release tag
(`git describe --tags --abbrev=0 --match 'v[0-9]*'`), groups the labelled ones by type with subject
and body verbatim, lists the commits without the version label in a last group, and inserts
`## vX.Y.Z (<today>)` above the previous release's section. Read its stderr summary
(`N commits, L with the version label, U without; ending at <sha>`), note that SHA for Phases 6
and 7 (the release commit's parent must be that commit), and read the unlabelled group before
writing the release note. Exit 2 means it refused;
show the user the message, and never delete a hand-written heading without their OK.

## Phase 5: release note
~~~

- **E27** replace lines 70-75, from `Source the content from CHANGELOG entries since the previous tag:`
  through `Entry under today's date. Mandatory (CLAUDE.md Documentation Requirements).` (this
  removes the old Phase 5 heading), with:

~~~text
Source the content from the section Phase 4 just wrote into `CHANGELOG.md`. It runs to thousands
of lines, so list it first: `awk '/^## v/{n++} n==1 && /^###/' CHANGELOG.md` prints its group and
subject lines only. Open the bodies you need (`git show -s --format=%b <sha>`), and grep the
section for `MCM` before writing the persisted-settings line.
~~~

- **E28** replace lines 79-80, from `Stage **explicitly**` through the line `edits.`, with:

~~~text
First, `git rev-parse HEAD` must print the SHA Phase 4 ended at. If another commit landed since,
the section misses it: `git checkout CHANGELOG.md` and run Phase 4 again.

Stage **explicitly** with `git add <paths>`, never `-A`: the Phase 3 version files, `CHANGELOG.md`
(Phase 4) and the release note (Phase 5). A shared file routinely holds two sessions' edits.
~~~

- **E29** in Phase 7, immediately before the ```` ```bash ```` line that precedes
  `git tag -a vX.Y.Z -m "TAOM vX.Y.Z`, insert these lines and one empty line, and change that tag
  line to `git tag -a vX.Y.Z <release commit sha> -m "TAOM vX.Y.Z`:

~~~text
First, `git rev-parse <release commit sha>^` must print the SHA Phase 4 ended at. If it does not,
a commit landed between the Phase 6 check and the commit, and it is in no section: stop and ask
the user before tagging anything.
~~~

- **E30** immediately before the line starting ``Annotated (`-a`), never lightweight.`` insert:

~~~text
Tag the Phase 6 commit by its SHA, not `HEAD`: a commit another session lands after it belongs
to the next release's section, and tagging it here would drop it from both.
~~~

- **E31** old `git describe --tags --match 'v[0-9]*' HEAD         # expect vX.Y.Z`, new
  `git describe --tags --match 'v[0-9]*' <release commit sha>   # expect vX.Y.Z`

**E32** `docs/ai-includes/completion-workflow.md`: replace lines 93-94 (inside a fence; keep the
ten leading spaces), from `          b7e7188. The pre-commit hook only enforces CHANGELOG, not issue`
through `          creation — discipline is on the author. Pattern: open the issue`, with:

~~~text
          b7e7188. No hook enforces issue creation; discipline is on the
          author. Pattern: open the issue
~~~

**E33** `docs/ai-includes/completion-workflow.md:98`: old `  13. Update CHANGELOG.md`, new
`  13. Write the commit body as the changelog entry (/release generates CHANGELOG.md)`

**E34** `docs/ai-includes/external-repo-adoption.md:33`: replace the whole line (starts
`8. **Commit MINE only.**`) with:

~~~text
8. **Commit MINE only.** Stage only the files this work produced. Leave shared/community files (`CLAUDE.md`, `AGENTS.md`) for the user's batch unless told otherwise; they're pushed last. Respect concurrent writers: re-read a shared file (`docs/INDEX.md`, `docs/reference/feature-map.md`) immediately before editing it. No AI attribution in commit messages (CLAUDE.md convention).
~~~

**E35 to E38** `docs/ai-includes/lord-skills-authoring.md`:

- **E35** line 182: old ``+ `SubModule.xml` + `CHANGELOG.md` |``, new ``+ `SubModule.xml` |``
- **E36** line 484: old ``, `taom_lord_skill_sets.xml`, `CHANGELOG.md`, possibly``, new
  ``, `taom_lord_skill_sets.xml`, possibly``
- **E37** line 485: old `- [ ] CHANGELOG.md updated.`, new
  ``- [ ] The commit body describes the change (`/release` turns it into the CHANGELOG entry).``
- **E38** line 502: old `Flag this in PR descriptions and CHANGELOG |`, new
  `Flag this in PR descriptions and the commit body |`

**E39** `docs/ai-includes/new-culture-authoring.md`: delete line 424, the row
``| **CHANGELOG** | `CHANGELOG.md` | modified (per-iteration entries) |``.

**E40** `docs/features/multi-culture-armor-revamp.md:94`: old `8. Add to CHANGELOG, update this feature doc table.`,
new `8. Describe the culture in the commit body (the changelog entry), update this feature doc table.`

**E41** `docs/features/troop-progression.md:84`: old ``5. Update `CHANGELOG.md`.``, new
``5. Describe the change in the commit body (`/release` builds `CHANGELOG.md` from it).``

**E42** `docs/modding/module-taom.md:352`: replace the whole line (starts
``5. Commit as `chore(release): TAOM vX.Y.Z` ``) with:

~~~text
5. Commit as `chore(release): vX.Y.Z - TAOM vX.Y.Z`, staging the release paths explicitly; tag that commit by its SHA, not `HEAD`, with `git tag -a vX.Y.Z <release commit sha> -m "..."` (`docs/reference/release-process.md` steps 7 and 8); `git push origin <branch> vX.Y.Z`. A plain `git push` does not push the tag.
~~~

**E43 to E46** `docs/reference/release-process.md` (plan 020 end state, keeping
`bannerlord-1.4.5`). Lines 1 to 74 keep their numbers, so the `release-process.md:8-13`, `:22-41`,
`:47-51` and `:69` to `:73` citations in `module-taom.md` stay true. Leave line 18 for Step 5.

- **E43** replace lines 75-77, from the duplicate
  ``4. Write `docs/releases/vX.Y.Z-discord.md` `` line through the line starting
  ``6. Commit `chore(release): vX.Y.Z - TAOM vX.Y.Z` ``, with these three lines (lines 78-80, the
  continuation of old step 6, stay as they are):

~~~text
5. Generate the CHANGELOG section: `python tools/changelog_from_commits.py --version vX.Y.Z --write` (every non-merge commit since the previous tag, subject and body verbatim, grouped by type). Its summary names the commit the range ends at.
6. Write `docs/releases/vX.Y.Z-discord.md` from that section (shape: [`v2.0.15-discord.md`](../releases/v2.0.15-discord.md)).
7. Confirm `HEAD` is still the commit step 5 ended at (if not, restore `CHANGELOG.md` and repeat step 5), then commit `chore(release): vX.Y.Z - TAOM vX.Y.Z`, staging release paths explicitly. Every other
~~~

- **E44** replace line 81 (starts ``7. `git tag -a vX.Y.Z -m``) with (the `…` is U+2026, kept from
  the old line):

~~~text
8. Confirm `git rev-parse <release commit>^` prints the commit step 5 ended at (if not, stop and ask), then `git tag -a vX.Y.Z <release commit> -m "…"`, tagging the step 7 commit by SHA, then `git push origin bannerlord-1.4.5 vX.Y.Z`.
~~~

- **E45** old `**Step 7 is the one that gets skipped**`, new `**Step 8 is the one that gets skipped**`
- **E46** after line 88, the last line of "Cutting a release"
  (`` `git push` does not push tags. The tag needs its own refspec, or `--follow-tags`. ``),
  insert one empty line and:

~~~text
**Version numbers on the 1.4.x line.** The next release cut from `bannerlord-1.4.5` is `v2.4.0`,
then `v2.4.1` and so on (maintainer decision, 2026-11-04). `v2.0.29` to `v2.0.36` are release tags
of the 1.5.x line, and `v2.1.0` and `v2.1.1` are tags on no branch, so none of them may be reused
here. Three parts keep `tools/changelog_from_commits.py`, whose `--version` accepts only
`vX.Y.Z`, and the subject hook unchanged; `2.4` sorts above the public build's hand-labelled
`2.0.29.5` and reads as "the 1.4 line" in a crash bundle's `TaomVersion`. Until that release
commit, every subject on this line carries `v2.0.28`.
~~~

3. Checks before staging:
   - **OLDPHRASE** (below) prints exactly two lines: `.claude/rules/hook-authoring.md:59:...`
     (history, stays) and `docs/reference/release-process.md:18:...` (Step 5 fixes it).
   - **DASHSCAN** (below) prints `DASHES 0`.
   - `WT$ grep -c 'The commit body is the changelog entry' AGENTS.md` prints `1`;
     `WT$ grep -c -e 'changelog_from_commits.py --version vX.Y.Z --write' -e 'Version numbers on the 1.4.x line' .claude/skills/release/SKILL.md docs/reference/release-process.md`
     prints `2` for each file.
   - `WT$ wc -c < AGENTS.md` is below 8192; `WT$ python tools/reviewctl.py lint` prints
     `Shared contract OK`.
   - `WT$ python -c "print(len(open('CLAUDE.md','rb').read().replace(b'\r\n', b'\n')))"` prints a
     number no larger than `base-claude-size.txt` (the edits of Steps 3 and 4 remove one byte net:
     simulated 46,283 to 46,282 on `1b00d1acd`).

```bash
WT$ git grep -n -F -e 'check-changelog-changed' -e 'check-changelog-updated' -e 'CHANGELOG.md every session' -e 'update CHANGELOG.' -e 'issue + feature doc + CHANGELOG' -e 'a CHANGELOG entry in `HEAD`' -e '33 hook registrations' -e '29 scripts on disk' -e 'five python-only gates' -e 'Latest CHANGELOG' -e 'closeout (issue, feature doc, CHANGELOG)' -e 'docs and the changelog' -e 'test evidence and the changelog' -e '**Update CHANGELOG' -e 'index and changelog' -e 'only enforces CHANGELOG' -e '13. Update CHANGELOG.md' -e 'shared file (`CHANGELOG.md`)' -e '+ `CHANGELOG.md` |' -e '`CHANGELOG.md`, possibly' -e 'CHANGELOG.md updated.' -e 'PR descriptions and CHANGELOG' -e '| **CHANGELOG** |' -e '](../../CHANGELOG.md)' -e '5. CHANGELOG entry.' -e 'mirrors the entries about this feature' -e 'in the CHANGELOG `Not-tested:`' -e 'Add to CHANGELOG' -e '5. Update `CHANGELOG.md`.' -e 'Commit as `chore(release): TAOM vX.Y.Z`' -e 'counter math off by one. Now caught' -e 'PR / CHANGELOG' -e '| 7. CHANGELOG |' -e '(commit message, CHANGELOG, log)' -e '`/verify` + CHANGELOG + issue' -e 'update changelog for execution system' -e 'CHANGELOG Updated:' -e 'CHANGELOG: [updated / NOT UPDATED]' -e 'A CHANGELOG "Known limitation:"' -e 'backlinks, CHANGELOG, delete branch' -e '### 4. CHANGELOG' -e '**CHANGELOG hook:**' -e 'recent `CHANGELOG.md` +' -e 'update CHANGELOG mid-loop' -e 'git history and CHANGELOG' -e 'head -30 CHANGELOG.md' -e 'git log themes, CHANGELOG entry' -e 'shape; CHANGELOG; GitHub issue' -e 'Source the content from CHANGELOG entries' -e "Entry under today's date" -e 'Read recent CHANGELOG entries' -e 'update docs + CHANGELOG' -e '10. Update `CHANGELOG.md`.' -e 'and `CHANGELOG.md` if bindings' -e 'Step 5: CHANGELOG Check' -e 'but CHANGELOG not updated' -e 'CHANGELOG:  [Updated/NOT UPDATED]' -- . ':!CHANGELOG.md' ':!docs/changelog-archive' ':!docs/archive' ':!docs/reviews' ':!docs/audits' ':!plans'
```

(While planning this matched 75 lines on `origin/bannerlord-1.4.5`: every line this plan changes
or deletes, plus `hook-authoring.md:59`.)

```bash
WT$ python - "$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE)" <<'PY'
import sys
sys.path.insert(0, 'tools')
import lint_docs as L
found = []
for p, lines in sorted(L._changed_markdown_lines(sys.argv[1]).items(), key=lambda kv: str(kv[0])):
    if p.is_file() and 'changelog-archive' not in p.as_posix():
        found += L.scan_text_for_dashes(p, p.read_text(encoding='utf-8-sig'), lines)
for p, n, kind, text in found:
    print(L.rel(p), n, kind)
print('DASHES', len(found))
PY
```

4. Stage the 36 paths (expected output `36`):

```bash
WT$ git add AGENTS.md CLAUDE.md README.md .ai/roles/builder.md .agents/skills/taom-build/SKILL.md .serena/memories/task_completion_checklist.md .claude/rules/external-skill-ports.md .claude/rules/gui-ui.md .claude/rules/simplicity-criterion.md .claude/rules/troops.md .claude/rules/working-discipline.md .claude/skills/author-armor/SKILL.md .claude/skills/commit-split/SKILL.md .claude/skills/deep-review/SKILL.md .claude/skills/finish-branch/SKILL.md .claude/skills/improve/SKILL.md .claude/skills/lint-cleanup-loop/SKILL.md .claude/skills/new-adr/SKILL.md .claude/skills/new-creature-mount/SKILL.md .claude/skills/release/SKILL.md .claude/skills/scope-check/SKILL.md .claude/skills/ship/SKILL.md .claude/skills/verify/SKILL.md .claude/skills/verify-bindings/SKILL.md docs/ai-includes/codex-operating-guide.md docs/ai-includes/completion-workflow.md docs/ai-includes/external-repo-adoption.md docs/ai-includes/lord-skills-authoring.md docs/ai-includes/new-culture-authoring.md docs/features/TEMPLATE.md docs/features/gondor-ithilien-ranger.md docs/features/gui-sprite-system.md docs/features/multi-culture-armor-revamp.md docs/features/troop-progression.md docs/modding/module-taom.md docs/reference/release-process.md && git diff --cached --name-only | wc -l
```

5. Write `msg-3.txt`, MSGCHECK it, then commit in the next call:

~~~text
docs(workflow): v2.0.28 - the commit body is the changelog entry

The commit body is now the changelog entry: write it for a reader of
the release note. /release generates CHANGELOG.md from commit
subjects and bodies, and nobody edits that file by hand.

AGENTS.md gains a "Documentation duty" section that states the rule
for every AI client, and CLAUDE.md replaces "CHANGELOG.md every
session". /release Phase 4 now runs tools/changelog_from_commits.py
--write, Phase 5 writes the release note from that section, Phase 6
checks that HEAD is still the commit the generator ended at, and
Phase 7 checks that the release commit's parent is that commit and
tags the release commit by SHA. release-process.md follows the same
order and records the version scheme for this line: the next release
is v2.4.0, then v2.4.1, because v2.0.29 to v2.0.36 are 1.5.x release
tags.

/verify Step 5 and the /deep-review completeness agent used to demand
a CHANGELOG edit; they now flag one. Every other instruction that told
an agent to update CHANGELOG.md now points at the commit body: the
ship, finish-branch, author-armor, verify-bindings, lint-cleanup-loop,
scope-check, new-adr, improve, commit-split and new-creature-mount
skills, five rules, the builder role, the taom-build Codex skill, the
Codex guide, the Serena checklist, README.md, completion-workflow.md,
external-repo-adoption.md, the lord-skills and new-culture guides,
docs/modding/module-taom.md, the feature-doc template and four
feature docs. Lines that name CHANGELOG as a kind of prose or record
history are left alone, as are code comments that cite a past
CHANGELOG date.

Ported-from: 30b54df759fe63e19ca5902110dbf702665b434b
Ported-from: 6e4bfedb7769bdc1d45d3cf8f90f1564851b9015
Ported-from: 971d8e97581cf6a43d7f9671eb9b19b25cb07c76
Ported-from: 5b242cee660b1675f2c3f582f91130bd9a85cd35
~~~

**Verify**: `WT$ git show --stat --format=%s HEAD` shows the commit 3 subject and 36 paths;
STATUS prints nothing.

### Step 5: Archive the hand-written CHANGELOG (commit 4)

Method: re-cut, never a cherry-pick (`ef5b7ff4e` carries the 1.5.x CHANGELOG blob; its simulated
pick conflicts in `CHANGELOG.md`). The archive is this branch's base blob, unchanged; the new
header is the exact 1.5.x header blob, whose text holds for 1.4.x.

1. `WT$ git log --oneline "$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE)"..bannerlord-1.4.5 -- CHANGELOG.md`
   should print nothing. If it prints commits, carry on and list them in your report; the
   integrator re-cuts at merge (Maintenance notes). Never re-cut it yourself.
2. `WT$ git mv CHANGELOG.md docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`
3. `WT$ git show ef5b7ff4e:CHANGELOG.md > CHANGELOG.md && git add CHANGELOG.md && git hash-object CHANGELOG.md`
   prints `673908fd5af1328a58f5629b8a541bf2fb658eb9` (11 lines, 782 bytes, no BOM: the file is
   generated at each release, the commit body is the entry, and links to both archives).
4. **E47** `docs/reference/release-process.md:18`: old ``crash reports in [`CHANGELOG.md`](../../CHANGELOG.md)``,
   new ``crash reports in [`CHANGELOG-2026-H2-handwritten.md`](../changelog-archive/CHANGELOG-2026-H2-handwritten.md)``.
   Then `WT$ git add docs/reference/release-process.md`.
5. Checks:
   - `WT$ B=$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE); [ "$(git rev-parse :docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md)" = "$(git rev-parse "$B:CHANGELOG.md")" ] && echo archive-ok`
     prints `archive-ok`; `WT$ grep -c '^## ' CHANGELOG.md` prints `0`.
   - The first release inserts cleanly:
     `WT$ python -c "import sys; sys.path.insert(0,'tools'); import changelog_from_commits as c; t=open('CHANGELOG.md',encoding='utf-8').read(); s=c.render_section('v2.4.0','2026-11-05','v2.0.28',c.parse_log('a'*40+'\x1ffix(x): v2.0.28 - y\x1f\n\x1e\n')); print(c.insert_section(t,s,'v2.4.0').count('## v2.4.0'))"`
     prints `1`.
   - D8 range: `WT$ python tools/changelog_from_commits.py --version v2.4.0 > /dev/null; git rev-list --no-merges --count v2.0.28..HEAD`
     prints a stderr line starting `changelog_from_commits: v2.0.28..HEAD: N commits,` and ending
     with the HEAD SHA, then a number equal to N (at least 124: 121 after WP101, plus this plan's
     three commits so far, plus WP101's own commits).
   - OLDPHRASE now prints exactly one line, `.claude/rules/hook-authoring.md:59:...`.
   - `WT$ git diff --cached --name-status` prints `M CHANGELOG.md`,
     `A docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md` and
     `M docs/reference/release-process.md`, in any order.
6. Write `msg-4.txt`, MSGCHECK it, then commit in the next call:

~~~text
chore(changelog): v2.0.28 - archive the hand-written CHANGELOG

The hand-written CHANGELOG.md moves unchanged (the same blob, old
header included) to
docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md, next to the
2026-H1 archive. lint_docs.py already treats that folder as verbatim
history, so its old links and version numbers raise no findings.

The new CHANGELOG.md is only a header: it says the file is generated
at each release by tools/changelog_from_commits.py, that the commit
body is the changelog entry, and links both archives. It stays a
header until the v2.4.0 /release writes the first generated section.
release-process.md now points its v2.0.12 crash-report link at the
archive.

Ported-from: ef5b7ff4e4b25620617fc9453b5971642c88f485
~~~

**Verify**: `WT$ git log --oneline "$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE)"..HEAD`
prints the four commits of "Git workflow", newest first; STATUS prints nothing. This is the last
commit.

### Step 6: Final regression and report

```bash
WT$ python tools/tests/test_changelog_from_commits.py 2>&1 | tail -3
WT$ S=C:/Users/mikew/source/repos/taom-port/scratch/102; python -m unittest discover -s tools/tests -t . > $S/end-tools.txt 2>&1; echo "exit $?"; tail -4 $S/end-tools.txt
WT$ S=C:/Users/mikew/source/repos/taom-port/scratch/102; bash tools/test_hooks.sh > $S/end-hooks.txt 2>&1; echo "exit $?"; grep 'FAIL' $S/end-hooks.txt | sort | comm -13 $S/base-hooks-fail.txt -; tail -3 $S/end-hooks.txt
WT$ python tools/lint_docs.py --summary; python tools/lint_docs.py --fail-on-drift > /dev/null 2>&1; echo "drift exit $?"
WT$ python tools/reviewctl.py lint; python tools/audit_claude_config.py --min HIGH
WT$ B=$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE); git log --format=%B $B..HEAD | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(sum(len(l) > 72 for l in t.splitlines()), t.count('\u2014') + t.count('\u2013'))"; git log --format=%B $B..HEAD | grep -ci 'co-authored-by\|generated with'; git log --format='%(trailers:key=Ported-from,valueonly)' $B..HEAD | grep . | sort -u | wc -l
WT$ B=$(cat C:/Users/mikew/source/repos/taom-port/scratch/102/BASE); git diff --name-only --no-renames $B..HEAD | wc -l; git diff --name-only $B..HEAD -- '*.cs' '*.csproj' '*.props' '*.xslt' Main TAOM.Tests Dependencies
WT$ dotnet restore TAOM.sln && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3
WT$ dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -5
```

Re-run DASHSCAN and OLDPHRASE too. Report (your hand-off; the integrator records it in
`plans/README.md`): status (DONE, or BLOCKED with the reason); the four commit SHAs; tools-suite
counts before and after; hook-suite summary lines before and after; lint summaries and drift exits
before and after; the dotnet totals; the Step 5 range line; and whether Step 5 item 1 printed any
commit. No old-plan row needs updating: plan 020 has no row in this line's `plans/README.md`.

## Disposition

| 1.5.x item | Subject | Disposition |
|---|---|---|
| `ae85381dc` | feat(tools): CHANGELOG release section from the commit log | Transplanted (generator and tests at the `9bfd0f411` end state, blob-identical); README row hand-inserted. Commit 1 |
| `c91bb95f8` | chore(hooks): retire the two CHANGELOG hooks and their rules | Adapted: scripts deleted, `settings.json` by the maintainer, comments, session-start, harness-facts, hook-authoring and hooks-catalog redone on 1.4.x text and recounted for the 1.4.x hook set; `PF_NARROWED_LIST` and prefilter-count hunks skipped (no plan 013 on 1.4.x). Commit 2 |
| `30b54df75` | docs(workflow): the commit body is the changelog entry | Adapted: 10 files by end-state copy, the rest hand-edited; AGENTS.md gains "Documentation duty" (1.4.x had none). Skipped: `armory-audit/SKILL.md`, `deep-review/lenses/4-completeness.md` (absent; adapted in `deep-review/SKILL.md`). Commit 3 |
| `ef5b7ff4e` | chore(changelog): archive the hand-written CHANGELOG | Re-cut: `git mv` of the 1.4.x file plus the 1.5.x header blob `673908fd`. Commit 4 |
| `6e4bfedb7` | fix(changelog): review follow-ups for plan 020 | Adapted: generator fixes and 11 tests (commit 1); banner and 5b2 (commit 2); release flow, release-process, commit-split, new-creature-mount, TEMPLATE, four feature docs, external-skill-ports, gui-ui, external-repo-adoption, hooks-catalog dash, completeness text, CLAUDE.md:340 for the absent `git-and-commits.md` (commits 2 and 3). Skipped: lenses 4/6/7, `git-and-commits.md`, `v1.5.2-impact.md` (absent or 1.5.x-only); review report, Codex prompt, RCA, REVIEW-LOG rows and lessons (bookkeeping; lessons are WP142's) |
| `971d8e975` | fix(changelog): convergence fixes for plan 020 | Adapted: docstring and README row (commit 1); banner file names and 5b2 (commit 2); release Phase 4/7 parent check and release-process step 8 (commit 3). Skipped: report, RCA, `lessons/misc.md` |
| `5b242cee6` | fix(changelog): final convergence fixes for plan 020 | Adapted: docstring (commit 1); 5b2 robustness, one comment line made true for 1.4.x (commit 2); `module-taom.md` release step (commit 3). Skipped: report, RCA, `lessons/build-tooling-workflow.md` |
| `9bfd0f411` | fix(tools): changelog lists a commit that landed twice once | Transplanted with the generator (K2 finding F4; its body cites #1009). Commit 1 |
| `96afb6fb4` | docs(plans): plan 020 merged, the sprint's last branch | Skipped: SKIP-BOOKKEEPING (1.5.x `plans/README.md`) |
| `55a729152` | merge(improve): plan 020 CHANGELOG generated at release | Not replayed (a merge). Its re-cut-at-merge recipe is the integrator note below; its plan 011/013/017 resolutions do not apply on 1.4.x |
| RELEASE-INTEGRITY-09 | Port the release process before the port wave | Addressed by this plan. D2 refuted two of its claims (every cherry-pick needing a CHANGELOG hand edit; all pre-2026-09-25 1.5.x commits touching it); adopted anyway under D1 |

## Test plan

- `tools/tests/test_changelog_from_commits.py`, 35 tests unchanged from 1.5.x: parsing (5),
  rendering incl. heading escape and duplicate collapse (11), label parity with the 1.4.x subject
  gate (1), insertion and refusal rules incl. CRLF and longer versions (8), end to end in a temp
  repo incl. BOM, non-ASCII bodies, empty range and missing tag (10). Model:
  `tools/tests/test_package_release.py`.
- `tools/test_hooks.sh` 5b2: RED with 6 failures before Step 3's edits, GREEN with 5 named gates.
- Regression: tools suite, hook suite (3b parses the four edited skill descriptions), `lint_docs`,
  `reviewctl lint`, `audit_claude_config`, one dotnet build and test.
- Plan gates: OLDPHRASE (no instruction to hand-edit CHANGELOG survives) and DASHSCAN (no dash on a
  changed markdown line outside the archive).
- Not testable here (the `Not-tested:` trailers): a live `/release` run, and a Claude Code session
  loading the new `settings.json` (only a restart after the merge shows it).

## Done criteria

ALL must hold, run from the worktree:

- [ ] `git log --oneline BASE..HEAD` shows exactly the four commits of "Git workflow", in order.
- [ ] `HEAD:tools/changelog_from_commits.py` is `01614b4e485179544a441e45d9a3c19003e514ed` and
      `HEAD:tools/tests/test_changelog_from_commits.py` is `767f5b3db4858068238e69c7465408674efdac8d`.
- [ ] `python tools/tests/test_changelog_from_commits.py` prints `Ran 35 tests` and `OK`; the tools
      suite ran the baseline count plus 35 with no new failure or error.
- [ ] `git ls-files .claude/hooks | grep -c '\.sh$'` prints `27`; `grep -c check-changelog .claude/settings.json`
      prints `0`; the registration count prints `26 9`.
- [ ] `bash tools/test_hooks.sh` has no FAIL line the baseline lacks, and 5b2 names 5 gates.
- [ ] OLDPHRASE prints exactly `.claude/rules/hook-authoring.md:59:...`; DASHSCAN prints `DASHES 0`.
- [ ] `HEAD:docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md` equals `BASE:CHANGELOG.md`
      (blob ids); `HEAD:CHANGELOG.md` is `673908fd5af1328a58f5629b8a541bf2fb658eb9`;
      `grep -c '^## ' CHANGELOG.md` prints `0`.
- [ ] The generator reports `v2.0.28..HEAD` with a count equal to
      `git rev-list --no-merges --count v2.0.28..HEAD`.
- [ ] `git cat-file -s HEAD:CLAUDE.md` is no larger than `git cat-file -s BASE:CLAUDE.md`;
      `git cat-file -s HEAD:AGENTS.md` is below 8192; `python tools/reviewctl.py lint` exits 0.
- [ ] `lint_docs.py --fail-on-drift` exits as in the baseline; each `--summary` count except
      `ai_dashes` is no higher than the baseline; `audit_claude_config.py --min HIGH` shows nothing new.
- [ ] Message checks over `BASE..HEAD`: length and dash check `0 0`; AI-trailer grep `0`;
      `Ported-from` count `8`.
- [ ] `git diff --name-only --no-renames BASE..HEAD | wc -l` prints `52`, all in-scope; the
      C#/project/XSLT path check prints nothing.
- [ ] dotnet build exits 0 with 0 errors; dotnet test shows 0 failed and the previous recorded total.
- [ ] STATUS prints nothing (or only ` M .serena/project.yml`); nothing was written in the main
      checkout; `plans/README.md` is untouched.

## STOP conditions

Stop and report (do not improvise) if:

- Step 1 item 2 does not print `WP101-OK`, or item 4 shows a version other than `v2.0.28` or a
  reachable `v*` tag newer than `v2.0.28`.
- Step 1 item 6 prints a `DIFF` line or `generator PRESENT`: an earlier work package changed a file
  this plan copies whole, and the copy would drop that change.
- Step 1 item 7 fails (Step 0 missing or different): finish Step 2 if you can, then stop. Never edit
  `.claude/settings.json` yourself, create a config-protection override, or rewrite it from a shell.
- `.claude/settings.json` registrations already start with `"$CLAUDE_PROJECT_DIR"/` or a matcher
  reads `Bash|PowerShell`: WP103 ran first, breaking the order D1 requires.
- Any old text in Steps 2 to 5 is not found exactly once.
- A commit is denied or blocked by any hook (`[check-changelog-changed]`,
  `[check-commit-subject-version]`, `[check-claude-files-tracked]` or another). Report the exact
  message; never `--no-verify`, never stage `CHANGELOG.md` to satisfy a gate, never edit a hook in
  the main checkout.
- A RED run does not fail as stated (Step 2 item 1, Step 3 item 1), or a GREEN run does not pass,
  twice after a fix inside scope.
- The hook suite, the tools suite or `lint_docs` shows a failure the baseline lacks after one
  re-run (only a timing line may be re-run).
- A test or tool outside this plan fails because it reads `CHANGELOG.md` (the assumption that no
  program parses the hand-written file is false).
- `CLAUDE.md` grows, or a step seems to need `Main/**`, `TAOM.Tests/**`, `Main/IoC.cs`,
  `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`, `docs/adrs/*`,
  `.claude/settings.local.json`, `docs/reviews/**`, `docs/INDEX.md` or `plans/**`.
- The Step 5 range check reports a `since` other than `v2.0.28` or a count that differs from
  `git rev-list --no-merges --count v2.0.28..HEAD`, or the archive check does not print `archive-ok`.

## In-game checks owed

None: no game file, C#, ModuleData or asset changes. Live harness checks owed to the maintainer
after the merge:

1. Restart every running Claude Code session on this repo; sessions started earlier keep calling
   the two deleted scripts (a missing script exits 127, non-blocking noise).
2. In a fresh session on the merged `bannerlord-1.4.5`, SessionStart shows "Recent commits:" and no
   "Latest CHANGELOG:" block.
3. Run `/security-scan` (CLAUDE.md routes hook and settings changes there; the executor ran its
   tool but cannot invoke the skill).
4. At the `v2.4.0` release, `/release` Phase 4 must report `v2.0.28..HEAD: N commits` with N equal
   to `git rev-list --no-merges --count v2.0.28..HEAD`. Expect a large "Commits without the version
   label" group: 105 of the 121 commits since `v2.0.28` on the post-WP101 trunk carry no label (70
   are dated before 2026-09-13, when the subject rule began, and 35 on or after it), so the
   release note needs a hand summary of that group.

## Maintenance notes

- **Integrator merge recipe.** Merge WP102 right after WP101, before WP103. Normally nothing extra
  is needed, because no other work package edits `CHANGELOG.md` (D1). If
  `git log --oneline <WP102 base>..bannerlord-1.4.5 -- CHANGELOG.md` prints anything, re-cut the
  archive from the trunk's file in Git Bash, byte-faithfully (never via PowerShell redirection):
  `git merge --no-ff --no-commit port148/102-changelog-at-release`;
  `git show HEAD:CHANGELOG.md > docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`;
  `git show MERGE_HEAD:CHANGELOG.md > CHANGELOG.md`;
  `git add CHANGELOG.md docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`; confirm
  `test "$(git hash-object docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md)" = "$(git rev-parse HEAD:CHANGELOG.md)" && echo archive-ok`;
  then commit. Precondition: `git status --porcelain -- CHANGELOG.md` prints nothing in the main
  checkout. Never `merge=union` on `CHANGELOG.md` (D18).
- **From here on** no plan or executor stages `CHANGELOG.md`, and commit bodies are the release
  notes: write them for 1.4.x readers, never paste a 1.5.x body. Pack notes written before D1 (in
  WP115, WP116, WP138 and WP143, saying "keep the 1.4.x CHANGELOG gates" or "hand-edited CHANGELOG
  entry") are superseded by D1.
- **WP103 builds on this**: `settings.json` has 26 registrations; the degraded banner names the five
  python-only gates by file name, and 5b2 fails when a new python-only gate is missing from it.
  WP116's end-state transplant must not bring back either CHANGELOG hook (the 1.5.x tip has none).
- **WP115** extends `/release` and `release-process.md`; lines 1 to 74 of the latter kept their
  numbers, so `module-taom.md`'s line citations still hold.
- **WP142**: the plan 020 lessons (`lessons/build-tooling-workflow.md`, `lessons/misc.md`) and
  review records were not ported; if WP142 syncs those lessons, reword or drop their links into
  `rca-changelog-at-release-2026-09-24.md`, which 1.4.x lacks. `module-taom.md:234` still calls the
  `v2.1.x` tags "a question for the maintainer" and can now point at the D8 paragraph.
- **What a reviewer should probe**: each replaced line still reads well in its paragraph; the four
  edited skill descriptions in the live listing; the release flow's two SHA checks; the 5b2 regex
  against 1.4.x's call sites; the archive blob equality; `CLAUDE.md` did not grow.
- **Between releases** "what changed" is `git log <last tag>..HEAD`, not a file. The generated file
  grows by one section per release; roll it into `docs/changelog-archive/` when it is large.
