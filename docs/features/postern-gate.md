# Postern Gate

## Overview

The Postern Gate is a military building every town and castle can hold. It cuts the troops the player's party (or the army it leads) loses when it breaks into or out of a siege at that fief: 50, 67 and 75 percent at levels 1, 2 and 3. It is the first building on TAOM's shared building pipeline, described in [taom-buildings.md](taom-buildings.md). Since #855.

## Why This Exists

- **Vanilla behavior:** `DefaultTroopSacrificeModel` charges a party `5 + 12 to 24 percent of its regulars` for breaking into or out of a besieged settlement, less the Tactics skill reduction, then less the Improviser perk; a break through a port (out of it, or in from the sea) costs nothing while no blockade is active. Nothing a fief builds changes it.
- **TAOM requirement:** a fief with a hidden way in and out should make relieving or abandoning a siege there cheaper, and the building should carry the flavour of its culture.
- **Without this feature:** every sally and every relief of a besieged fief costs the same, whatever the defenders built.

The building is one family with six names, chosen by the fief's culture:

| Name | Cultures |
|---|---|
| Postern Gate (default) | any culture not listed below |
| Hidden Door | `erebor`, `eredluin` |
| Culvert | `vlandia` (Rohan), `southrhovanion` |
| Sally Port | `gondor`, `arthedain`, `cardolan`, `rhudaur` |
| Hidden Way | `rivendell`, `mirkwood`, `lothlorien`, `lindon` |
| Bolt-Hole | `mordor`, `isengard`, `gundabad`, `dolguldur`, `goblin`, `mistymountainorcs`, `bluecraig` |

`empire` is Dunland and `battania` is Khand in this data, so both fall under the default. `PosternGateDefinitionTests` checks every listed id against the culture data. The six building type ids are saved in player saves: add new ids, never rename or remove one ([taom-buildings.md](taom-buildings.md)).

## Architecture

### Design Challenge

- The cut cannot key on the party: `BreakInOutBesiegedSettlementAction` hands the model only the main party, so only the player's party (or the army it leads) pays this toll.
- The cut must compose with the Improviser perk and the Tactics reduction without double counting.
- Registering, seeding and saving the building is the shared pipeline's job ([taom-buildings.md](taom-buildings.md)); this feature adds the rule and its settings.

### Solution Approach

The loss cut is the campaign `TroopSacrificeModel` slot (`TaomTroopSacrificeModel : DefaultTroopSacrificeModel`). It calls `base`, then adds `PosternGateService.CutFor` for the Postern Gate in `siegeEvent.BesiegedSettlement`, and holds the result at zero. The override must keep calling base: the co-op mod's transpiler on `DefaultTroopSacrificeModel.GetLostTroopCount` runs only through it. The factor is `-r(level) * (1 + SumOfFactors)`: the engine adds factors, so the cut is taken of what is left after the factors already on the number. `ExplainedNumber.SumOfFactors` is public (v1.5.5 `ExplainedNumber.cs:147`). With Improviser (`-0.25`) and level 3 the factor is `-0.5625`. The base model builds its number without descriptions, so no variant name is attached to the factor.

The MCM switch (`Fief Management / Postern Gate`, default on) turns off the cut only. The building stays registered and seeded, so switching it off never strips a save. While the switch is off the building's effect line is hidden too (the definition's level text returns nothing). The switch is read at every break-out check, which runs each frame in the siege menu, so the provider caches the settings object (`CampaignHotPathSettingsProvidersTests`).

### Component Diagram

```
postern_gate.json ── PosternGateConfigProvider ──┐
                                                 ├─ PosternGateDefinition ─ TaomBuildingCatalog (TaomBuildings)
TaomSettings.EnablePosternGate ─ SettingsProvider┘

SiegeEvent.BesiegedSettlement ─ TaomTroopSacrificeModel ─ PosternGateService ─ ITownBuildingsAdapter
```

## Configuration

### Config File: `Main/_Module/ModuleData/settlement_buildings/postern_gate.json`

| Field | Type | Valid |
|---|---|---|
| `costs` | array of 3 integers | each 1 to 20000, not decreasing; the cost of reaching level 1, 2 and 3 |
| `reductions` | array of 3 numbers | each finite, 0 to 0.85, not decreasing; the share of losses removed at level 1, 2 and 3 |

An invalid field reverts to its default on its own, with one summary warning naming every field that reverted. A missing file or a file that is not a JSON object reverts both. The file is read once per process; an edit needs a restart.

### Current Values

| Level | Cost | Reduction |
|---|---|---|
| 1 | 400 | 50 percent |
| 2 | 700 | 67 percent |
| 3 | 1000 | 75 percent |

