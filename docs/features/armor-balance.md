# Armor Balance

## Overview

TAOM's armor lives in the external `LOTRLOME_Armory` module (~2,800 items across 18 culture folders). This doc owns the armor balance system: the baseline + cultural-modifier curve, the **read-only analyzer** that reports drift, the **writing rebalancer** that applies the curve, and the **roster-derived tiering** method that anchors each item's tier to the troop that wears it. It is the armor analog of [troop-skill-balance.md](./troop-skill-balance.md) and [lord-perk-review.md](./lord-perk-review.md).

## Why This Exists

Two problems motivated this system (found during the 2026-06-30 armor audit):

1. **The rebalancer was aimed at a dead tree.** `tools/rebalance_armor.py` targeted `taommod/src/data/armory/` — a stale Node/TS web project (last touched 2026-03-11), **not** the `LOTRLOME_Armory` module the game loads. Confirmed by diff: the dead tree's first Gondor body item is `citidel_guard_armor1` (body 51); the live tree's is `faramir_armor` (body 35, with `material_type`). Any `--apply` against the dead tree was a silent no-op in-game. This is the live-vs-shadow trap (cf. `feedback_taom_map_live_vs_stale_shadow.md`).
2. **There was no way to see armor drift.** Troops and lords each have a read-only analyzer; armor had none — every rebalance to date was a blind write.

The audit also surfaced a dominant, recurring defect across cultures: **monolithic per-slot weight** — a slot frozen at one weight across all tiers, so a recruit's boots/bracers/chest weigh the same as a lord's. The analyzer makes that (and the integrity gaps) machine-detectable.

## Architecture

```
SLOT_BASELINES + CULTURAL_MODS   (rebalance_armor.py — single source of truth)
        │
        ├──► rebalance_armor.py   (WRITES: applies baseline+mod to the live armory; scoped/guarded)
        │
        └──► analyze_armor_balance.py  (READS: imports the curve, reports drift; never writes armor)
                    │
                    └──► tools/reports/armor-balance/{REPORT.html, REPORT.md, armor-balance.json}
```

The analyzer `import rebalance_armor`s the curve verbatim, so the two tools can never diverge (same pattern as `analyze_troop_balance.py` importing `rebalance_troops`).

### The curve

**Since 2026-09-13 (#583) the curve is the kingdom-cap model in the next section.** The baseline
tables and cultural protection mods below remain the model for the civilian tier, for weights
(`baseline_weight x weight_mult`, unchanged) and for any folder without a cap (the troll); they are
kept here because the two-tier invariant test still pins them.

### The kingdom-cap curve (2026-09-13, #583)

Each kingdom's armour power is one number, the chest (`body_armor`) of its elite band, set by the
maintainer; the other slots and the bands below are fixed ratios of it
(`rebalance_armor.KINGDOM_CAPS`, `SLOT_CAP_RATIO`, `BAND_RATIO`, `cap_value`):

| Cap | Kingdoms | Chest | Helmet 1.04 | Bracer .5 | Pauldron .28 | Greaves .45 |
|----:|---|---:|---:|---:|---:|---:|
| 52 | Erebor, Iron Hills (Ered Luin wears both lines' pieces) | 52 | 54 | 26 | 15 | 23 |
| 51 | Rivendell, Lindon | 51 | 53 | 26 | 14 | 23 |
| 48 | Mirkwood | 48 | 50 | 24 | 13 | 22 |
| 47 | Lothlorien | 47 | 49 | 24 | 13 | 21 |
| 46 | Gondor, Rhun, Black Numenoreans, Arnor | 46 | 48 | 23 | 13 | 21 |
| 43 | Dale | 43 | 45 | 22 | 12 | 19 |
| 42 | Gundabad | 42 | 44 | 21 | 12 | 19 |
| 41 | Dol Guldur, Khand (no item yet) | 41 | 43 | 21 | 11 | 18 |
| 40 | Isengard, Harad, Umbar, mercenary | 40 | 42 | 20 | 11 | 18 |
| 39 | Mordor Black Uruks | 39 | 41 | 20 | 11 | 18 |
| 37 | Rohan, Dunland | 37 | 38 | 19 | 10 | 17 |
| 36 | Mordor orcs, Misty Mountain orcs, goblins (one shared kit) | 36 | 37 | 18 | 10 | 16 |
| 35 | Thenn | 35 | 36 | 18 | 10 | 16 |

The columns are the elite band (rounded half up); `python -c "import rebalance_armor as ra; print(ra.cap_value(46, 'head', 'elite'))"`
from `tools/` prints any cell, and the other bands are in the subsection of 2026-11-14 (#1271) below.

Bands: light 0.16, medium 0.35, heavy 0.68, elite 1.0, lord 1.07 of the elite value, rounded half up
(the head has its own light and heavy bands, 0.24 and 0.82: `SLOT_BAND_RATIO`); only lord kit and
named hero kit (`HERO_NAMES`, `EXCLUDE_ID_SUBSTRINGS`) sit above a kingdom's elite band, apart from the
Lothlorien Mallorn kit (see the generator band fix below), and the
writer lowers the hero kit to the lord band (2026-11-14, below). The band an item sits in is the band of its LOWEST battle-set troop
wearer (`level_to_band`: light to 13, medium 14 to 18, heavy 19 to 30, elite 31 up), decided by
`--tier-source roster-first`; an id keyword decides only for kit no troop wears, and kit with
neither is left alone. Civilian sets never anchor, and the ladder-exempt troops
(`taom_schema.Validator._ARMOUR_LADDER_EXEMPT`: the Ithilien ranger, the troll, the Harad mount
riders, the five Feral Orc bandits) wear their kit without anchoring it, so the ranger's hood stays a hood. Since 2026-09-25
two narrower mechanisms sit beside it: noble-line troops anchor a band up, and a `(troop, item)`
pair excuses one piece (see the 2026-09-25 subsection below).

Sub-lines that share a folder are routed by id prefix (`LINE_PREFIXES`): `sk_md_num_` and
`sm_md_num_` to the Black Numenorean cap, `sk_uruk_mordor_` to the Black Uruk cap, `sk_md_mor_`,
`sk_md_orc_` and `sk_gn_orc_` to the orc cap (the goblin and Misty Mountain rosters wear those
same items), `sk_dg_` in the rhun folder to Dol Guldur, `urukscout_` to Isengard, `ar_ardunian_`
to Umbar. The `md_num` restat exclusion is lifted: the Black Numenorean set wanted level-anchored
tiering all along and now has its own cap. The Dol Guldur troop line is NAMED "Khamul ..." and is
not his kit, so `HERO_NAME_FALSE_POSITIVE_PREFIXES` keeps it on the curve.

A secondary stat an item carries (a Gondor chest's `arm_armor`, a Rhun chest's `leg_armor`, a
cape's `arm_armor`) exists from the heavy band only (`SECONDARY_FROM_BAND`). From heavy up it is the
primary times the smaller of the item's own share of its primary and `SECONDARY_SHARE_CAP` for that
slot and stat (chest arm 0.30, chest leg 0.40, cape arm 1.0), rounded half up
(`kingdom_secondary`); below heavy it is 0. A light or medium chest therefore loses its arm and leg
armour on the next apply; a 0 stat is also modifier-immune, so no loot roll lifts it. A piece the restat
raises into the heavy or elite band from below it (its current primary under the line's heavy curve value)
has an own share of 0, so its 0 secondaries take the legacy row's share for the tier instead, or, for a
stat the row lacks (a chest's `arm_armor`), the median share of its line's pieces at the heavy band or
above that carry the stat, hero and lord pieces left out (`raised_secondaries`, `line_secondary_shares`,
2026-11-17). A raised chest takes its line's median leg share even when it declares no `leg_armor`
(`UNDECLARED_RAISED_SECONDARIES`), and the apply adds the attribute on its own line: `mirkwood_body`
goes to 0/48/8/19 like the line's other elite chests, where a level 36 recruit wearing
`mkwd_inf3_chest` at 0/48/8/19 would otherwise lose armour when he upgrades to a warden. On
2026-11-17 it moved that one piece of the 2,945 the restat plans. A stat no such piece carries stays
0, and the dry run lists the piece. Weights and
`material_type` are not touched (`--keep-weights`, `--keep-material-type`); `modifier_group`
follows the band, and the three extremity slots (arm, leg, shoulder) share one loot table:
civilian, light and medium roll `cloth_unarmoured` (+3), heavy, elite and lord roll `leather` (+7;
Worn -2, Battered -3). The group also sets what a troop spawns with in a campaign battle, not only
what a looted piece rolls (open decision 1).
At the 2026-11-14 bands a +5 cloth roll let a light cape pass a heavy one (15 shoulder cases) and a
medium cape pass an elite one (5 cases). `analyze_armor_balance.check_kingdom_curve_invariant`
reports 0 violations at +3 and 20 at +5 (a test pins the 0). That invariant is on the primary
stat; the light side of each judged pair carries no secondary, and the secondaries of live items are
judged per item by `_check_tier_inversions`, which reads every governed stat with the item's own
modifier group. Secondaries are rounded half up from the item's CURRENT share at each apply, so an
item that passed through two bands can sit one point off a single direct pass; the property that
holds is that a dry run after any apply plans nothing (a test pins it, hero and unworn clamps
included).

`derive_armor_tiers.py`'s map follows the same precedence (anchor first, keyword for unworn kit,
civilian keyword kit civilian whoever wears it), so its `tier`, `target` and `status` columns
describe what the restat does; until the deep review of 2026-09-13 the map was keyword first and
mislabelled 682 of 1,861 worn items. Two engine facts checked on the installed 1.4.8 DLLs: no
armour stat is clamped on load (`ArmorComponent.Deserialize` is a plain `int.Parse`), and the
item's display tier and price follow `DefaultItemValueModel.CalculateArmorTier`
(`(1.2 head + body + leg + arm) x type x 0.1 - 0.4`), so an Erebor chest at 70 (the cap of that
day) showed the top "Armor Tier" in its tooltip and priced accordingly. The cap of 52 since
2026-11-14 keeps an elite chest on that top tier and drops every lower band (see "Two consumers
outside the tools"). No shop or loot path checked bans an item by
that tier, but the tier is not inert: `DefaultItemCategorySelector` files an armour item as
Garment, LightArmor, MediumArmor, HeavyArmor or UltraArmor by it (Tier2 up to Tier5), and
`WorkshopsCampaignBehavior` picks what a workshop produces by category, so an item whose tier moved
(an Erebor light chest went from Tier3 to Tier4, MediumArmor to HeavyArmor) changes which workshop
makes it and at what price.

**Applied 2026-09-13** to the Steam tree and the `lotraom-assets` mirror (byte-identical before
and after), backups `.bak-kingdomcurve-583` once per file: 2,510 items on the first run, 316
untouched (hero kit, civilian and unworn keyword-less kit), then 7 more after the ladder repair
below; a third run plans nothing. Command:

```
python tools/derive_armor_tiers.py
python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag kingdomcurve-583
python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag kingdomcurve-583 --armory-path "E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory\ModuleData\LOTRLOME_items"
```

The re-curve exposed 58 promotion edges where the child wore kit anchored low by a shared wearer
(`UPGRADE_ARMOUR_REGRESSION`); `fix_upgrade_armour_regressions.py --apply` resolved all 58 with 197
slot swaps in 12 troop files, and one derive + restat iteration after it was stable. Post-state
medians (total armour, capstone tiers): Gondor T9 251 (was 194), Erebor T9 335, elves T9 384,
Mordor T9 270, Rhun T9 288, Dol Guldur T9 243, Gundabad T8 248, Isengard T8 209, Rohan T8 203,
Dale T6 190, Dunland T6 165, Harad T6 174, Umbar T6 240 (it wears Black Numenorean pieces, which
carry the 57 cap), goblins T7 110. Left for the roster pass, as the cap-scaled
`CROSS_CULTURE_ARMOUR_INVERSION` cells: goblin T7, Mirkwood T7 to T10 (its tree shares a handful
of items across every tier), Lindon and Rivendell T10 (the golden-flower fan-out under its own T9);
also the vanilla `aserai_scale_armor_on_chain` (51) on Harad's camel lancer, out of the Armory's
reach.

Legacy baseline (primary stat, from `SLOT_BASELINES`; the fallback and the weight ladder):

| Slot | light | medium | heavy | elite | lord |
|------|------:|-------:|------:|------:|-----:|
| body (`body_armor`) | 20 | 32 | 42 | 50 | 60 |
| body (`leg_armor`, secondary) | 10 | 16 | 22 | 28 | 36 |
| head (`head_armor`) | 15 | 24 | 32 | 40 | 48 |
| leg (`leg_armor`) | 12 | 20 | 28 | 34 | 42 |
| arm (`arm_armor`) | 8 | 14 | 20 | 26 | 34 |
| shoulder (`body_armor`) | 5 | 9 | 13 | 19 | 25 |
| shoulder (`arm_armor`, secondary) | 3 | 6 | 11 | 17 | 22 |

Secondary rows were previously undocumented (they exist only in code). Civilian is omitted above but
is part of the curve; **shoulder civilian `arm_armor` is `0` on purpose** — see the invariant below.

Cultural mods (`CULTURAL_MODS`) express identity — e.g. dwarves high-protection/high-weight, elves high-protection/low-weight, orcs cheap/heavy-for-protection:

| Culture | protection | weight_mult | | Culture | protection | weight_mult |
|---------|-----------:|------------:|-|---------|-----------:|------------:|
| iron_hills | +5 | ×1.10 | | rivendell | +5 | ×0.70 |
| erebor | +4 | ×1.05 | | mirkwood | +5 | ×0.65 |
| arnor | +2 | ×1.00 | | gondor | +1 | ×1.00 |
| isengard | +2 | ×1.15 | | **dale** | +1 | ×1.05 |
| rhun | 0 | ×0.90 | | gundabad | 0 | ×1.15 |
| dol_guldur | 0 | ×1.10 | | mercenary | 0 | ×1.00 |
| rohan | −2 | ×0.90 | | dunland | −2 | ×0.85 |
| mordor | −1 | ×1.10 | | umbar | −1 | ×0.90 |
| harad | −3 | ×0.85 | | thenn | −3 | ×1.05 |
| troll | +8 | ×2.00 (boss — excluded) | | lothlorien* | +5 | ×0.70 |

`dale` was **added 2026-06-30** (it was previously absent → ran on the neutral default). `lothlorien*` has a mod but no live folder/troop (reconciliation item).

### Back to the vanilla range (2026-11-14)

The table above is the current one. On 2026-11-14 the maintainer brought armour back to the vanilla
range. The rule: the best troop's body sum (chest plus pauldron, elite band) is at most 67, the body
armour of vanilla's best armoured troop, and Erebor sits exactly on it (52 + 15). Measured vanilla
item maxima are helmet 55, chest 57, gloves 28, boots 26 and shoulder 16. The old table put the best
troop at a body sum of 112, and with it a 95 point sword cut did 3 damage to an elite. The order of
the kingdoms is the old order compressed, so the weakest kingdom (Thenn, 35) is at 0.67 to 0.70 of
the best; Dale moved up from the 44 group to 43 on its own because its smiths worked beside Erebor's.
The slot ratios changed with the caps: bracer 0.6 to 0.5, pauldron 0.6 to 0.28, greaves 0.5 to 0.45
(`SLOT_CAP_RATIO`); the helmet ratio rose from 0.9 to 1.04 the same day and `BAND_RATIO` was redone
(next subsection). `test_kingdom_caps.py` pins the body sum.

### Bands, secondaries, hero kit and worn limits (2026-11-14, #1271)

The first vanilla-range bands (light 0.40, medium 0.64, heavy 0.84) left a sword blow from an elite
unable to kill a recruit, so the bands fell and the head got its own two:

| Band | Chest, bracer, pauldron, greaves | Helmet | Secondaries |
|---|---:|---:|---|
| light | 0.16 | 0.24 | none |
| medium | 0.35 | 0.35 | none |
| heavy | 0.68 | 0.82 | the item's share, capped |
| elite | 1.0 | 1.0 | the item's share, capped |
| lord | 1.07 | 1.07 | the item's share, capped |

Gondor (cap 46), primary stat per band, and the secondaries an item at or over the share caps carries:

| Band | Chest (leg, arm) | Helmet | Bracer | Pauldron (arm) | Greaves |
|---|---|---:|---:|---|---:|
| light | 7 | 11 | 4 | 2 | 3 |
| medium | 16 | 17 | 8 | 5 | 7 |
| heavy | 31 (12, 9) | 39 | 16 | 9 (9) | 14 |
| elite | 46 (18, 14) | 48 | 23 | 13 (13) | 21 |
| lord | 49 (20, 15) | 51 | 25 | 14 (14) | 22 |

The goals the numbers serve (maintainer, 2026-11-14), all checked in tests or by a gate:

- R3a: the best elite set per region is at most vanilla's best troop (head 54, body 67, arm 59, leg
  49). `test_kingdom_caps.py` pins the head and body sums.
- R3b: the lord band is at most 5 points over the elite band per region, on every kingdom (pinned).
- R3c: no worn set sums above vanilla's best troop plus 10, region by region (head 64, body 77,
  arm 69, leg 59): `rebalance_armor.WORN_ARMOUR_CEILING`. The maintainer decided it is an error; it
  ships as a warning until every contributor has the rewritten Armory. The validator reads the Armory
  installed on each machine, so an error would block the commits of a contributor who still has the old
  one (which broke it on 367 troops before the apply of 2026-11-14). A medium, heavy or elite troop
  whose set carries less than the band floor (head and body 8, 20, 30 for medium, heavy, elite;
  light has none) is a warning, `WORN_ARMOUR_FLOOR`: a helmetless elite in a light chest is the case
  it names. The validator's gates of the same names are in
  [moduledata-validator-codes.md](../reference/moduledata-validator-codes.md).
- R3d: the two-tier loot invariant holds on every kingdom's primary stat at `VARIANT_CAP`.

A worn limit is judged on the reach of a seeded spawn (`worn_armour_extremes_per_slot`): per armour
slot the smallest and the largest value over the troop's battle sets, added over the slots.
`Equipment.GetRandomEquipmentElements` re-rolls the set index for every slot whenever its seed is not
-1 and a troop spawn passes `CharacterHelper.GetPartyMemberFaceSeed` (never -1), so in a campaign
battle each armour slot comes from its own set; only seed -1 (settlement guards) draws the armour slots
from one set (v1.5.7 `TaleWorlds.Core.Equipment.cs`, see `.claude/rules/troops.md`).
`analyze_kingdom_armour.py` has a "Worn floor and ceiling" section built from the same function and
constants. The ceiling and the floor judge the items' base stats, before item modifiers: a modifier
adds or removes flat points on a stat (the loot tables in the open decisions below), so a worn set
can sit a few points either side of the sum judged here.

Hero kit, with `derive_armor_tiers.py` recording `heroWorn` on every item a hero's battle sets wear
(it never moves `anchorLevel`). A hero's sets come from five places: the lords in
`characters/lords.xml` and the Harad lords, with their `<EquipmentSet id=...>` templates resolved
from `equipmentsets/`; the named companions; the wanderer templates of `taom_wanderers.xml`, which
say `is_hero="false"` but which `CompanionsCampaignBehavior` turns into heroes (it picks every
template with occupation Wanderer, `CompanionsCampaignBehavior.cs:348`, and calls
`HeroCreator.CreateSpecialHero` on it, `:389`; the hero takes a battle set from the template's
roster, `Hero.cs:2341`), read here as `is_template="true"` plus occupation Wanderer and never as a
troop anchor; the battle templates `lords.xslt` hands to the
retagged vanilla lords (the only stylesheet that assigns equipment sets: `lord_template_rosters.xslt`
only strips flags); and a direct `<equipment>` child of `<Equipments>`, which replaces its slot in
every set of that hero (`BasicCharacterObject.Deserialize` passes it to
`MBEquipmentRoster.AddOverriddenEquipments`; the shipped data has one, the Arnor king's crown on
`lord_AN1_1`). Until the xslt was read, a lord's piece such as `sk_gd_lam_inf_chest_lord_a`,
`ithilien_hood` or the Mouth of Sauron's helm took a full troop restat: reading it moved `heroWorn`
from 471 to 532 items, 38 more of them with no troop wearer. Reading the wanderer templates moved it
to 623: 91 more items, 8 of them with no troop wearer, none changing status. Eight changed plan, three of them a value, and
the dry run went from 2,742 to 2,739 changes: the Dol Guldur Uruk elite helmet
`sk_dg_uruk_helmet_elite_a`, which the plan had raised from 41 to 43, and two Rhun Loke-Rim pieces
are now clamped as hero kit instead of restated. Under `--tier-source roster-first`:

- Troll kit (`MONSTER_ID_SUBSTRINGS`) is never touched, worn or not.
- Kit a troop wears goes on the troop curve at its lowest wearer's band whatever its name: the
  Golden Flower `gf_` pieces, Erkenbrand's, Theoden's boots and gloves, Theodred's (26 items on
  2026-11-14). Their numbers can rise or fall.
- Hero-only kit with a kingdom cap (excluded by name, or worn only by a hero) is lowered to the lord
  band and never raised: `clamp_armour` keeps weight, material and loot table, and each secondary is
  the smaller of its value and the capped share of the new primary (Thranduil's and Legolas's gloves
  90 to 26; a Gondor 60/30 chest to 49/15).
- Named kit of Mordor's lords is priced on the `mordor_numenorean` line (cap 46, as Gondor), not on
  the `mordor/` folder's orc cap of 36: an id in that folder containing sauron, nazgul, witch_king,
  witchking or khamul (`MORDOR_LORD_KIT_MARKERS`, next to `LINE_PREFIXES`). The orc cap had put
  Sauron's torso at 39 body armour, under a Gondor elite troop (59). It is now 49 with arm 15 and
  leg 20 (it was 50, 40 and 45), the Nazgul's and Khamul's armour 48 with arm 14, and the Mouth of
  Sauron's helm stays at 50 (the lord helmet cap on that line is 51). The kit of the Dol Guldur
  troop line `sk_dg_khml_*` in `rhun/` is not matched. Seven other hero-only items are priced on a
  line weaker than a wearer's culture and are left as they are, because the routing is a decision
  for the maintainer: `dunland_caerdh_helmet_heavy_b` (dunland, cap 37; worn by Khand lords, 41) and
  six Dol Guldur pieces in `rhun/` (`sk_dg_khml_cape_heavy_b`, `sk_dg_khml_cape_plate_heavy_a`,
  `sk_dg_khml_chest_lord_a`, `sk_dg_khml_helmet_cult_lord_a`, `sk_dg_khml_helmet_inf_lord_a`,
  `sk_dg_khml_scalemail_lord_a`: dol_guldur, cap 41; worn by Rhun lords, 46). Wearers of five
  cultures with no cap line (southrhovanion, arthedain, eredluin, cardolan, bluecraig) were not
  compared.
- Unworn merchandise with a kingdom cap and no id keyword is lowered to the elite band plus the
  variant step, never raised; an unworn `_lord` keyword prices as elite.
- `--no-lower-armor` and `--weights-only` leave every clamp alone. Never pass `--no-lower-armor`
  (it would hold the light and medium pieces at their old values) or `--keep-materials` (it freezes
  the loot tables) for this apply.

Status: applied to the live Armory on 2026-11-14 (see "Applied on 2026-11-14" under the apply
sequence). The dry run before it
(`python tools/rebalance_armor.py --dry-run --all --tier-source roster-first --keep-weights
--keep-material-type`, which prints the totals) judged 2,945 items and planned 2,739 changes, with 97
generator-owned items left to `generate_lothlorien_armor.py` and `generate_snow_orc_armor.py`
(`--check` exited 1 on both until they were applied). 217 of the planned changes raise a stat (66
heavy, 151 elite): 213 helmets, because the head ratio rose to 1.04 and a helmet's band value can
pass its old one, and the four Golden Flower bodies `gf_body_1` to `gf_body_4`. 103 of the 217 are
unworn merchandise priced by their id keyword, and none is a clamp. A copy of the live
`LOTRLOME_items` took the same command with `--apply --backup-tag sim`: 83 of 95 files written, a
second apply wrote 0, no `weight` attribute text changed, no clamp raised a stat and no hero-only item
rose. The live Armory is unversioned: back it up before an apply, and land the in-repo gates with it.

Counts against the Armory before the apply, 2026-11-14 (`python tools/validate_moduledata.py`):
`WORN_ARMOUR_CEILING` 367 troops, `WORN_ARMOUR_FLOOR` 42, `CROSS_CULTURE_ARMOUR_INVERSION` 7,
`UPGRADE_ARMOUR_REGRESSION` 4. The dry run of the apply predicted 0 ceiling breaches
and 203 floor troops, because the light and medium bands fall; the write measured 196 floor troops
before the generator band fix (see "Applied on 2026-11-14"). On the copy, the
validator's `_worn_armour_limits` read 12 ceiling and 188 floor troops after the armour apply alone
and 0 and 203 once the two generators ran, and `fix_upgrade_armour_regressions.py` reported 14 edges
that lower armour against 4 now (0 left after its planned fixes).

### Consumers outside the tools (2026-11-14, #1271)

Several things read armour `Value` or `Tier` without going through the armour tools, and the new
numbers move all of them. Each was read in the v1.5.7 decompile or the repo; none was measured in game.

Creature attacks scale with the chest. `WargAttackService.CalculateWargAttackDamage`
(`Main/Features/Warg/WargAttackService.cs`, lines 27 to 32) multiplies a bite by
`clamp((100 - chest armour) / 100, 0, 1)`, and line 57 reads the chest as
`GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest)`. The spider
(`SpiderAttackService.CalculateSpiderBiteDamage`, `Main/Features/Spider/SpiderAttackService.cs:30`)
uses `clamp((100 - chest armour x 1.1) / 100, 0.2, 1)` with the constants in
`Main/Features/Spider/SpiderConfig.cs` (`ArmorMitigationFactor`, `MinArmorPassthrough`, lines 66 to
75), and its tuning comment assumes chest armour of about 15 light, 35 medium and 55 heavy. At the old
best-troop chest of 112 a warg did nothing to an elite (the factor clamps to 0) and a spider did its
20 percent floor. At 67 a third of a warg bite passes (0.33) and about a quarter of a spider bite
(0.26); a light troop at a chest of 6 to 8 takes about 0.93 of either. Neither creature is re-tuned
here.

Item price follows armour through `DefaultItemValueModel` (`TaleWorlds.Core.DefaultItemValueModel.cs`,
`CalculateArmorTier` at lines 9 to 29 and `CalculateValue` at lines 219 to 251): the tier is
`(1.2 head + body + leg + arm) x type x 0.1 - 0.4`, and the value is
`base x 2.75^Tierf x (1 + 0.2 x (appearance - 1)) + 100 x max(0, appearance - 1)`, with Tierf clamped
to -1..7.5 and a base of 120 for a chest, bracer or greaves and 100 for the other armour. The two
appearance terms are 0 at appearance 1, but the prices below use each item's own appearance (at
appearance 1 the four "after" prices would be 162, 403, 4,579 and 34,629). Either way the lower bands
get cheap. A Gondor chest before and after the plan: light `sk_gd_ano_chainmail_half_a` (23 to 7) 820 to
162, medium `sk_gd_ano_chainmail_full_a` (36 and arm 11, to 16 and 0) 13,214 to 765, heavy
`sk_gd_los_inf_chest_heavy_a` (48 and 16, to 31 and 9) 83,341 to 7,626, elite
`sk_gd_ano_chainmail_full_b` (57 and 17, to 46 and 14) 200,019 to 48,681. The formula is applied to the
plan by hand, not measured in game. Pricing is not re-balanced here, and these readers of `Value` and
`Tier` follow it:

- **Armoury upgrade price.** `ArmouryUpgradeService.PriceGold`
  (`Main/Features/ArmourAcquisition/ArmouryUpgradeService.cs:122` to `128`) charges the recipe's gold
  plus its `value_share` of the Value the piece gains (`armour_acquisition_config.xml`: heavy is 500
  gold and 0.35). A Gondor medium to heavy chest, `sk_gd_ano_chainmail_full_a` to
  `sk_gd_los_inf_chest_heavy_a`, gains 83,341 - 13,214 = 70,127 and costs 500 + 0.35 x 70,127 = about
  25,000 gold today; after the plan it gains 7,626 - 765 = 6,861 and costs 500 + 0.35 x 6,861 = about
  2,900.
- **Tournament prizes.** TAOM builds its own prize pool (`TaomTournamentModel`, `TournamentService.BuildPrizePool`)
  and ignores vanilla's 1,600 to 5,000 Value bounds. A regular tournament takes light, medium or civilian
  armour: a piece the class table lists needs a Value of at least 750, and an unlisted item needs Tierf 2
  ([arena.md](arena.md), "Prize pools"). The game then sorts the pool by Value and draws a small
  tournament's prize from one quarter of it by hero count (`FightTournamentGame.cs:362`). After the plan a
  Gondor medium chest (765) stays in the pool and a light chest (162) leaves it.
- **Loot choice.** `DefaultBattleRewardModel.GetRandomItem` (`DefaultBattleRewardModel.cs:103` to
  `150`) ranks a defeated troop's merchandise by Value and picks a piece with odds
  `0.325 x target / (max(target, Value + 0.1) x (n - rank))`. A piece whose Value drops under the
  target stops being discounted, so the odds shift toward armour.
- **Sell penalty.** `DefaultTradeItemPriceFactorModel.GetTradePenalty`
  (`DefaultTradeItemPriceFactorModel.cs:41`) adds `1.5 + max(0, Tierf - 1) x 0.25` to the penalty when
  the player sells a non-trade good, so a lower armour Tierf lowers the penalty as well as the price.
- **Discard and donate XP.** `DefaultItemDiscardModel.GetXpBonusForDiscardingItem`
  (`DefaultItemDiscardModel.cs:26` to `52`) gives Steward XP by `item.Tier`: 35 below Tier1, then 75,
  150, 250 and 300 for Tier1, Tier2, Tier3 and Tier4 and up, for a player with the perk that lets him
  donate armour (`PlayerCanDonateItem`). A lower armour tier gives less.
- **Companion gear.** When a companion becomes a lord, `CompanionRolesCampaignBehavior.AdjustCompanionsEquipment`
  (`CompanionRolesCampaignBehavior.cs:307` and `:315`) replaces each slot with the template's piece
  only if the companion's own piece is empty or has a lower `Item.Tier`, so the comparison moves with
  every tier.
- **Engine item category.** `DefaultItemCategorySelector` (`DefaultItemCategorySelector.cs:96` to
  `143`) puts armour of Tier1 or below in Garment, Tier2 in LightArmor, Tier3 in MediumArmor, Tier4 in
  HeavyArmor and Tier5 and Tier6 in UltraArmor, with `ItemObject.Tier` = `Round(Tierf) - 1` clamped
  (`ItemObject.cs:164` to `187`). A Gondor light chest (7) and medium chest (16) are Garment after the
  plan, and a heavy (31 with leg 12 and arm 9) and an elite chest (46, 18, 14) are UltraArmor. Each
  category has its own supply and demand in the markets; the effect on shop stock and prices is not
  verified.
- **The armour gate's fallback class.** `ArmourClass.FromEngineTier` classes a piece the class table
  lacks by the same tier, so an untabled piece moves with it.

### Lord kit (2026-11-15, #1271)

**What a lord wears.** A hero wears one whole battle set. `Hero.SetInitialValuesFromCharacter` takes
`characterObject.BattleEquipments.Where((Equipment x) => !x.IsEmpty()).ToList()` and then
`_battleEquipment = list.GetRandomElement().Clone()` (v1.5.7 `Hero.cs:2321` and `2341`), and
`_battleEquipment` is a `[SaveableProperty(210)]` (`Hero.cs:212`), so the set is picked once and kept
in the save. A lord's figure on each part (head, body, arm and leg, each summed over the Head, Body,
Cape, Gloves and Leg slots as the `WORN_ARMOUR_*` gates add them) is therefore his worst whole set.
Troops draw each slot from its own set, so a troop's figure stays the per-slot worst
(`rebalance_armor.worn_armour_extremes_per_slot`).

**The reference informs; it is not a target.** On each part it is the elite-band troop (level 31 to
51) of the lord's culture with the highest per-slot worst case, of the lord's race when the culture
has an elite troop of that race (lowest id on a tie). A lord is short on a part when his worst whole
set is under the reference by more than 10 percent of it and by 3 points or more
(`fix_lord_kits.TOLERANCE`, `SHORT_MIN_POINTS`, `is_short`), and over when his best set passes the
limit, the reference plus 5 (`CEILING_MARGIN`). A part that is not short is never a reason to change a
piece. The maintainer's word: "We can sacrifice armor points for variety", so a short lord is
information, not a defect. Khand (`battania`, 41 lords) and the Abanissa (80) have no elite troop of
their own, so each is judged against the reference of the culture whose templates its lords wear
(`judge_lords(..., template_cultures)`): Khand against Dunland (`empire`: head 38, body 47, arm 36 and
leg 17, from `dunland_bear_executioner`, `dunland_falcon_noble_horse_archer` and
`dunland_dragon_sniper`), the Abanissa against Harad (`aserai`: 39, 51, 25 and 39, all from
`harad_pz_camel_lancer`).

**Companions stay light.** This is the maintainer's decision (2026-11-15). The tool reads only
characters with occupation Lord and no wanderer template or named companion. A companion's hire price
is half the town price of its battle and civilian equipment plus ten per level
(`DefaultCompanionHiringPriceCalculationModel.cs:21` to `38`; TAOM does not override the model), so
heavier kit makes every companion dearer. A companion made a lord takes a template's piece wherever
its own has a lower `Item.Tier` ("Companion gear", above). The troll kit is not touched, and only the
five armour slots are written. The restat prices a lord-kit piece at the lord band only when a lord
wears it: `derive_armor_tiers.py` records `lordWorn` (an occupation Lord hero wears it) apart from
`heroWorn` (any hero, companions and wanderers included), and `_roster_first_plan` returns
`('lord', 'full')` on `lordWorn`. A piece only companions or wanderers wear stays on the clamp; on
2026-11-15 the map has 171 such pieces, `roh_nbl_shldr_s` (the named companion Eohart's) among them
(`test_a_named_companions_lord_line_cape_is_not_lord_kit`).

**The lord armour pool.** Per culture, the pieces of the culture's own Armory folders whose id
carries a lord-line token (`rebalance_armor.is_lord_kit`: `lord`, `noble`, `nbl`, `nob`, `king`,
`captain`, whole tokens) in a look family its troops or lords are seen in, plus the hero-line pieces
its lords or templates already wear (no troop under level 41 wears them). Hero-named kit
(`is_excluded`), monster kit, civilian kit and a tiered noble piece (`_noble_heavy_`, a soldier grade)
stay out. A family is the id less its slot words, tier words and trailing variants (`a` to `j`, `a04`,
`lord3`, `silver`). The table per culture (family, piece, armour, lowest troop wearer and level, the
templates that wear it, the promoter clones left out, and the pool pieces a troop under level 41
wears) is [lord-armour-pool.md](../reference/lord-armour-pool.md), written on 2026-11-15 by
`python tools/fix_lord_kits.py --values planned --pool-md docs/reference/lord-armour-pool.md` after the
templates were written, so it also holds the pieces the templates now wear. Body pieces in the pool on
2026-11-15 (the "Lord armour pool" lines of the same command): Gondor 14, Rivendell 13, Mordor 10,
Rhun 9, Lindon 9, Dunland 8, Rohan 8, Gundabad 6, Umbar 5, Dol Guldur 4, Lothlorien 3, Isengard 2,
Mirkwood 1, the orc cultures 1, and none for Harad, Dale or Erebor.

**The assignment, first pass (variety).** Within a culture the generic templates
(`<family>_bat_template_medium_<x>`) wear different Body pieces. A template keeps its set when its
Body is a lord or hero piece no earlier template wears. A template in a soldier's piece, or sharing
its Body while an unused pool Body fits its race, takes an unused one: a piece no low troop wears
first, then the old Body's family, then the family used least. A soldier's piece is never traded for
a pool piece a lower troop wears: that piece is priced at the lower troop's band, so the trade buys
no lord look. The other slots then come from the pool: a piece no template wears in the slot first,
then the new Body's family, else the nearest family; a lord piece already in a slot stays unless the
new family has it. When the pool runs out the template keeps what it wears and is reported. The
Armory has no race or gender usage flags (only `has_gender_variations`), so a piece fits a race only
when that race is seen wearing it or its family; gender fit is not known. Named rosters
(`faramir_bat_equipment`, `gondor_lord_*`) are never written. A promoted culture's templates are
clones: they follow on `promote_borrowed_cultures.py --only <culture> --apply`. For Arthedain and
Rhudaur the promoter's Arnor item map sends the new Gondor helmets and pauldrons to Arnor pieces of
their own (`sk_gd_lam_nob_helmet_lord_a` to the unworn `sk_ar_art_helmet_noble_prince_b`,
`sk_gd_lam_nob_helmet_lord_b` to `sk_ar_art_helmet_noble_elite_b`). The Arnor line has only four
noble chests and one lord-grade bracer, so the new chests and bracers share their Arnor piece with
another map entry (`sk_gd_lam_nob_chest_lord_a` and `sk_gd_mns_citadel_chest_heavy_a` both go to
`sk_ar_art_chest_noble_heavy_a`). Those lords keep the Arnor kit
(`test_an_item_mapped_clone_lord_wears_no_armour_of_its_source_culture`), and the report judges a
clone in its mapped kit (`clone_sets`).

**The assignment, second pass (tolerance).** Each set of each generic template is then walked slot
by slot (Body, Head, Cape, Gloves, Leg; a Cape counts on body, Gloves on arm). A slot changes only
when the set is short on that slot's part, or wears an ordinary soldier piece (not in the pool, worn
by a troop under level 41) while the pool has an unused variant. The order of choice
(`fix_lord_kits._tolerance_pass`):

1. (a) An unused lord-pool piece that fits the race, the Body's family first.
2. (b) When no pool piece fits, a rule (b) piece (`_rule_b_pieces`), from the culture's own Armory
   folders, so never a vanilla piece: a piece the culture's level 31 to 51 troops of that race wear in
   the slot (every troop counts, including those the reference leaves out, such as Harad's mumak
   riders); a piece of those folders that shares such a piece's id stem (the id less its trailing
   variant letter or number); or a piece of those folders of the slot that no troop, template or lord
   wears. Start kit (`starter_*`), monster, civilian and hero-named pieces, and a family seen only on
   another race, stay out. A piece no other template wears comes first. A piece goes on a second
   template only when the template's own piece leaves the part more than 25 percent under the
   reference (`REUSE_GAP`) and the reuse raises it by 3 points or more; inside 25 percent the template
   keeps its piece and is listed under (c).
