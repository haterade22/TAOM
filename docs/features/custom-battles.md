# Custom Battles

## Overview

TAOM Custom Battle support replaces vanilla factions, commanders, and troops in the Custom Battle screen with TAOM-specific content. All TAOM cultures (Gondor, Mordor, Rohan, etc.) appear as selectable factions, their lords appear as commanders, and each formation slot defaults to the culture's own troop wherever that troop fits the slot (see "Formation-to-Troop Mapping"). A team-fix MissionBehavior prevents friendly fire bugs in both field and siege custom battles.

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

No UI patches needed — TAOM's `CustomBattleScreen.xml` GUI prefab automatically overrides vanilla via Gauntlet module load order (TAOM loads after the CustomBattle module).

### Commander filter+cap (per faction)

Vanilla `CustomBattleSideVM.RefreshValues()` adds every entry from `CustomBattleData.Characters` to `CharacterSelectionGroup.ItemList`. With TAOM's expanded lord pool, this means picking "Dunland" still showed every culture's commanders. Vanilla `OnCultureSelection` only updates banner colors — it does NOT re-filter the dropdown.

The fix is a singleton hook (`ISideCommanderFilter` / `SideCommanderFilter`) that resolves a culture's commanders via `CustomBattleService.GetCommanderIdsForFaction(factionId, takeMax)`. The service applies `OrderBy(Id, OrdinalIgnoreCase).Take(takeMax)` so the cap is deterministic across launches. Cap is `SideCommanderFilter.MaxCommandersPerCulture = 3`.

Both side-VM postfixes log a `LogWarning` to `rgl_log.txt` if a culture has zero matching commanders, so future `lords.xml` culture-tag misalignment surfaces in logs instead of silently regressing the dropdown to the unfiltered list.

### Curated commander lists (per faction)

The default top-3-alphabetical behavior never reliably surfaces the named LOTR lords a player wants (Boromir, Théoden, Sauron…), and its 2-segment-id regex (`^lord_[A-Za-z0-9]+_[A-Za-z0-9]+$`, the `GetCommanderIds_ExcludesSubLords` guarantee) **excludes 3-segment ids outright** — Éomer `lord_4_3_1`, Éowyn `lord_4_3_2`, the lesser Nazgûl `lord_1_48_1/2/3` can never appear no matter the cap.

A data-driven config (`custom_battle/custom_battle_commanders.json`) maps each faction key (culture `StringId`) to an **ordered** list of lord ids. `CustomBattleService.GetCommanderIdsForFaction` branches on `ICustomBattleCommandersProvider.HasCuratedEntry(factionId)`: a configured faction returns that exact list, in order, **bypassing** the regex, the `takeMax` cap, and the culture filter (so 3-segment ids and cross-culture lords are allowed). Unconfigured factions keep the default behavior unchanged.

Because curated resolution bypasses the culture filter, a lord may be listed under a faction whose `StringId` differs from the lord's own culture — e.g. Khamûl (`lord_1_48`, `Culture.dolguldur`) and Duinhir (`lord_WE9_l`, `Culture.empire`) both appear under their requested faction. This is intentional: the config is the source of truth for what shows under each faction.

**Curated lords built from vanilla lords need a Custom Battle stub.** Sauron, the Witch-king, Boromir, Théoden and the other lords `lords.xslt` rebuilds from vanilla SandBox lords do not exist in a Custom Battle on their own: SandBox registers its `lords.xml` for `Campaign` and `CampaignStoryMode` only (the same in v1.3.0 and v1.5.3), so the XSLT has nothing to match. `characters/custom_battle_lords.xml`, registered for `CustomGame` only and placed before the `lords` node in `SubModule.xml`, holds a bare `id` + `culture` stub per such curated commander. `MBObjectManager.CreateMergedXmlFile` applies each node's XSLT to everything merged before it, so `lords.xslt` rebuilds each stub into the full character. Before this file (2026-10-02) nine of Mordor's twelve ids failed to resolve, so Mordor showed only the three lesser Nazgûl, which this branch defines in `characters/lords.xml`. Gondor showed only Duinhir and Rohan only Éomer and Éowyn, because each list's other ids were missing. `CustomBattleLordStubsTests` pins the load order, the transform output (name, hero, face, equipment) and that every stub is a curated commander.

The master `CustomBattleData.Characters` list (`GetCommanderIds()`) is deliberately **unchanged** — it stays regex-filtered. The per-faction dropdown is rebuilt directly from `GetBasicCharacter`-resolved objects (`CommanderSelectorRebuilder.Apply`), independent of the master list, so curated 3-segment ids surface in the dropdown without polluting the global pool. `CuratedDropdownIndependenceTests` pins this decoupling.

