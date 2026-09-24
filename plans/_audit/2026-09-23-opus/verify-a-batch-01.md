# Verify batch A-01: adversarial re-check of COMP-02, COMP-03, ARCH-02

Checker: fresh adversarial pass, all reads against `b2e387db` via `git show` / `git grep` (HEAD is
`4b5662b2`; `Main/SubModule.cs` and `Main/IoC.cs` read only as committed at `b2e387db`). No build,
no test run.

## COMP-02: 22 singleton CampaignBehaviors re-used across campaigns

**Outcome: CONFIRMED (mechanism), with four mis-citations corrected below. Impact today: LOW.**

What holds on my own re-reading:

- `git show b2e387db:Main/SubModule.cs`: `grep -c 'AddBehavior(IoC.Resolve'` = 22 and
  `grep -c 'AddBehavior(new'` = 34. The 22 lines are exactly 1091, 1156, 1177, 1288, 1293, 1310,
  1325, 1364, 1367, 1387, 1412-1421, 1436-1437.
- All 22 types are `Register<T>(Reuse.Singleton)` (per-type `git grep -E "Register<[^>]*\bT\b"`):
  e.g. `WarOfTheRingMomentumIoC.cs:24`, `ArmyTargetingIoC.cs:19`, `TroopWeightIoC.cs:16`,
  `QuickActionsIoC.cs:15`, `EquipPresetsIoC.cs:16`, `MessengerIoC.cs:14`, `CaravanTradeIoC.cs:16`,
  `EconomyDiagnosticsIoC.cs:12`, `AutoResolveDiagnosticsIoC.cs:22`, `WandererAllegianceIoC.cs:19`,
  `EnlistmentIoC.cs:90-99`, `FieldCommissionIoC.cs:47-48`.
- The container is process-lifetime: `IoC.Configure()` runs once in `OnSubModuleLoad`
  (`SubModule.cs:115`), `IoC.Dispose()` only in `OnSubModuleUnloaded` (`SubModule.cs:2127`);
  `OnGameEnd` (`:772-799`) resets two ArmyTargeting services and nothing else. So every campaign in
  one process receives the same behavior instances and their instance fields.
- `EnlistmentBehavior.cs:134-141` and `MessengerCampaignBehavior.cs:43-47,161-191` say exactly this
  and hand-roll a `_lastSessionStarter` gate; Messenger's comment records Codex review #34.
- Consumers: only `Patch34_SPInventoryVMSearchApply.cs:23` resolves a behavior instance from the
  container; `MessengerEncyclopediaMixin.cs:72,129` uses `Campaign.Current.GetCampaignBehavior<>`,
  which is lifetime-agnostic. No `RegisterMany`/`RegisterDelegate` alias of any behavior.
- Not by-design: no ADR or rule decides behavior lifetime. `.claude/rules/csharp-architecture.md:33-38`
  lists Singleton for "Services, engines, caches" and Transient for "Hooks, stateless helpers";
  entry-point behaviors are not listed. The one prior Transient rejection
  (`docs/reviews/lessons/state-lifecycle-save.md:106`) is about per-mission CareerSystem
  controllers, not campaign behaviors. Re-running `RegisterEvents` on a reused instance is safe
  (listeners are per campaign: `docs/reviews/REVIEW-LOG.md:2663-2667`), and the finding does not
  claim otherwise.

What does not hold, corrected:

