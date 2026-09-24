# Plan review 017: build identity dirty flag

Reviewer: cold read against `.claude/skills/improve/references/plan-template.md` ("Quality bar"),
excerpts compared with `git show b2e387db:<path>` on 2026-09-23. Line numbers below are the plan's
(`plans/017-build-identity-dirty-flag.md`) unless a file is named.

**Verdict:** executable by a weak model once two blocking gaps are fixed. The excerpts are
accurate apart from one off-by-one in `Directory.Build.props`.

## Verified this pass

- `Directory.Build.props` lines 23-24 at `b2e387db` match (plan 75-76).
- `BuildStampReport.cs`: 206 lines; summary 18-21, `TryParseStamp` 107-126 and
  `ReadInformationalVersion` 189-205 all match (plan 148-180). The type sits in namespace
  `TAOM.Core.Diagnostics`, which holds no other type, so the new `using` cannot collide.
- `IdentitySnapshot.cs` (whole file), `IdentityCollector.cs:9-26` with its five usings,
  `CrashReportService.cs:182-183`, `PlainTextCrashReportRenderer.cs:63-70`,
  `CrashBundleWriter.cs:101-109`, `JsonCrashReportRenderer.cs:19-20` and the Rendering folder listing
  all match.
- `git grep -n "IdentitySnapshot(" b2e387db -- Main TAOM.Tests` returns exactly the four
  construction sites in the table at 206-211, plus the record declaration.
- The test anchors are correct: PlainText line 10 `{`, test ending at 38, helper at 197;
  CrashBundleWriter line 19 `{`, `_dir` at 20, test ending at 60, helper at 128;
  BuildStampReportTests test ending at 50. Baseline counts are 16, 11 and 5, so 36 after the change
  is right.
- `InternalsVisibleTo` `TAOM.Tests` is in `Main/TAOM.csproj`.
- `tools/package_release.py`: every quoted line and range matches (397 lines, imports 36-45, 54,
  100, 187-202, 299-393, 311, 334, 336-345, 347, 371, 375-383, docstring 28-34).
  `plan_module` sets `name=module_root.name`.
- `tools/tests/test_package_release.py`: 403 lines, 33 tests, imports 25-31, `TOOL` at 37, `_run`
  at 323-326, `_write` at 259, main block at 402-403. `python tools/tests/test_package_release.py`
  prints `Ran 33 tests` and `OK` on the main tree, where this file is unmodified.
- `.claude/skills/release/SKILL.md` (117 lines, Phase 2 at 32-35, Phase 7 fence at 104, Gotchas
  at 106, bullet at 114-116, no mention of packaging), `docs/reference/release-process.md`
  (59-63, 69, 83, 99-110) and `docs/features/crash-report.md:117` all match.
- The SDK facts hold on SDK 10.0.401. All four Step 0.3 greps hit at lines 62, 803, 34 and 70.
  `_InitializeSourceControlInformationFromSourceControlManager` is at
  `InitializeSourceControlInformation.targets:23-26`, and `EnableSourceControlManagerQueries`
  defaults to true (`Microsoft.SourceLink.Common.props:11`).
- The live DLL stamp claim holds: both main-tree DLLs read
  `build.20260924-020931Z+4b5662b22afaa90aed8ba555b9d64ecc030cbe1c`, and an ASCII scan of `TAOM.dll`
  finds exactly one match.
- The two negative greps (plan 89 and 276) return nothing (rc=1).
- The drift-check path set against `b2e387db..4b5662b2` prints nothing.
- The main-tree SKILL.md diff is exactly the pre-cleared Phase 1 item 5 (plan 32-34).
- `.gitignore:125` ignores `__pycache__/`, so the Python test runs leave `git status` clean.
- No U+2014 or U+2013 anywhere in the plan, and no secret values.

## Blocking

1. **The `config-protection.sh` hook blocks Step 5's edit, and the plan never says so (plan 71,
   391, 631-671).** `.claude/settings.json` registers `.claude/hooks/config-protection.sh` on
   `Edit|Write`. It matches on the basename `Directory.Build.props`, at any path including
   `E:/repos/wt-plan-017/`, and exits 2 with "not allowed without explicit user request ... ask
   the user first". The only escape is a user-created override file. The plan does not tell the
   executor this, the Status block does not require the orchestrator to get the maintainer's
   explicit approval, and no STOP condition covers an Edit or Write hook denial (plan 1149 covers
   commit hooks only). A weak executor that is refused will either improvise or, worse, write the
   file through Bash or PowerShell (`sed`, `Set-Content`), which escapes the Edit/Write matcher.
   That would bypass a protection gate. **Fix:**
   - Status: "Directory.Build.props is hook-protected; the orchestrator obtains the maintainer's
     explicit approval before dispatch."
   - Step 5: "if the Edit is BLOCKED by config-protection, STOP and report it verbatim; never write
     this file through Bash or PowerShell."
   - Add the matching STOP bullet.

2. **The tree-diff Done criterion is not stable while the main tree is live (plan 1101-1102,
   1132).** `git diff --name-only bannerlord-1.5.x..HEAD` is a two-dot tree diff between the
   branch tip and HEAD. The plan itself says another session works on `bannerlord-1.5.x` in the
   main tree (plan 9-12). If that session commits during the run, the list gains every path it
   changed. A correct execution then fails its own Done criterion and Step 12, and the executor
   either STOPs wrongly or "fixes" the branch. **Fix:**
   - Record `BASE=$(git rev-parse HEAD)` in Step 0 (to `step0-stamp.txt`) and use
     `git diff --name-only <BASE>..HEAD`, or the three-dot form `bannerlord-1.5.x...HEAD`.
   - Use the same base for `git log` in Step 12 and at 1133.

