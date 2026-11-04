# TAOM Buildings

## Overview

The shared pipeline for buildings TAOM adds to towns and castles: type registration, new-game variant choice, seeding into loaded saves and the town-screen sprite and effect line. Its first user is the [Postern Gate](postern-gate.md). Since #855.

## Architecture

### Design Challenge

- A new `BuildingType` must exist before the campaign builds its lists, and a save made before the building existed holds no such building.
- `Town.AfterLoad` removes any building whose type is null or not ready, then clears the town's whole build queue. A failed registration would silently strip every building of the type from a save.

### Shared building pipeline

A feature supplies a `TaomBuildingDefinition` (family id, variants with their cultures, three level costs, military flag, sprite name and a per-level effect line) by registering it in the container; Postern Gate does so in `PosternGateModule`. The catalog collects every registered definition when it is first resolved, at campaign start. The pipeline then does five things:

1. **Registers the types.** `TaomFeatureModule.RegisterCampaignObjects` runs inside `FeatureModuleHooks.AddGameStartContent`, ahead of the module's behaviors and models, for a `CampaignGameStarter` only. `TaomBuildingsModule` calls `BuildingTypeRegistrarAdapter` (`Main/Adapters/`), the only code that does `new BuildingType`: `RegisterPresumedObject`, then `Initialize` with start level 0, variance chance 0 and no `BuildingEffectEnum` entries. On a save load `RegisterPresumedObject` returns the type the saved town already references, and `Initialize` makes it ready. `OnGameStart` runs before `InitializeDefaultCampaignObjects` and `CreateLists` (`Campaign.cs:1410`, `:1417`, `:1428`).
2. **Fails closed.** `TaomBuildingsModule.OwnsSaveData` is true although it writes no `SyncData`: a module whose save holds its objects owns save data. The runner's campaign-start step throws for a save-owning module, so a registration failure does not let `Town.AfterLoad` strip buildings unnoticed. What the player sees after that throw is UNVERIFIED: Patch37's finalizer on `Module.OnApplicationTick` may swallow it and the loading step retry (decision 48 in `plans/_audit/2026-09-23-opus/DECISIONS.md`, not settled here).
3. **Picks the variant for a new game.** `TaomBuildingModel : DefaultBuildingModel` answers `CanAddBuildingTypeToTown` from the catalog. The only caller is the engine's new-game pass, which adds every allowed type a town lacks.
4. **Seeds a loaded save.** `TaomBuildingSeedingBehavior` runs on `OnGameLoadedEvent` and passes `ICoopSessionProvider.IsAuthority`. `TaomBuildingSeedingService.Plan` adds the culture's variant to each fortification that holds no member of the family, so a culture change after seeding never adds a second member. A peer without authority (a co-op client) reads and writes nothing. The pass fails open per fief: a throw out of an `OnGameLoaded` handler loops the loading screen, so a failed fief is counted, one warning is logged, and the next load retries (the pass is idempotent). It writes no save data of its own: the buildings it adds are ordinary town buildings.
5. **Dresses the town screen.** Patch108 has two postfixes: `DevelopmentItemVisualButtonWidget.DetermineSpriteImageFromSpriteCode` (private) and `BuildingType.GetExplanationAtLevel`. The sprite postfix carries `[ClientOnly]` (`Patch108_TaomBuildings.cs:21`, commit `6d7e67f3f`), so a dedicated server leaves it out; the effect-line postfix has no marker. Both go through `ITaomBuildingUiHook`, which the catalog implements. A fault leaves the engine's result and logs one warning per class.

### Rules

- Variant ids are lowercase: the town screen derives the sprite code from the lowercased id.
- Never rename or remove a shipped variant id. Ids are saved in player saves; add new ones.

### Adding another TAOM building

1. Write a `TaomBuildingDefinition` with exactly three costs and register it with `registrator.RegisterDelegate`.
2. Add its module to `FeatureModules.All`. List order does not matter: every `RegisterServices` runs before anything resolves.
3. Add its sprite (`<name>` and `<name>_t`) and its strings (`{=KEY}Default`, then `/localize`).
4. Give it a service that reads its level through `ITownBuildingsAdapter.GetLevel`.

### Entity state matrix (OnGameLoaded seeding)

| State | Result |
|---|---|
| Fief holds a family member | skipped |
| Fief holds none | variant added at level 0 |
| Town and castle | both seeded (`Town.AllFiefs` yields towns, then castles) |
| Village or hideout | never listed |
| Besieged at load | added (level 0 cuts nothing) |
| Culture changed since seeding | skipped (a member is present) |
| Peer without authority (co-op client) | skipped, no fief read |
| Registration failed | campaign start fails closed first; never reached |
| Adding to one fief throws | that fief is counted and skipped, the rest are seeded, one warning, retried next load |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TaomBuildings/TaomBuildingsModule.cs` | the feature module (save-owning, fails closed) |
| `Main/Features/TaomBuildings/TaomBuildingDefinition.cs` | definition, variant and effect-line records |
| `Main/Features/TaomBuildings/TaomBuildingCatalog.cs` | built from the registered definitions; also the UI hook |
| `Main/Features/TaomBuildings/TaomBuildingSeedingService.cs` | plans and applies the seeding |
| `Main/Features/TaomBuildings/Hooks/TaomBuildingSeedingBehavior.cs` | the `OnGameLoaded` handler |
| `Main/Features/TaomBuildings/Hooks/TaomBuildingUiHooks.cs` | the lazy hook handle the patches read |
| `Main/Features/TaomBuildings/Hooks/Patch108_TaomBuildings.cs` | sprite and effect-line postfixes |
| `Main/Features/TaomBuildings/Models/TaomBuildingModel.cs` | the new-game variant gate |
| `Main/Adapters/BuildingTypeRegistrarAdapter.cs` | the only `new BuildingType` |
| `Main/Adapters/TownBuildingsAdapter.cs` | reads and extends a town's buildings by id |

## Tests

`TaomBuildingCatalogTests`, `TaomBuildingSeedingServiceTests`, `TaomBuildingsPatch108Tests`, `TaomBuildingsWiringTests`, `TownBuildingsAdapterSourceTests`, `TaomBuildingBindingTests`, `FeatureModuleHooksTests`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/postern-gate.md](./postern-gate.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
