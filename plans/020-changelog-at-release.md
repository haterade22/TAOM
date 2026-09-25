# Plan 020: Generate CHANGELOG release sections at /release from commit bodies instead of hand-editing a shared file

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> keeps that index and updates the 020 row from your report.
>
> **Where you work**: the worktree `E:/repos/taom-improve/wt-020` on branch
> `improve/020-changelog-at-release`, created from `a39a9c86`. Every Read, Edit and Write path in
> this plan is relative to that worktree (for example `E:/repos/taom-improve/wt-020/AGENTS.md`).
> Your default cwd may be the main tree `E:\repos\TAOM`, where the live hooks run and other
> sessions have uncommitted edits (`CHANGELOG.md`, `tools/README.md` among them): never edit,
> write, stage or commit anything under `E:/repos/TAOM`. Run every command with the **Bash tool**
> (the commands use `grep`, `wc` and pipes), prefixed with `cd E:/repos/taom-improve/wt-020 && `
> unless it uses `git -C`, and pass `timeout: 600000` for `bash tools/test_hooks.sh`, the tools
> test suite and every `dotnet` command. Scratch files (commit messages, the phrase list) go in
> `E:/repos/taom-improve/scratch/`, never in the repo and never under `C:` or `/tmp`.
>
> **Where this plan lives**: it is untracked and absent at `a39a9c86`. Read it from the main tree
> (`E:/repos/TAOM/plans/020-changelog-at-release.md`, read-only) or from the copy the orchestrator
> may place at `E:/repos/taom-improve/wt-020/plans/020-changelog-at-release.md`. That copy shows in
> `git status --porcelain` as `?? plans/020-changelog-at-release.md`; it is expected, and every
> status check in this plan filters it out (the STATUS command under "Git workflow"). Never stage,
> commit, edit or delete it.
>
> **No CHANGELOG entry, no fifth commit.** Whatever a dispatch or follow-up prompt says (the
> standard executor wrapper `plans/_audit/2026-09-23-opus/machinery/x-exec.js:41` asks for a
> hand-written `## 2026-09-24` entry in `CHANGELOG.md` and a closing commit once done criteria
> hold; `x-followup.js:30` asks for "a line to the branch's CHANGELOG entry"), this plan's four
> commits are its whole output. Do not add a CHANGELOG.md entry and do not commit anything after
> Step 10; say in your report that you skipped the wrapper's entry because of this line. A
> hand-written `## ` section above the releases is exactly what the generator this plan
> ships refuses (exit 2) at the first `/release`, and a fifth commit breaks the done criteria. The
> commit bodies are this branch's changelog entries.
>
> **Drift check (run first, from the main tree `E:\repos\TAOM`)**:
> `git diff --stat a39a9c86..HEAD -- tools/README.md tools/test_hooks.sh .claude/settings.json .claude/hooks/check-changelog-changed.sh .claude/hooks/check-changelog-updated.sh .claude/hooks/session-start.sh .claude/hooks/block-broad-git-add.sh .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-version-tagged.sh .claude/hooks/check-commit-subject-version.sh docs/reference/hooks-catalog.md docs/reference/release-process.md .claude/rules/harness-facts.md .claude/rules/hook-authoring.md .claude/rules/external-skill-ports.md .claude/rules/troops.md .claude/rules/gui-ui.md .claude/rules/simplicity-criterion.md .claude/rules/working-discipline.md AGENTS.md CLAUDE.md README.md .ai/roles/builder.md .agents/skills/taom-build/SKILL.md .serena/memories/task_completion_checklist.md docs/ai-includes/codex-operating-guide.md docs/ai-includes/completion-workflow.md docs/ai-includes/external-repo-adoption.md docs/ai-includes/lord-skills-authoring.md docs/ai-includes/new-culture-authoring.md .claude/skills/release/SKILL.md .claude/skills/ship/SKILL.md .claude/skills/verify/SKILL.md .claude/skills/deep-review/SKILL.md .claude/skills/deep-review/lenses/4-completeness.md .claude/skills/finish-branch/SKILL.md .claude/skills/author-armor/SKILL.md .claude/skills/verify-bindings/SKILL.md .claude/skills/lint-cleanup-loop/SKILL.md .claude/skills/scope-check/SKILL.md .claude/skills/new-adr/SKILL.md .claude/skills/improve/SKILL.md .claude/skills/armory-audit/SKILL.md docs/changelog-archive`
> Expected: no output (verified empty while planning, HEAD was `a39a9c86`). Your worktree is
> pinned to `a39a9c86`, so drift on the trunk does not change what you edit; it changes what the
> merge must resolve. If the command prints anything, list the paths in your report and carry
> on (the orchestrator re-cuts at merge; see "Maintenance notes"). `CHANGELOG.md` is left out on
> purpose: nearly every commit touches it, and this plan replaces it.

## Status

- **Priority**: P2
- **Effort**: M (one stdlib script with 22 tests, two hook deletions, about 40 one-line rule edits, one archive move)
- **Risk**: MED (it changes a rule every session and every AI client follows, and the archive move must be re-cut at merge time)
- **Depends on**: none. Merge it AFTER the open `improve/*` branches, which each add a CHANGELOG entry (see "Maintenance notes", merge recipe).
- **Category**: dx
- **Planned at**: commit `a39a9c86`, 2026-09-24
- **Issue**: create before implementation lands (orchestrator)
- **Needs before dispatch** (orchestrator, then Mike):
  1. Orchestrator: `git -C E:/repos/TAOM worktree add -b improve/020-changelog-at-release E:/repos/taom-improve/wt-020 a39a9c86`
  2. Mike, in an editor: **Step 0** below, inside that worktree. The executor never edits
     `.claude/settings.json`.
  3. Orchestrator: always pass this `it.note` to `x-exec.js` (the main tree's working copy of the
     wrapper supports `it.note`; the `a39a9c86` copy does not): "Plan 020 replaces the hand-written
     CHANGELOG: do NOT add a CHANGELOG.md entry and do NOT commit anything after Step 10; the plan's
     four commits are the whole output."
  4. Orchestrator: the wrapper's contract path `${it.wt}\plans\020-changelog-at-release.md` does not
     exist at `a39a9c86`. Either copy this file there (untracked; the plan filters the resulting
     `??` line from every status check) or add to the note "read the plan from
     `E:/repos/TAOM/plans/020-changelog-at-release.md`".

## Step 0: before dispatch (Mike, in an editor, inside `E:/repos/taom-improve/wt-020`)

