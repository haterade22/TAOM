# Cold review: plan 025 (delete unreachable scaffolds)

Reviewed file: `plans/025-delete-unreachable-scaffolds.md` (735 lines), against the template
`.claude/skills/improve/references/plan-template.md` ("Quality bar"). Every code and doc excerpt
was compared with `git show a39a9c86:<path>`. Line numbers below are the plan's unless a file is named.

**Verdict:** one blocking defect (command working directory), otherwise executable. The plan's
factual claims hold at `a39a9c86`. The mismatches are only in excerpt labels and in one expected output format.

## What I verified (evidence read this turn)

- `git rev-parse --short bannerlord-1.5.x` is `a39a9c86`, and the plan's drift check (line 31) prints nothing.
- Line counts at `a39a9c86` match the table (lines 63-80): 29, 16, 8, 16, 42, 160, 50 (the source
  total is 292), then 62, 92, 169, 83 (the test total is 406), 30, 62, 301, 163, 134, 240.
- The history claim (line 84) holds: `git log` over the three deleted paths prints only `6a80bac6 2026-05-12 feat(EditorCacheRebuild): ...`.
- Every Step 1 grep (lines 417-430) gives the stated result at `a39a9c86`. `IEditorSceneAdapter` has a single hit, `:5`. The type-name grep finds the 14 listed files. `EnablePathReuse`/`EnablePersistentPathCache` have 4 hits (config `:43`, `:46`; tests `:106`, `:107`). There are no `_Module`/`tools` hits. `EditorCacheRebuild.Caching` appears only in the 10 Caching files and `EditorCacheRebuildIoC.cs:2`. The per-file test counts are 4/6/8/8, with no `DataRow` and no `Inconclusive` in the deleted tests.
- `Main/IoC.cs:160` is `EditorCacheRebuildIoC.RegisterEditorCacheRebuildFeature(container);` (line 139).
- `EditorCacheRebuildIoC.cs:2` and `:16-17` are exactly the lines to delete, and 12 registrations remain (line 504).
- `CacheRebuildConfig.cs` lines 3-14 and 37-49 match. Deleting 42-47 leaves `CheckpointEvery`, a blank line, then the `IncrementalSpatialRadius` summary, as Step 5 says.
- `CacheRebuildConfigProvider.cs:47-48` deserializes with no settings. `Validate` is 59-123. `MissingMemberHandling`/`DefaultSettings` grep is empty. With parallelism 6 and a fake processor count of 8, the compatibility test reaches the `LogInfo("...Loaded cache_rebuild_config.json...")` branch (provider `:120`).
- `IModLogger.LogError(string)` and `LogInfo(string)` are single-parameter, so the NSubstitute calls in the new tests compile. MSTest 3.1.1 formats the failure as `Assert.IsNull failed. <message>`.
- All 7 type names in the absence test (lines 462-468) match the namespaces and type names at `a39a9c86`.
- `CacheRebuildConfigProviderTests.cs`: usings 1-7, `WriteConfig` at 53, and the test at 79-117 all match. The JSON keys are at 87-88 and the asserts at 106-107.
- `ReflectionSiteBindingTests.cs:64-66` match, the file has 48 `[DataRow(`, and the fallback is at 138-146. `GameAssemblies.cs:60-62` match.
- `reflection-sites.md:50` and `:82` match. `editor-cache-rebuild.md` lines 84, 104-105, 119, 122, 184, 208 and 210 match. The em dashes the plan cites at 84 and 119 are there.
- `CHANGELOG.md:5` is `## 2026-09-23`, and `SubModule.xml` has `<Version value="v2.0.30" />`. The subject is 67 characters, and every commit-body line (656-680) is 72 characters or fewer.
- The DLL byte scan (lines 286-290) reproduces: 51 `TaleWorlds.*.dll`, and `grep -l -a PathReuseCache` over them and every `Modules/*/bin/Win64_Shipping_Client/*.dll` finds only `Modules/TAOM/bin/Win64_Shipping_Client/TAOM.dll`.
- Decision 21 (line 49) matches `plans/_audit/2026-09-23-opus/DECISIONS.md:27` word for word.
- The branch-overlap claims in Maintenance notes (line 730) roughly match the `improve/008`, `improve/010` and `improve/006` diffs.
- **Not verified:** the baseline `10,313 or more passed, 2 skipped` (line 356). I did not run the suite.

