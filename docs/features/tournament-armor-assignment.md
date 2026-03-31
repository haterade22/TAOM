# Tournament Armor Assignment (TaomTournamentModel)

## Overview

`TaomTournamentModel` overrides `DefaultTournamentModel.GetParticipantArmor` so that each tournament participant is dressed in their **own culture's** practice dummy armor rather than the hosting settlement's culture. A human Gondor lord visiting an Erebor tournament now receives human-scale armor instead of dwarf chainmail.

## Why This Exists

- **Vanilla behavior:** `DefaultTournamentModel.GetParticipantArmor` ignores the `participant` parameter entirely. All 16 participants — heroes, lords, and filler troops — receive armor from `gear_practice_dummy_{settlement.MapFaction.Culture.StringId}`.
- **TAOM requirement:** TAOM has 13+ cultures with race-specific character skeletons (dwarves, elves, orcs). Armor is modeled to fit specific skeletons. Applying dwarf chainmail to a human skeleton, or human armor to a dwarf, produces visible clipping and scaling glitches.
- **Without this feature:** Any tournament in a non-human settlement (Erebor, Gundabad, Mordor, Isengard, Dol Guldur) visually breaks for visiting human lords/heroes.

## Architecture

### Design Challenge

The vanilla method completely discards participant identity — it only cares about which settlement is hosting. There is no race check anywhere in the tournament equipment pipeline (`TournamentFightMissionController.AddRandomClothes`, `FightTournamentGame.GetParticipantCharacters`, etc.).

### Solution Approach

`GameModel` override — the correct TAOM extension point. `TaomTournamentModel` inherits `DefaultTournamentModel` and overrides the single method responsible for armor selection. The fix is data-driven: each culture already has a `gear_practice_dummy_*` character with skeleton-appropriate gear. No explicit race-to-armor mapping is needed.

### Component Diagram

```
Tournament participant (any culture/race)
        |
TaomTournamentModel.GetParticipantArmor(participant)
        |
        ├─ gear_practice_dummy_{participant.Culture}  ← try participant's own culture
        |     found → return RandomBattleEquipment
        |
        └─ base.GetParticipantArmor(participant)      ← vanilla fallback
              ├─ gear_practice_dummy_{settlement.Culture}
              └─ gear_practice_dummy_empire
```

## Configuration

No configuration. Purely data-driven via the `gear_practice_dummy_*` NPCCharacter entries in each culture's `npcs_{culture}.xml`.

### gear_practice_dummy_* coverage

All 13 TAOM cultures have entries (fixed in session 2026-03-31):

| Culture | File | Body item |
|---------|------|-----------|
| erebor | `npcs_erebor.xml` | `sk_dwarf_erebor_chest_chain_a` |
| gondor | `npcs_gondor.xml` | `ithilien_jerkin_short` |
| isengard | `npcs_isengard.xml` | `sk_uruk_hai_chainmail_a2` |
| mordor | `npcs_mordor.xml` | `sk_uruk_mordor_chainmail_light_b` |
| rivendell | `npcs_rivendell.xml` | `rivendell_torso_light_light_tier1` |
| dolguldur | `npcs_dolguldur.xml` | `sk_dg_uruk_chest_light_b` |
| mirkwood | `npcs_mirkwood.xml` | `rivendell_torso_light_light_tier1` |
| gundabad | `npcs_gundabad.xml` | `sk_gb_uruk_chest_light_b` |
| harad | `npcs_harad.xml` | `aserai_civil_b` |
| dunland | `npcs_dunland.xml` | `dunland_caerdh_chainmail_light_a` |
| rhun | `npcs_rhun.xml` | `sk_rh_loke_chest_light_a` |
| dale | `npcs_dale.xml` | `nordic_padded_cloth` |
| khand | `npcs_khand.xml` | `dunland_caerdh_chainmail_light_a` |
| lothlorien | `npcs_lothlorien.xml` | `rivendell_torso_light_light_tier1` |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Arena/Models/TaomTournamentModel.cs` | GameModel override — participant culture armor lookup |
| `TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs` | 5 unit tests on `ResolveDummyId` |
| `Main/SubModule.cs` | Registration: `campaignStarter.AddModel(new TaomTournamentModel())` |
| `Main/_Module/ModuleData/characters/npcs_{culture}.xml` | `gear_practice_dummy_*` entries per culture |

## Dependencies

None. `TaomTournamentModel` has no constructor dependencies — instantiated directly with `new`.

## Tests

- `TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs` — 5 tests covering `ResolveDummyId`:
  - Participant culture present → returns participant culture dummy ID
  - Null participant culture → returns settlement culture dummy ID
  - Empty participant culture → returns settlement culture dummy ID
  - Both null → returns empire fallback
  - Null participant + empty settlement → returns empire fallback
- `GetParticipantArmor` is not unit-testable (requires live `ObjectManager` / game state)

## How to Add a New Culture

1. Add `gear_practice_dummy_{culture_string_id}` to `npcs_{culture}.xml` with a non-civilian `EquipmentRoster` using skeleton-appropriate items from that culture's armory
2. No code changes needed — `TaomTournamentModel` picks it up automatically via the culture StringId lookup

## GitHub Issue

- **Issue:** [#52 — feat: TaomTournamentModel — per-participant culture armor assignment](https://github.com/haterade22/TAOM/issues/52)
- **Status:** Open (close after in-game verification)