3. (c) Nothing fits: the template keeps its piece and the tool lists it.

A swap never lowers another part of the set and never takes a part over the limit, the template
culture's and that of every other culture whose lords wear the template (the Blue Crag lords share
the goblin templates). It must hold on the live values and on the values the restat would write
(`--values planned` swaps the two). Among candidates of one rule the one closest to the reference
from below wins over a far stronger one. When a swap changes a Body, the Cape follows it: the Cape
the new Body's wearers (troops or lords) wear with it first, else a Cape of its family
(`same_look`: one family starts the other, in the same colour), and only when the Cape's part does
not fall. Templates that come to share a Body by a reuse then take different Head, Cape, Gloves and
Leg pieces where the pool or rule (b) has one that keeps the part from turning short; the template
that reused the Body is the one that changes, and its Cape keeps the Body's look. The Khamul pieces
stay on the Rhun templates `_c` and `_f` whatever their part (`KEEP_PIECE_RE`, the maintainer's word).
The pass runs to a fixed point, so a second run plans nothing.

**What the plan changed** (2026-11-15: 127 slots in 47 templates; the first pass 34, as soldier piece
11, shared Body 1, nearest family 21 and family of the new Body 1; the second pass 93, as rule (a) 14,
rule (b) 38, a rule (b) reuse 22, a Cape brought by its Body 12 and a slot changed on a shared Body
7; `python tools/fix_lord_kits.py` printed each with its rule before `--apply`):

| Templates | Slots | Rule | New pieces |
|---|---|---|---|
| Harad `_a` to `_e` | Body, Cape, Head, Leg | b, pair, variety | the table under "Harad" below |
| Dol Guldur `_a`, `_b`, `_c`, `_e`, `_g`, `_h` | Leg | a | `sk_dg_uruk_boots_lord_a` to `_f` in place of the elite boots |
| Dunland `_a` to `_d` | Body, Head, Cape (`_c`, `_d`), Gloves and Leg (`_c`, `_d`) | soldier piece, nearest family, a, b, pair | `dunland_caerdh_chainmail_lord_a` to `_d` (37), `caerdh_helmet_lord_a` to `_d`, `caerdh_pauldron_elite_a`, `caerdh_bracer_heavy_b`, `_c`, `caerdh_boots_heavy_b`, `wulf_boots_heavy_c` |
| Dunland `_e` | Body, Head, Cape | soldier piece, pair | `dunland_wulf_scalemail_lord_a`, `wulf_helmet_lord_a`, `wulf_pauldron_cape_a` |
| Goblin `_a`, `_c`, `_d` | Leg, Gloves (`_c`) | b | `sk_md_orc_boots_med_b` (a reuse on all three), `sk_md_orc_bracer_heavy_a` |
| Gondor `_a`, `_b`, `_d` (and the Cardolan clones) | all five (`_d` keeps its Cape) | soldier piece, nearest family, family of the new Body | `sk_gd_lam_inf_chest_lord_a`, `sk_gd_lam_nob_chest_lord_a`, `sk_gd_lon_chest_lord_a` (49), helmets `sk_gd_lam_nob_helmet_lord_a`, `_b`, `sk_gd_lin_helmet_lord_a`, capes `sk_gd_lam_cape_noble_a`, `sk_gd_lam_pauld_cape_noble_lord_a`, `sk_gd_sere_bracer_lord_a`, `sk_gd_sere_grvs_lord_a` |
| Gondor `_c` | Gloves | b | `sk_gd_ano_bracer_noble_heavy_a` |
| Arthedain and Rhudaur `_a` to `_d` | as Gondor | the item map | the Arnor noble chests, helmets (`noble_prince_b`, `noble_elite_b`), pauldrons, bracer and greaves |
| Rhun `_a`, `_d` | Gloves | b | `sk_rh_loke_bracer_heavy_c`, `sk_dg_khml_bracer_lam_heavy_a` |
| Lindon `_a`, Rivendell `_a` to `_e` | Body (`_a`, `_c`), Cape, Head, Gloves, Leg | soldier piece, nearest family, b | `rivendell_torso_lord1`, `_lord1_silver`, `pauldron_elite_robe`, `helmet_elite`, `_elite_silver`, `gloves_silver`, `vambraces_1_silvergold`, `_2_silvergold`, `boots_greaves1_silver`, `greaves2_silver`, `greaves2_silvergold`, `greaves3_silver` |
| Lothlorien `_a` | Head | a | `sk_ll_mw_helmet_lord_a` |
| Mirkwood `_a` to `_d` | Body, Cape, Gloves, Leg | b (reuse), pair, variety | `mkwd_inf3_chest_silver`, `pauldrons_silver`, `greaves_silver` on all four, `vambraces_bronze`, `_silver`, `mirkwood_gloves` |
| Mirkwood `_e` | Head | b | `mkwd_inf1_t3_helmet_gold_hc` |
| Mistymountain `_c` | Gloves | b | `sk_md_mor_arc_bracer_heavy_b` |
| Mordor `_a`, `_e` | Gloves, Leg, Cape | b | `sk_uruk_mordor_bracer_heavy_f`, `greaves_heavy_f`, `_b` (a reuse), `pauldron_heavy_f` |
| Dale `_d`, `_g` | Body, Gloves, Leg | b (reuse), variety | `chivalry_a04` and `mariner_b04`, which `_c` and `_h` also wear, with the archer gauntlets and boots so the pairs differ |
| Rohan `_a` to `_e` | all five | a, b, shared Body, pair, nearest family | Bodies `cts_rohan_armor_captain`, `roh_nbl_arm_r`, `cts_rohan_armor_captain1`, `whiterun_armor`, `cts_rohan_royal_guard_armor`; Capes `cts_rohan_captain_shoulder`, `whiterun_cloak`, `roh_nbl_shldr_cgs_2`; helmets `cts_rohan_helmet_captain`, `_captain1`, `whiterun_helm`; gloves `cts_rohan_royal_guard_gloves`, `whiterun_bracers`, `jor_lord_brcr_fgc3`; boots `cts_rohan_captain_boots1`, `whiterun_boots` (South Rhovanion follows) |

The `whiterun_*` pieces and `jor_lord_brcr_fgc3` are pieces of the Rohan folder that no troop, template
or lord wore: rule (b)'s third kind.

Distinct pieces per template culture, Body/Head/Cape/Gloves/Leg, at HEAD and now (2026-11-15, read
from the template files of a copy of ModuleData with HEAD's `equipmentsets/` and of this checkout;
`fix_lord_kits.py` prints Body/Head/Gloves/Leg in its "Distinct pieces" lines):

| Culture | Templates | HEAD | Now |
|---|---:|---|---|
| Harad (`aserai`) | 5 | 4/4/1/4/2 | 3/5/3/4/2 |
| Dol Guldur | 10 | 6/8/6/10/6 | 6/8/6/10/9 |
| Dunland (`empire`) | 5 | 3/3/3/3/2 | 5/5/3/3/3 |
| Goblin | 5 | 3/4/2/2/2 | 3/4/2/3/1 |
| Gondor (and Arthedain, Cardolan, Rhudaur) | 4 | 4/4/4/3/3 | 4/4/4/2/2 |
| Mirkwood | 5 | 4/5/4/4/4 | 1/5/1/3/1 |
| Mistymountain | 5 | 3/4/2/2/2 | 3/4/2/3/2 |
| Mordor | 5 | 5/5/5/5/5 | 5/5/5/5/4 |
| Rivendell | 5 | 5/5/3/4/2 | 5/4/2/3/5 |
| Dale (`sturgia`) | 8 | 8/8/6/8/8 | 6/8/6/7/6 |
| Anduinvale, Angaladh, Dorwinion (Dale clones) | 4 | 4/4/4/4/4 | 3/4/4/3/3 |
| Rohan (`vlandia`) | 5 | 1/1/1/1/1 | 5/4/3/4/2 |
| South Rhovanion (Rohan clone) | 4 | 1/1/1/1/1 | 4/4/3/4/2 |

Every other culture is unchanged. Mirkwood pays the most: its pool has one Body, its own chests
(body 8 and 17) are far more than 25 percent under the reference of 61, and the only Mirkwood piece
that repairs the body part is `mkwd_inf3_chest_silver`, so all five templates wear it and differ in
their helmets and gloves.

**Templates that share a Body after the plan** (the report's list; "same" names the slots two of them
still share):

- By this plan: Harad `_a` and `_c` (`sk_hr_st_chest_cav_heavy_a`; head, gloves and leg differ, the
  Cape `pauld_cav_elite_a` is the same because the other cavalry pauldron, 11/9, would lower a body
  part that is already short);
  Harad `_b` and `_e` (`chest_cav_heavy_b`; every other slot differs); Mirkwood `_a` to `_e`
  (`mkwd_inf3_chest_silver`; five helmets and three gloves, one Cape and one Leg); Dale `_c` and `_d`
  (`sk_dale_chest_chivalry_a04`) and `_g` and `_h` (`sk_dale_lake_town_chest_mariner_b04`), every
  other slot different.
- Shared before this work and left so: Dol Guldur `_a`/`_g`, `_b`/`_h`, `_c`/`_i`, `_d`/`_j`;
  Erebor `_a`/`_e`; Goblin and Mistymountain `_a`/`_d`, `_b`/`_e`; Gundabad `_a`/`_g`, `_b`/`_h`;
  Isengard `_a`/`_c`/`_e`, `_b`/`_d`/`_f`. The tool differentiates only the templates a reuse made.

**Body and Cape of different families after the plan** (the report's list, `same_look`): 13, all of
them so before this work too: Dale `_g` and `_h` (a Lake-town chest under a Dale archer shoulder),
Gondor `_a` to `_d` (a `sk_gd_lam_inf` or `_lam_nob` chest under a `sk_gd_lam` noble cape, and
`sk_gd_ano` pauldrons over the `sk_gd_mns_fount` and `sk_gd_lon` chests), Lindon `_a` and Rivendell
`_a` to `_e` (`rivendell_torso_lord1`, its silver twin or a silver or gold body under
`rivendell_pauldron_elite_robe` or `_tier2_cape`), and Umbar `_e` (`ar_ardunian_elite_armour` under
`sm_md_num_inf_pauld_cape_elite_b`). The pairs this work broke in its first version are fixed: Rohan `_a` wears the
captain shoulder with the captain chest, Dunland `_e` the Wulf cape with the Wulf chest, Mirkwood `_a`
to `_d` the silver pauldrons with the silver chest.

**The fallback and ruler rosters.** `taom_<culture>_(lord|ruler)_battle_(male|female)` in
`taom_lord_template_equipment.xml` (not `spc_*_lord`: those are the spawned lord character templates
that name them). The ruler rosters carry `IsKingdomRulerTemplate`, which
`DefaultEquipmentSelectionModel` asks for (`LordTemplateRosterTests`). They mirror the five armour
slots of their source template `_a` (`fix_lord_kits.FALLBACK_SOURCES`). On 2026-11-15 this work
changed 452 of their armour slots in 112 rosters: 296 slots in 80 rosters by `fix_lord_kits.py` (its
"Fallback and ruler roster changes" line, run on HEAD's `equipmentsets/`) and the rest by the
promoter runs for the promoted cultures (a slot count of the file at HEAD against this checkout). The
tool holds them to `_a`, through the tool and never the file's unsafe generator (#637), and
`test_every_fallback_and_ruler_roster_wears_its_template_a_armour` fails on any drift. The mirror
follows `_a`, so it can lower a roster's part: it lowered the Umbar rosters' leg by 17 (38 to 21
against a limit of 19, so 2 over instead of 19) and the Erebor and Eredluin rosters' head by 4 (54 to
50 against a reference of 54, not short), while their body and leg rose to the reference. Every other
part of every roster rose or held.

**Values follow the wearers.** 96 pairs of a pool piece and a troop under level 41 who wears it remain
(Gondor noble chainmail and capes, the Mordor Uruk captain chainmail `_a2` to `_d2` on the level 11
grunts, the Rohan `nbl` and `cts_rohan` captain pieces on riders of level 16 to 36 and the South
Rhovanion troops that clone them; the "Lord pool pieces a troop under level 41 wears" line);
[lord-armour-pool.md](../reference/lord-armour-pool.md) lists them by culture. The mesh ladder has a
substitute for none of them, because `pick_substitute` needs a tier token and these ids carry none,
so no troop swap is applied and they stay priced at their lowest wearer. A lord-worn lord-kit piece
is priced at the lord band (above); hero-named kit, a hero-line piece with no lord token and a piece
only companions wear stay on the clamp. On 2026-11-15 the restat dry run (`Changed: 755`) changes an
armour value on 37 pieces: the eight `sk_dg_uruk_helmet_lord_a` to `_h` 41 to 46, the six
`sk_dg_uruk_boots_lord_a` to `_f` leg 18 to 20, `sk_dg_khml_helmet_inf_lord_a` and `_cult_lord_a` 41
to 46, `sk_uruk_hai_helmet_captain_ar1` 40 to 45, `sk_ar_art_helmet_noble_prince_a` and `_b` 45 to 51,
`sk_gd_lam_nob_helmet_lord_b` 48 to 51, the five Dunland lord helmets 38 to 41, the five Dunland lord
chests body 37 to 40 and arm 9 to 10, `sk_uruk_mordor_chainmail_captain_a1`, `_b1` and `_c1` body 35
to 42 and arm 11 to 13, both `rivendell_torso_lord1` body 51 to 55, arm 15 to 16 and leg 20 to 22,
`roh_nbl_arm_r` body 38 to 40, and `jor_lord_brcr_fgc3` arm 19 to 20. No companion-only piece is among
them (`roh_nbl_shldr_s` stays at 7/5). The other 718 rows change no armour value: they move the
modifier group of extremities (325 shoulder, 223 arm and 170 leg pieces) from `leather` to `chain`,
the extremity loot table the session lead wrote to the live Armory (below). No Khamul piece goes
down.

**This change writes nothing in the Armory.** The values above are a dry run. On 2026-11-15 the
live Armory already carries the extremity loot table of `balance/extremity-modifier-group` (backup
tag `leather-extremity-20261115`), which this branch's `rebalance_armor.py` does not have, so its dry
run also plans those loot tables back. They go once both branches are merged. After the merge, the
session lead writes the lord values, from the repo root:

1. `python tools/derive_armor_tiers.py`, then `git diff --stat -- tools/data/armor_roster_tiers.json`.
2. `python tools/rebalance_armor.py --dry-run --all --tier-source roster-first --keep-weights --keep-material-type`:
   expect the 37 lord pieces above and no `modifier` change; stop if any extremity loot table moves.
3. `python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag lordkit-<date>`,
   then the same dry run again plans `Changed: 0`.
4. `python tools/generate_armour_classes.py --check`; if it reports the table stale,
   `python tools/generate_armour_classes.py --apply` and commit the table. On 2026-11-15 the check
   exits 0 on the live values; the tool reads only the install, so its run on the planned values is
   owed after step 3.
5. `python tools/fix_lord_kits.py --check` and `python tools/validate_moduledata.py` (read
   `LORD_ARMOUR_RANGE`).

**The check.** `LORD_ARMOUR_RANGE` (warn) in `python tools/validate_moduledata.py` gives one `floor:`
warning naming every part where a set is short, and one `ceiling:` warning naming every part where the
best set passes the limit, on the install's values (`taom_schema.Validator._lord_armour_range`, which
calls `fix_lord_kits.judge_lords`). Every lord that generic templates alone dress wears the same sets,
so the row is the template's: it names the template, counts its lords and lists the first six. A lord
with a roster or override of his own, and each fallback and ruler roster, keeps a row of its own. A
Khand or Abanissa row says which culture it was judged as. A lord file that does not parse, or a run
that reads no lords, is a `(tooling)` warning. Tests: `tools/tests/test_fix_lord_kits.py`,
`LordArmourRangeTests` in `tools/tests/test_validate_moduledata.py`, and the lord band tests in
`tools/tests/test_kingdom_caps.py`. On 2026-11-15 the validator gives 97 `LORD_ARMOUR_RANGE`
warnings: 58 on 42 generic templates and 39 on named lords and fallback rosters; 79 `floor:` (head 49,
body 41, arm 41, leg 51 part hits) and 18 `ceiling:` (head 2, arm 12, leg 13).

**Before and after** (`python tools/fix_lord_kits.py`, 2026-11-15; 1,671 lords and fallback rosters
judged, the 121 Khand and Abanissa lords as Dunland and Harad; "under (short)" counts every lord under
the reference and, in brackets, those short; "planned" is `--values planned`; HEAD is the same command
on a copy of ModuleData with HEAD's `equipmentsets/`):

| | head under (short) | body under (short) | arm under (short) | leg under (short) | over head/body/arm/leg |
|---|---:|---:|---:|---:|---|
| HEAD, live values | 797 (595) | 733 (533) | 966 (670) | 677 (616) | 42/0/17/18 |
| HEAD, planned values | 664 (565) | 704 (521) | 937 (670) | 677 (616) | 42/0/17/18 |
| Now, live values | 611 (322) | 461 (248) | 614 (189) | 340 (275) | 42/0/17/18 |
| Now, planned values | 451 (292) | 402 (248) | 573 (189) | 340 (275) | 42/0/17/18 |

Short lords per culture, head/body/arm/leg (same command and date):

| Culture | Lords | Short h/b/a/l: HEAD | now, live | now, planned | Over h/b/a/l: HEAD | now, live | now, planned |
|---|---:|---|---|---|---|---|---|
| abanissa | 80 | 80/80/80/80 | 62/80/35/80 | 62/80/35/80 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| anduinvale | 20 | 4/4/4/4 | 4/0/0/0 | 4/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| angaladh | 16 | 3/3/3/3 | 3/0/0/0 | 3/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| arthedain | 24 | 19/5/24/24 | 1/0/5/5 | 1/0/5/5 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| aserai | 56 | 56/56/56/56 | 41/56/25/56 | 41/56/25/56 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| battania | 41 | 41/33/24/16 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| bluecraig | 44 | 0/0/10/0 | 0/0/0/0 | 0/0/0/0 | 10/0/0/0 | 10/0/0/0 | 10/0/0/0 |
| cardolan | 20 | 0/4/20/16 | 0/0/4/0 | 0/0/4/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| dolguldur | 131 | 4/4/5/5 | 0/0/1/1 | 0/0/1/1 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| dorwinion | 20 | 4/4/4/4 | 4/0/0/0 | 4/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| empire | 56 | 56/46/30/20 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| erebor | 40 | 0/4/0/4 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| eredluin | 20 | 0/4/4/4 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| goblin | 44 | 34/0/10/29 | 34/0/0/0 | 34/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| gondor | 119 | 7/8/12/7 | 7/8/8/3 | 7/8/8/3 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| gundabad | 104 | 4/0/4/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| isengard | 50 | 5/5/5/11 | 1/1/1/7 | 1/1/1/7 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| khuzait | 126 | 30/4/50/4 | 30/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| lindon | 27 | 5/10/17/27 | 0/0/0/12 | 0/0/0/12 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| lothlorien | 18 | 0/0/0/1 | 0/0/0/1 | 0/0/0/1 | 0/0/1/0 | 0/0/1/0 | 0/0/1/0 |
| mirkwood | 31 | 6/24/24/24 | 1/0/0/0 | 1/0/0/0 | 0/0/2/0 | 0/0/2/0 | 0/0/2/0 |
| mistymountainorcs | 154 | 0/0/32/0 | 0/0/0/0 | 0/0/0/0 | 32/0/0/0 | 32/0/0/0 | 32/0/0/0 |
| mordor | 98 | 7/19/20/32 | 7/7/7/7 | 7/7/7/7 | 0/0/0/4 | 0/0/0/4 | 0/0/0/4 |
| rhudaur | 16 | 13/3/16/16 | 0/0/3/3 | 0/0/3/3 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| rivendell | 16 | 2/7/10/15 | 1/2/2/6 | 1/2/2/6 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| shaghana | 94 | 94/94/94/94 | 70/94/43/94 | 70/94/43/94 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| southrhovanion | 20 | 20/16/16/20 | 4/0/4/0 | 4/0/4/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| sturgia | 84 | 17/17/17/17 | 17/0/17/0 | 17/0/17/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| umbar | 14 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 | 0/0/14/14 | 0/0/14/14 | 0/0/14/14 |
| vlandia | 88 | 84/79/79/83 | 35/0/34/0 | 35/0/34/0 | 0/0/0/0 | 0/0/0/0 | 0/0/0/0 |
| **All** | | 595/533/670/616 | 322/248/189/275 | 292/248/189/275 | 42/0/17/18 | 42/0/17/18 | 42/0/17/18 |

The ceiling hits are older than this work: the Misty Mountain and Blue Crag orc lords' heads (32 and
10), the Umbar arm and leg (14 each), Mirkwood arm (2), Lothlorien arm (1) and Mordor leg (4).

**Left short under rule (c)** (2026-11-15, the `(c)` notes of `python tools/fix_lord_kits.py` on
HEAD's `equipmentsets/`):

- Harad `_a` to `_e`: body, Cape, leg and four heads (below).
- Goblin `_a`, `_c`, `_d` and `_e` Head (31 against 37): the only stronger elite helmet,
  `sk_md_orc_arc_helmet_heavy_a` (37), would take the Blue Crag lords who share these templates over
  their own limit of 36.
- Dale `_d` and `_g` Head (37 against 45) and Gloves (arm 38 and 39 against 46); Rohan `_d` and `_e`
  Head (32 against 38) and Gloves (arm 35 and 34 against 40): every piece that raises them is on
  another template, and the own piece is inside 25 percent.
- Rivendell `_b` Leg (25 against 43): no Rivendell lord or rule (b) piece is higher; `_d` and `_e`
  Leg (23 against 43): the greaves of 25 are on `_b`, and a reuse would gain 2 points.
- Dunland `_d` Body (37 against 47), Gondor `_c` Gloves (arm 42 against 49), Mirkwood `_d` Gloves (arm
  35 against 43) and Mordor `_e` Body (42 against 50).
- Kept by the maintainer's word: Rhun `_c` and `_f` Head, the Khamul helmets (41 against 48 live, 46
  on planned values).

The named rosters stay hand decisions: `gondor_lord_anfalas_1`, `gondor_lord_blackroot_1`,
`gondor_lord_dolamroth_2`, `saruman_bat_template` and the single heroes (Elrond, Arwen, Faramir,
Khamul and others).

**Harad.** Harad (`aserai`), Shaghana and the Abanissa lords wear `harad_bat_template_medium_a` to
`_e`, all three against the same reference: head 39, body 51, arm 25 and leg 39
(`harad_pz_camel_lancer`; Shaghana's own `harad_redserp_archer` has the same values). The reference
is the vanilla kit (`aserai_scale_armor_on_chain` body 51, worn from the level 16 `harad_pz_levy`),
and no vanilla piece goes on a lord template. Harad's pool has no Body, so the templates take rule (b)
pieces of the Harad folder: the kit of its mumak riders (`harad_elephant_rider`,
`harad_mumakil_rider`, level 51), the helmets no troop wears (`sk_hr_st_helmet_heavy_c` 38 and `_d`
34, `sk_sg_st_helmet_heavy_d` 34) and an unworn infantry pauldron:

| Template | Head | Body | Cape | Gloves | Leg | Head/body/arm/leg |
|---|---|---|---|---|---|---|
| `_a` | `helmet_heavy_c` | `chest_cav_heavy_a` | `pauld_cav_elite_a` | `bracer_light_a` | `grvs_heavy_a` | 38/39/21/12 |
| `_b` | `helmet_heavy_a` | `chest_cav_heavy_b` | `pauld_cav_elite_b` | `bracer_med_a` | `grvs_heavy_b` | 34/38/24/12 |
| `_c` | `helmet_heavy_d` | `chest_cav_heavy_a` | `pauld_cav_elite_a` | `bracer_med_b` | `grvs_heavy_b` | 34/39/25/12 |
| `_d` | `helmet_heavy_b` | `chest_inf_chest_heavy_b` | `pauld_inf_heavy_c` | `bracer_heavy_b` | `grvs_heavy_b` | 34/35/29/12 |
| `_e` | `sk_sg_st_helmet_heavy_d` | `chest_cav_heavy_b` | `pauld_cav_elite_a` | `bracer_light_a` | `grvs_heavy_a` | 34/39/21/12 |

Every other id carries the `sk_hr_st_` prefix; the planned values give the same totals. Each template
has its own helmet, and `_a` reaches the head reference less 1. The Capes follow the Bodies: a
cavalry chest takes a cavalry pauldron (the riders wear them together), and the other cavalry pauldron
(11/9) would lower a body part that is already short, so `_a`, `_c` and `_e` share
`pauld_cav_elite_a`. The two cavalry chests go on four templates (a reuse on `_c` and `_e`, whose own
chests of body 14 were far more than 25 percent under), and the two heavy greaves on all five (the
Shaghana heavy greaves are worn by level 26 troops and share no stem with them, as with the chests). The infantry and archer
heavy chests and the Shaghana twins (all 27/8, the cavalry chests' values) would give distinct Bodies,
but troops of level 26 wear them and they share no id stem with an elite piece, so rule (b) as decided
does not take them; the body and leg stay short until Harad has lord armour of its own line (new art,
or a restat of an unworn Harad piece), the maintainer's choice. `_a` and `_e` keep their light bracer
because the heavy one (arm 14) takes the set over the arm limit of 30. The fallback and ruler rosters
of all three cultures follow `_a`.

**Existing saves.** A hero's battle set is saved (above), so the kits reach new campaigns only. The
maintainer does not mind existing saves (2026-11-15).

The Harad and Mirkwood kits above are replaced by the lord armour variants below (#1333).

### Lord armour variants (2026-11-15, #1333)

**What a variant is.** A lord armour variant is a TAOM item that copies one Armory armour piece: the
same mesh, culture, weight, appearance, cover and gender flags, `material_type`, `Flags` and every
other attribute or child the source declares. It differs in five things: the id `taom_lord_<source
id>`; `is_merchandise="false"`; its own name key `taom_lord_<source id>_name`, with "Lord's" after the
culture tag of the source's English name and the tier words Light, Med, Medium, Heavy and Elite left
out (a variant is at lord values; the look letter or numeral stays), and a name two variants would
share numbered I, II, ... in spec order (`unique_names`); `modifier_group` at the lord band
(`modifier_group_for`); and its armour, at the lord band of the source's kingdom line
(`rebalance_armor.calculate_stats('lord')`).

The slot's primary is the lord value, or the spec row's `cap` where the lord band would pass the
culture's limit (the reference plus 5); a cap must bind, carries a reason, and fails `--check` when it
passes the limit of the slot's part as the reference stands today (`check_cap`), so a changed
reference cannot leave a stale cap. Each secondary takes the
larger of two shares of the primary (`kingdom_secondary`, as the restat prices a lord piece): the
source's own, and that of the culture's reference piece in the slot. The reference piece is the one
the troop that sets the culture's reference on the slot's part (`fix_lord_kits.culture_reference`; a
Cape's part is body) wears there, the lowest of its sets: Mirkwood's is `mkwd_inf3_chest_silver`
(0/48/8/19, from `mirkwood_beleglas`), Harad's the vanilla `aserai_scale_armor_on_chain` (0/51/16/16,
from `harad_pz_camel_lancer`). So a lord does not lose arm armour because his chest's look carries no
arm share. Where the source has no share, the row secondary of the slot is the least value written (a
chest's leg, a cape's arm, as `generate_lothlorien_armor.py` writes them). A declared stat with no share
and no row value stays 0.

A variant is made only for a piece whose primary the curve leaves short of the lord band (or the cap)
for good (`fix_lord_kits.is_short`): one a troop wears (`derive_armor_tiers.parse_rosters`), priced at
its lowest wearer's band, or one no troop wears that has no lord-line token, which the restat only
lowers when a lord wears it (`rebalance_armor._roster_first_plan`). An unworn lord-line piece is
restated at the lord band and needs none. The refusal tests the lord-line token only, while the restat
raises such a piece only when it is also not hero-named (`rebalance_armor.py:1076`): an unworn,
hero-named lord-line piece is refused too, on the safe side. A piece already at lord values is worn as
it is, but a shipped variant whose source the restat later raises to the lord band stays at the
generator's values (`shipped_ids`; the four Mirkwood band clone sources, 2026-11-17), because a save can
hold it. No Armory
value changes: the Armory is only read, and `python tools/rebalance_armor.py --check` stays at 0.

**The spec and the output.** `tools/lord_armour_variants.json` lists, per template culture (`aserai` is
Harad), the source ids with a reason each; one source belongs to one culture.
`tools/generate_lord_armour_variants.py` writes `Main/_Module/ModuleData/lord_armour/lord_armour_variants.xml`
(CRLF, parsed before any write) and one `Items` node in `Main/_Module/SubModule.xml`, after the
placeholder weapons' node. `--check` also fails when any roster other than a generic lord template
(`<family>_bat_template_medium_<x>`) or a fallback or ruler roster, or any other `*.xml` or `*.xslt`,
names a variant (`roster_violations`), and when a troop, companion, wanderer or other hero who is not a
lord wears one, inline or through a template roster by id (`worn_violations`, through
`derive_armor_tiers._hero_battle_items`). A lord is an occupation Lord hero or `lords.xslt` lord
(`_hero_elements(lords_only=True)`), or a template a culture lists in `<lord_templates>` or
`<rebellion_hero_templates>` (the `spc_<culture>_lord_<n>` templates wear the fallback rosters). A
variant is an id the spec lists (`derive_armor_tiers.lord_variant_source`), not any id that starts with
`taom_lord_`: the `taom_lord_intro_*` dialogue keys share the prefix.

**Who reads the variants.** `derive_armor_tiers.build_armory_index` reads the file after the Armory,
each variant in its source's folder and slot, so `derive_armor_tiers.py`, `generate_armour_classes.py`
and `fix_lord_kits.planned_items` see them. `fix_upgrade_armour_regressions.load_item_armour` already
read the repo's ModuleData, so the validator (`LORD_ARMOUR_RANGE`, `WORN_ARMOUR_*`, item refs) and
`fix_lord_kits.py` see them as well. In `fix_lord_kits.py` a variant has its source's family and
colour (`_split`), and it is a lord-pool piece of the culture the spec names whatever its folder
(`lord_pool`, argument `variants`); it is never a rule (b) piece. The class table classes every variant
`named` (`generate_armour_classes.classify`). The gate `lord-variant-meshes` runs
`validate_mesh_refs.py` over the variant file, because the `mesh-refs` gate scans the Armory only.

**Commands**, from the repo root:

```
python tools/generate_lord_armour_variants.py            # dry run: each variant, its source, old -> new values
python tools/generate_lord_armour_variants.py --apply    # write the file and the SubModule node
python tools/generate_lord_armour_variants.py --check    # gate lord-armour-variants: 0 clean, 1 drift or a bad roster, 2 no install, 3 crash
python tools/derive_armor_tiers.py
python tools/generate_armour_classes.py --apply
python tools/fix_lord_kits.py --check                    # then again with --values planned
python tools/fix_lord_kits.py --values planned --pool-md docs/reference/lord-armour-pool.md
```

**Adding a culture.**

1. Pick the looks: the culture's own line, one Body, Cape and Leg family per template. A piece at lord
   values needs no variant.
2. Add a row per source to `tools/lord_armour_variants.json`, under the template culture, with a
   reason. Run the dry run; it refuses a source already at lord values or an unworn lord-line piece.
   Where a template's total would pass the reference plus 5, add a `cap` with its reason.
3. `--apply`, then edit the templates (`equipmentsets/taom_equipment_sets_<culture>.xml`) by hand to
   wear the variants. Keep each file's CRLF.
4. `python tools/fix_lord_kits.py`, read its plan, then `--apply`: it writes the fallback and ruler
   rosters (they mirror `_a`) and any swap of its own. Repeat until `--check` exits 0 on the live and
   the planned values.
5. `python tools/derive_armor_tiers.py`, `python tools/generate_armour_classes.py --apply`, the pool
   table above, and `python tools/validate_moduledata.py`. If a promoted culture clones the culture's
   templates, run `python tools/promote_borrowed_cultures.py --only <culture> --check` and re-apply
   through the promoter.
6. Add the new name keys to the backlog in `docs/localization/translation-backlog.md`.

**The variants of 2026-11-15** (head/body/arm/leg; `python tools/generate_lord_armour_variants.py`):

| Culture | Source | Slot | Worn from | Source values | Variant values | English name |
|---|---|---|---|---|---|---|
| mirkwood | `mkwd_inf3_chest` | body | L11 | -/8/0/0 | -/51/9/20 | [Mirkwood] Lord's Infantry Chestplate I |
| mirkwood | `mkwd_inf3_greaves` | leg | L11 | -/-/-/3 | -/-/-/23 | [Mirkwood] Lord's Greaves I |
| mirkwood | `mkwd_inf3_chest_bronze` | body | L16 | -/17/0/0 | -/51/9/20 | [Mirkwood] Lord's Infantry Chestplate III |
| mirkwood | `mkwd_inf3_pauldrons_bronze` | shoulder | L16 | -/5/0/- | -/14/12/- | [Mirkwood] Lord's Pauldrons III |
| mirkwood | `mkwd_inf3_greaves_bronze` | leg | L16 | -/-/-/8 | -/-/-/23 | [Mirkwood] Lord's Greaves III |
| mirkwood | `mirkwood_body` | body | L11 | -/8/0/- | -/51/9/20 | [Mirkwood] Lord's Palace Guard Body Armor |
| mirkwood | `mirkwood_boots` | leg | L11 | -/-/-/3 | -/-/-/23 | [Mirkwood] Lord's Palace Guard Boots |
| aserai | `sk_hr_st_chest_inf_chest_light_a` | body | L6 | -/6/0/- | -/43/13/17 | [Harwan] Lord's Infantry Chest A I |
| aserai | `sk_hr_st_chest_inf_chest_med_a` | body | L11 | -/6/0/- | -/43/13/17 | [Harwan] Lord's Infantry Chest A II |
| aserai | `sk_hr_st_chest_inf_chest_med_b` | body | L16 | -/14/0/- | -/43/13/17 | [Harwan] Lord's Infantry Chest B I |
| aserai | `sk_hr_st_chest_inf_chest_heavy_b` | body | L26 | -/27/8/- | -/43/13/17 | [Harwan] Lord's Infantry Chest B II |
| aserai | `sk_hr_st_chest_cav_heavy_b` | body | L26 | -/27/8/- | -/43/13/17 | [Harwan] Lord's Cavalry Chest B |
| aserai | `sk_hr_st_pauld_inf_med_a` | shoulder | L11 | -/2/0/- | -/12/11/- | [Harwan] Lord's Infantry Pauldron A |
| aserai | `sk_hr_st_grvs_light_a` | leg | L6 | -/-/-/3 | -/-/-/19 | [Harwan] Lord's Greaves A I |
| aserai | `sk_hr_st_grvs_med_a` | leg | L16 | -/-/-/6 | -/-/-/19 | [Harwan] Lord's Greaves A II |
| aserai | `sk_hr_st_helmet_heavy_c` | head | no troop | 38/-/-/- | 44/-/-/- (cap) | [Harwan] Lord's Helmet C |
| aserai | `sk_hr_st_helmet_heavy_a` | head | L21 | 34/-/-/- | 44/-/-/- (cap) | [Harwan] Lord's Helmet A |
| aserai | `sk_hr_st_helmet_heavy_d` | head | no troop | 34/-/-/- | 44/-/-/- (cap) | [Harwan] Lord's Helmet D |
| aserai | `sk_hr_st_helmet_heavy_b` | head | L26 | 34/-/-/- | 44/-/-/- (cap) | [Harwan] Lord's Helmet B |
| aserai | `sk_sg_st_helmet_heavy_d` | head | no troop | 34/-/-/- | 44/-/-/- (cap) | [Shaghana] Lord's Helmet D |

A `-` is a stat the item does not declare. The Harad helmet's lord band is 45, over the head limit 44
(the reference 39 plus 5), so each helmet row carries `cap` 44. Every variant keeps its source's other
attributes, the helmets' `hair_cover_type` and `beard_cover_type` among them.

**Mirkwood.** The four looks of 12b21d5e3 are back: `_a` plain (`mkwd_inf3_*`), `_b` bronze, `_c` and
`_d` the palace guard (`mirkwood_*`), `_e` silver. The fifth template, `_c`, takes the look whose other
slots differ most from those of the template already in it: its helmet and vambraces share one family
token with `_d`'s, two with `_b`'s, three with `_a`'s and four with `_e`'s. Heads and Gloves are
unchanged; the plain and palace-guard pauldrons (13/11) are worn as they are.

| Template | Body | Cape | Leg | h/b/a/l before | after |
|---|---|---|---|---|---|
| `_a` | `taom_lord_mkwd_inf3_chest` | `mkwd_inf3_pauldrons` | `taom_lord_mkwd_inf3_greaves` | 50/61/43/41 | 50/64/44/43 |
| `_b` | `taom_lord_mkwd_inf3_chest_bronze` | `taom_lord_mkwd_inf3_pauldrons_bronze` | `taom_lord_mkwd_inf3_greaves_bronze` | 50/61/43/41 | 50/65/45/43 |
| `_c` | `taom_lord_mirkwood_body` | `mirkwood_shoulder` | `taom_lord_mirkwood_boots` | 50/61/43/41 | 50/64/44/43 |
| `_d` | `taom_lord_mirkwood_body` | `mirkwood_shoulder` | `taom_lord_mirkwood_boots` | 50/61/43/41 | 50/64/44/43 |
| `_e` | `mkwd_inf3_chest_silver` | `mkwd_inf3_pauldrons_silver` | `mkwd_inf3_greaves_silver` | 50/61/43/41 | 50/61/43/41 |

The reference is 50/61/43/41 (`mirkwood_beleglas` and its peers), the limit 55/66/48/46. The plain,
bronze and palace-guard chests declare no arm armour; each variant chest takes the silver chest's arm
share (8 of 48) and so carries arm 9, and no template is lower on any part than before.

**Harad.** Each template has its own chest, all of the Harad infantry or cavalry line, and its own
helmet at 44; the Capes follow the chest's line (`same_look`). Every chest variant carries
arm 13 (the reference chest's share, capped at 0.30), so the Gloves are the lightest that keep arm at
or over its value before #1333 and inside the limit of 30: light bracers on `_a`, `_b`, `_c` and `_e`.
`_d` was at arm 29 before; with the infantry pauldron variant (arm 11) no Harad bracer lands it on 29
or 30, so `_d` wears `sk_hr_st_pauld_inf_elite_b` (11/9) and a medium bracer (7).

| Template | Head | Body | Cape | Gloves | Leg | h/b/a/l before | after |
|---|---|---|---|---|---|---|---|
| `_a` | `taom_lord_..._helmet_heavy_c` | `taom_lord_..._inf_chest_light_a` | `sk_hr_st_pauld_inf_elite_a` | `sk_hr_st_bracer_light_a` | `taom_lord_..._grvs_light_a` | 38/39/21/12 | 44/55/26/36 |
| `_b` | `taom_lord_..._helmet_heavy_a` | `taom_lord_..._inf_chest_med_a` | `taom_lord_..._pauld_inf_med_a` | `sk_hr_st_bracer_light_b` | `taom_lord_..._grvs_med_a` | 34/38/24/12 | 44/55/27/36 |
| `_c` | `taom_lord_..._helmet_heavy_d` | `taom_lord_..._inf_chest_med_b` | `taom_lord_..._pauld_inf_med_a` | `sk_sg_st_bracer_light_a` | `taom_lord_..._grvs_med_a` | 34/39/25/12 | 44/55/27/36 |
| `_d` | `taom_lord_..._helmet_heavy_b` | `taom_lord_..._inf_chest_heavy_b` | `sk_hr_st_pauld_inf_elite_b` | `sk_hr_st_bracer_med_b` | `taom_lord_..._grvs_med_a` | 34/35/29/12 | 44/54/29/36 |
| `_e` | `taom_lord_sk_sg_st_helmet_heavy_d` | `taom_lord_..._chest_cav_heavy_b` | `sk_hr_st_pauld_cav_elite_a` | `sk_hr_st_bracer_light_a` | `taom_lord_..._grvs_light_a` | 34/39/21/12 | 44/55/26/36 |

`...` is `sk_hr_st`. The reference is 39/51/25/39 (`harad_pz_camel_lancer`), the limit 44/56/30/44.
No part is short on any template. The fallback and ruler rosters of Harad, Shaghana, the Abanissa and
Khand follow `_a`, and Mirkwood's follow its `_a` (written by `fix_lord_kits.py --apply`).

**Before and after** (`python tools/fix_lord_kits.py`, live values; the planned values print the same
line): all judged lords 1,671; head short 292 to 119, body short 248 to 18, arm short 189 to 86, leg
short 275 to 45; every over count unchanged (head 42, body 0, arm 17, leg 18). Per culture, short
head/body/arm/leg: Harad (`aserai`) 41/56/25/56 to 0/0/0/0, Shaghana 70/94/43/94 to 0/0/0/0, the
Abanissa 62/80/35/80 to 0/0/0/0, Mirkwood 1/0/0/0 to 1/0/0/0. `LORD_ARMOUR_RANGE` warnings: 95 to 72,
none of them on a Mirkwood or Harad template or roster.

A source piece the templates no longer wear loses its lord wearers, so five Harad pieces change plan in
`tools/data/armor_roster_tiers.json` (`rebalance_armor._roster_first_plan`): `sk_hr_st_helmet_heavy_d`
and `sk_sg_st_helmet_heavy_d` from (lord, clamp) to (heavy, full), `sk_hr_st_pauld_cav_elite_b` to
(elite, full), `sk_hr_st_pauld_inf_heavy_c` to (heavy, full), and `sk_hr_st_pauld_inf_elite_b`, which
`_d` now wears, from (elite, full) to (lord, clamp). Each is already at its new band (`status` match),
so `python tools/rebalance_armor.py --check` stays at 0. Mirkwood has four looks for five templates,
so `_c` and `_d` share Body, Cape and Leg.

**Where a variant can and cannot reach a player** (engine lines from the v1.5.7 decompile):

| Route | What happens to a variant | Where |
|---|---|---|
| Shops | `is_merchandise="false"` sets `NotMerchandise`; a workshop never produces such an item; TAOM's market gate refuses a non-merchandise record and a named piece, and never flips one to merchandise | `ItemObject.cs:501`, `WorkshopsCampaignBehavior.cs:993`; `ArmourGateService.cs:141` and `:147`, `ArmourGatePlanner.cs:51` |
| Tournament prizes | vanilla skips `NotMerchandise`; TAOM's pool refuses a non-merchandise item and takes only light, medium, civilian or heavy classes | `DefaultTournamentModel.cs:101`; `TournamentPrizeRules.cs:59`, `TournamentService.cs:62` to `:64` |
| Loot from a defeated lord | vanilla loots a casualty (dead or wounded) through `GetLootedItemFromTroop`, whose `GetRandomItem` skips a `NotMerchandise` item; a party's item roster is looted without them too. TAOM does not override it, and loot is unchanged | `MapEvent.cs:1490`, `:1502`, `:1657`, `:1396`; `DefaultBattleRewardModel.cs:92`, `:108`, `:136`; `BattleLootAdapter.cs:54` |
| Armoury upgrade | a named piece is no upgrade source (rank -1), and the table names no variant as `next` | `ArmouryUpgradeService.cs:49` to `:53`, `ArmourClass.cs:73` |
| Lord's Ladder, lord harness | they hand out `GetPieces(ArmourClass.Lord)`, and a variant is `named` | `LordsLadderService.cs:137`, `:145`; `LordHarnessService.cs:41`, `:48` |
| Stripping a captive lord | named pieces are left on him | `StripService.cs:84` |
| Companion made a lord | the template kit replaces a lower tier piece when the companion leaves the player's clan as a lord: a lord wears it | `CompanionRolesCampaignBehavior.cs:291`, `:299` to `:318`; `TaomEquipmentSelectionModel.cs:22` |
| Enlistment and career kits | both generators read only non-hero troops of `troops/troops_*.xml`, and no troop wears a variant | `generate_enlistment_rosters.py:377`, `:1029`; `generate_career_kits.py:140`, `:207` |
| Faction screen preset or takeover (known route) | a lord picked on the faction screen puts his gear on the player: a preset clones the hero's battle equipment and fills the player's with it, and a takeover plays the lord himself with his gear ([faction-ui.md](faction-ui.md), the takeover and "Copied picks" items) | `PresetAppearanceAdapter.cs:26` to `:33`, `:94` |
| Come of age in the player's clan (known route) | a hero of the player's clan who comes of age as a Harad or Mirkwood lord gets a peer lord's kit | `TaomEquipmentSelectionModel.cs:19`, `LordKitDonorAdapter.cs:45` to `:66` |

The two known routes are the same class: every hero-only lord piece already reaches the player this
way, and the variants add no new route. A variant on the player can be moved in the inventory and sold:
the engine's transfer checks only a quest item and `GetIsTransferable`, which refuses only
`stealth_throwing_stone` (`SPItemVM.cs:487`, `DefaultItemValueModel.cs:254` to `:257`), and
`InventoryLogic.cs` reads no `NotMerchandise`. A sold variant stays in the town's market until the next
daily tick of that town, when TAOM's sweep removes it as a gated `named` piece that is not merchandise
(`ArmourAcquisitionCampaignBehavior.cs:89`, `ArmourStockSweepService.cs:37` to `:41`,
`ArmourGateService.cs:141`, `ArmourClass.cs:45`); the sweep does not run while the armour gate is off.

### Armour band clones and upgrades (2026-11-17)

**The problem.** The roster-first restat prices an Armory piece at the band of its lowest battle wearer
(`_roster_first_plan`, `worn_band`). When a troop of tier 7 to 10 and a low troop wear the same piece, the
low troop drags it down for the top-tier troop. `mirkwood_body` (the palace guard chest) went from body 25
to 8 on 2026-11-14, because two level 11 militia wear it beside the level 41 to 51 wardens, guardians and
palace guards; `sk_md_orc_inf_chest_med_b` sits at body 13 on three level 36 goblins. The scope (the
maintainer, 2026-11-17) is a piece worn in battle by a troop of level `rebalance_armor.TOP_TIER_LEVEL` (36)
or higher and by a troop of level 30 or lower (`rebalance_armor.shared_down_pieces`, levels at their anchor:
a noble line a band up, the ladder's exempt troops and `(troop, item)` pairs skipped). On trunk 642b1c482
that was 101 pieces. Every other piece keeps its lowest-wearer price: the restat rule did not change.

**Two treatments, one classifier** (`generate_armour_band_clones.treatment`):

- **A, duplicate.** The piece is an elite troop's own armour that low troops borrow: an `_elite_` or
  `_lord_` id, or a named piece with no tier token (the palace guard chest, "[Noldor] Gold Greaves III").
  Each band of its low wearers wears a clone; the source keeps only its level 31+ wearers, so the next
  derive gives it an elite anchor and the restat raises it to the elite band.
- **B, upgrade.** The piece is a low or mid piece: a `_light_`, `_med_` or `_heavy_` id, or a name that
  ends in the numeral I or II or says Infantry, Militia or Levy. Its level 36+ wearers swap to a stronger
  piece of the same kingdom line and slot (`pick_upgrade`), and the piece keeps its price for its low
  wearers. No clone.

**The upgrade pick.** A candidate is a live Armory piece of the same slot file, Type and kingdom line
(`kingdom_key`). It is not civilian, troll, hero-named, Mallorn (a band up by its generator) or a clone, and
not lord armour (a lord-line token with no tier or the lord tier, as `fix_lord_kits.lord_pool` marks it)
unless the source is of a lord line itself. A colour variant of the source is never an upgrade: the rule
(`rebalance_armor.COLOR_VARIANT_RE`) drops a trailing colour suffix, a hyphen or en dash and a colour word
at the end of the name (" - Gold"), and compares what is left. A leading colour word ("Gold Vambrace") is
not caught. Its mesh tier is above the source's own and allowed by the ladder for each
wearer, or it has none; no troop of level 30 or lower wears it, so it prices at the elite band and is never
in scope itself. On the values the restat will write (the replacement at the elite band, the source at the
band of its lowest wearer) it is at least the source on every region and above it on the primary. With each
slot drawn from any battle set no wearer passes `WORN_ARMOUR_CEILING`. No wearer ends above a troop it
upgrades into by more than the fallback (the source at the elite band) would put it: B is never worse than
A on `UPGRADE_ARMOUR_REGRESSION`. The pick is the candidate of the longest id prefix shared with the source
(its look family), then a tier inside the ladder (the lowest such tier), then the id. B pieces that find
no candidate are judged again against the whole plan until a pass adds no pick; a B piece that still has
none falls back to A, and the dry run gives the reason.

**What a clone is.** A copy of the Armory item with the id `taom_band_<lt|md|hv>_<source>`
(`rebalance_armor.band_clone_id`; the band code carries no tier token, so `mesh_tier_of`, `kingdom_key`
and `is_lord_kit` read a clone as its source), `is_merchandise="false"`, and armour and `modifier_group` at
its band by the restat's own arithmetic (`rebalance_armor.restat_armour`, which `process_file` now calls;
a generator-owned source as its generator prices it), never above the source at the elite band. It is
priced from the source as the restat leaves it (the source's values at the elite band), not from the live
values, so the clone file is the same before and after the restat apply: a heavy clone of a source raised
from the light band takes the source's new arm or leg share, a light or medium clone has none (a stat
the restat adds to the source, such as `mirkwood_body`'s leg, is 0 on those clones). Mesh,
culture, weight, material, flags and the name key and text are the source's. The Armory's own language
files translate those keys, so `tools/loc_inline_backlog.json` lists the clone file with that reason.

**Files.** `tools/generate_armour_band_clones.py` writes the plan `tools/armour_band_clones.json`
(`clones`: source to bands; `upgrades`: source to the replacement, its troops and `wrote`; `fallback`), the clones
in `Main/_Module/ModuleData/armour_band_clones/armour_band_clones.xml` (CRLF) with one `Items` node in
`Main/_Module/SubModule.xml`, and the battle sets of `troops/troops_*.xml` and
`fords_of_war/fords_troops.xml` (a `fow_` copy follows its campaign troop). A low troop's civilian sets take
the clone of its band too, never an upgrade. A troop file a generator writes whole
(`fix_melee_ladder.GENERATED_TROOP_FILES`) is not written: the realm generators (`realm_troops.py`),
`generate_lothlorien_troops.py` and `generate_snow_orc_troops.py` call `armour_for_troop` as they render,
and the dry run lists the swaps each still owes under `NEXT`. A plan made after an apply is the plan made
before it: an upgrade is read back as its source, and a clone as its source (`band_clone_source`). The
upgrade read-back is exact (review 2026-11-18). Each upgrade row records in `wrote` the slots the upgrade put
its replacement in, as `{roster troop id: {slot: [battle set index]}}` (a `fow_` copy by its own id, the
index counting the troop's battle sets that name an armour slot, as `load_troops` lists them). The next plan
reads back those slots only (`written_table`, `as_planned_from`), so a set that wears the replacement
natively keeps it when the pick changes, and two sources on one replacement need no special case.
`fix_upgrade_armour_regressions.write_changes` rewrites only the listed sets of a swap that carries
`set_indexes`. Before 2026-11-18 the read-back turned every copy of a replacement in a top wearer's sets back
into the source; it rewrote six slots of the marksmen of Rivendell and Lindon (`imladris_marksman`,
`lindon_imladris_marksman`, `fow_imladris_marksman`, battle sets 5 and 7, which held
`rivendell_torso_infantry_tier2_silver` and `_tier3_silver` natively). The shipped record was rebuilt from the
rosters before the upgrades existed (commit 7506a4c49's parent), and the first plan reproduced it. Adding or
removing a battle set of a top wearer moves the indexes after it: shift that troop's `wrote` indexes in
`tools/armour_band_clones.json` in the same edit, then `--check` must say OK. A `wrote` drift in `--check`
after such an edit means the indexes are wrong: never `--apply` over it (`--apply` writes the misread record,
and the written piece then counts as native).

**Every other wearer** (review 2026-11-17). A set outside the troop battle sets that names a source takes
the clone of its wearer's band (`scan_wearers`, `planned_for`, written by `write_worn_swaps`): an NPC by
its `level` (a guard at 16 wears the medium clone), an enlistment kit by its rank
(`generate_enlistment_rosters.RANK_BANDS`: recruit light, soldier medium, veteran heavy, sergeant elite,
so the sergeant keeps the source), and any set with no level (townsfolk, notables, wanderer and companion
kits, character creation, child templates, the faction screen's `taom_fui_*` art characters) the light
clone. A lord keeps the source: the lord files (`taom_equipment_sets_*.xml`,
`taom_lord_template_equipment.xml`) and an `occupation="Lord"` NPC. These wearers widen the clone's band
set (`wearer_bands`), so `sk_rh_loke_bracer_heavy_b` has a light, a medium and a heavy clone. The tools
that write such sets whole apply the same rule: `generate_enlistment_rosters.kit_at_rank` puts each kit at
its rank's clone (a clone is never read back as its source for a low rank), and
`promote_borrowed_cultures.py` runs `band_text` on each block it clones (`band_block`; the Arthedain and Rhudaur recruit
kits, cloned from Gondor's). `generate_char_creation_equipment.py` only appends new cultures and never
re-renders one, so it needs nothing. The validator warns `BAND_CLONE_SOURCE_WORN_LOW` for a set below the
elite band that still names a source (`sources_worn_low`, 0 after the apply).

**Retired clones.** A shipped clone id never disappears: when no set wears it any more, `--apply` keeps it in
the clone file (still not merchandise) and lists it under `retired` in the spec, because a save can hold
it. `--check` fails when a shipped id is missing.

**Who reads a clone.** `derive_armor_tiers.build_armory_index` reads the clone file after the lord
variants, in the source's folder and slot; the class table classes a clone `named`;
`fix_upgrade_armour_regressions.load_item_armour` and `taom_schema.build_item_folders` give it its source's
folder; `fix_lord_kits.lord_pool` and `fix_lord_kits._rule_b_pieces` never take one;
`fix_upgrade_armour_regressions.plan_fixes` leaves every slot the plan owns (a source, an upgrade of its
troop, a clone) and never picks one as a replacement. `generate_career_kits.index_troop_gear` counts a clone
as its source, and `career_armour` gives a Body or Leg pick that is a clone source its light clone, so a
career kit hands out the light clone; an enlistment kit takes the clone of its rank. The validator warns `TOP_TIER_ARMOUR_WORN_LOW` for
every piece still in scope (0 after the apply). Gates: `armour-band-clones` (`--check`) and
`armour-band-clone-meshes`.

**Where a clone can reach a player** (the engine lines of the variant table above):

| Route | What happens to a clone | Where |
|---|---|---|
| Shops, workshops | `is_merchandise="false"`: no shop stocks it and no workshop makes it; the class table classes it `named`, which the market gate refuses | `ItemObject.cs:501`, `WorkshopsCampaignBehavior.cs:993`; `ArmourGateService.cs:141`, `:147` |
| Tournament prizes | vanilla skips `NotMerchandise`, and TAOM's pool takes no `named` piece | `DefaultTournamentModel.cs:101`; `TournamentPrizeRules.cs:59` |
| Loot from a defeated troop | never dropped: the casualty loot roll skips a `NotMerchandise` item, and a `named` piece never drops | `MapEvent.cs:1490`, `:1502`; [armour-acquisition.md](armour-acquisition.md) |
| Armoury upgrade | a `named` piece is no upgrade source, and no row names a clone as `next` | `ArmouryUpgradeService.cs:49` to `:53` |
| Lord kits | no lord set names a clone; `fix_lord_kits.py` never picks one | `fix_lord_kits.lord_pool`, `_rule_b_pieces` |
| Enlistment kit (known route) | a recruit, soldier or veteran kit issues the clone of its rank; an honourable discharge takes the issued items back, but a desertion and an item the player equipped or sold keep it with him | `DischargeConsequenceService.ReclaimIssuedKit`; [enlistment.md](enlistment.md), "One kit rule" |
| Character creation (known route) | a starting kit that named a source (Mirkwood boots, the Arnor capes, a Rhûn bracer) now names its light clone, so the player starts in it | `taom_char_creation_equipment.xml` |
| Companions (known route) | a hired wanderer or a named companion (Elegost) wears the light clone, and the player can take it off him | `taom_wanderer_equipment.xml`, `named_companions.xml` |
| Faction screen art | `taom_fui_*` characters are never spawned | `FactionScreenArt.cs:11` |

On the player a clone moves and sells like a lord variant: a sold clone stays in the town's market until
that town's next daily tick, when the sweep removes it as a `named` piece that is not merchandise (only
while the armour gate is on).

**The class change.** The 24 A and fallback sources are `elite` in `armour_classes.xml` now; on trunk
642b1c482 16 were `heavy`, 4 `light` and 4 `medium` (`mirkwood_body`, `mirkwood_boots`,
`sk_ar_art_cape_a` and `_b`; `mkwd_inf3_greaves_bronze`, `mkwd_inf3_pauldrons_bronze`,
`rivendell_boots_greaves3`, `rivendell_pauldron_tier1_cape_silver`). With the armour gate on, the 8 former
light and medium pieces no longer drop as loot or appear as tournament prizes, and a town sells them only
at armoury level 2; the 16 former heavy pieces move from armoury level 1 to 2 (heavy already never
dropped). The 40 clones are `named`. Measured with `python tools/generate_armour_classes.py --check` and a
comparison of the table against trunk's.

**The repo half needs the Armory half.** The clone file and the roster swaps price the sources at the elite
band only after the restat apply writes the live Armory. Until then the sources keep their low live values,
and the top-tier troops' upgrades and raised sources lift them over the troops they upgrade into: on the
live Armory, `UPGRADE_ARMOUR_REGRESSION` goes from 1 (trunk) to 10 with this change. Ship the repo change
and the restat apply together (the apply sequence below, step 4 and the loop of step 6).

**Commands**, from the repo root, after a roster change or an Armory art drop:

```
python tools/generate_armour_band_clones.py                 # dry run: the plan and the table of pieces (--report PATH writes it)
python tools/generate_armour_band_clones.py --apply         # write the rosters and other sets, the clones, SubModule.xml, the spec
python tools/scan_armour_colours.py --apply                  # a new clone needs its colour row (gate armour-colours)
python tools/generate_<realm>_troops.py --apply             # each generator NEXT names, then
python tools/generate_lothlorien_troops.py --apply          #   (and generate_snow_orc_troops.py when named)
#   NEXT also names the clone file's name key count: set "keys" of
#   armour_band_clones/armour_band_clones.xml in tools/loc_inline_backlog.json to it (InlineDataKeyTests)
python tools/generate_armour_band_clones.py --check         # 0 clean, 1 drift or a step owed, 2 no Armory, 3 crash
python tools/promote_borrowed_cultures.py --only arthedain --check   # and rhudaur: their cloned kits carry band_block
python tools/derive_armor_tiers.py                           # before the restat, which reads the tracked map
python tools/rebalance_armor.py --dry-run --all --tier-source roster-first --keep-weights --keep-material-type
python tools/fix_lord_kits.py --values planned               # then --apply --values planned when it plans changes
python tools/generate_armour_classes.py --apply
python tools/derive_armor_tiers.py                           # last: the map records the lord and hero wearers as they end
```

`--apply` writes only stale files and lists them after `written:`; a step it cannot do itself is under
`NEXT:`, and `--check` fails while one is owed. It refuses (exit 1, nothing written) when a piece the spec
shipped is no longer live Armory armour, and exits 2 when no Armory slot file parses. The spec and the
clone file are pinned to LF and CRLF in `.gitattributes`, and `--check` compares them with newlines made
LF, so a checkout with `core.autocrlf` reads as clean.

The restat apply (the apply sequence below) is the maintainer's step; until it runs, the originals keep
their low prices in the live Armory, and the `armour-restat` data gate fails on the items the dry run
changes.

**Lord kits follow.** `fix_lord_kits.py` judges each lord against the elite troops of his culture. Those
troops now wear the upgrades, so the reference rises and the lord templates move up with it: the tool ran
with `--apply --values planned`, and `--check --values planned` exits 0. A piece a level 36 troop now
wears leaves the lord pool (the pool takes pieces whose lowest troop wearer is level 41 or higher), but
the lords that wore it keep it; nothing re-picks them.

**Measured on 2026-11-17, after the review fixes** (worktree on trunk 642b1c482, the live Armory read and
a copy of it restated): 101 pieces, A 20, B 77, fallback 4 (a B candidate must share the source's
`culture` attribute, so `sk_dg_khml_bracer_hplate_heavy_a` lost its Dol Guldur pick and fell back); 40
clones of 24 sources; 200 upgrade troop-piece pairs. 130 troop battle-set slots and 309 slots of other sets
(10 files) name a clone. The restat on the copy changed 34 Armory items: the 24 A and fallback sources go
up (`mirkwood_body` 0/8/0/0 to 0/48/8/19, its arm and leg from the Mirkwood line's shares); seven B replacements go
up and three go down by 1 to the elite curve value (`rivendell_vambraces_1_silver` and `_2_silver` arm 27
to 26, `whiterun_boots` leg 18 to 17). The palace guard goes from 50/21/35/3 (head/body/arm/leg) to
50/61/43/41 and Bolg's Ironfang from 31/20/10/6 to 37/46/38/16. On that copy
`generate_armour_band_clones.py --check` is OK, `rebalance_armor.py --check` 0 items,
`fix_lord_kits.py --check --values live` 0 changes, `generate_armour_classes.py --check` current, and the
validator gives `TOP_TIER_ARMOUR_WORN_LOW`, `BAND_CLONE_SOURCE_WORN_LOW` and
`ENLISTMENT_KIT_RANK_REGRESSION` 0 each.

**Known limits.** `UPGRADE_ARMOUR_REGRESSION` is 5 on the restated copy (1 on trunk, Umbar). In each edge
the B pick gives the same total as the A fallback or a lower one (traced through `pick_upgrade`); the drops
come from the rule itself, a level 36 troop wearing the elite band:

- `gundabad_tracker` to `gundabad_dread_rider_of_the_tower` (178 to 174) and `ironpass_ram_vanguard` to
  `ironpass_ram_marshal` (222 to 220): the level 41 troops wear lower pauldrons (18; 27 and 29) than the
  level 36 troops' own (19 to 24; 30). The step 6 repair loop resolves both: on a copy of the repo,
  `fix_upgrade_armour_regressions.py --apply` swapped those pauldrons and the Umbar edge, leaving the
  2 edges below.
- `rivendell_noble` and `lindon_noble` to their royal guards (214.04 to 214.00): one set of 24 wears the
  raised `rivendell_pauldron_tier1_cape_silver`, whose arm share is 13 against 12 on the guards' capes. No
  piece of 26.04 exists, so the repair loop leaves it. Claude accepted this edge under the maintainer's
  standing word to act on its recommendations; the maintainer has not reviewed it.

Until the restat apply, `rohan_bat_template_medium_e` wears `roh_nbl_arm_y` at its live 0/25/8/0 in place
of 0/37/11/0.

### Colour families (2026-11-18, #1450)

The maintainer found a Mirkwood troop in game in a silver chest with bronze pauldrons. The band clone
upgrade pick (#1440) chose a stronger piece of the same line and slot by its values only, so it put silver
pieces into bronze sets. Every armour piece now has a colour family, the upgrade pick refuses a piece that
clashes with the rest of a top wearer's set, and the validator reports troops whose sets clash.

**The rule.** gold and bronze are warm, silver is cold. silvergold, black, neutral, unknown and the cloth
colours (green, red, blue, white) clash with nothing. Two pieces of one set clash when one is warm and the
other cold (`scan_armour_colours.clashes`); gold beside bronze does not clash. A set clashes when any two
of its Head, Body, Cape, Gloves and Leg pieces do.

**Where a family comes from**, first match wins (`scan_armour_colours.build`):

1. a row of `tools/armour_colour_overrides.json`: the exact id, else the longest prefix. Every row says why.
   An override reaches the pieces derived from its row: a band clone, then a lord variant, reads the row of
   its source when it has none of its own (`override_chain`).
2. a colour word as a whole token (`_silver_`, `gold2`, never `goldenrod`) in the item id, then its mesh,
   then a diffuse texture name. `iron` is no colour word: it names the Iron Hills line of the Dwarves.
3. the measured texture. The chain is item, metamesh, materials, textures. The engine shows a male wearer
   `<mesh>_male`/`_female`, then `_slim`/`_converted`, then the plain mesh (`MissionScreen`
   `OnEquipItemsFromSpawnEquipment`, `GetMultiMeshCopyWithGenderData`); the scanner reads the male non-slim
   pick, `<mesh>_male` else `<mesh>`. The review of 2026-11-18 measured that 243 body pieces have a slim
   mesh, 3 of them with other textures (all silver), and that no female mesh differs. The diffuse texture
   is the one a material holds in slot 0, the engine's `Material.MBTextureType.DiffuseMap`
   (`audit_battle_equipment_memory.material_texture_slots`); a mesh whose materials do not decode falls back
   to the name rule (names ending `_d`, `_diffuse`, `_albedo`, `_basecolor`, `_col` or `_color`). Each image
   is found by its name in the Armory's or Native's asset folders. Each pixel is put in a class (gold,
   bronze, silver, black and others) by hue, saturation and value. A texture with gold and bronze at 35% or
   more makes the piece warm (gold or bronze, whichever the warm textures hold more of). Else a texture with
   50% or more silver whose name is not cloth, leather, mail or hair makes it silver. Else the piece is
   neutral.

The result is `tools/data/armour_colour_families.json`, generated from the live install for every Armor
item of the Armory and the TAOM module, band clones included. Never edit it by hand: fix a wrong piece in
the override file, then run `--apply`. A piece the lookup cannot find (`scan_armour_colours.family_of`, used
by the upgrade pick and the validator) reads its own row, else its band clone source's, else `unknown`. A
vanilla item has no row: on 2026-11-18, 29 vanilla ids are worn in the battle sets of `troops_dunland.xml`,
`troops_harad.xml` and `troops_umbar.xml`, and they read `unknown`, so they clash with nothing.

On 2026-11-18 `python tools/scan_armour_colours.py` read 3304 pieces and 2533 images: 1362 silver, 1111
neutral, 641 bronze, 121 gold, 44 silvergold, 11 black, 7 green, 5 red and 2 unknown (no image:
`starter_eastern_leather_boots`, `taom_chariot_armour_imperial`). 21 rows came from the override file, 168
from a colour word and 3113 from the measure.

**Known limits of the measure.** Lothlorien lines share texture atlases (`t_ll_gl_props_a_d` holds gold
trim for every line), so the measure cannot tell the Marchwarden silver pieces from the Galadhrim gold;
prefix rows (`sk_ll_mw_bracer_`, `sk_ll_mw_pauld_`, `sk_ll_rg_chest_`) fix them, read from the Kit
thumbnails on 2026-11-18. The Ranger chest reads gold from the shared Galadhrim atlas
`t_ll_gl_chest_a_d`; its own texture `t_ll_rg_chest_a_d` is 94% brown leather, so it is neutral. A measured
error is still in the table: the `sk_ll_ml_` line, which goes to step 2 (#1452), with the maintainer's eye on it.
The review of 2026-11-18 also found that a cloth or leather texture can make a piece read warm (34 rows):
those need a Kit check per piece (step 2, #1452).

**The upgrade rule** (`generate_armour_band_clones.colour_clash`, called by `pick_upgrade`). A candidate
upgrade is refused when its family clashes with a piece in any battle set of a top wearer, with the swaps
the plan already made applied. When every candidate clashes, the piece falls back to treatment A (a band
clone), and the report names the clash: `mkwd_inf3_chest_silver (silver) clashes with mirkwood_glaivesman
Cape mkwd_inf3_pauldrons bronze`. The scanner exits 2 and writes nothing when
the install has no texture image (a packaged install ships no `AssetSources`) or no numpy or Pillow. The
clone tool stops with a `CloneError` when the table is empty, or when a piece of the Armory it reads has no
row (`colour_table`; repair `python tools/scan_armour_colours.py --apply`); on 2026-11-18 no piece lacked a
row. The validator gives one `(table)` warning that it checked nothing when the table is missing or empty.
On 2026-11-18 the plan was A 20, B 74, fallback 7, 49 clones; `mkwd_inf3_chest`, `mkwd_inf3_chest_bronze` and
`mkwd_inf3_greaves` fell back to clones, and five upgrades took a piece that clashes with nothing in the set
(`mkwd_inf1_t2_helmet_silver_hc`, the two Rivendell marksman torsos and `rivendell_vambraces_1` and `_2`).

**Career kits.** A career kit is a light-band set, so the clone tool gives it the light clone of a clone
source. `generate_career_kits.py` reads a worn clone as its source and gives a Body or Leg pick that is a
clone source its light clone (`career_armour`), so `--verify` agrees with the clone tool.

**The count.** A one-off count on 2026-11-18 read every battle set of every `NPCCharacter` under
`ModuleData` (6398 sets of 5244 characters) against the committed table. No tool prints it;
`ARMOUR_COLOUR_CLASH` counts troops. 206 sets clashed before #1440 (commit 7ed5bc562's parent,
d8e7f86b3), 313 on trunk 686947589 and 200 on branch fix/armour-colour-families. A count that resolves
character templates and skips them gives 209, 306 and 203 on the same trees. No
set clashes here that did not clash before #1440, and 6 that did no longer do. `python
tools/validate_moduledata.py` reports `ARMOUR_COLOUR_CLASH` 41 (one warning per troop of `troops_*.xml`, at
its first clashing set). The 200 sets belong to 51 characters; the other 10 are `fow_` copies in
`fords_troops.xml`, which the validator does not read.

**Step 2** (#1452) is the clashes that predate #1440: culture by culture, with the maintainer's eye on each
measured piece, by swaps in the troop files or rows in the override file.

**Commands:**

```
python tools/scan_armour_colours.py            # dry run: counts and the rows that would change
python tools/scan_armour_colours.py --apply    # write the table
python tools/scan_armour_colours.py --check    # exit 1 when the table is stale (gate armour-colours)
```

Exit codes: 0 clean or written; 1 stale (`--check`) or a bad override row; 2 nothing could be measured (no
Armory under the install, no texture image, no numpy or Pillow: the gate reads it as NOT RUN); 3 a crash.
The install is `$BANNERLORD_GAME_MODULES`, else the Modules folder of `$BANNERLORD_GAME_DIR`.

After an Armory art drop or a new band clone, run `--apply`, then `generate_armour_band_clones.py --check`.
After `generate_armour_band_clones.py --apply`, run `scan_armour_colours.py --apply` (a new clone needs its
row), then `--check` of the clone tool.

### Open decisions for the maintainer (2026-11-14, #1271)

1. **The loot table of heavy, elite and lord extremities: decided, `leather`.** Bracers, greaves and
   pauldrons from the heavy band up rolled on `chain`, whose down-rolls are Loose (-6) and Rusty (-8),
   flat on any stat above 0: a heavy Gondor pauldron of 9 could drop to 1 and a heavy bracer of 16 to
   8. They now roll on the vanilla `leather` group (`_EXTREMITY_MODIFIER_GROUPS`; light and medium keep
   `cloth_unarmoured`). Same five loot weights as `chain` (102 in all, 40 on the two down-rolls, no
   modifier is 45), so the odds are unchanged and only the size changes: Battered -3 and Worn -2 below,
   Waxed +3, Fine +5 and Legendary +7 above. The kingdom gate reads the loot table of the LOW side of a
   judged pair (heavy over light, elite over medium), which stays `cloth_unarmoured`, so it keeps its 0
   violations. Heavy and up are never the low side of a pair there, but that holds for the kingdom gate
   only. `check_curve_invariant` also judges (lord, heavy) and so reads the heavy top roll: it reports 0
   violations at `leather` (+7) and at `chain` (+9) and 16 at `plate` (+12). `_check_tier_inversions` in
   `tools/analyze_armor_balance.py` reads each item's own live group, so it follows the write, not the
   table. No new group was needed: a mod can define one (an `ItemModifier` names its group, so the
   modifiers must be new ones too), but `leather` already meets the target, needs no new text (the names
   are vanilla keys) and no `SubModule.xml` node.

   **The group also decides what a troop spawns with.** `Mission.DecideAgentSpawnEquipment` builds the
   equipment of an agent that has no fixed equipment with
   `Equipment.GetRandomEquipmentElements(agentBuildData.AgentCharacter, !Game.Current.GameType.IsCoreOnlyGameMode, ...)`
   (`TaleWorlds.MountAndBlade.Mission.cs:4108`, installed v1.5.7), and for each randomized piece
   `GetRandomizedEquipment` sets the modifier from
   `equipmentFromSlot.Item?.ItemComponent?.ItemModifierGroup?.GetRandomItemModifierLootScoreBased()`
   when that flag is true (`TaleWorlds.Core.Equipment.cs:609`). `GameType.IsCoreOnlyGameMode` is `false`
   (`TaleWorlds.Core.GameType.cs:36`) and only `CustomGame` overrides it to `true`
   (`TaleWorlds.MountAndBlade.CustomBattle.CustomGame.cs:23`). Heroes are set to fixed equipment before
   that call (`SpawnAgent`, `Mission.cs:4284-4286`). So in a campaign battle every heavy or elite
   troop's bracers, greaves and pauldron roll on this group at spawn, and a Custom Battle does not roll.
   Per piece, over the 102 loot weights, the mean roll moves from -2.08 on `chain` (-212 over 102) to
   -0.35 on `leather` (-36 over 102), before the floor of 1, and the worst roll, one spawn in four (25 of
   102), from -8 to -3.

   Worst and best roll before and after (`max(1, base + delta)`, the engine's `ModifyArmor`):

   | Piece | Base | `chain` | `leather` |
   |---|---|---|---|
   | Heavy pauldron, Gondor | 9 | 1 to 18 | 6 to 16 |
   | Heavy pauldron, the lowest, 7 (Rohan, Dunland, Thenn, the orc lines and the Mordor Uruk line) | 7 | 1 to 16 | 4 to 14 |
   | Heavy bracer, Gondor | 16 | 8 to 25 | 13 to 23 |
   | Elite greave, Gondor | 21 | 13 to 30 | 18 to 28 |
   | Elite greave, Erebor | 23 | 15 to 32 | 20 to 30 |
   | Lord pauldron, bracer, greave, Erebor | 16, 28, 25 | 8 to 25, 20 to 37, 17 to 34 | 13 to 23, 25 to 35, 22 to 32 |
   | Lord pauldron, bracer, greave, Rohan | 11, 20, 18 | 3 to 20, 12 to 29, 10 to 27 | 8 to 18, 17 to 27, 15 to 25 |

   Across the 25 kingdom caps and the three extremity slots (75 heavy values), a Rusty roll on `chain`
   lands on 1 in 21 and a Battered roll on `leather` in none. That counts the curve primaries only, and
   the lowest is 4. A pauldron can also hold an `arm_armor` secondary of 4 or less (28 shoulder pieces on
   `leather` in the dry run of 2026-11-15), and a Battered roll still takes those to 1. A heavy piece at
   its best roll beat the elite base in 73 cases on `chain` and in 50 on `leather`, and the lord base in
   60 and in 29. The adjacent band is allowed to be beaten; the kingdom gate does not judge the lord
   over heavy pair (its comment calls that gap adjacent), so the 29 cases against the lord base are not
   gated. The one name that reads oddly on plate is Waxed (10 of 102).

   **Rolls already made stay.** A save keeps the modifiers its pieces rolled before, and the armoury
   upgrade carries a piece's modifier onto the new piece (`ArmouryUpgradeService.cs:100`), so "drops to
   6 at worst" holds for new rolls only.

   **What the write reaches.** The dry run of 2026-11-15 plans 814 items (409 elite, 321 heavy and 84
   lord; 364 pauldrons, 254 bracers and 196 greaves), and only their `modifier_group` changes: 755 from
   `chain` and 59 from `plate`, to `leather`. Armour, weight and `material_type` do not move. 720 of
   them are the heavy and elite pieces the troop curve restates. The other 94 (the 84 lord and 10 elite)
   are hero-only kit and unworn merchandise without a keyword, which the clamp lowers and never
   restates: for an arm, leg or shoulder piece that is still on `chain` or `plate` the clamp now writes
   the table of its band too (lord for hero-only kit, elite for the unworn clamp), so a defeated
   lord's bracer no longer drops with a -6 or -8 roll. A clamped piece on `cloth`, `cloth_unarmoured` or
   `leather` is left alone, because a clamp must not raise a loot table, and head and body pieces keep
   theirs. Two kinds of piece are not in the write:
   - Generator-owned pieces. The restat skips them, and they move with the two generators' `--apply`:
     27 Lothlorien items (8 bracers, 7 greaves, 12 pauldrons) and 4 Snow Orc pauldrons change their
     `modifier_group` in the render of 2026-11-15.
   - The troll bracer, skipped by design: troll kit stays on the legacy curve, and it is the one
     arm, leg or shoulder piece still on `chain` or `plate` after the write.

   To apply, write the live Armory only after this change is merged to trunk, from a checkout that
   holds the merged change: every checkout shares the one Armory, so until the merge trunk's restat
   and both generators put `chain` back and trunk's `lorien-armor` and `snow-orc-armor` gates read
   FAIL. Then, in this order: the restat with a backup tag
   (`python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag <tag>`),
   then `python tools/generate_lothlorien_armor.py --apply`, then
   `python tools/generate_snow_orc_armor.py --apply`. Then `python tools/generate_lothlorien_armor.py --check`,
   `python tools/generate_snow_orc_armor.py --check` and `python tools/rebalance_armor.py --check`
   must each exit 0. The in-game check of the loot names is owed, in a campaign battle.
2. **The seven hero-only items on a weaker line than their wearer** (previous section): route them
   or accept the lower cap.

### The apply sequence (2026-11-14, #1271)

This sequence ran on 2026-11-14, with three exceptions and the results listed under "Applied on
2026-11-14" after step 18; it stays as the procedure to repeat after an engine or art change.
**LIVE ARMORY** marks a command that writes the unversioned install
(`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory`); **REPO** marks one
that writes only this checkout. Every command runs from the repo root, and every writer is run as a
dry run, report or `--check` first. The `lotraom-assets` mirror (`E:/repos/lotraom-assets/v1.5`, not
`v1.4`) is not part of this sequence: the maintainer syncs it. Before step 1, land and test the repo
(`python -m pytest tools/tests -q -p no:cacheprovider`, `python tools/lint_docs.py --fail-on-drift`) and
close the game, the launcher and the dedicated server.

1. **LIVE ARMORY, backup.** `cp -r "/e/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory" /e/Backups/LOTRLOME_Armory-20261114`.
   The armour writer's `.bak-<tag>` files cover only its own XML; this copy covers every step below.
2. **REPO.** `python tools/derive_armor_tiers.py --stdout`. It has no dry run: it rewrites the tracked
   `tools/data/armor_roster_tiers.json` (and the ignored `tools/reports/armor-balance/ROSTER-TIERS.md`)
   from the rosters and the live Armory, and writes nothing in the Armory.
3. **REPO, read only.** `python tools/rebalance_armor.py --dry-run --all --tier-source roster-first --keep-weights --keep-material-type`.
   Expect `Changed: 2739` of 2,945 items (the figures of 2026-11-14; a roster change moves them).
4. **LIVE ARMORY.** `python tools/rebalance_armor.py --apply --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag kcurve-20261114`.
   `--backup-tag` is required with `--apply` (the script exits 2 without it). Never pass
   `--no-lower-armor` (it would hold the light and medium pieces at their old values) or
   `--keep-materials` (it freezes the loot tables). Expect `Changes written to 83 files`; the same
   command again must write 0.
5. **LIVE ARMORY.** `python tools/generate_lothlorien_armor.py`, then `--apply`, then `--check`; the
   same three runs for `python tools/generate_snow_orc_armor.py`. Their 97 items belong to the
   generators and take the new secondaries from `calculate_stats`; the restat leaves them alone, so
   their loot tables move only with this step (2026-11-15: 27 Lothlorien and 4 Snow Orc extremity
   pieces go from `chain` to `leather`; run steps 4 and 5 from a checkout that has this change on
   trunk, because the Armory is shared and trunk's restat and generators would put `chain` back).
   After steps 4 and 5 the three checks must exit 0:
   `python tools/rebalance_armor.py --check` (the same plan as step 3 with the step 4 flags fixed; it
   prints the count and the first twenty differences, exits 1 when the plan is not empty and 2 when the
   Armory is not installed, no armour file was read, or a `--cultures` or `--slots` value matches
   nothing; `tools/data_gates.py` runs it as `armour-restat`),
   `python tools/generate_lothlorien_armor.py --check` and `python tools/generate_snow_orc_armor.py --check`.
6. **REPO, the repair loop.** Item armour is read from the install, so this runs after steps 4 and 5.
   `python tools/fix_upgrade_armour_regressions.py` reports the upgrade edges that now lower armour
   (14 on a copy after steps 4 and 5, against 4 today; for example `imladris_nobleman` head 53 to 20),
   and `python tools/fix_upgrade_armour_regressions.py --apply` rewrites the troop rosters. The
   2026-09-13 curve apply needed the same loop (58 edges, 197 slot swaps). The fixer never touches a
   slot the armour band clone plan owns (`band_clone_plan`): after it, run
   `python tools/generate_armour_band_clones.py --check`, and on exit 1
   `python tools/generate_armour_band_clones.py --apply` and the steps its `NEXT:` names (the
   "Commands" of "Armour band clones and upgrades" above), because a repair can put a level 36 troop
   and a level 30 one on the same piece again. The rosters changed, so go back to step 2, then step 3,
   and repeat steps 2 to 7 until step 3 plans `Changed: 0`, the report finds no edge but the known
   limits of the clone section, and the clone `--check` exits 0 (when step 3 still plans changes,
   step 4 again with the same `--backup-tag`, which keeps the first backup).
7. **REPO, carry the step 6 swaps into the troop generators.** A generator owns some troop files, and
   a roster repair edits them by hand: the generator's next run reverts the swap, and its sync test
   fails now. The owners are the realm files in `generate_new_faction_kingdoms.REGENERATABLE`
   (arthedain, cardolan, rhudaur, eredluin, anduinvale, angaladh, dorwinion and southrhovanion, each
   `tools/generate_<realm>_troops.py`), `troops_lothlorien.xml` (`generate_lothlorien_troops.py`), and
   two that have no `--check` and no test, `troops_dale.xml` (`generate_dale_troops.py`) and
   `troops_gundabad_snow.xml` (`generate_snow_orc_troops.py`). `generate_gondor_troops.py`,
   `generate_rhun_troops.py` and the `apply_*_troop_revamp.py` scripts are historical scaffolds, not
   live generators ([melee-damage-model.md](./melee-damage-model.md), "What the generators needed"):
   leave them alone.
   1. List the swapped troops that a generator owns. `git diff --stat -- Main/_Module/ModuleData/troops/`
      names the files the repair changed. For a realm file, `python tools/generate_<realm>_troops.py --check`
      exits 1 and names what differs. For Lorien, Dale and Snow Orc, search the generator source for
      the swapped troop ids.
   2. Carry each swap into the generator's table. An in-memory run of steps 4 to 6 changed
      `cardolan_levy`'s two body alternatives from `sk_gd_anf_inf_chainmail_a` and `_b` to
      `gondor_noble_jerkin_b`, which `LIGHT_FOOT` in `tools/generate_cardolan_troops.py` now names.
   3. Regenerate: `python tools/generate_<realm>_troops.py --apply`, then `--check` again. Lorien:
      `python tools/generate_lothlorien_troops.py --apply`, which carries the placed skills over; it has
      no `--check`, and `test_generated_files.py` in the next step checks it.
   4. Run `python -m pytest tools/tests/test_generator_regions_in_sync.py tools/tests/test_generated_files.py -q -p no:cacheprovider`.
      Both must pass before step 8.
   5. The armour band clones: `python tools/generate_armour_band_clones.py --check` must exit 0 after
      the regeneration. Its swaps reach the same generators through `armour_for_troop`, so a realm
      generator's `--apply` keeps them, and the dry run lists any it still owes under `NEXT:`.
8. **REPO.** `python tools/analyze_armor_balance.py --stdout` (0 kingdom invariant violations) and
   `python tools/analyze_kingdom_armour.py --stdout`. Then `python tools/generate_armour_classes.py --apply`
   and `python tools/generate_armour_classes.py --check`: it classes pieces with `calculate_stats`
   and the derived map, so the committed `armour_classes.xml` is stale from the moment the curve
   changes.
9. **LIVE ARMORY, then REPO, then LIVE ARMORY.** The melee ladder, in this order. `python tools/restat_melee_blades.py`
   and `--apply` (the Armory's `LOTRLOME_crafting_pieces.xml`); `python tools/fix_melee_ladder.py` and
   `--apply` (REPO: troop rosters); then `python tools/restat_melee_blades.py` as a dry run again,
   which must report 0 blades to move. If it does not, repeat this step.
10. **REPO, carry the step 9 swaps into the troop generators.** Since 2026-11-16 `fix_melee_ladder.py`
    reports a weapon in a troop file a generator writes (`GENERATED_TROOP_FILES`) and does not swap it.
    Change such a weapon in its generator, then repeat step 7, parts 1 to 4, for those troops.
11. **Career, starter and enlistment kit.**
    - REPO: `python tools/generate_career_kits.py`, `--apply`, `--verify`.
    - LIVE ARMORY: `python tools/generate_starter_armor.py`, read the dry run, then `--apply`. Its 12
      `starter_armors.xml` files get a recruit's armour (a chest of 6 to 8, greaves of 3 to 4, no arm
      armour), but the live `harad/starter_armors.xml` carries hand fixes (weights, appearance, modifier
      group) that the generator overwrites: diff each folder against its `.bak-startergear` and carry
      the hand fixes back. The write parses the XML first and keeps the replaced file's BOM and line
      endings. Gondor is not generated. Its hand-authored `gondor/starter_armors.xml` still
      reads 9 and 4 on the infantry chest and 9 on the greaves: edit it by hand to 7 with arm 0 and
      greaves 3 (cavalry 7, 0, 3; ranged 5, 0, 3).
    - LIVE ARMORY: `python tools/generate_starter_kit.py`, `--apply`, `--verify` (no `--asset-repo`, so
      the mirror is not written).
    - REPO: `python tools/wire_starter_kit_rosters.py`, and `--apply` only on drift.
    - REPO: `python tools/generate_enlistment_rosters.py --repair-ranks`, read the plan, then `--apply`; then `python tools/promote_borrowed_cultures.py --only <c> --check` for each promoted culture and `--apply` where only the enlistment file drifts, until `ENLISTMENT_KIT_RANK_REGRESSION` lists nothing new ([enlistment.md](./enlistment.md#a-ranks-kit-is-never-weaker-than-the-kit-before-it-1290); a full generator `--apply` refuses the file, because it would drop the promoter's regions).
12. **REPO.** `python tools/generate_placeholder_weapons.py`, `--apply`, `--verify`. It runs after
    step 9: its scale factor is relative to the donor blade, and the restat moves the donor. It
    writes `placeholder_weapons/*`, `SubModule.xml` once and the troop rosters, and reads the Armory.
13. **REPO, carry the step 12 changes into the troop generators.** The placeholder run rewrote troop
    rosters. Repeat step 7, parts 1 to 4, for the troops it changed.
14. **Ranged ladders.**
    - LIVE ARMORY: `python tools/generate_ranged_ladder_items.py`, `--apply`, `--verify`. The mirror is
      written only when `--asset-repo` is passed, so leave it off.
    - LIVE ARMORY: `python tools/restat_ranged_donors.py --asset-repo E:/Temp/no-mirror`, then the same
      with `--apply`, then with `--verify`. Without `--asset-repo` this tool writes the
      `E:/repos/lotraom-assets/v1.5/LOTRLOME_Armory` mirror as well (its default); a path that does not
      exist prints one warning and leaves the live Armory as the only tree.
    - REPO: `python tools/rebalance_ranged_ladders.py`, then `--apply` (troop rosters and
      `docs/reference/ranged-troops.html`).
    - REPO and LIVE ARMORY: `python tools/sync_ranged_ladder_translations.py --asset-repo E:/Temp/no-mirror`
      and `--apply` only if item ids changed (it writes the tracked translation cache and the Armory's
      `loc_*.xml`).
15. **Gates, all green.** `python tools/data_gates.py`, `python tools/validate_moduledata.py`, `python tools/validate_xml_schemas.py`,
    `python tools/lint_docs.py --fail-on-drift`, `python -m pytest tools/tests -q -p no:cacheprovider`
    and `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`. The Python run includes the two
    generator sync tests of step 7: a failure there after steps 11 and 14 (they also write troop
    rosters) means a generator-owned troop changed, so repeat step 7 for it. `MELEE_BLOW_CEILING` is
    the blow check against the tier limits (`analyze_melee_ladder.py` has none). In the C# suite,
    `MouthOfSauronHelmDataTests` pins `head_armor` 50 on the live Armory; the plan leaves that helm at
    50 (the lord helmet cap on its line is 51), so the pin changes only if step 3 of the final run
    plans a different value for `sk_mordor_mouth_of_sauron_helm`. If it does, change the pin in the
    commit of step 16.
16. **The second commit (REPO).** Stage explicit paths: `tools/data/armor_roster_tiers.json`,
    `Main/_Module/ModuleData/armour_acquisition/armour_classes.xml`,
    `Main/_Module/ModuleData/troops/troops_*.xml`, the generator sources that steps 7, 10 and 13
    changed (`tools/generate_*_troops.py`, with the `taom_partyTemplates.xml` region a realm generator
    writes), `Main/_Module/ModuleData/placeholder_weapons/*`
    and `Main/_Module/SubModule.xml` (if the placeholder run added its nodes),
    `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml`,
    `taom_char_creation_equipment.xml`, `taom_player_start_vanilla_override.xml` and
    `taom_enlistment_equipment.xml`, `docs/reference/ranged-troops.html`, and
    `tools/translation_cache/*` if step 14 changed it. **The severity flip waits** (decided at the
    apply of 2026-11-14): the validator reads the Armory installed on each machine, so an ERROR would
    block every commit of a contributor who still has the old Armory. Flip it in its own commit once
    the rewritten Armory has reached every contributor and the mirror. To make `WORN_ARMOUR_CEILING`
    an error then: set the constant `WORN_ARMOUR_CEILING_SEVERITY` in `tools/taom_schema.py` to
    `Severity.ERROR` (the ceiling and its `(tooling)` issue both read it), flip the pin in
    `test_the_ceiling_and_its_tooling_failure_share_one_severity_constant`
    (`tools/tests/test_validate_moduledata.py`) to match, and add `--code WORN_ARMOUR_CEILING` to
    both hook lists, `.claude/hooks/check-moduledata-validation.sh` and `.githooks/pre-commit`
    (`CommitGateCoverageTests` fails until the first names the code, and `CodeList` in
    `tools/tests/test_githooks_pre_commit.py` fails until the second matches the first), then update its row in
    [moduledata-validator-codes.md](../reference/moduledata-validator-codes.md) and drop the two
    TRANSITIONAL comments (on the constant and in `Validator._worn_armour_limits`).
17. **The release note** (the commit body) says that the Armory package must ship with this release
    and that co-op hosts, co-op clients and the dedicated server must all carry the same Armory:
    armour values come from the item XML on each machine, so a mixed set disagrees about every hit.
    Keep the E: backup of step 1 until the in-game checks pass.
18. **The in-game checks** below are owed (label `triage-needs-ingame`).

#### Applied on 2026-11-14

Steps 1 to 15 ran against the live Armory on 2026-11-14, except the items under "Not run". Steps 16
and 17 (the second commit and its release note) are not done by an apply: the commit and the release
steps follow. The generator band fix is applied too (last paragraph).

- Step 4, the armour restat: 2,739 items written in 83 files, plus 2 more files in the repair loop;
  the dry run after it plans 0.
- Step 5, the Lothlorien and Snow Orc generators, applied, then re-run with the generator band fix
  (last paragraph).
- Step 6, `fix_upgrade_armour_regressions.py --apply`: 154 equipment lines changed across 18 troops in 7
  troop files when it ran. After the Umbar file was restored (see "Not run"), the net diff
  (`git diff --numstat` on `Main/_Module/ModuleData/troops`) is 146 changed equipment lines across 17
  troops in 6 files; 0 upgrade edges left that lower armour.
- Step 8, `armour_classes.xml` regenerated.
- Step 9, `restat_melee_blades.py --apply`: 203 blade pieces; a second run plans 0.
- Steps 11, 12 and 14: the career kits, the starter kit, the placeholder weapons and 147 ranged ladder
  items.
- The validator after the write (`python tools/validate_moduledata.py`): 0 errors, `WORN_ARMOUR_CEILING`
  0, `UPGRADE_ARMOUR_REGRESSION` 0, `ARMOUR_CLASS_TABLE_DRIFT` 0, `MELEE_BLOW_CEILING` 8 (the blades that
  the one handed and two handed Erebor axes share), `WORN_ARMOUR_FLOOR` 196 (183 after the generator band
  fix) and `MELEE_LADDER_INVERSION` 318.

Not run, and why:

- `fix_melee_ladder.py` (step 9). Its plan replaced poleaxes with short spears, which moves a troop to
  another weapon category; see "A troop keeps its weapon category" in
  [melee-damage-model.md](./melee-damage-model.md). The tool holds the category since 2026-11-16, and
  its roster pass was applied that day (the counts are in that section).
- `generate_starter_armor.py` (step 11). It overwrites the hand fixes in `harad/starter_armors.xml`,
  and Gondor's starter file needs a hand edit first. The starter armour literals are still the old
  ones.
- `generate_enlistment_rosters.py --apply` (step 11). A full run rewrites the `enlist_<culture>_*`
  rosters that `promote_borrowed_cultures.py` owns for the eight promoted cultures, and their sync
  checks then fail, so the run was reverted. The enlistment kits keep their old items: 14 ranks now
  give a weaker kit than the rank below. They need a reseed per culture (`--culture` with
  `--reseed`), then the promoted cultures regenerated.
- Also not applied: the repair of the Umbar edge `umbar_elite_root000` to `umbar_elite_root0000`.
  The parent wears uncurved vanilla Aserai armour, and the repair would hand the same pieces to the
  child, above the Umbar curve. One `UPGRADE_ARMOUR_REGRESSION` warning stays for it.
- The `WORN_ARMOUR_CEILING` error flip (step 16). The validator reads the Armory installed on each
  machine, so an error would block the commits of a contributor who still has the old Armory.

Backup. `E:/Backups/LOTRLOME_Armory-ModuleData-pre-rebalance-20261114` holds the whole `ModuleData` of
the Armory from before the write (step 1 planned a full module copy; this one is the data only). The
tools also left 102 `*.bak-*` files beside the live XML files (counted with `find` in the live Armory
`ModuleData` on 2026-11-15). The engine does not load them, and they
must be swept before a mirror sync or a release package: `pwsh tools/sweep_module_backups.ps1` is the
dry run, and `pwsh tools/sweep_module_backups.ps1 -Apply` moves them into a dated quarantine with a
SHA256 manifest.

Swept and mirrored on 2026-11-15 (maintainer's word). The sweep moved 164 sidecars, all from the
Armory's `ModuleData`, into `E:\Bannerlord_Backups\module_bak_sweep_2026-11-15`. The `lotraom-assets`
mirror then took the live data in commit `2088acbc2` (pushed): 181 `ModuleData` files and
`SubModule.xml` under `v1.5/LOTRLOME_Armory`, each newer in the install than its last mirror commit.
119 of them carry the rebalance, and 62 were older live edits the mirror did not hold (`action_sets.xml`,
`skins.xml`, five language files in each of the 12 languages). `SubModule.xml` went from v2.0.36 to
v2.0.40 there. Not mirrored: the eight scene shader caches and the shader compile log that differ,
`RuntimeDataCache`, and nothing was deleted from the mirror.

Generator band fix, applied to the live Armory. The first generator run priced
each of the 97 generator-owned pieces by the tier word in its id, so 47 of the 79 worn pieces sat below
their wearers' band: `sk_ll_rg_helmet_med_b` was priced medium (17) while its lowest wearer is level
31 (elite, 49). Both generators now price a worn piece at the band of its lowest wearer, as a restat
does, and an unworn piece by its id word; the Mallorn kit stays one band above either, and the
appearance stays with the id word. Both generators' `--check` pass on the live Armory and
`armour_classes.xml` is regenerated (one row, `sk_ll_ml_pauld_med_a`, changed its `next`). The apply
changed 55 items (50 Lothlorien, 5 Snow Orc), no unworn piece changed, `WORN_ARMOUR_FLOOR` went from
196 to 183 and `WORN_ARMOUR_CEILING` stays 0. No troop passes a ceiling.

The Mallorn exception to the lord band: a worn Mallorn piece stays one band above its wearers' band,
by design. Three level 46 Lothlorien troops, `lorien_gl_highblade`, `lorien_gl_mallorn_defender` and
`lorien_gl_bowmaster`, wear lord-band values (chest 50 against the Lothlorien cap of 47). Each sums to
52 head, 64 body, 37 arm and 43 leg, inside every worn limit.

### The two-tier invariant (modifier-aware curve, 2026-07-31)

The curve above describes **base** stats, but the player never only sees base stats. Bannerlord item
modifiers add a **flat** armor bonus — `legendary_plate +12`, `chain +9`, `leather +7`, `cloth +5`,
`cloth_unarmoured +3` — and `EquipmentElement.GetModifiedBodyArmor()` / `GetModifiedArmArmor()` apply
it **independently to every nonzero stat**, each guarded by `num > 0`. A two-stat cape therefore takes
the bonus twice, and a stat of exactly `0` is **modifier-immune**.

The old shoulder curve spanned 16 points across all six tiers — less than a single `legendary_plate`
roll — so a legendary tier-2 pauldron (9/9 → 21/21) out-armored a plain tier-6 one (20/14). The curve
now satisfies, for every slot, governed stat, tier pair `(n, n-2)` and cultural protection value:

```
base[n] > base[n-2] + legendary(modifier_group[n-2]) + VARIANT_CAP
```

Beating the **adjacent** tier on a great roll is intended loot excitement and is never flagged;
beating one **two or more** tiers up is the defect.

Three constants enforce it, all in `rebalance_armor.py`:

| Constant | Purpose |
|---|---|
| `LEGENDARY_ARMOR` | The native ladder, mirrored from `Native/ModuleData/item_modifiers.xml`. Pinned against the live install by a test, so an engine bump can't move it silently. |
| `SLOT_MODIFIER_GROUPS` | Per-slot loot-roll magnitude. Capes, bracers and greaves roll `cloth_unarmoured` (+3) up to the medium band and `leather` (+7, down-rolls -2 and -3) from heavy, instead of `plate` (+12, down-rolls -4 and -6): the native deltas are sized for chest-scale bases (30-60), not 7 to 28 (the heavy to lord curve primaries). **`material_type` is deliberately NOT changed**: it drives hit sounds/FX and stays lore-correct; the engine reads the two attributes independently. |
| `VARIANT_CAP` | `extract_variant_number()` maps roman numerals to `+0..+17` straight into the stat. Uncapped it eats the invariant margin, which runs as thin as +1. |

`GOVERNED_STATS` replaces the single-stat view: shoulder now governs **both** `body_armor` and
`arm_armor`. `_get_primary_stat` still returns one scalar (14 call sites depend on it) but reads the
head of that table; use `governed_stats()` for any balance judgment. Shoulder `arm_armor` being
invisible to the primary lookup is exactly the blind spot that let the inversion ship.

**Regression guards:** `analyze_armor_balance.check_curve_invariant()` is a pure function of the
constants (reads no XML), runs before the per-culture loop, and exits non-zero on violation.
`_check_tier_inversions()` reports the *item-level* version against the live tree, preferring roster
tiers over the keyword guess and splitting severity (roster-backed = ERROR, keyword-tiered = WARN).
`tools/tests/test_armor_curve_invariant.py` pins the whole thing, including the four per-culture
generators, which carry their own copies of the tier table and do **not** import the curve.

### Tier assignment: roster-derived, not keyword-guessed (the Phase-2 principle)

`rebalance_armor.detect_tier` currently **guesses** an item's tier from name keywords + a value-threshold fallback. This is brittle: a Dale `Archer Helmet A01` (15 armor) and `A03` (24 armor) both lack a tier keyword, so both fall to "light" and a blanket apply would flatten them to one value — destroying the progression.

The authoritative signal is the other direction: **an item's intended tier = the level of the troop that wears it** × which weight-line (light `a` / heavy `b`) the troop sits on. A level-6 troop's chest is "light" by definition; a level-31 troop's is "elite". Because **items are deliberately reused across troops** (not enough meshes for every soldier), a shared item anchors its tier to its **primary/lowest** wearer so it never over-arms the lower troops.

This roster walk is implemented in **`tools/derive_armor_tiers.py`** (Phase 2). It joins every `troops_*.xml` roster (equipment slot → item id, via `Item.<id>`; slots map `Head/Body/Leg/Gloves→arm/Cape→shoulder`) to the live armory by item id, anchors each item to its lowest wearer, and writes the map to `tools/data/armor_roster_tiers.json` + a report at `tools/reports/armor-balance/ROSTER-TIERS.md`.

Tier-signal precedence per item, since #583 (2026-09-13): (1) the roster anchor band, the lowest BATTLE-set troop wearer's level, ladder-exempt troops never anchoring; (2) an explicit tier keyword in the id (`_light_`/`_med_`/`_heavy_`/`_elite_`/`_lord_`) for kit no troop wears, with `_civ`/`civilian` ids civilian whoever wears them; (3) unworn and keyword-less, no tier. Until 2026-09-13 the keyword came first, which is how the Fountain Guard's `_heavy_` helmet, worn only at level 46, stayed on the heavy row, and why the map mislabelled 682 of 1,861 worn items against what `--tier-source roster-first` writes.

Level→tier bands (calibrated on Dale ground truth + the owner's elite=31-51 decision): `≤13 light · 14-18 medium · 19-30 heavy · 31-51 elite`. The armor `lord` tier is hero-only (named lords/heroes, excluded from rosters) — no troop is roster-tiered `lord`.

Reading the map: **UNDER** = the item is weaker than its wearer's level implies (an under-progressed line — the primary actionable signal). **OVER** is often *intended* — a heavy `b`-line item worn by a heavy troop at its level, or a deliberately-strong culture (rivendell); the report's `Line` column + the culture's identity disambiguate, so OVER is reviewed, not auto-applied. The map computes the level-band target as a **reference** — whether to scale an under-progressed line up or accept it (e.g. Dale's flat `a`-line) is a Phase-3 design call.

## Configuration

| Knob | Where | Notes |
|------|-------|-------|
| Baseline curve | `SLOT_BASELINES` in `tools/rebalance_armor.py` | per-tier × per-slot primary + weight |
| Cultural identity | `CULTURAL_MODS` in `tools/rebalance_armor.py` | per-culture protection + weight_mult |
| Live armory path | `_default_armory_dir()` | `$BANNERLORD_GAME_DIR` override → Steam fallback → `--armory-path` |
| Apply scope | `PRESERVE_CULTURES` + `--cultures` / `--all` | blanket `--apply` is refused; see below |
| Hero/boss exclusion | `HERO_NAMES` + `EXCLUDE_ID_SUBSTRINGS` (analyzer) | excluded from all curve judgments; the writer lowers hero-only kit to the lord band and restates kit a troop wears (2026-11-14) |

### Apply-safety guard

`rebalance_armor.py --apply` **refuses a blanket run.** It requires either `--cultures a,b,c` (scoped) or an explicit `--all`, and since 2026-11-14 a `--backup-tag <tag>` as well (it exits 2 with a message before it reads the Armory, because the live Armory is unversioned and the one-time `<file>.bak-<tag>` copy is the only way back). The final line counts the files really written, not the files analysed. This protects the hand-authored cultures (`PRESERVE_CULTURES` = gondor, mordor, isengard, dol_guldur, gundabad, erebor, iron_hills) from being flattened by the keyword-tier guesser. Even when fixing a preserved culture's genuine bug (e.g. iron_hills arm slot), scope the apply to that culture.

## Workflow

```
1. python tools/analyze_armor_balance.py --stdout      # structural defects (monolithic/integrity/ceiling)
2. python tools/derive_armor_tiers.py --stdout         # roster-derived tiers: per-item under/over re-stat list
3. (open REPORT.html + ROSTER-TIERS.md)                # defects + the re-stat candidates
4. python tools/rebalance_armor.py --dry-run --cultures <c>   # preview a scoped re-stat
5. python tools/rebalance_armor.py --apply --cultures <c> --backup-tag <tag>   # write (scoped, tagged; never blanket)
   #   --materials-only  writes ONLY material_type + modifier_group (no armor, no weight);
   #                     safe with --all, skips hero kit. Run it before any curve pass.
   #   --keep-materials  freezes them during a full re-stat (this used to be implied by
   #                     --no-lower-armor, which is how ~1000 items kept an unearned `plate`).
6. python tools/analyze_armor_balance.py               # re-run: confirm the defect cleared
7. python tools/validate_all_troop_refs.py             # underwear-bug gate (no broken refs)
```

Never run `--apply` without first reading the analyzer report and a dry-run.

## Key Files

| File | Purpose |
|------|---------|
| `tools/rebalance_armor.py` | WRITES. Curve source of truth; repointed at live armory 2026-06-30; scope-guarded. |
| `tools/analyze_armor_balance.py` | READS. Per-culture HTML/MD/JSON report; monolithic + integrity + ceiling detection. |
| `tools/derive_armor_tiers.py` | READS. Roster→item→tier join (Phase 2); reuse-anchored; under/over re-stat candidates. |
| `tools/data/armor_roster_tiers.json` | The derived map (item → wearers → anchor → tier → current vs target). |
| `tools/reports/armor-balance/{REPORT,ROSTER-TIERS}.{html,md}` | The viewable reports (regenerated each run). |
| `tools/analyze_kingdom_armour.py` | READS. Cross-culture overview by engine tier (#581): matrices, inversions, ceilings, curve view, gate preview. Reports to `tools/reports/kingdom-armour/`. |
| `tools/taom_schema.py` `cross_culture_armour_inversions` | The `CROSS_CULTURE_ARMOUR_INVERSION` rule as a pure function; the validator and the overview's gate preview both call it. |
| `tools/validate_all_troop_refs.py` | Cross-checks troop→item refs (underwear-bug gate). |
| Live armory `…/LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/*.xml` | The only tree the game loads. |

## What the analyzer flags

- **MONOLITHIC weight/armor** (ERROR) — a slot's weight or armor frozen at one value across ≥4 combat items. The #1 recurring defect; per decision #5, per-tier weight scaling is treated as a project-wide requirement, so any monolithic slot fails.
- **Data integrity** — missing `material_type` (ERROR) / `modifier_group` (WARN); 0/None combat armor (WARN); helmets missing hair/beard cover (WARN); legs/arms missing cover attrs (INFO).
- **Ceiling shortfall** (WARN) — top combat armor well under the culture's elite target (a near-full-tier gap, not a 1-2 point rounding miss).

It does **not** judge qualitative identity (e.g. "rhun weighs too much for cavalry") — that stays a human call. The analyzer is structural; the per-culture identity verdicts live in the audit.

## Current state (2026-06-30 baseline)

18 cultures, ~2,800 items: **21 errors, 21 warnings** (down from 25 errors after the harad fix). The analyzer independently reproduced the audit's structural findings (iron_hills arm 25/3.5, mirkwood no progression, etc.).

**Phase 3 progress:** `harad` is fixed — `--weights-only` laddered its 4 monolithic-weight slots (head/body/arm/leg) onto the tier curve with armor byte-untouched. `dol_guldur` pauldron_med_c `body_armor=8`→30 typo fixed (matches its `_med_a/b` siblings; was weaker than the lights).

**Owner decisions encoded (2026-06-30):** elite = troop levels 31-51 (→ bands above; lord = hero-only); dunland are raiders/skirmishers → light (`weight_mult` 0.95→0.85); rhun has the best cavalry in the game → mobile (`weight_mult` 1.00→0.90).

**Across-the-board sweep complete (2026-06-30).** Armory-wide: **20 analyzer errors → 2**, `validate_all_troop_refs` PASS (no item id changed). Method: `--no-lower-armor` full re-tiers for cultures with broken armor progression (dale, rohan, arnor, mirkwood — 0 stats lowered, only raised); roster `--weights-only` for weight-only issues (rhun mobile, rivendell light, gundabad, isengard — armor byte-untouched); targeted preserve fixes (iron_hills arm 25→14/18/22/25/31, dol_guldur leg weight, erebor 62 beard-covers, the Dain hero rebase above his troops); thenn body spread via keyword re-tier.

- **Clean (8):** dale, erebor, gondor, iron_hills, mordor, rhun, rohan, troll.
- **Minor (0 errors):** arnor, dol_guldur, dunland, gundabad, harad, isengard, mercenary, thenn.
- **Remaining 2 errors:** mirkwood + rivendell shoulder slots are monolithic-*armor* (few shoulder items, all at the elite value). Clearing them requires lowering low-tier shoulder coverage — vetoed by "do not nerf" — so they are flagged-not-fixed (arguably a heuristic false-positive for cultures with limited shoulder meshes).
- **Deferred:** mercenary's 37 items are mis-tagged `Culture.gondor` (pool into Gondor merchants). Retagging needs the neutral-pool culture decision; left untouched rather than guessed.

Full pre-sweep backup at `…/scratchpad/FULL_armory_backup_*`. Remaining design decisions (optional): mordor relabel-vs-split; whether to nerf the 2 elf shoulder slots; the mercenary retag target.

## Gondor/Mordor parity pass (2026-07-13, #342)

User reports "Mordor armor beats Gondor" confirmed and fixed. `tools/oneoff/fix_gondor_mordor_armor_parity.py` (one-off; reuses the curve via `import rebalance_armor`) applied 371 stat changes to the live tree, backed up as `*.bak-parity-20260713`:

- **Mordor cap-only (153):** items worn *exclusively* by Mordor troops pulled down to the Mordor (−1) curve. The `sk_uruk_mordor_*_heavy_*` Black Uruk set — the pending relabel-vs-split decision above — was resolved as **roster-ELITE** (worn L26–36): bracers 30→25, pauldrons 25/20→14/11, greaves kept at 30 (≤ elite 33). Uruk `_medium_` kit capped at medium (bracers 20→13, pauldrons 15/12→7/7); the L11-anchored `chainmail_captain_*` capped at roster-light (35→19).
- **Shared-pool constraint:** `sk_gn_orc_*` + `sk_md_orc_*` (also worn by goblin/mistymountainorcs/isengard), `urukscout_*` (isengard, misfiled in mordor/), and `ar_ardunian_*` (umbar) are NOT capped — nerfing them would hit other cultures as collateral. Ties on shared kit are broken by the Gondor top-up instead: Gondor leads shared kit by 1, Mordor-exclusive kit by 2.
- **Gondor top-up-only (218):** keyword-tiered stats sitting exactly at the plain baseline raised to the Gondor (+1) curve (e.g. heavy chest 42→43, elite helmet 40→41). Off-pattern regional specials untouched.
- **Roster fixes (repo `troops_{gondor,mordor}.xml`):** L6 Gondor levies got the light helmet (they had none — orc recruits out-armored them); L16 Gondor line infantry/nobles upgraded from light gloves/greaves to medium (they wore med chest+helm with light extremities, losing to fully-slotted orc medium kit); mirrored `gondor_militia_archer`/`_veteran_*` topped up; `mordor_warg_rider` (L6) dropped its medium chest + cape/gloves to band-correct light.

Post-fix verification: Gondor > Mordor at every slot×tier max (uruk set judged as elite), and per-troop total armor Gondor-dominant (median AND max) at every shared level band (L6–L36). `validate_moduledata.py` PASS; analyzer still 2 errors (the pre-existing elf-shoulder flags). In-game smoke owed (restart required — armor stat changes only load at launch).

## Dale ladder repair (2026-09-04, #541)

Dale's four-tier per-line ladder (`a01..a04` bronze, `b01..b04` silver) had been statted on a
different axis by `generate_dale_armor.py`, which treats the eight suffixes as one light-to-elite
ladder. A roster-tiered restat could not repair it on its own because the tier map anchors an item
to its LOWEST wearer, and three low troops wore the mid items (the L11 Squire in `a02`, the L16
militia veterans in `a03`). Those rosters moved first (`a01`, and the silver `b01`), then
`rebalance_armor.py --cultures dale --tier-source roster --no-lower-armor --keep-materials` raised
`sk_dale_*_infrantry_a02`, `sk_dale_*_infrantry_a03` and `sk_dale_*_archer_a03` to the heavy row,
applied to the Steam install and to `E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory`. Raise-only,
so the over-target silver line the roster report lists was left where it is. The same pass restatted
the two Dunland `wulf_helmet_medium_*` from 45 (the lord row) to 22: an item value that made every
L21 Dunlending an armour downgrade from the L16 warriors. Equipment ladders are now gated per upgrade
edge by `UPGRADE_ARMOUR_REGRESSION` (see `troop-skill-balance.md`, "Armour is a ladder too").

## Kingdom armour ladder: cross-culture (2026-09-12, #581)

A report that a Gondor tier-9 troop wore less armour than a Dunland tier-5 troop. Every tool
above judges items within one culture or one upgrade edge, so nothing had ever put the kingdoms
side by side by tier. `tools/analyze_kingdom_armour.py` does that now (read-only, reports under
`tools/reports/kingdom-armour/`, gitignored like the other analyzer output), and the validator
carries a `CROSS_CULTURE_ARMOUR_INVERSION` warning so the class stays visible.

**How a troop is measured.** The four engine regions (head, body, arm, leg), each summed over the
five armour slots exactly as `Equipment.GetHeadArmorSum` / `GetHumanBodyArmorSum` /
`GetArmArmorSum` / `GetLegArmorSum` do (v1.4.8 `Equipment.cs:271-325`): a chest contributes arm
armour, a cape body armour. Each region is the mean over the troop's battle sets, an unfilled
slot counting 0, civilian sets excluded; the total equals `fix_upgrade_armour_regressions.total`.
Troops are placed by ENGINE tier, `clamp(ceil((level - 5) / 5), 0, 10)`, the number the party
screen shows, not by the curve's five level bands. Creature troops, the two bespoke mount riders
and any troop without a `level=` are excluded; militia and standalone troops are tagged; the
bare-chested-by-design troops are compared on Head, Gloves and Leg only; the validator's
`_ARMOUR_LADDER_EXEMPT` troops leave the matrices, the pair list and the gate together and stay in
the per-culture troop tables tagged `ladder_exempt` (the first draft kept them in the pairs, and
the Ithilien ranger topped every worst-pairs list as if it were a live regression). The per-slot
draw is the campaign spawn path's behaviour: `Equipment.GetRandomEquipmentElements` redraws per
slot whenever the seed is not -1, and `CharacterHelper.GetPartyMemberFaceSeed` never yields -1
(`.claude/rules/troops.md` carries the exception, settlement guards). In the ceiling tables an
unworn item's tier is judged on the curve of the Armory FOLDER it lives in, because that is the
curve it was statted on: Umbar wears Harad's folder (-3) while carrying its own -1, and judged on
Umbar's curve Harad's elite pieces read as heavy and dropped out of Umbar's reserve list. Items
worn only by an excluded troop (the troll plate on `cave_troll`) count as worn, so they appear in
no reserve list, and their folder counts as worn by nobody, so they appear in no ceiling table. The
curve-view aliases (`dolguldur`, `rhun_new`, `lindon`, `goblin` to the folder each wears) are for
the armour curve only; the skill curve in `rebalance_troops.detect_culture` keeps goblin as its own
weaker culture, on purpose. Since the Codex pass of 2026-09-13 an unworn item's tier is also routed
by its id, so a `sk_dg_` helmet in the rhun folder is judged on Dol Guldur's cap (62 Dol Guldur items
had read as Rhun heavy and left Rhun's reserve list; it is 176 items, not 114), and the report ends
with two observation tables for the roster pass, not findings: **kit off the culture's line** (a
worn item whose line cap differs from the wearer's own; a `mordor_num_` or `mordor_uruk_` troop is
held to its own line, as `tools/ranged_ladders.json` routes it) and **uncurved kit above the
ceiling** (a vanilla item, or one from a folder off the curve, whose primary stat sits above the
culture's elite value for the slot; no restat reaches it). On 2026-09-13: 162 rows over 33 troops
(Isengard orcs in Mordor orc kit 54 rows, Rhun troops in Dol Guldur's `sk_dg_` kit 53, Mordor
militia in Black Uruk kit 36, Umbar nobles in Black Numenorean plate 15, the elves in Gondor kit
4) and 11 uncurved rows (Dunland's vanilla `tall_helmet` 38 on four troops and `plumed_helmet` 47
over an elite value of 36; Harad's `aserai_scale_armor_on_chain` 51 over 44 and
`strapped_mail_chausses` 23 over 22 on three troops).

**What the first run found (854 troops, 16 cultures).**

- The literal pair is not inverted: the weakest Gondor T9 (`gondor_ith_moon_guard`, L46) totals
  180, the strongest Dunland T5 (L26 nobles) 163. The head slot is: Dunland T5 helmets are 40 and
  T6 helmets 45, while Gondor's head median is 33 at every tier from T4 to T9 and its best helmet
  in the whole folder is 41 (Dale 41, Dunland 45, Isengard 46, Mordor 50, Rohan 50, Rhun 56,
  Erebor 78). Dunland T6 (182) beats Gondor's T9 Moon Guard (180) and five of its twelve T8s.
- Dunland is not the outlier; Gondor is. At threshold 20 and a two-tier gap the run lists 7,803
  troop pairs over 150 culture pairs, and Gondor is the weaker side of 2,766 of them (goblin next
  at 998; Dunland is the stronger side of 43, none of them against Gondor: at threshold 0 a
  Dunlending tops a Gondor troop two or more tiers up in 61 pairs, the widest by 17 points).
  Gondor's T8 median (177) sits under the T6 median of the elves (280), Erebor (228),
  Isengard (215), Dale (202), Dol Guldur (195), Umbar (190), Rhun (188) and Gundabad (184); its T5
  median (150) is above only Goblin-town (126), Mordor (126), Rohan (128) and Rhun (149) among the
  fifteen cultures with a T5 cell.
- The curve's one "elite" row for L31 to L51 means Gondor's T6 through T10 all target 221 and
  land at 171, 172, 177, 194 and 125 (the ranger). The curve view's leg column reads 31 to 35 under
  target for Gondor at every tier from T4, but that view is a generic benchmark, not a per-item
  target (the culture's default line at the troop's band, every slot filled, secondaries at the
  legacy proportion; a Black Numenorean in `troops_mordor` is held against the orc cap), and part
  of the leg gap is a convention, not a deficit: the
  curve's body row carries a `leg_armor` secondary and Gondor chests carry `arm_armor` instead (1
  of 116 has any leg armour; Dunland, Erebor, Rohan and Dale are the same, Rhun and Isengard carry
  it). Read "d leg" against the culture's own convention.
- The Armory holds a reserve. Gondor has 20 unworn elite/lord-row items: seven helmets at 40
  (Dol Amroth foot, cavalry and warden, Linhir lord), six chests at 50 to 51 (Cair Andros, Dol
  Amroth B, Linhir, Lond Galen, Osgiliath, Arndir), five elite capes, the Serelond lord bracer and
  greaves. Nothing sits above body 51 / head 41 in the folder, so re-slotting can lift the T8/T9
  capitals to the folder ceiling (the Serelond set: 41 / 51 / 20 / 27 / 35, all worn) and no further.
- The gate warns on four cells: `goblin/tier7` (135 vs a 178 field at T5), `gondor/tier9` (194 vs
  220 at T7), `lindon/tier10` and `rivendell/tier10` (205 vs 227 at T8, the golden-flower fan-out
  capstones at 191 under their own T9 at 376). The exemptions are `cave_troll`, the two Harad
  riders and `gondor_ithilien_ranger` (a light ranger kit by design; without it Gondor T10 warns too).

**The gate.** Per (culture, engine tier) cell, on medians: a cell warns when its median total sits
more than 20 points (about 0.4 of one curve row) under the median of the other cultures' medians two
tiers lower, with at least three other cultures holding such a cell. Per-troop pairs were rejected
(thousands, unreadable) and culture-pair medians too (the elves are twice everyone by design). A
culture uniformly OVER the field is not flagged; the report's pairwise section shows that side. The
arithmetic is one pure function, `taom_schema.cross_culture_armour_inversions`, which the report's
"gate preview" section calls with the validator's own constants.

**What was decided (2026-09-13, #583).** The three options on the table were (A) re-slot rosters
only, (B) restat the top-tier items, (C) rebuild the curve. The maintainer took (C) in its own
form: a chest cap per kingdom with fixed slot and band ratios, applied to the whole Armory (the
"kingdom-cap curve" section above), followed by the ladder repair on the 58 promotion edges the
re-curve exposed. The gate scales every item to the 57 reference cap of its own line since
then, because kingdoms now differ in armour power by design.

## Mesh-tier ladder: which artist tier a troop level may wear (2026-09-16, #609)

A screenshot: `[Gundabad] Uruk Medium Chest IV` at 31 body armour (tier 6, 14.9 kg) beside
`[Gundabad] Uruk Lord Chest III` at 20 (tier 4, 25.3 kg). The kingdom-cap curve above had priced
both correctly for the rosters it was given: the six `sk_gb_uruk_chest_lord_*` were worn by the
level-11 snaga and hunter (the 2026-05-19 fan-out, `b88a25e7`, padded three low troops with the
whole lord line as extra battle sets), so the line anchored at the light band, 49 x 0.40 = 20,
while the medium line, worn from level 16, sat at 31. "Lord" is the mesh the artist named. Nothing
tied it to a troop level, so the plate chest was gutted to fit the recruit and the level-41 rider
fought in it.

**The ladder** (`rebalance_armor.MESH_TIER_LADDER`, the maintainer's table). The tier is read
from the id token (`mesh_tier_of`: `_light_`, `_med_`/`_medium_`, `_heavy_`, `_elite_`, `_lord_`;
`_civ`/`civilian` is off the ladder, an id with no token is not judged). Rows are inclusive upper
bounds; TAOM levels run 6, 11, 16 ... so a level between rows takes the row above it.

| Troop level | Allowed mesh tiers |
|---|---|
| <= 6 | light |
| <= 16 | light, medium |
| <= 21 | medium |
| <= 26 | heavy |
| <= 31 | heavy, elite |
| <= 36 | elite |
| >= 41 | elite, lord |

Lord kit is for level 41+ troops or lords; hero kit never anchors and is out of scope. The
ladder-exempt and bare-chested-by-design troops are skipped, as are civilian sets. Only
`troops/troops_*.xml` is judged (villagers and notables are not on the ladder).

**Two directions, one gate.** Over-dressed (the tier above the highest allowed) is the harmful
one: the low wearer anchors the mesh, so every higher troop in it loses armour. Under-dressed
(below the lowest allowed) is cosmetic; the troop-level gates already price it. The validator's
`ARMOUR_MESH_TIER_LADDER` warning reports both with the direction in the message; the pure function
is `rebalance_armor.mesh_ladder_violations`, shared with the fixer, and it needs no install because
the tier is in the id. Baseline on 2026-09-16 before the fix: 251 over-dressed (troop, item) pairs
over 147 troops in 9 cultures, 1,283 under-dressed over 344 troops.

**The fixer**, `tools/fix_armour_mesh_ladder.py` (dry-run default, `--apply`), swaps each
over-dressed pair to the SAME line (the id up to its tier token, in the same slot file, so a helmet
is never offered as a chest) at the substitute tier, the same old item becoming the same new item
in every set of the troop so its sets stay interchangeable. Three rules decide the target, in
order, and each exists because the naive choice recreated the bug:

1. **Tier**: `substitute_mesh_tiers(level)`, the allowed tiers not above the troop's STAT band
   (`level_to_band`) highest first, then the allowed tiers above it lowest first. A level-11 troop
   may wear medium, but its band is light: put in `_med_a` it anchors that variant to the light
   band, one notch down from the original bug. So light first; medium stays on the list because a
   line with no light variant (the Mordor orc infantry chest) still has a ladder-legal swap, and a
   medium mesh dragged to the light band beats a heavy one left there.
2. **Variant**: among a tier's variants, the one whose current anchor band (`line_anchors`, the
   lowest in-scope wearer) is nearest the troop's band wins; at equal distance, below beats above.
   Two `_med_` chests can be two bands apart, because each is priced by its own lowest wearer: the
   first apply put a level-21 guardsman on `anf_inf_helmet_med_a` (a level-11 wearer, 21) when
   `med_b` sat beside it at 33, and nine upgrade edges regressed. Troops are placed lowest level
   first and every pick moves the anchor it lands on, so two troops converging on an unworn
   variant in one run see each other (the deep review found the level-16 grunt and the level-21
   orcs stacked on `sk_md_orc_inf_chest_med_d`; one run now spreads them over `med_b`/`med_c`).
   A pick that lands off the troop's band because the line has nothing at it is marked on its
   row (`anchor L16, a band below`), 85 of 186 on 2026-09-16, most of them level-21 troops whose
   only ladder-legal mesh is medium while their stat band is heavy.
3. **Suffix**: same variant letter, else the nearest one of the same shape (`_cape_a` before `_a`).
   A digit may follow the tier token (`rivendell_torso_lord3_silver`, `thenn_armor_med1`), so the
   split reads every id `mesh_tier_of` tiers; a token followed by a letter (`_lordly`) is not a tier.

A line with nothing at any allowed tier is REPORTED and left alone (hand decision). Under-dressed is
reported, never written (`--report-under` lists the rows). After `--apply` the rosters are re-read
and the run exits 1 if an over-dressed pair with a substitute remains. Then re-derive and restat,
because the anchors moved:

```
python tools/fix_armour_mesh_ladder.py            # read the table
python tools/fix_armour_mesh_ladder.py --apply
python tools/derive_armor_tiers.py
python tools/rebalance_armor.py --dry-run --all --tier-source roster-first --keep-weights --keep-material-type
python tools/rebalance_armor.py --apply   --all --tier-source roster-first --keep-weights --keep-material-type --backup-tag <tag>
python tools/rebalance_armor.py --apply   ... --armory-path "E:\repos\lotraom-assets\v1.5\LOTRLOME_Armory\ModuleData\LOTRLOME_items"
```

**What the 2026-09-16 pass did.** 186 over-dressed pairs swapped over 118 troops in 11 troop
files (249 equipment lines), the Uruk warrior's nine capeless sets given the `pauldron_medium_b` it
already wore in the tenth (its demoted heavy chest had been masking the gap as an upgrade
regression), 60 Armory items restatted on the live install and the `lotraom-assets` v1.5 mirror
(weights and materials untouched; `.bak-meshladder-609` beside each Armory file): the six Gundabad
lord chests 20 to 49, Iron Hills heavy chests 28 to 59, the Noldor gold heavy torso 44 to 68, the
Mordor orc heavy chests 15 to 32, five Gondor heavy helmets 33 to 43, and so on. One item went
down, `sk_md_orc_inf_chest_med_d` 24 to 15: the line has no light variant, the level-11 goblin
snaga and hunter had to land on a medium mesh, and the picker gave them the unworn one, so nobody
else pays. `CROSS_CULTURE_ARMOUR_INVERSION` stayed at the same 7 cells. Four
`UPGRADE_ARMOUR_REGRESSION` warnings appeared, all pre-existing under-dress that surfaced when the
parent's freed mesh climbed to its real band (the level-36 goblin veterans in `_heavy_` helmets
under a parent whose `arc_helmet_heavy_a` rose 29 to 34; the Imladris and Lindon guardsmen in
untokened tier-3 kit under a swordguard whose `torso_heavy_tier1` rose 44 to 68); they belong to
the under-dressed pass, not to `fix_upgrade_armour_regressions.py`, whose cascade would re-dress
the capstone's lord torsos and push the beastmaster into medium mesh.

**65 hand decisions** (over-dressed, nothing in the line at an allowed tier). Dunland's whole tree
sits one notch high because the artist named that kit a tier up (`heavy` is its mid mesh); Gondor's
level-6 peasants have no light helmet in the Anorien infantry line; the Rhun and Iron Hills lines
have no medium greaves or chest. The choices are: author the missing mesh tier, accept the
kit and add the troop to `_ARMOUR_LADDER_EXEMPT` with a reason, or move the troop to another line.

| Culture | Troop | L | Slot | Item | Mesh | Allowed |
|---|---|---|---|---|---|---|
| rhun_new | `black_sun_scout` | L21 | Leg | `sk_rh_drag_grvs_heavy_a` | heavy | medium |
| rhun_new | `darkhun_horseman` | L21 | Leg | `sk_dg_khml_grvs_scale_heavy_a` | heavy | medium |
| rhun_new | `darkhun_infantry` | L21 | Leg | `sk_dg_khml_grvs_hplate_heavy_a` | heavy | medium |
| dunland | `dunland_bear_chosen` | L21 | Body | `dunland_wulf_scalemail_heavy_e` | heavy | medium |
| dunland | `dunland_boar_spearman` | L21 | Body | `dunland_wulf_scalemail_heavy_c` | heavy | medium |
| dunland | `dunland_dragon_crossbowman` | L21 | Body | `dunland_caerdh_chainmail_heavy_f` | heavy | medium |
| dunland | `dunland_dragon_crossbowman` | L21 | Cape | `dunland_caerdh_pauldron_heavy_cape_a` | heavy | medium |
| dunland | `dunland_dragon_crossbowman` | L21 | Head | `dunland_caerdh_helmet_heavy_d` | heavy | medium |
| dunland | `dunland_falcon_archer` | L21 | Head | `dunland_caerdh_helmet_heavy_e` | heavy | medium |
| dunland | `dunland_lizard_horseman` | L21 | Body | `dunland_caerdh_chainmail_heavy_b` | heavy | medium |
| dunland | `dunland_lizard_horseman` | L21 | Cape | `dunland_caerdh_pauldron_heavy_cape_a` | heavy | medium |
| dunland | `dunland_lizard_horseman` | L21 | Head | `dunland_caerdh_helmet_elite_b` | elite | medium |
| dunland | `dunland_ox_pikeman` | L21 | Body | `dunland_wulf_scalemail_heavy_b` | heavy | medium |
| dunland | `dunland_raiders_boss` | L21 | Body | `dunland_wulf_scalemail_heavy_f` | heavy | medium |
| dunland | `dunland_raven_archer` | L21 | Body | `dunland_caerdh_chainmail_heavy_a` | heavy | medium |
| dunland | `dunland_raven_archer` | L21 | Head | `dunland_caerdh_helmet_heavy_f` | heavy | medium |
| dunland | `dunland_raven_warrior` | L16 | Head | `dunland_caerdh_helmet_heavy_e` | heavy | light/medium |
| dunland | `dunland_stag_lancer` | L21 | Body | `dunland_caerdh_chainmail_heavy_a` | heavy | medium |
| dunland | `dunland_stag_lancer` | L21 | Cape | `dunland_caerdh_pauldron_heavy_cape_b` | heavy | medium |
| dunland | `dunland_stag_lancer` | L21 | Head | `dunland_caerdh_helmet_elite_a` | elite | medium |
| dunland | `dunland_wolf_raider` | L21 | Body | `dunland_wulf_scalemail_heavy_f` | heavy | medium |
| rhun_new | `far_rhun_cavalry` | L21 | Leg | `sk_rh_loke_grvs_heavy_a` | heavy | medium |
| rhun_new | `far_rhun_horse_master` | L21 | Leg | `sk_rh_loke_grvs_heavy_a` | heavy | medium |
| rhun_new | `far_rhun_infantry` | L21 | Leg | `sk_rh_loke_grvs_heavy_a` | heavy | medium |
| gondor | `gondor_anf_cavalry` | L21 | Head | `sk_gd_anf_cav_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_anf_cavalry` | L21 | Head | `sk_gd_anf_cav_helmet_heavy_b` | heavy | medium |
| gondor | `gondor_anf_infantry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_anf_levy` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_ano_mt_cavalry` | L21 | Head | `sk_gd_ano_cav_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_ano_peasant` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_bel_infantry` | L21 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | medium |
| gondor | `gondor_bel_recruit` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_bel_recruit_merc` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_bel_vet_infantry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_b` | elite | heavy |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_a` | lord | heavy/elite |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_b` | lord | heavy/elite |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_c` | lord | heavy/elite |
| gondor | `gondor_lam_hill_warden` | L31 | Head | `sk_gd_lam_nob_helmet_lord_d` | lord | heavy/elite |
| gondor | `gondor_lam_vet_swordman` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_leb_infantry` | L21 | Head | `sk_gd_ano_cav_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_loss_axe_thrower` | L21 | Cape | `sk_gd_los_pauld_inf_heavy_a` | heavy | medium |
| gondor | `gondor_loss_lumberman` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_loss_lumberman_merc` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_loss_noble` | L16 | Cape | `sk_gd_los_pauld_nob_heavy_a` | heavy | light/medium |
| gondor | `gondor_loss_noble_captain` | L36 | Body | `sk_gd_los_nob_chest_lord_a` | lord | elite |
| gondor | `gondor_loss_noble_veteran` | L21 | Cape | `sk_gd_los_pauld_nob_heavy_a` | heavy | medium |
| gondor | `gondor_loss_vet_axebearer` | L21 | Cape | `sk_gd_los_pauld_inf_heavy_a` | heavy | medium |
| gondor | `gondor_osg_archer` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_osg_infantry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_pg_archer` | L21 | Head | `sk_gd_pin_arc_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_pg_cavalry` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | heavy |
| gondor | `gondor_pg_spearman` | L21 | Cape | `sk_gd_osg_pauld_cape_inf_elite_a` | elite | medium |
| gondor | `gondor_pg_spearman` | L21 | Head | `sk_gd_pin_spear_helmet_heavy_a` | heavy | medium |
| gondor | `gondor_pg_vet_spearman` | L26 | Cape | `sk_gd_osg_pauld_cape_inf_elite_b` | elite | heavy |
| gondor | `gondor_pg_volunteer` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_ring_peasant` | L6 | Head | `sk_gd_ano_inf_helmet_med_a` | medium | light |
| gondor | `gondor_ser_noble` | L16 | Head | `sk_gd_sere_helmet_heavy_a` | heavy | light/medium |
| gondor | `gondor_ser_veteran` | L21 | Head | `sk_gd_sere_helmet_heavy_a` | heavy | medium |
| erebor | `ironpass_arbalest` | L21 | Body | `sk_dwarf_iron_chest_heavy_a` | heavy | medium |
| erebor | `ironpass_arbalest` | L21 | Body | `sk_dwarf_iron_chest_heavy_b` | heavy | medium |
| erebor | `ironpass_arbalest` | L21 | Body | `sk_dwarf_iron_chest_heavy_c` | heavy | medium |
| erebor | `ironpass_infantry` | L21 | Body | `sk_dwarf_iron_chest_heavy_a` | heavy | medium |
| erebor | `ironpass_infantry` | L21 | Body | `sk_dwarf_iron_chest_heavy_b` | heavy | medium |
| erebor | `ironpass_infantry` | L21 | Body | `sk_dwarf_iron_chest_heavy_c` | heavy | medium |
| erebor | `ironpass_ram_rider` | L21 | Body | `sk_dwarf_iron_chest_heavy_c` | heavy | medium |

### 2026-09-25: Gondor after KEYforce's Lamedon drop, and the noble-line rule

KEYforce's drop (lotraom-assets `429746b2`) re-kitted Gondor into new Lamedon and Ringlo Vale
armour. His spec (`tools/gondor_armors_and_troops.md` in the mirror) splits each Gondor region into
a regular line and one or more "Noble" lines, and dresses the nobles in their own `_nob_` kit from
the first tier. Mike's rule (2026-09-25): **nobles wear better armour than regular troops of their
level.** It is implemented in the tools, not as exemptions:

- **`taom_schema.Validator._NOBLE_LINE_TROOPS`** names the 92 noble troops at engine tier 2 to 7
  (level 11 to 36) of the spec's sixteen noble lines. Tier 8+ nobles already sit on the elite and
  lord rows.
- **The mesh gate and fixer judge a noble one tier up:** `rebalance_armor.allowed_mesh_tiers(level,
  noble=True)` adds the tier above the level's ceiling, so the level-11 Ringlo militia may wear the
  heavy Anorien helmet KEYforce gave it.
- **A noble anchors an item a stat band up, never below the item's own tier:**
  `rebalance_armor.noble_anchor_level(level, item_id)`, read by `derive_armor_tiers.py` and
  `fix_armour_mesh_ladder.line_anchors`. The floor matters: without it the level-11 militia
  anchored the regular Anorien heavy helmet at the medium band and dragged it from 43 to 33 for
  every regular troop wearing it (the lowest at level 26). An elite-band noble (level 31 and 36)
  anchors at its own level: elite is the top band.
- Nobles stay judged by every gate and stay in the cross-kingdom cells. The first attempt put them
  in `_ARMOUR_LADDER_EXEMPT` instead; that stopped them anchoring at all, so their `_med_` pieces
  priced by the id keyword and most landed below a regular of the same level (Ithil Guard armour 57
  to 36 on a level-31 noble), while the gates stopped seeing Gondor's tier 3 to 7. Deep review
  wave 2 caught it the same day.

**Hand decisions are (troop, item) pairs:** `_ARMOUR_LADDER_EXEMPT_ITEMS` holds 26 pairs: the
18 over-dressed pieces of regular Gondor troops whose line ships no lower-tier variant (Anorien infantry helmets at
level 6, Lamedon and Lossarnach heavy pauldron-capes, Anfalas, Anorien cavalry and Pinnath Gelin
heavy helmets, Anorien and Osgiliath elite pauldron-capes), and 8 Arnor noble-chest pairs (the Arthedain king's guard
and ranger captain, the Rhudaur lord of the Angle and ranger captain). Only that item is excused on that troop:
it is not judged there and does not anchor its price, and the troop's other slots stay on the
ladder. Whole-troop exemption had repriced slots nobody decided on (the Osgiliath chest 63 to 47).

The roster-first restat ran for Gondor (`--keep-weights --keep-material-type`) three times while
this was built. Against KEYforce's typed stats the net is 91 items up, 67 down and 216 unchanged.
Noble medians now sit above regulars at every tier (level 16: 185 against 124; 21: 204 against 144;
26: 241 against 207; 31: 246 against 217; 36: 250 against 218), and the Gondor lord template reads
215 (202 before). Seven noble troops stay under the regular median by their kit, not their pricing: the Cair
Andros and Osgiliath first tiers, the Tolfalas arbalest and crossbowman, the Methir noble, the
Pelargir skirmisher, and the Blackroot archer by one point. Backups in the
live Armory: `LOTRLOME_items/gondor/*.bak-noble-restat-2026-09-25` (KEYforce's stats),
`*.bak-noble-band-2026-09-25`, `*.bak-noble-floor-2026-09-25`. Validator after:
`ARMOUR_MESH_TIER_LADDER` 1,330 against 1,348 at `HEAD`, `CROSS_CULTURE_ARMOUR_INVERSION` 7
(unchanged), `UPGRADE_ARMOUR_REGRESSION` 4 against 6, none of them Gondor.

Until 2026-11-15 the restat lived only in the unversioned Armory, and the mirror held KEYforce's
stats by Mike's rule. Since mirror commit `2088acbc2` of that day the mirror holds the live values, the
Gondor ones included, so a sync from the mirror's head no longer reverts them; a sync from an older
copy, or a new artist drop, still can. Re-run the restat after any Gondor sync:
`python tools/derive_armor_tiers.py`, then
`python tools/rebalance_armor.py --dry-run --tier-source roster-first --keep-weights
--keep-material-type --cultures gondor`; a clean tree reports `Changed: 0`.

**Not fixed here, known:** the under-dressed direction (1,283 pairs) is the next roster pass. (The
inherited-ratio secondaries of #583, Codex review 104, which let the medium chest's arm armour (41)
read above the lord chest's (25), were closed on 2026-11-14: a secondary is 0 below the heavy band
and capped from it, so a Gondor medium chest has 0 and a lord chest 15.)

## Dependencies

- `rebalance_armor.py` (curve) — the analyzer imports it.
- The live `LOTRLOME_Armory` module install (read at runtime).
- Python stdlib only (`xml.etree`, `statistics`, `json`).

## Tests / verification

The analyzer is read-only and self-verifying: running it against the live tree must reproduce the known defects (iron_hills arm monolithic, harad body all 9.5, dale clean). No unit-test harness yet; the regression check is "re-run and confirm the executive summary matches this doc's baseline."

The kingdom overview has one: `tools/tests/test_analyze_kingdom_armour.py` (on a synthetic two-culture tree: region sums, civilian exclusion, classification, ladder-exempt handling, curve aliases, matrices, pairwise and bare-chested inversions, ceilings judged on the item's own folder, gate preview, a `main` run that hashes every fixture XML before and after, a sub-line item in a shared folder kept in the reserve, the two observation tables with a sub-line troop held to its own cap, and one over the shipped troops that fails if a troop the analyzer excludes by name is missing from `_ARMOUR_LADDER_EXEMPT`). The gate's own tests are `CrossCultureArmourInversionTests` in `tools/tests/test_validate_moduledata.py`, including one that fails if an `_ARMOUR_LADDER_EXEMPT` id no longer exists in the shipped troops.

The kingdom-cap curve (#583) has `tools/tests/test_kingdom_caps.py`: the cap table and ratios as the maintainer stated them, every cap key a folder or a routed line, the routing of each sub-line (`tier_from_value` included), the cap-model values per band and slot (rounding half up, the lord band 7 percent over elite and at most 5 points over it, the four Mordor-folder lines by id), the capped secondary rule and the decided parameter table, the hero and unworn clamps, `heroWorn` in the derived map, the worn-limit functions, weight on the legacy ladder, `level_to_band` at each band edge and shared with the derivation, the Black Numenoreans back on the curve with hero kit and the Khamul line told apart, the two-tier invariant clean on every cap and reported on a compressed one, anchors (battle sets only, civilian and ladder-exempt troops never anchor, the exempt set is the validator's, the map anchor first like the writer), and the writer (roster-first banding, secondaries by ratio with zero staying zero, weights and material kept while the extremity loot tables move, a commented copy skipped, BOM and CRLF preserved, one `.bak-<tag>` per file that a second apply leaves alone), and since 2026-11-14 also the Mordor lords' routing and clamp, the hero wearers read from `lords.xslt`, from the wanderer templates (a shipped-data case pins the Dol Guldur Uruk elite helmet) and from a direct `<equipment>` override, `--keep-weights` leaving the `weight` text alone, the written-file count and the `--backup-tag` requirement. `tools/tests/test_generate_starter_armor.py` pins every starter value at or under its culture's light band and the byte-faithful write of a generated file (parse first, BOM and line endings kept).

## How-To

**Fix a culture's monolithic weight slot:** use `rebalance_armor.py --apply --weights-only --cultures <c> --backup-tag <tag>`. It ladders ONLY the weight to each item's armor-derived tier (`tier_from_value`) and leaves armor + material untouched, so it can't mangle hand-tuned armor through the keyword detector. It is guarded: it only touches a slot that is **currently monolithic-weight**, so it can never collapse an already-varied slot (the harad-shoulder regression, fixed 2026-06-30). Back up the culture folder first; re-run the analyzer to confirm the error cleared.

**`--weights-only` applicability (important):** it is correct ONLY when the slot's armor is varied AND correctly tiered, because weight follows armor. It is the right tool where the armor is already right and only the weight is frozen (harad body/head/leg). It is the WRONG tool when: (a) the armor is itself uniform — it can't ladder (no-op, e.g. rohan's combat boots, iron_hills arm), those need an armor mid-tier first; or (b) the armor is over-tiered for the culture's intended weight class — it would propagate that into heavier weights (e.g. dunland body 30-45 → 12-21kg, the opposite of the light-raider intent). Cultures in (b) need the identity decision (lower armor + material) before any weight pass.

**Re-tier a whole culture from its rosters:** `python tools/derive_armor_tiers.py` (refresh the map), then `rebalance_armor.py --dry-run --tier-source roster-first --cultures <c> --keep-weights --keep-material-type` and read the proposed changes. Each worn item is re-stated to its lowest wearer's band on the kingdom-cap curve (troll and civilian kit skipped, hero-only kit lowered to the lord band, unworn kit by its id keyword up to elite, keyword-less unworn kit lowered to elite, nothing ever raised by a clamp); the older `--tier-source roster` reads the map's `tier` column, which is now anchor first too, so the two differ only in that `roster` skips a keyword-less unworn item (an unworn item with a tier word in its id has a tier in the map and is re-stated by both) and folds `lord` to `elite`. Add `--no-lower-armor` for a "do not nerf" culture (raises under-tiered items, never reduces; on its own it no longer holds material or weight, so pair it with `--keep-materials` (material and loot table frozen) or `--keep-material-type` (loot table still follows the band) and `--keep-weights`). Add `--weights-only` to set weight by roster tier without touching armor (e.g. a mobility trim that spares elite plate). Always dry-run first: the dunland dry-run caught a wrong-direction change before applying.

**Set a kingdom's armour power:** edit `KINGDOM_CAPS` (one chest value; a sub-line that shares a folder gets a `LINE_PREFIXES` row); dry-run with `--tier-source roster-first --keep-weights --keep-material-type`. `CULTURAL_MODS[culture]` no longer moves a capped kingdom's protection (Gondor at protection 99 still writes a 46 chest); it carries `weight_mult` and the protection of the legacy path (the civilian tier and the uncapped folders such as `troll`). The analyzer's "In CULTURAL_MODS" column still flags a culture on the neutral default, which matters for weight.

**Exclude a new hero/boss item from the curve:** add its id substring to `EXCLUDE_ID_SUBSTRINGS` or its name to `HERO_NAMES`. Since 2026-11-14 (#1271) that keeps it off the troop curve only while no troop wears it, and the writer still lowers it to the lord band (never raises it); a monster piece that must stay untouched goes in `MONSTER_ID_SUBSTRINGS`.

## How to verify in game

Not verified in game: the 2026-11-14 bands, secondaries, hero clamps and loot tables (#1271) are in the
live Armory since the apply of 2026-11-14, but nobody has played them. After a full restart (item XML
loads at launch), each step pairs a check a tool can make with what only a person can see.

1. Gondor elite (tier 6 or more) armour. Tool: after the apply, `python tools/analyze_kingdom_armour.py --stdout`
   reads the live Armory and its "Worn floor and ceiling" section lists no troop over a ceiling (head
   64, body 77), and a Gondor elite sums to about 48 head and 59 body (chest 46 plus pauldron 13), an
   Erebor elite to at most 54 and 67. Person: the encyclopedia or the party screen shows those numbers.
2. A light and a medium chest. Tool: the dry run of step 3 plans `Changed: 0`, so the live files hold
   arm and leg armour 0 on both. Person: the item tooltip shows no arm or leg line.
3. Thranduil's or Legolas's gloves (arm 26, was 90) and a Golden Flower helmet (53, was 30). Tool: the
   same `Changed: 0` dry run. Person: the inventory tooltip.
4. A Custom Battle, same-tier one handed hits on heavy and elite troops. Tool: none for the blow itself;
   `python tools/validate_moduledata.py` reports `MELEE_BLOW_CEILING` for the weapon class limits.
   Person only: that no such hit kills in one blow in play, and that an elite (tier 6 or more) killing
   a tier 1 to 3 recruit in one blow still happens, as wanted.
5. A troop the dry run lists under `WORN_ARMOUR_FLOOR` (a helmetless elite archer). Tool:
   `python tools/validate_moduledata.py` lists it. Person only: that it dies to near-tier one-shots
   until its kit is fixed.
6. A heavy or elite bracer, greave or pauldron with a modifier, in a campaign battle (a Custom Battle
   does not roll modifiers). Tool: `test_extremity_loot_tables_are_scoped` in
   `test_kingdom_caps.py` pins `cloth_unarmoured` (+3, not `cloth` +5) and, from heavy up, `leather`;
   `ExtremityLootTableTests` in `test_armor_curve_invariant.py` pins a worst roll of -3 on the installed
   Native modifiers; `python tools/rebalance_armor.py --check` exits 0 once the write is done. Person
   only: the roll and the name on a looted piece (the loot screen after a campaign battle), `Waxed`
   included. No game screen shows the modifier of a spawned enemy's piece, so the spawn roll has no
   in-game check.
7. The armoury upgrade offer for a Gondor medium to heavy chest. Tool: the arithmetic above gives about
   2,900 gold (it was about 25,000). Person only: the price shown in the armoury screen.
8. A Dol Guldur wanderer companion (`spc_wanderer_dolguldur_0`). Tool: `python tools/rebalance_armor.py --dry-run ...`
   leaves `sk_dg_uruk_helmet_elite_a` at 41. Person only: the hired companion's helmet in the inventory.
9. Lord looks (Lord kit, 2026-11-15). Start a NEW campaign: a hero's battle set is saved, so an
   existing save keeps the old kit. Tool: `python tools/fix_lord_kits.py --check` exits 0 and
   `python tools/validate_moduledata.py` lists `LORD_ARMOUR_RANGE` as warnings only. Person only: in
   the encyclopedia, two Rohan lords and two Dunland lords of different templates wear different
   chests, an Arthedain lord wears Arnor kit (no Gondor piece), and a Rhun lord of template `_c` or
   `_f` keeps the Khamul armour.
10. Lord armour variants (#1333). Start a NEW campaign after a full restart. Tool:
   `python tools/generate_lord_armour_variants.py --check` and `python tools/fix_lord_kits.py --check`
   exit 0, and `armour_classes.xml` classes every `taom_lord_` id `named`. Person only: in the
   encyclopedia, Mirkwood lords of templates `_a`, `_b`, `_c` and `_e` wear four different chests
   (plain, bronze, palace guard, silver), and Harad lords of the five templates five different chests;
   the tooltip of a lord's piece reads "Lord's", carries no Light, Med, Heavy or Elite, and shows the
   variant's values (a Harad chest 43 body, 13 arm, 17 leg; a Harad helmet 44 head; a Mirkwood plain
   chest 51 body, 9 arm, 20 leg); the Harad helmets keep their hair and beard cover; and no `Lord's`
   piece appears in a town market, a tournament prize list or the loot screen after defeating a
   Mirkwood or Harad lord's party. Known route: start a new campaign, pick a Harad lord (template `_b`
   to `_e`) on the faction screen as a preset, and the player wears his `Lord's` pieces; sell one in a
   town, and it is gone from that town's market the next day.
11. Armour band clones (2026-11-17). After the restat apply of the apply sequence and a full restart.
   Tool: `python tools/generate_armour_band_clones.py --check` and `python tools/rebalance_armor.py --check`
   exit 0, and `python tools/validate_moduledata.py` reports `TOP_TIER_ARMOUR_WORN_LOW` and
   `BAND_CLONE_SOURCE_WORN_LOW` 0 each. Person only:
   1. The Palace Guard (`mirkwood_palaceguard`): the chest tooltip reads body 48, arm 8, leg 19, and the
      troop sums to 50 head, 61 body, 43 arm and 41 leg in the encyclopedia.
   2. Enlist with Mirkwood as an infantry recruit (`enlist_mirkwood_infantry_recruit`): the issued chest
      has the palace guard chest's name and look at body 8, with no arm or leg line (the light clone).
   3. No clone is in a town market, a tournament prize list or the loot screen after a battle with
      Mirkwood's low troops (they wear the light clone).
   4. On a save made before the change, a companion hired before it still wears what he wore: a hero's
      equipment is saved, so the new wanderer kits reach only companions created after the change.
12. Colour families (#1450). Tool: `python tools/scan_armour_colours.py --check` and
    `python tools/generate_armour_band_clones.py --check` exit 0. Person only: in the encyclopedia the Mirkwood
    glaivesman, woodsman, guards and swordsman each wear one metal family per battle set (no silver chest
    under bronze pauldrons), and the Lorien warden keeps the silver elite helmet (`sk_ll_mw_helmet_elite_a`).

## Changelog

- 2026-11-18: Colour families (#1450). Every armour piece has a colour family (gold, bronze, silver and
  others), read from its names or its texture. A band clone upgrade never puts a silver piece into a gold
  or bronze set, or the other way round: such a piece falls back to a band clone. The Mirkwood infantry
  chest, the bronze chest and the greaves now give their top wearers the elite source, not a silver
  upgrade. The validator warns about troops whose sets still mix colours (41, all older than #1440).
- 2026-11-17: Review rounds of the armour band clones. Every set outside the troop battle sets now names
  a clone too: the enlistment kits by rank (a recruit's kit issues the light clone), wanderer and
  companion kits, character creation, NPCs by level and the child templates; a lord keeps the source
  (warning `BAND_CLONE_SOURCE_WORN_LOW`). A chest the restat raises from below the heavy band now takes
  its line's leg share even when it declares no leg armour: the palace guard chest reads 0/48/8/19, and
  a Mirkwood recruit no longer loses armour when he upgrades to a warden.
- 2026-11-17: A piece the restat raises into the heavy band from below it takes its arm and leg armour
  from the legacy row, or from its line's heavier pieces, in place of 0. Band clones are priced from
  their source as the restat leaves it, and the lord templates follow the stronger elite troops
  (`fix_lord_kits.py --apply --values planned`).
- 2026-11-17: Armour band clones and upgrades: a piece a level 36+ troop and a level 30- troop both wear
  is no longer priced down for the top-tier troop. An elite piece gets a clone per low band; a low piece
  gives its top-tier wearers a stronger piece of the same line (`tools/generate_armour_band_clones.py`,
  gates `armour-band-clones` and `armour-band-clone-meshes`, warning `TOP_TIER_ARMOUR_WORN_LOW`).
- 2026-11-15: Lord armour variants: TAOM copies of Armory pieces at the lord band, worn only by the lord
  templates (`tools/generate_lord_armour_variants.py`, gate `lord-armour-variants`); Mirkwood's four
  looks are back and each Harad template has its own chest and helmet, with no part lower than
  before (#1333).
- 2026-11-15: Lord kit: a lord is judged by his worst whole battle set on head, body, arm and leg;
  the generic lord templates wear different lord pieces of their culture's pool, variety before armour
  points; the fallback and ruler rosters mirror template `_a`; a hero-only lord piece is priced at the
  lord band (`tools/fix_lord_kits.py`, warning `LORD_ARMOUR_RANGE`); companions stay light (#1271).
- 2026-11-15: Heavy, elite and lord arm, leg and shoulder pieces roll `leather`, not `chain` (open
  decision 1 of #1271). The group sets the roll of a troop at spawn in a campaign battle as well as
  loot. The clamp of hero-only and unworn kit moves its extremities off `chain` and `plate` too, and
  `rebalance_armor.py --check` (gate `armour-restat`) compares the live Armory with the curve.
- 2026-11-14: Bands, secondaries, hero clamps and worn limits for the vanilla armour range (#1271); the
  roster-first plan is one function; the wanderer templates count as heroes; the apply sequence gains
  the generator sync steps and the list of consumers outside the tools.

## GitHub Issue

- **Issue:** #1271: [Weapons and armour sit far above the vanilla range](https://github.com/haterade22/TAOM/issues/1271)
- **Status:** Open
- **Issue:** #1333: [Lord armour variants: Mirkwood and Harad lords get distinct looks at lord armour values](https://github.com/haterade22/TAOM/issues/1333)
- **Status:** Open
- **Issue:** #1450: armour band clone upgrades mixed silver with bronze (colour families)
- **Status:** Open
- **Issue:** #1452: [Armour colour clashes older than the band clones, culture by culture](https://github.com/haterade22/TAOM/issues/1452)
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/black-numenorean.md](./black-numenorean.md)
- [docs/features/gondor-armor-revamp.md](./gondor-armor-revamp.md)
- [docs/features/starting-equipment-tuning.md](./starting-equipment-tuning.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/items-armor.md](../modding/items-armor.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)

<!-- backlinks-end -->
