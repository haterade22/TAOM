# Offspring Race Inheritance (LOTR-Style)

## Overview

TAOM uses same-sex parent race inheritance: male children inherit the father's race and appearance, female children inherit the mother's race and facial features. This is thematically appropriate for Middle-earth — sons take after their fathers, daughters take after their mothers.

## Why This Exists

Vanilla Bannerlord uses the same same-sex parent logic, but includes a `Debug.SilentAssert` that checks `mother.Race == father.Race`, which fires on every cross-race birth (e.g., Human + Elf). While not a crash in release builds, it triggers debugger breakpoints and logs noise. TAOM removes this assert since cross-race couples are expected in Middle-earth.

## Architecture

### Two-Layer Solution

1. **TaomHeroCreationModel** (GameModel override) — Overrides `GetCharacterTemplateForOffspring` to use same-sex parent logic: male children get `father.CharacterObject`, female children get `mother.CharacterObject`. This matches vanilla behavior but is explicitly defined so TAOM controls the logic.

2. **DeliverOffSpring_RaceAssert_Patch** (Harmony Transpiler) — Surgically removes the `Debug.SilentAssert(mother.Race == father.Race)` call from `HeroCreator.DeliverOffSpring` at IL level. The assert checks the parents' races (not the child's), so it still fires for cross-race couples even with the GameModel override.

### Component Diagram

```
TaomHeroCreationModel (GameModel)
  └─ GetCharacterTemplateForOffspring
        ├─ male child  → father.CharacterObject (father's race)
        └─ female child → mother.CharacterObject (mother's race)
              │
              ▼
HeroCreator.DeliverOffSpring (vanilla, static)
  └─ Debug.SilentAssert ← REMOVED by Transpiler
              │
              ▼
  CreateHero(template) → CharacterObject.CreateFrom(parent)
              │            └─ FillFrom copies Race from same-sex parent
              ▼
  New Hero with same-sex parent's race
```

### Why Not Just a GameModel Override?

The `SilentAssert` in `DeliverOffSpring` checks `mother.CharacterObject.Race == father.CharacterObject.Race` — the **parents'** races, not the child's template. Even though our GameModel ensures the child always gets the father's race, the assert still fires because the parents are different races. The transpiler is needed to eliminate this.

### Why Not Just a Transpiler?

A transpiler alone would suppress the assert but leave vanilla's same-sex-parent race logic in place. Female children would still get the mother's race. The GameModel override is the proper extension point for changing race inheritance behavior.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/RaceAge/Models/TaomHeroCreationModel.cs` | GameModel — father's CharacterObject always used as offspring template |
| `Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs` | Harmony transpiler — removes SilentAssert from DeliverOffSpring |

## Integration

- `TaomHeroCreationModel` registered in `SubModule.OnGameStart` via `campaignStarter.AddModel()`
- Transpiler registered under `Patch13_RaceAge` category in `SubModule.OnGameInitializationFinished`

## Dependencies

- None beyond standard TaleWorlds assemblies and HarmonyLib

## Relationship to RaceAge Feature

This is part of the broader RaceAge feature. The race inherited by offspring determines which age/fertility config applies to them from `race_age_config.json`. A child born to a Human father will have Human lifespan (85 years) and Human fertility rates, regardless of the mother's race.

## Face Generation Note

Vanilla's `GetStaticBodyProperties` method for offspring uses `hero.Mother.CharacterObject.Race` for face mesh generation — always the mother's race, regardless of the child's actual race. This means a child of a Human father and Elf mother will have the father's race (Human) but facial features generated from the Elf mesh. This is deferred as a cosmetic issue — in most cases the visual difference is minimal, and it could be argued as lore-appropriate (half-elven features).