## Blocking

1. **Commands without the worktree `cd` prefix can act on the main checkout (lines 15-16 vs. 417-430, 490, 496, 507, 562-563, 615, 617, 626-627, 655, 683-686, 705-711).**
   The header says to prefix every command with `cd E:/repos/taom-improve/wt-025 && `. But roughly half of the step commands are printed without it. These include `git rm Main/Adapters/IEditorSceneAdapter.cs` (490), `git rm -r ...Caching...` (496), `git commit -F ...` (655) and the Step 7 `git diff` dash check (617), while other commands in the same steps carry the prefix. A weak executor copies commands literally, and the Bash cwd resets between calls. If the reset lands in `E:/repos/TAOM`, `git rm` deletes tracked files in that shared tree. Worse, `git commit` there would commit that tree's index, which currently holds another session's staged hunks (`MM CHANGELOG.md`, `A docs/releases/2026-09-24-since-v2.0.28-discord.md`). That violates AGENTS.md "Preserve others' work". The dash check would also count the main tree's unrelated doc edits and fail.
   **Fix:** write the prefix into every command in Steps 1-8 and the Done criteria, or use `git -C E:/repos/taom-improve/wt-025 ...` for every git call.

## Non-blocking

1. **Hard-coded `Skipped: 2` (lines 407, 623, 696, 703).** Several tests `Assert.Inconclusive` when a path or install is missing, and a worktree may skip a different number. Step 0 gives no instruction for a baseline skip count other than 2, but Step 8 and the Done criteria require exactly 2. Record the baseline as **S0** and expect `Skipped: S0` afterwards. None of the deleted tests can skip, so S1 should equal S0.
2. **Exit codes are masked by pipes (lines 406, 480, 623).** `... | tee ... | tail -5` returns the exit code of `tail`, yet line 623 expects "exit 0". Add `set -o pipefail;`, or read `${PIPESTATUS[0]}`, or check the totals line only and say so.
3. **The Step 2 RED output may be truncated (line 480).** `tail -30` can cut the `Error Message:` block when the stack trace is long. Tee the run to `scratch/025/red.log`, then `grep -A2 "Failed RetiredPath"` it.
4. **The Step 1.6 expected output format is wrong (line 429).** `grep -c` over a glob prints `TAOM.Tests/Features/EditorCacheRebuild/Caching/NavigationPathClonerTests.cs:4`, not `NavigationPathClonerTests.cs:4`, and line 415 says to "compare with the expected output exactly". A literal-minded executor could STOP here.
5. **The absence test proves RED for one name only (lines 455-476, 732).** It fails on the first name, `IEditorSceneAdapter`, so a misspelled later name would pass vacuously forever. I checked all 7 names and they are correct at `a39a9c86`. A stronger Step 2 would first assert that all 7 names and both properties resolve, and the sweep at Step 8.4 partly covers this. Separately, a permanent test that pins absence is of debatable value under `simplicity-criterion.md`. `/deep-review` may question it.
6. **Scope narrows decision 21 (lines 49, 379, 731).** The maintainer wrote "the reserved config fields", and ARCH-02's fix sketch says to delete "the reserved `CacheRebuildConfig` fields" without a count. The plan deletes 2 of the 8 and defers 6, with the reason stated. The orchestrator should confirm this reading with Mike before dispatch rather than leave it to the executor.
7. **Hooks read the main checkout, not the worktree.** `check-commit-subject-version.sh` and `check-changelog-changed.sh` `cd "${CLAUDE_PROJECT_DIR}"` before reading `SubModule.xml` and `git diff --cached`. Today that is harmless: the version is the same, and no `.claude/` path is staged in the main tree. But a denial could cite the main tree's state. Line 395 has a STOP for a confused hook. Naming this cause there would help the executor recognise it.
8. **A stale line reference is left in scope.** `editor-cache-rebuild.md:195` cites `ReflectionSiteBindingTests.cs:65-80`, which was already stale (the rows are at 67-82). After Step 6 it is off by one more. It sits in a dated historical section, so leaving it is defensible. The plan could name it as deliberately untouched, as it does for the README count at line 733.
9. **Step 0's Verify (line 411) is partly judgment.** "P0 and L0 are recorded" could be an echo of the two numbers into `scratch/025/`.

