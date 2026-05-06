# Character Creation Body Properties

## Overview

When the player picks a culture during Character Creation, the player-character preview adopts a TAOM-defined `BodyProperties` key string for that culture instead of the vanilla random-within-min/max default. The body re-applies on every culture change. Cultures not configured fall back to vanilla behavior.

## Why This Exists

- **Vanilla behavior:** When the player selects a culture in CC, vanilla generates a random body using `FaceGen.GetRandomBodyProperties(race, isFemale, BodyPropertiesMin, BodyPropertiesMax, ...)` from `default_character_creation_body_property_<culture>` in `sandbox_bodyproperties.xml`. The result is non-deterministic — every new game produces a different silhouette for the same culture.
- **TAOM requirement:** Lock in a specific cultural silhouette per culture so the starting body matches lore expectations (e.g., Rohirrim are taller/leaner, Dunlendings stockier, dwarves shorter). The modder wants to retune frequently without rebuilding the mod.
- **Without this feature:** Body silhouettes are random; cultural body archetypes can't be controlled centrally; tuning means editing vanilla XML or shipping XSLT overrides for both `BodyPropertiesMin` and `BodyPropertiesMax`.

## Architecture

### Design Challenge

`BodyProperties` is a sealed `TaleWorlds.Core` struct. Per ADR-007, it cannot cross service boundaries. The vanilla `CharacterCreationContent.SetSelectedCulture(CultureObject, CharacterCreationManager)` does not fire any event hook — Harmony is the only way to react to culture selection. Two places must be mutated to make the new body actually render: the `CharacterObject.PlayerCharacter`'s `BodyPropertyRange` (so FaceGen pulls from the new values) and `Hero.MainHero`'s scalar properties (`StaticBodyProperties`, `Weight`, `Build`) since `Hero.BodyProperties` is computed from those at access time.

### Solution Approach

Standard TAOM 4-layer stack: Harmony patch → service → adapter → engine. The patch is a thin postfix on `SetSelectedCulture` that delegates to `ICCBodyPropertiesService`. The service orchestrates `provider lookup → adapter call`. The adapter (`IPlayerBodyPropertiesAdapter`) is the only class that touches the sealed `BodyProperties` struct: it parses the XML string via `BodyProperties.FromString` and applies the result through both engine entry points.

### Component Diagram

```
charactercreation/cc_body_properties.xml
        |
  CCBodyPropertiesProvider  (loads + validates entries, lowercase culture-id keyed)
        |
  CCBodyPropertiesService   (orchestrates lookup, logging, error handling)
        |
  IPlayerBodyPropertiesAdapter  (parses BodyProperties.FromString,
                                  calls UpdatePlayerCharacterBodyProperties
                                  + sets Hero.MainHero scalars)
        ^
        |
  CharacterCreationContent_SetSelectedCulture_Patch  (Harmony postfix)
        ^
        |
  TaleWorlds CharacterCreationContent.SetSelectedCulture
```

## Configuration

### Config File: `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`

Per-culture body-properties strings. Cultures listed here override the vanilla random body during CC preview.

```xml
<CCBodyProperties>
  <Culture id="vlandia">
    <BodyProperties version="4" key="0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002" />
  </Culture>
</CCBodyProperties>
```

| Field | Type | Description |
|-------|------|-------------|
| `Culture/@id` | string | Culture string id (case-insensitive). Vanilla: `vlandia`, `empire`, `aserai`, `battania`, `sturgia`, `khuzait`. TAOM custom: `mordor`, `gondor`, `erebor`, `mirkwood`, `lothlorien`, `rivendell`, `dol_guldur`, `gundabad`, `isengard`, `umbar`, `dale` |
| `BodyProperties/@version` | int | `4` for the v1.3.15 body-key encoding |
| `BodyProperties/@key` | hex string | Exactly 128 hex characters. Shorter or empty keys are skipped with a warning |
| `BodyProperties/@age` | float (optional) | Defaults to `20` if absent (vanilla `BodyProperties.FromString` parser default) |
| `BodyProperties/@weight` | float (optional) | Defaults to `0` if absent |
| `BodyProperties/@build` | float (optional) | Defaults to `0` if absent |

### Validation

The provider rejects (and warns on) entries with:
- Missing `Culture/@id`
- Missing `<BodyProperties>` child element
- Missing or empty `key` attribute
- `key` length not equal to 128

Duplicate culture ids cause a warning and last-wins. Malformed XML logs an error and the entire file is skipped.