Costs: 400 pays level 0 to 1 (`Building.cs:124`, `BuildingType.cs:127-134`), and the Castle Charters 0.8 factor applies (`Building.cs:119-124`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/PosternGate/PosternGateModule.cs` | the feature module: services, the model slot |
| `Main/Features/PosternGate/PosternGateService.cs` | the factor rule and `CutFor` |
| `Main/Features/PosternGate/PosternGateDefinition.cs` | the six variants, their cultures, strings and effect line |
| `Main/Features/PosternGate/PosternGateConfigProvider.cs` | validating loader for the JSON |
| `Main/Features/PosternGate/PosternGateSettingsProvider.cs` | the MCM switch, cached lazily |
| `Main/Features/PosternGate/Models/TaomTroopSacrificeModel.cs` | the `TroopSacrificeModel` override |
| `Main/_Module/ModuleData/settlement_buildings/postern_gate.json` | costs and reductions |
| `Main/_Module/GUI/SpriteParts/ui_taom/TAOM/TownManagement/taom_building_postern_gate.png` | the town-screen icon |
| `Main/_Module/GUI/SpriteParts/ui_taom/TAOM/TownManagement/taom_building_postern_gate_t.png` | the small icon |
| `tools/building_art/provenance.json` | where the art came from |
| `Main/Composition/TaomFeatureModule.cs` | the composition hook (`RegisterCampaignObjects`) the pipeline uses |

The shared pipeline's files are listed in [taom-buildings.md](taom-buildings.md).

## Dependencies

- `TaomBuildingsModule` (the shared pipeline): the catalog collects this feature's definition.
- `TaomSettings.EnablePosternGate` (MCM): the cut switch.

## Tests

- `PosternGateServiceTests`, `PosternGateConfigProviderTests`, `ShippedPosternGateConfigTests`, `PosternGateSettingsProviderTests`, `PosternGateDefinitionTests`, `PosternGateWiringTests`
- `CampaignHotPathSettingsProvidersTests` (the lazy settings accessor)
- `TaomBuildingBindingTests`, `TaomBuildingCatalogTests`, `TaomBuildingSeedingServiceTests`, `TaomBuildingsPatch108Tests`, `TaomBuildingsWiringTests`, `TownBuildingsAdapterSourceTests`
- `FeatureModuleHooksTests` (`RegisterCampaignObjects_RunsBeforeBehaviors_CampaignOnly`, `SaveOwningThrow_FailsClosed`)

## Known Limits

- Removing TAOM from a save drops the building and clears the build queue of each town that had it (`Town.AfterLoad`).
- AI fiefs build it too. Their next build is a random pick among what they can still build, except that Keep Wardens (#889) queues the Guard House first, so the Postern Gate takes its turn among the remaining picks.
- Only the player's party, or the army it leads, pays this toll: the engine passes `MainParty` to the break-in and break-out action.
- The hover tooltip shows the previous level's line, as for every vanilla building (`Building.GetBonusExplanation` uses `CurrentLevel - 1`).
- Moving a culture between variants affects only fiefs seeded later.
- The building icon needs the sprite `TAOM\TownManagement\taom_building_postern_gate` and its `_t` variant. Until the art is baked the postfix leaves the engine's `building_default`: it returns the TAOM name only when `SpriteData.GetSprite(name)` finds it.
- The cut reads `siegeEvent.BesiegedSettlement`. Break-in and break-out from a port use the same key.

### In-game checklist (owed)

- A castle in an old save gets the gate after load.
- Each culture's name and, after the art is baked, icon.
- Level 1 break-in and break-out, with and without the building.
- The switch off hides the effect line and the cut.
- The seeding log count is about the number of fortifications.

## How to Change a Value

1. Edit `postern_gate.json` (a restart applies it).
2. To change a culture's name, move its id between variants in `PosternGateDefinition.cs`; `PosternGateDefinitionTests` rejects an id in two variants or one the culture data lacks.

## Changelog

2026-10-18: first build on feat/settlements-buildings (#855): building family, loss cut, shared building pipeline, MCM switch, JSON config. Art not baked yet; not yet seen in game.

## GitHub Issue

- **Issue:** [#855](https://github.com/haterade22/TAOM/issues/855) Postern Gate: town and castle building cuts siege break-in losses ([#915](https://github.com/haterade22/TAOM/issues/915) was a
  duplicate, closed)
- **Status:** Shipped on `bannerlord-1.5.x` in `8b015667f` (v2.0.35). Issue open: the in-game check and the editor bake
  of the building icon are owed; the paid translation of its strings is tracked in [#931](https://github.com/haterade22/TAOM/issues/931).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/taom-buildings.md](./taom-buildings.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
