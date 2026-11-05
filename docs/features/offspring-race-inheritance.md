# Offspring Race Inheritance (LOTR-Style)

## Overview

TAOM uses same-sex parent race inheritance: male children inherit the father's race and appearance, female children inherit the mother's race and facial features. This is thematically appropriate for Middle-earth — sons take after their fathers, daughters take after their mothers.

## Why This Exists

Vanilla Bannerlord uses the same same-sex parent logic, but includes a `Debug.SilentAssert` that checks `mother.Race == father.Race`. This is a soft assert: it only logs (and breaks into an attached debugger), never crashes or prevents execution. Cross-race couples are expected in Middle-earth, so the assert would fire on every such birth. `DeliverOffSpring_RaceAssert_Patch` (`Main/Features/RaceAge/Hooks`) is a transpiler that NOPs the assert call, so it never runs; it is noise reduction, and the birth proceeds the same without it.

## Architecture

Vanilla's `DefaultHeroCreationModel.GetCharacterTemplateForOffspring` already provides the rule: male children get `father.CharacterObject`, female children get `mother.CharacterObject`. TAOM overrides nothing for it; `TaomHeroCreationModel` used to restate the same two returns and no longer does. The model overrides only `GetCivilianEquipment`/`GetBattleEquipment`, so an adult lord created at runtime wears a peer kit; that half belongs to [generated-lord-kits.md](generated-lord-kits.md).

### Component Diagram

```
DefaultHeroCreationModel (vanilla GameModel)
  └─ GetCharacterTemplateForOffspring
        ├─ male child  → father.CharacterObject (father's race)
        └─ female child → mother.CharacterObject (mother's race)
              │
              ▼
HeroCreator.DeliverOffSpring (vanilla, static)
  └─ Debug.SilentAssert ← NOPped by DeliverOffSpring_RaceAssert_Patch
              │
              ▼
  CreateHero(template) → CharacterObject.CreateFrom(parent)
              │            └─ FillFrom copies Race from same-sex parent
              ▼
  New Hero with same-sex parent's race
```

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs` | Transpiler that silences vanilla's cross-race `SilentAssert` in `HeroCreator.DeliverOffSpring` |

## Integration

- `DeliverOffSpring_RaceAssert_Patch` is applied by `Patch13_RaceAge` in `SubModule`; the offspring template itself comes from vanilla's model

## Dependencies

- None beyond standard TaleWorlds assemblies

## Relationship to RaceAge Feature

This is part of the broader RaceAge feature. The race inherited by offspring determines which age/fertility config applies to them from `race_age_config.json`. A child born to a Human father will have Human lifespan (`maxAge` 200 in `race_age_config.json`) and Human fertility rates, regardless of the mother's race.

## Face Generation Note

Vanilla's `GetStaticBodyProperties` method for offspring uses `hero.Mother.CharacterObject.Race` for face mesh generation — always the mother's race, regardless of the child's actual race. This means a child of a Human father and Elf mother will have the father's race (Human) but facial features generated from the Elf mesh. This is deferred as a cosmetic issue — in most cases the visual difference is minimal, and it could be argued as lore-appropriate (half-elven features).

## Changelog

_No dated entries mapped from the global `CHANGELOG.md` yet — this section is the go-forward home for offspring-race-inheritance changes. See the repository-root `CHANGELOG.md` for full chronological history._

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
