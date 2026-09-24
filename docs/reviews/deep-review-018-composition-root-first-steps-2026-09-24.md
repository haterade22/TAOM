# Deep review: plan 018, feature-module composition root (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 018, start the feature-module composition root (one shared source reader, the
         module contract, an empty-by-default module list and the WandererAllegiance pilot)
Date: 2026-09-24

Scope:   C# (Main/Composition, Main/IoC.cs, Main/SubModule.cs, the pilot module), 29 test files,
         CHANGELOG and one feature doc. Branch improve/018-composition-root-first-steps,
         4c728dac..44045b34 (three commits), worktree E:\repos\taom-improve\wt-018.
Waves:   Agents 1 to 6 (one wave); Agent 7 (XML) and tooling NOT IN SCOPE (no XML, no scripts).
         Codex adversarial review complete (raw file ends "END OF CODEX REVIEW").

STANDARDS:     FAIL, 9 violations (all LOW); 7 confirmed and fixed, 1 not a defect, 1 needs Mike
COMPATIBILITY: PASS on API use (30 verified, 0 incompatible, 2 unverified); 1 MEDIUM runner
               defect (fixed) and 1 LOW comment (fixed)
EFFICIENCY:    PASS, 0 issues (7 items checked below threshold)
COMPLETENESS:  INCOMPLETE at review time: no GitHub issue (needs Mike), the IoC hand-off line and
               the engine-facing hooks untested (both fixed), 4 LOW (fixed or deferred)
DATA FLOW:     FAIL, 3 gaps, 3 inconsistencies (F1, F3, F4 fixed; F2 needs Mike; F5 narrowed and
               deferred; F6 not a defect)
DESIGN:        9 KEEP proposals (5 apply, 4 follow-up); 4 applied, 1 (P5) applied as the F1 defect fix
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Verification of each finding

Every finding below was re-read against the worktree at `44045b34` before acting. "Fixed" means a
test proves it; the RED or mutation run is named.

| # | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| 1 | L2, L5 F1, L6 P5, Codex P2 | MED | `ModuleRunner.Run` skipped an already-faulted module (`if (_faulted.Contains(module.Id)) continue;`, `ModuleRunner.cs:61`) before the fail-closed rethrow (`:72-73`), so a save owner that faulted in a fail-open step was left out of the next campaign silently; a retried campaign start skipped it too | CONFIRMED (read `:56-76`; RED: two new tests threw nothing) | Fixed: a faulted save owner in a fail-closed step throws `InvalidOperationException`. Tests `ASaveOwningModule_ThatFaultedInAFailOpenStep_FailsClosedAtTheNextCampaignStart`, `..._ThatFailedCampaignStart_FailsClosedAgainOnTheRetry` |
| 2 | L5 F2 | MED, latent | TAOM's `Patch37_CrashReport` finalizer on `Module.OnApplicationTick` swallows the campaign-start rethrow while crash capture is on; `GameLoadingState.OnTick` re-runs loading step 3 on the next tick | CONFIRMED on the code chain (read `Patch37_CrashReport.cs:54-61`, `CrashReportPatchHelper.HandleAndSwallow` returns null, v1.5.3 `GameLoadingState.cs:22-32`, `SandBoxGameManager.cs:82-101`); in-game end state UNVERIFIED | NEEDS MIKE: what "closed" means at campaign start (inquiry and return to menu, or a Patch37 exemption). CHANGELOG "Known limitation" bullet records it. No module owns save data today |
| 3 | L4 F1 | MED | `Modules = modules;` (`IoC.cs:213`) is the only hand-off to the hooks and no test pinned it; deleting it kept the suite green while every module went silent | CONFIRMED (grep of TAOM.Tests found no reference) | Fixed: kernel test pins it. Mutation: line removed, `Kernel_IoCConfigure_...` failed; restored |
| 4 | L4 F2 | MED | The engine-facing half (`AddGameStartContent`, build-before-add, the fail-closed rethrow through the hook, `Hold` for the inquiry) had no test; the plan's "needs the game" reason is wrong for v1.5.3 starters | CONFIRMED (v1.5.3 `CampaignGameStarter` ctor only stores its arguments; `BasicGameStarter()` parameterless) | Fixed: internal overloads taking the runner and resolver; new `FeatureModuleHooksTests` (7 tests) with real starters |
| 5 | L1 #1, L5 F4 | LOW | Comments and CHANGELOG said modules run after every hand-wired block; `ManualPatchApplicator.ApplyAll` (`SubModule.cs:1880`), the mission kernel tail (`:2076`, `:2081`) and the main-menu work after the once-only block follow the module call. The OnGameStart anchors also passed if the call moved inside the campaign branch | CONFIRMED (read the three sites) | Fixed: `FeatureModules.cs`, `SubModule.cs:2037`, CHANGELOG corrected, test renamed `..._AfterItsFeatureBlock`, and a new assertion that a `}` separates `RegisterCampaignLifeBehaviors` from the hook. Mutation: hook moved inside the branch, test failed; restored |
| 6 | L1 #2 | LOW | The campaign-start fail-closed flag lived as a literal in `FeatureModuleHooks` and the runner test modelled campaign start with `failClosed: false` | CONFIRMED | Fixed: `ModuleRunner.RunCampaignStart`; the tests call it |
| 7 | L1 #3 | LOW | `IoC.Resolver` exposes the container to all of Main and the lens-9 grep for `IoC.Resolve<` cannot see it | CONFIRMED (hazard; only boundary use today) | Fixed: `IoCResolver_IsReadOnlyByTheFeatureModuleHooks`. Lens-9 grep widening is a harness edit: FOLLOW-UP |
| 8 | L1 #5 | LOW | A failed fault notice was swallowed with no log line | CONFIRMED (`FeatureModuleHooks.cs:151-154`) | Fixed: logs `[Module] fault notice not shown: ...` through the runner's guarded logger |
| 9 | L1 #6, L4 F5, L6 P3 | LOW | `WandererAllegianceWiringTests` summary still described the SubModule `AddBehavior` line and the IoC line | CONFIRMED | Fixed: summary rewritten |
| 10 | L1 #7, L4 F6 | LOW | "six small types" in CHANGELOG (13 types in six files) | CONFIRMED | Fixed: after the design deletions it is five files and 11 types, and the CHANGELOG says so. The body of `9ed3ee30` keeps the old count (no amend) |
| 11 | L2 LOW | LOW | `ModelTarget.CustomBattle` comment omits the editor's `BasicGameStarter` | CONFIRMED (v1.5.3 `EditorGame.OnInitialize` :13-19 passes `new BasicGameStarter()` to `OnGameStart`) | Fixed: same wording as `RegisterCustomBattleModels` |
| 12 | L5 F3 | LOW | A parked save owner still failed closed in service registration although its behavior never runs | CONFIRMED (RED: `AParkedSaveOwningModule_IsIsolated_InServiceRegistration` threw) | Fixed: a parked module never fails closed |
| 13 | L5 F5, L2 follow-up | LOW, latent | The one-model-per-slot test compares module slots only, not slots SubModule fills by hand | CONFIRMED; no module declares a model | Comment narrowed to what it checks. Deferred: the guard lands with the first model migration, together with rule 7 of `gamemodels.md` and the `GameModelOverrideBindingTests` conflict (L6 P8) |
| 14 | L4 F3 | LOW | `GameModelDecl.Of`, `MissionBehaviorDecl.Of`, the `PatchCategoryDecl` guard and the runner's null checks untested | CONFIRMED | `MissionBehaviorDecl.Of` now runs in `FeatureModuleHooksTests`; the rest deferred to the first module that declares a model or a category |
| 15 | L4 F4 | LOW | The `OwnsSaveData` IL check had never been seen to fire, and a null `SyncData` was skipped silently | CONFIRMED | Fixed: `PersistsData` helper asserts `SyncData` exists; `PersistsData_FlagsARealSyncData_AndPassesAnEmptyOne` uses `FieldCampCampaignBehavior` as the positive control |
| 16 | Codex P3-3 | LOW | `ReadSource_ReturnsTheFileWithLfLineEndingsOnly` passes on an LF checkout with the normalisation removed | CONFIRMED (mutation: `.Replace` removed, the old test still passed) | Fixed: `ReadSource_NormalisesAnExplicitCrlfFile_ToLf` writes a CRLF probe under the ignored `TAOM.Tests/bin/`; it failed under the same mutation |
| 17 | Codex P3-2 | LOW, doc | Plan 018's preconditions expect five `ReportPatchFailures(` calls; there are four (`grep -c` on `SubModule.cs`), and its startup excerpts predate the plan-009 follow-ups | CONFIRMED | NEEDS MIKE / orchestrator: `plans/018-composition-root-first-steps.md` has uncommitted edits in this worktree that this review did not make, so it was not touched |
| 18 | L4 | process | No GitHub issue for plan 018 | CONFIRMED (lens 4's searches) | NEEDS MIKE: `/issue` is never auto-invoked |
| 19 | L1 #9 | process | CHANGELOG committed although the plan lists it out of scope; the main tree has another session's `MM CHANGELOG.md` | CONFIRMED as a plan deviation | NEEDS MIKE: CLAUDE.md asks for a CHANGELOG entry each session; the merge needs coordination on that file |
| 20 | Codex P3-4 | LOW, pre-existing | `wanderer-allegiance.md:9` lists Dunland as neutral; `alignment.json:5` sets `empire` to evil | `empire` evil verified; that `empire` is Dunland is Codex's claim, UNVERIFIED here | FOLLOW-UP: line 9 predates this diff |
| 21 | L1 #8 | LOW | `FeatureDecls.cs` names none of its types | FALSE POSITIVE: 44 files in Main follow the same pattern (lens 1's own precedent count) | None |
| 22 | L5 F6 | NIT | The fault summary mixes enum names ("B (ProcessLoad)") with prose step labels | FALSE POSITIVE as a defect: the label is the step name the log and `ApplyPhase` use, greppable in a bug report | None |

## DETAILS

**Agent 1, Standards.** 9 LOW. Items 1, 2, 3, 5, 6, 7 confirmed and fixed (rows 5 to 10); item 4 is
the same proposal as Agent 6 P1 and was applied; item 8 not a defect (row 21); item 9 needs Mike
(row 19). ADR-002, 003, 004, 005 and 007 clean.

**Agent 2, Engine compatibility.** 30 API uses verified against v1.5.3, 0 incompatible. The MEDIUM
runner defect (row 1) and the editor comment (row 11) fixed. Unverified: what native code does with
an exception escaping `Module.OnApplicationTick`, and when the inquiry shows relative to the splash
video. Follow-ups listed below.

**Agent 3, Efficiency.** No performance issue. Two per-mission closure objects are kept (they put
the engine add inside the per-module try); empty list per module rejected under the simplicity
criterion; comment-strip cost about 0.3 s per suite run, rejected.

**Agent 4, Completeness.** INCOMPLETE at review time. F1 and F2 fixed (rows 3, 4); F3 partly (row
14); F4 fixed (row 15); F5, F6 fixed (rows 9, 10). The issue is Mike's (row 18).

**Agent 5, Data flow.** 16 flows traced. F1 fixed (row 1), F2 needs Mike (row 2), F3 fixed (row
12), F4 fixed (row 5), F5 narrowed and deferred (row 13), F6 not a defect (row 22).

**Agent 6, Design.** P1 to P4 applied (below). P5 is row 1. P6 to P9 are follow-ups.

## ACTION ITEMS

1. Mike: decide what fail-closed means at campaign start given Patch37 (row 2) before the first
   save-owning module migrates.
2. Mike: file the plan 018 issue (row 18) and confirm the CHANGELOG deviation (row 19).
3. Orchestrator: refresh the plan's precondition count and startup anchors (row 17).
4. Orchestrator: run one convergence `deep-reviewer` over the fix commit (Step 4.6); this delegate
   cannot spawn agents.

## IMPROVEMENTS (Step 4)

APPLIED (all behaviour-preserving; the Composition and pilot filter, 65 tests, green after each):
- `Main/Composition/ITaomFeatureModule.cs` deleted (Agent 6 P1, Agent 1 #4): `TaomFeatureModule` is
  the contract; `ApplyPhase` and the member docs moved into `TaomFeatureModule.cs`. Proof: build,
  `ModuleRunnerTests`, `FeatureModulesTests`, `WandererAllegianceWiringTests`.
- `FeatureState` and `State` deleted (Agent 6 P2): a module is parked when `ParkedReason` is not
  null; `ModuleRunner.Run` tests that; `ParkedModules_NameTheirReason` replaces the consistency test.
  Proof: `RegisterServices_VisitsEveryModuleInListOrder_ParkedIncluded`,
  `InitializeStatics_SkipsParkedModules`, `RunPhase_SkipsParkedModules`,
  `BaseModule_DefaultsToAnEnabledModuleThatDeclaresNothing`.
- `WandererAllegianceIoC_RegistersEveryConsumerOfTheBehavior` deleted (Agent 6 P3). Proof: with
  `WandererAllegianceIoC.cs:17` commented out, `Module_RegistersTheServiceGraph_...` failed; restored.
- `FeatureDecls.cs:40, :84, :103` pass-through lambdas replaced by the delegate itself (Agent 6 P4).
  Proof: build and `Module_RegistersTheServiceGraph_...`, which calls `Create` twice.

Behaviour changes made as defect fixes, not as proposals (Mike to confirm on waking): row 1 (a
faulted save owner now throws at campaign start; Agent 6 rated its P5 CHANGING) and row 12 (a
parked save owner no longer throws in registration). Neither can fire today: the only module owns
no save data and is not parked.

NOT APPLIED:
- Agent 3 item 2 (`FeatureModuleHooks.cs:76,109`, skip the empty list): rejected, simplicity
  criterion (a few bytes against a mission load).
- Agent 3 item 6 (`RepoPaths.cs:50-51`, cheaper blanking): rejected, simplicity criterion (under a
  second per run).
- Agent 6 P6 (one startup inquiry for patch and module faults): behaviour-CHANGING and edits plan
  009's helper in `SubModule.cs`; needs Mike.

FOLLOW-UP (pre-existing code or deferred by the plan; no issue filed, because `/issue` is Mike's):
- Agent 6 P7: fold the private `ReadSource` helpers in five wiring test files and two comment
  strippers into `RepoPaths` (plan 018's deferred TEST-L5-03 pass).
- Agent 6 P8 and Agent 2: `GameModelOverrideBindingTests.EveryTaomGameModel_IsRegistered_InSubModule`
  and `AssertNotHandWiredToo` will contradict each other for the first module model; fix with
  that migration.
- Agent 6 P9: one generic IoC double-registration guard once a second module exists.
- Agent 3 item 4: `IoC.Dispose` leaves `Modules` set (with COMP-05).
- Agent 2: a module mission behavior whose `OnCreated` throws leaves earlier ones added; stale
  vanilla line numbers in `WandererAllegianceDialogBehavior.cs:10-19` (v1.5.3 hire lines are :808
  and :831).
- Agent 1: `.ai/review-reference.md` and lens-1 check 9(e) should name `Main/Composition/**` as
  boundary code, and the lens-9 grep should include `IoC.Resolver`; `csharp-architecture.md`
  "File Layout" lacks `XModule.cs`; the pre-existing "added LAST" comment at `SubModule.cs:2040`;
  "TAOM loaded successfully!" at `SubModule.cs:625` has no receiver.
- Agent 4: `architecture.md` folder tree, `/new-feature` and `feature-builder.md` still send new
  features to `IoC.cs` and `SubModule.cs`; the ADR the plan defers.
- Agent 5: contract hooks missing for every main-menu return, `OnGameEnd`, `OnGameLoaded` and the
  unload sweep; `Keep` fallbacks and migration order (FieldCommission, Enlistment).
- Codex P3-4: the Dunland line in `wanderer-allegiance.md:9`.

## Tests

Baseline before any change: `Failed: 2, Passed: 10277, Skipped: 2, Total: 10281`. Final full suite
(`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`):
`Failed: 2, Passed: 10289, Skipped: 2, Total: 10293`. The two failures are the known live-Armory
tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`. Net +12 tests: 3 runner, 7 hooks,
2 module-list, 1 reader, minus the deleted pilot text test.

VERDICT: READY FOR COMMIT (defects fixed, suite green apart from the two known Armory failures).
Open items for Mike: rows 2, 17, 18, 19 and Agent 6 P6; the Step 4.6 convergence pass is owed.

## CODEX REVIEW

Raw: `docs/reviews/raw/codex-adversarial-018-composition-root-first-steps-2026-09-24.md` (complete).
Codex read the diff through git refs, ran nothing and wrote nothing. It gave 1 P2, 2 P3 and one
pre-existing P3 observation, with no P1, and resolved the ten Known Suspects (2 confirmed plan
drift, 1 disputed, the rest unverified history or no defect).

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | MED | Yes | Row 1: the faulted skip precedes the fail-closed rethrow; both of Codex's sequences (early fault, retry) are now tests |
| 2 | P3 | LOW (doc) | Yes | Row 17: four `ReportPatchFailures(` calls at base and head, not five; plan file left to the orchestrator |
| 3 | P3 | LOW | Yes | Row 16: proved by mutation on this LF working copy |
| 4 | P3 (pre-existing) | LOW | Partly | Row 20: `empire` is evil in `alignment.json`; the Dunland identity is Codex's, unverified here |

**Confirmed bugs:** rows 1, 16 (fixed); row 17 (doc, left to the orchestrator).
**False positives:** none.
**Design questions:** none raised by Codex; row 2 (Patch37) came from lens 5.
**Things Codex missed:** row 2 (its own finalizer defeats the fail-closed throw; Codex quoted the
engine chain but not TAOM's Patch37), row 3 (the untested hand-off line), row 4 (untested hooks;
Codex called source assertions weaker than execution checks but did not name the hooks), row 5
(the wrong "after every hand-wired block" claim), row 12 (parked save owners), row 15 (vacuous IL
check).

Root cause of Codex's confirmed findings (Phase 3e):

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Faulted save owner skipped at campaign start | Logic error | The plan's "skip every later step" rule and its save-protection rule were each tested alone; nobody tested the sequence where both apply | Two sequence tests; lesson in `lessons/state-lifecycle-save.md` |
| 16 | LF test passes without normalising | Other: vacuous test | The test fed input that needed no normalising on this checkout | Explicit CRLF probe; lesson in `lessons/testing-qa.md` |

## AGENTS.md lessons (pending)

Not written here (Phase 3h is consolidated for all branches). Proposed:
- "Bugs Codex typically misses": a TAOM finalizer (Patch37 on `Module.OnApplicationTick`) that
  swallows a deliberate throw from a SubModule hook; Codex traced the engine callers but not TAOM's
  own patches on them.
- "What Codex does well": turning a documented invariant (fail closed) into concrete call sequences
  (early fault, retry) that break it.