### TAOM Culture-ID Reference

In TAOM, several vanilla culture ids are XSLT-rebound to LOTR factions:

| Culture id | LOTR faction |
|------------|--------------|
| `vlandia` | Rohan |
| `empire` | Dunland |
| `battania` | Khand |
| `aserai` | Harad |
| `sturgia` | Barding |
| `khuzait` | Rhun |

TAOM custom cultures (`mordor`, `gondor`, `erebor`, etc.) keep their natural ids.

### Reload Scope

The provider is `Reuse.Singleton` (DryIoc) — cached for the entire Bannerlord process lifetime. **Edits to `cc_body_properties.xml` require a full Bannerlord restart**, not a save-load or a "new campaign" click.

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/CharacterCreation/ICCBodyPropertiesProvider.cs](../../Main/Features/CharacterCreation/ICCBodyPropertiesProvider.cs) | Provider interface |
| [Main/Features/CharacterCreation/CCBodyPropertiesProvider.cs](../../Main/Features/CharacterCreation/CCBodyPropertiesProvider.cs) | XML loader + validation |
| [Main/Features/CharacterCreation/ICCBodyPropertiesService.cs](../../Main/Features/CharacterCreation/ICCBodyPropertiesService.cs) | Service interface |
| [Main/Features/CharacterCreation/CCBodyPropertiesService.cs](../../Main/Features/CharacterCreation/CCBodyPropertiesService.cs) | Orchestration + structured logging |
| [Main/Adapters/IPlayerBodyPropertiesAdapter.cs](../../Main/Adapters/IPlayerBodyPropertiesAdapter.cs) | Adapter interface |
| [Main/Adapters/PlayerBodyPropertiesAdapter.cs](../../Main/Adapters/PlayerBodyPropertiesAdapter.cs) | `BodyProperties.FromString` + `UpdatePlayerCharacterBodyProperties` + Hero scalar writes |
| [Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs](../../Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs) | Harmony postfix; Patch29_CCBodyProperties |
| [Main/_Module/ModuleData/charactercreation/cc_body_properties.xml](../../Main/_Module/ModuleData/charactercreation/cc_body_properties.xml) | Config |

## Dependencies

- `ICCBodyPropertiesProvider` — loads + caches per-culture body strings
- `IPlayerBodyPropertiesAdapter` — wraps `BodyProperties.FromString`, `BasicCharacterObject.UpdatePlayerCharacterBodyProperties`, and `Hero.MainHero` scalar writes
- `IPathService` (Core) — resolves `ModuleDataPath`
- `IModLogger` (Core) — structured warning/error/info logging

## Tests

- [TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesProviderTests.cs](../../TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesProviderTests.cs) — 14 tests covering: file missing, malformed XML, configured culture, not-configured culture, null/empty cultureId, case-insensitive culture lookup, missing-id skip, missing-BodyProperties skip, missing-key skip, empty-key skip, wrong-hex-length skip, duplicate-id last-wins, caching, age/weight/build attribute preservation
- [TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesServiceTests.cs](../../TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesServiceTests.cs) — 7 tests covering: configured-culture happy path, not-configured no-op, adapter parse-failure warning, null cultureId guard, empty cultureId guard, adapter exception swallowed + logged, success info logging

The adapter (`PlayerBodyPropertiesAdapter`) is intentionally not unit-tested — it's a thin wrapper over sealed TaleWorlds engine calls (`BodyProperties.FromString`, `UpdatePlayerCharacterBodyProperties`, Hero property setters). Coverage is via in-game verification.

## How to Add a New Culture Body

1. Open Bannerlord, generate or capture the desired body in any face-customizer (CC, the in-game "edit your face" debug menu, or a save export).
2. Copy the `<BodyProperties version="4" key="..."/>` element exactly. (Optional age/weight/build attributes are honoured if present.)
3. Open `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`.
4. Add a new `<Culture id="<your_culture_id>">` block, paste the BodyProperties element inside.
5. Save and **restart Bannerlord** (the provider is process-cached).

No code changes required. Validation warnings will appear in `rgl_log.txt` if the entry is malformed.

## How to Remove a Culture Body Override

Delete the `<Culture id="...">` block from `cc_body_properties.xml` and restart. The culture falls back to vanilla random-body generation with no errors.

## GitHub Issue

- **Issue:** _Pending — to be created at session close per CLAUDE.md "issue must exist BEFORE the closing commit" rule._
- **Status:** Open
