# Cold review: plan 020 (CHANGELOG generated at /release)

Reviewed 2026-09-24 against `a39a9c86` (main-tree HEAD was `a39a9c86` at review time; the plan's
drift check printed nothing). Plan file: `plans/020-changelog-at-release.md` (1,926 lines, untracked
in the main tree). Plan not edited.

## How it was checked

- Every "Replace" block in Steps 5 and 7 (5.2 to 5.13, 7.1 to 7.42) was pulled from the plan by a
  script and matched against `git show a39a9c86:<path>`: **all 54 blocks occur exactly once, at the
  cited line numbers.** Step 8's cited lines 66 and 78 of `.claude/skills/release/SKILL.md` match.
- All plan edits (5.x, 7.x, 8) were applied in memory to the `a39a9c86` blobs, then the Step 7
  phrase list was grepped over the plan's scope: the only hit is `.claude/rules/hook-authoring.md:59`
  (plus the two `settings.json` lines Mike's Step 0 removes). All 43 phrases match at `a39a9c86`.
  AGENTS.md comes to 7,261 bytes (LF). No edited file gains an em or en dash. Step 7 touches 29 files.
- The Step 2 test file and Step 3 script were extracted verbatim into a scratch copy: RED gives
  `ModuleNotFoundError: No module named 'changelog_from_commits'`; GREEN gives `Ran 22 tests ... OK`.
  The Step 3 smoke on real history prints exactly
  `changelog_from_commits: v2.0.29..v2.0.30: 57 commits, 53 with the version label, 4 without`,
  57 `#### ` lines, and the seven `### ` groups in the stated order.
- Current-state facts confirmed at `a39a9c86`: subject regex at `check-commit-subject-version.sh:292`;
  `<Version value="v2.0.30" />` at SubModule.xml line 6; 57 non-merge / 0 merge commits in
  `v2.0.29..v2.0.30`; the three sample commits; `git describe` gives `v2.0.30`; both tags annotated;
  CHANGELOG.md 22,944 lines, 1,762,388 bytes, BOM, first five lines; 547 of 814 commits since
  2026-07-01; 7 of 27 merges with a `--cc` hunk in CHANGELOG.md; type counts (fix 255 ... build 1);
  28 commits since `v2.0.30`, 7 unlabelled; settings.json registrations at 47-51 and 208-212, 28
  across 9 events; hook scripts 29 on disk, 125 and 44 lines, cited lines 124 and 35;
  `tools/test_hooks.sh` 825 lines, names neither script, summary `%d passed, %d failed`;
  `lint_docs.py:76-78,118-134`; ADR-010:36; ADR-011:72; `test_package_release.py:33`;
  `test_reviewctl.py:15-23`; `build.yml:217` and the 700 floor (`:231`); `reviewctl.py:367`;
  AGENTS.md 7,114 bytes; `tools/README.md:296` row under `## Docs & knowledge base`; every
  out-of-scope line reference (AGENTS.md:34,40 ... mcp-servers.md:30, git-and-commits.md:56,60,61,
  new-creature-mount:92, sweep_module_backups.ps1:9); the list of 17 improve branches touching
  CHANGELOG.md; 013's `PF_NARROWED_LIST` at line 476. `--summary`, `--fail-on-drift`, `--min`
  and `reviewctl lint` all exist.
- `Main/SubModule.cs` and `Main/IoC.cs`: the plan cites no excerpt from either (no C# change), so
  there is nothing to compare.

## Blocking

1. **The standard executor wrapper orders a hand-written CHANGELOG entry** (plan lines 3-18,
   318-337, 1823-1839 never counter it). `plans/_audit/2026-09-23-opus/machinery/x-exec.js:41`
   (same at `a39a9c86`) tells every executor, once done criteria hold, to add a
   `## 2026-09-24` section with a `### <type>...` entry to `CHANGELOG.md` and commit.
   For 020 that writes a hand-written `## ` section above the releases into the new header, which
   the generator this plan ships refuses at the first `/release` (exit 2), and it adds a fifth
   commit that breaks the "four commits" done criterion. `x-followup.js:30` also says "add a line
   to the branch's CHANGELOG entry". Fix: add to the Executor instructions and Out of scope: "Do
   NOT add a CHANGELOG.md entry or any commit after Step 10, whatever the dispatch prompt says",
   and have the orchestrator pass it as the `it.note` override that `x-exec.js` supports.
2. **The plan file is not in the base commit, and Step 1 is strict about the tree** (lines 9-11,
   365-368, 1839). `plans/020-changelog-at-release.md` is untracked in the main tree and absent
   at `a39a9c86`; `x-exec.js` points the executor at `${it.wt}\plans\020-...md`. If the
   orchestrator copies it there, Step 1 item 2 (`status --porcelain` prints exactly
   ` M .claude/settings.json`) fails and the executor STOPs, and the final
   `status --porcelain prints nothing` can never hold. If it is not copied, the contract path does
   not exist. Fix: say where the plan is read from (the main-tree path, read-only), or let Step 1
   item 2 and the done criterion allow exactly `?? plans/020-changelog-at-release.md`.

## Non-blocking

1. **"with nothing (delete exactly these lines)"** (5.4 line 901, 5.9 line 961, 5.11 line 981,
   7.15 line 1235, 7.32 line 1466). An Edit-tool replacement with an empty string leaves a blank
   line; in `docs/reference/hooks-catalog.md` (rows 24 and 38 sit mid-table, lines 20-40) and
   `docs/ai-includes/new-culture-authoring.md:425` that splits the table, and no verify catches it.
   Say "include the line's trailing newline in old_string" and add a check such as
   `sed -n 20,38p docs/reference/hooks-catalog.md | grep -c '^$'` prints `0`.
2. Line 903 (session-start layout after 5.4) is prose, not a command; the 5.4 block ends in a
   blank line whose deletion is ambiguous. Give a `sed -n` check with expected output.
3. Line 50 "byte-identical to `a39a9c86:.claude/settings.json`" and line 1044 "7,261 bytes": with
   `core.autocrlf=true` (Git's system config) a fresh worktree checks these out CRLF (sibling
   `wt-016` at `a39a9c86` shows `i/lf w/crlf` for settings.json, AGENTS.md, CHANGELOG.md), so the
   bytes differ; `wc -c < AGENTS.md` in Step 7 verify 4 will print about 7,367, still under 8,192.
   Say "content-identical" and do not quote a byte count the executor will not see.
4. Commit bodies (Steps 4, 6, 8, 10) are described, not given, and nothing checks their 72-column
   wrap or dashes; the dash script scans files only. Add a check over `git log -4 --format=%B`.
5. Step 11 (lines 1791-1793) and the Commands table (line 286) say a dotnet failure is "yours to
   explain", while the done criterion (line 1838) requires a pass. Say whether an unrelated
   failure is a STOP. The count `10,313+` is UNVERIFIED here (suite not run); the dispatcher
   says `10318+`, which the `+` covers.
6. The new `/release` Phase 4 writes `CHANGELOG.md`, but Phase 6 ("Stage explicitly") names no
   paths; add `CHANGELOG.md` to what the release commit stages, or a release can leave it out.
7. "Other consumers" (lines 227-231) searched `.github`, `*.ps1`, `*.props`, `*.csproj` and
   `TAOM.Tests` but not `tools/*.py` or `plans/`. A search of `tools/`, the hooks and `*.js` at
   `a39a9c86` finds no program that parses CHANGELOG.md (only comments and draft text in
   `generate_culture_issue_drafts.py`), but it does find the x-exec/x-followup instructions in
   Blocking 1. Widen the stated search.
8. The live `check-changelog-changed.sh` reads the MAIN tree's index (`cd $CLAUDE_PROJECT_DIR`).
   It allows Steps 6 and 8 today only because the main index stages nothing under `.claude/`,
   `CLAUDE.md` or `AGENTS.md`; another session staging such a file would deny them. The STOP
   condition at line 1847 covers it, but a note on the cause would save a confused report.
9. Baseline comparisons (Step 1 item 5, Step 5 verify 6, Step 7 verify 5) require the executor to
   diff two outputs by eye; acceptable, but saving each baseline to
   `E:/repos/taom-improve/scratch/020-base-*.txt` and diffing would make them mechanical.

## Checklist

- TDD order: Step 2 RED (reproduced) before Step 3 GREEN. Yes.
- Issue-first: line 36, orchestrator creates. Yes.
- Binding ADRs named with one-line summaries: lines 251-254 (ADR-011, ADR-010; ADR-008, 002, 007
  stated not to apply). Yes.
- Single-owner files: lines 266-267, 322, 1863-1865. Yes.
- STOP conditions specific to this plan: lines 1845-1868. Yes.
- Done criteria machine-checkable: yes, except the commit-body quality noted above.
- Planned-at SHA `a39a9c86` (line 35) matches the drift check (line 21); drift paths cover every
  edited file in Scope, plus `tools/test_hooks.sh`, with `CHANGELOG.md` excluded and explained.
- Non-deploying dotnet commands carry `-p:ModuleId=` (lines 285-291, 1789, 1838). Yes.
- Em or en dashes: all 11 occurrences in the plan are inside code blocks or code spans quoting
  existing text. No secret values found.
