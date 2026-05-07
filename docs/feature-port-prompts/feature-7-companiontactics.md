# Feature Port Session: CompanionTactics (the big one — 3 sub-features)

You are porting feature #7 of 7 from the external-developer drop at `Downloads/Features_fixed/CompanionTactics/` into TAOM's `Main/Features/CompanionTactics/`. This is the largest feature: it bundles **three independent sub-features** under one DLL. The other 6 features are tracked separately. Don't touch them.

## Why this is last

This feature is the biggest (~30+ files in the planned scaffold). Doing it last lets you:
- Reuse `IFormationAdapter` (introduced by feature 2) — already shipped
- Apply patterns from features 1, 2, 3, 5, 6 — they've been rehearsed
- Run the full SaveableType infrastructure from feature 6 EquipPresets as precedent

## Prerequisites — read before writing any code

1. **The integration plan**: `C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md` — section "6. CompanionTactics (the big one — three sub-features)" has the planned folder layout.

2. **This prompt** — end to end.

3. **Pattern templates** — read all five completed features' folders before starting:
   - [Main/Features/SiegeDismount/](../../Main/Features/SiegeDismount/)
   - [Main/Features/MixedFormations/](../../Main/Features/MixedFormations/)
   - [Main/Features/SmartCavalryAI/](../../Main/Features/SmartCavalryAI/)  *(if completed)*
   - [Main/Features/QuickActions/](../../Main/Features/QuickActions/)  *(if completed)*
   - [Main/Features/EquipPresets/](../../Main/Features/EquipPresets/)  *(if completed)*

   Specifically reuse:
   - `IFormationAdapter` from feature 2
   - SaveableType pattern from feature 6 EquipPresets
   - Mission singleton + per-formation cache from feature 2 MixedFormations

4. **The decompiled source you're porting**:
   `C:/Users/mikew/Downloads/Features_fixed/_decompiled/CompanionTactics/CompanionTactics.decompiled.cs`

   This is large (~2400 words of analysis from the original Explore agent). Read it end-to-end. Three sub-features inside ONE namespace:
   - `CompanionRoles` — detects 7 combat roles (Melee/Ranged/Archer/Crossbow/Cavalry/Healer/Support) from equipment + skills; appends role to character tooltips and OOB hero items
   - `FormationPresets` — saves/loads OOB hero-to-formation assignments as named presets; injects buttons into the OOB screen
   - `BattleActionBar` — context-sensitive on-screen action bar during field battles (1-9 hotkeys for stance toggles)

5. **GUI prefabs**: copy these two verbatim to `Main/_Module/GUI/Prefabs/`:
   - `Downloads/Features_fixed/CompanionTactics/GUI/Prefabs/BattleActionBar.xml`
   - `Downloads/Features_fixed/CompanionTactics/GUI/Prefabs/OOBButtonsOverlay.xml`

   Both use only vanilla brushes; no custom sprites needed.

## Goal in one sentence

Add three battle-control quality-of-life features under one TAOM feature module: companion role tooltips, OOB formation presets, and a context-sensitive battle action bar.

## CRITICAL: BattleActionBar is UI-only — do NOT add stance enforcement

The original developer's `TroopStanceManager` records stances (BracedForCavalry / PikeWall / Testudo / LineCharge / Skirmish) when the player presses the action bar buttons. **The stance state is never enforced on actual formation behavior** — the buttons are display-only. The integration plan explicitly preserves this: "BattleActionBar stances are display-only; if the user wants real stance enforcement, that's a follow-up feature."

**Port the UI-only behavior verbatim.** Do NOT add stance enforcement (e.g., calling `formation.OrderBraceForCavalry()`) — the UI-only design is intentional Phase 1 scope. Document this clearly in the feature doc.

## Architecture — what to build

The integration plan specifies splitting into three internal sub-feature folders, each with its own service. Single feature module (`Main/Features/CompanionTactics/`), single IoC entry, three independently-toggleable services. **Match the plan's folder layout exactly.**

### Files to create — verbatim from the integration plan

