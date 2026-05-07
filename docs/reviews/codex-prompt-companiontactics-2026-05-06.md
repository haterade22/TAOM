# Codex Adversarial Review Prompt — CompanionTactics

Dispatch with `/codex:adversarial-review --background` from the host terminal.

---

## Feature

CompanionTactics (Patch35) -- three sub-features in one module:
1. CompanionRoles -- equipment-based combat-role detector + tooltip decorator (11 roles)
2. FormationPresets -- saveable named OOB hero-to-formation assignments
3. BattleActionBar -- contextual action bar in field battles with 1-9 hotkeys

Ported from external developer drop at `Downloads/Features_fixed/CompanionTactics/` for Bannerlord v1.3.15.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

NB: this feature is ID-agnostic -- it uses no kingdom or culture identifiers. The cheatsheet is included for completeness only.

## READ FIRST

- `docs/feature-port-prompts/feature-7-companiontactics.md` -- the original port spec the human gave Claude
- `C:/Users/mikew/.claude/plans/feature-port-session-tidy-diffie.md` -- the approved plan file
- `Main/_Module/GUI/Prefabs/BattleActionBar.xml` and `OOBButtonsOverlay.xml` -- copied verbatim from source mod

## Known Suspects (CONFIRM or DISPUTE each)

1. **SaveableTypeDefiner BaseId 726900601 collision risk.** TAOM has no prior SaveableTypeDefiner -- this is the first. CareerSystem deliberately avoided it (see `CareerPersistenceBehavior.cs:27`). Confirm: is the BaseId really unique TAOM-wide, what happens on collision, and does `FormationPresetCampaignBehavior.SyncData` survive a deserialization fault gracefully without losing other features' state?

2. **OOBOverlayService reflection on `_dataSource` and `_isActive`.** Confirm v1.3.15 field names by decompiling `MissionGauntletOrderOfBattleUIHandler` from `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll`. Confirm the inert-mode fallback (`_inertMode = true` when fields are null) actually disables the overlay safely without crashing on subsequent ticks.

3. **CompanionRoleService cache leak.** `_cache` is `Dictionary<string, (long sig, CombatRole role)>` keyed by Hero.StringId. There is NO eviction path. Confirm: when a hero is killed/disabled/removed in the mid-campaign, does the cache entry leak forever? Are there OnHeroKilled / OnGameLoaded resets? If not, is the leak bounded enough to not matter in practice?

4. **`Patch35_Mission_OnTick` hot-path allocations.** This patch is on `Mission.OnTick(float, float, bool, bool)` which fires every frame. AGENTS.md "Per-tick allocations" rule mandates zero allocations / no LINQ / no closures in patch body OR service it calls. Read the patch + any service method it invokes and confirm.

5. **`MultiSelectionInquiryData` parameter order.** v1.3.15 ctor is `(title, description, elements, isExitShown, MIN, MAX, affirmText, negText, affirmAction, negAction, ...)`. Older mod templates have `MAX` before `MIN`. `OOBButtonsVM.ExecuteManagePresets` uses NAMED arguments (`maxSelectableOptionCount: 1, minSelectableOptionCount: 0`) -- confirm this is safe regardless of positional order.

6. **`TextInquiryData` ctor signature drift.** Same shape concern -- v1.3.15 has 11+ params with optional defaults. `OOBButtonsVM.ShowSavePrompt` uses named args. Confirm none of the named arg names conflict with v1.3.15.

7. **GauntletLayer ctor parameter order.** v1.3.15 sig is `(string name, int localOrder, bool shouldClear)`. Both `OOBOverlayService.Attach` and `BattleActionBarMissionView.OnMissionScreenInitialize` use the (string, int, bool) order. Confirm both are correct in v1.3.15.

8. **Manual `GetCaptainTooltip` patch wiring.** `OrderOfBattleHeroItemVM.GetCaptainTooltip()` is PRIVATE in v1.3.15. The manual patch is defined at `Patch35_OOBHeroItem_GetCaptainTooltip.cs` but the wiring in `Main/SubModule.cs` is currently AUTO-COMMENTED by a parallel-port hook (see "PARALLEL-PORT NOTE" below). Confirm the patch class itself is structurally correct (Postfix signature `(OrderOfBattleHeroItemVM __instance, ref List<TooltipProperty> __result)`) so when wired up, it actually works.

## PARALLEL-PORT NOTE (review-blocking observation)

