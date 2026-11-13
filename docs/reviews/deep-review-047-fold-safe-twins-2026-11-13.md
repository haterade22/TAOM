# Deep review: plan 047, fold three behaviour-identical twins (2026-11-13)

```
DEEP REVIEW REPORT
===================
Feature: Fold three behaviour-identical twins: one LotrIssue rules helper (LotrIssueTemplateRules), one
         CultureStageView type probe (CultureStageViewType), one supply row view model (SupplyRowVM)
Date: 2026-11-13

Scope:   C# (Main/Features/LotrIssues, Main/Features/FactionMap/Hooks, Main/Features/SupplyLines/UI),
         tests, one feature doc. Branch improve/047-fold-safe-twins, diff 664f19c5b..911373c9e
         (three commits, one per part), plus this review's follow-up commit.
Blast radius: graphify not run by the lead (delegated step); callers traced by hand with git grep over the
         whole tree: SupplyOrderScreenVM is the only user of SupplyRowVM; the three CultureStageView
         patches are the only users of CultureStageViewType (category Patch7_FactionMap); the three
         templates and LotrIssuesCampaignBehavior are the only users of LotrIssueTemplateRules. The old
         names (SupplyGoodRowVM, SupplyTroopRowVM, FindCultureStageViewType) survive only in plans/,
         docs/reviews and one archived 2026-04 Codex review.
Waves:   lens reports received: 2 (Engine compatibility) and 5 (Data flow). No other lens report was
         handed to this lead.

STANDARDS:     NOT RUN (no lens report received)
COMPATIBILITY: PASS: 0 incompatible, 18 verified, 1 unverified (not load-bearing); 2 LOW (wrong or
               overstated claims in new comments), both fixed
EFFICIENCY:    NOT RUN (no lens report received)
COMPLETENESS:  NOT RUN (no lens report received)
DATA FLOW:     PASS: 12 flows traced, 0 gaps, 0 inconsistencies; 1 LOW note (the fold removed the
               compile-time split between goods and troop rows), closed with two tests
DESIGN:        NOT RUN (no lens report received; no KEEP proposals)
XML:           NOT IN SCOPE (no XML in the diff; the prefab was read, not changed)
TOOLING:       NOT IN SCOPE (no hook, validator or CI step changed)
```

## Details

Every finding below was re-read against the code in the worktree before it was classified. The fixes are
in the commit `fix(faction-map): v2.0.38 - review follow-ups for plan 047`.

### Orchestrator questions (answered from the code and the installed v1.5.7 engine)

- **(A) LotrIssues.** Same result for every input. `Hero.GetRelationWithPlayer()` is `public float`
  (taom-src v1.5.7 `Hero.cs:2110`) and returns `MainHero.GetRelation(this)`, an `int` (`:2140`).
  `LotrIssueDefinition.RelationMin` is `int` (`Domain/LotrIssueDefinition.cs:86`), so
  `def?.RelationMin ?? DefaultRelationMin` is an `int` widened to `float`, exactly as the old
  `GetRelationWithPlayer() < (_def?.RelationMin ?? -10)` did; nothing truncates
  (`LotrIssueTemplateRules.cs:37-38`). The at-war test still sits behind `CurrentSettlement != null &&`
  in all three templates (lens 2 cites the lines; unchanged hunks). No base class, no `SaveableField`
  change, `LotrIssueSaveableTypeDefiner.cs` not in the diff stat.
- **(B) CultureStageView.** Each patch keeps `private static I...? _hook`, `static bool Prepare()` and
  `static MethodBase? TargetMethod()` (`CultureStageView_Constructor_Patch.cs:11-23`); the two names are
  byte-identical and in the same order (`CultureStageViewType.cs`), `Find()` has no cache field, and the
  helper carries no `[HarmonyPatch]`, `TargetMethod` or `[ClientOnly]`.
- **(C) SupplyRowVM.** Both item templates of `TaomSupplyOrderScreen.xml` bind `@Label` (`:234`, `:318`),
  `ExecuteMinus` (`:243`, `:327`), `@QtyText` (`:254`, `:338`) and `ExecutePlus` (`:258`, `:342`); the
  factories pass the two literals unchanged with `STOCK` and `COUNT`.

### Confirmed and fixed

| # | Sev | Finding | Lens | Fix | Proof |
|---|---|---|---|---|---|
| R1 | LOW | `CultureStageViewType.cs:7-9` said the view "lives in SandBox.GauntletUI or SandBox.View, which TAOM cannot reference at compile time". `Main/TAOM.csproj:47-50` references every DLL in `$(TaomSandBoxModuleBin)` (`GameReferences.targets:34`, `Modules\SandBox\bin`), several patches compile against SandBox.GauntletUI.dll (for example `using SandBox.GauntletUI;` at `Patch33_GauntletInventoryScreen.cs:3` and `using SandBox.GauntletUI.Missions;` at `Patch60_TournamentExitMovieRelease.cs:4`), and taom-src finds the type only as `SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView` | L2 | Comment says the view is found by name, names the v1.5.7 type and calls the SandBox.View name a fallback no installed assembly ships | Read back; taom-src `path CharacterCreationCultureStageView` printed the one SandBox.GauntletUI file |
| R2 | LOW | `CultureStageViewTypeTests.cs:8-9` said the binding gate "proves that the names resolve against the installed engine". The gate fails when `TargetMethod()` returns null or a method with no body, or throws (`HarmonyPatchBindingTests.cs:76-79`, `:160-162`); nothing probes each name, and `Find()` stops at the first one, so the second name is never probed | L2 | Comment says what the gate checks and that it does not test the second name | Read back against `HarmonyPatchBindingTests.cs:76-79`, `:160-162` |
| R3 | LOW | Both lists are now `MBBindingList<SupplyRowVM>` (`SupplyOrderScreenVM.cs:31-32`), so a list built with the other factory compiles, and no screen test read a row's label (only `ItemId`, `SupplyOrderScreenVMTests.cs:189`, `:721`) | L5 | `Populate_GoodsAndTroopRows_EachUseTheirOwnLabel` (call sites `:664`, `:753`) and `SearchHitSelect_PromotedGood_KeepsTheGoodsLabel` (`:660`) in `SupplyOrderScreenVMTests` | Green on the fixed tree; RED under two mutants, reverted by Edit: sites 660 and 753 swapped gave `Expected:<Militia - 4 available, 50 denars>. Actual:<Militia - 4 in stock, 50 denars>` and `Expected:<Grain - 9 in stock, 4 denars>. Actual:<Grain - 9 available, 4 denars>`; site 664 swapped gave `Expected:<Grain - 5 in stock, 10 denars>. Actual:<Grain - 5 available, 10 denars>` and `Expected:<Wine - 2 in stock, 30 denars>. Actual:<Wine - 2 available, 30 denars>` |