Two side effects of the stubs, kept on purpose (Mike, 2026-10-02): all 21 stub ids on this branch match the master list's `^lord_X_Y$` pattern, so in Sergeant mode vanilla's random player-side general (`CustomBattleData.Characters.GetRandomElement()`) can now be Sauron, the Witch-king or a Nazgûl. And Khamûl (`lord_1_48`, `Culture.dolguldur`) now heads Dol Guldur's default (uncurated) list, which reads Khamûl, Vrâkmug, Shâgthul instead of Vrâkmug, Shâgthul, Gûrnak. A Custom Battle Sauron fights without TAOM's race combat rules (his CTB, knockdown resistance and crush): those live in the campaign-only `TaomCombatMechanicsModel`, and Custom Battle's damage model is `TaomCustomBattleCreatureDamageModel` (a `CustomAgentApplyDamageModel` subclass for creature bandits) without them. His signature strikes do run there.

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
- Has a faction banner: `faction_banner_key` is present and parses to at least one layer. Vanilla `CustomBattleHelper.GetCustomBattleParties` recolours layer 0 of each side's culture banner on Start without a length check, so a culture with no key throws `ArgumentOutOfRangeException` (crash f9a7181d). This keeps out vanilla's minor cultures `nord`, `vakken` and `darshi`, leaving the 22 TAOM factions.
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

A Custom Battle registers cultures as plain `BasicCultureObject` (`CustomGame.OnRegisterTypes`), whose deserializer reads none of these attributes, so `ObjectManagerAdapter` reads them back from the merged `SPCultures` XML for the current game type (`CultureTroopIdReader`, through `MBObjectManager.GetMergedXmlForManaged`). In a campaign the values come from `CultureObject` directly. Until 2026-10-02 the adapter read only `CultureObject`, so in Custom Battle every TAOM default came back empty, and vanilla's troop picker marked the first matching troop in load order as the default. The log line `ObjectManagerAdapter: read troop ids for N cultures from the merged SPCultures XML (CustomGame)` confirms the re-read ran.

