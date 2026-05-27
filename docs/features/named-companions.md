# Named Companions

## Overview

Named Companions places lore-significant characters (Aragorn, Legolas, Gimli, and 15 others) as recruitable wanderer companions in specific settlements. Each companion has fixed identity, skills, appearance, equipment, unique backstory dialog, and correct race. The system is fully XML/JSON-driven — adding a new companion requires zero C# changes.

## Why This Exists

- **Vanilla behavior:** Bannerlord generates anonymous wanderers from templates with randomized names and appearance. No mechanism exists for persistent, named companions with fixed identity.
- **TAOM requirement:** Middle-earth has iconic characters that players expect to encounter. Aragorn should be findable in Bree, Legolas in Mirkwood, Gimli in Erebor — with their canonical skills, equipment, and backstory.
- **Without this feature:** Lore characters are absent from the game world. Players can only recruit generic unnamed wanderers.

## Architecture

### Design Challenge

Vanilla `CompanionsCampaignBehavior` manages all wanderers via `is_template="true"` templates. Template wanderers get randomized names, random ages, and are subject to a 10% daily kill chance. Named companions need fixed identity, specific spawn locations, and protection from the vanilla kill system.

### Solution Approach

Named companions use `is_hero="true"` + `occupation="Wanderer"` in their NPCCharacter XML. This combination:
1. Makes them **invisible** to `CompanionsCampaignBehavior` (it only iterates `IsTemplate` characters)
2. **Triggers the vanilla recruitment dialog** (`LordConversationsCampaignBehavior` checks `IsHero && Occupation == Wanderer`)
3. Gives them a **fixed StringId** (no cloning/renaming)

A custom `NamedCompanionBehavior` places them in specific settlements on new game and re-pins them on load.

### Component Diagram

```
named_companion_config.json     named_companions.xml
        |                              |
  ConfigProvider                Engine loads NPCCharacters
  (loads spawn config)          (is_hero="true", race, skills, equipment)
        |                              |
  NamedCompanionService ---------> INamedCompanionAdapter
  (spawn, ensure, race)           (PlaceInSettlement, MarkAsMet)
        |                              |
  NamedCompanionBehavior          IHeroRosterAdapter
  (OnNewGame, OnLoad)             (SetHeroRace — existing)
```

### Key Design Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| Hero type | `is_hero="true"` | Invisible to vanilla CompanionsCampaignBehavior; fixed identity; no randomization |
| Kill protection | `is_hero="true"` + `HasMet=true` | Named companions never enter `_aliveCompanionTemplates` (requires IsTemplate); HasMet is belt-and-suspenders |
| Spawn event | `OnNewGameCreatedPartialFollowUpEvent` index 1 | Heroes fully hydrated; matches StartupResourcesBehavior pattern |
| Load event | `OnGameLoadedEvent` | Re-pin displaced companions; skip recruited/in-party companions |
| Dialog | Vanilla wanderer flow | Triggers automatically for `IsHero && Occupation == Wanderer` |
| Backstory | Vanilla string key pattern | `prebackstory.{id}`, `backstory_a.{id}`, etc. read by LordConversationsCampaignBehavior |
| Race | XML `race=` + existing RacePersistenceService | Native deserialization handles it; defensive SetHeroRace as insurance |
| Faction | `faction="Faction.neutral"` in heroes.xml | Required — Hero.Deserialize accesses clan.StringId without null check |

## Configuration

### Config File: `Main/_Module/ModuleData/named_companions/named_companion_config.json`

Controls which companions spawn, where, and their race.

| Field | Type | Description |
|-------|------|-------------|
| `character_id` | string | Must match `id` in named_companions.xml |
| `spawn_settlement` | string | Settlement ID from settlements.xml |
| `race` | string | Race name from monsters.xml (human, elf, dwarf, uruk_hai, etc.) |
| `enabled` | bool | Set false to disable without removing |

### Current Values (18 companions)