```
Main/Features/CompanionTactics/
├── CompanionTacticsIoC.cs                   ← single IoC entry, registers all three services
│
├── Roles/
│   ├── ICompanionRoleService.cs             ← detects Melee/Ranged/Archer/Crossbow/Cavalry/Healer/Support
│   ├── CompanionRoleService.cs              ← port of CompanionRoleDetector
│   └── Hooks/
│       ├── Patch35_PartyCharacterVM_RefreshValues.cs
│       ├── Patch35_PartyTroopTuple_RefreshState.cs
│       ├── Patch35_OOBHeroItem_Refresh.cs   ← targets RefreshValues (NOT RefreshInformation — TAOM Patch23 owns that)
│       └── Patch35_OOBHeroItem_GetCaptainTooltip.cs
│
├── FormationPresets/
│   ├── IFormationPresetService.cs           ← CRUD over presets, hero auto-assignment
│   ├── FormationPresetService.cs
│   ├── HeroAutoAssigner.cs                  ← consumes ICompanionRoleService
│   ├── Models/
│   │   ├── HoNFormationPreset.cs            ← SaveableType id 726900601, verify uniqueness
│   │   └── FormationPresetSaveableTypeDefiner.cs
│   ├── Hooks/
│   │   ├── FormationPresetCampaignBehavior.cs
│   │   ├── Patch35_OrderOfBattleVM_Ctor.cs
│   │   ├── Patch35_OrderOfBattleVM_Finalize.cs
│   │   ├── Patch35_OOBUIHandler_Tick.cs
│   │   ├── Patch35_OOBUIHandler_Finalize.cs
│   │   └── Patch35_Mission_OnTick.cs
│   └── UI/
│       └── OOBButtonsVM.cs
│
└── BattleActionBar/
    ├── IBattleActionBarService.cs            ← composes contextual buttons from ITroopStanceManager + IFormationCompositionAnalyzer
    ├── BattleActionBarService.cs
    ├── ITroopStanceManager.cs                ← per-formation stance dict
    ├── TroopStanceManager.cs                 ← scoped to mission (DISPLAY-ONLY — no behavior enforcement, see warning above)
    ├── FormationCompositionAnalyzer.cs       ← reads ranged/polearm/shields/cavalry counts
    ├── Models/
    │   ├── ActionCategory.cs                 ← enum (Ranged/Polearm/Shield/Cavalry)
    │   ├── RangedAction.cs
    │   ├── PolearmAction.cs
    │   ├── ShieldAction.cs
    │   ├── CavalryAction.cs
    │   └── TroopStance.cs
    ├── UI/
    │   ├── BattleActionBarVM.cs
    │   └── ActionButtonVM.cs
    └── Hooks/
        └── BattleActionBarMissionView.cs     ← MissionView (NOT a Harmony patch); attaches GauntletLayer "BattleActionBar"
```

Plus:
- `Main/_Module/GUI/Prefabs/BattleActionBar.xml` (copy verbatim)
- `Main/_Module/GUI/Prefabs/OOBButtonsOverlay.xml` (copy verbatim)
- `Main/Adapters/IFormationAdapter.cs` — REUSE (already exists from feature 2)
- `Main/Adapters/IAgentCombatAdapter.cs` + `AgentCombatAdapter.cs` — extend `IAgentAdapter` if needed for `IsRanged`, `Equipment[slot]` access; otherwise build a small new adapter
- Tests in `TAOM.Tests/Features/CompanionTactics/`:
  - `CompanionRoleServiceTests.cs`
  - `FormationCompositionAnalyzerTests.cs`
  - `BattleActionBarServiceTests.cs`
  - `HeroAutoAssignerTests.cs`
  - `FormationPresetServiceTests.cs` — saveable round-trip + missing-hero pruning

### Adapter usage

