# Custom Battles

## Overview

TAOM Custom Battle support replaces vanilla factions, commanders, and troops in the Custom Battle screen with TAOM-specific content. All TAOM cultures (Gondor, Mordor, Rohan, etc.) appear as selectable factions, their lords appear as commanders, and each formation slot defaults to the culture's own troop wherever that troop fits the slot (see "Formation-to-Troop Mapping"). A team-fix MissionBehavior prevents friendly fire bugs in both field and siege custom battles.

Siege battles here can run TAOM's staged siege on a recorded scene; see [staged-siege.md](staged-siege.md), "Custom battle sieges".

Since 2026-11-11 the Custom Battle screen opens TAOM's muster screen (movie `TaomCustomBattle`) on a layer over the vanilla screen. The vanilla screen with the patches below stays underneath as the fallback: **OLD SCREEN** removes the muster layer, and any failure to open the muster leaves the vanilla screen in place. See "Muster screen".

## Why This Exists

- **Vanilla behavior:** Custom Battle lists vanilla factions (Empire, Sturgia, Aserai, etc.) and 24 hardcoded vanilla commanders. Troop formation defaults are hardcoded per vanilla culture.
- **TAOM requirement:** Total conversion mod needs TAOM cultures, TAOM lords as commanders, and TAOM troops in formations. Without this, Custom Battle is unusable for testing mod content.
- **Without this feature:** Players see vanilla faction names, commanders resolve to null (no `commander_1` etc. in TAOM data), and troops fall through to null causing crashes or empty armies.

## Architecture

### Design Challenge

`CustomBattleData.Characters` and `CustomBattleData.Factions` are static property getters on a **struct** that yield hardcoded vanilla IDs. `CustomBattleHelper.GetDefaultTroopOfFormationForFaction` uses a giant switch statement on vanilla culture string IDs. All three must be intercepted before the Custom Battle screen initializes.

### Solution Approach

10 Harmony patches (category `Patch19_CustomBattles`) applied in `OnSubModuleLoad()` — before any CustomGame type loads:

