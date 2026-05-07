# Codex Adversarial Prompt -- EquipPresets (2026-05-06)

You are an adversarial reviewer. Your job is to find bugs that Claude missed in TAOM's EquipPresets feature port. Treat every claim with skepticism. Verify with code. Decompile vanilla TaleWorlds APIs from the INSTALLED v1.3.15 DLLs at `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/` -- the pre-decompiled folder at `E:\Decompiled_Bannerlord\` is v1.4 and unreliable for signature checks.

## Output

Write your full review to: `docs/reviews/codex-adversarial-equippresets-2026-05-06.md`

## TAOM ID CHEATSHEET (use to spot config ID mismatches)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

1. Feature doc: `docs/features/equip-presets.md` -- full architecture writeup, known limitations
2. CHANGELOG entry at top of `CHANGELOG.md` -- summary of what shipped + design decisions
3. Plan: `C:/Users/mikew/.claude/plans/feature-port-session-gentle-harbor.md` -- the originally-approved design

## What was built

Per-hero equipment-preset Save / Load / Update / Delete on the inventory screen, persisted across the campaign save with full `ItemModifier` round-trip preservation. Three MCM toggles, GauntletLayer overlay, three Harmony patches under `Patch33_EquipPresets`, one CampaignBehavior, six new files in `Main/Adapters/`, a SaveableTypeDefiner with BaseId 726900501.

## Known suspects -- Codex must CONFIRM or DISPUTE each

1. **`PromptSaveName` always sets `includeMount=true`.** The saveable model exposes `IncludesMount` for forward compatibility, but the user has no UI to set it false. Doc disclosure: "Known limitation". Is the disclosure adequate, or should the flag itself be removed (drop dead config)?

2. **`SetTextVariable(string, string)` chained calls in `PresetsOverlayVM`.** Claude verified via ilspy that the v1.3.15 overload returns `this` (chainable). Confirm by independently decompiling `TaleWorlds.Localization.TextObject.SetTextVariable(string, string)`.

3. **`InventoryScreenAdapter.ActiveHeroStringId`** uses cached reflection to read private `SPInventoryVM._currentCharacter`. Edge case: companion is selected but `CharacterObject.HeroObject` returns null -- adapter returns null. Service treats null as `NoActiveHero`. Verify there is no path where a non-null CharacterObject with a null HeroObject leaks into the dictionary key.

4. **`EquipmentPresetCampaignBehavior.OnGameLoaded` orphan pruning.** Builds the live set from `Hero.AllAliveHeroes`. Edge cases: dead heroes removed from collection, captured/fugitive heroes still in the collection. Service guards against empty live-set (treats as "don't prune"). Confirm: are there transient load states where AllAliveHeroes returns null or empty? Would pruning then incorrectly drop legitimate presets?

5. **Modifier preservation chain.** Claude's claim: every step uses the lossless setter. Verify by tracing:
   - SAVE: `EquipmentSlotAdapter.Capture` reads `equipment[i].ItemModifier?.StringId` -> `EquippedSlotSnapshot.ItemModifierStringId` -> `HoNPresetItemReference.ItemModifierStringId` -> `[SaveableProperty(3)]` persists.
   - LOAD: `HoNPresetItemReference.ItemModifierStringId` -> `_modifierLookup.ExistsOrEmpty` (validate) -> `_slots.ApplySlot(itemId, modifierId)` -> `MBObjectManager.GetObject<ItemModifier>` -> `new EquipmentElement(item, modifier)` -> `equipment[(EquipmentIndex)slotIndex] = element` (lossless setter via `Equipment.this[EquipmentIndex].set`).
   - Verify NO step drops the modifier. Verify the v1.3.15 setter is genuinely lossless (decompile `TaleWorlds.Core.Equipment.this[EquipmentIndex].set` -- expected: assigns to `_itemSlots[(int)index]` directly).

6. **Patch33_GauntletInventoryScreen z-order = 1000.** Claude says vanilla's InventoryScreen layer is z-order 15. Decompile `SandBox.GauntletUI.GauntletInventoryScreen.OnInitialize` (DLL: `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.GauntletUI.dll`) and confirm. Also confirm 1000 doesn't collide with any other TAOM `GauntletLayer` z-orders (grep `Main/` for `new GauntletLayer(`).

## Files to review (group by category)

### Service layer
- `Main/Features/EquipPresets/IEquipmentPresetService.cs`
- `Main/Features/EquipPresets/EquipmentPresetService.cs`
- `Main/Features/EquipPresets/IEquipPresetsSettingsProvider.cs`
- `Main/Features/EquipPresets/EquipPresetsSettingsProvider.cs`

### Adapters
- `Main/Adapters/IEquipmentSlotAdapter.cs`
- `Main/Adapters/EquipmentSlotAdapter.cs`
- `Main/Adapters/IItemModifierLookupAdapter.cs`
- `Main/Adapters/ItemModifierLookupAdapter.cs`
- `Main/Adapters/IInventoryScreenAdapter.cs`
- `Main/Adapters/InventoryScreenAdapter.cs`

