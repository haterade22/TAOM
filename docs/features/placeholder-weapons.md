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
  against tier targets of 32 to 76 (checked 14 Nov 2026 with the dry run of the generator).

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
   `ladder target DPS / donor DPS`, rounded down to two decimals. DPS is linear in `damage_factor`
   (`restat_melee_blades.py`).
2. Clones the donor CraftedItem with the new id, a `{=<id>_name}` name, the cloned blade, and
   `is_merchandise="false"`, so it does not sell in shops or drop as loot.
3. Appends the cloned blade to every WeaponDescription that lists the donor blade, and to the donor's
   CraftingTemplate. This happens in stylesheet-only rows: `ph_weapon_descriptions.xslt` and
   `ph_crafting_templates.xslt`, with no sibling `.xml`.
4. Swaps the donor for the placeholder in every battle slot of each troop whose culture is listed and whose
   melee-ladder tier is inside `tiers`.

The tool prices every placeholder with the engine port. It refuses one above its tier limit (the class limit
scaled to the placeholder's tier and culture, `melee_ladder.tier_limit`), one that lands outside its band
without a limit as the cause, and one that no troop carries. The limit is checked on every `(tier, craft key)`
row of `carrier_rows`: each real carrier's row plus the spec's own tier and first culture, and the error names
every troop of a breached row. The band and the spec's row rate the first culture through `ladder.craft_key`.

Run it after `restat_melee_blades.py --apply`. The factor is relative to the donor, so a restat that moves a
donor moves k, while the placeholder's own DPS stays on its target (or on its blend, or its tier limit). Run
before the restat, `--verify` reports drift in `ph_crafting_pieces.xml` afterwards by rounding.

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

16 weapons. The target is the donor's class curve (`melee_ladder.target`: class top x `class_tier_fraction` at
the tier x the culture's craft column, band 15%; a polearm donor that thrusts harder than it swings is banded
from the thrust top by `dominant_flags`), and the multiplier stops at the tier limit (`tier_limit`, the
`class_limits` of `tools/melee_ladders.json` scaled to the placeholder's tier). Values computed 14 Nov 2026 with
the donors as `restat_melee_blades.py` leaves them (in memory, nothing written to the Armory; the generator's
dry run prints the same table once the restat has been applied):

| Placeholder | Donor DPS after the restat | DPS | Swing | Thrust | k |
|---|---|---|---|---|---|
| Isengard 1H sword T7 | 46 | 63 | 72 | 19 | 1.35 |
| Erebor spear a, T4 | 21 | 27 | 35 | 31 | 1.24 |
| Erebor spear b, T4 | 23 | 27 | 34 | 32 | 1.19 |
| Erebor spear a, T8 | 21 | 30 | 40 | 35 | 1.41 |
| Erebor spear b, T8 | 23 | 32 | 38 | 37 | 1.34 |
| Gondor heavy spear T5 (base) | 26 | 32 | 0 | 39 | 1.22 |
| Gondor heavy spear T5 (Gondor, Pinnath Gelin, Swan Knight) | 25 | 32 | 0 | 39 | 1.28 |
| Gondor heavy spear T8 (base) | 26 | 38 | 0 | 46 | 1.45 |
| Gondor heavy spear T8 (Gondor, Pinnath Gelin, Swan Knight) | 25 | 38 | 0 | 47 | 1.51 |
| Gondor Fountain Guard spear T8 | 28 | 39 | 0 | 47 | 1.42 |
| Mirkwood sword T8 | 48 | 66 | 78 | 43 | 1.37 |
| Mirkwood sword T10 | 48 | 70 | 83 | 46 | 1.47 |

All 16 beat their donor (k between 1.19 and 1.51). The Isengard and Mirkwood swords and the Gondor spears
stop at the tier limit of their class, below their DPS target (66, 72 and 76 for the swords; 34 at T5 and 39
at T8 for the spears, which reach 32 and 38), and the four Erebor spears are blended (0.5) on purpose. The
Fountain Guard spear is the one Gondor spear that reaches its target. A placeholder capped over every
carrier's row stays under the limit of the lowest tier and weakest craft that will carry it.

136 troop slots. The tool's dry run prints the full list.

**Status, 2026-11-14 (#1271):** the Armory blades have NOT been restated yet. The committed
`ph_crafting_pieces.xml` was generated against today's donors before k was capped over every real
carrier, so the generator runs again with `--apply` after `restat_melee_blades.py --apply` at the Armory
apply, and the table above is what it should then print. Until then the dry run and `--verify` both exit 1
at `taom_ph_gondor_spear_b_t5` (the tightest tier limit over its carriers, who start at tier 4, leaves no
room above the unrestated donor), so
the `placeholder-weapons` row of `python tools/data_gates.py` fails, and the validator reports 11
`MELEE_BLOW_CEILING` findings on `taom_ph_` weapons (2026-11-14).

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
  additions are wiped and the placeholders get no weapon usage. TAOM's SubModule.xml declares
  `<DependedModuleMetadata id="LOTRLOME_Armory" order="LoadBeforeThis" optional="true" />`: BUTR/BLSE
  launchers sort by it, the vanilla launcher ignores it.
- The installed Armory (donors are read from it) and lxml (the shared `melee_catalogue`).

## Tests

- `--verify` exits 1 when the generated files, SubModule.xml or the troop swaps drift from the spec.
- The XSLT chain was checked on 14 Nov 2026 with .NET `XslCompiledTransform`, Native, then the Armory, then
  these sheets:
  - weapon_descriptions: 5230 to 5285 AvailablePiece entries (55 placeholders, every Armory entry kept);
  - crafting_templates: 2968 to 2984 (16 placeholders).
- `PlaceholderWeaponStylesheetChainLiveTests` (LiveInstall) runs the same chain on the installed Native and
  Armory files: every `taom_ph_` blade must be listed and every Armory entry kept. Inconclusive without
  the Armory.
- Since 2026-11-14 (#1271) the validator's `MELEE_LADDER_INVERSION` and `MELEE_BLOW_CEILING` price the
  placeholders from this tree's `placeholder_weapons/` files (`taom_schema.price_placeholders`) and judge
  each against the troops that carry it. A placeholder that will not price is a `MELEE_LADDER_INVERSION`
  warning naming it.

## How to verify in game

Not verified in game. After the Armory apply (the restat, then this generator with `--apply`) and a full
restart:
1. Start a Custom Battle with Isengard against Gondor. Field Orthanc Guards and Gondor T5 spearmen.
2. Check that each troop holds its weapon, can attack with it, and that the weapon tooltip shows the damage
   from the dry run.
3. Check that no placeholder one handed sword shows a swing of 100 or more (the Mirkwood T10 sword is the
   highest, about 84).

## How to replace a placeholder

1. Give its troops the real Armory weapon (`git grep <placeholder id> -- Main/_Module/ModuleData/troops`).
2. Delete its entry from `tools/placeholder_weapons.json`.
3. Run `python tools/generate_placeholder_weapons.py --apply`, then `--verify`.

The validator then reports any troop still pointing at the removed id.

## Changelog

- v2.0.38 (14 Nov 2026): first 16 placeholders (owner request).
- v2.0.40 (14 Nov 2026): class limits on one blow and sustained damage; 14 of the 16 lowered.
- v2.0.40 (14 Nov 2026, #1271): k is capped by the tier limit and targeted from `class_tier_fraction`;
  thrust-dominant polearm donors are banded from the thrust top; run after the restat.
- v2.0.40 (14 Nov 2026, #1271): factors are rounded down; the limit check reads one set of
  `(tier, craft key)` rows and names every carrier of a breached row.

## GitHub Issue

[#1271](https://github.com/haterade22/TAOM/issues/1271) for the tier limit; none for the first 16.