1. **Prefix** on `CustomBattleData.Characters` getter — replaces vanilla commanders with TAOM lords
2. **Prefix** on `CustomBattleData.Factions` getter — replaces vanilla cultures with TAOM cultures
3. **Postfix** on `CustomBattleHelper.GetDefaultTroopOfFormationForFaction`: sets the culture's TAOM troop for the formation, replacing vanilla's pick, but only a troop vanilla's slot list can show (the slot's culture and formation class; see "Formation-to-Troop Mapping"). With no vanilla pick it returns a loaded candidate even if it does not fit, because Start spawns the default for an empty slot. For the six re-skinned cultures vanilla's switch returns a Calradian troop (`vlandia` gives `vlandian_swordsman`), which SandBoxCore still loads for Custom Battle; that pick stays wherever TAOM has no eligible troop
4. **Postfix** on `BannerlordMissions.OpenCustomBattleMission` — injects team-fix behavior
5. **Postfix** on `BannerlordMissions.OpenSiegeMissionWithDeployment` — injects team-fix for siege (only for non-campaign missions)
6. **Postfix** on `CustomBattleSideVM` constructor — replaces `FactionSelectionGroup` with `TaomFactionSelectionVM` and explicitly fires the `OnCultureSelection` callback so the initial commander dropdown aligns with the visible faction (vanilla `SelectFaction(0)` doesn't fire the callback)
7. **Postfix** on `CustomBattleSideVM.OnCultureSelection(BasicCultureObject)` (private method, patched by name) — rebuilds `CharacterSelectionGroup.ItemList` filtered to the selected faction, capped at 3 commanders
8. **Postfix** on `CustomBattleSideVM.RefreshValues()` — defensive re-filter for refresh events (language/resolution change)
9. **Prefix** on `CustomBattleSideVM.OnCharacterSelection(SelectorVM<CharacterItemVM>)` — defensive null-guard on `selector.SelectedItem`. Vanilla derefs `selector.SelectedItem.Character` without a null check, but `SelectorVM<T>.SelectedIndex` setter fires `_onChange.Invoke(this)` even when `GetCurrentItem()` returns null (empty `ItemList` or out-of-range index). The Prefix returns `false` to skip the vanilla body when `SelectedItem == null`, eliminating an NRE that surfaced during in-game testing. ([CustomBattleSideVM_OnCharacterSelection_Patch.cs](../../Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCharacterSelection_Patch.cs))
10. **Prefix** on `CustomBattleSideVM.UpdateCharacterVisual()` — sister null-guard for the OnCharacterSelection Prefix. Vanilla `RefreshValues()` calls `UpdateCharacterVisual()` unconditionally after `SelectedIndex = 1` (enemy side default), and `UpdateCharacterVisual` derefs `SelectedCharacter.Equipment[(EquipmentIndex)5]`. When the OnCharacterSelection Prefix skipped the body (preventing vanilla from setting `SelectedCharacter`), the next line in `RefreshValues` would NRE. The Prefix returns `false` when `__instance.SelectedCharacter == null`, letting `RefreshValues` continue past the visual update without crashing. (Codex Review 32 P2 — `UpdateCharacterVisual` was outside the OnCharacterSelection Prefix's blast radius despite being the same call chain.) ([CustomBattleSideVM_UpdateCharacterVisual_Patch.cs](../../Main/Features/CustomBattles/Hooks/CustomBattleSideVM_UpdateCharacterVisual_Patch.cs))

The vanilla screen needs no UI patches: TAOM's `CustomBattleScreen.xml` GUI prefab overrides vanilla via Gauntlet module load order (TAOM loads after the CustomBattle module). The muster screen adds two more `Patch19_CustomBattles` postfixes in TAOM.Client (ADR-013), below.

### Muster screen

**Opening.** `CustomBattleScreen_OnInitialize_Patch` (postfix on `CustomBattleScreen.OnInitialize`) adds a `GauntletLayer` (order 200, input restrictions set, focus layer) that loads `TaomCustomBattle` with a `TcbVM`. The prefab draws TAOM's shared front-end art (`ec_*`, `cc_*`, `fs_*`, `mm_*`) and the resident fonts, so it opens only when `FactionUIPatchContext.Movies` is set: `FrontEndMovieService` matches the `TaomCustomBattle` movie with a `TcbVM` data source and holds the main menu and character creation image groups while the movie lives, and `FrontEndSpriteService` loads the `TaomCustomBattle` brush file with the chrome group. Without that service, or on any exception, the log says so and the vanilla screen stays. **BACK** pops the state; **OLD SCREEN** removes the layer and shows the vanilla screen.

**Armies tab.** Two cards, *Your army* and *Enemy army*; the card clicked is the army mustered below. Each card has popups for:

- **Faction:** `CustomBattleData.Factions`, so the faction filter above applies. A faction change clears the army's units and selects the first commander.
- **Commander:** every hero of the faction's culture, its curated commanders first (`CustomBattleMuster.CuratedCommanderIds`, the same `ISideCommanderFilter` as the vanilla dropdown). With none chosen at Start, `CommanderOf` takes the first curated commander that resolves, else the culture's highest-level `lord_` hero, else its highest-level hero.
- **Banner:** starts as the culture's kingdom banner (`IBannerConfigProvider.GetCultureKingdomBannerKeys`, read from `taom_spkingdoms.xml` and `spkingdoms.xslt`; the first kingdom of a culture wins), else the culture banner in its colours. `MusterBannerDesign` keeps that exact code until the player edits the background colour (layer 0), the emblem colour (every icon layer) or the emblem (one centred emblem). A faction change resets the edits. The exact code goes to the battle.

The roster lists the faction's troops (`CaptainResolver`: defined by a non-vanilla module, part of a troop tree or level 21+, not a training dummy) with search and type tabs, plus its heroes (one man each, no roles). A mustered unit has plus and minus (Shift 10, Ctrl 100 per click), remove, and three options: banner bearer, captain and horn blower, each one man taken from the unit's number. **RANDOMISE** fills the active army. Both armies together are capped at `ArmySpec.MaxTroops` (1000); over it the footer says so.

**Battlefield tab.** A large preview of the chosen map with a strip of its neighbours, and dropdowns for battle type (field battle, village, siege), player type, side, map, season and time of day; a siege adds breached walls and the town level. A siege gets a ram and two siege towers. Previews are `GUI/CustomBattleMuster/Maps/<scene id>.png`, loaded as loose sprites on first use and kept for the process; a map without one shows `ec_stone_landscape`.

**Start.** `ExecuteStart` refuses with a status line when a faction, a unit on either side, or the map is missing, or `ArmySpec.Validate` fails. Otherwise it stores the army in `CustomBattleMuster.Pending` and calls vanilla `GetCustomBattleParties`, `PrepareBattleData` and `StartGame`. `CustomBattleHelper_GetCustomBattleParties_Patch` (postfix) then runs `MusterArmyBuilder.Apply`: it consumes `Pending` once and rebuilds each side with its commander, the exact unit numbers (a captain comes out of his unit's number: the troop's upgrade, else the culture's elite with the closest weapon) and the exact banner. It records the roles in `CustomBattleMuster.Plan` and sets `Applied`. A battle started from the vanilla screen has no `Pending`, so it keeps vanilla's sides and gets no roles.

**In the mission.** The two `BannerlordMissions_*` postfixes call `CustomBattleMuster.AddMissionBehaviors`, which adds two behaviors only when `Applied` is set:

- `CustomBattleRolesBehavior` hands out the roles once the armies stand: the captain leads his unit's formation, the banner bearer gets the banner through the engine's `BannerBearerLogic`, and the horn blower is marked.
- `CustomBattleHornCallBehavior` sounds the horn when the blower's formation gets a new charge, advance, follow or move order (not the first order). He keeps marching, and a call plays the culture's horn sound and rallies his formation. His action set `as_tcb_horn_<own>` comes from `ModuleData/action_sets.xml` (registered in `project.mbproj`) and is given once while he stands, because switching sets on a walking agent crashed the engine. The horn is `Prefabs/tcb_horn.xml` on his head bone. Held agents pass `AgentSlotIdentity` first.

Log lines carry `[CustomBattleMuster]`. Screen text is hard-coded English, not yet `{=KEY}` localized.

### Commander filter+cap (per faction)

Vanilla `CustomBattleSideVM.RefreshValues()` adds every entry from `CustomBattleData.Characters` to `CharacterSelectionGroup.ItemList`. With TAOM's expanded lord pool, this means picking "Dunland" still showed every culture's commanders. Vanilla `OnCultureSelection` only updates banner colors — it does NOT re-filter the dropdown.

The fix is a singleton hook (`ISideCommanderFilter` / `SideCommanderFilter`) that resolves a culture's commanders via `CustomBattleService.GetCommanderIdsForFaction(factionId, takeMax)`. The service applies `OrderBy(Id, OrdinalIgnoreCase).Take(takeMax)` so the cap is deterministic across launches. Cap is `SideCommanderFilter.MaxCommandersPerCulture = 3`.

Both side-VM postfixes log a `LogWarning` to `rgl_log.txt` if a culture has zero matching commanders, so future `lords.xml` culture-tag misalignment surfaces in logs instead of silently regressing the dropdown to the unfiltered list.

### Curated commander lists (per faction)

The default top-3-alphabetical behavior never reliably surfaces the named LOTR lords a player wants (Boromir, Théoden, Sauron…), and its 2-segment-id regex (`^lord_[A-Za-z0-9]+_[A-Za-z0-9]+$`, the `GetCommanderIds_ExcludesSubLords` guarantee) **excludes 3-segment ids outright** — Éomer `lord_4_3_1`, Éowyn `lord_4_3_2`, the lesser Nazgûl `lord_1_48_1/2/3` can never appear no matter the cap.

A data-driven config (`custom_battle/custom_battle_commanders.json`) maps each faction key (culture `StringId`) to an **ordered** list of lord ids. `CustomBattleService.GetCommanderIdsForFaction` branches on `ICustomBattleCommandersProvider.HasCuratedEntry(factionId)`: a configured faction returns that exact list, in order, **bypassing** the regex, the `takeMax` cap, and the culture filter (so 3-segment ids and cross-culture lords are allowed). Unconfigured factions keep the default behavior unchanged.

Because curated resolution bypasses the culture filter, a lord may be listed under a faction whose `StringId` differs from the lord's own culture — e.g. Khamûl (`lord_1_48`, `Culture.dolguldur`) and Duinhir (`lord_WE9_l`, `Culture.empire`) both appear under their requested faction. This is intentional: the config is the source of truth for what shows under each faction.

**Curated lords built from vanilla lords need a Custom Battle stub.** Sauron, the Witch-king, Boromir, Théoden and the other lords `lords.xslt` rebuilds from vanilla SandBox lords do not exist in a Custom Battle on their own: SandBox registers its `lords.xml` for `Campaign` and `CampaignStoryMode` only (the same in v1.3.0 and v1.5.3), so the XSLT has nothing to match. `characters/custom_battle_lords.xml`, registered for `CustomGame` only and placed before the `lords` node in `SubModule.xml`, holds a bare `id` + `culture` stub per such curated commander. `MBObjectManager.CreateMergedXmlFile` applies each node's XSLT to everything merged before it, so `lords.xslt` rebuilds each stub into the full character. Before this file (2026-10-02) all twelve Mordor ids failed to resolve, so Mordor fell back to its default lords. Gondor showed only Duinhir and Rohan only Éomer and Éowyn, because each list's other ids were missing. `CustomBattleLordStubsTests` pins the load order, the transform output (name, hero, face, equipment) and that every stub is a curated commander.

The master `CustomBattleData.Characters` list (`GetCommanderIds()`) is deliberately **unchanged** — it stays regex-filtered. The per-faction dropdown is rebuilt directly from `GetBasicCharacter`-resolved objects (`CommanderSelectorRebuilder.Apply`), independent of the master list, so curated 3-segment ids surface in the dropdown without polluting the global pool. `CuratedDropdownIndependenceTests` pins this decoupling.

Two side effects of the stubs, kept on purpose (Mike, 2026-10-02): 21 of the 24 stub ids match the master list's `^lord_X_Y$` pattern, so in Sergeant mode vanilla's random player-side general (`CustomBattleData.Characters.GetRandomElement()`) can now be Sauron, the Witch-king or a Nazgûl. And Khamûl (`lord_1_48`, `Culture.dolguldur`) now heads Dol Guldur's default (uncurated) list, which reads Khamûl, Vrâkmug, Shâgthul instead of Vrâkmug, Shâgthul, Gûrnak. Since #788 a Custom Battle Sauron fights under TAOM's race combat rules too (his CTB, knockdown resistance and crush): Custom Battle's damage model is `TaomCustomBattleDamageModel`, the `CustomAgentApplyDamageModel` twin of the campaign's `TaomCombatMechanicsModel`. His signature strikes run there, primary-victim knockdown included.

The provider validates per the **Config Providers MUST Validate** rule (missing/malformed file → all factions default; empty/whitespace ids skipped; duplicates deduped first-occurrence-order; unknown faction keys kept-but-warned; a faction with no valid ids falls back to default). `Reuse.Singleton` → **edits require a full game restart**, not a save-load. Unresolvable ids cannot be checked at load (no live `MBObjectManager`); they are warned + skipped at resolve time in `SideCommanderFilter`.

### Component Diagram

```
CustomBattleData.Characters [HarmonyPrefix]
    |
CustomBattleData_Characters_Patch
    |
IOnGetCustomBattleCommanders (hook interface)
    |
CustomBattleCommandersHook
    |--- ICustomBattleService.GetCommanderIds()  (string IDs only)
    |--- IObjectManagerAdapter.GetBasicCharacter(id)  (resolves to TW type)
    |
    v
IEnumerable<BasicCharacterObject> returned to game
```

Same pattern for Factions and Troops.

## Configuration

One external configuration file: `custom_battle/custom_battle_commanders.json` (the curated per-faction commander lists — see "Curated commander lists" above). Everything else (factions, the default commander selection, formation troops) is loaded dynamically from `Game.Current.ObjectManager` at runtime. A faction with no curated entry — or a curated entry whose ids don't resolve to any live character — falls back to the dynamic default selection below.

### Faction Selection Criteria
- `CanHaveSettlement = true`
- `IsBandit = false`
- Has a faction banner: `faction_banner_key` is present and parses to at least one layer. Vanilla `CustomBattleHelper.GetCustomBattleParties` recolours layer 0 of each side's culture banner on Start without a length check, so a culture with no key throws `ArgumentOutOfRangeException` (crash f9a7181d). This keeps out vanilla's minor cultures `nord`, `vakken` and `darshi`, leaving the TAOM factions (22 when this was written; `taom_spcultures.xml` alone now defines 24 non-bandit cultures with a settlement flag and a banner key).
- Non-empty culture ID

### Commander Selection Criteria
- `IsHero = true`
- ID does not contain: `companion`, `child`, `tutorial`, `commander_`, `wanderer`, `notable`

### Formation-to-Troop Mapping
| Formation | Troop Source (culture attribute) | Fallback |
|-----------|-------------|----------|
| Infantry (0) | `melee_militia_troop` | `basic_troop` |
| Ranged (1) | `ranged_militia_troop` | none |
| Cavalry (2) | `elite_basic_troop` | none |
| Horse Archer (3) | `ranged_elite_militia_troop` | none |

A Custom Battle registers cultures as plain `BasicCultureObject` (`CustomGame.OnRegisterTypes`), whose deserializer reads none of these attributes, so `ObjectManagerAdapter` reads them back from the merged `SPCultures` XML for the current game type (`CultureTroopIdReader`, the same re-read `MonsterSizeCatalogAdapter` does for Monsters). In a campaign the values come from `CultureObject` directly. Until 2026-10-02 the adapter read only `CultureObject`, so in Custom Battle every TAOM default came back empty, and vanilla's troop picker marked the first matching troop in load order as the default. The log line `ObjectManagerAdapter: read troop ids for N cultures from the merged SPCultures XML (CustomGame)` confirms the re-read ran.

**A troop is offered only when the slot can show it.** Vanilla's slot list holds only soldiers that are not obsolete (`ArmyCompositionGroupVM`), and `ArmyCompositionItemVM.IsValidUnitItem` then keeps a troop only when it is the slot's culture and its `DefaultFormationClass` fits (infantry 0 or 5, ranged 1, cavalry 2, 6 or 7, horse archer 3); a default outside that list is ignored and the slot's first troop becomes the default. `CustomBattleService.GetDefaultTroopIdForFormation` applies the same check (`IsEligibleForSlot`). When vanilla already picked a troop, it returns null for a TAOM troop that fails the check, so vanilla's pick stands. When vanilla has no pick, a fitting troop is still preferred, and failing that it returns the first TAOM candidate that loads, because the default has a second consumer: at Start, `CustomBattleHelper.PopulateListsWithDefaults` spawns it unchecked for any slot left empty, and a null default for slots 0-2 throws once that slot has troops to spawn (a null horse-archer default is redistributed instead). That keeps Abanissa and Shaghana, which have no soldiers of their own culture, from crashing: every slot is empty for them, and as at `9e2a39f4` their whole army spawns as the one Harad foot archer their horse-archer slot names, because that slot keeps 100% when all four are invalid. As of 2026-10-02 the shipped culture data fit that check in 33 of the 88 culture-and-slot pairs (then 22 playable cultures times 4 slots; the count has not been recomputed since more cultures were added): every `ranged_elite_militia_troop` is a foot archer, most `elite_basic_troop` entries are infantry nobles, and seven cultures (abanissa, shaghana, umbar, battania, bluecraig, mistymountainorcs, lothlorien) name another culture's troops. Defaulting those slots to a TAOM troop needs slot-correct ids in the culture data, a separate change.

**The Harwan and Shaghana slots offer only the Harad army.** Two CustomGame-only stylesheets from KEYforce (`d94ee1631`) shape them. `custom_battle_harad_troop_filter.xslt`, registered after every other `NPCCharacters` node except `fords_of_war/fords_troops`, marks every non-hero `Culture.aserai` or `Culture.shaghana` character obsolete unless its id starts with `harad_`, so the slot list above drops Jawwal, Beni Zilal, militia, caravan guards and the like: 52 TAOM troops stay, plus DualWield_TAOM's `dw_harad_twinscimitar`, which loads after the filter. It works only while it comes after every `NPCCharacters` node that adds a non-obsolete Aserai or Shaghana character, because an XSLT applies only to what merged before it, and a future Harad troop whose id lacks the `harad_` prefix would be hidden. It is no longer the last `NPCCharacters` node: `fords_of_war/fords_troops` (CustomGame only, `SubModule.xml:1441`) loads after it and holds 15 Aserai copies, but every character in that file is `is_obsolete="true"`, so the troop slots never offer them. `custom_battle_cultures.xslt` shows `Culture.aserai` as `{=aom_aserai_cb_name}Harwan` in the faction list; the campaign keeps "Haradrim". The same stylesheet also marks the `legion_of_the_betrayed_tier_*`, `company_of_the_boar_tier_*`, `eleftheroi_tier_*` and `brotherhood_of_woods_tier_*` troops obsolete (#957): `lords.xslt` retags them to Arthedain, Umbar, Dale and Dunland, which would otherwise offer them in those cultures' slots. It also hides, by id, the vanilla Calradian soldiers of the other reused cultures (`imperial_*`, `sturgian_*`, `vlandian_*`, `khuzait_*`, the vanilla minor clans `hidden_hand`, `embers_of_flame`, `lakepike`, `karakhuzaits`, and `bucellarii`, `varyag`, `druzhinnik`), every training dummy and training-yard opponent (`gear_practice_dummy_*`, `gear_dummy_*`, `weapon_dummy_*`, `weapon_practice_stage_*`, most of them TAOM's own Soldier-occupation dummies), and the vanilla CustomBattle module's `commander_N` heroes, which the muster offered as commanders. Dunland (`Culture.empire`) and Khand (`Culture.battania`) keep their vanilla commanders, and Khand keeps the vanilla Battanian troop line: TAOM has no Custom Battle heroes for either and no Khand soldiers, so hiding them would leave the Khand ranged and cavalry slots and both commander lists empty. Counted by merging the v1.5.7 install's CustomGame NPCCharacters with TAOM's (2026-11-14, not seen in game), the change takes 88 vanilla soldiers, 75 dummies and opponents and 16 commanders out of the lists. `CustomBattleTroopFilterTests` runs the stylesheet over a synthetic document; `CustomBattleTroopFilterLiveTests` (`LiveInstall`) repeats that merge on the installed game and fails when a vanilla soldier, dummy or commander comes back, or when a TAOM soldier is hidden. A campaign loads neither file.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CustomBattles/ICustomBattleService.cs` | Service interface — faction/commander/troop queries |
| `Main/Features/CustomBattles/CustomBattleService.cs` | Service implementation with caching |
| `Main/Features/CustomBattles/CustomBattlesIoC.cs` | DryIoc registration + hook initialization |
| `Main/Features/CustomBattles/CustomBattleTeamFixBehavior.cs` | MissionBehavior ensuring teams are enemies |
| `Main/Features/CustomBattles/Hooks/IOnGetCustomBattleCommanders.cs` | Hook interface for commander replacement |
| `Main/Features/CustomBattles/Hooks/IOnGetCustomBattleFactions.cs` | Hook interface for faction replacement |
| `Main/Features/CustomBattles/Hooks/IOnGetDefaultTroopOfFormation.cs` | Hook interface for troop assignment |
| `Main/Features/CustomBattles/Hooks/CustomBattleCommandersHook.cs` | Hook impl — resolves commander IDs to objects |
| `Main/Features/CustomBattles/Hooks/CustomBattleFactionsHook.cs` | Hook impl — resolves faction IDs to objects |
| `Main/Features/CustomBattles/Hooks/CustomBattleTroopHook.cs` | Hook impl — resolves troop IDs to objects |
| `Main/Features/CustomBattles/Hooks/CustomBattleData_Characters_Patch.cs` | Harmony prefix — replaces Characters getter |
| `Main/Features/CustomBattles/Hooks/CustomBattleData_Factions_Patch.cs` | Harmony prefix — replaces Factions getter |
| `Main/Features/CustomBattles/Hooks/CustomBattleHelper_Troop_Patch.cs` | Harmony postfix: replaces vanilla's formation default with the culture's eligible TAOM troop; with no vanilla pick, any loaded candidate (Start spawns it for an empty slot) |
| `Main/Features/CustomBattles/Hooks/BannerlordMissions_CustomBattle_Patch.cs` | Harmony postfix — injects team fix |
| `Main/Features/CustomBattles/Hooks/BannerlordMissions_Siege_Patch.cs` | Harmony postfix — injects team fix for siege |
| `Client/Features/CustomBattles/TaomFactionSelectionVM.cs` | Custom faction-selection VM with prev/next navigation |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_Constructor_Patch.cs` | Harmony postfix — swaps FactionSelectionGroup; fires initial OnCultureSelection callback |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCultureSelection_Patch.cs` | Harmony postfix — filters commander dropdown on faction click (cap=3) |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_RefreshValues_Patch.cs` | Harmony postfix — defensive re-filter for refresh events |
| `Main/Features/CustomBattles/Hooks/ISideCommanderFilter.cs` | Hook interface — resolves commanders for a culture |
| `Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs` | Hook impl — calls service with `MaxCommandersPerCulture = 3`; warns + skips any commander id that fails to resolve |
| `Main/_Module/ModuleData/custom_battle/custom_battle_commanders.json` | Curated per-faction commander lists (faction culture id → ordered lord ids) |
| `Main/Features/CustomBattles/Config/CustomBattleCommandersConfig.cs` | DTO for the curated config (`Dictionary<string, List<string>> Factions`) |
| `Main/Features/CustomBattles/Config/ICustomBattleCommandersProvider.cs` | Provider interface — `HasCuratedEntry` (validate-before-lookup gate) + `GetCuratedCommanderIds` |
| `Main/Features/CustomBattles/Config/CustomBattleCommandersProvider.cs` | Validating `Lazy` singleton provider (mirrors `CultureConversionConfigProvider`) |
| `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` | Static helper — calls vanilla `SelectorVM<T>.Refresh(items, 0, onChange)` to safely rebuild the selector. Reads existing `_onChange` via cached `FieldInfo` so Refresh's overwrite preserves the wiring. (Issue #105 — replaced manual `Clear() + AddItem(*N) + reflection-on-_selectedIndex` approach to match the canonical safe rebuild pattern.) |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCharacterSelection_Patch.cs` | Defensive Prefix on the private `OnCharacterSelection(SelectorVM<CharacterItemVM>)` — returns `false` when `selector?.SelectedItem == null`. Stops vanilla NRE that surfaced when `SelectedIndex` setter fires `_onChange.Invoke` with an empty `ItemList` (Issue #105 Bug 1). |
| `Main/Adapters/IObjectManagerAdapter.cs` | Adapter interface + CultureInfo/CharacterInfo DTOs |
| `Main/Adapters/ObjectManagerAdapter.cs` | ObjectManager bridge implementation; in a Custom Battle it reads culture troops from the merged SPCultures XML |
| `Main/Adapters/CultureTroopIdReader.cs` | Pure parser: culture id to troop ids from a merged SPCultures document |
| `Main/_Module/ModuleData/characters/custom_battle_lords.xml` | CustomGame-only stubs that `lords.xslt` rebuilds into the curated vanilla-derived commanders |
| `Main/_Module/ModuleData/custom_battle/custom_battle_harad_troop_filter.xslt` | CustomGame-only: hides non-army Aserai and Shaghana characters (ids without `harad_`), the four minor clans' troops, the vanilla Calradian soldiers of Dunland, Dale, Rohan and Rhun, every training dummy and training-yard opponent, and the vanilla Calradian commanders of Rohan, Rhun, Dale and Harwan from the troop and commander lists |
| `Main/_Module/ModuleData/custom_battle/custom_battle_cultures.xslt` | CustomGame-only: names `Culture.aserai` "Harwan" in the faction list (`aom_aserai_cb_name`) |
| `Main/_Module/GUI/PreFabs/CustomBattle/` | The vanilla screen's Gauntlet prefab XMLs (pre-existing) |
| `Client/Features/CustomBattles/Muster/Hooks/CustomBattleScreen_OnInitialize_Patch.cs` | Postfix: opens the muster layer over the vanilla screen |
| `Client/Features/CustomBattles/Muster/Hooks/CustomBattleHelper_GetCustomBattleParties_Patch.cs` | Postfix: rebuilds both sides from the muster (`MusterArmyBuilder`) |
| `Client/Features/CustomBattles/Muster/TcbVM.cs` | Muster screen VMs: armies, cards, popups, roster, units, banner editor |
| `Client/Features/CustomBattles/Muster/TcbFieldVM.cs` | Battlefield tab: map, battle settings, siege engines |
| `Client/Features/CustomBattles/Muster/MusterArmyBuilder.cs`, `CaptainResolver.cs` | Exact sides from the `ArmySpec`; captain choice and the troops offered |
| `Client/Features/CustomBattles/Muster/TcbSprites.cs`, `BannerPalette.cs` | Map preview sprites; nearest palette colour for culture banners |
| `Main/Features/CustomBattles/Muster/CustomBattleMuster.cs`, `ArmySpec.cs` | Hand-over from the screen to the battle; the army spec and its validation |
| `Main/Features/CustomBattles/Muster/MusterBannerDesign.cs` | One army's banner code and its edits |
| `Main/Features/CustomBattles/Muster/CustomBattleRolesBehavior.cs`, `CustomBattleRoleAssigner.cs` | Captain, banner bearer and horn blower roles in the mission |
| `Main/Features/CustomBattles/Muster/CustomBattleHornCallBehavior.cs`, `HornBlowerActions.cs` | Horn calls on new orders |
| `Main/Features/BannerInjection/BannerConfigProvider.cs` | `GetCultureKingdomBannerKeys`: culture to its kingdom's banner key |
| `Main/Features/FactionUI/Menus/FrontEndMovieService.cs`, `Resources/FrontEndSpriteService.cs` | Image hold for the `TaomCustomBattle` movie; its brush file |
| `Main/_Module/GUI/PreFabs/CustomBattle/Muster/`, `GUI/Brushes/TaomCustomBattle.xml` | Muster screen prefabs and brushes |
| `Main/_Module/GUI/CustomBattleMuster/Maps/` | Map previews, `<scene id>.png` |
| `Main/_Module/ModuleData/action_sets.xml`, `Main/_Module/Prefabs/tcb_horn.xml` | Horn blower action sets (registered in `project.mbproj`) and the horn prefab |

## Dependencies

- **CustomBattle module** — vanilla DLL providing `CustomBattleData`, `CustomBattleHelper`, `CustomBattleScreen`
- **Harmony** — patch framework for intercepting static property getters and method calls
- **DryIoc** — IoC container for service/hook registration
- **FactionUI front-end art** (muster screen only): `FrontEndMovieService` and `FrontEndSpriteService` register and hold the shared images the muster prefab draws; without them the vanilla screen stays
- **SubModule.xml** — declares `<DependedModule Id="CustomBattle" />` and registers troop trees, cultures (XSLT + custom), and lord characters for `CustomGame`/`EditorGame`. **Critical:** Lord/culture XMLs MUST be registered for `CustomGame` — without this, ObjectManager has no heroes in Custom Battle mode, causing NRE in `CustomBattleSideVM.OnCharacterSelection`

## Tests

| Test File | Methods | Coverage |
|-----------|---------|----------|
| `TAOM.Tests/Features/CustomBattles/CustomBattleServiceTests.cs` | 46 | Faction filtering (one isolating test per clause: settlement, bandit, faction banner), default-troop slot eligibility (not a soldier, obsolete, wrong formation class, another culture's troop, troop not loaded, slot outside 0-3, vanilla's sibling classes, basic-troop fallback, and with no vanilla pick the first loaded candidate, the Abanissa shape), commander filtering, formation mapping, takeMax cap, null/empty edge cases + **8 curated-branch tests** (curated order preserved, regex+cap bypass, culture-filter bypass, all-unresolvable → fallback-to-default, partially-resolvable → only-existing-in-order, non-curated default path, null guard precedes provider, master list unchanged) |
| `TAOM.Tests/Features/CustomBattles/CustomBattleCommandersProviderTests.cs` | 16 | Config load + validation: order preserved (incl. 3-segment/>3-length), case-insensitive keys, missing/malformed file, no-factions-map, empty/whitespace id, dedupe, empty/unknown faction key, all-invalid faction not registered, info-not-warning, lazy caching |
| `TAOM.Tests/Features/CustomBattles/CustomBattleCommandersShippedDataTests.cs` | 2 | Shipped-data regression: every curated faction key is a known culture + curated, and every shipped lord id exists in a Custom Battle (`characters/lords.xml` or a `custom_battle_lords.xml` stub; a `lords.xslt` template alone does not count) |
| `TAOM.Tests/Features/CustomBattles/CustomBattleLordStubsTests.cs` | 3 | Stub node registered for CustomGame only and before the `lords` node, which must itself keep CustomGame; `lords.xslt` rebuilds every stub with name, hero flag, face and equipment; every stub is a curated commander not already in `characters/lords.xml` |
| `TAOM.Tests/Adapters/CultureTroopIdReaderTests.cs` | 5 | All five troop attributes read with the `NPCCharacter.` prefix stripped, missing/empty attribute is null, id-less culture skipped, case-insensitive lookup, null document |
| `TAOM.Tests/Features/CustomBattles/CuratedDropdownIndependenceTests.cs` | 1 | Pins the master-list/dropdown decoupling (curated id absent from `GetCommanderIds()` still resolves) |
| `TAOM.Tests/Features/CustomBattles/CustomBattleCommandersHookTests.cs` | 3 | Resolution, null filtering, empty case |
| `TAOM.Tests/Features/CustomBattles/CustomBattleFactionsHookTests.cs` | 3 | Resolution, null filtering, empty case |
| `TAOM.Tests/Features/CustomBattles/CustomBattleTroopHookTests.cs` | 7 | TAOM troop replaces vanilla's Calradian pick, vanilla kept when TAOM has none or its troop does not resolve, TAOM resolution, null service/adapter results |
| `TAOM.Tests/Features/CustomBattles/SideCommanderFilterTests.cs` | 6 | Null/empty culture, cap=3 propagation, ID resolution, null-resolution filtering, empty result |
| `TAOM.Tests/Features/CustomBattles/PanelEaseTests.cs` | 1 | `CustomBattleScreen.xml` against the install (`LiveInstall`): each panel `VisualDefinition` has vanilla SandBoxCore's attributes (ease, duration, any extra) and the same `VisualState` children with the same attributes |
| `TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs` | 3 | `custom_battle_scenes.xml` against the install (`LiveInstall`): every row's scene exists in an installed module's `SceneObj`, and every siege row's scene declares `siege` plus `level_1` to `level_3`, the levels a Custom Battle siege loads. Two fragment tests (run on CI) pin the declaration parse: a commented-out, single-quoted or reordered declaration, and an entity's own level list, which is not a declaration |

| `TAOM.Tests/Features/CustomBattles/MusterBannerDesignTests.cs` | 15 | Kingdom code kept exactly until an edit; background, emblem colour and emblem edits on simple and multilayer banners; non-palette colour ignored; reset; sides independent; `ExactCode`; `KingdomCode` |
| `TAOM.Tests/Features/CustomBattles/MusterKingdomBannerShippedDataTests.cs` | 3 | Shipped kingdom data gives whole banners for the main cultures, Gondor and Mordor included |
| `TAOM.Tests/Features/CustomBattles/TcbSwatchVMTests.cs` | 2 | The open palette frames the army's current colour |
| `TAOM.Tests/Features/FactionUI/CustomBattleMovieSwapTests.cs` | 9 | `TaomCustomBattle` hold on the menu and chrome groups, brush file order, reopen, wrong VM, missing or failed prefab, no placeholder sprite |
| `TAOM.Tests/Features/BannerInjection/BannerConfigProviderTests.cs` | (extended) | `GetCultureKingdomBannerKeys` |

Patches, `CustomBattleTeamFixBehavior`, `MusterArmyBuilder` and the roles and horn behaviors are thin or boundary entry points: tested in game per ADR-002 and ADR-008.

## How to verify in game

Not yet verified in game for the 2026-11-11 polish (portraits, banners); the earlier muster build ran in the maintainer's game.

1. Main menu, Custom Battle: the muster screen opens over the vanilla screen with both army cards and commander portraits.
2. Click each card, open Faction: pick another faction; the card's banner changes to that culture's kingdom banner, the commander to the first of its list (curated names first) and the army empties.
3. Open the banner popup: change background colour, emblem colour and emblem; the card banner follows. Change faction: the edits reset to the kingdom default.
4. Roster: search, switch type tabs, add troops and a hero; use plus and minus with Shift and Ctrl; remove a unit; toggle banner bearer, captain and horn blower. Push both armies past 1000 men: the footer warns and Start refuses.
5. Battlefield tab: step through maps with the strip; set season and time. Start a field battle: the armies match the muster (numbers, banner as edited) and `rgl_log.txt` has `[CustomBattleMuster]` side lines. Order a mustered horn unit to charge: the horn blower sounds.
6. Start a siege the same way: it loads with a ram and two towers.
7. Leave the battle and reopen Custom Battle: the muster opens again. **OLD SCREEN** shows the vanilla screen, and a battle started there keeps vanilla's sides.
8. Pick Rohan, Dunland, Dale and Rhun in turn: the roster has no Calradian troops (Vlandian, Imperial, Sturgian, Khuzait) and no training dummies or practice opponents; Rohan, Dale and Rhun offer no Calradian commanders. Khand still shows the Battanian line and Dunland and Khand still show the vanilla commanders (by design until TAOM has its own).

## How-To

### How to add a new TAOM culture to Custom Battle

1. Create the troop tree XML in `Main/_Module/ModuleData/troops/troops_{culture}.xml`
2. Register it in `SubModule.xml` with `<GameType value="CustomGame"/>` and `<GameType value="EditorGame"/>`
3. Ensure the culture has `can_have_settlement="true"` and a `faction_banner_key` in its `SPCultures` definition. A culture without the key is left out of the picker, and the only trace is the faction count that `CustomBattleFactionsHook` logs ("Loaded N TAOM factions").
4. The CustomBattleService will automatically pick it up — no code changes needed

### How to add a new commander

1. Define the lord/hero NPC in the culture's characters XML (e.g., `characters/npcs_{culture}.xml`)
2. Register the characters XML in `SubModule.xml` with `CustomGame`/`EditorGame` game types
3. Ensure the character has `is_hero="true"` and an ID that doesn't contain `companion`, `child`, `tutorial`, or `commander_`
4. The service will automatically include them

### How to curate a faction's commander dropdown

1. Edit `Main/_Module/ModuleData/custom_battle/custom_battle_commanders.json`.
2. Add or edit a `"factions"` entry keyed by the faction's **culture `StringId`** (note: Rohan = `vlandia`) with an ordered array of lord ids. Any id that resolves via `MBObjectManager` is allowed, including 3-segment ids and lords whose own culture differs from the faction key.
3. To revert a faction to the default top-3-alphabetical behavior, remove its entry.
4. A lord that `lords.xslt` builds from a vanilla SandBox lord (a `lord_1_*` or `lord_4_*` id not in `characters/lords.xml`) also needs a stub in `characters/custom_battle_lords.xml`, or it does not exist in Custom Battle. `CustomBattleCommandersShippedDataTests` fails until it has one.
5. **Restart the game**: the provider is `Reuse.Singleton` (cached for the whole process), so edits do not take effect on a save-load.

### How to change formation troop assignments

The formation mapping reads the culture's troop attributes (in Custom Battle from the merged `SPCultures` XML). A troop becomes the default only if it is a soldier of that culture whose `default_group` fits the slot; otherwise vanilla's pick stays. To change which troop appears for a formation:
- Modify the culture's `melee_militia_troop`, `ranged_militia_troop`, `elite_basic_troop`, or `ranged_elite_militia_troop` attributes in the culture XML definition.

---

## GitHub Issue

- [#709](https://github.com/haterade22/TAOM/issues/709): the 2026-10-02 Start crash, missing named commanders and ignored default troops.
- Muster screen: no issue, waived by Mike for this work (2026-11-11).

## Changelog

- 2026-11-11: Custom Battle opens TAOM's muster screen over the vanilla one. Each army gets a faction, a commander and its kingdom's banner, which the player can recolour or give another emblem. The roster musters exact unit numbers, heroes and a banner bearer, captain and horn blower per unit. The Battlefield tab picks map, settings and siege options with map previews. Start builds the sides exactly as mustered, and the horn blower sounds when his formation gets a new order. **OLD SCREEN** keeps the vanilla screen available.

- 2026-10-26: the custom battle screen's three panels slide in with vanilla's ease (`EaseType="EaseOut" EaseFunction="Quint"`, the same 0.45 s): they start fast and settle into place instead of moving at constant speed. TAOM's copy of the screen had carried a dead `EaseIn` attribute in their place (#1027, after #1024). In-game check: open Custom Battle; the side panels slide in and decelerate into place over about 0.45 s.
- 2026-10-02: TAOM's formation default troops now apply in Custom Battle where the slot can show them (33 of 88 culture-and-slot pairs). The adapter read troop ids only from `CultureObject`, but a Custom Battle loads cultures as `BasicCultureObject`, so TAOM's default never applied; it now reads them from the merged `SPCultures` XML. For the re-skinned cultures an eligible TAOM troop also replaces vanilla's Calradian pick (Rohan infantry no longer defaults to the Vlandian Swordsman); where TAOM's troop does not fit the slot, vanilla's pick stays.
- 2026-10-02: the curated Mordor, Gondor and Rohan commanders now exist in Custom Battle. 24 of them are vanilla SandBox lords rebuilt by `lords.xslt`, and SandBox loads `lords.xml` for campaigns only; `characters/custom_battle_lords.xml` now supplies a stub for each, which `lords.xslt` rebuilds at load. Before, Mordor's whole list failed to resolve.
- 2026-10-02: the faction picker now lists only cultures with a faction banner. Vanilla v1.5.3's minor cultures `nord`, `vakken` and `darshi` can own settlements but have no `faction_banner_key`, so they passed the old filter, and picking one crashed Start inside vanilla `Banner.ChangePrimaryColor` (crash f9a7181d). New `CultureInfo.HasFactionBanner` in `ObjectManagerAdapter`; the picker goes from 25 to 22 factions. The two older clauses also got isolating tests. RCA: `docs/reviews/rca-custom-battle-bannerless-factions-2026-10-02.md`.
- 2026-06-27 — Codex review fix: a curated faction whose ids ALL fail to resolve (typo / removed lord) now falls back to the default per-culture selection instead of leaving the dropdown on the vanilla global list. The service filters curated ids by character existence; if none survive it logs a warning and uses the default path. Added 2 fallback tests + a shipped-data regression test (`CustomBattleCommandersShippedDataTests`) that cross-checks every shipped id against `lords.xml`/`lords.xslt`. Also fixed a "No external configuration files" doc-drift line. RCA: `docs/reviews/rca-custom-battle-lords-2026-06-27.md`.
- 2026-06-27 — Added curated per-faction commander lists (`custom_battle/custom_battle_commanders.json` + validating `CustomBattleCommandersProvider`). A configured faction shows an exact ordered list of named lords, bypassing the alphabetical cap, the 2-segment-id regex, and the culture filter; unconfigured factions keep the default. Ships lists for Mordor, Gondor, Rohan, Mirkwood, Rivendell, Lothlórien, Isengard, Erebor. Also reassigned the 3 lesser Nazgûl (`lord_1_48_1/2/3`) from `dolguldur` to `mordor` culture (Khamûl stays Dol Guldur).
- 2026-05-13 — Verified the `CustomBattleSideVM.OnCultureSelection(BasicCultureObject)` private signature against the installed CustomBattle DLL and documented the assembly path inline so the Patch19 hook target won't silently break (#162).
- 2026-05-04 — Commander dropdown now filters per-culture and caps at 3 via the new `ISideCommanderFilter`; added `OnCultureSelection`/`RefreshValues` postfixes and the `CommanderSelectorRebuilder` safe-rebuild helper (Codex Review 30 P1).
- 2026-03-27 — Fixed the Custom Battle screen-init NRE by registering cultures/lords for `CustomGame`/`EditorGame`, adding empty-list fallbacks, excluding wanderers/notables, and replacing the broken faction selector with `TaomFactionSelectionVM`.
- 2026-03-27 — Initial Custom Battles feature: TAOM cultures, commanders, and formation-mapped troops in Custom Battle mode via the `Patch19_CustomBattles` Harmony patches plus a friendly-fire team-fix MissionBehavior.

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
