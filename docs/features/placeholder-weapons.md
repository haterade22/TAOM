# Placeholder Melee Weapons

## Overview

Sixteen generated melee weapons arm elite troops whose faction owns no weapon of the right class in their
tier's band on the melee ladder (`tools/melee_ladders.json`). Each one is a clone of an Armory weapon with the
same look and a cloned blade piece whose `damage_factor` puts its sustained damage on the ladder target. Every
id starts with `taom_ph_`, so `git grep taom_ph_` lists each piece, item and troop that carries one, and a
real weapon can replace it later.

## Why This Exists

- **Vanilla behaviour:** a troop fights with the weapons in its roster. A crafted weapon's damage comes from
  its blade piece (`damage_factor`), never from the item.
- **TAOM requirement:** the melee ladder (#631) sets a sustained-damage band per tier and kingdom. The roster
  pass (`tools/fix_melee_ladder.py`) can only swap a troop onto a weapon its own culture already owns, and many
  culture/class pairs own nothing in band. Pass 2 (`tools/restat_melee_blades.py`) restats the shared blade,
  which also raises every low-tier troop that carries it.
- **Without this:** Orthanc T6-T8, Mirkwood T7-T10 and the Erebor and Gondor spear lines fight with
  low-tier weapons. Measured with the engine port (`tools/melee_damage.py`), the donors give 21 to 45 DPS
  against tier targets of 63 to 119.

## Architecture

### Design Challenge

- **No stats on the item.** A new item with the donor's pieces is the donor. The damage has to sit on a new
  blade piece.
- **Pieces must be listed.** The engine builds a crafted weapon only from pieces its WeaponDescription
  (`AvailablePieces`) and CraftingTemplate (`UsablePieces`) list. The Armory's stylesheets REPLACE those
  lists rather than appending to them.
- **The Armory is unversioned.** Anything written there is lost on a reinstall, so everything lives in the
  repo.

### Solution Approach

`tools/generate_placeholder_weapons.py` reads `tools/placeholder_weapons.json`. For each entry it:

1. Clones the donor's blade piece, with id `<item id>_blade` and every `damage_factor` multiplied by
   `ladder target DPS / donor DPS`. DPS is linear in `damage_factor` (`restat_melee_blades.py`).
2. Clones the donor CraftedItem with the new id, a `{=<id>_name}` name, the cloned blade, and
   `is_merchandise="false"`, so it does not sell in shops or drop as loot.
3. Appends the cloned blade to every WeaponDescription that lists the donor blade, and to the donor's
   CraftingTemplate. This happens in stylesheet-only rows: `ph_weapon_descriptions.xslt` and
   `ph_crafting_templates.xslt`, with no sibling `.xml`.
4. Swaps the donor for the placeholder in every battle slot of each troop whose culture is listed and whose
   melee-ladder tier is inside `tiers`.

The tool prices every placeholder with the engine port and refuses one that lands outside its band, or one
that no troop carries.

## Configuration

### Config File: `tools/placeholder_weapons.json`

| Field | Meaning |
|---|---|
| `tag` | Id prefix every placeholder must carry (`taom_ph_`) |
| `id` | New item id; the blade is `<id>_blade` |
| `donor` | Armory CraftedItem to clone (look and pieces) |
| `tier` | Ladder tier whose target DPS the blade is set to |
| `tiers` | `[lo, hi]` troop tiers (the melee ladder's tier, noble bump included) that get it |
| `cultures` | Troop culture ids |
| `name` | English display name |
| `blend` | Optional, default 1. The blade lands at donor DPS + blend x (target - donor). Below 1 it sits under the band on purpose |

### Current Values

16 weapons:
- Isengard 1H sword T7: 87 DPS.
- Erebor spears, a and b, at half the boost (`blend` 0.5, owner choice: at full target the dwarf spear troops
  reached 1.5 to 2 times their tier peers): T4 42-44 DPS, T8 61-63 DPS, below the band on purpose.
- Gondor heavy spears in four heraldic variants at T5 (71-72) and T8 (99-100).
- Gondor Fountain Guard spear T8: 100.
- Mirkwood sword T8 (100) and T10 (119).

136 troop slots. The tool's dry run prints the full list.

## Key Files

| File | Purpose |
|---|---|
| `tools/placeholder_weapons.json` | Spec |
| `tools/generate_placeholder_weapons.py` | Generator: plan, `--apply`, `--verify` |
| `Main/_Module/ModuleData/placeholder_weapons/ph_crafting_pieces.xml` | Generated blade pieces |
| `Main/_Module/ModuleData/placeholder_weapons/ph_items.xml` | Generated CraftedItems |
| `Main/_Module/ModuleData/placeholder_weapons/ph_weapon_descriptions.xslt`, `ph_crafting_templates.xslt` | Generated piece registration |
| `Main/_Module/SubModule.xml` | Four XmlNodes (CraftingPieces, Items, WeaponDescriptions, CraftingTemplates) |

## Dependencies

- **TAOM must load after LOTRLOME_Armory** (the documented launcher order). The Armory's
  `weapon_descriptions.xslt` and `crafting_templates.xslt` replace the piece lists. If TAOM loads first, its
  additions are wiped and the placeholders get no weapon usage. TAOM's SubModule.xml declares no order
  against the Armory today.
- The installed Armory (donors are read from it) and lxml (the shared `melee_catalogue`).

## Tests

- `--verify` exits 1 when the generated files, SubModule.xml or the troop swaps drift from the spec.
- The XSLT chain was checked on 14 Nov 2026 with .NET `XslCompiledTransform`, Native, then the Armory, then
  these sheets:
  - weapon_descriptions: 5230 to 5285 AvailablePiece entries (55 placeholders, every Armory entry kept);
  - crafting_templates: 2968 to 2984 (16 placeholders).
- No C# test covers this yet.
- The validator's `MELEE_LADDER_INVERSION` prices Armory items only, so it does not see the placeholders.

## How to verify in game

Not verified in game. After a full restart:
1. Start a Custom Battle with Isengard against Gondor. Field Orthanc Guards and Gondor T5 spearmen.
2. Check that each troop holds its weapon, can attack with it, and that the weapon tooltip shows the damage
   from the dry run.

## How to replace a placeholder

1. Give its troops the real Armory weapon (`git grep <placeholder id> -- Main/_Module/ModuleData/troops`).
2. Delete its entry from `tools/placeholder_weapons.json`.
3. Run `python tools/generate_placeholder_weapons.py --apply`, then `--verify`.

The validator then reports any troop still pointing at the removed id.

## Changelog

- v2.0.38 (14 Nov 2026): first 16 placeholders (owner request).

## GitHub Issue

None opened.
