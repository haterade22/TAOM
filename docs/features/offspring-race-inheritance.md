# Offspring Race Inheritance (LOTR-Style)

## Overview

TAOM overrides Bannerlord's offspring race system so that children always inherit their father's race. This is lore-accurate for Middle-earth — Eldarion, son of Aragorn (Man) and Arwen (Elf), is a Man of the Dunedain, not an Elf.

## Why This Exists

Vanilla Bannerlord determines offspring race by same-sex parent:
- Male child → father's `CharacterObject` (and race)
- Female child → mother's `CharacterObject` (and race)

This creates inconsistencies in LOTR:
- A daughter of a Human lord and Elven lady would be classified as an Elf
- A son of the same couple would be classified as a Human
- Siblings would have different races

Additionally, vanilla includes a `Debug.SilentAssert` that checks `mother.Race == father.Race`, which fires on every cross-race birth. While not a crash in release builds, it triggers debugger breakpoints and logs noise.

## Architecture

### Two-Layer Solution

1. **TaomHeroCreationModel** (GameModel override) — Overrides `GetCharacterTemplateForOffspring` to always return `father.CharacterObject`, regardless of child gender. The child inherits the father's race, culture template, and character properties.

2. **DeliverOffSpring_RaceAssert_Patch** (Harmony Transpiler) — Surgically removes the `Debug.SilentAssert(mother.Race == father.Race)` call from `HeroCreator.DeliverOffSpring` at IL level. The assert checks the parents' races (not the child's), so it still fires for cross-race couples even with the GameModel override.

### Component Diagram

```
TaomHeroCreationModel (GameModel)
  └─ GetCharacterTemplateForOffspring → always father.CharacterObject
        │
        ▼
HeroCreator.DeliverOffSpring (vanilla, static)
  └─ Debug.SilentAssert ← REMOVED by Transpiler
        │
        ▼
  CreateHero(template) → CharacterObject.CreateFrom(father)
        │                   └─ FillFrom copies Race from father
        ▼
  New Hero with father's race
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