**A troop is offered only when the slot can show it.** Vanilla's slot list holds only soldiers that are not obsolete (`ArmyCompositionGroupVM`), and `ArmyCompositionItemVM.IsValidUnitItem` then keeps a troop only when it is the slot's culture and its `DefaultFormationClass` fits (infantry 0 or 5, ranged 1, cavalry 2, 6 or 7, horse archer 3); a default outside that list is ignored and the slot's first troop becomes the default. `CustomBattleService.GetDefaultTroopIdForFormation` applies the same check (`IsEligibleForSlot`). When vanilla already picked a troop, it returns null for a TAOM troop that fails the check, so vanilla's pick stands. When vanilla has no pick, a fitting troop is still preferred, and failing that it returns the first TAOM candidate that loads, because the default has a second consumer: at Start, `CustomBattleHelper.PopulateListsWithDefaults` spawns it unchecked for any slot left empty, and a null default for slots 0-2 throws once that slot has troops to spawn (a null horse-archer default is redistributed instead). That keeps Abanissa and Shaghana, which have no soldiers of their own culture, from crashing: every slot is empty for them, and as at `9e2a39f4` their whole army spawns as the one Harad foot archer their horse-archer slot names, because that slot keeps 100% when all four are invalid. The shipped culture data fits that check in 33 of the 88 culture-and-slot pairs (all 22 playable cultures times 4 slots): every `ranged_elite_militia_troop` is a foot archer, most `elite_basic_troop` entries are infantry nobles, and seven cultures (abanissa, shaghana, umbar, battania, bluecraig, mistymountainorcs, lothlorien) name another culture's troops. Defaulting those slots to a TAOM troop needs slot-correct ids in the culture data, a separate change.

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
| `Main/Features/CustomBattles/TaomFactionSelectionVM.cs` | Custom faction-selection VM with prev/next navigation |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_Constructor_Patch.cs` | Harmony postfix — swaps FactionSelectionGroup; fires initial OnCultureSelection callback |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCultureSelection_Patch.cs` | Harmony postfix — filters commander dropdown on faction click (cap=3) |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_RefreshValues_Patch.cs` | Harmony postfix — defensive re-filter for refresh events |
| `Main/Features/CustomBattles/Hooks/ISideCommanderFilter.cs` | Hook interface — resolves commanders for a culture |
| `Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs` | Hook impl — calls service with `MaxCommandersPerCulture = 3`; warns + skips any commander id that fails to resolve |
| `Main/Features/CustomBattles/Config/custom_battle_commanders.json` *(in `Main/_Module/ModuleData/custom_battle/`)* | Curated per-faction commander lists (faction culture id → ordered lord ids) |
| `Main/Features/CustomBattles/Config/CustomBattleCommandersConfig.cs` | DTO for the curated config (`Dictionary<string, List<string>> Factions`) |
| `Main/Features/CustomBattles/Config/ICustomBattleCommandersProvider.cs` | Provider interface — `HasCuratedEntry` (validate-before-lookup gate) + `GetCuratedCommanderIds` |
| `Main/Features/CustomBattles/Config/CustomBattleCommandersProvider.cs` | Validating `Lazy` singleton provider (mirrors `CultureConversionConfigProvider`) |
| `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` | Static helper — calls vanilla `SelectorVM<T>.Refresh(items, 0, onChange)` to safely rebuild the selector. Reads existing `_onChange` via cached `FieldInfo` so Refresh's overwrite preserves the wiring. (Issue #105 — replaced manual `Clear() + AddItem(*N) + reflection-on-_selectedIndex` approach to match the canonical safe rebuild pattern.) |
| `Main/Features/CustomBattles/Hooks/CustomBattleSideVM_OnCharacterSelection_Patch.cs` | Defensive Prefix on the private `OnCharacterSelection(SelectorVM<CharacterItemVM>)` — returns `false` when `selector?.SelectedItem == null`. Stops vanilla NRE that surfaced when `SelectedIndex` setter fires `_onChange.Invoke` with an empty `ItemList` (Issue #105 Bug 1). |
| `Main/Adapters/IObjectManagerAdapter.cs` | Adapter interface + CultureInfo/CharacterInfo DTOs |
| `Main/Adapters/ObjectManagerAdapter.cs` | ObjectManager bridge implementation; in a Custom Battle it reads culture troops from the merged SPCultures XML |
| `Main/Adapters/CultureTroopIdReader.cs` | Pure parser: culture id to troop ids from a merged SPCultures document |
| `Main/_Module/ModuleData/characters/custom_battle_lords.xml` | CustomGame-only stubs that `lords.xslt` rebuilds into the curated vanilla-derived commanders |
| `Main/_Module/GUI/Prefabs/CustomBattle/` | 5 Gauntlet UI prefab XMLs (pre-existing) |

## Dependencies

- **CustomBattle module** — vanilla DLL providing `CustomBattleData`, `CustomBattleHelper`, `CustomBattleScreen`
- **Harmony** — patch framework for intercepting static property getters and method calls
- **DryIoc** — IoC container for service/hook registration
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

Patches and `CustomBattleTeamFixBehavior` are thin entry points — tested indirectly via in-game smoke tests per ADR-002.

## How-To

### How to add a new TAOM culture to Custom Battle

1. Create the troop tree XML in `Main/_Module/ModuleData/troops/troops_{culture}.xml`
2. Register it in `SubModule.xml` with `<GameType value="CustomGame"/>` and `<GameType value="EditorGame"/>`
3. Ensure the culture has `can_have_settlement="true"` and a `faction_banner_key` in its `SPCultures` definition. A culture without the key is left out of the picker, and the only trace is the faction count that `CustomBattleFactionsHook` logs ("Loaded 22 TAOM factions" today).
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

## Changelog

- 2026-10-02: TAOM's formation default troops now apply in Custom Battle where the slot can show them (33 of 88 culture-and-slot pairs). The adapter read troop ids only from `CultureObject`, but a Custom Battle loads cultures as `BasicCultureObject`, so TAOM's default never applied; it now reads them from the merged `SPCultures` XML. For the re-skinned cultures an eligible TAOM troop also replaces vanilla's Calradian pick (Rohan infantry no longer defaults to the Vlandian Swordsman); where TAOM's troop does not fit the slot, vanilla's pick stays.
- 2026-10-02: the curated Mordor, Gondor and Rohan commanders now exist in Custom Battle. 21 of them on this branch (24 on bannerlord-1.5.x, which has no `lord_1_48_1/2/3` in `characters/lords.xml`) are vanilla SandBox lords rebuilt by `lords.xslt`, and SandBox loads `lords.xml` for campaigns only; `characters/custom_battle_lords.xml` now supplies a stub for each, which `lords.xslt` rebuilds at load. Before, Mordor showed only the three lesser Nazgûl. On this branch the Nazgûl templates set no race, so they render human.
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

<!-- backlinks-end -->
