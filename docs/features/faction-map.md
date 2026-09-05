# Faction Map

## Overview
FactionMap replaces Bannerlord's vanilla culture selection screen during character creation with an interactive Middle-earth faction map. Players see faction regions drawn as labeled polygons over the world map, can hover over regions to highlight them, click to read faction lore (name, description, traits, bonuses, special unit, strengths, weaknesses, difficulty), and confirm a faction to set their starting culture.

## Why This Exists
- **Vanilla behavior:** The character creation culture stage shows a flat list of culture names with generic descriptions. There is no geographic context, no faction lore, and no way to see how cultures relate to each other on the map.
- **TAOM requirement:** TAOM has 10+ custom cultures mapped to specific geographic regions of Middle-earth. Players need to understand what each faction is, where it is, and what its playstyle implies before committing to a culture.
- **Without this feature:** Players see a plain list of TAOM culture names that mean nothing to players unfamiliar with the lore. Culture selection has no geographic grounding.

## Architecture
### Design Challenge
Bannerlord's `CultureStageView` is a sealed class. The culture selection UI is loaded as a GauntletLayer movie backed by the vanilla `_dataSource` view model. Replacing it requires intercepting construction, releasing the vanilla movie from the layer, loading a new movie backed by `FactionSelectionVM`, and replicating the vanilla `NextStage()` flow without accessing `_dataSource.CurrentSelectedCulture.Culture` (which would be null after the VM is swapped).

The `_affirmativeAction` delegate, `_characterCreationManager`, and `_dataSource` are all private fields on `CultureStageView`, requiring `Harmony.AccessTools` for reflection-based access.

### Solution Approach
Three Harmony patches intercept `CultureStageView`'s lifecycle:

- `Patch7_FactionMap` (`CultureStageView_Constructor_Patch`) — Postfix on `CultureStageView` constructor. Delegates to `CultureStageViewCreatedHook.OnCreated`, which: loads region and faction data from JSON via `IFactionConfigProvider`, initializes `IFactionRegistryService`, releases the vanilla movie, constructs `FactionSelectionVM`, and loads the custom `CharacterCreationCultureStage` movie backed by `FactionSelectionVM`.
- `CultureStageView_Tick_Patch` — Postfix on `Tick`. Delegates to `CultureStageViewTickHook` for per-frame hover state updates.
- `CultureStageView_Finalize_Patch` — Postfix on `Finalize`. Delegates to `CultureStageViewFinalizeHook` for cleanup.

When a player confirms a faction, the `onCultureConfirmed` callback calls `ICultureSettingService.SetCultureOnCharacterCreation`, then replicates the vanilla `NextStage()` flow by: calling `SetMainCharacterName` on the character creation content, then invoking the `_affirmativeAction` delegate.

