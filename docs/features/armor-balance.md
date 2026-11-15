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
armour on the next apply; a 0 stat is also modifier-immune, so no loot roll lifts it. Weights and
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
   2026-09-13 curve apply needed the same loop (58 edges, 197 slot swaps). The rosters changed, so go
   back to step 2, then step 3, and repeat steps 2 to 7 until step 3 plans `Changed: 0` and the
   report finds no edge (when step 3 still plans changes, step 4 again with the same `--backup-tag`,
   which keeps the first backup).
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
8. **REPO.** `python tools/analyze_armor_balance.py --stdout` (0 kingdom invariant violations) and
   `python tools/analyze_kingdom_armour.py --stdout`. Then `python tools/generate_armour_classes.py --apply`
   and `python tools/generate_armour_classes.py --check`: it classes pieces with `calculate_stats`
   and the derived map, so the committed `armour_classes.xml` is stale from the moment the curve
   changes.
9. **LIVE ARMORY, then REPO, then LIVE ARMORY.** The melee ladder, in this order. `python tools/restat_melee_blades.py`
   and `--apply` (the Armory's `LOTRLOME_crafting_pieces.xml`); `python tools/fix_melee_ladder.py` and
   `--apply` (REPO: troop rosters); then `python tools/restat_melee_blades.py` as a dry run again,
   which must report 0 blades to move. If it does not, repeat this step.
10. **REPO, carry the step 9 swaps into the troop generators.** `fix_melee_ladder.py --apply` rewrote
    troop rosters too. Repeat step 7, parts 1 to 4, for the troops it changed.
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
  [melee-damage-model.md](./melee-damage-model.md). The 318 ladder inversions stay until the tool
  holds the category.
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

The restat lives only in the unversioned Armory, and the mirror still holds KEYforce's stats by
Mike's rule, so a re-sync reverts it silently. Re-run the restat after any Gondor sync:
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

## Changelog

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