## Checklist (template "Quality bar")

| Check | Result |
|---|---|
| Self-contained for a zero-context executor | Yes, apart from Blocking 1. Worktree, scratch, TEMP, the version source and recovery are all inlined. |
| Every step ends in a command with an expected result | Yes. The small gaps are Non-blocking 2 and 9. |
| Current-state excerpts match `a39a9c86` | Yes in content. Label and format mismatches are listed below. |
| TDD order | Yes. The RED absence test (Step 2) comes before the deletions (Steps 3-5), and GREEN is checked at Step 5. |
| Issue-first | Yes. Line 48: the orchestrator creates the issue, and `Refs #n` is conditional. |
| Binding ADRs and rules named with summaries | Yes: simplicity-criterion, banned `[Obsolete]`, ADR-002/007/008, Config Providers MUST Validate, Human prose (lines 326-332). |
| Single-owner files | Yes. `IoC.cs`, `SubModule.cs`, `TAOM.csproj`, `Directory.Build.props` and `TAOM.Tests.csproj` are out of scope, a STOP condition covers them, and a Done-criteria diff checks them (lines 333, 377, 708, 721). |
| STOP conditions specific | Yes: string-based loaders, compatibility-test failure, a wrong RED reason, binding-gate skips, count drift, CompanionTactics. |
| Done criteria machine-checkable | Yes, with the Skipped caveat in Non-blocking 1. |
| Planned-at SHA and drift paths vs. Scope | Consistent. `a39a9c86` is used throughout. The drift paths cover every in-scope file except `CHANGELOG.md`, which is excluded with a stated reason, and they add the read-only cited files. |
| Non-deploying commands with `-p:ModuleId=` | Yes, on every `dotnet` command, and `./build.ps1` is forbidden. |
| No em or en dash in prose | Yes. The dashes at lines 187, 197, 319, 322 and 578 are inside code spans or blocks that quote existing text. Lines 623, 696, 703 and 723 use U+2212 (minus), which is not a banned dash. |
| No secret values | None found. |

## Excerpt mismatches

- Line 116: the label says `EditorCacheRebuildIoC.cs:1-17`, but the excerpt runs to line 18 (`container.Register<SerialPhase1Builder>(Reuse.Singleton);` is line 18 at `a39a9c86`).
- Line 87: the label says "(whole file, 29 lines)", but the excerpt elides the `TryGetPathDistance` parameters with `...`. At `a39a9c86`, lines 19-27 are `fromFace, toFace, fromPos, toPos, agentRadius, distanceLimit, excludedFaceIds, regionSwitchCostTo0, regionSwitchCostTo1`.
- Line 143: the label says `PersistentPathCache.cs:145-153`, but the excerpt shows only 145-149.
- Line 429: the expected `grep -c` output uses bare file names, while the real output carries the `TAOM.Tests/Features/EditorCacheRebuild/Caching/` prefix.
- Line 381: "rows (lines 66-82)". Line 66 is the section comment, and the `NavigationCacheAdapter` rows are 67-82 (trivial).
