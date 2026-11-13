# Plan review 049, round 1 (cold reviewer)

Plan: `plans/049-refuge-load-repair-unit.md`. Code read from the baseline worktree at `7087fa633`
(`git rev-parse --short HEAD` printed `7087fa633`, tree clean). No earlier review files for this plan
existed in the audit folder.

## Verdict

One blocking item: Step 5's grep checks cannot pass as written. Every other excerpt, line number,
count and command I checked matches the base. Once the blocking item is fixed, a weak executor can
carry this plan out.

## Blocking

1. **Step 5 Verify, plan lines 918 to 921: `git grep` does not search the untracked new file.**
   `Main/Features/Refuge/Domain/RefugeBookRepair.cs` is created in Step 3 and is not staged until
   Step 9, and plain `git grep` searches tracked files only. So at Step 5:
   - `git grep -n "MinBuildTargetHours" -- Main` prints 2 lines (the two `SaneBuildHours` uses in the
     service), not the expected 4;
   - `git grep -n "const float MinBuildTargetHours" -- Main TAOM.Tests` prints nothing, not the
     expected 1 line;
   - `git grep -n "0\.1f" -- Main/Features/Refuge` prints nothing, not the expected 1 line.

   I checked this in a scratch repository with an isolated git config: `git grep -n Foo` printed only
   the tracked file's hit, and `git grep -n --untracked Foo` printed both files' hits. A weak executor
   gets three mismatches against the expected results and STOPs (line 1020), or improvises.
   **Fix:** add `--untracked` to the three greps on lines 918 to 921, or have Step 5 stage the two new
   files by name before the greps. The Done criteria greps (lines 996 to 998) run after the commit and
   are correct as written.

## Non-blocking

1. **Step 5, plan lines 785 to 808: the line numbers are base numbers.** Item 1 deletes three lines
   (113 to 115), so the ranges in items 2 and 3 (643 to 666, 748 to 790) and the `SaneBuildHours`
   lines are 3 lower once item 1 is done. With the Edit tool (string match) nothing goes wrong. An
   executor working by line number would cut the wrong text. Add one sentence: "the numbers are the
   base's; edit by text with the Edit tool, or apply the items bottom-up (4, 3, 2, 1)". I applied the
   four edits bottom-up to a scratch copy, and the result was what the plan describes:
   `RefugeIdPrefix`, a blank line, then the `FrameWorkIntervalHours` summary; and
   `// --- internals ---`, a blank line, then `FinishBuild`.
2. **Plan lines 384 to 390: R has no expected value.** "R is greater than 0" is a weak check. Under
   Step 6 the R comparison does the real work, so this is acceptable. Recording the number of test
   methods the planner counted would let Step 1 catch a filter that matches the wrong set.
3. **Step 4, plan lines 776 to 778.** With the mutation in place, `Repair_BuildingRowStartedInTheFuture_ResetsToNow`
   and `Repair_EveryDefectOnOneRow_WarnsInTheFixedOrder` also fail (`_nowCalls` becomes 2). The plan
   says "other tests may fail too", so this is accurate. Naming the full expected set of 5 failing
   results would make the check exact.
4. **Line endings.** Working-tree `.cs` files in `Main/Features/Refuge` are `i/lf w/crlf`
   (`git ls-files --eol`), and the base service has a UTF-8 BOM. A new file written with LF and no BOM
   is normalized on add, and no gate checks line endings or BOMs. The move check strips each line and
   reads with universal newlines, so it is not affected. No action needed. A note would stop an
   executor from asking.
5. **ADR-009 (self-documenting code) is not named** in "Conventions that bind this change" (lines 272
   to 291). The plan moves the comments verbatim and forbids rewording them, so ADR-009 changes
   nothing here. Optional.

## Checks run, all matching the plan

- **Excerpts vs `7087fa633`:** `RefugeService.cs` lines 11, 109, 111 to 118, 643 to 672, 746 to 792,
  873 to 881, 988 and 992 match the plan's excerpts and line numbers exactly. The definition of
  `RepairLoadedRow` is at 755 and its call at 661. `git grep -n RepairLoadedRow` hits the two code
  lines plus `docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md:12823` and
  `docs/reviews/camps-review-log-2026-08-22.md:213`, as the plan says.