`.claude/hooks/config-protection.sh` blocks the Edit and Write tools on any file named
`settings.json` (it matches the basename against `"Directory.Build.props" "settings.json"
"settings.local.json"` and exits 2 unless a user-approved override file exists; on 2026-09-24 the
harness refused the orchestrator's attempt to create that override). So an executor can never
make this edit, not through a tool and not through a shell command. Mike removes the two
registrations of the hooks this plan retires, in `E:/repos/taom-improve/wt-020/.claude/settings.json`
(its content is identical to `a39a9c86:.claude/settings.json`; the worktree checks it out with
CRLF line endings under the system `core.autocrlf=true`, so the bytes differ but `git diff` shows
nothing before this edit):

1. Delete these five lines (lines 47 to 51, inside `"PreToolUse"`, `"matcher": "Bash"`):

   ```json
             {
               "type": "command",
               "command": ".claude/hooks/check-changelog-changed.sh",
               "timeout": 5
             },
   ```

2. Delete these five lines (lines 208 to 212, the first entry of `"Stop"`):

   ```json
             {
               "type": "command",
               "command": ".claude/hooks/check-changelog-updated.sh",
               "timeout": 10
             },
   ```

Nothing else in the file changes. Leave the edit uncommitted: the executor verifies it in Step 1
and commits it in Step 6. Check (Mike or orchestrator):
`git -C E:/repos/taom-improve/wt-020 diff --numstat -- .claude/settings.json` prints `0	10	.claude/settings.json`.

## Why this matters

`CHANGELOG.md` is the most contended file in the repo: 547 of the 814 commits since 2026-07-01
touch it (`git rev-list --count --since=2026-07-01 a39a9c86 -- CHANGELOG.md`), it is 1,762,388
bytes at `a39a9c86`, and 7 of the 27 merges reachable from `a39a9c86` carry a resolved conflict
hunk in it. It has lost work more than once (`dc2d13a5` restored entries another session's stale
rewrite dropped; `a035a2d3` repaired a CRLF rewrite; `41d4afbe` recorded entries swept into an
unrelated commit), and three git-safety rules in `docs/ai-includes/git-and-commits.md:56,60,61`
exist because of it. Meanwhile the file mostly repeats the commit log: every subject already
carries the gated label `<type>[(scope)]: vX.Y.Z - <description>`, and the bodies already carry the
detail. Mike decided (decision 18, 2026-09-24) that `/release` generates the CHANGELOG from commit
subjects and bodies, the "every session" duty goes, the two CHANGELOG hooks go, and today's file is
archived. After this plan, no ordinary commit touches `CHANGELOG.md`; the one writer is
`tools/changelog_from_commits.py`, run once per release.

## Current state

All excerpts were read at `a39a9c86` with `git show a39a9c86:<path>`. In the main tree
`E:\repos\TAOM`, `CHANGELOG.md` (staged and unstaged) and `tools/README.md` carry other sessions'
uncommitted edits; your worktree does not see them.

### What decision 18 settled (do not reopen)

1. `/release` compiles the release's section from `git log <previous tag>..HEAD`, keyed on the
   subject label that `.claude/hooks/check-commit-subject-version.sh` enforces.
2. AGENTS.md's "CHANGELOG.md is updated every session" changes: the commit body is the entry.
3. `.claude/hooks/check-changelog-changed.sh` is retired or repurposed (this plan retires it; see
   "Rejected alternative" below).
4. The current `CHANGELOG.md` is archived under `docs/changelog-archive/`.

NOT decided, so NOT in this plan: rewriting past sections; a player-facing prose layer; shrinking
the incident rules in `docs/ai-includes/git-and-commits.md`.

### The version label (the generator's key)

`.claude/hooks/check-commit-subject-version.sh:292`:

```python
pat = re.compile(r"^[a-z][a-z0-9]*(?:\([^)]+\))?!?: (v\d+\.\d+\.\d+(?:\.\d+)?) - \S")
```

The label is the version in `Main/_Module/SubModule.xml` at commit time (`<Version value="v2.0.30" />`
at line 6 at `a39a9c86`). The version moves only in a release commit, so the commits between two
tags carry the OLD version's label and the release commit carries the new one:

- `git log v2.0.29..v2.0.30 --no-merges`: 57 commits; 53 match the pattern (fix 20, feat 14, docs 11,
  balance 5, chore 2, test 1; labels `v2.0.29` x52 and `v2.0.30` x1, the release commit
  `96f17fec chore(release): v2.0.30 - TAOM v2.0.30`); 4 do not, for example
  `ef6afeb6 Refactor validation and auditing tools; enhance troop cost definitions` and
  `35007dbb fix(field-commission): expand allowed race names for promotions` (a type, no label).
- `git describe --tags --abbrev=0 --match 'v[0-9]*' a39a9c86` prints `v2.0.30`. Tags `v2.0.29` and
  `v2.0.30` are annotated (`git for-each-ref refs/tags/v2.0.30` shows `tag`). No merge commits lie in
  `v2.0.29..v2.0.30`.
- Types used since 2026-07-01: fix 255, feat 176, docs 169, chore 60, refactor 34, balance 21,
  test 11, data 3, ci 3, perf 3, diag 2, localization 1, build 1, content 1.

So the generator takes every non-merge commit in `<previous tag>..HEAD`, groups the labelled ones by
type, and lists the rest in their own group. It does not filter by the label's version value.

### The CHANGELOG today

`CHANGELOG.md` at `a39a9c86`: 22,944 lines, starts with a UTF-8 BOM, stored with LF line endings
(`git ls-files --eol CHANGELOG.md` shows `i/lf`; a fresh worktree shows `w/crlf`, because the
system Git config sets `core.autocrlf=true`, and so do `AGENTS.md`, `.claude/settings.json` and
`docs/reference/hooks-catalog.md`), dated sections `## 2026-09-23` down to `## 2026-07-01`.
Lines 1 to 5:

```markdown
# CHANGELOG — TAOM (Tales From the Age of Men)

> **Archive:** entries before 2026-07-01 live in [`docs/changelog-archive/CHANGELOG-2026-H1.md`](docs/changelog-archive/CHANGELOG-2026-H1.md) (rolled 2026-07-12; cadence: each Jan 1 / Jul 1 — keep the current half-year here, roll the rest).

## 2026-09-23
```

`docs/changelog-archive/` holds one file, `CHANGELOG-2026-H1.md` (rolled by `b53b2d80`,
2026-07-12). `tools/lint_docs.py:76-78` names that directory `CHANGELOG_ARCHIVE_DIR` ("Rolled-out
CHANGELOG halves: verbatim historical text ... never lint them as living docs") and puts it in
both `STALE_VERSION_EXEMPT_PREFIXES` (`:118-128`) and `DEAD_LINK_EXEMPT_PREFIXES` (`:129-134`);
`tools/build_backlinks.py` skips dead-link-exempt files. So a file moved there is not linted.
ADR-010 (`docs/adrs/010-knowledge-base-architecture.md:36`: "It stays as a chronological release
log") and ADR-011 (`:72`, CHANGELOG in the Archive tier) are both satisfied by a generated
per-release log; no ADR changes.

### What `/release` does today

`.claude/skills/release/SKILL.md:66-78`:

```markdown
## Phase 4 — Release note

`docs/releases/vX.Y.Z-discord.md`, following `docs/releases/v2.0.15-discord.md`: emoji section
headers, player-facing framing (what changed for them, not which class was refactored), and an
explicit ⚠️ line whenever MCM-persisted settings mean **existing players keep old values** and must
reset them by hand.

Source the content from CHANGELOG entries since the previous tag:
`git log <previous-tag>..HEAD --format='%s'`.

## Phase 5 — CHANGELOG

Entry under today's date. Mandatory (AGENTS.md "Documentation duty").
```

Phase 3 bumps `Main/_Module/SubModule.xml`; Phase 6 commits `chore(release): vX.Y.Z - TAOM vX.Y.Z`
staging paths explicitly; Phase 7 tags. `docs/reference/release-process.md:78` is the matching
contract step: `5. CHANGELOG entry.` No other file refers to the release skill's phase numbers
(`git grep -n "Phase [0-9]"` finds them only in the skill itself).

### The two hooks this plan retires

- `.claude/hooks/check-changelog-changed.sh` (125 lines, PreToolUse on Bash, registered at
  `.claude/settings.json:47-51`): denies a `git commit` whose staged set (plus amend and pathspec
  forms) touches `.claude/`, `CLAUDE.md` or `AGENTS.md` without `CHANGELOG.md`. Its deny reason,
  line 124: `"[check-changelog-changed] This commit touches .claude/, CLAUDE.md, or AGENTS.md but does NOT include a CHANGELOG.md update. Per AGENTS.md 'Documentation duty', every session updates CHANGELOG.md. ..."`.
  With the duty gone, the gate enforces nothing that still exists.
- `.claude/hooks/check-changelog-updated.sh` (44 lines, Stop, registered at
  `.claude/settings.json:208-212`): when a `.cs/.xml/.xslt/.json` file is dirty and CHANGELOG.md is
  not, line 35 prints `REMINDER: CHANGELOG.md has not been updated this session. ...` to stderr and
  exits 0 (so Claude never sees it; plan 011 was going to convert it). It demands the duty this plan
  removes.

`tools/test_hooks.sh` (825 lines) names neither script: its per-hook sections loop over
`.claude/hooks/*.sh` and over the registrations in `settings.json`, so deleting both files plus
their registrations removes their rows and nothing else. `docs/reference/hooks-catalog.md:3` says
"29 scripts on disk, 28 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter
registrations ... for 33 total" (both counts measured at `a39a9c86`: `git ls-tree --name-only
a39a9c86 .claude/hooks/ | grep -c '\.sh$'` gives 29; the registrations sum to 28 across 9 events).
After this plan: 27, 26, 9 events, 31 total.

### Rejected alternative: repurpose `check-changelog-changed.sh`

Keeping the registration and changing the check (for example "deny a commit that stages
`CHANGELOG.md` unless `Main/_Module/SubModule.xml` is staged too, i.e. a release commit") would
avoid the `settings.json` edit for that hook, but: (a) the Stop hook's registration must go anyway,
so Step 0 is needed regardless; (b) a hard deny would fail the commits of every in-flight session
that still carries a hand-written entry right after the cut-over (at planning time the main tree
shows `MM CHANGELOG.md`, and several in-flight features hold uncommitted entries); (c) the
same protection costs nothing as a check inside the generator (it refuses a hand-written `## `
section above the releases) plus one line in `/verify` and the completeness lens. Under
`.claude/rules/simplicity-criterion.md`, a rewritten 125-line gate for a benefit the generator
already gives is "tiny win plus added complexity": **Reject**. Record this as a `Rejected:` trailer
in the Step 6 commit.

### Other consumers of the file

- `.claude/hooks/session-start.sh:198-205` prints the newest dated section's `### ` titles at every
  session start (`awk '/^## [0-9]/{...}'`). Release sections start `## v`, so after the cut-over it
  would print nothing; lines 193-196 already print `git log --oneline -5`. The block is deleted.
- `.claude/hooks/session-start.sh:33-35,41-42` name `check-changelog-changed` among "Five gates"
  that fail open without python; the name goes and "Five"/"five" become "Four"/"four".
- Comments naming a retired hook: `.claude/hooks/block-broad-git-add.sh:111`,
  `.claude/hooks/check-moduledata-validation.sh:35`, `.claude/hooks/check-version-tagged.sh:5`.
- Nothing in `.github/`, `build.ps1`, any `*.ps1`, `*.props` or `*.csproj` reads `CHANGELOG.md`
  (`git grep -n -i changelog a39a9c86 -- .github build.ps1 '*.ps1' '*.props' '*.csproj'` hits only
  a comment in `tools/sweep_module_backups.ps1:9`). No C# test reads it
  (`git grep -n -E '"[^"]*\.md"' a39a9c86 -- TAOM.Tests` names only two feature docs and `CLAUDE.md`
  as a root marker). No script parses it either:
  `git grep -n -i changelog a39a9c86 -- tools '*.py' '*.js' '*.sh'` finds, besides the hooks above,
  only comments, issue-draft text and the archive-directory exemption (`tools/lint_docs.py`,
  `tools/generate_culture_issue_drafts.py`,
  `tools/test_hooks.sh:170,628`, `tools/tests/test_lint_docs.py:800`), a path list in
  `plans/_audit/2026-09-23-opus/machinery/make_codex_prompt.py:45`, and the executor-wrapper
  instructions `x-exec.js:41` and `x-followup.js:30` that the Executor instructions above override.

### Every instruction that tells an agent to edit CHANGELOG.md

Found with `git grep -n -i changelog a39a9c86 -- AGENTS.md CLAUDE.md README.md .ai .agents .claude .serena docs/ai-includes docs/reference`,
each line read. The list in Step 7 names every line this plan changes, with exact old and new
text. Lines that only name CHANGELOG as a kind of prose, cite a past entry, or record history are
left alone on purpose (list under "Scope", out of scope).

### Conventions that bind this change

- **TDD** (AGENTS.md "Always"): RED, GREEN, REFACTOR. The generator's test file is written and run
  red before the script exists.
- **Tools tests are stdlib `unittest`**, no pytest imports; each module inserts `tools/` on
  `sys.path` (model: `tools/tests/test_package_release.py:33`,
  `sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))`) and ends with
  `unittest.main(...)`. A fixture git repository in a temp directory follows
  `tools/tests/test_reviewctl.py:15-23` (`git init -q`, local `user.email`, `user.name`,
  `core.autocrlf false`). CI runs the suite as `python3 -m unittest discover -s tools/tests -t .`
  (`.github/workflows/build.yml:217`) and fails below 700 tests.
- **ADR-011** (knowledge tiers): CHANGELOG is Archive tier (grep, never loaded). **ADR-010**: it stays
  a chronological release log. **ADR-008** (testability) is about C# services and does not apply;
  its intent (test pure logic without the live environment) is why `parse_log`, `render_section`
  and `insert_section` are pure. ADR-002 and ADR-007 do not apply: no C# changes.
- **AGENTS.md size**: `tools/reviewctl.py:367` requires `AGENTS.md` under 8,192 bytes
  (7,114 at `a39a9c86`); `python tools/reviewctl.py lint` checks it.
- **Context budget**: `tools/lint_docs.py` caps CLAUDE.md plus its imports at 24,576 bytes and the
  unscoped rules at 16,384 bytes; this plan only shortens those files.
- **Prose** (AGENTS.md "Human prose", `.claude/rules/output-style.md`): no em or en dash (U+2014,
  U+2013) in any line you write, code spans excepted. Existing lines you do not rewrite keep theirs.
  In an unquoted YAML `description:`, never write `: ` (it breaks the frontmatter).
- **Hooks** (`.claude/rules/hook-authoring.md`, `tools/test_hooks.sh:16-17`): run
  `bash tools/test_hooks.sh` before committing any change under `.claude/hooks/` or to
  `.claude/settings.json`. After editing hooks, settings or CLAUDE.md, CLAUDE.md asks for
  `/security-scan`; you cannot invoke skills, so run its tool `python tools/audit_claude_config.py --min HIGH`.
- **Single-owner files** `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
  `Directory.Build.props`: not touched by this plan. If anything seems to need them, STOP.
- **Commits** (AGENTS.md "Git and commits"): subject `<type>(<scope>): v<version> - <description>`,
  where the version is the `<Version>` in `Main/_Module/SubModule.xml` (`v2.0.30` at `a39a9c86`), at
  most 72 characters; body wrapped at 72; no AI attribution trailer; stage explicit paths only
  (never `git add -A`, `git add .` or `git commit -a`); never push. From this plan on, the commit
  body IS the changelog entry: write your four bodies for a reader of the release note.

## Commands you will need

| Purpose | Command (from the worktree) | Expected on success |
|---|---|---|
| Generator tests | `python tools/tests/test_changelog_from_commits.py` | `Ran 22 tests`, `OK`, exit 0 |
| Tools suite | `python -m unittest discover -s tools/tests -t .` | the Step 1 baseline plus 22 tests, no new failure or error |
| Hook suite | `bash tools/test_hooks.sh` | exit 0, `0 failed` (or exactly the Step 1 baseline failures) |
| Docs | `python tools/lint_docs.py` | no finding that the Step 1 baseline lacks |
| Docs, gating | `python tools/lint_docs.py --fail-on-drift` | same exit code as the Step 1 baseline (0 expected) |
| AGENTS.md contract | `python tools/reviewctl.py lint` | exit 0 |
| Config security | `python tools/audit_claude_config.py --min HIGH` | no HIGH or CRITICAL finding the Step 1 baseline lacks |
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (not needed: no C# changes) |
| Test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | baseline at `a39a9c86`: 10,313+ tests pass, 2 skipped, 0 failed; any failure is yours to explain (see Step 11) |
| Filtered test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | only to re-run a failing test in Step 11 |
| Data | `python tools/validate_moduledata.py` | not needed: no ModuleData change |

Never run `./build.ps1` (it deploys into the game install). `-p:DisableModuleCopy=true` alone does
not stop the copy; `-p:ModuleId=` is required on both dotnet commands. The C: drive is nearly
full: run `mkdir -p E:/repos/taom-improve/scratch/tmp` once and prefix every `dotnet` command with
`TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp `.

## Scope

**In scope** (the only files you may create, modify, move or delete):

- New: `tools/changelog_from_commits.py`, `tools/tests/test_changelog_from_commits.py`,
  `docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md` (by `git mv` of `CHANGELOG.md`).
- Replaced: `CHANGELOG.md` (a new, short generated-file header).
- Deleted: `.claude/hooks/check-changelog-changed.sh`, `.claude/hooks/check-changelog-updated.sh`.
- Committed but edited by Mike only (Step 0): `.claude/settings.json`.
- Edited: `tools/README.md`, `.claude/hooks/session-start.sh`, `.claude/hooks/block-broad-git-add.sh`,
  `.claude/hooks/check-moduledata-validation.sh`, `.claude/hooks/check-version-tagged.sh`,
  `docs/reference/hooks-catalog.md`, `docs/reference/release-process.md`, `.claude/rules/harness-facts.md`,
  `.claude/rules/hook-authoring.md`, `.claude/rules/external-skill-ports.md`, `.claude/rules/troops.md`,
  `.claude/rules/gui-ui.md`, `.claude/rules/simplicity-criterion.md`, `.claude/rules/working-discipline.md`,
  `AGENTS.md`, `CLAUDE.md`, `README.md`, `.ai/roles/builder.md`, `.agents/skills/taom-build/SKILL.md`,
  `.serena/memories/task_completion_checklist.md`, `docs/ai-includes/codex-operating-guide.md`,
  `docs/ai-includes/completion-workflow.md`, `docs/ai-includes/external-repo-adoption.md`,
  `docs/ai-includes/lord-skills-authoring.md`, `docs/ai-includes/new-culture-authoring.md`,
  `.claude/skills/release/SKILL.md`, `.claude/skills/ship/SKILL.md`, `.claude/skills/verify/SKILL.md`,
  `.claude/skills/deep-review/SKILL.md`, `.claude/skills/deep-review/lenses/4-completeness.md`,
  `.claude/skills/finish-branch/SKILL.md`, `.claude/skills/author-armor/SKILL.md`,
  `.claude/skills/verify-bindings/SKILL.md`, `.claude/skills/lint-cleanup-loop/SKILL.md`,
  `.claude/skills/scope-check/SKILL.md`, `.claude/skills/new-adr/SKILL.md`,
  `.claude/skills/improve/SKILL.md`, `.claude/skills/armory-audit/SKILL.md`.

**Out of scope** (do NOT touch, even though they mention CHANGELOG):

- `.claude/settings.json` by any tool or shell command (Step 0 is Mike's), and every other
  protected file (`settings.local.json`, `Directory.Build.props`, `docs/adrs/*.md`).
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, any `.cs` file, any ModuleData.
- `plans/README.md` (the orchestrator's) and everything under `plans/`, including the untracked
  copy of this plan.
- A hand-written `CHANGELOG.md` entry of any kind, and any commit after Step 10 (see Executor
  instructions): the only `CHANGELOG.md` change is Step 9's archive move and new header.
- `docs/ai-includes/git-and-commits.md` (its CHANGELOG incident rules still protect other shared
  files; shrinking them was not decided), `.claude/skills/new-creature-mount/SKILL.md` (another
  session's live edits; its `:92` mention is a follow-up), `docs/INDEX.md`.
- Lines that name CHANGELOG as a kind of prose or as history, left as they are:
  `AGENTS.md:34,40`, `.claude/rules/evidence-over-claims.md:54`, `.claude/rules/output-style.md:33`,
  `.claude/rules/external-skill-ports.md:132`, `.claude/rules/hook-authoring.md:59,65`,
  `.claude/rules/provenance.md`, `.claude/agents/refactoring-specialist.md:75`,
  `.claude/skills/humanizer/SKILL.md`, `.claude/skills/commit-split/SKILL.md:45`,
  `.claude/skills/engine-bump/SKILL.md:164`, `.claude/skills/deep-review/lenses/5-data-flow.md`,
  `6-design.md:29`, `7-xml.md:44`, `docs/ai-includes/codex-operating-guide.md:40`,
  `docs/reference/mcp-servers.md:30`, `Main/Features/NavalTravel/CHANGELOG.md`, and every code
  comment or test that cites "CHANGELOG <date>" (those entries now live in the archive).
- `tools/test_hooks.sh`: it names neither retired hook at `a39a9c86`; do not edit it.
- The live Armory, TAOM_Map, `E:\Steam\...`, and anything under `E:/repos/TAOM`.

## Git workflow

- Branch `improve/020-changelog-at-release` in the worktree `E:/repos/taom-improve/wt-020`. Never
  push, never open a PR, never merge.
- Four commits, in this order (subjects measured at 72 characters or fewer with `v2.0.30`):
  1. `feat(tools): v2.0.30 - CHANGELOG release section from the commit log` (Step 4)
  2. `chore(hooks): v2.0.30 - retire the two CHANGELOG hooks and their rules` (Step 6)
  3. `docs(workflow): v2.0.30 - the commit body is the changelog entry` (Step 8)
  4. `chore(changelog): v2.0.30 - archive the hand-written CHANGELOG` (Step 10)
- If `Main/_Module/SubModule.xml` in the worktree does not say `v2.0.30`, use the version it says.
- Write each message with the Write tool to `E:/repos/taom-improve/scratch/020-msg-<n>.txt`
  (Bash heredocs mangle quotes and backslashes), then
  `git -C E:/repos/taom-improve/wt-020 commit -F E:/repos/taom-improve/scratch/020-msg-<n>.txt`.
- Stage explicit paths with `git -C E:/repos/taom-improve/wt-020 add <paths>`; stage deletions with
  `git -C E:/repos/taom-improve/wt-020 rm <path>`; move with `git -C E:/repos/taom-improve/wt-020 mv`.
- Bodies: plain prose naming plan 020, every line 72 characters or fewer (trailers too: continue a
  long trailer on lines indented by two spaces), no em or en dash, no `Co-Authored-By` or
  "Generated with" trailer. Useful trailers: `Rejected:` (Step 6), `Not-tested:` (a live
  `/release` run).
- **BODYCHECK** (run on each message file BEFORE its `git commit`; expected output `0 0`):
  `python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(sum(len(l) > 72 for l in t.splitlines()), t.count('\u2014') + t.count('\u2013'))" < E:/repos/taom-improve/scratch/020-msg-<n>.txt`
  (lines over 72 characters, then em or en dashes). If it prints anything else, fix the file and
  re-run it; never `--amend` (an amend re-runs the live hooks against the main tree's HEAD).
- **STATUS** (every "status prints" check in this plan means this command):
  `git -C E:/repos/taom-improve/wt-020 status --porcelain | grep -vx '?? plans/020-changelog-at-release.md'`
- The live hooks in `E:/repos/TAOM` run on your commits and read the MAIN tree's index and
  `SubModule.xml`, not the worktree's. `check-changelog-changed.sh` does `cd "$CLAUDE_PROJECT_DIR"`
  (line 44) and lists `git diff --cached` there (line 57), so it allows commits 2 and 3 only while
  the main index stages nothing under `.claude/`, `CLAUDE.md` or `AGENTS.md` (true at planning
  time). If another session stages such a file there, the hook denies your commit; that is a STOP,
  never a reason to stage `CHANGELOG.md`. If any commit is denied, STOP (see STOP conditions).

## Steps

### Step 1: Verify the worktree, Step 0 and the baselines

Run, from the worktree:

1. `git -C E:/repos/taom-improve/wt-020 rev-parse --abbrev-ref HEAD` prints
   `improve/020-changelog-at-release`; `git -C E:/repos/taom-improve/wt-020 rev-parse --short HEAD`
   prints `a39a9c86`.
2. STATUS (the filtered command under "Git workflow") prints exactly ` M .claude/settings.json`.
3. `git -C E:/repos/taom-improve/wt-020 diff --numstat -- .claude/settings.json` prints
   `0	10	.claude/settings.json`, and `grep -c "check-changelog" .claude/settings.json` prints `0`.
4. `python -c "import json; d=json.load(open('.claude/settings.json',encoding='utf-8')); print(sum(len(g['hooks']) for ev in d['hooks'].values() for g in ev), len(d['hooks']))"`
   prints `26 9`.
5. Record baselines, each into a scratch file (later steps compare against these files), and copy
   the summary lines into your report. Shell variables do not persist between Bash calls, so start
   each command below with `cd E:/repos/taom-improve/wt-020 && S=E:/repos/taom-improve/scratch && `:
   `bash tools/test_hooks.sh > $S/020-base-hooks.txt 2>&1; tail -8 $S/020-base-hooks.txt`;
   `python -m unittest discover -s tools/tests -t . > $S/020-base-tools.txt 2>&1; tail -5 $S/020-base-tools.txt`;
   `python tools/lint_docs.py --summary > $S/020-base-lint.txt 2>&1; cat $S/020-base-lint.txt`;
   `python tools/lint_docs.py --fail-on-drift > /dev/null 2>&1; echo "exit $?" > $S/020-base-drift.txt; cat $S/020-base-drift.txt`;
   `python tools/reviewctl.py lint; echo "exit $?"`;
   `python tools/audit_claude_config.py --min HIGH > $S/020-base-audit.txt 2>&1; cat $S/020-base-audit.txt`;
   `mkdir -p $S/tmp && TEMP=$S/tmp TMP=$S/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > $S/020-base-dotnet.txt 2>&1; tail -5 $S/020-base-dotnet.txt`
   (baseline at `a39a9c86`: 10,313+ tests pass, 2 skipped, 0 failed; the executor wrapper also
   asks for this run before the first edit). If this base run fails a test, name it in your report
   as pre-existing and carry on; Step 11 may show no failure beyond it.
   Note: the hook suite already runs with Step 0 applied, so its numbers are this plan's baseline.
   Its section 4 runs each script still on disk, the two retired ones included, as non-gates; that
   is expected until Step 5.

**Verify**: items 1 to 4 print exactly what is stated. If item 2, 3 or 4 differs, Step 0 is
missing or wrong: STOP.

### Step 2: Write the failing generator tests (RED)

Create `tools/tests/test_changelog_from_commits.py` with exactly this content:

```python
#!/usr/bin/env python
"""Unit tests for the release CHANGELOG generator (tools/changelog_from_commits.py).

Run:  python tools/tests/test_changelog_from_commits.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib. The parse, render and insert functions are tested on fixture `git log` text;
the EndToEndTests build a throwaway git repository in a temp directory and run the script.
"""
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import changelog_from_commits as cfc  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "changelog_from_commits.py"


def record(sha, subject, body=""):
    """One commit as `git log --format=%H%x1f%s%x1f%b%x1e` prints it (git ends a body with a
    newline and separates records with one)."""
    return f"{sha}\x1f{subject}\x1f{body}\n\x1e\n"


FEAT = record("a" * 40, "feat(nazgul): v2.0.30 - the Nine scream (#645)",
              "The SCREAM is a second signature.\n\nNot-tested: in game.\n")
FIX = record("b" * 40, "fix(mission): v2.0.30 - park off-thread callback writes (#634)",
             "One paragraph.\n")
DOCS = record("c" * 40, "docs: v2.0.30 - a doc with no scope")
DIAG = record("d" * 40, "diag(boot): v2.0.30 - an uncommon type")
PLAIN = record("e" * 40, "Add unit tests for Animalia armory writer functionality",
               "Body of an IDE commit.\n")
NO_VERSION = record("f" * 40, "feat: Implement career kit generation from troop data (#629)")


class ParseLogTests(unittest.TestCase):
    def test_parse_log_reads_type_scope_version_and_description(self):
        (c,) = cfc.parse_log(FEAT)
        self.assertEqual(c.sha, "a" * 40)
        self.assertEqual(c.type, "feat")
        self.assertEqual(c.scope, "nazgul")
        self.assertEqual(c.version, "v2.0.30")
        self.assertEqual(c.description, "the Nine scream (#645)")

    def test_parse_log_reads_a_subject_with_no_scope(self):
        (c,) = cfc.parse_log(DOCS)
        self.assertEqual((c.type, c.scope, c.version), ("docs", None, "v2.0.30"))

    def test_parse_log_marks_subjects_without_the_label_as_unlabelled(self):
        commits = cfc.parse_log(PLAIN + NO_VERSION)
        self.assertEqual([c.type for c in commits], [None, None])
        self.assertEqual(commits[1].subject,
                         "feat: Implement career kit generation from troop data (#629)")

    def test_parse_log_keeps_a_multi_paragraph_body_verbatim(self):
        (c,) = cfc.parse_log(FEAT)
        self.assertEqual(c.body, "The SCREAM is a second signature.\n\nNot-tested: in game.")

    def test_parse_log_keeps_log_order_and_skips_empty_records(self):
        commits = cfc.parse_log(FIX + "\n" + FEAT + "\n")
        self.assertEqual([c.sha[0] for c in commits], ["b", "a"])


class RenderSectionTests(unittest.TestCase):
    def render(self, raw):
        return cfc.render_section("v2.0.31", "2026-10-01", "v2.0.30", cfc.parse_log(raw))

    def test_render_heading_carries_version_date_and_counts(self):
        text = self.render(FEAT + PLAIN)
        self.assertTrue(text.startswith(
            "## v2.0.31 (2026-10-01)\n\n"
            "Commits since v2.0.30: 2 (1 with the version label, 1 without).\n"))

    def test_render_groups_known_types_in_fixed_order_then_other_types(self):
        text = self.render(DIAG + DOCS + FIX + FEAT)
        order = [text.index(h) for h in
                 ("### Features", "### Fixes", "### Documentation", "### diag")]
        self.assertEqual(order, sorted(order))

    def test_render_lists_unlabelled_commits_in_their_own_last_group(self):
        text = self.render(PLAIN + FEAT + NO_VERSION)
        tail = text[text.index("### Commits without the version label"):]
        self.assertIn("#### Add unit tests for Animalia armory writer functionality", tail)
        self.assertIn("#### feat: Implement career kit generation from troop data (#629)", tail)
        self.assertNotIn("### Features", tail)

    def test_render_entry_is_subject_short_sha_then_body(self):
        text = self.render(FEAT)
        self.assertIn(
            "#### feat(nazgul): v2.0.30 - the Nine scream (#645)\n\n"
            "`aaaaaaaa`\n\n"
            "The SCREAM is a second signature.\n\nNot-tested: in game.\n", text)

    def test_render_keeps_log_order_inside_a_group(self):
        second_fix = record("9" * 40, "fix(ui): v2.0.30 - a later fix")
        text = self.render(second_fix + FIX)
        self.assertLess(text.index("a later fix"), text.index("park off-thread"))

    def test_render_omits_empty_groups_and_ends_with_one_newline(self):
        text = self.render(FIX)
        self.assertNotIn("### Features", text)
        self.assertNotIn("### Commits without the version label", text)
        self.assertTrue(text.endswith("One paragraph.\n"))


class InsertSectionTests(unittest.TestCase):
    HEADER = "# CHANGELOG\n\n> Generated.\n"
    SECTION = "## v2.0.31 (2026-10-01)\n\nnew\n"

    def test_insert_places_the_section_above_the_newest_release(self):
        old = self.HEADER + "\n## v2.0.30 (2026-09-18)\n\nold\n"
        out = cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertEqual(out, self.HEADER + "\n## v2.0.31 (2026-10-01)\n\nnew\n\n"
                                            "## v2.0.30 (2026-09-18)\n\nold\n")

    def test_insert_into_a_header_only_file_appends_after_the_header(self):
        out = cfc.insert_section(self.HEADER, self.SECTION, "v2.0.31")
        self.assertEqual(out, self.HEADER + "\n" + self.SECTION)

    def test_insert_refuses_a_version_already_present(self):
        old = self.HEADER + "\n## v2.0.31 (2026-10-01)\n\nnew\n"
        with self.assertRaises(ValueError):
            cfc.insert_section(old, self.SECTION, "v2.0.31")

    def test_insert_refuses_a_hand_written_section_above_the_releases(self):
        old = self.HEADER + "\n## 2026-10-02\n\n### fix: by hand\n\n## v2.0.30 (2026-09-18)\n"
        with self.assertRaises(ValueError) as ctx:
            cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertIn("## 2026-10-02", str(ctx.exception))

    def test_insert_does_not_confuse_a_longer_version(self):
        old = self.HEADER + "\n## v2.0.310 (2027-01-01)\n\nx\n"
        out = cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertIn("## v2.0.31 (2026-10-01)", out)

    def test_insert_keeps_crlf_line_endings(self):
        old = self.HEADER.replace("\n", "\r\n")
        out = cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertNotIn("\n", out.replace("\r\n", ""))


class EndToEndTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name)
        self.git("init", "-q")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "user.name", "Changelog generator test")
        self.git("config", "core.autocrlf", "false")
        self.commit("chore(release): v2.0.30 - TAOM v2.0.30")
        self.git("tag", "v2.0.30")
        self.commit("fix(combat): v2.0.30 - archers hold the wall", "They hold it now.")
        self.commit("Add a test from an IDE")
        (self.repo / "CHANGELOG.md").write_text("# CHANGELOG\n\n> Generated.\n",
                                                encoding="utf-8")

    def git(self, *args):
        return subprocess.run(["git", "-C", str(self.repo), *args], capture_output=True,
                              check=True).stdout.decode("utf-8")

    def commit(self, subject, body=""):
        message = subject + ("\n\n" + body if body else "")
        self.git("commit", "-q", "--allow-empty", "-m", message)

    def run_tool(self, *args):
        return subprocess.run([sys.executable, str(TOOL), "--repo", str(self.repo), *args],
                              capture_output=True)

    def test_main_writes_the_section_from_the_previous_tag(self):
        proc = self.run_tool("--version", "v2.0.31", "--date", "2026-10-01", "--write")
        self.assertEqual(proc.returncode, 0, proc.stderr)
        text = (self.repo / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertIn("## v2.0.31 (2026-10-01)", text)
        self.assertIn("Commits since v2.0.30: 2 (1 with the version label, 1 without).", text)
        self.assertIn("#### fix(combat): v2.0.30 - archers hold the wall", text)
        self.assertIn("They hold it now.", text)
        self.assertIn("#### Add a test from an IDE", text)
        self.assertNotIn("TAOM v2.0.30", text)
        self.assertIn(b"2 commits, 1 with the version label, 1 without", proc.stderr)

    def test_main_refuses_a_second_write_of_the_same_version(self):
        self.assertEqual(self.run_tool("--version", "v2.0.31", "--write").returncode, 0)
        before = (self.repo / "CHANGELOG.md").read_bytes()
        proc = self.run_tool("--version", "v2.0.31", "--write")
        self.assertEqual(proc.returncode, 2)
        self.assertEqual((self.repo / "CHANGELOG.md").read_bytes(), before)

    def test_main_prints_the_section_without_write(self):
        proc = self.run_tool("--version", "v2.0.31", "--date", "2026-10-01")
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertTrue(proc.stdout.decode("utf-8").startswith("## v2.0.31 (2026-10-01)\n"))
        self.assertEqual((self.repo / "CHANGELOG.md").read_text(encoding="utf-8"),
                         "# CHANGELOG\n\n> Generated.\n")

    def test_main_rejects_a_malformed_version(self):
        self.assertEqual(self.run_tool("--version", "2.0.31").returncode, 2)

    def test_main_refuses_an_empty_range(self):
        self.git("tag", "v2.0.31")
        self.assertEqual(self.run_tool("--version", "v2.0.32").returncode, 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
```

**Verify**: `python tools/tests/test_changelog_from_commits.py; echo "exit $?"` fails with
`ModuleNotFoundError: No module named 'changelog_from_commits'` and a non-zero exit.

### Step 3: Write the generator (GREEN)

Create `tools/changelog_from_commits.py` with exactly this content:

```python
#!/usr/bin/env python
"""Generate one CHANGELOG.md release section from the commits since the previous release tag.

`/release` runs this (AGENTS.md "Documentation duty"): the commit body is the changelog entry,
so nobody hand-edits CHANGELOG.md between releases. Pure stdlib.

Every commit subject carries the version label `<type>[(scope)][!]: vX.Y.Z - <description>`
(`.claude/hooks/check-commit-subject-version.sh` gates it). Labelled commits are grouped by type
in a fixed order; commits without the label are listed in their own last group so nothing is lost.

    python tools/changelog_from_commits.py --version v2.0.31            # print the section
    python tools/changelog_from_commits.py --version v2.0.31 --write    # insert it into CHANGELOG.md

Exit codes: 0 success; 2 refused (bad version, no commits in range, no previous tag, git
failure, or CHANGELOG.md already has this version's section or a hand-written section above
the release sections).
"""
import argparse
import datetime
import re
import subprocess
import sys
from collections import namedtuple
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

# Same shape as the subject gate in .claude/hooks/check-commit-subject-version.sh, with groups.
LABEL_RE = re.compile(
    r"^(?P<type>[a-z][a-z0-9]*)(?:\((?P<scope>[^)]+)\))?!?: "
    r"(?P<version>v\d+\.\d+\.\d+(?:\.\d+)?) - (?P<description>\S.*)$"
)
VERSION_ARG_RE = re.compile(r"^v\d+\.\d+\.\d+$")
RELEASE_HEADING_RE = re.compile(r"^## v\d+\.\d+\.\d+ \(")

# Known types first, in this order; any other type follows alphabetically under its own name.
TYPE_ORDER = [
    ("feat", "Features"),
    ("fix", "Fixes"),
    ("balance", "Balance"),
    ("data", "Data"),
    ("perf", "Performance"),
    ("refactor", "Refactoring"),
    ("test", "Tests"),
    ("docs", "Documentation"),
    ("chore", "Chores"),
]
UNLABELLED_HEADING = "Commits without the version label"

LOG_FORMAT = "%H%x1f%s%x1f%b%x1e"

Commit = namedtuple("Commit", "sha subject body type scope version description")


def parse_log(raw):
    """Parse `git log --format=%H%x1f%s%x1f%b%x1e` output into Commits, in log order.

    A subject without the version label yields a Commit whose type, scope, version and
    description are None."""
    commits = []
    for record in raw.split("\x1e"):
        record = record.strip("\r\n")
        if not record.strip():
            continue
        sha, subject, body = (record.split("\x1f") + ["", ""])[:3]
        body = body.replace("\r\n", "\n").strip("\n").rstrip()
        m = LABEL_RE.match(subject)
        if m:
            commits.append(Commit(sha, subject, body, m.group("type"), m.group("scope"),
                                  m.group("version"), m.group("description")))
        else:
            commits.append(Commit(sha, subject, body, None, None, None, None))
    return commits


def _groups(commits):
    """(heading, commits) pairs: known types in TYPE_ORDER, other types alphabetically, then
    the unlabelled commits. Empty groups are omitted; log order is kept inside a group."""
    known = dict(TYPE_ORDER)
    groups = []
    for type_name, heading in TYPE_ORDER:
        members = [c for c in commits if c.type == type_name]
        if members:
            groups.append((heading, members))
    others = sorted({c.type for c in commits if c.type is not None and c.type not in known})
    for type_name in others:
        groups.append((type_name, [c for c in commits if c.type == type_name]))
    unlabelled = [c for c in commits if c.type is None]
    if unlabelled:
        groups.append((UNLABELLED_HEADING, unlabelled))
    return groups


def render_section(version, date, since, commits):
    """The Markdown section for one release, ending in exactly one newline."""
    labelled = sum(1 for c in commits if c.type is not None)
    lines = [
        f"## {version} ({date})",
        "",
        f"Commits since {since}: {len(commits)} ({labelled} with the version label, "
        f"{len(commits) - labelled} without).",
        "",
    ]
    for heading, members in _groups(commits):
        lines += [f"### {heading}", ""]
        for c in members:
            lines += [f"#### {c.subject}", "", f"`{c.sha[:8]}`", ""]
            if c.body:
                lines += [c.body, ""]
    return "\n".join(lines).rstrip("\n") + "\n"


def insert_section(changelog, section, version):
    """Insert `section` above the first `## ` heading of `changelog`, or append it after the
    header when there is none. Keeps the file's line endings (CRLF if it has any).

    Raises ValueError if a `## <version> ` heading already exists, or if the first `## `
    heading is not a generated release heading (`## vX.Y.Z (`): that is a hand-written
    section, and only /release writes this file."""
    newline = "\r\n" if "\r\n" in changelog else "\n"
    text = changelog.replace("\r\n", "\n")
    if re.search(r"^## " + re.escape(version) + r"(?: |$)", text, re.M):
        raise ValueError(f"CHANGELOG.md already has a section for {version}")
    m = re.search(r"^## .*$", text, re.M)
    if m and not RELEASE_HEADING_RE.match(m.group(0)):
        raise ValueError(f"CHANGELOG.md has a hand-written section above the releases: "
                         f"{m.group(0)!r}. Only /release writes this file; move those entries "
                         f"into commit bodies or the release note, then run again")
    if m:
        result = text[:m.start()] + section + "\n" + text[m.start():]
    else:
        result = text.rstrip("\n") + "\n\n" + section
    return result.replace("\n", newline)


def _git(repo, *args):
    proc = subprocess.run(["git", "-C", str(repo), *args], capture_output=True,
                          encoding="utf-8", errors="replace")
    if proc.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {proc.stderr.strip()}")
    return proc.stdout


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--version", required=True, help="the release being cut, e.g. v2.0.31")
    ap.add_argument("--since", help="previous release tag (default: git describe --tags "
                                    "--abbrev=0 --match 'v[0-9]*' <until>)")
    ap.add_argument("--until", default="HEAD", help="end of the range (default: HEAD)")
    ap.add_argument("--date", default=None, help="section date YYYY-MM-DD (default: today)")
    ap.add_argument("--write", action="store_true", help="insert into <repo>/CHANGELOG.md "
                                                         "instead of printing")
    ap.add_argument("--repo", default=str(REPO_ROOT), help="repository root (default: this "
                                                           "script's repository)")
    args = ap.parse_args(argv)

    if not VERSION_ARG_RE.match(args.version):
        sys.stderr.write(f"changelog_from_commits: --version must look like v2.0.31, got "
                         f"{args.version!r}\n")
        return 2
    date = args.date or datetime.date.today().isoformat()
    repo = Path(args.repo)
    try:
        since = args.since or _git(repo, "describe", "--tags", "--abbrev=0",
                                   "--match", "v[0-9]*", args.until).strip()
        raw = _git(repo, "log", "--no-merges", f"--format={LOG_FORMAT}",
                   f"{since}..{args.until}")
    except RuntimeError as exc:
        sys.stderr.write(f"changelog_from_commits: {exc}\n")
        return 2
    commits = parse_log(raw)
    if not commits:
        sys.stderr.write(f"changelog_from_commits: no commits in {since}..{args.until}\n")
        return 2
    section = render_section(args.version, date, since, commits)
    labelled = sum(1 for c in commits if c.type is not None)
    summary = (f"changelog_from_commits: {since}..{args.until}: {len(commits)} commits, "
               f"{labelled} with the version label, {len(commits) - labelled} without\n")

    if args.write:
        path = repo / "CHANGELOG.md"
        with open(path, encoding="utf-8-sig", newline="") as fh:
            current = fh.read()
        try:
            updated = insert_section(current, section, args.version)
        except ValueError as exc:
            sys.stderr.write(f"changelog_from_commits: {exc}\n")
            return 2
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(updated)
    else:
        sys.stdout.buffer.write(section.encode("utf-8"))
        sys.stdout.flush()
    sys.stderr.write(summary)
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

Design points the tests pin (do not change them without changing the tests): stdout is written as
UTF-8 bytes (`sys.stdout.buffer`) so a Windows console code page cannot raise on a non-ASCII body;
the file is read with `utf-8-sig` and written without a BOM, keeping its line endings; the count
summary goes to stderr; exit 2 on every refusal.

**Verify**:

1. `python tools/tests/test_changelog_from_commits.py` prints `Ran 22 tests` and `OK`.
2. Smoke on real history, read-only (the tags are shared with the main repository):
   `python tools/changelog_from_commits.py --version v2.0.30 --since v2.0.29 --until v2.0.30 --date 2026-09-18 > E:/repos/taom-improve/scratch/020-smoke.md`
   prints on stderr exactly
   `changelog_from_commits: v2.0.29..v2.0.30: 57 commits, 53 with the version label, 4 without`
   and exits 0; `grep -c '^#### ' E:/repos/taom-improve/scratch/020-smoke.md` prints `57`;
   `grep '^### ' E:/repos/taom-improve/scratch/020-smoke.md` prints, in order, `### Features`,
   `### Fixes`, `### Balance`, `### Tests`, `### Documentation`, `### Chores`,
   `### Commits without the version label`. (These numbers were measured while planning with
   this exact script.)
3. STATUS prints exactly three lines, ` M .claude/settings.json`,
   `?? tools/changelog_from_commits.py` and `?? tools/tests/test_changelog_from_commits.py` (the
   smoke writes nothing in the repo; `tools/__pycache__/` is gitignored).

### Step 4: Register the tool and commit it

In `tools/README.md`, section `## Docs & knowledge base`, insert this row directly after the row
that starts `` | `build_backlinks.py` | `` (line 296 at `a39a9c86`):

```markdown
| `changelog_from_commits.py` | **Writes one `CHANGELOG.md` release section** (pure stdlib), run by `/release` Phase 4: every non-merge commit since the previous `v*` tag, the labelled ones (`<type>[(scope)]: vX.Y.Z - <description>`) grouped by type with subject, short SHA and body verbatim, the rest in a last "Commits without the version label" group. Refuses (exit 2) a version already present or a hand-written section above the releases; the commit body is the changelog entry, so nobody else edits the file. Tested by `tools/tests/test_changelog_from_commits.py`. | `--version` (required), `--write`, `--since`, `--until`, `--date`, `--repo` |
```

Then run the tools suite: `python -m unittest discover -s tools/tests -t . 2>&1 | tail -5`.

**Verify**: the tools suite ran exactly 22 more tests than `E:/repos/taom-improve/scratch/020-base-tools.txt`,
with no failure or error that the baseline lacked. Then commit 1: stage
`tools/changelog_from_commits.py tools/tests/test_changelog_from_commits.py tools/README.md`
(NOT `.claude/settings.json` yet) and commit with subject
`feat(tools): v2.0.30 - CHANGELOG release section from the commit log`. Body: what the tool
reads, how it groups, the refusals, the smoke numbers from Step 3. `git -C E:/repos/taom-improve/wt-020 show --stat HEAD`
lists exactly those three files, and BODYCHECK printed `0 0` on the message file before you committed.

### Step 5: Retire the two CHANGELOG hooks

**5.1.** Run `git -C E:/repos/taom-improve/wt-020 rm .claude/hooks/check-changelog-changed.sh .claude/hooks/check-changelog-updated.sh`.
Then make each edit below with the Edit tool (read the file first). Each block is the exact text at
`a39a9c86`, leading spaces included, and occurs exactly once in its file. The worktree files have
CRLF line endings; a multi-line block still matches line for line. Where a whole line goes, the
block to replace ends with the start of the NEXT line, and the replacement is that start alone:
this removes the line together with its line break, so no blank line is left inside a table.

**5.2.** `.claude/hooks/session-start.sh`, lines 33-35 at `a39a9c86`. Replace:

```text
# Two separate conditions, deliberately NOT ANDed. Five gates
# (check-changelog-changed, check-claude-files-tracked, check-doc-config-drift,
# check-moduledata-validation, check-native-dll-crt) have no jq path at all and call
```

with:

```text
# Two separate conditions, deliberately NOT ANDed. Four gates
# (check-claude-files-tracked, check-doc-config-drift,
# check-moduledata-validation, check-native-dll-crt) have no jq path at all and call
```

**5.3.** `.claude/hooks/session-start.sh`, line 41 at `a39a9c86`. Replace:

```text
    echo "    The five python-only gates are failing OPEN right now: changelog-staged,"
```

with:

```text
    echo "    The four python-only gates are failing OPEN right now:"
```

The next line (`claude-files-tracked, doc-config-drift, moduledata-refs, native-DLL-CRT.`) stays.

**5.4.** `.claude/hooks/session-start.sh`, lines 198-207 at `a39a9c86` (the block, its trailing
blank line 206, and the comment on line 207). Replace:

```text
# Latest CHANGELOG entry (date + feature titles only)
echo ""
echo "Latest CHANGELOG:"
if [[ -f CHANGELOG.md ]]; then
  awk '/^## [0-9]/{if(found) exit; found=1; print; next} found && /^### /{print}' CHANGELOG.md
else
  echo "  (no CHANGELOG.md)"
fi

# Uncommitted changes count
```

with:

```text
# Uncommitted changes count
```

Check: `grep -n -A2 'git log --oneline -5' .claude/hooks/session-start.sh` prints three lines:
`196:git log --oneline -5 2>/dev/null || echo "  (no commits)"`, `197-` (blank) and
`198-# Uncommitted changes count`.

**5.5.** `.claude/hooks/block-broad-git-add.sh`, line 111 at `a39a9c86`. Replace:

```text
    # different thing entirely and is gated by check-changelog-changed.sh.
```

with:

```text
    # different thing entirely.
```

**5.6.** `.claude/hooks/check-moduledata-validation.sh`, line 35 at `a39a9c86`. Replace:

```text
# Extract the bash command from tool_input (mirrors check-changelog-changed.sh).
```

with:

```text
# Extract the bash command from tool_input.
```

**5.7.** `.claude/hooks/check-version-tagged.sh`, line 5 at `a39a9c86`. Replace:

```text
# Mirrors check-changelog-updated.sh / check-verification-evidence.sh conventions: reads
```

with:

```text
# Mirrors check-verification-evidence.sh conventions: reads
```

**5.8.** `docs/reference/hooks-catalog.md`, line 3 at `a39a9c86` (part of the line). Replace:

```text
**Recounted 2026-09-13 (after `check-commit-subject-version.sh`): 29 scripts on disk, 28 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 33 total.**
```

with:

```text
**Recounted YYYY-MM-DD (after plan 020 retired the two CHANGELOG hooks): 27 scripts on disk, 26 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 31 total.**
```

Write the date you make the edit in place of `YYYY-MM-DD`. Leave the rest of line 3 as it is.

**5.9.** `docs/reference/hooks-catalog.md`, line 24 at `a39a9c86`, plus the start of line 25. Replace:

```text
| `check-changelog-updated.sh` | Stop | Reminds to update CHANGELOG.md when source is dirty. One-shot per streak (`.changelog-reminded` marker, added 2026-08-05); re-arms when CHANGELOG becomes dirty/staged |
| `check-version-tagged.sh` |
```

with:

```text
| `check-version-tagged.sh` |
```

**5.10.** `docs/reference/hooks-catalog.md`, line 26 at `a39a9c86` (part of the line). Replace:

```text
Prints branch, recent commits, CHANGELOG summary on startup.
```

with:

```text
Prints branch and recent commits on startup.
```

**5.11.** `docs/reference/hooks-catalog.md`, line 38 at `a39a9c86`, plus the start of line 39. Replace:

```text
| `check-changelog-changed.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when `.claude/`, `CLAUDE.md`, or `AGENTS.md` is staged but `CHANGELOG.md` is not. Catches the recurring "forgot to update CHANGELOG" process violation. |
| `check-commit-subject-version.sh` |
```

with:

```text
| `check-commit-subject-version.sh` |
```

Check (after 5.8 to 5.11): `sed -n 20,38p docs/reference/hooks-catalog.md | grep -c '^|'` prints
`19` (the table still runs unbroken from its header on line 20; before the edits it ran to line 40).

**5.12.** `.claude/rules/harness-facts.md`, lines 105-108 at `a39a9c86`. Replace:

```text
3. **Committing `.claude/` changes:** `check-changelog-changed.sh` blocks unless CHANGELOG.md is in the
   commit, amends included; `check-claude-files-tracked.sh` blocks if a file under
   `.claude/{skills,agents,rules,hooks}/` is untracked or ignored. Both fire only on Claude-driven
   commits.
```

with:

```text
3. **Committing `.claude/` changes:** `check-claude-files-tracked.sh` blocks if a file under
   `.claude/{skills,agents,rules,hooks}/` is untracked or ignored. It fires only on Claude-driven
   commits.
```

**5.13.** `.claude/rules/hook-authoring.md`, line 41 at `a39a9c86` (part of the line). Replace:

```text
**Reference pattern** (used by `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, and `suggest-compact.sh`):
```

with:

```text
**Reference pattern** (used by `check-claude-files-tracked.sh` and `suggest-compact.sh`):
```

**Verify**:

1. `git -C E:/repos/taom-improve/wt-020 ls-files .claude/hooks | grep -c '\.sh$'` prints `27`.
2. `grep -rn "check-changelog" .claude/hooks docs/reference/hooks-catalog.md .claude/rules/harness-facts.md`
   prints nothing; `grep -n "check-changelog" .claude/rules/hook-authoring.md` prints only line 59.
3. `for f in session-start block-broad-git-add check-moduledata-validation check-version-tagged; do bash -n ".claude/hooks/$f.sh" || echo "SYNTAX $f"; done`
   prints nothing.
4. `bash tools/test_hooks.sh > E:/repos/taom-improve/scratch/020-after5-hooks.txt 2>&1; echo "exit $?"; tail -8 E:/repos/taom-improve/scratch/020-after5-hooks.txt`:
   exit 0 and `0 failed`, or exactly the failures in `020-base-hooks.txt`; the pass count may drop
   (the two scripts' rows are gone), nothing new may fail.
5. `CLAUDE_PROJECT_DIR="$PWD" bash .claude/hooks/session-start.sh </dev/null | grep -c "Latest CHANGELOG"`
   prints `0`, and the output still contains `Recent commits:`.
6. `python tools/audit_claude_config.py --min HIGH > E:/repos/taom-improve/scratch/020-after5-audit.txt 2>&1; diff E:/repos/taom-improve/scratch/020-base-audit.txt E:/repos/taom-improve/scratch/020-after5-audit.txt`:
   no added (`>`) line reports a HIGH or CRITICAL finding.

### Step 6: Commit the hook retirement

Stage `.claude/settings.json` (Mike's Step 0 edit), the two deletions (already staged by `git rm`),
`.claude/hooks/session-start.sh`, `.claude/hooks/block-broad-git-add.sh`,
`.claude/hooks/check-moduledata-validation.sh`, `.claude/hooks/check-version-tagged.sh`,
`docs/reference/hooks-catalog.md`, `.claude/rules/harness-facts.md`, `.claude/rules/hook-authoring.md`.
Commit with subject `chore(hooks): v2.0.30 - retire the two CHANGELOG hooks and their rules`. The
body says what each hook enforced, why that duty is gone (decision 18: `/release` generates the
file), that Mike made the `settings.json` edit, and ends with this trailer, wrapped exactly so:

```text
Rejected: repurposing check-changelog-changed.sh as a hand-edit gate
  (the generator refuses a hand-written section; a commit gate would
  fail in-flight sessions at the cut-over)
```

**Verify**: `git -C E:/repos/taom-improve/wt-020 show --stat HEAD` lists exactly those 10 paths
(two as deletions); STATUS prints nothing; BODYCHECK printed `0 0` on the message file before you committed.

### Step 7: Change every instruction that tells an agent to edit CHANGELOG.md

Make each edit below with the Edit tool (read the file first). Each block is the exact text at
`a39a9c86` (every one was checked to occur exactly once in its file, and the whole set was applied
to a copy while planning: the phrase grep then printed only `hook-authoring.md:59`, no dash was
added, every skill frontmatter still parsed as YAML, and `AGENTS.md` came to 7,261 bytes with LF
endings, about 7,370 as the CRLF worktree copy; either is under the 8,192 limit). Replace
only the block; where the line keeps an existing em dash outside the block, leave that dash.
Leading spaces inside a block are part of the text.

**Entry documents**

**7.1.** `AGENTS.md`, line 80 at `a39a9c86`. Replace:

```text
  [feature-map](docs/reference/feature-map.md) row. CHANGELOG.md is updated every session.
```

with:

```text
  [feature-map](docs/reference/feature-map.md) row.
- The commit body is the changelog entry: write it for a reader of the release note. `/release`
  generates `CHANGELOG.md` from commit subjects and bodies; never edit that file by hand.
```

**7.2.** `CLAUDE.md`, line 56 at `a39a9c86` (part of the line). Replace:

```text
then issue, docs, CHANGELOG |
```

with:

```text
then issue and docs |
```

**7.3.** `README.md`, line 166 at `a39a9c86` (part of the line). Replace:

```text
closeout (issue, feature doc, CHANGELOG).
```

with:

```text
closeout (issue, feature doc, commit body as the changelog entry).
```

**7.4.** `.ai/roles/builder.md`, line 16 at `a39a9c86`. Replace:

```text
   compatibility and the test oracle. Update relevant docs and the changelog.
```

with:

```text
   compatibility and the test oracle. Update relevant docs; the commit body is
   the changelog entry.
```

**7.5.** `.agents/skills/taom-build/SKILL.md`, lines 25-26 at `a39a9c86`. Replace:

```text
Update feature documentation, actual test evidence and the changelog when the
task warrants them. Report unavailable engine/runtime verification explicitly.
```

with:

```text
Update feature documentation and actual test evidence when the task warrants
them; the commit body is the changelog entry (`/release` writes CHANGELOG.md).
Report unavailable engine/runtime verification explicitly.
```

**7.6.** `docs/ai-includes/codex-operating-guide.md`, line 157 at `a39a9c86`. Replace:

```text
Update the relevant feature doc, index and changelog when the task warrants it.
```

with:

```text
Update the relevant feature doc and index when the task warrants it; the commit
body is the changelog entry.
```

**7.7.** `.serena/memories/task_completion_checklist.md`, line 7 at `a39a9c86` (part of the line). Replace:

```text
3. **Update CHANGELOG.md**: Summarize all changes (grouped by date, then category)
```

with:

```text
3. **Write the commit body as the changelog entry**: `/release` generates CHANGELOG.md from commit subjects and bodies; never edit it by hand
```

**Workflow docs**

**7.8.** `docs/ai-includes/completion-workflow.md`, lines 99-100 at `a39a9c86`. Replace:

```text
          b7e7188. The pre-commit hook only enforces CHANGELOG, not issue
          creation — discipline is on the author. Pattern: open the issue
```

with:

```text
          b7e7188. No hook enforces issue creation; discipline is on the
          author. Pattern: open the issue
```

**7.9.** `docs/ai-includes/completion-workflow.md`, line 104 at `a39a9c86`. Replace:

```text
  13. Update CHANGELOG.md
```

with:

```text
  13. Write the commit body as the changelog entry (/release generates CHANGELOG.md)
```

**7.10.** `docs/ai-includes/external-repo-adoption.md`, line 33 at `a39a9c86` (part of the line). Replace:

```text
Respect concurrent writers: re-read a shared file (`CHANGELOG.md`) immediately before editing it; the pre-commit hook requires `CHANGELOG.md` staged alongside any `.claude/` change.
```

with:

```text
Respect concurrent writers: re-read a shared file (`docs/INDEX.md`, `docs/reference/feature-map.md`) immediately before editing it.
```

**7.11.** `docs/ai-includes/lord-skills-authoring.md`, line 182 at `a39a9c86` (part of the line). Replace:

```text
 + `SubModule.xml` + `CHANGELOG.md` |
```

with:

```text
 + `SubModule.xml` |
```

**7.12.** `docs/ai-includes/lord-skills-authoring.md`, line 484 at `a39a9c86` (part of the line). Replace:

```text
`taom_lord_skill_sets.xml`, `CHANGELOG.md`, possibly
```

with:

```text
`taom_lord_skill_sets.xml`, possibly
```

**7.13.** `docs/ai-includes/lord-skills-authoring.md`, line 485 at `a39a9c86`. Replace:

```text
- [ ] CHANGELOG.md updated.
```

with:

```text
- [ ] The commit body describes the change (`/release` turns it into the CHANGELOG entry).
```

**7.14.** `docs/ai-includes/lord-skills-authoring.md`, line 502 at `a39a9c86` (part of the line). Replace:

```text
Flag this in PR descriptions and CHANGELOG |
```

with:

```text
Flag this in PR descriptions and the commit body |
```

**7.15.** `docs/ai-includes/new-culture-authoring.md`, line 425 at `a39a9c86`, plus the start of
line 426. Replace:

```text
| **CHANGELOG** | `CHANGELOG.md` | modified (per-iteration entries) |
| **RCA (if any Codex findings)** |
```

with:

```text
| **RCA (if any Codex findings)** |
```

Check: `sed -n 424,425p docs/ai-includes/new-culture-authoring.md | grep -c '^|'` prints `2`.

**7.16.** `docs/reference/release-process.md`, line 78 at `a39a9c86`. Replace:

```text
5. CHANGELOG entry.
```

with:

```text
5. Generate the CHANGELOG section: `python tools/changelog_from_commits.py --version vX.Y.Z --write` (every commit since the previous tag, subject and body verbatim, grouped by type).
```

**Rules**

**7.17.** `.claude/rules/external-skill-ports.md`, line 123 at `a39a9c86`. Replace:

```text
2. **Update CHANGELOG.md** in the same commit. The pre-commit hook `check-changelog-changed.sh` enforces this for `.claude/` changes.
```

with:

```text
2. **Describe the port in the commit body**: it is the changelog entry (`/release` generates `CHANGELOG.md` from commit bodies).
```

**7.18.** `.claude/rules/troops.md`, line 22 at `a39a9c86` (part of the line). Replace:

```text
| 7. CHANGELOG | `CHANGELOG.md` | Document the changes |
```

with:

```text
| 7. Commit body | the commit message | Describe the changes; `/release` builds `CHANGELOG.md` from it |
```

**7.19.** `.claude/rules/gui-ui.md`, line 40 at `a39a9c86` (part of the line). Replace:

```text
say so in the CHANGELOG `Not-tested:` line.
```

with:

```text
say so in the commit's `Not-tested:` trailer.
```

**7.20.** `.claude/rules/simplicity-criterion.md`, line 14 at `a39a9c86` (part of the line). Replace:

```text
in the PR or CHANGELOG |
```

with:

```text
in the PR or commit body |
```

**7.21.** `.claude/rules/working-discipline.md`, line 20 at `a39a9c86` (part of the line). Replace:

```text
(commit, CHANGELOG, log)
```

with:

```text
(commit body, log)
```

**Skills**

**7.22.** `.claude/skills/ship/SKILL.md`, line 3 at `a39a9c86` (part of the line). Replace:

```text
then close the issue and update docs + CHANGELOG.
```

with:

```text
then close the issue and update the docs.
```

This is the YAML `description:`; keep it unquoted, one line.

**7.23.** `.claude/skills/ship/SKILL.md`, line 34 at `a39a9c86`. Replace:

```text
10. Update `CHANGELOG.md`.
```

with:

```text
10. Write the commit body as the changelog entry; `/release` generates `CHANGELOG.md` from it.
```

**7.24.** `.claude/skills/verify/SKILL.md`, lines 56-65 at `a39a9c86`. Replace:

````text
## Step 5: CHANGELOG Check

Check if `CHANGELOG.md` has been modified (staged or unstaged):

```bash
git diff --name-only -- CHANGELOG.md
git diff --staged --name-only -- CHANGELOG.md
```

If C# or XML files changed but CHANGELOG not updated, flag it.
````

with:

````text
## Step 5: CHANGELOG.md untouched

Only `/release` writes `CHANGELOG.md` (generated from commit bodies). Check that this work did
not edit it by hand:

```bash
git diff --name-only HEAD -- CHANGELOG.md
```

If it prints `CHANGELOG.md` outside a `/release` run, flag it: the entry belongs in the commit body.
````

**7.25.** `.claude/skills/verify/SKILL.md`, line 78 at `a39a9c86`. Replace:

```text
CHANGELOG:  [Updated/NOT UPDATED]
```

with:

```text
CHANGELOG:  [Untouched/HAND-EDITED]
```

**7.26.** `.claude/skills/deep-review/lenses/4-completeness.md`, line 11 at `a39a9c86`. Replace:

```text
5. **CHANGELOG Updated:** Check if CHANGELOG.md has been modified with an entry for this work. Use `git diff --name-only -- CHANGELOG.md`.
```

with:

```text
5. **CHANGELOG.md untouched:** only `/release` writes it, from commit bodies. `git diff --name-only HEAD -- CHANGELOG.md` must print nothing; a hand edit is a defect.
```

**7.27.** `.claude/skills/deep-review/lenses/4-completeness.md`, line 19 at `a39a9c86`. Replace:

```text
- ✅/❌ CHANGELOG: [updated / NOT UPDATED]
```

with:

```text
- ✅/❌ CHANGELOG.md: [untouched / HAND-EDITED]
```

**7.28.** `.claude/skills/deep-review/SKILL.md`, line 233 at `a39a9c86`. Replace:

```text
   - A CHANGELOG "Known limitation:" bullet
```

with:

```text
   - A `Known limitation:` paragraph in the commit body (it becomes the release's CHANGELOG entry)
```

**7.29.** `.claude/skills/finish-branch/SKILL.md`, line 3 at `a39a9c86` (part of the line). Replace:

```text
regenerate backlinks, CHANGELOG, delete branch
```

with:

```text
regenerate backlinks, delete branch
```

This is the YAML `description:`; keep it unquoted, one line.

**7.30.** `.claude/skills/finish-branch/SKILL.md`, lines 33-36 at `a39a9c86`. Replace:

```text
### 4. CHANGELOG
- If the merged branch didn't already include a CHANGELOG entry, add one under today's date summarizing the landed work. Commit.

### 5. Delete the merged branch
```

with:

```text
### 4. Delete the merged branch
```

This deletes the old step 4 with its blank line and renumbers step 5 to 4.

**7.31.** `.claude/skills/finish-branch/SKILL.md`, line 40 at `a39a9c86`. Replace:

```text
### 6. Push the trunk
```

with:

```text
### 5. Push the trunk
```

**7.32.** `.claude/skills/finish-branch/SKILL.md`, line 48 at `a39a9c86`, plus the start of line 49.
Replace:

```text
- **CHANGELOG hook:** if the branch touched `.claude/`, `CLAUDE.md`, or `AGENTS.md`, the pre-commit hook (`check-changelog-changed.sh`) requires CHANGELOG.md in the post-merge commit set — usually already satisfied by the branch's own CHANGELOG entry.
- **Push is shared state:**
```

with:

```text
- **Push is shared state:**
```

Check: `grep -c '^[[:space:]]*$' .claude/skills/finish-branch/SKILL.md` prints one less than
`git -C E:/repos/taom-improve/wt-020 show a39a9c86:.claude/skills/finish-branch/SKILL.md | grep -c '^[[:space:]]*$'`
(7.30 removes one blank line; 7.32 removes none).

**7.33.** `.claude/skills/finish-branch/SKILL.md`, line 55 at `a39a9c86` (part of the line). Replace:

```text
the push guard referenced in step 6.
```

with:

```text
the push guard referenced in step 5.
```

**7.34.** `.claude/skills/author-armor/SKILL.md`, line 37 at `a39a9c86` (part of the line). Replace:

```text
otherwise `/verify` + CHANGELOG + issue.
```

with:

```text
otherwise `/verify` + issue, with the commit body as the changelog entry.
```

**7.35.** `.claude/skills/verify-bindings/SKILL.md`, line 74 at `a39a9c86` (part of the line). Replace:

```text
Update `docs/migration/TRACKING.md` and `CHANGELOG.md` if bindings were fixed.
```

with:

```text
Update `docs/migration/TRACKING.md` if bindings were fixed, and say what changed in the commit body.
```

**7.36.** `.claude/skills/lint-cleanup-loop/SKILL.md`, line 126 at `a39a9c86`. Replace:

```text
- Don't update CHANGELOG mid-loop — wait until the branch is ready and let the user write one batch entry.
```

with:

```text
- Don't edit CHANGELOG.md: `/release` generates it from commit bodies, so each commit body in the loop is the entry.
```

**7.37.** `.claude/skills/scope-check/SKILL.md`, line 15 at `a39a9c86`. Replace:

```text
1. **Read recent CHANGELOG entries** — Check `CHANGELOG.md` for the last 2-3 dated sections to understand recent work themes and feature areas.
```

with:

```text
1. **Read recent commit bodies**: run `git log -5 --format='%h %s%n%n%b'` to understand recent work themes and feature areas (the commit body is the changelog entry; `CHANGELOG.md` changes only at a release).
```

**7.38.** `.claude/skills/new-adr/SKILL.md`, line 3 at `a39a9c86` (part of the line). Replace:

```text
pre-filled from recent git history and CHANGELOG.
```

with:

```text
pre-filled from recent git history.
```

This is the YAML `description:`; keep it unquoted, one line.

**7.39.** `.claude/skills/new-adr/SKILL.md`, lines 36-37 at `a39a9c86`. Replace:

```text
# Latest CHANGELOG entry (first 30 lines)
head -30 CHANGELOG.md
```

with:

```text
# Latest commit bodies (the changelog entries since the last release)
git log -3 --format='%s%n%n%b'
```

**7.40.** `.claude/skills/new-adr/SKILL.md`, line 60 at `a39a9c86` (part of the line). Replace:

```text
git log themes, CHANGELOG entry, changed files.
```

with:

```text
git log themes, recent commit bodies, changed files.
```

**7.41.** `.claude/skills/improve/SKILL.md`, line 36 at `a39a9c86` (part of the line). Replace:

```text
recent `CHANGELOG.md` + `git log --oneline -30` (what's
```

with:

```text
recent commit bodies + `git log --oneline -30` (what's
```

**7.42.** `.claude/skills/armory-audit/SKILL.md`, line 57 at `a39a9c86` (part of the line). Replace:

```text
say in the CHANGELOG
```

with:

```text
say in the commit body
```

**Verify** (all must hold):

1. Write this list with the Write tool to `E:/repos/taom-improve/scratch/020-old-phrases.txt`, one
   phrase per line, exactly as shown (each was confirmed to match at `a39a9c86`):

```text
CHANGELOG.md is updated every session
then issue, docs, CHANGELOG
Update relevant docs and the changelog
test evidence and the changelog
feature doc, index and changelog
The pre-commit hook only enforces CHANGELOG
13. Update CHANGELOG.md
update docs + CHANGELOG
10. Update `CHANGELOG.md`
check-changelog-changed
check-changelog-updated
Update CHANGELOG.md
7. CHANGELOG
say so in the CHANGELOG
in the PR or CHANGELOG
(commit, CHANGELOG, log)
CHANGELOG Check
CHANGELOG Updated
regenerate backlinks, CHANGELOG
### 4. CHANGELOG
`/verify` + CHANGELOG + issue
and `CHANGELOG.md` if bindings
Don't update CHANGELOG mid-loop
Read recent CHANGELOG entries
head -30 CHANGELOG.md
git history and CHANGELOG
CHANGELOG entry, changed files
recent `CHANGELOG.md`
say in the CHANGELOG
A CHANGELOG "Known limitation:" bullet
re-read a shared file (`CHANGELOG.md`)
+ `CHANGELOG.md` |
`taom_lord_skill_sets.xml`, `CHANGELOG.md`
CHANGELOG.md updated.
PR descriptions and CHANGELOG
| **CHANGELOG** |
closeout (issue, feature doc, CHANGELOG)
Latest CHANGELOG
changelog-staged
CHANGELOG summary on startup
5. CHANGELOG entry.
Source the content from CHANGELOG entries
## Phase 5 — CHANGELOG
```

   Then run
   `git -C E:/repos/taom-improve/wt-020 grep -n -F -f E:/repos/taom-improve/scratch/020-old-phrases.txt -- AGENTS.md CLAUDE.md README.md .ai .agents .claude .serena docs/ai-includes docs/reference/hooks-catalog.md docs/reference/release-process.md`.
   Expected: exactly one line, starting `.claude/rules/hook-authoring.md:59:` (a historical record,
   out of scope). The `Source the content ...` and `## Phase 5 — CHANGELOG` phrases disappear in
   Step 8, so until then also expect `.claude/skills/release/SKILL.md:73:` and `:76:`.
2. `git -C E:/repos/taom-improve/wt-020 diff --name-only` lists exactly the 29 files edited in this
   step (items 7.1 to 7.42 touch 29 files).
3. `bash tools/test_hooks.sh 2>&1 | tail -8`: same summary as `020-after5-hooks.txt` (section 3b
   parses every skill's frontmatter, so a broken `description:` fails here).
4. `python tools/reviewctl.py lint; echo "exit $?"` prints `exit 0`, and
   `wc -c < AGENTS.md` prints less than `8192`.
5. `python tools/lint_docs.py --fail-on-drift > /dev/null 2>&1; echo "exit $?"` prints the same
   line as `020-base-drift.txt`, and
   `python tools/lint_docs.py --summary > E:/repos/taom-improve/scratch/020-after7-lint.txt 2>&1; diff E:/repos/taom-improve/scratch/020-base-lint.txt E:/repos/taom-improve/scratch/020-after7-lint.txt`
   shows no count that went up.
6. Write this script with the Write tool to `E:/repos/taom-improve/scratch/020-dash-check.py`:

```python
"""List changed files (against a39a9c86) that gained an em or en dash. Expected output: []"""
import os
import subprocess
import sys

repo = sys.argv[1] if len(sys.argv) > 1 else "."
EM, EN = chr(0x2014), chr(0x2013)


def dashes(text):
    return text.count(EM) + text.count(EN)


names = subprocess.run(["git", "-C", repo, "diff", "--name-only", "a39a9c86"],
                       capture_output=True, encoding="utf-8").stdout.split()
bad = []
for name in names:
    path = os.path.join(repo, name)
    if not os.path.exists(path) or name.startswith("docs/changelog-archive/"):
        continue
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        new = fh.read()
    old = subprocess.run(["git", "-C", repo, "show", "a39a9c86:" + name], capture_output=True,
                         encoding="utf-8", errors="replace").stdout
    if dashes(new) > dashes(old):
        bad.append(name)
print(bad)
```

   Then `python E:/repos/taom-improve/scratch/020-dash-check.py E:/repos/taom-improve/wt-020`
   prints `[]` (no changed file gained an em or en dash; lines you did not rewrite keep theirs).

### Step 8: Point /release at the generator

In `.claude/skills/release/SKILL.md`, replace lines 66-78 (from `## Phase 4 — Release note` through
`Entry under today's date. Mandatory (AGENTS.md "Documentation duty").`) with exactly:

````markdown
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
(`N commits, L with the version label, U without`) and read the unlabelled group before writing the
release note. Exit 2 means it refused: a section for vX.Y.Z already exists, or someone hand-wrote a
section above the releases. Show the user the message; never delete a hand-written section without
their OK.

## Phase 5: release note

`docs/releases/vX.Y.Z-discord.md`, following `docs/releases/v2.0.15-discord.md`: emoji section
headers, player-facing framing (what changed for them, not which class was refactored), and an
explicit ⚠️ line whenever MCM-persisted settings mean **existing players keep old values** and must
reset them by hand.

Source the content from the section Phase 4 just wrote into `CHANGELOG.md`.
````

Then, in the same file, name what the release commit stages (Phase 6 named no paths, so a release
could leave the generated `CHANGELOG.md` behind). Replace lines 82-83 at `a39a9c86`:

```text
Stage **explicitly** — `git add <paths>`, never `-A`. A shared file routinely holds two sessions'
edits.
```

with:

```text
Stage **explicitly** with `git add <paths>`, never `-A`: the Phase 3 version files, `CHANGELOG.md`
(Phase 4) and the release note (Phase 5). A shared file routinely holds two sessions' edits.
```

Phases 1 to 3, the rest of 6, and 7 stay as they are.

**Verify**: re-run the Step 7 phrase grep; it now prints exactly the one
`.claude/rules/hook-authoring.md:59:` line. `grep -n "^## Phase" .claude/skills/release/SKILL.md`
prints seven headings, numbered 1 to 7 in order, Phase 4 `generate the CHANGELOG section` and
Phase 5 `release note`. Then commit 3: stage the 29 files from Step 7 plus
`.claude/skills/release/SKILL.md` (30 paths, listed by `git -C E:/repos/taom-improve/wt-020 diff --name-only`)
and commit with subject `docs(workflow): v2.0.30 - the commit body is the changelog entry`. The
body names the new AGENTS.md rule, the `/release` Phase 4, the `/verify` and completeness-lens
check that flags a hand edit, and what was deliberately left (history, prose mentions,
`git-and-commits.md`). `git -C E:/repos/taom-improve/wt-020 show --stat HEAD` lists 30 files;
``grep -c -F 'never `-A`: the Phase 3 version files' .claude/skills/release/SKILL.md`` prints `1`;
BODYCHECK printed `0 0` on the message file before you committed.

### Step 9: Archive the hand-written CHANGELOG and write the new header

1. `git -C E:/repos/taom-improve/wt-020 mv CHANGELOG.md docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`
   (do not edit the moved file at all: the index keeps the exact `a39a9c86` blob, BOM and old
   header included; the CRLF working copy is normalized back to that blob by git).
2. Create `CHANGELOG.md` with the Write tool, exactly this content (no BOM, ending in one newline):

```markdown
# CHANGELOG: TAOM (Tales From the Age of Men)

> **Generated at each release; do not edit by hand.** `/release` runs
> `python tools/changelog_from_commits.py --version vX.Y.Z --write`, which adds one section per
> release from the subjects and bodies of every commit since the previous release tag. The commit
> body is the changelog entry, so write it for a reader of the release note. Between releases,
> `git log <last tag>..HEAD` is what changed.
>
> **Archive:** the hand-written entries from 2026-07-01 up to the switch to generation are in
> [`docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`](docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md);
> older ones are in [`docs/changelog-archive/CHANGELOG-2026-H1.md`](docs/changelog-archive/CHANGELOG-2026-H1.md).
```

3. `git -C E:/repos/taom-improve/wt-020 add CHANGELOG.md`

**Verify**:

1. `git -C E:/repos/taom-improve/wt-020 rev-parse :docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`
   equals `git -C E:/repos/taom-improve/wt-020 rev-parse a39a9c86:CHANGELOG.md` (the archive is the
   exact `a39a9c86` blob).
2. `grep -c '^## ' CHANGELOG.md` prints `0` and `wc -l < CHANGELOG.md` prints `11`.
3. Dry run of the first real release against the new header, without writing:
   `python -c "import sys; sys.path.insert(0,'tools'); import changelog_from_commits as c; t=open('CHANGELOG.md',encoding='utf-8').read(); s=c.render_section('v2.0.31','2026-10-01','v2.0.30',c.parse_log('a'*40+'\x1ffix(x): v2.0.30 - y\x1f\n\x1e\n')); o=c.insert_section(t,s,'v2.0.31'); print(o.count('## v2.0.31'))"`
   prints `1`.
4. `python tools/lint_docs.py --fail-on-drift > /dev/null 2>&1; echo "exit $?"` prints the same
   line as `E:/repos/taom-improve/scratch/020-base-drift.txt`.

### Step 10: Commit the archive

Stage nothing new (both paths are staged by Steps 9.1 and 9.3) and commit with subject
`chore(changelog): v2.0.30 - archive the hand-written CHANGELOG`. The body says the file moved
unchanged (same blob) to `docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`, that the new
`CHANGELOG.md` is a header until the next `/release` writes the first generated section, and that
the orchestrator re-cuts the archive from the trunk at merge time (merge recipe in the plan).

**Verify**: `git -C E:/repos/taom-improve/wt-020 show --stat -M HEAD` lists `CHANGELOG.md` and
`docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md` only; STATUS prints nothing;
BODYCHECK printed `0 0` on the message file before you committed; `git -C E:/repos/taom-improve/wt-020 log --oneline a39a9c86..HEAD`
prints four commits. This is the last commit: add no CHANGELOG entry and make no further commit,
whatever a wrapper prompt asks (Executor instructions).

### Step 11: Final regression pass

Run, from the worktree: `python tools/tests/test_changelog_from_commits.py`;
`python -m unittest discover -s tools/tests -t . 2>&1 | tail -5`; `bash tools/test_hooks.sh 2>&1 | tail -8`;
`python tools/lint_docs.py --fail-on-drift; echo "exit $?"`; `python tools/reviewctl.py lint; echo "exit $?"`;
`python tools/audit_claude_config.py --min HIGH`;
`TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`;
`git -C E:/repos/taom-improve/wt-020 log --format=%B a39a9c86..HEAD | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(sum(len(l) > 72 for l in t.splitlines()), t.count('\u2014') + t.count('\u2013'))"`.

**Verify**: every result matches "Done criteria". The dotnet run is insurance (no C# file
changed; it builds in the worktree's own `obj/`, so it does not collide with the main tree). The
baseline at `a39a9c86` is 10,313+ tests pass, 2 skipped, 0 failed, and any failure is yours to
explain: a failure also present in `020-base-dotnet.txt` is pre-existing (name it in the report
and carry on). For any other failure, re-run it once with the filtered command
(`--filter "FullyQualifiedName~<ClassName>"`); if it still fails, STOP and report the test name,
its message, and whether it reads any path in `git diff --name-only a39a9c86..HEAD`.

## Test plan

- New file `tools/tests/test_changelog_from_commits.py`, 22 tests in four classes:
  - `ParseLogTests` (5): type, scope, version and description read from a labelled subject; a
    subject with no scope; subjects without the label (an IDE subject, and `feat: ... (#629)` with a
    type but no version) come back with `type None`; a multi-paragraph body with a `Not-tested:`
    trailer kept verbatim; log order kept and blank records skipped.
  - `RenderSectionTests` (6): heading `## vX.Y.Z (date)` and the count line; known types in the
    fixed order (Features, Fixes, ..., Documentation) then other types (`diag`) alphabetically;
    unlabelled commits last in `### Commits without the version label`; each entry is `#### subject`,
    backticked 8-character SHA, body; log order inside a group; empty groups omitted, one trailing
    newline.
  - `InsertSectionTests` (6): above the newest release; appended after a header-only file; refuses a
    version already present; refuses a hand-written `## 2026-10-02` section above the releases; does
    not mistake `v2.0.310` for `v2.0.31`; keeps CRLF line endings.
  - `EndToEndTests` (5, a real temp git repository with a `v2.0.30` tag): `--write` from the previous
    tag (the tagged release commit excluded, the unlabelled IDE commit included, stderr summary);
    a second `--write` of the same version exits 2 and leaves the file byte-identical; without
    `--write` the section goes to stdout and the file is untouched; a malformed `--version` exits 2;
    an empty range exits 2.
- Structural models: `tools/tests/test_package_release.py` (sys.path insert, subprocess of the tool),
  `tools/tests/test_reviewctl.py` (fixture git repository).
- Real-history smoke (Step 3): `v2.0.29..v2.0.30` gives 57 commits, 53 labelled, 4 not.
- Not testable here, for the `Not-tested:` trailer: a live `/release` run (the next real release is
  the first); the harness reloading `settings.json` without the two hooks (only a session restart
  after the merge shows it).

## Done criteria

ALL must hold, run from the worktree:

- [ ] `python tools/tests/test_changelog_from_commits.py` prints `Ran 22 tests` and `OK`.
- [ ] `python -m unittest discover -s tools/tests -t .` ran the Step 1 count plus 22, with no failure or error the baseline lacked.
- [ ] The Step 3 smoke prints `changelog_from_commits: v2.0.29..v2.0.30: 57 commits, 53 with the version label, 4 without`.
- [ ] The Step 7 phrase grep prints exactly one line, `.claude/rules/hook-authoring.md:59:...`.
- [ ] `git ls-files .claude/hooks | grep -c '\.sh$'` prints `27`; `grep -c check-changelog .claude/settings.json` prints `0`.
- [ ] `bash tools/test_hooks.sh` exits 0 (or shows exactly the Step 1 baseline failures).
- [ ] `python tools/lint_docs.py --fail-on-drift` exits as in Step 1 (0 expected); `python tools/reviewctl.py lint` exits 0.
- [ ] `git rev-parse HEAD:docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md` equals `git rev-parse a39a9c86:CHANGELOG.md`.
- [ ] `git diff --name-only a39a9c86..HEAD -- '*.cs' '*.csproj' '*.props' Main TAOM.Tests` prints nothing.
- [ ] `git diff --name-only a39a9c86..HEAD | wc -l` prints `45`, all of them in-scope paths (3 new files, 2 deleted hooks, 40 modified files; the archive counts as a new file).
- [ ] `git log --oneline a39a9c86..HEAD` shows exactly the four commits of "Git workflow" (no fifth, no CHANGELOG entry commit), none with an AI trailer (`git log -4 --format=%B | grep -ci "co-authored-by\|generated with"` prints `0`).
- [ ] The Step 11 body check over `a39a9c86..HEAD` prints `0 0` (no message line over 72 characters, no em or en dash).
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows 0 failed, or only failures already in `020-base-dotnet.txt` (baseline at `a39a9c86`: 10,313+ tests pass, 2 skipped, 0 failed).
- [ ] STATUS prints nothing (the untracked plan copy, if present, is filtered out); nothing was written under `E:/repos/TAOM`.

## STOP conditions

Stop and report (do not improvise) if:

- Step 1 items 2 to 4 fail: Step 0 is missing or differs. Never edit `.claude/settings.json`
  yourself, never create a config-protection override file, never rewrite it through a shell.- A commit is denied by a live hook in `E:/repos/TAOM` (`[check-changelog-changed]`,
  `[check-commit-subject-version]`, `[check-claude-files-tracked]` or any other). The live hooks read
  the main tree, not your worktree. Report the exact message. Never use `--no-verify`, never stage
  `CHANGELOG.md` to satisfy a gate, never edit a hook under `E:/repos/TAOM`.
- The worktree's `Main/_Module/SubModule.xml` version differs from the version the live subject hook
  demands.
- The Step 3 smoke numbers differ from 57, 53 and 4, or the group order differs (history or tags
  differ from what this plan measured).
- Any block to replace in Steps 5, 7 or 8 is not found exactly once in its file (the worktree is not
  at `a39a9c86`, or the plan is wrong about a line).
- After Step 7 the phrase grep prints a line in a file outside the in-scope list, or a line this
  plan does not name: report it; do not widen the scope.
- `bash tools/test_hooks.sh`, `python tools/lint_docs.py --fail-on-drift` or
  `python tools/reviewctl.py lint` shows a failure the Step 1 baseline lacked, twice after a
  reasonable fix inside the in-scope files.
- The archive blob check in Step 9 fails (the move changed bytes).
- Any step seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
  `Directory.Build.props`, an ADR, `docs/ai-includes/git-and-commits.md`,
  `.claude/skills/new-creature-mount/SKILL.md` or `plans/README.md`.
- You discover that something other than the files named in "Other consumers of the file" reads
  `CHANGELOG.md` (a tool, a CI step, a test): the assumption "no program parses the hand-written
  file" is false.

## Maintenance notes

- **Merge recipe (orchestrator, not the executor).** This branch replaces `CHANGELOG.md` whole, so
  it must merge last, after every `improve/*` branch that adds an entry (001, 002, 003, 005, 006,
  007, 008, 009, 010, 012, 013, 014, 015, 018, 019, 023 and 024 each touch `CHANGELOG.md` at their
  current tips).
  1. Precondition: in `E:\repos\TAOM`, `git status --porcelain -- CHANGELOG.md` prints nothing. At
     planning time it printed `MM CHANGELOG.md` (another session's uncommitted entries); those must
     be committed first, or they are lost from the archive. `git merge` refuses to run over a
     locally modified file anyway.
  2. `git merge --no-ff --no-commit improve/020-changelog-at-release` (so `HEAD` stays the trunk tip
     whether or not git reports a conflict on `CHANGELOG.md`). Then, in Git Bash and byte-faithfully
     (never through PowerShell redirection), in both cases:
     `git show HEAD:CHANGELOG.md > docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`,
     `git show MERGE_HEAD:CHANGELOG.md > CHANGELOG.md`,
     `git add CHANGELOG.md docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`, then check
     `test "$(git hash-object docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md)" = "$(git rev-parse HEAD:CHANGELOG.md)" && echo archive-ok`.
     This writes an exact committed blob, not a reconstruction, which is why precondition 1 matters
     (`git-and-commits.md:60`). Resolve the other paths (items 3 to 5), then `git commit`.
  3. If plan 013 (`improve/013-bash-hook-prefilter`) merged first: `check-changelog-changed.sh` is a
     modify/delete conflict (resolve with `git rm`), `check-moduledata-validation.sh` may conflict
     next to line 35 (keep 013's prefilter and this plan's comment), and 013's
     `tools/test_hooks.sh` names `check-changelog-changed.sh` in `PF_NARROWED_LIST` (line 476 on
     013's tip): remove that one word.
  4. If plan 011 merged first: `check-changelog-updated.sh` is a modify/delete conflict (`git rm`);
     `.claude/settings.json`'s `Stop` block must end with neither CHANGELOG hook; drop the converted
     hook's row from `hooks-catalog.md` and from any `tools/test_hooks.sh` list 011 added
     (011's plan loops `for name in ... check-changelog-updated.sh`). If 011 has NOT run yet,
     execute it without its Step 7 and without that script in its lists.
  5. Plans 008 and 010 edit `docs/reference/hooks-catalog.md` near line 39, next to the deleted row
     at line 38: keep their text, drop the row.
  6. On the merged tree: re-run the Step 7 phrase grep (a branch merged earlier may have added a new
     "update CHANGELOG" line), `bash tools/test_hooks.sh`, `python tools/lint_docs.py --fail-on-drift`,
     and recount `hooks-catalog.md:3` against `git ls-files .claude/hooks | grep -c '\.sh$'`.
  7. After the merge lands in `E:\repos\TAOM`, every running Claude session still holds the old
     hook registrations and will call two missing scripts until it restarts: tell Mike to restart
     sessions.
- **Follow-up dispatches on this branch**: pass the same `it.note` as the executor got.
  `x-followup.js:30` asks for "a line to the branch's CHANGELOG entry"; this branch has none by
  design, and a hand-written `## ` section in the new `CHANGELOG.md` makes the first `/release`
  exit 2. Fixes go in the follow-up commit's body.
- **What a reviewer should probe**: that each replacement line in Step 7 still reads correctly in
  its paragraph; the YAML of the three edited `description:` lines (`ship`, `finish-branch`,
  `new-adr`); that `/verify` and the completeness lens now flag a hand edit instead of demanding
  one; `insert_section`'s refusal rule (the first `## ` heading must be a release heading).
- **Between releases** "what changed" is `git log <last tag>..HEAD`, not a file. The verify pass
  found that bodies are sometimes thinner than the hand-written entry for the same commit (the
  2026-09-23 top entry added detail its commit `b2e387db` body lacks); the new AGENTS.md line asks
  for bodies written for a release-note reader. If players need prose distinct from bodies,
  fragments (`changelog.d/`) are the fallback the audit compared; not decided.
- **Unlabelled commits** (7 of the 28 since `v2.0.30`, 4 of 57 in the last release) land in their own
  group. The subject gate is a Claude Code PreToolUse hook, so it never sees a commit made outside
  Claude's shell tools; a CI or git-hook subject check is a separate follow-up.
- **Follow-ups deliberately deferred**: `.claude/skills/new-creature-mount/SKILL.md:92` (another
  session's file; replace "CHANGELOG;" with "the commit body;"); shrinking the three incident rules
  in `docs/ai-includes/git-and-commits.md:56,60,61` into one shared-file rule; Mike's private
  auto-memory files that repeat "update CHANGELOG every session" (outside the repo).
- **Half-year rolls**: the H1 cadence note ("each Jan 1 / Jul 1") lived in the old header and is
  archived with it. A generated file grows by one section per release; roll it when it is large,
  into `docs/changelog-archive/` like the two archives.