The TAOM project is currently in a parallel-port environment where multiple feature ports run simultaneously. A build-watch hook auto-comments using directives, IoC registration, and SubModule wiring whenever the build fails. This caused several of the CompanionTactics integration calls to be commented out:

- `Main/IoC.cs:91` -- `// CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);` (commented)
- `Main/SubModule.cs:67-70` -- using directives commented
- `Main/SubModule.cs:379-381` -- `FormationPresetCampaignBehavior` registration commented
- `Main/SubModule.cs:431-437` -- manual `GetCaptainTooltip` patch wiring commented
- `Main/SubModule.cs:502` -- `BattleActionBarMissionView` registration commented

The CODE itself in `Main/Features/CompanionTactics/` is intact and the test assembly's CompanionTactics tests (74 tests) reportedly passed when last run by the originating feature-builder agent. Codex should review the CODE on its merits and flag the commented-out integration as a separate finding. The hook's commenting pattern is documented in `.claude/rules/harness-facts.md` (or should be after this review's RCA).

## File List (grouped)

### Adapters (new and extended)
- `Main/Adapters/IBattleEquipmentSnapshot.cs` (new)
- `Main/Adapters/BattleEquipmentSnapshot.cs` (new)
- `Main/Adapters/IHeroCombatAdapter.cs` (new)
- `Main/Adapters/HeroCombatAdapter.cs` (new)
- `Main/Adapters/IAgentCombatAdapter.cs` (new)
- `Main/Adapters/AgentCombatAdapter.cs` (new)
- `Main/Adapters/IFormationAdapter.cs` (extended +5 properties)
- `Main/Adapters/FormationAdapter.cs` (extended +5 property impls with TTL polearm/shield cache)

### Roles sub-feature
- `Main/Features/CompanionTactics/Roles/Models/CombatRole.cs`
- `Main/Features/CompanionTactics/Roles/ICompanionRoleService.cs`
- `Main/Features/CompanionTactics/Roles/CompanionRoleService.cs`
- `Main/Features/CompanionTactics/Roles/IRoleTooltipDecorator.cs`
- `Main/Features/CompanionTactics/Roles/RoleTooltipDecorator.cs`
- `Main/Features/CompanionTactics/Roles/Hooks/Patch35_PartyCharacterVM_RefreshValues.cs`
- `Main/Features/CompanionTactics/Roles/Hooks/Patch35_OOBHeroItem_RefreshValues.cs`
- `Main/Features/CompanionTactics/Roles/Hooks/Patch35_OOBHeroItem_GetCaptainTooltip.cs`

### FormationPresets sub-feature
- `Main/Features/CompanionTactics/FormationPresets/Models/HoNFormationPreset.cs` (SaveableField fields)
- `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs` (BaseId 726900601)
- `Main/Features/CompanionTactics/FormationPresets/Models/SaveResult.cs`
- `Main/Features/CompanionTactics/FormationPresets/IFormationPresetService.cs`
- `Main/Features/CompanionTactics/FormationPresets/FormationPresetService.cs`
- `Main/Features/CompanionTactics/FormationPresets/IHeroAutoAssigner.cs`
- `Main/Features/CompanionTactics/FormationPresets/HeroAutoAssigner.cs`
- `Main/Features/CompanionTactics/FormationPresets/IOrderOfBattleVMTracker.cs`
- `Main/Features/CompanionTactics/FormationPresets/OrderOfBattleVMTracker.cs`
- `Main/Features/CompanionTactics/FormationPresets/IOOBOverlayService.cs`
- `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs` (reflection + GauntletLayer attach)
- `Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs`
- `Main/Features/CompanionTactics/FormationPresets/Hooks/FormationPresetCampaignBehavior.cs`
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_OrderOfBattleVM_Ctor.cs`
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_OrderOfBattleVM_Finalize.cs`
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_OOBUIHandler_Tick.cs`
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_OOBUIHandler_Finalize.cs`
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs` (hot-path postfix)

### BattleActionBar sub-feature
- `Main/Features/CompanionTactics/BattleActionBar/Models/{ActionCategory,BattleAction,CavalryAction,FormationComposition,PolearmAction,RangedAction,ShieldAction,TroopStance}.cs`
- `Main/Features/CompanionTactics/BattleActionBar/IFormationCompositionAnalyzer.cs`
- `Main/Features/CompanionTactics/BattleActionBar/FormationCompositionAnalyzer.cs`
- `Main/Features/CompanionTactics/BattleActionBar/ITroopStanceManager.cs`
- `Main/Features/CompanionTactics/BattleActionBar/TroopStanceManager.cs`
- `Main/Features/CompanionTactics/BattleActionBar/IBattleActionBarService.cs`
- `Main/Features/CompanionTactics/BattleActionBar/BattleActionBarService.cs` (gates Volley on EnableVolleyFire)
- `Main/Features/CompanionTactics/BattleActionBar/UI/ActionButtonVM.cs`
- `Main/Features/CompanionTactics/BattleActionBar/UI/BattleActionBarVM.cs`
- `Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs` (MissionView, NOT a Harmony patch)
- `Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs` (CancelStanceOnMove)

### IoC + Settings
- `Main/Features/CompanionTactics/CompanionTacticsIoC.cs`
- `Main/Features/CompanionTactics/ICompanionTacticsSettingsProvider.cs`
- `Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs` (reads TaomSettings.Instance directly -- not via reflection)
- `Main/Features/TaomSettings.cs` (10 new MCM properties at GroupOrder 27/28/29 -- NOT 22/23/24, those are taken)
- `Main/IoC.cs` (registration call -- currently auto-commented)
- `Main/SubModule.cs` (Patch35 wiring -- partially auto-commented)

### GUI Prefabs
- `Main/_Module/GUI/Prefabs/BattleActionBar.xml`
- `Main/_Module/GUI/Prefabs/OOBButtonsOverlay.xml`

### Tests (74 passing, per feature-builder report)
- `TAOM.Tests/Features/CompanionTactics/Roles/CompanionRoleServiceTests.cs` (24 tests)
- `TAOM.Tests/Features/CompanionTactics/BattleActionBar/{FormationCompositionAnalyzerTests,BattleActionBarServiceTests,TroopStanceManagerTests}.cs`
- `TAOM.Tests/Features/CompanionTactics/FormationPresets/{FormationPresetServiceTests,HeroAutoAssignerTests}.cs`

## REQUIRED SECTIONS

### VANILLA CODE

For each Harmony patch and reflection target, decompile the v1.3.15 vanilla type via `ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/<dll>" -t "<type>"`. Paste each as a code block:

- `TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyCharacterVM.RefreshValues` (TaleWorlds.CampaignSystem.dll)
- `TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM` -- show RefreshValues + GetCaptainTooltip (TaleWorlds.MountAndBlade.dll)
- `TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleVM` -- show ctor + OnFinalize
- `TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler` -- show OnMissionScreenTick + private fields `_dataSource` / `_isActive`
- `TaleWorlds.MountAndBlade.Mission.OnTick(float, float, bool, bool)`
- `TaleWorlds.MountAndBlade.Formation.SetMovementOrder(MovementOrder)`
- `TaleWorlds.Engine.GauntletUI.GauntletLayer..ctor(string, int, bool)`
- `TaleWorlds.Library.MultiSelectionInquiryData..ctor` -- confirm parameter order (min before max in v1.3.15)
- `TaleWorlds.Library.TextInquiryData..ctor`
- `TaleWorlds.Core.WeaponClass` enum -- list all 32 values

### Feature-specific deep analysis

**A. SaveableType safety.** Read `FormationPresetSaveableTypeDefiner.cs` and confirm the BaseId 726900601 / class id 101 are unique TAOM-wide. Grep for `BaseId` and `SaveableTypeDefiner` across `Main/`. Confirm the definer adds `ConstructContainerDefinition` for `Dictionary<string,int>`, `Dictionary<int,int>`, `List<string>`, and `List<HoNFormationPreset>`. Verify `FormationPresetCampaignBehavior.SyncData` wraps the call in try/catch with graceful degradation (resets to empty list rather than crashing).

**B. Hot-path allocation audit.** Read `Patch35_Mission_OnTick.cs` postfix body. Confirm: lazy-cached `??=` settings provider (one IoC.Resolve ever); single bool read; early return; NO `new`, NO LINQ, NO closure, NO foreach over heap collection. Then trace what service method it calls (if any) and verify same constraints apply.

**C. Reflection robustness.** Read `OOBOverlayService.EnsureInitialized` -- it caches `FieldInfo` for `_isActive` and `_dataSource`. Confirm: (1) if either FieldInfo is null, `_inertMode = true` and a single warning logs; (2) subsequent `OnTick` calls return immediately when `_inertMode` is true; (3) the `Detach` path is safe to call when nothing is attached.

**D. CombatRole color coverage.** `CompanionRoleService.GetRoleColor` -- I just patched this to add explicit cases for `OneHanded` (4287090411u, ShieldInfantry tone) and `Slinger` (4289583334u, Skirmisher tone). Confirm these are sensible visual choices and no other role is missing a case.

**E. State leak audit.** For every static dictionary, instance cache, or per-mission scoped state in CompanionTactics:
- `CompanionRoleService._cache` -- never cleared. Confirm whether OnGameLoaded reset is needed (e.g., a save file with stale Hero StringId after a mod transition).
- `TroopStanceManager._stances` -- cleared by `ClearAllStances()` called from `BattleActionBarMissionView.OnMissionScreenFinalize`. Confirm.
- `FormationAdapter._polearmShieldCache` -- instance-level, lifetime tied to FormationAdapter lifetime. Confirm `BattleActionBarMissionView.RefreshFromSelectedFormation` constructs a fresh FormationAdapter per refresh -- if not, the cache lifetime needs review.
- `_currentPreset` (if any) -- search `FormationPresetService.cs` for static state.

**F. Observation state machine -- `OOBOverlayService._wasActive`.** Walk the state-1 (init) -> state-2 (first observe before OOB opens) -> state-3 (open) -> state-4 (close) cycle. The `_wasActive = false` initial AND the OOB-closed state both have `isActive = false`. Confirm this is NOT a sentinel-collision (per `harmony-patches.md` "Static State Machines" rule). Specifically confirm Detach only fires on the `true -> false` transition, not on `false (init) -> false (still closed)`.

**G. UI binding completeness.** Read both prefabs and verify every `@Property` and `Command.Click=` reference resolves to a `[DataSourceProperty]` or public `void` method on the corresponding VM:
- `OOBButtonsOverlay.xml` -> `OOBButtonsVM` (IsVisible, PresetsButtonText, ExecuteAssignCharacters, ExecuteManagePresets)
- `BattleActionBar.xml` -> `BattleActionBarVM` + `ActionButtonVM` (IsVisible, FormationName, ActionButtons collection; CategoryColor, IsActive, DisplayText, Hotkey, ExecuteAction)

**H. Adapter pattern boundary verification.** Services in CompanionTactics MUST NOT reference sealed TaleWorlds types. Confirm:
- `CompanionRoleService` accepts only `IHeroCombatAdapter`
- `BattleActionBarService` accepts only `IFormationAdapter`
- `FormationCompositionAnalyzer` accepts only `IFormationAdapter`
- `TroopStanceManager` accepts only `int formationIndex` (primitive)
- `FormationPresetService` accepts only `HoNFormationPreset` (POCO)
- `HeroAutoAssigner` accepts only `IHeroCombatAdapter` + primitive `formationClass`
Boundary classes (Hooks/, MissionView, ViewModels, OOBOverlayService) ARE allowed to see sealed types.

### CONFIG CROSS-REFERENCE

This feature has no XML/JSON config (settings live in MCM via TaomSettings.cs). Skip ID cross-referencing.

### FINDINGS OR OBSERVATIONS

For each finding, label severity (P1 = critical / P2 = high / P3 = medium / P4 = low / OBS = observation), provide file path + line number, paste 5-15 lines of vanilla code or TAOM code, and explain the bug or risk.

## QUALITY GATES

A high-quality review:
1. Decompiles every Harmony patch target and posts the v1.3.15 vanilla signature
2. Cross-references the in-tree code against the decompiled vanilla, NOT against the older external mod's source
3. Verifies every Known Suspect with explicit CONFIRMED or DISPUTED verdict
4. Reports findings even when they confirm Claude's design (we want to validate, not just find faults)
5. Specifies exact line numbers for every finding (no "around line X")
6. Distinguishes P1 (build/save corruption / null deref / API broken) from P3/P4 (style, naming)

## Prior review lessons

SUCCESSES:
- Vanilla decompilation caught the GetCaptainTooltip private-method issue early
- Field-name reflection cross-reference (`_dataSource` vs `_orderOfBattleVM`) caught the v1.3.15 rename
- Setting consumer trace caught dead MCM toggles and forced explicit decisions

FAILURES:
- Codex assumed empire=Rohan in past reviews (it is Dunland). Trust the cheatsheet above.
- Codex flagged vanilla-matching code as bugs in past reviews. If the code matches the decompiled vanilla, it is NOT a bug regardless of what the older mod template did.
- Codex sometimes reports "I did not find this method" without grepping the codebase. Always grep before claiming missing.

## Output

Write findings to `docs/reviews/codex-adversarial-companiontactics-2026-05-06.md`. Begin with a CONFIRMED/DISPUTED verdict for each Known Suspect, then list all findings ordered P1 -> P4 -> OBS.