### Models / saveable types
- `Main/Features/EquipPresets/Models/HoNEquipmentPreset.cs`
- `Main/Features/EquipPresets/Models/HoNPresetItemReference.cs`
- `Main/Features/EquipPresets/Models/EquippedSlotSnapshot.cs`
- `Main/Features/EquipPresets/Models/PresetLoadResult.cs`
- `Main/Features/EquipPresets/Models/Outcomes.cs`
- `Main/Features/EquipPresets/Models/PresetSaveableTypeDefiner.cs`

### Hooks (Patch33 + behavior)
- `Main/Features/EquipPresets/Hooks/EquipmentPresetCampaignBehavior.cs`
- `Main/Features/EquipPresets/Hooks/Patch33_GauntletInventoryScreen.cs`
- `Main/Features/EquipPresets/Hooks/Patch33_SPInventoryVMRefresh.cs`

### UI / IoC
- `Main/Features/EquipPresets/UI/PresetsOverlayVM.cs`
- `Main/Features/EquipPresets/EquipPresetsIoC.cs`
- `Main/_Module/GUI/Prefabs/PresetsOverlay.xml`

### Wiring
- `Main/IoC.cs` -- look for `EquipPresetsIoC.RegisterEquipPresetsFeature(container);`
- `Main/SubModule.cs` -- look for `_harmony.PatchCategory("Patch33_EquipPresets");` and `campaignStarter.AddBehavior(IoC.Resolve<EquipmentPresetCampaignBehavior>());`
- `Main/Features/TaomSettings.cs` -- 3 properties at GroupOrder 33

### Tests
- `TAOM.Tests/Features/EquipPresets/EquipmentPresetServiceTests.cs`
- `TAOM.Tests/Features/EquipPresets/HoNEquipmentPresetTests.cs`
- `TAOM.Tests/Features/EquipPresets/PresetSaveableTypeDefinerTests.cs`
- `TAOM.Tests/Features/EquipPresets/EquipPresetsSettingsProviderTests.cs`

## REQUIRED SECTIONS in your review

### 1. VANILLA CODE

For each Harmony patch and adapter that touches a TaleWorlds API, paste the relevant decompiled vanilla code in a code block. Required:
- `TaleWorlds.Core.Equipment.this[EquipmentIndex] {get; set;}` -- confirm lossless setter
- `TaleWorlds.Core.EquipmentElement(ItemObject, ItemModifier, ItemObject, bool)` ctor -- confirm signature
- `SandBox.GauntletUI.GauntletInventoryScreen.OnInitialize / OnFinalize` -- confirm method names + access modifier (Harmony patches by string)
- `TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM.RefreshValues` -- confirm public override
- `TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM._currentCharacter` -- private CharacterObject field, exists in v1.3.15
- `TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM.EquipmentMode` -- public int property (0=Civilian, 1=Battle, 2=Stealth per decompiled enum at line 26)
- `TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPItemVM.IsLocked` -- public setter
- `TaleWorlds.SaveSystem.SaveableTypeDefiner.AddClassDefinition(Type, int, IObjectResolver)` -- protected, signature
- `TaleWorlds.SaveSystem.SaveableTypeDefiner.ConstructContainerDefinition(Type)` -- protected
- `TaleWorlds.SaveSystem.SaveablePropertyAttribute(short)` -- ctor parameter is `short`, NOT `int` -- audit `[SaveableProperty(N)]` usages for narrowing risk (none of EquipPresets's are above 6, so no actual issue, but flag the latent class)
- `TaleWorlds.Engine.GauntletUI.GauntletLayer(string, int, bool)` ctor signature
- `TaleWorlds.CampaignSystem.Hero.BattleEquipment` and `Hero.CivilianEquipment` -- confirm separate fallbacks (not both falling back to DeadBattleEquipment -- see review #34's P1)

### 2. DEEP ANALYSIS

a) **Hot-path performance.** `Patch33_SPInventoryVMRefresh.Postfix` fires on every `SPInventoryVM.RefreshValues`. Claude added static `??=` caching for the IoC.Resolve calls. Trace whether ANY allocation, LINQ, or closure can occur on the postfix fast path. Flag closure captures in `try/catch` blocks.

b) **Save format compatibility.** A campaign saved with v1 of EquipPresets and loaded with a future v2: assert that:
   - Every `[SaveableProperty(N)]` index in `HoNEquipmentPreset` (1..6) and `HoNPresetItemReference` (1..3) is stable.
   - The `BaseId 726900501` is stable.
   - `Dictionary<string, List<HoNEquipmentPreset>>` ContainerDefinition can decode pre-existing data even if a v2 adds fields.
   - `[SaveableProperty]` index numbering goes through `short` per the attribute -- confirm via decompile.

