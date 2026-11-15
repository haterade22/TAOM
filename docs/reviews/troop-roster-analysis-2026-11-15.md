# Troop roster analysis: skills and kit against vanilla (2026-11-15)

Read-only analysis for #343, the troop counterpart of the weapon and armour baseline
([rebalance proposal](weapon-armour-rebalance-proposal-2026-11-14.md)). Measured on the installed
v1.5.7 game and trunk at `12b21d5e3`. The script is not in the repo; the numbers below are its
output on that day. The breakdown of the 231 and the Item0 count in section 3 were measured again
the same day on `078b86761`, whose troop files are the same, with a second script that is not in
the repo either.

## What was measured

- **Vanilla:** the 119 troops of the six main culture trees (`basic_troop` and `elite_basic_troop`
  in `SandBoxCore/ModuleData/spcultures.xml`, followed through `upgrade_targets`).
- **TAOM:** the 1,120 troops in `Main/_Module/ModuleData/troops/troops_*.xml`, less the 12 on
  `SKIP_TROOP_IDS` in `tools/rebalance_troops.py` (creatures and bespoke mounts).
- **Kit:** the weapon classes in the four weapon slots of every battle set, from
  `taom_schema.build_item_class_registry`. No TAOM troop carries a weapon the registry cannot class.
- **Primary skill:** the highest weapon skill among the classes the troop carries.
- **Tier:** level 6 is tier 1, then one tier per five levels. Vanilla ends at level 31 (tier 6);
  TAOM ends at level 51 (tier 10).

## 1. Vanilla is a flat grid; TAOM starts higher and climbs 50% further

Primary weapon skill, median per tier (minimum and maximum in brackets):

| Tier | Level | Vanilla | TAOM | TAOM over vanilla |
|---|---|---|---|---|
| 1 | 6 | 20 (20 to 20) | 38 (20 to 53) | +18 |
| 2 | 11 | 40 (40 to 60) | 70 (27 to 95) | +30 |
| 3 | 16 | 70 (70 to 100) | 100 (65 to 145) | +30 |
| 4 | 21 | 100 (90 to 150) | 130 (78 to 180) | +30 |
| 5 | 26 | 130 (130 to 160) | 180 (90 to 220) | +50 |
| 6 | 31 | 210 (200 to 260) | 240 (90 to 280) | +30 |
| 7 | 36 | none | 285 (110 to 345) | |
| 8 | 41 | none | 340 (250 to 380) | |
| 9 | 46 | none | 375 (270 to 425) | |
| 10 | 51 | none | 390 (380 to 440) | |

Vanilla's regular lines end at tier 5 with 130. Tier 6 is the noble line only (six troops): the
Imperial Elite Cataphract and the Vlandian Banner Knight hold the vanilla maximum, Polearm 260.
Vanilla gives every culture the same number at a tier (20, 40, 70, 100, 130) and marks a culture's
strength with one troop a step higher (Aserai tier 2 at 50, Sturgia tier 3 at 80, the Fian Champion's
Bow 220).

The engine multiplies a blow by `1 + factor x skill` (OneHanded 0.0015, TwoHanded 0.0016, Polearm
0.0007, Bow 0.0011 per point, no Crossbow factor). The best vanilla troop therefore hits 1.39 times
harder with a sword than an unskilled one would (skill 260); a TAOM tier 10 troop at 390 hits 1.59
times harder. Athletics and Riding follow the same shape: vanilla Athletics is equal to the tier's
weapon number through tier 5, and TAOM's runs 38 to 225.

## 2. Vanilla troops are skilled in what they carry; one TAOM troop in five is not

In vanilla a carried class sits at the tier number and a class the troop does not carry sits at about
half of it: a second carried weapon has the same skill as the first (median ratio 1.00), the best
melee class that is not carried has 0.55 to 0.60 of the primary, and a launcher that is not carried
0.40. Two vanilla troops of 119 have their best melee skill on a weapon they do not carry.

TAOM's curve (`GROUP_BASELINES` in `tools/rebalance_troops.py`) is keyed by formation group, not by
kit. Infantry has three near-equal melee skills with Polearm on top (level 31: OneHanded 230,
TwoHanded 210, Polearm 235); Ranged has OneHanded as the second skill (165) and TwoHanded as a weak
one (100). The only kit-driven corrections are Bow against Crossbow and Polearm against TwoHanded.
So a swordsman is a slightly better spearman than swordsman, and an archer with a two handed sword
fights with his weak skill.

| Finding | Vanilla (119) | TAOM (1,108) |
|---|---|---|
| Best melee skill is on a class the troop does not carry | 2 (1%) | 231 (20%) |
| A carried melee class sits below one that is not carried | 4 (3%) | 62 (5%) |
| A launcher is carried and is not the top weapon skill | 1 | 89 (8%) |
| The top weapon skill of any kind is not carried | 2 (1%) | 173 (15%) |

