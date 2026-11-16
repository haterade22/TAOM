# Placeholder Melee Weapons

## Overview

77 generated melee weapons arm troops whose faction owns no weapon of the right class in their tier's band on
the melee ladder (`tools/melee_ladders.json`). Each one is a clone of an Armory weapon with the same look and a
cloned blade piece whose `damage_factor` puts its sustained damage on the ladder target, or as near it as the
tier limit allows. 19 were picked by hand for elite tiers. The other 58 are tier clones (owner request,
16 Nov 2026): a copy of the weapon a troop already carries, restated to the troop's tier and named as the next
Roman numerals of the donor's line. Every id starts with `taom_ph_`, so `git grep taom_ph_` lists each piece,
item and troop that carries one, and a real weapon can replace it later.

## Why This Exists

- **Vanilla behaviour:** a troop fights with the weapons in its roster. A crafted weapon's damage comes from
  its blade piece (`damage_factor`), never from the item.
- **TAOM requirement:** the melee ladder (#631) sets a sustained-damage band per tier and kingdom. The roster
  pass (`tools/fix_melee_ladder.py`) can only swap a troop onto a weapon its own culture already owns, and many
  culture/class pairs own nothing in band. Pass 2 (`tools/restat_melee_blades.py`) restats the shared blade,
  which also raises every low-tier troop that carries it.
- **Without this:** Orthanc T6-T8, Mirkwood T7-T10, the Erebor, Gondor and Gundabad spear lines and the Lothlorien
  riders fight with low-tier weapons. Measured with the engine port (`tools/melee_damage.py`), the donors give
  21 to 68 DPS against tier targets of 32 to 115 (checked 15 Nov 2026 with the dry run of the generator).

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
   (`restat_melee_blades.py`). The Swing and Thrust factors are capped separately, each by its own attack's
   tier limit (as `restat_melee_blades.class_ratios` does), so a thrust already at its limit does not hold the
   swing back (#1296). An attack the donor does not have takes the other attack's factor.
2. Clones the donor CraftedItem with the new id, a `{=<id>_name}` name, the cloned blade, and
   `is_merchandise="false"`, so it does not sell in shops or drop as loot.
3. Appends the cloned blade to every WeaponDescription that lists the donor blade, and to the donor's
   CraftingTemplate. This happens in stylesheet-only rows: `ph_weapon_descriptions.xslt` and
   `ph_crafting_templates.xslt`, with no sibling `.xml`.
4. Swaps the donor for the placeholder in every battle slot of each troop whose culture is listed and whose
   melee-ladder tier is inside `tiers`. The rule is one function, `placeholder_for`, and a troop the melee ladder
   exempts (`exempt_troops`) keeps its donor. A troop file that a generator writes whole
   (`fix_melee_ladder.GENERATED_TROOP_FILES`) is not written here, or that generator's next `--apply` would undo
   the swap: `realm_troops.py` (the eight realm generators), `generate_lothlorien_troops.py` and
   `generate_snow_orc_troops.py` call `placeholder_for_troop` as they render each weapon. The tool lists the slots
   still owed by a generator, and `--verify` fails until that generator runs with `--apply`.

The tool prices every placeholder with the engine port. It refuses two entries that serve the same donor,
culture and tier, and a numbered line (Armory names plus placeholders) that repeats a numeral or falls in DPS by
more than half a point at a step a placeholder takes part in (`line_faults`; a fall between two Armory weapons
is the Armory's own). It refuses one above its tier limit (the class limit
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

### Tier clones

A tier clone fills a slot that `fix_melee_ladder.plan` leaves under its band ("owns no", "fields no other" or
"tier limit"). The clone copies the weapon the troop carries, so the weapon category, usage, couch and shield
behaviour stay the same: only the blade differs. One clone starts at the lowest tier of a (donor, culture)
line and also serves each higher tier the melee ladder accepts it at, tier tolerance included. Lines of one
donor that start at the same tier share one clone across cultures, and a troop's shield and no-shield lines
share one clone; the generator caps it at the tier limit of every carrier row.

Only a clone that reaches the band (or the tolerance floor) is made. Where the donor is a slow, heavy weapon
that the per-blow limit already holds under its band, a clone cannot help, and the slot stays a
`MELEE_LADDER_INVERSION` warning.

Names: the donor's line name (`analyze_melee_ladder.line_key`, `[Rohan] Sword` from `[Rohan] Sword III`) and
the next numerals above the highest one that line has in the Armory or among the placeholders, in order of
rising DPS. An unnumbered donor counts as I. The names were set when the clones were planned (16 Nov 2026,
not by a repo tool); `line_faults` holds them: `--verify` refuses a repeated numeral or a DPS fall at a
placeholder's step. A line reads I to XXXIX, and `roman` raises outside that range.

The spec rules that need no install run when the spec loads (`check_spec`): the tag, a unique id, `tier` inside
`tiers`, one entry per donor, culture and tier (so `placeholder_for` has one match), and one craft row for all
cultures of an entry (the band is rated by the first culture).

Players: career kits and enlistment kits name the donor, never a clone (`generate_career_kits.index_troop_gear`,
`generate_enlistment_rosters.placeholder_donors`). One route still puts a clone in a player's hands: a
commissioned soldier hero takes its template's battle kit (`HeroCommissionAdapter`), as with the first 19
placeholders. A held clone can be sold, because `NotMerchandise` is no sell bar.

`promote_borrowed_cultures.py` writes `troops_lindon.xml` whole from Rivendell's tree and does not apply the
swap rule, so a promoter `--apply` for Lindon would put back the donor in the 116 slots of
`taom_ph_wm_rivendell_sword_a01_black_t10`. The promoter refuses Lindon today (13 ids defined twice, as on
trunk), and `--verify` would report the revert.

### Current Values

77 weapons: the 19 below and 58 tier clones. The target is the donor's class curve (`melee_ladder.target`: class top x `class_tier_fraction` at
the tier x the culture's craft column, band 15%; a polearm donor that thrusts harder than it swings is banded
from the thrust top by `dominant_flags`), and the multiplier stops at the tier limit (`tier_limit`, the
`class_limits` of `tools/melee_ladders.json` scaled to the placeholder's tier). Since 2026-11-16 (#631) a
placeholder whose every carrier has a shield is banded on the modes a shield allows (for a spear, the one handed
thrust and its `OneHandedPolearm` class), and each carrier row is held to the tier limit of the mode it wields,
as the validator judges them (melee-damage-model.md, "A troop with a shield"). Values from the generator's dry
run on 16 Nov 2026, after the blade restat of that day was written to the Armory; DPS marked * is in the shield modes, swing and thrust are the whole weapon's:

| Placeholder | Donor DPS | DPS | Swing | Thrust | k |
|---|---|---|---|---|---|
| Isengard 1H sword T7 | 46 | 63 | 72 | 20 | 1.35 |
| Erebor spear a, T4 | 21 | 27 | 35 | 31 | 1.24 |
| Erebor spear b, T4 | 18* | 22* | 35 | 33 | 1.23 |
| Erebor spear a, T8 | 21 | 30 | 40 | 35 | 1.41 |
| Erebor spear b, T8 | 23 | 32 | 38 | 37 | 1.34 |
| Gondor heavy spear T5 (base) | 26 | 31 | 0 | 38 | 1.22 |
| Gondor heavy spear T5 (Pinnath Gelin) | 25 | 32 | 0 | 39 | 1.25 |
| Gondor heavy spear T5 (Gondor, Swan Knight) | 18* | 25* | 0 | 40 | 1.29 |
| Gondor heavy spear T8 (base) | 20* | 29* | 0 | 46 | 1.47 |
| Gondor heavy spear T8 (Gondor, Pinnath Gelin, Swan Knight) | 18* | 28* | 0 | 47 | 1.53 |
| Gondor Fountain Guard spear T8 | 21* | 31* | 0 | 47 | 1.47 |
| Mirkwood sword T8 | 48 | 66 | 78 | 45 | 1.37 |
| Mirkwood sword T10 | 48 | 70 | 83 | 48 | 1.47 |
| Gundabad War Spear (a02), T4 | 52 | 86 | 110 | 28 | 1.68 |
| Gundabad Blackshield Spear (a03), T5 | 67 | 91 | 106 | 32 | 1.37 |
| Gundabad Blackshield Spear (a03), T8 | 24* | 27* | 91 | 35 | 1.11 |

All 19 beat their donor in the view they are banded on (k between 1.11 and 1.68). The swords, the Gondor spears
and the Gundabad T8 spear stop at the tier limit of the mode their carriers wield, below their DPS target; the
four Erebor spears are blended (0.5) on purpose. The Gundabad T8 spear is weaker than the T5 one in its two
handed swing (91 against 106) and stronger in the one handed thrust its shield carriers use (27 against 24): the
T5 spear has carriers without a shield, so it is banded on the whole weapon. A placeholder capped over every
carrier's row stays under the limit of the lowest tier and weakest craft that will carry it.

`taom_ph_lothlorien_spear_t6` was retired on 16 Nov 2026: the Lorien rider and lancer carry a shield, and
`he_spear`'s one handed thrust (30.6 DPS) already sat at their tier 6 limit (30.9), so the generator found no
room above the donor. The two riders carry `he_spear` again (`generate_lothlorien_troops.py`).

`git grep -o "Item\.taom_ph_" -- Main/_Module/ModuleData/troops | wc -l` counts the troop equipment rows that
carry one: 697 on 16 Nov 2026 (301 before the tier clones). The tool's dry run lists every carrier.

The 58 tier clones, from the dry run of 16 Nov 2026: 50 donors, 23 culture ids (gondor 12, vlandia 8, mirkwood
and dolguldur 5, the rest 4 or fewer), 31 clones that serve two tiers or more. Every one beats its donor (k from
1.03 to 2.11), and the tier limit holds every one at or under its tier target. 52 reach the band of their own
tier; the other 6 reach the tier tolerance floor. They take 172 troop slots.

Left out: 44 (culture, donor) lines on 77 slots, where the tier limit stops a clone under the tolerance floor
(17 Numenorean bastard "elite" lines and 27 slow heavy weapons), and the Ered Luin T4 spearman, because the
Erebor spear clone of tiers 2 and 3 cannot span T4 beside the Erebor War Spear placeholder that serves T4 and T5.
The rerun of the clone plan prints that residue.

Before #1296 one factor scaled both attacks, so `he_spear` and `wm_gundabad_spear_a03`, whose thrust DPS sits
at its tier limit, could not rise at all (66.8 and 68.4 DPS against bands of 98 to 133 and 78 to 105) and the
generator refused them. With a factor per attack their swing reaches the band and their thrust stays at or one
hundredth under its limit. The same change raised the swords' secondary thrust a little (Isengard 19 to 20, Mirkwood 43 to 45 and
46 to 48); their swing and DPS did not change.

**Status, 2026-11-14 (#1271):** the Armory blades were restated the same day
(`restat_melee_blades.py --apply`), and the generator then ran with `--apply`, so the table above is what
its dry run prints and `--verify` exits 0. Before the restat the committed `ph_crafting_pieces.xml` had
been generated against the old donors, before k was capped over every real carrier; the dry run and
`--verify` both exited 1 at `taom_ph_gondor_spear_b_t5` (the tightest tier limit over its carriers, who
start at tier 4, left no room above the unrestated donor), the `placeholder-weapons` row of
`python tools/data_gates.py` failed, and the validator reported 11 `MELEE_BLOW_CEILING` findings on
`taom_ph_` weapons.

## Key Files

| File | Purpose |
|---|---|
| `tools/placeholder_weapons.json` | Spec |
| `tools/generate_placeholder_weapons.py` | Generator: plan, `--apply`, `--verify`; `placeholder_for`, `check_spec`, `line_faults` |
| `tools/realm_troops.py`, `tools/generate_lothlorien_troops.py`, `tools/generate_snow_orc_troops.py` | Apply the swap to the troop files they write |
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
- The installed Armory (donors are read from it) and lxml (the shared `melee_catalogue`). The catalogue
  leaves TAOM's own placeholder sheets out (#1372): a caller that prices placeholders adds the registrations
  from its own tree, as `taom_schema.price_placeholders` and `price_new` do.

## Tests

- `--verify` exits 1 when the generated files, SubModule.xml or the troop swaps drift from the spec, including a
  slot in a generated troop file that its generator has not yet swapped.
- `tools/tests/test_generate_placeholder_weapons.py`: the numeral range (`LineNumeralTests`), the spec rules
  (`CheckSpecTests`), the line check (`NumberedLineFaultTests`), the swap rule and the generated files
  (`SharedSwapRuleTests`), each generator rendering a clone (`GeneratorAppliesTheSwapTests`), and, with the
  Armory installed, every numbered line that holds a placeholder (`LiveNumberedLineTests`).
- `generate_career_kits.py` counts a placeholder as its donor, so a career kit keeps the Armory weapon
  (`test_a_placeholder_counts_as_its_donor`); `generate_enlistment_rosters.py` does the same
  (`ParseTroopsPlaceholderTests`), and `StarterKitCoverageTests` reads a clone as its donor.
- The XSLT chain was checked on 14 Nov 2026 with .NET `XslCompiledTransform`, Native, then the Armory, then
  these sheets:
  - weapon_descriptions: 5230 to 5285 AvailablePiece entries (55 placeholders, every Armory entry kept);
  - crafting_templates: 2968 to 2984 (16 placeholders).
  On 15 Nov 2026 (#1296) the generated sheets hold 71 AvailablePiece and 20 UsablePiece rows for 20
  placeholders (`grep -c '<AvailablePiece id='`, `grep -c '<UsablePiece '`); the chain was not re-run by hand.
- `PlaceholderWeaponStylesheetChainLiveTests` (LiveInstall) runs the same chain on the installed Native and
  Armory files: every `taom_ph_` blade must be listed and every Armory entry kept. Inconclusive without
  the Armory.
- Since 2026-11-14 (#1271) the validator's `MELEE_LADDER_INVERSION` and `MELEE_BLOW_CEILING` price the
  placeholders from this tree's `placeholder_weapons/` files (`taom_schema.price_placeholders`) and judge
  each against the troops that carry it. A placeholder that will not price is a `MELEE_LADDER_INVERSION`
  warning naming it.
- Since 2026-11-16 (#1354) `tools/audit_polearm_shield_parity.py` applies these sheets through their
  `SubModule.xml` rows, so a placeholder beside a shield is judged on its real primary usage. On that date all 20
  resolved: 17 to `onehanded_polearm_block_long_rshield_thrust` and 3 to `onehanded_block_shield_swing_thrust`.

## How to verify in game

Not verified in game. After the Armory apply (the restat, then this generator with `--apply`) and a full
restart:
1. Start a Custom Battle with Isengard against Gondor. Field Orthanc Guards and Gondor T5 spearmen.
2. Check that each troop holds its weapon, can attack with it, and that the weapon tooltip shows the damage
   from the dry run.
3. Check that no placeholder one handed sword shows a swing of 100 or more (the Mirkwood T10 sword is the
   highest, about 84).

## How to replace a placeholder

1. Give its troops the real Armory weapon (`git grep <placeholder id> -- Main/_Module/ModuleData/troops tools`).
   Two troop generators name placeholders in their own specs, so a rerun keeps them:
   `generate_lothlorien_troops.py` (the Lorien rider and lancer) and `generate_snow_orc_troops.py` (the Snow
   Orc reaver, Blackshield spearman and Blackshield wolf rider). Change the id there too, then rerun that
   generator with `--apply`. A tier clone in a generated file comes from the spec, so step 2 and a rerun of
   that generator remove it. `tools/audit_polearm_shield_parity.py` names four Gondor pairs by clone id in
   `KNOWN_FAILURES`; rename those with the clone.
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
- v2.0.40 (15 Nov 2026, #1296): a damage factor per attack; four placeholders for the Gundabad spear line
  (a02 at T4, a03 at T5 and T8) and the Lothlorien riders (`he_spear` at T6); the value table matches the dry
  run again.
- v2.0.40 (16 Nov 2026, #1354): the shield gate reads these sheets; before, every placeholder resolved to no
  usage there and was never checked.
- v2.0.40 (16 Nov 2026, #1375): the shield gate and the catalogue read only TAOM's playset, and a row's
  `.xsl` sibling wins over its `.xslt`, as in the engine. The 20 placeholders resolve as before.
- v2.0.40 (16 Nov 2026, #1384): `docs/reference/troop-rosters.html` prices these weapons from this folder;
  before, their 305 slots on the page read "not found".
- v2.0.40 (16 Nov 2026, #631): a placeholder whose every carrier has a shield is banded on its shield
  modes, and each carrier row is held to the tier limit of the mode it wields;
  `taom_ph_lothlorien_spear_t6` retired.
- v2.0.40 (16 Nov 2026, #631): 58 tier clones with Roman numeral names; the realm, Lothlorien and Snow Orc
  generators apply the same swap rule.

## GitHub Issue

[#1271](https://github.com/haterade22/TAOM/issues/1271) for the tier limit; [#1296](https://github.com/haterade22/TAOM/issues/1296)
for the per-attack factor and the Gundabad and Lothlorien spears; none for the first 16.