| Adapter | Source | Why |
|---|---|---|
| `IFormationAdapter` | EXISTING (feature 2) | OOB ticks read formation state; FormationCompositionAnalyzer reads ranged/polearm/shield/cavalry counts. Extend with any missing properties; do NOT duplicate. |
| `IAgentCombatAdapter` | NEW (or extend `IAgentAdapter`) | CompanionRoleService reads `Hero.CharacterObject.GetSkillValue`, `Equipment[EquipmentIndex.WeaponItemBeginSlot..WeaponItemEndSlot]`, weapon class detection. Bonus: this is also the cleanest way to get `Character.IsRanged` if not already on `IAgentAdapter`. |
| `IOrderOfBattleAdapter` | NEW — wraps `OrderOfBattleVM` reflection (the original injects buttons via reflection on the VM's protected fields) | OOB integration is reflection-heavy; consolidate it. |

### Harmony patches

Reserve **`Patch35_CompanionTactics`** as a single category covering 7 patches:

1. `PartyCharacterVM.RefreshValues` (Postfix) — append role to tooltip
2. `PartyTroopTupleButtonWidget.RefreshState` (Postfix) — append role indicator to widget text
3. `OrderOfBattleHeroItemVM.RefreshValues` (Postfix) — cache tooltip data + add role to OOB hero item display. **NOTE: target `RefreshValues`, NOT `RefreshInformation` — the latter is owned by TAOM Patch23 (BannerColorPersistence).**
4. `OrderOfBattleHeroItemVM.GetCaptainTooltip` (Postfix) — append role info to captain tooltip
5. `MissionGauntletOrderOfBattleUIHandler.OnMissionScreenTick` (Postfix) — inject preset buttons into OOB UI every frame
6. `MissionGauntletOrderOfBattleUIHandler.OnMissionScreenFinalize` (Postfix) — cleanup
7. `Mission.OnTick` (Postfix) — sync formation presets and hero assignments during battle

Wire in `Main/SubModule.cs` `OnGameInitializationFinished`:
```csharp
_harmony.PatchCategory("Patch35_CompanionTactics");
```

### MissionBehavior + MissionView wiring

`SubModule.cs` `OnMissionBehaviorInitialize`:
```csharp
mission.AddMissionBehavior(new BattleActionBarMissionView());  // a MissionView, attaches GauntletLayer when the mission is a field battle
```

The MissionView checks `Mission.Mode == MissionMode.Battle` AND `Mission.IsSiegeBattle == false` (field battles only) before attaching its layer. Otherwise no-op.

### CampaignBehavior

`SubModule.cs` `OnGameStart` after EquipmentPresetCampaignBehavior:
```csharp
campaignStarter.AddBehavior(new FormationPresetCampaignBehavior(IoC.Resolve<IFormationPresetService>(), IoC.Resolve<IModLogger>()));
```

### MCM settings — append to `Main/Features/TaomSettings.cs`

Three sub-features → three groups, each with their own toggle:

```csharp
// --- Battle Tactics / Companion Roles ---

[SettingPropertyGroup("Battle Tactics/Companion Roles", GroupOrder = 23)]
[SettingPropertyBool("Enable Companion Role Tooltips", Order = 0,
    HintText = "Append detected combat role (Melee/Ranged/Archer/etc.) to companion/troop tooltips on the party screen.")]
public bool EnableCompanionRoleTooltips { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Companion Roles")]
[SettingPropertyBool("Enable OOB Role Display", Order = 1,
    HintText = "Show role indicators on hero items in the Order of Battle screen.")]
public bool EnableOOBRoleDisplay { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Companion Roles")]
[SettingPropertyBool("Companion Roles Debug Mode", Order = 2,
    HintText = "Show diagnostic [CompanionRoles] messages on the in-game HUD.")]
public bool CompanionRolesDebug { get; set; } = false;

// --- Battle Tactics / Formation Presets ---

[SettingPropertyGroup("Battle Tactics/Formation Presets", GroupOrder = 24)]
[SettingPropertyBool("Enable Formation Presets", Order = 0,
    HintText = "Save/load named OOB hero-to-formation assignments per campaign.")]
public bool EnableFormationPresets { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Formation Presets")]
[SettingPropertyInteger("Max Formation Presets", 1, 20, Order = 1,
    HintText = "Maximum saved formation presets per campaign. Default: 10.")]
public int MaxFormationPresets { get; set; } = 10;

[SettingPropertyGroup("Battle Tactics/Formation Presets")]
[SettingPropertyBool("Formation Presets Debug Mode", Order = 2,
    HintText = "Show diagnostic [FormationPresets] messages.")]
public bool FormationPresetsDebug { get; set; } = false;

// --- Battle Tactics / Battle Action Bar ---

[SettingPropertyGroup("Battle Tactics/Battle Action Bar", GroupOrder = 25)]
[SettingPropertyBool("Enable Battle Action Bar", Order = 0,
    HintText = "Show contextual action bar during field battles (1-9 hotkeys for stance toggles). NOTE: stances are display-only in Phase 1 — they record state but do not change formation behavior.")]
public bool EnableBattleActionBar { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
[SettingPropertyBool("Cancel Stance On Move", Order = 1,
    HintText = "Auto-clear stance when the formation receives a movement order.")]
public bool CancelStanceOnMove { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
[SettingPropertyBool("Enable Volley Fire", Order = 2,
    HintText = "Include 'Volley Fire' as a ranged action option.")]
public bool EnableVolleyFire { get; set; } = true;

[SettingPropertyGroup("Battle Tactics/Battle Action Bar")]
[SettingPropertyBool("Battle Action Bar Debug Mode", Order = 3,
    HintText = "Show diagnostic [BattleActionBar] messages.")]
public bool BattleActionBarDebug { get; set; } = false;
```

### IoC

```csharp
using TAOM.Features.CompanionTactics;
// ...
CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);
```

`CompanionTacticsIoC.cs` registers all three sub-services + their helpers (Reuse.Singleton each).

## Cross-session memory rules that apply to THIS feature

| Memory | How it applies here |
|---|---|
| `feedback_substring_keyword_matches_external_data.md` | NOT APPLICABLE — feature uses no scene-name matching. |
| `feedback_adapter_modifier_preserving_overload.md` | NOT APPLICABLE — feature reads equipment for role detection (read-only); doesn't transfer items. |
| `feedback_user_facing_promise_must_match_code.md` | **APPLIES STRONGLY.** The original module ships ~9 MCM settings across the 3 sub-features. Trace EACH ONE to a consumer: `CancelStanceOnMove` (does the StanceManager actually clear on Move order? OR is it dead?), `EnableVolleyFire` (gates the ranged-action button list?), `MaxFormationPresets` (enforced on Save?). Audit each, drop dead ones or implement them. The plan explicitly notes that BattleActionBar stances are display-only — that IS the user-facing promise the MCM hint text must match (note the explicit Phase 1 caveat I added to `EnableBattleActionBar` above). |

## Per-feature gotchas

1. **Three sub-features, three independent toggles.** Each sub-service must short-circuit when its toggle is off. Patches stay registered but their bodies first-line-check the toggle and `return;`.

2. **TroopStanceManager scope = mission.** Cleared on `OnEndMission` of the `BattleActionBarMissionView`. Don't let stance state leak across missions.

3. **OOB UI injection via reflection.** The original `OOBButtonInjector` injects buttons into `MissionGauntletOrderOfBattleUIHandler` via reflection on protected fields. Verify the field names in v1.3.15 with `ilspycmd`; likely candidates: `_orderOfBattleVM`, `_dataSource`, `_ootBattleLayer`. Wrap the reflection in `IOrderOfBattleAdapter`.

4. **SaveableType ID 726900601.** Original uses this for `HoNFormationPreset`. Verify uniqueness across TAOM (grep for `BaseId` in Main/) — feature 6 EquipPresets uses 726900501 as a different range. If feature 6 has been completed first, the SaveableType registration order matters: register CompanionTactics' definer AFTER EquipPresets'.

5. **Mission.OnTick Postfix.** This patch fires every mission tick. The patch body MUST be lean — first-line toggle check, then a single service call. **Per AGENTS.md "Per-tick allocations in BT-tick hot paths" rule**: NO `new List<>`, NO LINQ, NO closure allocation in the patch body OR in the service method it calls. Cache everything that can be cached.

6. **CompanionRoleService weapon-class detection.** Reads each weapon's `WeaponClass` enum. Verify all 25+ weapon-class values are mapped to roles in v1.3.15:
   ```bash
   ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.WeaponClass" 2>&1 | head -50
   ```
   Any weapon class not mapped should fall back to a default role with a `LogWarning` flagging the unmapped class.

7. **TAOM Patch23 conflict on OOBHeroItemVM.** Patch23 patches `OrderOfBattleHeroItemVM.RefreshInformation`. CompanionTactics targets `OrderOfBattleHeroItemVM.RefreshValues` — DIFFERENT method. Verify no collision but document the fact in feature doc.

8. **Hotkeys 1–9 conflict check.** The BattleActionBar binds 1–9. TAOM doesn't currently bind 1–9 (verified via grep at start of port queue). But MixedFormations binds `L`, FiefManagement binds `F6`, SiegeDismount has none — NEW collisions could emerge. Re-verify before shipping:
   ```bash
   grep -rni "InputKey\.D[1-9]\|IsKeyDown.*[1-9]" Main/ --include "*.cs"
   ```

9. **`Mission.Mode` check.** The MissionView attaches its layer only on `MissionMode.Battle`. Verify the enum value in v1.3.15:
   ```bash
   ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.MissionMode" 2>&1
   ```

## Verification of v1.3.15 API surface

```bash
# OrderOfBattleHeroItemVM
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM" 2>&1 | grep -E "RefreshValues|RefreshInformation|GetCaptainTooltip"

# MissionGauntletOrderOfBattleUIHandler
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll" -t "TaleWorlds.MountAndBlade.GauntletUI.Singleplayer.MissionGauntletOrderOfBattleUIHandler" 2>&1 | grep -E "OnMissionScreenTick|OnMissionScreenFinalize"

# PartyTroopTupleButtonWidget
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll" -t "TaleWorlds.MountAndBlade.GauntletUI.Widgets.Party.PartyTroopTupleButtonWidget" 2>&1 | grep RefreshState

# Hero.CharacterObject + skill access
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Hero" 2>&1 | grep -E "CharacterObject|GetSkillValue"

# WeaponClass enum
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.WeaponClass" 2>&1
```

## Acceptance gates

- Build clean — 0 errors
- Tests: at least **50 tests** covering: 
  - CompanionRoleService: at least 7 role assignments (one per role) + edge cases (no equipment, dual-class character, healer-skill detection)
  - FormationCompositionAnalyzer: each composition flag (HasRanged/HasPolearm/HasShields/HasCavalry) positive + negative
  - BattleActionBarService: contextual button list per composition; per-MCM-toggle effect on button list (e.g., volley fire off → no volley button)
  - HeroAutoAssigner: each role → expected formation slot
  - FormationPresetService: save/load/update/delete; missing-hero pruning on load; max-presets enforcement; SaveableType round-trip
  - TroopStanceManager: set/clear/cancel-on-move; mission-end clearance
- Full suite stays green
- `docs/features/companion-tactics.md` from TEMPLATE — call out the **three sub-features**, the UI-only-stances design decision, the OOB UI injection mechanism
- CHANGELOG.md entry at top
- `/deep-review CompanionTactics` and `/review-codex CompanionTactics` — fix every confirmed finding (this is the largest feature; expect 5–10 findings between the two reviews)
- New feedback memories if RCA produced any

**Do NOT commit** — leave dirty for in-game test.

## Verification — in-game golden path

**Companion Roles:**
1. Open party screen → hover a companion → tooltip shows role ("Cavalry", "Archer", etc.) appended to character description.
2. Open Order of Battle in a battle → hero items show role indicators next to names.

**Formation Presets:**
3. Enter a battle → open OOB → assign heroes to formations manually.
4. New buttons in OOB: Save/Load/Delete preset.
5. Save preset "Defensive". Reset OOB. Load "Defensive" → assignments restored.
6. Save campaign → reload → preset persists.

**Battle Action Bar:**
7. Enter a FIELD battle (NOT siege) → action bar appears at the bottom of the screen.
8. Select a formation with mixed troops → action bar shows contextual buttons (Hold Fire, Brace, Shield Wall, etc.) based on composition.
9. Press 1–9 hotkeys → button highlights (recall: stances are DISPLAY-ONLY in Phase 1; this is expected).
10. Order the formation to move → if `CancelStanceOnMove = true`, stance highlight clears.
11. Disable round-trip (each toggle):
    - `EnableCompanionRoleTooltips = false` → tooltips revert to vanilla
    - `EnableFormationPresets = false` → preset buttons absent in OOB
    - `EnableBattleActionBar = false` → no bar appears in field battles

## Final report format

```
CompanionTactics port complete (3 sub-features).
- Files created: [count] (likely 30+)
- Files modified: TaomSettings.cs (~10 settings), IoC.cs, SubModule.cs (Patch35 + behavior + MissionView registration)
- Sub-features: CompanionRoles, FormationPresets, BattleActionBar — all 3 independently toggleable
- IFormationAdapter REUSED from feature 2 (extended with [list of new properties if any])
- BattleActionBar stances DISPLAY-ONLY confirmed in feature doc + MCM hint text
- SaveableType IDs: BaseId=726900601 — uniqueness verified across Main/ (no collision with feature 6)
- WeaponClass mappings: [N classes mapped, M unmapped — log fallback]
- OOB UI injection: [reflection probe results]
- Tests: NN/NN CompanionTactics tests pass; XXXX/XXXX total
- /deep-review verdict: [PASS / N findings fixed]
- /review-codex verdict: [PASS / N findings fixed]
- New feedback memories codified: [list]
- Awaiting in-game verification before commit.
```

After this feature passes verification: **the entire 7-feature port is complete.** Run the cross-feature stress test (todo #9): all 7 features enabled, free-roam map for 5 in-game days, check `rgl_log.txt` for any cross-feature interactions or `[Harmony]` errors.
