# Creature Saddles

## Overview

The warg, fell warg, dire wolf and spider each get their own harness family, and their own saddles the same one. The
inventory then refuses a horse saddle on them and a creature saddle on a horse. A save that already holds such a pair
loses it at load: the harness goes into the party inventory. Horse-rig reskins (elk, moose, war ram, WildMesh), the
elephants and the chariot keep their families.

## Why This Exists

- **Vanilla behavior:** a harness fits a mount when `ArmorComponent.FamilyType == Monster.FamilyType` (v1.5.7
  `SPInventoryVM`: the equip check, and the unequip after a mount swap). Nothing else checks the pair. Vanilla families
  are 0 human, 1 horse and mule, 2 camel; the Armory adds 4 chariot and 10 elephant.
- **The Armory:** the warg, fell warg, dire wolf and spider Monsters are family 1, like the horse, so all 111 family 1
  harnesses fit them, 45 of them with reins. Those rigs are not horse skeletons: a horse saddle does not fit their
  bodies, and its reins have no rein skeleton to hang on.
- **Player case (2026-11-15):** a Rivendell elf hero on a `[Goblin] Brown Dire Wolf` with `light_harness` crashed at
  every mounted field-battle spawn, and stopped once the wolf and harness came off. `light_harness` has reins. See
  [mount-reins-guard.md](mount-reins-guard.md) (Patch153) for the crash path.
- **The "[Goblin]" in the name** is a label plus `culture="Culture.goblin"`, which only decides which markets stock it.
  No race or culture is barred from riding it.

## The families

| Monster | Rig | Items | Family (data, TAOM) | Saddles (TAOM rosters) |
|---|---|---|---|---|
| `warg` | `skeleton_warg` | `warg_albino`, `warg_brown`, `warg_dark` | 1, **11** | `warg_saddle` (59 rosters) |
| `fell_warg` | `skeleton_warg` (`as_warg`) | `fell_warg` | 1, **11** | `warg_saddle` (35 rosters) |
| `dire_wolf` | `adod_wolf_armature_unused` | `dire_wolf_grey`, `_white`, `_black`, `_brown` | 1, **12** | `dire_wolf_saddle` (24 rosters) |
| `spider` | spider rig | 5 `spider_mount_*` | 1, **13** | none (30 rosters ride bare) |
| `taom_war_ram`, `taom_elk`, `taom_animalia_elk`/`_moose`, 5 `taom_wildmesh_*` | horse skeleton (`base_monster="horse"`) | | 1, unchanged | ram bardings, `taom_elk_saddle_a`, horse saddles |
| `taom_war_elephant`, `taom_mumakil` | elephant | | 10, unchanged | `sk_elephant_armor_*` |
| `chariot` | chariot | `taom_chariot_a` | 4, unchanged | `taom_chariot_armour_*` |

`warg_saddle` takes 11 and `dire_wolf_saddle` 12. The new numbers avoid every family the data uses.

## Architecture

### Where the change lives

In TAOM, not in the Armory. The Armory ships as its own package and the copies players have predate any change made
now; a TAOM-side change reaches every player with the TAOM update. The Armory is also not a repository TAOM can open
a pull request against. Two postfixes write the family right after the engine reads each Monster and item from XML
(`Monster.Deserialize`, `ItemObject.Deserialize`), through the private `FamilyType` setters, cached once
(`CreatureSaddleFamilyWriter`). Category `Patch154_CreatureSaddleFamilies`, applied at ProcessLoad, so it is in place
before the first game reads its XML; it runs for each game (campaign and Custom Battle) and each module copy of an id.

If the Armory later sets these families itself, the postfix finds the value already right and writes nothing.
`CreatureSaddleRosterTests` accepts either value in the live Armory.

### Old saves

`CreatureSaddleSaveRepairBehavior` runs at session launch, host only in co-op. For each living hero of the player clan
(members and companions), battle and civilian equipment: when the mount's and the harness's families differ, the
harness moves into the main party's item roster and the slot empties. One log line per harness and a total. AI heroes
are not touched: their kits come from templates, and Patch153 keeps the cloth reins off every mount without a rein
skeleton anyway.

### Not covered

- `EquipmentPresetService` applies a saved preset's mount and harness by id with no family check. A preset saved
  before this change can put a horse saddle back on a warg until the next load repairs it; Patch153 keeps that from
  crashing. A family check there is a possible follow-up.
- How a creature looks with no saddle when a soldier carried a horse saddle before: it rides bare.

## Configuration

None.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CreatureSaddles/CreatureSaddleFamilies.cs` | The table and the unequip rule |
| `Main/Features/CreatureSaddles/CreatureSaddleFamilyWriter.cs` | The two private setters |
| `Main/Features/CreatureSaddles/Hooks/Patch154_CreatureSaddleFamilies.cs` | The two postfixes |
| `Main/Features/CreatureSaddles/CreatureSaddleSaveRepairBehavior.cs` | The old-save repair |
| `Main/Features/CreatureSaddles/CreatureSaddlesModule.cs` | Wiring |

## Testing

- `TAOM.Tests/Features/CreatureSaddles/CreatureSaddleFamiliesTests.cs`: the table by literal, horse rigs and saddles
  keep their family, new families avoid 0/1/2/4/10, the unequip rule, the writer on a real `Monster` and
  `ArmorComponent`, both postfixes attached to the installed engine, module wiring.
- `CreatureSaddleRosterTests.cs` (`LiveInstall`): every TAOM roster pairs a creature mount with a harness of the same
  family, and the live Armory still has family 1 (or TAOM's) for the four Monsters.
- Reflection sites `Monster.set_FamilyType` and `ArmorComponent.set_FamilyType` in `ReflectionSiteBindingTests`.

## How to verify in game

Nothing below has run yet.

1. Inventory: a horse saddle cannot go on a warg, dire wolf or spider; `warg_saddle` fits a warg and a fell warg,
   `dire_wolf_saddle` a dire wolf; neither fits a horse. A ram barding and the elk saddle still fit the ram and elk.
2. Load a save whose hero rides a dire wolf with `light_harness`: the log has a `[CreatureSaddles] ... taken off`
   line, the harness is in the party inventory, and a field battle starts.
3. Goblin and Gundabad wolf riders, Mordor warg riders and spider riders spawn with their saddles as before.
4. The log has one `[CreatureSaddles] Monster 'dire_wolf' takes harness family 12` line per game load.

## Changelog

- 2026-11-15: first version, player case 2026-11-15 (elf hero on a dire wolf with `light_harness`).
