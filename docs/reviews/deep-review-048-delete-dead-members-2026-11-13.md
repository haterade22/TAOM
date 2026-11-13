# Deep review: plan 048, delete eight members nothing calls (2026-11-13)

```
DEEP REVIEW REPORT
===================
Feature: plan 048, delete eight members nothing calls and correct the comments that still
         describe them (branch improve/048-delete-dead-members, 664f19c5b..5e788732f)
Date:    2026-11-13

Scope:   C# (7 files: 6 deletions, 1 comment-only edit), one test-file comment, the entry point
         line-count baseline, one feature doc. No XML, no harness files.
Blast radius: 8 deleted members. git grep -n -w over every tracked .cs and .xml at the tip finds
         no caller (orchestrator spec check, re-run by the lead with the member names and with the
         names as string literals for reflective use: no hit). Lens 5 also searched the installed
         Modules tree (TAOM_Map and LOTRLOME_Armory included) and byte-searched every installed DLL.
         graphify not run: no caller exists to trace.
Waves:   one lens report delivered to the lead: Data flow (5). Codex not run.

STANDARDS:     NOT RUN (no report delivered to the lead)
COMPATIBILITY: NOT RUN (no report delivered to the lead); the one engine claim was re-checked by the lead
EFFICIENCY:    NOT IN SCOPE (deletions and comments only)
COMPLETENESS:  NOT RUN (no report delivered to the lead)
DATA FLOW:     PASS: 13 flows traced, 0 gaps, 0 inconsistencies; 2 pre-existing items (FOLLOW-UP)
DESIGN:        NOT RUN (no report delivered to the lead)
XML:           NOT IN SCOPE (no ModuleData change)
TOOLING:       NOT IN SCOPE (no hook, validator or CI change)
```

## Details

### Agent 5, Data flow

13 flows, all CONNECTED. The ones the verdict rests on, each re-checked by the lead:

- **Weapon toggle.** The engine's quick release reaches only vanilla's controller:
  `MissionGauntletMainAgentEquipDropView` declares `MissionMainAgentController _missionMainAgentController`
  (decompile v1.5.7, line 28), fills it with `GetMissionBehavior<MissionMainAgentController>()` (line 84)
  and calls `OnWeaponUsageToggleRequested()` on it (line 252). TAOM's class is
  `AutonomousMovementPlayerController : MissionView, IAutonomousMovementController`
  (AutonomousMovementPlayerController.cs:17), not a subclass, so its copy of the method was never
  reached and `_weaponUsageToggleRequested` was always false. Key 17 still sets
  `ToggleAlternativeWeapon` (diff hunk at :554-560). Behaviour preserved.
- **No reflective use.** `git grep` at HEAD over `*.cs`, `*.xml` and `*.xslt` for
  `OnWeaponUsageToggleRequested`, `"GetBool"`, `"GetInt"` and `"IsExpanded"`: no hit. The same search
  for `TryInvokeStatic`, `GetAssemblySimpleName`, `FromCurrent`, `IsRoutable`, `ExecuteToggleExpand` and
  `_weaponUsageToggleRequested` over all tracked files (historical folders excluded): no hit.
- **Usings.** `using System.Globalization` in MutationParams.cs is still used by `GetFloat`
  (MutationParams.cs:19). `using System.Reflection` in ReflectionUtils.cs is now unused (its only use was
  `BindingFlags` in the deleted `TryInvokeStatic`); plan 048 puts `using` directives out of scope
  (an unused `using` raises no warning in this build), so it stays. See NOT APPLIED.
- **Patch0 comment.** The new claim at MBMapScene_GetBattleSceneIndexMap_Patch.cs:18-20 is true:
  `TryPatchCategory("Patch0_BattleScenes")` is at SubModule.cs:495 with no condition, the re-enable is
  commit 4a900dcd3 dated 2026-06-01, and harmony-patch-registry.md:15 says ACTIVE.
- **Patch21 example.** The formatter's new example is a parked category: its apply line is commented
  out (SubModule.cs:535) and harmony-patch-registry.md:177 reads "PARKED 2026-09-25, not applied".
- **Baseline.** Only the AutonomousMovementPlayerController row changed (665 to 661), as the diff shows.

The lens's other flows (mutation params from ModuleData, career prefab bindings, formation routing,
crash-report frames, the ReflectionUtils caller list, installed DLL references) are relayed from its
report; the lead re-ran the tracked-file greps, not the installed-module byte search.

## Action items

None. No defect was found in the diff.

## Improvements (Step 4)

- **APPLIED:** none. No Agent 3 or Agent 6 report was delivered, so no proposal exists.
- **NOT APPLIED:**
  - Dependencies/Foundation/ReflectionUtils.cs:3, remove the now-unused `using System.Reflection`.
    Found by the lead. Plan 048 lists `using` directives as out of scope, and the build raises no warning
    for it. The lead applied and built it (suite green), then restored the line with an edit, so the
    branch tree equals 5e788732f again.
- **FOLLOW-UP** (pre-existing, not caused by the diff; no issue: /improve agents file no issues, the
  orchestrator routes these):
  1. LOW, UNVERIFIED in game: a quick release during a warg rage. Lens 5 reads vanilla's
     `OnWeaponUsageToggleRequested` flag as delayed, not dropped, while TAOM's controller has vanilla's
     disabled: the flag is cleared only in vanilla's `ControlTick`, which runs again after `Enable()`, so
     weapon usage may toggle late. This refines the plan's own UNVERIFIED lead (plan 048, Maintenance
     notes). The lead checked the call site (line 252) but did not re-trace the enable and disable path.
     Recommended: `/investigate` with an in-game check.
  2. LOW: `IsLocked` on `CareerChoiceGroupObjectVM` is computed (CareerScreenVM.cs:324-325)
     and read by nothing now that the unreachable `ExecuteToggleExpand` is gone. The screen shows its
     lock state through `Tier2Locked` and `Tier3Locked`. Plan 048 defers it; a later deletion would also
     edit gui-sprite-system.md:241, :304 and :371.

## Verification

- Full suite in the worktree at 5e788732f, run by the lead before any edit:
  `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259` (equal to the base totals
  the orchestrator quoted). The same totals after the trial `using` edit; the edit was then undone, and
  `git status` showed a clean tree.
- No gate changed, so no differential sweep. No hook or tool file changed, so the hook suite and
  `tools/tests` were not run.
- Not covered: no in-game check (another session holds the game install). The changes delete code
  with no caller, so none is owed for this diff.

VERDICT: READY FOR COMMIT

## Codex review

Codex not run: out of usage, and a paid call needs the maintainer's word. The convergence rounds of the
/improve run stand in for it.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| (none) | not run | | | |

- **Confirmed bugs:** none.
- **False positives:** none.
- **Design questions:** the two FOLLOW-UP items above.
- **Things Codex missed:** not applicable.

### Codex lessons (pending)

None. No new false-positive pattern and no missed category.
