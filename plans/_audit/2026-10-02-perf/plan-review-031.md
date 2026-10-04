# Plan review 031, round 1 (cold)

Plan: `plans/031-settings-reads-off-hot-paths.md`, read as a zero-context executor. Code read at
`dffdf879` in the program worktree (`git diff --stat dffdf879..HEAD -- Main TAOM.Tests docs/features`
prints nothing, so the worktree files equal the planned-at commit). No earlier review file for this
plan exists in this folder, so there is nothing to re-check from a previous round.

Verdict: two blocking items. Everything else is executable as written; the excerpts match the code
except for one line-range label.

## Blocking

**B1. Step 18's draft breaks two pre-existing tests (plan lines 1061-1096).** The draft declares the
nested `private sealed class IdSliceComparer` ABOVE `Prefix`. `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`
scans every `Main/**/*.cs` for `static bool Prefix(` (line 328) and names its owner as the LAST
`class` declaration before it, using `ClassDeclPattern` (line 334-336:
`^\s*(?:(?:public|private|internal|protected|static|sealed|partial|abstract)\s+)*class\s+(\w+)`).
`private sealed class IdSliceComparer` matches, so the Prefix's owner becomes `IdSliceComparer`.
Proof: I ran both regexes over the draft layout (scratch `review-031-r1/coop_scan_sim.py`); output
`prefix owner: IdSliceComparer`. Result after Step 18:
- `EveryBoolPrefix_HasACoopDisposition` (line 404) fails: `IdSliceComparer` is unclassified.
- `Registry_HasNoStaleEntries` (line 422) fails: the registry key `MBTextManager_GetLocalizedText_Patch`
  (line 200) no longer matches.

Step 18's verify runs only the `MBTextManager_GetLocalizedText_PatchTests` filter, so the break
surfaces in Step 19's full suite, where "Any pre-existing test fails after a change" (line 1196) forces
a STOP; a weaker executor may instead "fix" it by adding `IdSliceComparer` to the registry, which is
a gate edit. Fix: place `IdSlice` and `IdSliceComparer` at the END of the class (after
`ClearOverrides`), say why in one sentence (the co-op veto scan attributes a bool `Prefix` to the
last `class` declared above it), and add to Step 18's Verify:
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CoopVetoClassificationTests"`
gives 0 failed. Name `CoopVetoClassificationTests.Registry` as a gate the executor must not edit.

**B2. Step 9 says "six test files"; stage 1 touches seven (plan line 797).** Stage 1 creates or edits
`HotPathSettingsProvidersTests.cs`, `CombatMechanicsSettingsProviderTests.cs`,
`BlowDiagnosticsSettingsProviderTests.cs`, `CreatureCombatServiceTests.cs`,
`CrushThroughServiceTests.cs`, `ChargeKnockdownServiceTests.cs` and
`RaceCombatModifiersResolverTests.cs`. An executor staging "exactly" six leaves one dirty, fails
Step 9's `git status --porcelain is empty`, and either STOPs (rule: reality differs from the
contract) or adds a fourth commit that breaks the "exactly three commits" done criterion. Fix: list
the eleven stage-1 paths explicitly in Step 9 (and likewise in Steps 12 and 19).

## Non-blocking

1. **Stale line refs the plan leaves behind (scope gap).** `docs/modding/file-catalogue.md:268` cites
   `CombatMechanicsSettingsProvider.cs:19-35` and `:280` cites `DreadAuraSettingsProvider.cs:33-36`;
   both ranges move once Steps 5 and 11 add the comment, field, accessor and constructor. Review 133
   (REVIEW-LOG.md:12) recorded the same miss for plan 003 ("stale feature doc and file catalogue").
   Add the file to Scope and the drift check, and replace the line ranges with member names.
2. **Stale test counts in docs already in scope.** `docs/features/companion-tactics.md:181` says
   `SharedMovementOrderPostfixTests.cs` has 5 tests (Step 15 makes it 6);
   `docs/features/localization-override.md:98` says **11 tests** (Step 17 makes it 18). Add both
   edits to Steps 12 and 19, or drop the numbers.
3. **Done criterion range (line 1184).** PROGRESS.md:73 says wave A branches start "from the plans
   commit", which sits after `761b20fe` and `e9cd8b39` (docs only), so `git log --oneline dffdf879..HEAD`
   lists more than three commits. "from this plan" saves it for a careful reader; prefer
   `git log --oneline <base>..HEAD` (the base the executor is given) shows exactly three. The
   `dffdf879..HEAD` diff criteria (line 1182) and the drift check stay correct, since those commits
   touch no in-scope path.