The 231 by kind: 121 carry only a one handed weapon and have Polearm on top (the class #343
recorded as 108); 64 carry only a two handed weapon and have OneHanded on top; 21 carry only a
polearm and have OneHanded on top; 10 carry a polearm and a two handed weapon with OneHanded on top;
10 carry only a one handed weapon with TwoHanded on top; 2 carry only a polearm with TwoHanded on
top; 2 carry only a two handed weapon with Polearm on top; 1 carries a one handed weapon and a
polearm with TwoHanded on top. A class tied for the top counts as on top.

The player report on Lothlorien is confirmed in the data:

| Troop | Carries | OneHanded | TwoHanded | Polearm | Bow |
|---|---|---|---|---|---|
| `lorien_mw_archer` (Marchwarden line, level 31) | bow, and a two handed sword in one set, a one handed sword in the other | 195 | 130 | 125 | 255 |
| `lorien_gl_archer` (Galadhrim bow line, level 36) | bow and a two handed sword | 260 | 235 | 265 | 320 |
| `lorien_gl_blademaster` (Galadhrim two handed line, level 41) | two handed sword in Item0, one handed sword | 355 | 325 | 315 | 70 |

The widest gaps are archers with a two handed side weapon: `arthedain_steel_bowman` OneHanded 220
against TwoHanded 80, `gondor_brv_shadowbow` 230 against 145, `imladris_marchwarden` 255 against 180.

## 3. Item0 is the weapon in the hand at spawn

`Equipment.GetInitialWeaponIndicesToEquip` walks the slots in the order extra slot, Item0, Item1,
Item2, Item3 and takes the first item that is not held in the off hand as the main hand weapon
(`mainHandWeaponIndex == EquipmentIndex.None` and not `ItemFlags.HeldInOffHand`).
`Agent.WieldInitialWeapons` wields it. So the Item0 weapon is the one a troop starts the battle
with, and it is the first weapon the encyclopedia and the party screen show.

Vanilla's convention, counted over battle sets:

| Kit | Vanilla Item0 | TAOM Item0 |
|---|---|---|
| Archer | bow 78, one handed 3 | bow 638 |
| Crossbowman | crossbow 16 | crossbow 68 |
| Horse archer | bow 6 | bow 55 |
| Cavalry | polearm 15, one handed 6 | polearm 376, one handed 21 |
| One handed only | one handed 54 | one handed 665, **thrown 27** |
| Two handed, no one handed | two handed 13 | two handed 188, polearm 49 |
| One handed and two handed | two handed 10, one handed 3 | one handed 102, two handed 48 |
| Spear and one handed | polearm 107, one handed 43 | polearm 510, one handed 217, two handed 5 |

TAOM follows the convention for launchers and lances. The 27 battle sets with a javelin in Item0
are not breaks (corrected 2026-11-15): they belong to eight thrower troops (`gondor_loss_skirmisher`,
`gondor_loss_axe_thrower`, `gondor_loss_vet_axe_thrower`, `gondor_har_skirmisher`,
`gondor_har_vet_skirmisher`, `gondor_har_javelineer`, `sagarun_naffatun`,
`sagarun_storm_helmed_naffatun`) whose javelin is their main weapon, and the Marshal's Word thrower
trial passed with the javelineers holding it. Vanilla's skirmisher minor factions do the same:
`hidden_hand_tier_1` to `_3` and `beni_zilal_tier_1` to `_3` (SandBoxCore `spnpccharacters.xml`)
open every battle set with a thrown weapon beside a one handed weapon. The mixed rows
are a design choice per troop (a swordsman who carries a spare spear, or a spearman with a side
sword), and the skills must then agree with the choice: on 158 troops the Item0 class (the class
most battle sets open with) sits below the troop's best carried class. The 173 of section 2 is a
different count: troops whose top weapon skill of any kind is on a class they do not carry.

## 4. Culture strength is flat for men and orcs, and one culture is broken

Median primary skill by tier:

| Culture | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | T9 | T10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Gondor | 38 | 70 | 100 | 135 | 185 | 240 | 280 | 320 | 360 | 380 |
| Rohan | 38 | 70 | 110 | 147 | 195 | 260 | 310 | 350 | | |
| Dale | 53 | 95 | 120 | 155 | 195 | 250 | | | | |
| Erebor | | 75 | 100 | 135 | 172 | 245 | 285 | 320 | 340 | |
| Iron Hills | | 75 | 105 | 130 | 175 | 255 | 297 | 330 | 350 | |
| Rivendell, Lindon | | | | 160 | 220 | 267 | 320 | 355 | 410 | 390 |
| Lothlorien | | | | 180 | 220 | 262 | 320 | 355 | 375 | |
| Mirkwood | | | 145 | | | | 310 | 350 | 385 | 395 |
| Isengard | 40 | 75 | 100 | 145 | 185 | 250 | 290 | | | |
| Mordor | 28 | 45 | 90 | 125 | 185 | 235 | 280 | 320 | 345 | |
| Gundabad | 28 | 47 | 90 | 120 | 175 | 250 | 280 | 345 | | |
| Rhun | 28 | 60 | 90 | 142 | 180 | 245 | 290 | 340 | 345 | |
| Dunland | 33 | 65 | 95 | 130 | 170 | 235 | | | | |
| **Harad** | 42 | 54 | 70 | 85 | 90 | 105 | 124 | | | |

- The Elves start at tier 4 and sit 25 to 45 above the men at each tier, as `CULTURAL_MODS` intends.
- Dwarves, men and orcs are within about 15 of each other from tier 5 up. A culture's strength
  shows in one or two skills of the modifier table (Rohan Riding +20, Dale Polearm +25, Erebor
  TwoHanded +20), and section 2 shows that the troop often does not carry the weapon that skill
  belongs to.
- **Harad is at about half of every other culture** from tier 3 up (a level 31 Serpent Guard has
  Polearm 105 where Gondor has 240). 15 of the 32 troops under 60% of their tier's median are Harad.
- Nine culture files have no modifier row at all (`anduinvale`, `angaladh`, `arthedain`, `cardolan`,
  `dorwinion`, `eredluin`, `feral_orcs`, `rhudaur`, `southrhovanion`).
- The realm generator (`tools/realm_troops.py`) gives a two handed troop the low curve on TwoHanded
  at level 11: `arthedain_dunadan_youth` and `cardolan_dunadan` carry a two handed weapon with
  TwoHanded 27 and OneHanded 70.
- Several level 11 archers have no bow skill to speak of: `dunland_hunter` Bow 15, `goblin_hunter`,
  `gundabad_hunter` and `feral_orcs_archer` 35.

## Recommendation

Two changes, separate because the second moves battle results and the first does not.

**A. Purpose first.** Superseded in part by the tie rule; what shipped is in the
[RCA](rca-troop-skill-purpose-2026-11-15.md). A troop's kit decides its skills. The class in Item0 is the troop's
main weapon and takes the troop's highest weapon skill; the other carried classes come next; the
classes it does not carry come last. On disk this is a reorder of the troop's own numbers, so no
troop gets stronger or weaker on paper, and the troops that carried their weak weapon get the skill
they were meant to have. Item0 holds a real weapon (a thrower's javelin counts), never a shield or
ammunition. A validator code keeps both rules, and the generators and `rebalance_troops.py`
produce them.

What shipped differs in one place. A reorder cannot settle a troop whose sets hold two alternative
weapons for one role, so those classes tie at the higher value, and the clamp then carries a raised
parent value down the tree. Measured on the troop files of `078b86761` against the result: skills
changed on 369 troops; the reorder kept the 8-skill total on 297 of them, and on 72 the total moved
by +1518 net: 21 by the tie rule (+712), 33 by the clamp cascade (+601) and 18 by the
regeneration of the Arthedain, Cardolan and Rhudaur realm files (+205; four of those went down).
"No troop gets stronger or weaker on paper" holds only for what `--fix-purpose` does to one troop's
own values; the realm files were regenerated, not reordered. The RCA lists the tie raises and the
troops whose Item0 class rose over a class they carry in every set.

**B. The level of the curve (maintainer's decision).** Vanilla spreads 20 to 260 over six tiers.
The same treatment the weapons got would spread the vanilla range over TAOM's ten tiers: about 20,
40, 65, 90, 115, 140, 170, 200, 230, 260, with the culture table scaled to match (Elves about 10%
above, orcs a few percent under) and each culture's strong skills kept. Carried skills at the tier
number, a class that is not carried at about half, as vanilla does. This lowers a tier 10 troop's
sword multiplier from 1.59 to 1.39 and its bow and riding skills with it, and the weapon and armour
values of #1271 were fitted on today's skills, so the damage matrix must be run again on the new
curve before anything is written. It also repairs Harad and the missing modifier rows in one pass.

Built (#343): the curve is 20, 40, 70, 100, 130, 156, 182, 208, 234, 260, with a role table and a
percent culture table in place of the group tables. The values, the damage tables and what differs
from this sketch are in [troop-skill-balance](../features/troop-skill-balance.md) and the
[RCA](rca-troop-skill-curve-2026-11-15.md).