## Non-blocking

1. **The `Directory.Build.props` line numbers are off by one (plan 79, 633).** At `b2e387db` the
   file has 43 lines. `<PropertyGroup>` spans lines 2-42, `</PropertyGroup>` is line 42 and
   `</Project>` is line 43. The plan says 2-43, 43 and 44. The textual anchors are unambiguous,
   but the STOP at 1139 ("an excerpt ... does not match") may make a literal executor halt.
   Correct the numbers.
2. **Pre-cleared drift will shift the SKILL.md anchors (plan 32-35, 312-317, 974-998).** If the
   other session commits its SKILL.md Phase 1 item 5 before Step 0, that adds 3 lines. Phase 7's
   fence becomes 107, Gotchas 109 and the bullet 117-119. The plan pre-clears this drift but
   still gives the old line numbers in Step 11. Add "line numbers shift by +3 in that case; use
   the textual anchors."
3. **`cd` compound commands may prompt (plan 14-16).** The plan requires every Bash call to start
   with `cd /e/repos/wt-plan-017 && `. The harness notes that `cd` in a compound command can
   trigger a permission prompt, which a non-interactive executor cannot answer. Most calls could
   use `git -C` and absolute paths instead. If the `cd` convention stays, confirm with the
   orchestrator that the executor's permissions allow it and the writes under `E:/repos/wt-plan-017*`.
4. **The scratch directory convention (plan 21-22).** CLAUDE.md says scratch goes in the session
   scratchpad. The plan uses `E:/repos/wt-plan-017-scratch/`, which is outside every configured
   working directory. This works if permitted, but it is a place where permission can be refused.
5. **Deep-review before a C# commit (plan 606-613, 703).** CLAUDE.md requires `/deep-review`
   before every commit touching C#. The plan has the executor commit A and B directly and moves
   review to Maintenance notes (1158). This is defensible for a worktree branch the orchestrator
   reviews before merge, but the plan should say so explicitly: "the orchestrator runs
   /deep-review on the branch before merge; you cannot invoke skills."
6. **The test baseline is UNVERIFIED (plan 359-367, 458-462).** The 10,239 / 2-failed / 2-skipped
   numbers and the claim that they came from `b2e387db` (rather than the dirty main tree, which
   has uncommitted Elk/Animalia test edits) could not be checked without running the suite. A
   fresh worktree may fail other tests that depend on untracked or generated inputs, which would
   trip the STOP at 461-462. That STOP is safe, but it may halt a correct run.
7. **Step 0.4 and 0.5 both write `step0-stamp.txt` (plan 451-455).** One says to record the warning
   count there and the other to "write the three lines". Say "append" for one of them, or use a
   separate file.
8. **Step 9.2 names the wrong source build (plan 949-952).** It says "the real DLLs from Step 6.5",
   but Step 7's `dotnet test` rebuilt them since then. Both are clean builds of commit B, so the
   outcome is the same; only the wording is wrong.
9. **The CrashBundleWriter range is overstated (plan 257).** It reads `CrashBundleWriter.cs:101-110`,
   but the excerpt ends at line 109 (`Origin:`). Line 110 is the unshown `Exception:` line, and the
   comment at line 100 is omitted. This is harmless.
10. **Rewriting `SourceRevisionId` is global (Maintenance, plan 1158).** Any other consumer of the
    property now sees `<sha>.dirty` (for example, SourceLink/NuGet `RepositoryCommit` if a project
    is ever packed). This does not matter today, since nothing is packed, but it belongs in the
    reviewer focus list.
11. **One stale comment remains.** The existing test comment at `BuildStampReportTests.cs:25-29`
    still says Bannerlord.BuildResources appends the SHA, which the plan corrects only in the
    production summary. This is optional, and the file is in scope.

## Checklist

| Check | Result |
|---|---|
| Self-contained for a weak model | Yes, except for the hook-protected edit (Blocking 1) and the permission conventions (NB 3, 4) |
| Every step ends in a command with an expected result | Yes. Step 4 and 5 verifies are DLL reads with exact expected suffixes. The Step 11 lint check needs a light judgment on pre-existing findings |
| Excerpts match `b2e387db` | Yes, except the props line numbers (NB 1) and the 101-110 range (NB 9) |
| TDD order | RED 1 before GREEN 2 (C#); RED 4 (build observation) before GREEN 5; RED 8 before GREEN 9 (Python). Expected RED counts are correct: CS1729 plus CS0122, and `failures=4, errors=9` over 46 |
| Issue-first | Status: "create before implementation lands (orchestrator)" |
| Binding ADRs named | ADR-002, 003/004/005, 007, 008 and 009, each with a one-line summary |
| Single-owner files | `SubModule.cs`, `IoC.cs`, `TAOM.csproj` and the Dependencies csproj are out of scope with a STOP. `Directory.Build.props` is in scope but its hook is not handled (Blocking 1) |
| STOP conditions specific | Yes (SDK hooks, clean-worktree precondition, `+nogit` shape, metadata encoding, warnings). Missing: an Edit-hook denial |
| Done criteria machine-checkable | Yes, but the tree-diff item is unstable (Blocking 2) |
| Planned-at SHA and drift paths versus Scope | `b2e387db`. The drift check covers 15 of the 16 in-scope paths and excludes CHANGELOG.md with a stated reason |
| Non-deploying commands | Every `dotnet build` and `dotnet test` carries `-p:DisableModuleCopy=true -p:ModuleId=`, including Step 6.2 |
| No em or en dash; no secrets | Clean |
