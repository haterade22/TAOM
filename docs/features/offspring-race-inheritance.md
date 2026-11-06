# Offspring Race Inheritance (LOTR-Style)

## Overview

TAOM uses same-sex parent race inheritance: male children inherit the father's race and appearance, female children inherit the mother's race and facial features. This is thematically appropriate for Middle-earth — sons take after their fathers, daughters take after their mothers.

## Why This Exists

Vanilla Bannerlord uses the same same-sex parent logic, but includes a `Debug.SilentAssert` that checks `mother.Race == father.Race`. This is a soft assert — it only logs, never crashes or prevents execution. Since cross-race couples are expected in Middle-earth, this assert fires harmlessly and is ignored.

## Architecture

Vanilla's `DefaultHeroCreationModel.GetCharacterTemplateForOffspring` (v1.4.8 `:346-353`) already provides the rule: a son gets his father's `CharacterObject` as his template and a daughter gets her mother's. TAOM overrides nothing for it. `Patch13_RaceAge` (`DeliverOffSpring_RaceAssert_Patch`) still does the race work, by silencing the cross-race assert.

### Component Diagram

```
DefaultHeroCreationModel (vanilla GameModel)
  └─ GetCharacterTemplateForOffspring
        ├─ male child  → father.CharacterObject (father's race)
        └─ female child → mother.CharacterObject (mother's race)
              │
              ▼
HeroCreator.DeliverOffSpring (vanilla, static)
  └─ Debug.SilentAssert ← harmless soft assert, ignored
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

This is part of the broader RaceAge feature. The race inherited by offspring determines which age/fertility config applies to them from `race_age_config.json`. A child born to a Human father will have Human lifespan (85 years) and Human fertility rates, regardless of the mother's race.

## Face Generation Note

Vanilla's `GetStaticBodyProperties` method for offspring uses `hero.Mother.CharacterObject.Race` for face mesh generation — always the mother's race, regardless of the child's actual race. This means a child of a Human father and Elf mother will have the father's race (Human) but facial features generated from the Elf mesh. This is deferred as a cosmetic issue — in most cases the visual difference is minimal, and it could be argued as lore-appropriate (half-elven features).

## Changelog

_No dated entries mapped from the global `CHANGELOG.md` yet — this section is the go-forward home for offspring-race-inheritance changes. See the repository-root `CHANGELOG.md` for full chronological history._

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
