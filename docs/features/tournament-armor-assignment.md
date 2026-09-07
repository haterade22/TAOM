# Tournament Model (TaomTournamentModel)

## Overview

`TaomTournamentModel` overrides five `DefaultTournamentModel` methods. **This doc covers the two equipment/prize concerns** — participant armor assignment and regular/elite prize selection. Participants wear their own culture's skeleton-appropriate gear, and prizes are drawn from LOTRLOME_Armory items matching the hosting settlement's culture and tier. For the full override list, the Phase 9b #137 service extraction, and the separate **dwarf-dismount** fix (Patch46), see the authoritative code-side doc [arena.md](arena.md).

> **Architecture note (Phase 9b #137):** the decision logic described below now lives in [`TournamentService`](../../Main/Features/Arena/TournamentService.cs); `TaomTournamentModel` is a thin entry point that delegates to it via the injected `ITournamentService`. Earlier revisions of this doc described logic on the model and a no-arg constructor — both are outdated.

## Why This Exists

- **Vanilla behavior:** `DefaultTournamentModel.GetParticipantArmor` ignores the `participant` parameter entirely. All 16 participants — heroes, lords, and filler troops — receive armor from `gear_practice_dummy_{settlement.MapFaction.Culture.StringId}`.
- **TAOM requirement:** TAOM has 13+ cultures with race-specific character skeletons (dwarves, elves, orcs). Armor is modeled to fit specific skeletons. Applying dwarf chainmail to a human skeleton, or human armor to a dwarf, produces visible clipping and scaling glitches.
- **Without this feature:** Any tournament in a non-human settlement (Erebor, Gundabad, Mordor, Isengard, Dol Guldur) visually breaks for visiting human lords/heroes.

## Architecture

### Design Challenge

The vanilla method completely discards participant identity — it only cares about which settlement is hosting. For **armor**, there is no race check anywhere in the equipment pipeline (`TournamentFightMissionController.AddRandomClothes`, `FightTournamentGame.GetParticipantCharacters`, etc.) — TAOM keys armor on the participant's *culture* instead. (For **mounts** — a separate concern — TAOM now *does* do a race check: `Patch46_TournamentDwarfDismount` strips the horse from dwarf participants in `PrepareForMatch`, because the mount comes from the culture weapon template, not from `GetParticipantArmor`. See [arena.md](arena.md).)

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

Every TAOM culture has a `gear_practice_dummy_<culture>` entry. Counting `id="gear_practice_dummy_` across `npcs_*.xml` (2026-09-06) finds **19**, one per culture (coverage filled in session 2026-03-31). Eighteen of those cultures also carry the rest of the arena set (`weapon_practice_stage_1/2/3_*` and `gear_dummy_*`); Lothlórien has only the gear dummy. Representative sample below (`rohan`, `mistymountainorcs`, and `goblin` also have entries but are not tabulated here):

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

### Six of those entries never resolve, because the culture's StringId is the vanilla one

Both lookups build a string from `Culture.StringId`, so an entry only works when its name suffix IS
that id. TAOM reskins six vanilla cultures through `spcultures.xslt` rather than declaring new ones,
and those keep the vanilla id: `empire` is Dunland, `aserai` Harad, `vlandia` Rohan, `khuzait` Rhûn,
`sturgia` Dale, `battania` Khand. Their troops carry `culture="Culture.vlandia"` and the like. So
`ResolveDummyId` asks for `gear_practice_dummy_vlandia`, TAOM's entry is named
`gear_practice_dummy_rohan`, and the id that does resolve is SandBoxCore's Calradian one. The same
applies to `weapon_practice_stage_N_*`, which `ArenaPracticeFightMissionController.AddRandomWeapons`
resolves the same way.

Net effect: in a Dunland, Harad, Rohan, Rhûn, Dale or Khand town the arena fighters wear vanilla
Calradian kit and the TAOM sets authored for those six cultures are dead data. The rows for `harad`,
`dunland`, `rhun`, `dale` and `khand` in the table above are authored but unreachable. Only the 18
cultures that declare their own id in `taom_spcultures.xml` (plus `lothlorien`) actually resolve.
Fixing it means renaming those six sets to the vanilla-id suffix, which changes the armour six
cultures fight in, so it is a deliberate content decision rather than a typo repair. Not done.
<!-- measured: culture ids from taom_spcultures.xml and spcultures.xslt template matches, checked
     against the gear_practice_dummy_/weapon_practice_stage_1_ id set of the merged install, 2026-09-06 -->

## Prize Item Selection

`GetRegularRewardItems` and `GetEliteRewardItems` both scan `Items.All` filtered by settlement culture and `item.Tierf` (TaleWorlds' computed quality float derived from damage/armor stats):

| Method | Tierf range | Approximate item quality |
|--------|-------------|--------------------------|
| `GetRegularRewardItems` | `>= 2f && < 4f` | Leather/chainmail, basic weapons |
| `GetEliteRewardItems` | `>= 4f` | Plate, named lore weapons |

Falls back to `base` (vanilla) when no culture-specific items are found (e.g., lothlorien, dale, khand have no dedicated armory entries). Called once per tournament win — not a performance concern.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Arena/Models/TaomTournamentModel.cs` | 5 overrides (thin; delegates to `ITournamentService`): participant armor, regular/elite prizes, start/end chance |
| `Main/Features/Arena/TournamentService.cs` | Decision logic: `ResolveDummyId`, `BuildPrizePool`, start/end-chance, `ShouldDismountInTournament` |
| `TAOM.Tests/Features/Arena/TournamentServiceTests.cs` | 21 unit tests (`ResolveDummyId` fallback chain, start/end chance, `ShouldDismountInTournament`) |
| `TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs` | 7 unit tests (tier-constant invariants on the model) |
| `Main/SubModule.cs:385` | Registration: `campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<ITournamentService>()))` |
| `Main/_Module/ModuleData/characters/npcs_{culture}.xml` | `gear_practice_dummy_*` entries per culture |

## Dependencies

`TaomTournamentModel` takes `ITournamentService` via constructor injection (registered `Reuse.Singleton` in [ArenaIoC.cs](../../Main/Features/Arena/ArenaIoC.cs)). `TournamentService` in turn injects [`IRaceManager`](../../Main/Core/Domain/IRaceManager.cs) (for the dwarf-dismount check). It is no longer instantiated with a no-arg `new`.

## Tests

- `TAOM.Tests/Features/Arena/TournamentServiceTests.cs` — **21 tests**. `ResolveDummyId` fallback chain (participant culture → settlement culture → empire), start/end-chance functions, and `ShouldDismountInTournament` (dwarf/case/non-dwarf/invalid). These moved here from the model test when the logic was extracted to the service (#137).
- `TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs` — **7 tests** (tier-constant invariants on the model).
- `GetParticipantArmor` and the `Patch46` postfix are not unit-testable (require a live `ObjectManager` / game state) — covered by the service unit tests + in-game verification.

## How to Add a New Culture

1. Add `gear_practice_dummy_{culture_string_id}` to `npcs_{culture}.xml` with a non-civilian `EquipmentRoster` using skeleton-appropriate items from that culture's armory
2. **Give it a `<face>` block**, normally `<face><face_key_template value="BodyProperty.fighter_{culture}" /></face>`, matching the sibling characters in the same file. An `NPCCharacter` with no `<face>` gets `BodyProperties` with age 0 and renders on the toddler skin if anything ever spawns it. Ten cultures shipped without one and their arena fighters appeared as children (2026-09-06; see [body-properties.md](../modding/body-properties.md) "Gotchas"). `CharacterFaceCoverageTests` now fails the build if a faceless character lands again.
3. No code changes needed: `TaomTournamentModel` picks it up automatically via the culture StringId lookup

## Changelog

- 2026-09-06: Gave all 46 faceless arena practice characters a `<face>` block across ten cultures (dale, dunland, gondor, harad, isengard, khand, lothlorien, mordor, rhun, rohan). Without one the engine builds their `MBBodyProperty` from `default(BodyProperties)`, whose age is 0, and renders them on the toddler skin: players reported "Practice Fighter" and "Gear Dummy" fighting in the arena as children. Added `CharacterFaceCoverageTests` as the gate.
- 2026-06-09 — Fixed the Patch46 dwarf-dismount postfix crashing every campaign load (`____match` underscore-count fix for the private `_match` field); corrected the stale Phase-9b-#137 architecture notes in this doc + `arena.md`.
- 2026-06-09 — Added `Patch46_TournamentDwarfDismount` postfix on `PrepareForMatch` to clear the Horse/HorseHarness slots for dwarf participants (mount comes from the culture weapon template, not `GetParticipantArmor`) so dwarves no longer spawn inside the horse mesh (#277).
- 2026-05-14 — Extracted the decision logic (`ResolveDummyId`, `BuildPrizePool`, start/end-chance) from `TaomTournamentModel` into the new `ITournamentService`, leaving the model a thin boundary delegate (#137).
- 2026-03-31 — Added per-participant culture armor: `TaomTournamentModel.GetParticipantArmor` tries the participant's own culture's `gear_practice_dummy_*` first, then falls back to vanilla, so visiting human lords no longer wear dwarf gear on human skeletons (#52).
- 2026-03-31 — Added culture-specific tournament prize items: `GetRegularRewardItems`/`GetEliteRewardItems` scan `Items.All` filtered by settlement culture and `item.Tierf` (regular 2–4, elite 4+), with graceful `base` fallback (#52).
- 2026-03-31 — Tuned tournament frequency: removed the vanilla week-gate start chance and extended the end-chance grace period, with all tuning values as testable `internal const` (#52).

## GitHub Issue

- **Issue:** [#52 — feat: TaomTournamentModel — per-participant culture armor assignment](https://github.com/haterade22/TAOM/issues/52)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/arena.md](./arena.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/npcs-notables-and-townsfolk.md](../modding/npcs-notables-and-townsfolk.md)

<!-- backlinks-end -->