### Not findings

- L2 UNVERIFIED (not load-bearing): no caller of `IssueBase.GetFrequency()` in the cached decompile or in
  TAOM. The fold returns the same value either way.
- L5: `CultureStageViewType.PossibleTypeNames` is an `internal` array where each patch held a `private`
  copy; only the test reads it. Not changed (the simplicity criterion: no reader can mutate it today).

### Test runs

- Base, before any edit: `Passed!  - Failed:     0, Passed: 39266, Skipped:     9, Total: 39275`.
- After the fixes: `Passed!  - Failed:     0, Passed: 39268, Skipped:     9, Total: 39277` (the two new
  tests). Both runs: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` in the worktree.
- The two new tests are in `SupplyOrderScreenVMTests`, whose class carries `[TestCategory("RequiresGame")]`
  (`:17`), so the no-game replay skips them; the RefAsm unit step was not re-run.
- No hook or validator changed, so no hook suite and no gate sweep.

## Action items

1. None open on the changed code.

## Improvements (Step 4)

```
APPLIED:     none (no Agent 3 or Agent 6 report; R3's tests are the fix for a confirmed finding)
NOT APPLIED: none
FOLLOW-UP:   CultureStageView_Constructor_Patch.cs:25-27, CultureStageView_Finalize_Patch.cs:23-24 and
             CultureStageView_Tick_Patch.cs:23-24 keep the same wrong compile-time claim (unchanged lines;
             no issue: the review lead files none, listed for the orchestrator).
             docs/features/faction-map.md:22 says "Postfix on Finalize"; the code is a [HarmonyPrefix] on
             OnFinalize (CultureStageView_Finalize_Patch.cs:20, :26-27); already in plan 047's list.
             The SandBox.View fallback name is dead on v1.5.7; removing it is behaviour-neutral there but
             was kept by the plan on purpose.
Convergence: the single convergence deep-reviewer on the fix diff was not launched (this lead cannot spawn
             agents); the fix diff is two comments and two tests.
```

VERDICT: READY FOR COMMIT

## Codex review

Codex not run: out of usage, and a paid call needs the maintainer's word. The lens rounds stand in for it.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | not run | not run | not applicable | No Codex review file exists for this item |

- Confirmed bugs (from Codex): none.
- False positives: none.
- Design questions: none.
- Things Codex missed: not applicable; the three LOW findings above came from lenses 2 and 5.

### Codex lessons (pending)

None: no Codex pass ran, so there is no new false-positive pattern or missed category to record.

RCA: [rca-fold-safe-twins-2026-11-13.md](rca-fold-safe-twins-2026-11-13.md).

## Convergence round 1

The convergence reviewer read `911373c9e..cf21b3d33` and reported two LOW findings, both in review
prose; neither touches code. Each was re-read against the worktree before it was classified. The fixes
are in the commit `fix(reviews): v2.0.38 - convergence fixes for plan 047`.

| # | Sev | Finding | Verdict | Proof |
|---|---|---|---|---|
| C1 | LOW | The lesson (`lessons/adapters-taleworlds-api.md:1935`), the RCA (row 2) and R2 above said the binding gate fails only on a null `TargetMethod()` | Fixed: all three now say the gate fails on a null target, a target with no body, or a throw during resolution, and that nothing probes each name | `HarmonyPatchBindingTests.cs:160-161` fails a null target, `:162` fails a target with no body, and `:76-79` records a throw from `ResolveTarget` as a failure. `CultureStageViewTypeTests.cs:8-9` was already correct and is unchanged |
| C2 | LOW | R1 above quoted `using SandBox.GauntletUI;` as present in several patches and gave `Patch60_TournamentExitMovieRelease.cs` as the example | Fixed: R1 now says several patches compile against SandBox.GauntletUI.dll and cites both usings by file:line | `git grep -n "using SandBox.GauntletUI" HEAD -- Main` finds the exact `using SandBox.GauntletUI;` only at `Patch33_GauntletInventoryScreen.cs:3`; `Patch60_TournamentExitMovieRelease.cs:4` is `using SandBox.GauntletUI.Missions;`. R1's conclusion (TAOM compiles against the assembly) stands |

Not fixed: the body of `cf21b3d33` repeats C1's "fails only when a patch's target is missing". Rewording
it needs a history rewrite, which is the orchestrator's call; the docs above now carry the correct text.

Test runs: base before any edit and after the fixes, both `Passed!  - Failed:     0, Passed: 39268,
Skipped:     9, Total: 39277`. No hook or validator changed, so no hook suite and no gate sweep.