4. **Line-anchored doc edits collide with sibling plans.** Plan 030 (Item E) edits
   `docs/features/companion-tactics.md`; plan 032 edits `docs/features/mixed-formations.md`
   (lines 30, 32, 34) and `Main/Features/MixedFormations/FormationLayoutService.cs`, which is inside
   this plan's drift-check folder and cited in Current state (`:68`). Wave A runs in parallel from one
   base, so no drift today, but anchor the doc edits (lines 787, 881-886, 1111-1115) by the quoted
   text rather than line numbers so a rebase or a stacked run does not trip the STOP condition.
5. **Usings an executor must discover.** Step 10b's files need `TAOM.Features.DreadAura.Domain`
   (`DreadAuraConfig`), `TAOM.Features.CultureDoctrine.Domain` (`DoctrineCatalog`, `Doctrine`) and
   `TAOM.Features.MixedFormations.Models` (`FormationLayoutType`); Step 17's IL test needs `System.Linq`
   and `TAOM.Tests.Migration`. Step 10b's Verify (line 855) says every error must be an arity
   diagnostic; add Step 4's sentence "Any other compile error is yours to fix before moving on".
6. **Local absolute path (line 80).** `E:/Decompiled_Bannerlord/_modules_build/TAOM.Dependencies__MCMv5.cs`
   is a machine path in a file that may become public (quality bar: no local absolute paths). The
   cited lines are right (6321 `Instance`, 2214, 1964, 1971, 1995, 2005, 4449 `OverrideSettings`
   calling `OverrideValues`); name the file without the drive.
7. **Excerpt label (line 327).** `MBTextManager_GetLocalizedText_Patch.cs:23-56` is quoted, but the
   excerpt also shows `ClearOverrides` (lines 58-61). The code itself matches.

## Checks that passed (evidence)

- Excerpts at `dffdf879`: all nine providers (line ranges 14-38/40-70, 9-10, 7-14, 10-21, 28-46, 10,
  8-24, 7-19, 10-12), the gate lines (`CreatureCombatService.cs:67, :82, :90`,
  `CrushThroughService.cs:88-91, :100-102, :104, :139`, `ChargeKnockdownService.cs:40-43, :66, :78-79`,
  `RaceCombatModifiersResolver.cs:35-42`, `ShieldPenetrationService.cs:51, :71`,
  `ChargeDamageService.cs:25`), `MixedFormationsMissionBehavior.cs` (150 lines, `:61`, `:124-133`,
  `Array.Empty` at :80-81), `Patch35_Formation_SetMovementOrder.cs:34-52`, `SubModule.cs:283`, every
  `TaomSettings.cs` default and line cited (492 to 1230), `CombatMechanicsConfig.cs:120`, and the four
  doc lines (combat-mechanics :91, companion-tactics :139 and :162 with its em dash, mixed-formations
  :149, localization-override :36) all match.
- Engine facts (`pwsh tools/taom-src.ps1 path`, v1.5.3 cache): `public readonly Team Team;`
  (Formation.cs:90), `public void SetMovementOrder(MovementOrder input)` (Formation.cs:707),
  `public Team PlayerTeam` (Mission.cs:1304), `internal static string GetLocalizedText(string text)`
  (MBTextManager.cs:232).
- All nine providers are `Reuse.Singleton` in their `*IoC.cs`; no `new <Provider>(` in `Main/`; no
  TAOM code registers or replaces an MCM settings instance.
- RED steps: Step 3 (no `Settings` property yet), Step 6 (each old gate reads the toggle first; the
  `Ctx` defaults `isAiControlled = false`, `attackerMonsterId = null`, fixed `roll` make the
  CrushThrough rows deterministic and RED), Step 15 (`.CancelStanceOnMove` at :37 precedes
  `Mission.Current?.PlayerTeam` at :49 after comment stripping), Step 17 (`Substring` at :42). Test
  counts 26/22/21/11 are plain `[TestMethod]`s with no `DataRow`, so Step 7's arithmetic holds; the
  localization class has 11 tests.
- DryIoc 4.8.8 has both non-generic overloads Step 3 uses
  (`RegisterInstance(IRegistrator, Type, Object, ...)`, `Register(IRegistrator, Type, Type, IReuse, ...)`);
  `LangVersion` 10 covers `where TEnum : struct, Enum`, target-typed `new()` and file-scoped namespaces;
  MSTest 3.1.1, NSubstitute 5.1.0; `InternalsVisibleTo` already serves `BattleBalanceSettingsProvider`'s
  internal constructor.
- Baseline totals match `plans/_audit/2026-10-02-perf/baseline.md`. `python tools/lint_docs.py
  --fail-on-drift` exits 0 at base (run in the worktree; it wrote nothing).
- TDD order, issue line, ADR-002/003/005/007/008 named, no protected file, single-owner files out of
  scope, STOP conditions specific, non-deploying commands with `-p:ModuleId=`, no worktree or branch
  name, no CHANGELOG step. Draft subjects are 63, 65 and 70 characters with `v2.0.32`. The only em dash
  in the plan is inside a quoted code excerpt (line 311).