- `MinBuildTargetHours` and `0.1f` under `Main/Features/Refuge`: only `RefugeService.cs:114`, 775,
  878 and 879.
- `RefugeData.cs`: `RefugeTier.Refuge = 0`, so a default row has a valid tier and the
  "no warnings" tests hold. The fields are public, and `TierEnum` has a setter.
- `RefugeServiceTests.cs`: the class tag at 27, the subclass at 30, overrides at 99 and 101, `Seed` at
  363 and 364, the `LoadFrom` calls at 364, 1176, 1284, 2052, 2069, 2150, 2163, 2177, 2191, 2365 and
  2392 (eleven). There are seven `LoadFrom_` test methods (1262, 2048, 2060, 2143, 2156, 2169, 2183)
  and none is a `DataTestMethod`, so Step 6's `Total: 9` is right. `HeroMergeProbeService` (2312)
  overrides neither clock seam, and its two tests load `Established = true` rows that are not
  building. Only one test class contains `RefugeServiceTests` in its name.
- `RefugeCampaignBehavior.cs:88`, `RefugeCampaignBehaviorTests.cs:138` and `:149`,
  `Main/TAOM.csproj:114-116` (InternalsVisibleTo `TAOM.Tests`), `Directory.Build.props:6`
  (Nullable enable), `.editorconfig:40-44` (CS8600 to CS8604 set to none for `Main/**.cs`; CS8618 and
  CS8625 too, at 45 and 46). No `.editorconfig` under `Main/Features/Refuge`.
  `TimeVictory.cs:8` and `EnlistmentTransitionTable.cs:15`, `FiniteFloatValidator.cs:22`,
  `ServiceSignatureGateTests.cs:157-160` (name ends in `Service`), `EarlyLogBridgeTests.cs:126`.
  `IModLogger` is in `TAOM.Core.Logging` and has a single `LogWarning(string)`, so the
  expression-tree `When(...)` compiles. MSTest 3.1.1, and `DataTestMethod` is used across the suite.
- Engine (`taom-src path CampaignTime`, v1.5.7): `Now` at 70, `Never` at 72, `Equals` at 205.
  `CampaignTime` is a struct.
- `.ai/verification.md:28-30`: the RefAsm build and unit-step commands match the plan's commands
  table (line 305), and the class filter is added correctly.
- `docs/features/refuge.md:112-116` (the "Load repairs hostile rows" paragraph) and `:162-168` (the
  Key files table) are as the plan cites them.
- **Move check (plan lines 828 to 910), lifted and run.** I applied the plan's Step 3 unit text and
  Step 5 service edits to scratch copies. The check against them printed
  `OK: 5 literals, byte-identical and in order; both moved bodies match line for line` with exit 0.
  Against the unedited base service it printed five `FAIL: still in the service` lines with exit 1.
  The plan's claims hold.
- **Test count:** 12 methods give 16 results (3 + 2 + 2 DataRows), 15 without the one `RequiresGame`
  method, matching lines 302, 305, 939 and 948.
- **Docs lint:** `python tools/lint_docs.py --fail-on-drift` at the base exited 0. Step 9's expected
  result holds when no doc changes.
- **Ignored output:** `bin/`, `obj/` and `TestResults/` are gitignored, and the graph refresh writes
  outside the repo, so Step 9's `git status --porcelain` empty check is reachable.
- **Structure:** TDD order holds (RED at Step 2, GREEN at Step 3, mutation proof at Step 4, wiring at
  Step 5). The issue line is present. ADR-002, ADR-007 and ADR-008 are named. No protected file and no
  Step 0 are needed. Single-owner files are out of scope. The STOP conditions are specific to this
  change. The Done criteria can be checked by machine. The planned-at commit `7087fa633` matches the
  drift-check paths and Scope. Every dotnet command carries `-p:DisableModuleCopy=true -p:ModuleId=`.
  There is no worktree path, no branch name and no CHANGELOG step.
- **Prose:** no U+2014 or U+2013 in the plan (scripted scan). No secret values.