c) **CampaignBehavior idempotence.** EquipmentPresetCampaignBehavior is `Reuse.Singleton` -- the same instance survives across campaigns within a single Bannerlord process. Trace what happens when the player exits campaign A, starts campaign B: `OnNewGameCreated` fires `RestoreFromSerializableState(null)`. But the `CampaignEvents.AddNonSerializedListener(this, ...)` calls in `RegisterEvents` -- do they accumulate duplicates if RegisterEvents runs twice on the same instance? Consult vanilla `MbEvent<T>.AddNonSerializedListener` semantics.

d) **`PresetsOverlayVM.PromptSaveName` defaults `includeCivilian` from `_screen.IsViewingCivilianEquipment`.** That means: if the player is viewing civilian equipment when they hit Save, the preset captures CIVILIAN slots only (because `_screen.IsViewingCivilianEquipment == true` -> `includeCivilian = true` -> civilian-set captured). Is this the right semantic? Should pressing Save while viewing battle equipment ALSO prompt for "include civilian?" Or is the current "match what you're viewing" behavior the right design?

e) **`Patch33_GauntletInventoryScreen` static state leak.** `_layer` and `_overlayVm` are nulled on `OnFinalize` Prefix. What if `OnInitialize` Postfix throws BEFORE setting `_layer`? Then on next inventory open, `_layer` is null but `_overlayVm` is set (from the prior failed init). Re-trace the try-catch flow.

f) **`InventoryScreenAdapter.SetItemLocked` iteration.** Claude iterates `_active.RightItemListVM` to set `IsLocked`. If `RightItemListVM` is mutated during iteration (e.g., by vanilla code reacting to a slot change), enumeration throws. Currently this method is unused in the EquipPresets flow (the SlotLocked plumbing was removed in deep-review). Confirm it is truly unused -- if so, propose deletion as YAGNI per `feedback_no_aspirational_enum_values.md`.

### 3. CONFIG CROSS-REFERENCE

The MCM properties are in `Main/Features/TaomSettings.cs` lines ~373-388:
- `EnableEquipmentPresets` (bool, default true)
- `MaxPresetsPerCharacter` (int [1,20], default 10)
- `EquipPresetsDebug` (bool, default false)

Trace each through:
- Provider: `Main/Features/EquipPresets/EquipPresetsSettingsProvider.cs` -- does it pass through correctly? Does `MaxPresetsPerCharacter` clamp out-of-range to default 10?
- Consumer: `Main/Features/EquipPresets/EquipmentPresetService.cs` -- gates Save/Load/Update/Delete on `IsEnabled`; gates Save on `MaxPresetsPerCharacter`; gates LogDebug on `IsDebugMode`. Confirm every consumption matches the user-facing promise in the MCM hint text.

### 4. FINDINGS

For each finding, provide:
- File and line number
- Severity: P1 (critical -- crash, data loss, save corruption), P2 (high -- logic bug user will notice), P3 (medium -- minor issue), P4 (low -- nit / style)
- The bug in plain English
- A minimum-viable fix
- Whether it's a CONFIRMED bug or a SPECULATION (you couldn't verify, need source/decompile)

## QUALITY GATES

A high-quality review:
- Pastes vanilla code blocks for every Harmony patch / GameModel / TaleWorlds API claim
- Cross-references config IDs against the cheatsheet above
- Disputes Claude's claims that don't hold up under decompile
- Identifies bugs Claude's deep-review missed (Agent 5 already caught: dead SlotLocked plumbing, IInventoryScreenAdapter.Clear() interface gap, IoC.Resolve uncached in patch -- all FIXED before this Codex pass)
- Flags potential save-format breakage

## Prior review lessons

SUCCESSES:
- Config ID cross-ref caught rohan/dol_guldur mismatches across multiple ports.
- Vanilla decompilation caught missing safety gates in MixedFormations Patch30 (review #36).
- Lifecycle tracing caught stale caches in shader-precompilation (RCA 2026-05-04).
- "Validate before lookup" rule caught silent acceptance of invalid race IDs (review #33).

FAILURES (Codex tendencies to suppress):
- Codex assumed `empire = Rohan` (it is Dunland).
- Codex flagged vanilla-matching code as bugs.
- Codex skipped "hard" sections (vanilla decompile blocks) when the source was inconvenient.
- Codex once reported an API as "removed" when it was simply private (Harmony can still patch by string).

This is the SECOND deep-review for EquipPresets. The first (Claude's /deep-review) already found and fixed:
- Patch33 PatchCategory commented out (now uncommented)
- IoC.Resolve uncached in Patch33_SPInventoryVMRefresh (now `??=`-cached)
- IInventoryScreenAdapter.Clear() not on the interface (now exposed)
- Dead SlotApplyOutcome.SlotLocked + SkippedLockedSlots fields (removed)

Your job: find what Claude missed.