1. "each one hand-rolls new-campaign detection" is overstated. Only 3 of 22 carry a
   `_lastSessionStarter` gate: `MessengerCampaignBehavior`, `EnlistmentBehavior`,
   `EnlistmentContentBehavior` (script over each file for field declarations and the
   `_lastSessionStarter` token). Two more reset behavior-held state another way:
   `AutoResolveDiagnosticsBehavior.cs:46,66-71` (a readonly `Dictionary<MapEvent,...>` cleared on
   every session launch, comment "A second campaign in the same process must not inherit the first
   one's state") and `InventorySearchCampaignBehavior.cs:52-59` (seeds on `OnNewGameCreated`). 11 of 22
   hold no instance state beyond readonly injected dependencies (12 counting
   `EnlistmentMenuBehavior.cs:31`'s wait-menu `Stopwatch` as scratch).
2. `CaravanVisitMemoryBehavior.cs:31-34,44` is mis-cited: the behavior is stateless (one readonly
   field, `:22`); its reset clears the singleton SERVICE `ICaravanVisitMemory`, which a Transient
   behavior would still have to clear. It is F3-class service residue, not behavior reuse.
3. "The per-behavior `_lastSessionStarter` / `_justLoadedFromSave` machinery exists only because of
   the reuse" is half right. `_lastSessionStarter` exists only because of reuse; `_justLoadedFromSave`
   is MANDATED by `.claude/rules/csharp-architecture.md:110-114` for any behavior fronting a singleton
   store (it decides whether to reset the store) and survives the flip.
4. Effort says "21 registrations"; there are 22 (the fix also flips `InventorySearchCampaignBehavior`
   once Patch34 switches to `GetCampaignBehavior`). And one test pins the current lifetime:
   `TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs:62-68`
   (`Resolve_Behavior_IsASingleton`, `Assert.AreSame`), which the flip must invert.

Impact today: no present cross-campaign defect is shown by the cited evidence; the remaining
mutable fields in the other 19 are per-interaction scratch (`FieldCommissionDismissDialogBehavior`
`_pendingDismissHeroId`, `EnlistmentQuartermasterBehavior` `_lastResult`, `EnlistmentBattleBehavior`
`_loggedUnresolvedCommanderId`) or rewritten on every save/load (`WarOfTheRingMomentumBehavior`
`_chunks`, `:103-127`). `InventorySearchCampaignBehavior` (`:16-26`) keeps `_persistedVersion` across
campaigns and seeds only in `OnNewGameCreated`; whether a legacy save's missing key leaves the prior
campaign's value in place depends on engine `IDataStore.SyncData` missing-key semantics (UNVERIFIED,
not cited by the finding). Side note, not this finding: `TroopCountDiagnosticsBehavior.cs:45-47`
subscribes a process-static `ScreenManager.OnPushScreen` per campaign and unsubscribes only on
`OnGameOverEvent`; a Transient flip would not fix that one.

## COMP-03: SubModule text-scraping wiring tests pass on commented-out code

**Outcome: CONFIRMED. Impact today: LOW (test-only; no runtime effect).**

Every link re-read at `b2e387db`:

- `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs:55-62`: `ReadRepoFile("Main","SubModule.cs")`
  (raw `File.ReadAllText`, `:190-198`), then `!subModule.Contains($"new {m.Name}(")`. No comment
  stripping, no parked allowlist. `DiscoverGameModels` (`:153-172`) keeps every non-abstract type whose
  base chain reaches `TaleWorlds.Core.GameModel`.
- `TaomPartyNavigationModel : DefaultPartyNavigationModel`
  (`Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs:28`); the committed API snapshot
  lists it as a GameModel subclass (`docs/reference/taleworlds-api-snapshot/gamemodel-bases.md:179-180`),
  so reflection over the TAOM assembly finds it.
- `new TaomPartyNavigationModel(` occurs exactly once in `git show b2e387db:Main/SubModule.cs`, at
  line 1044, inside `// campaignStarter.AddModel(...)`. So the "is registered" test passes for a model
  that is not registered, purely because of the comment. The baseline run (`baseline.md`: 10,235
  passed, 2 Armory failures, 0 inconclusive) had this test green.
- The same raw `Contains` shape: `FieldCampWiringTests.cs:189-191` (ReadSource `:53-58` is raw
  `File.ReadAllText`), `UncapturableHeroesWiringTests.cs:90-102`,
  `Patch85EnlistedDetachDeferralBindingTests.cs:128-138`, `Patch84SiegeAftermathMenuGuardTests.cs:201-211`,
  `RefugeWiringTests.cs:57-75`, `SignatureStrikesBindingTests.cs:206-214`. The commented
  `// _harmony.PatchCategory("Patch54_NavalTravelBoatVisual");` is at `SubModule.cs:1753` as cited.
- `CoopVetoClassificationTests.cs:304-312` holds `CommentPattern` + `StripComments` (used at `:350`,
  `:445`); none of the 26 files uses it or any other comment filter (per-file grep for
  `StripComments|CommentPattern|Regex.Replace|StartsWith("//")`: 0 hits in all 26).
- Count: `git grep -l -E '"SubModule\.cs"|"IoC\.cs"|SubModule\.cs"|/IoC\.cs"' b2e387db -- 'TAOM.Tests/*.cs'`
  = 26 files; 18 of them absent at `141b749` (`git cat-file -e` per file), matching the Delta line.
- No other guard covers the hole: the other category tests (`Patch73BindingTests`, `Patch75BindingTests`,
  `Patch74NameplateBindingTests`, `UncapturableHeroesBindingTests:244-262`, `RecruitGatePatchTests`)
  pin the attribute literal, never read SubModule text; Harmony is not applied in the MSTest host
  (`docs/reviews/lessons/harmony-il.md:55`).
- Not by-design: no rule mandates source-text wiring tests; the lessons already criticise them for a
  neighbouring blind spot (`docs/reviews/lessons/build-tooling-workflow.md:787-807`, `:1549-1557`),
  which a comment hole is not listed under.

Corrections (minor): the Refuge span is `:57-75` and SignatureStrikes `:206-214` (the three
`Contains` sit at 212-214). `UncapturableHeroesWiringTests.cs:57-74` pins ORDER by `IndexOf`, but that
order is a real constraint (the single `IInquiryAdapter` registration in `EnlistmentIoC`, commented at
`IoC.cs:194-199`), so it is a legitimate invariant expressed textually, not arbitrary spelling.
The "a rename of `AddTaomBehavior`" example is real: it is a local function at `SubModule.cs:1936`.

## ARCH-02: three unreachable scaffolds kept alive by tests and a binding row

**Outcome: CONFIRMED (all three are unreachable), with one mechanism corrected (the binding row is
not an engine binding) and a prior keep decision the finding does not cite. Impact today: LOW.**

Verified at `b2e387db`:

- `Main/Adapters/IEditorSceneAdapter.cs:5`, 29 lines. `git grep -n -w IEditorSceneAdapter b2e387db`
  over the whole tree: only the declaration and one archive changelog line. Only commit: `6a80bac6`
  (2026-05-12).
- `EditorCacheRebuildIoC.cs:16-17` registers `IPathReuseCache`/`PathReuseCache` and
  `IPersistentPathCache`/`PersistentPathCache`; `EditorCacheRebuildIoC` is called at `IoC.cs:158`.
  Per-type `git grep -l -w` over `Main` and `TAOM.Tests`: the interfaces appear only inside
  `Caching/` and the IoC file; `NavigationPathCloner` and `SortedPathKey` only inside `Caching/` and
  their own tests. `Caching/` = 6 files, 292 lines (16+8+16+42+160+50); tests
  `TAOM.Tests/Features/EditorCacheRebuild/Caching/*.cs` = 4 files, 406 lines (62+92+169+83). Only
  commit on `Caching/`: `6a80bac6`.
- `CacheRebuildConfig.cs:37` opens the reserved block; `:42` and `:45` summaries cite the `Caching/`
  scaffolding; `EnablePathReuse`/`EnablePersistentPathCache` have no reader in `Main` outside the
  config class (the only other hits are `CacheRebuildConfigProviderTests.cs:87-88,106-107`).
- `CompanionTacticsIoC.cs:27` registers `IHeroAutoAssigner`/`HeroAutoAssigner` (51 + 17 lines; tests
  93 lines); nothing resolves or injects it (`git grep -w` hits: the IoC line, the two declarations,
  a comment in `OOBButtonsVM.cs:82`, the test). `OOBButtonsOverlay.xml:24`
  `Command.Click="ExecuteAssignCharacters"` binds `OOBButtonsVM.cs:73-89`, whose `:88` prints the
  literal, unlocalized `Auto-Assign is a Phase-1 stub — feature pending...` (em dash present). The
  movie is loaded by `OOBOverlayService.cs:113,120`. Text landed in `55950378` (2026-05-07), per
  `git log -S "Phase-1 stub"`.

Corrections:

1. **The `ReflectionSiteBindingTests.cs:65` row is not an engine binding.** The site it names,
   `PersistentPathCache.cs:149`, is `typeof(PathReuseCache).GetField("_store", ...)` on TAOM's OWN
   `TAOM.Features.EditorCacheRebuild.Caching.PathReuseCache`. The row's full name
   `TaleWorlds.Engine.PathReuseCache` does not exist: `grep -c -a PathReuseCache` is 0 for every
   `TaleWorlds.*.dll` in `bin/Win64_Shipping_Client` and every DLL in the Native, SandBox,
   SandBoxCore, StoryMode and CustomBattle module bins. The row passes only through the simple-name
   fallback (`ReflectionSiteBindingTests.cs:133-141`), which finds the TAOM type. So an engine bump
   cannot break it; the finding's "an engine bump must keep green for code that never runs" is wrong.
   The same misattribution sits in `docs/reference/taleworlds-api-snapshot/reflection-sites.md:47`
   and in the comment at `TAOM.Tests/Migration/GameAssemblies.cs:62`. The row is still deletable
   with the scaffold (and is a mislabelled self-reflection in its own right). Whether the row
   resolves in a filtered run where `TAOM.dll` is not yet loaded is UNVERIFIED (no test run here).
2. **A prior recorded keep decision exists and is not cited.**
   `docs/changelog-archive/CHANGELOG-2026-H1.md:7625` ("Tolerated orphans ... `IEditorSceneAdapter`,
   `PathReuseCache`/`PersistentPathCache` pair ... not deleted to preserve test coverage and future
   hook points. Re-evaluate in v2 if not wired.") and `docs/features/editor-cache-rebuild.md:84,
   104-105,184` document them as reserved v2 path-reuse scaffolding (`:184` prices it as a "2-3x win"
   if wired). It is not an ADR, rule, orientation trap or BRIEF tradeoff, and
   `.claude/rules/simplicity-criterion.md` rejects "code in case we need it later", so the finding
   stands, but as a re-decision of a documented keep, with the feature doc to be edited alongside.
3. **Fix-sketch omissions:** `CacheRebuildConfigProviderTests.cs:87-88,106-107` asserts the two
   reserved flags parse, and the catalogue row `reflection-sites.md:47` must go with the DataRow.
4. **Auto-Assign reach:** the overlay attaches only when `EnableFormationPresets` is on
   (`OOBOverlayService.cs:70`), which defaults to false and whose MCM hint already says
   "Work-in-progress ... off by default; opt in to try it" (`TaomSettings.cs:834-836`). Players who
   see the stub text opted into a feature labelled WIP.
5. Arithmetic: 292 + 406 + 68 + 93 = 859 with no overlap between the sets (888 with
   `IEditorSceneAdapter`'s 29); "850 less overlap" is close but the overlap does not exist.

## What I did not cover

- No `dotnet build` or `dotnet test` (brief forbids). COMP-03's "the parked model passes by accident"
  rests on reading the test, the single SubModule match and the committed API snapshot, plus the
  orchestrator's green baseline; the test was not run in isolation.
- `pwsh tools/taom-src.ps1 path <Type>` failed in this environment (it printed a module-path error
  for `CampaignBehaviorDataStore` and `DefaultPartyNavigationModel`); I did not try to fix it. So the
  engine `IDataStore.SyncData` missing-key behaviour (COMP-02 side note on `InventorySearch`) stays
  UNVERIFIED, and the engine-DLL check for `PathReuseCache` used a byte grep of the installed DLLs,
  not a decompile.
- COMP-02: I did not audit every one of the 22 behaviors' constructors for DryIoc Transient pitfalls
  (for example a disposable behavior), nor the two-campaign smoke the fix sketch asks for.
- ARCH-02: I did not check whether the `CompanionTactics` container-level tests resolve every
  registration (none named `RegisterCompanionTacticsFeature` or `RegisterEditorCacheRebuildFeature`
  in `TAOM.Tests`, so none do by that route).