`SetCultureOnCharacterCreation` assigns `Hero.MainHero.Culture` **before** invoking vanilla `SetSelectedCulture`, so the auto-generated family name is drawn from the selected culture's `<clan_names>` rather than the stale default culture. (Vanilla's culture stage sets the culture on click, *before* name generation; the faction map calls `SetSelectedCulture` first, so it must assign the culture explicitly.) For the `vlandia` id — TAOM's Rohan — it then regenerates the clan name to bypass vanilla `FactionHelper.GenerateClanNameforPlayer`'s hardcoded `"dey Corvand"`, guarded on a non-empty `ClanNameList`. See issue [#264](https://github.com/haterade22/TAOM/issues/264). The override **must pass a non-null `Settlement`** to `NameGenerator.GenerateClanName`: the engine's `vlandia` special-case unconditionally dereferences `clanOriginSettlement.Name`, so a `null` argument NREs on every Rohan confirm (caught by the method's try/catch, but the override then silently fails and the clan keeps `"dey Corvand"`). It resolves a culture-appropriate settlement (`Settlement.All` first-of-culture, else first overall); the `ORIGIN_SETTLEMENT` text variable is unused by TAOM's `<clan_names>`, so the choice is cosmetic — only non-null-ness matters (issue [#301](https://github.com/haterade22/TAOM/issues/301)).

A separate `TrySwitchToNextMenu_Patch` guards the vanilla next-menu transition to prevent double-advance when TAOM has already advanced the stage.

Data flow:
- `FactionConfigProvider` loads `factionmap/factions.json` (faction lore) and `factionmap/regions.json` (normalized bounding boxes and capital positions) at view creation time.
- `FactionRegistryService` stores the loaded data and exposes lookup by region key and faction id.
- `FactionSelectionService` translates a region name click into a `FactionSelectionResult` POCO (name, color, description, difficulty text, derived dark panel and accent colors, banner position).
- `FactionHoverService` tracks the last-hovered faction name and emits `HoverStateChange` events only when the hovered faction changes.
- `ICultureResolverService` (`CultureResolverService`) resolves a TAOM faction id to a live `CultureObject` for passing to `SetCultureOnCharacterCreation`.
- `ILandmarkService` (`LandmarkService`) provides static landmark definitions displayed on the map.

Widgets: `PolygonWidget` draws region outlines; `FactionImageWidget` shows faction art; `BannerWidget` shows a faction banner at the capital position; `MapContainerWidget` is the root container.

### Component Diagram
```
CultureStageView constructor [Postfix Patch7_FactionMap]
    |-> CultureStageViewCreatedHook.OnCreated(viewInstance)
            |-> IFactionConfigProvider.LoadRegions() + LoadFactions()   [JSON]
            |-> IFactionRegistryService.Initialize(regions, factions)
            |-> FactionMapStaticBridge.Initialize(registry)
            |-> GauntletLayer.ReleaseMovie(originalMovie)
            |-> new FactionSelectionVM(onCultureConfirmed, onPreviousStage, ...)
            |-> GauntletLayer.LoadMovie("CharacterCreationCultureStage", FactionSelectionVM)

Player clicks region
    |-> FactionSelectionVM -> IFactionSelectionService.SelectRegion(regionName)
            |-> IFactionRegistryService.GetRegion + GetFactionForRegion
            |-> returns FactionSelectionResult (name, color, description, banner pos, etc.)

Player hovers region
    |-> CultureStageView_Tick_Patch -> CultureStageViewTickHook
            |-> IFactionHoverService.UpdateHover(currentHoveredFaction)
            |-> if changed: returns HoverStateChange to VM

Player confirms faction
    |-> onCultureConfirmed(culture)
            |-> ICultureSettingService.SetCultureOnCharacterCreation(culture, viewInstance, originalDataSource)
            |-> SetMainCharacterName via reflection
            |-> _affirmativeAction.DynamicInvoke()
```

## Configuration
Two JSON files under `Main/_Module/ModuleData/factionmap/`:

### `factions.json`
One entry per faction id. Fields:

| Field | Type | Purpose |
|-------|------|---------|
| `name` | string | Display name. **Wrap in `{=KEY}default` format** (see Localization below). |
| `color` | string | Hex color with alpha (`#RRGGBBAA`) used for map highlight and UI panels |
| `playable` | bool | Whether the faction appears as selectable for the player |
| `game_faction` | string | TAOM culture id to set on confirmation (empty = no culture). Non-text — leave raw. |
| `side` | string | `"free"`, `"evil"`, or `"neutral"`. Non-text — leave raw. |
| `description` | string | Lore text. Wrap in `{=KEY}default`. |
| `image` | string | Sprite id for faction art. Non-text — leave raw. |
| `traits` | string[] | Short trait strings shown in the info panel. Wrap each in `{=KEY}default`. |
| `bonuses` | array | `{ "text": "...", "positive": true/false }` gameplay bonus notes. Wrap `text` in `{=KEY}default`; `positive` is a bool. |
| `perks` | array | `{ "name": "...", "description": "..." }`. Wrap both fields. |
| `special_units` | array | `[ { "name": "...", "description": "..." }, ... ]`. Wrap both fields. Supports >1 entry for factions with multiple iconic units. |
| `special_unit` | object | **DEPRECATED but still accepted** for backward-compat. Legacy single-object form is coerced into a 1-entry array by `FactionDataParser.ParseSpecialUnits`. Prefer `special_units` in new content. |
| `strengths` / `weaknesses` | string[] | Displayed in info panel. Wrap each in `{=KEY}default`. |
| `difficulty` | int | 1–7 scale; 0 omits difficulty line. `1=Very Easy`, `2=Easy`, `3=Medium`, `4=Medium-Hard`, `5=Hard`, `6=Very Hard`, `7=Extreme`. The label text comes from `FactionSelectionService.FormatDifficultyText` which returns `{=taom_faction_difficulty_N}default`. |

### Localization

Every player-facing string in `factions.json` is wrapped in `{=KEY}default` format using the convention `taom_faction_<faction_json_key>_<section>_<index>` (e.g. `taom_faction_stewardship_of_gondor_perk_0_name`). At runtime, `FactionDisplayHelper.Localize(string)` wraps each VM-bound string in `new TextObject(s).ToString()`, which resolves `{=KEY}` against the GameTextManager. Plain English strings pass through unchanged.

Workflow when editing content:
1. Edit `factions.json` content (preserve `{=KEY}` prefix or add new keys per the convention).
2. Run `python tools/harvest_factionmap_strings.py` to update the auto-harvested block in `taom_module_strings.xml`. Idempotent — re-runs replace the marked block without duplication.
3. For difficulty strings or any C# code that returns keyed strings (`FactionSelectionService.FormatDifficultyText`), hand-author the matching `<string>` entry in `taom_module_strings.xml` above the auto-harvested block.
4. Run `python tools/translate_with_claude.py --lang <LANG> --module TAOM --apply` per language to propagate to all 12 languages. Cache deduplicates already-translated strings.
5. Validate: `dotnet test TAOM.Tests --filter FactionMap` covers JSON parse, key coverage, per-playable-faction shape; `LanguageDataXmlTests` covers per-language file parse.

Cross-references:
- Standing instruction: when adding/changing a cultural feat, also update factions.json — see `feedback_faction_map_update_with_cultural_feats` (memory; distilled in [lessons/data-content-cultures.md](../reviews/lessons/data-content-cultures.md)).
- Helper resolution: `Main/Features/FactionMap/FactionDisplayHelper.cs::Localize`.
- Harvester: `tools/harvest_factionmap_strings.py`.
- XSLT inheritance audit (mandatory for vlandia/empire/sturgia/battania/aserai/khuzait factions): see [`docs/reviews/rca-faction-map-phase2-codex-2026-06-01.md`](../reviews/rca-faction-map-phase2-codex-2026-06-01.md) — paraphrasing an inventory summary instead of decompiling `DefaultCulturalFeats` was the root cause of 3 HIGH findings (Dale fabricated "forest speed", Khand/Dunland wrong Battanian numbers, Harad/Rhûn vague + missed negatives).

### `regions.json`
One entry per region id (matching faction id). Fields:

| Field | Type | Purpose |
|-------|------|---------|
| `faction` | string | Faction id from `factions.json` |
| `norm_bbox` | float[4] | Normalized bounding box `[x, y, w, h]` (0–1) for polygon placement |
| `capital_pos` | float[2] | Normalized `[x, y]` for banner widget placement |

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/FactionMap/FactionMapIoC.cs` | DryIoc registrations; resolves and initializes all three hook singletons |
| `Main/Features/FactionMap/Hooks/CultureStageViewCreatedHook.cs` | Core orchestrator: loads data, swaps movie, constructs FactionSelectionVM |
| `Main/Features/FactionMap/Hooks/CultureStageViewTickHook.cs` | Per-frame hover update |
| `Main/Features/FactionMap/Hooks/CultureStageViewFinalizeHook.cs` | Cleanup on view destruction |
| `Main/Features/FactionMap/Hooks/TrySwitchToNextMenu_Patch.cs` | Guards vanilla next-menu advance |
| `Main/Features/FactionMap/FactionConfigProvider.cs` | JSON loader for `factions.json` and `regions.json` |
| `Main/Features/FactionMap/FactionRegistryService.cs` | In-memory faction/region registry |
| `Main/Features/FactionMap/FactionSelectionService.cs` | Translates region click to `FactionSelectionResult`; computes derived colors |
| `Main/Features/FactionMap/FactionHoverService.cs` | Hover state tracking with change detection |
| `Main/Features/FactionMap/CultureResolverService.cs` | Resolves faction game_faction id to live `CultureObject` |
| `Main/Features/FactionMap/CultureSettingService.cs` | Sets the selected culture on the character creation data source. Assigns `Hero.MainHero.Culture` before vanilla `SetSelectedCulture` so the family name uses the selected culture's `<clan_names>`; overrides the vanilla "dey Corvand" hardcode for the `vlandia` (Rohan) id (#264) |
| `Main/Features/FactionMap/LandmarkService.cs` | Provides landmark definitions for map display |
| `Main/Features/FactionMap/FactionDataParser.cs` | Parses bonuses, perks, and special unit from JSON |
| `Main/Features/FactionMap/FactionDisplayHelper.cs` | UI display utilities |
| `Main/Features/FactionMap/FactionMapStaticBridge.cs` | Static accessor to `IFactionRegistryService` for widgets that cannot use DI |
| `Main/Features/FactionMap/ViewModels/FactionSelectionVM.cs` | Main view model for the custom movie |
| `Main/Features/FactionMap/Widgets/PolygonWidget.cs` | Draws faction region polygon outlines |
| `Main/Features/FactionMap/Widgets/FactionImageWidget.cs` | Faction art display |
| `Main/Features/FactionMap/Widgets/BannerWidget.cs` | Banner at capital position |
| `Main/Features/FactionMap/Widgets/MapContainerWidget.cs` | Root container widget |
| `Main/Features/FactionMap/Models/FactionData.cs` | POCO for faction lore data (`SpecialUnits[]` field, see schema below) |
| `Main/Features/FactionMap/Models/RegionData.cs` | POCO for region bbox and capital position |
| `Main/Features/FactionMap/ViewModels/FactionSpecialUnitItemVM.cs` | Per-unit item VM bound to the prefab's special-units `ListPanel` (`UnitName` / `UnitDescription` props) |
| `Main/_Module/ModuleData/factionmap/factions.json` | Live faction lore data |
| `Main/_Module/ModuleData/factionmap/regions.json` | Live region bounding box data |
| `TAOM.Tests/Features/FactionMap/FactionRegistryServiceTests.cs` | Registry lookup |
| `TAOM.Tests/Features/FactionMap/FactionSelectionServiceTests.cs` | Selection result building, color derivation, difficulty text |
| `TAOM.Tests/Features/FactionMap/FactionHoverServiceTests.cs` | Hover state change detection |
| `TAOM.Tests/Features/FactionMap/FactionConfigProviderTests.cs` | JSON parsing |
| `TAOM.Tests/Features/FactionMap/CultureResolverServiceTests.cs` | Culture resolution |
| `TAOM.Tests/Features/FactionMap/LandmarkServiceTests.cs` | Landmark data |

## Dependencies
- `ICultureObjectAdapter` (`CultureObjectAdapter`) — wraps `CultureObject` lookups
- `IPathService` — resolves `ModuleDataPath` for JSON loading
- `IModLogger` — diagnostic logging
- Harmony `AccessTools` — reflection access to `CultureStageView` private fields
- `GauntletLayer` / `UIResourceManager` — Bannerlord UI layer for movie loading and brush/sprite management

## Tests
- `FactionRegistryServiceTests.cs` — verifies `GetRegion`, `GetFaction`, `GetFactionForRegion`, and `GetAllRegionKeys` return correct data after `Initialize`.
- `FactionSelectionServiceTests.cs` — verifies `FactionSelectionResult` fields for known and unknown regions; verifies `MakeDarkPanelHex` and `MakeAccentColorHex` color derivation; verifies all 7 difficulty text mappings (Very Easy → Extreme).
- `FactionConfigProviderTests.cs` additionally covers: `LoadFactions_ParsesValidJson` (new `special_units` array form), `LoadFactions_LegacySingleSpecialUnitForm_CoercedToArray` (backward-compat for `special_unit` singular), `LoadFactions_MultipleSpecialUnits_ParsesAllEntries` (Mordor case — 2 units).
- `FactionHoverServiceTests.cs` — verifies that `UpdateHover` returns `null` when faction unchanged, `ShouldShow=false` when cleared, and a `HoverStateChange` with correct color when changed.
- `FactionConfigProviderTests.cs` — verifies JSON parsing for all faction and region fields; verifies missing file returns empty dictionaries.
- `CultureResolverServiceTests.cs` — culture id to `CultureObject` resolution.
- `LandmarkServiceTests.cs` — landmark data access.

## How to Add a New Faction Region
1. Add an entry to `Main/_Module/ModuleData/factionmap/factions.json` with the faction id as key and all required fields.
2. Add a matching entry to `Main/_Module/ModuleData/factionmap/regions.json` with `faction` pointing to the new faction id and `norm_bbox` / `capital_pos` calibrated to the map image coordinates.
3. If the faction is playable, set `"playable": true` and set `"game_faction"` to the matching TAOM culture id.
4. Rebuild and test in character creation — the new region polygon should appear and clicking it should show the faction info panel.

## Changelog
- 2026-06-07 — Surfaced the 24 Wave 1 cultural feats on the CC faction pages (26 keyed `bonuses[]` lines across 12 playable factions, harvested into `taom_module_strings.xml`).
- 2026-06-01 — #260 CC faction-map rewrite: all 16 playable factions rewritten and keyed for localization (Phases 1-3), 11 AI-language translation propagation, U+2212 minus-glyph fix, FormatDifficultyText localized, and the XSLT-inheritance/hover-Localize Codex reconcile.
- 2026-05-24 — Kingdom-card overhaul: multi-unit support (`special_units[]` array schema + `FactionSpecialUnitItemVM`), painted portraits, tuned difficulty, content refresh.
- 2026-05-04 — Fixed the spurious `banner_flag.png` ERROR on CC entry (empty banner defaults + demoted the file-not-found LogError to LogDebug).
- 2026-03-11 — Ported the external LOTRAOM_FactionMap feature into `Main/Features/FactionMap/`, replacing vanilla culture selection with a clickable Middle-earth map.

## GitHub Issue
- **Issue:** [#260](https://github.com/haterade22/TAOM/issues/260) — `feat(faction-map): rewrite CC pages + full localization sweep` (2026-06-01)
- **Status:** Shipped — Phase 1 (helper) `53ce308`, Phase 2 main (16-faction content + 599 keys) `cbbcc41`, Phase 2 deep-review fix `7f0de78`, Phase 2 Codex fix `0577363`, Phase 3 (11-language translation) — see CHANGELOG for the Phase 3 commit.
- **Follow-up:** [#264](https://github.com/haterade22/TAOM/issues/264) — `fix(character-creation): family/clan name uses default culture instead of selected; review-stage name field empty` (2026-06-01). `CultureSettingService` culture-before-name ordering + `vlandia`/Rohan clan-name override.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/character-creation.md](./character-creation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/modding/kingdoms.md](../modding/kingdoms.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/modding/recipe-add-a-kingdom.md](../modding/recipe-add-a-kingdom.md)

<!-- backlinks-end -->