| Companion | Race | Settlement | Culture |
|-----------|------|-----------|---------|
| Aragorn | human | town_EN2 (Bree) | gondor |
| Legolas | elf | town_M1 (Mirkwood) | mirkwood |
| Gimli | dwarf | town_E1 (Erebor) | erebor |
| Swan, the Cartographer | human | town_EW2 (Dol Amroth) | gondor |
| Rukumazig-khamiz, the Smith | dwarf | town_E2 | erebor |
| Leovlas, of the Silvans | elf | town_M1 | mirkwood |
| Thanor | dwarf | town_E1 | erebor |
| Whitegoat, the Pathfinder | dwarf | town_E1 | erebor |
| Igor Fahlun, the Lost | human | town_A3 | harad |
| Thyrell, the Golden | elf | town_R1 (Rivendell) | rivendell |
| Haterade | dwarf | town_E1 | erebor |
| Eohart, of the Wold | human | town_EN3 | rohan |
| BlackRose | uruk_hai | town_isengard | isengard |
| Belecthar, the Spartan | human | town_EW1 (Minas Tirith) | gondor |
| Solus, the Cairn | elf | town_R1 | rivendell |
| Maztog, the Ostler's Dread | uruk_hai | town_isengard | isengard |
| Balakhor, the Lore-Grandmaster | elf | town_R1 | rivendell |
| Noxix Iluvatar, Flame of the East | elf | town_M1 | mirkwood |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NamedCompanions/INamedCompanionService.cs` | Service interface |
| `Main/Features/NamedCompanions/NamedCompanionService.cs` | Core spawn/ensure logic with recruited-companion guard |
| `Main/Features/NamedCompanions/NamedCompanionBehavior.cs` | Thin CampaignBehaviorBase (OnNewGame + OnLoad) |
| `Main/Features/NamedCompanions/INamedCompanionConfigProvider.cs` | Config interface |
| `Main/Features/NamedCompanions/NamedCompanionConfigProvider.cs` | JSON loader with caching |
| `Main/Features/NamedCompanions/Domain/NamedCompanionDefinition.cs` | JSON-mapped POCO |
| `Main/Features/NamedCompanions/NamedCompanionIoC.cs` | DryIoc registrations |
| `Main/Adapters/INamedCompanionAdapter.cs` | Adapter interface |
| `Main/Adapters/NamedCompanionAdapter.cs` | Wraps Hero/Settlement sealed types |
| `Main/_Module/ModuleData/named_companions/named_companions.xml` | 18 NPCCharacter definitions |
| `Main/_Module/ModuleData/named_companions/named_companion_config.json` | Spawn settlement + race config |
| `Main/_Module/ModuleData/named_companions/named_companion_strings.xml` | 126 backstory strings (7 per companion) |
| `Main/_Module/ModuleData/characters/heroes.xml` | Hero entries (faction="Faction.neutral") |

## Dependencies

- `IHeroRosterAdapter` (Adapters) — sets race on hero
- `IRaceManager` (Core/Domain) — converts race name to int ID
- `IPathService` (Core/Infrastructure) — locates ModuleData directory
- `IModLogger` (Core/Logging) — structured logging
- `RacePersistenceService` (HeroRace) — persists race through save/load automatically

## Tests

- `TAOM.Tests/Features/NamedCompanions/NamedCompanionConfigProviderTests.cs` — 7 tests: JSON parsing, missing file, malformed JSON, caching, disabled entries, default enabled, empty array
- `TAOM.Tests/Features/NamedCompanions/NamedCompanionServiceTests.cs` — 13 tests: spawn placement, mark-as-met, race setting, disabled skip, missing hero warning, multiple companions, idempotent spawn, load re-placement, recruited-companion guard, already-placed skip, dead hero skip, missing hero skip, disabled skip on load

## How to Add a New Named Companion

1. Add NPCCharacter to `named_companions/named_companions.xml`:
   - `is_hero="true"`, `occupation="Wanderer"`, `race="X"`, `culture="Culture.X"`
   - Include `<BodyProperties>`, `<skills>`, `<Traits>`, `<Equipments>` with inline `<EquipmentRoster>`
   - Verify all item IDs exist in LOTRLOME_Armory

2. Add entry to `named_companions/named_companion_config.json`:
   - `character_id` must match the XML `id`
   - `spawn_settlement` must exist in `settlements.xml`
   - `race` must match a race name in `monsters.xml`

3. Add backstory strings to `named_companions/named_companion_strings.xml`:
   - 7 keys per companion: `prebackstory`, `backstory_a/b/c/d`, `response_1/2`
   - Key format: `{type}.{character_id}`

4. Add Hero entry to `characters/heroes.xml`:
   - `<Hero id="{character_id}" faction="Faction.neutral" />`
   - `faction="Faction.neutral"` is **required** — without it, Hero.Deserialize throws NullReferenceException

5. **No C# changes needed.**

## Bugs Found and Fixed

| Bug | Source | Fix |
|-----|--------|-----|
| Load-path teleports recruited companions | Codex Review #23 | Added `IsRecruitedOrInParty` guard to `EnsureCompanionsPlaced()` |
| Hero.Deserialize NullReferenceException | In-game crash | Added `faction="Faction.neutral"` to all Hero entries |
| 6 missing Armory item IDs | In-game (naked companions) | Replaced deleted LOTRAOM items with LOTRLOME_Armory equivalents |
| Unused `_logger` field in behavior | Codex Review #23 | Removed dead field, simplified constructor |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
