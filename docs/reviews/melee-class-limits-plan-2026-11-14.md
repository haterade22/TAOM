# Melee weapon rebalance plan: class limits, ten tiers, hero blades

> **Status after the build (2026-11-14, #1271).** Parts 1 to 3 and the ranged half of Part 5 are
> built in the working tree as tools, specs and gates; the live Armory is not yet written. The
> rest of this document is the plan as it went to review and is kept as the record. Where the
> build differs:
>
> - **Part 2.** The tier share is `class_tier_fraction`, 0.58 at tier 0 rising to 1.0 at tier 10,
>   not the shared curve's shape (0.22 to 1.0). The culture offsets became a craft table with
>   three columns (sword, axe, polearm). The band is 15 percent, not 10. A tier limit (the class
>   limit times the same share and craft) also holds the blow and the DPS each tier may carry.
> - **Part 3.** `hero_blades` stays a list; `hero_makers` maps a blade to its group and
>   `hero_bonus` a group to its DPS bonus (+10, +8, +6, +5). The five Noldor swords and the Khamul
>   blades were added as hero blades.
> - **Part 4.** Not run. The Erebor shared one handed and two handed blades are not cloned, so the
>   restat leaves them unpriced by tier. `--limits-only` is built. The order of the apply is in
>   `armor-balance.md` "The apply sequence".
> - **Part 5.** The Bow and Crossbow damage rows are rescaled (no bow cell above 95, no crossbow
>   cell above 105). Thrown weapons are not measured. Part 6 is not built.
> - **Armour.** The target is what a troop wears in total: the best ordinary troop wears 67 on the
>   body and lord kit at most 5 above it, not 72 and 77. The chest caps come from the maintainer's
>   kingdom table (Erebor 52 down to Thenn 35), not Recommendations 1 and 2.
>
> The current truth is in [armor-balance.md](../features/armor-balance.md) and
> [melee-damage-model.md](../features/melee-damage-model.md).

Date: 2026-11-14. Status: plan for adversarial review. Part 1 is built and uncommitted in the
working tree; Parts 2 to 6 are not built. No GitHub issue yet (work claim key
`melee-class-envelope`).

## What the reviewer is asked to do

Attack this plan before more of it is built. Report defects and better designs with evidence. Do
not fix anything, do not write to the live Armory, and do not commit. The numbers below come from
`tools/melee_catalogue.py` runs on the live install on 2026-11-14; rerun them rather than trust
them (commands in "How to check the numbers"). The questions the author is least sure of are in
"Where this plan is weakest".

## The problem

Mike's report: after the v2.0.38 weapon work, some one handed swords do 140 damage. Most troops
have 100 health, so a one handed weapon should not kill one in a single blow.

Root cause, confirmed in the data:

1. The melee ladder (`tools/melee_ladders.json`, #631) is one sustained-damage (DPS) curve for
   every weapon class: 25 DPS at tier 0, 69 at tier 5, 114 at tier 10, band plus or minus 25%.
2. Nothing limited the damage of one blow.
3. `tools/generate_placeholder_weapons.py` and `tools/restat_melee_blades.py` set a blade's
   `damage_factor` to `tier target DPS / current DPS`. A slow one handed sword reaches 119 DPS
   only with a 140 swing (`taom_ph_mirkwood_sword_t10`). Shield spears reached 123 per thrust,
   because a thrust carries a 0.67 s recovery.

Vanilla never does this. Its one handed weapons stop at 83 DPS; only two handed weapons pass 100.

## Mike's requirements

1. Use vanilla as the baseline: min and max damage and DPS per class, speed included.
2. A one handed weapon must not one-shot a 100 health troop.
3. TAOM has 10 troop tiers (vanilla has 6). The weapons of each class must spread over all 10.
4. Hero blades sit slightly above the vanilla class maximum: 5 to 10 DPS above, never more than
   10. The size of the bonus follows the culture's craft (Elven and Dwarven heroes highest).

## Vanilla baseline

261 melee weapons in SandBoxCore and SandBox, none failed to price. Values are min / median /
90th percentile / max. The low ends are practice and tournament weapons.

| Template | n | Swing blow | Thrust blow | Swing DPS | Thrust DPS |
|---|---|---|---|---|---|
| Dagger | 7 | 10/17/41/41 | 28/51/63/63 | 12/19/46/46 | 25/46/56/56 |
| Mace | 30 | 18/55/67/71 | 12/15/26/26 | 18/48/56/58 | 10/13/22/22 |
| OneHandedAxe | 26 | 33/64/78/83 | none | 34/62/68/70 | none |
| OneHandedSword | 85 | 17/71/84/103 | 12/38/49/54 | 18/60/70/83 | 11/33/41/47 |
| Pike | 5 | none | 26/41/42/42 | none | 22/34/35/35 |
| TwoHandedAxe | 17 | 66/116/136/139 | none | 57/92/98/103 | none |
| TwoHandedMace | 5 | 38/73/85/85 | none | 30/52/59/59 | none |
| TwoHandedPolearm | 62 | 59/121/182/184 | 16/40/47/49 | 44/97/127/138 | 14/34/40/43 |
| TwoHandedSword | 24 | 20/102/133/140 | 13/48/57/57 | 22/99/118/132 | 12/42/49/50 |

Ranged, from item XML (Native, SandBoxCore, SandBox, no multiplayer items): bows 30/51/69/95
damage at speed rating 80 to 96; crossbows 57/80/100/100 at 60 to 80. TAOM reaches 133
(`ladder_mirkwood_bow_t10`) and 126 (`ladder_gondor_xbow_t8`).

## Part 1, built and uncommitted: class limits

`class_limits` in `tools/melee_ladders.json`, keyed by crafting template. Four numbers each: the
displayed damage of one blow (`swing`, `thrust`) and the sustained damage of that attack
(`swing_dps`, `thrust_dps`). null means vanilla has no such attack in the class.

| Template | swing | thrust | swing_dps | thrust_dps |
|---|---|---|---|---|
| Dagger | 45 | 65 | 46 | 56 |
| Mace | 75 | 30 | 60 | 25 |
| OneHandedAxe | 85 | null | 70 | null |
| OneHandedSword | 95 | 55 | 83 | 47 |
| Pike | null | 60 | null | 45 |
| TwoHandedAxe | 140 | null | 105 | null |
| TwoHandedMace | 110 | null | 80 | null |
| TwoHandedPolearm | 185 | 60 | 138 | 45 |
| TwoHandedSword | 140 | 60 | 132 | 50 |

Departures from the vanilla maximum, each a choice the reviewer may reject:

- OneHandedSword swing 95, not 103. The 103 is one weapon (`battania_noble_sword_1_t5`); the
  next two are 95 and 86. 95 keeps requirement 2.
- TwoHandedMace 110 and 80, not 85 and 59. Vanilla has five, too few to copy; the class sits
  between the one handed mace and the two handed axe.
- Polearm and pike thrust 60 and 45, not 49 and 43. The couched charge is outside the model.

Code:

- `tools/melee_ladder.py`: `class_limit`, `max_factor` (the largest `damage_factor` multiplier
  inside the limits; blow and DPS are both linear in the factor), `limit_breaches`,
  `blow_findings`, and spec validation.
- `tools/generate_placeholder_weapons.py`: the multiplier stops at `max_factor`; a placeholder
  above a limit is refused. Regenerated: Mirkwood swords 118 and 140 swing became 94 (80 DPS),
  Isengard sword 99 became 93, Gondor shield spears 87 to 123 thrust became 54 to 56.
- `tools/restat_melee_blades.py`: worn and unworn blades stop at the limits, swing factor and
  thrust factor each by its own limits. Not applied to the Armory.
- `tools/taom_schema.py`: warning `MELEE_BLOW_CEILING` in the install-gated melee pass.
- Tests: `tools/tests/test_melee_ladder.py`, `tools/tests/test_restat_melee_blades.py`.

Verified on that code: `python -m pytest tools/tests -q` 9086 passed, 13 skipped; the
placeholder C# tests 51 passed; `python tools/validate_moduledata.py` exit 0. Not run: the full
C# suite, `/deep-review`, any game check.

State of the live Armory against these limits (384 weapons): 97 above a limit, of which 12 are on
hero blades, 46 are carried by troops and 39 are in shops and loot only. By class: polearm 22,
one handed sword 19, two handed mace 14, two handed sword 12, two handed axe 8, mace 5, one
handed axe 3, pike 2. Thrust is the common breach (55 weapons above `thrust_dps`).

## Part 2, not built: one curve per class, over ten tiers

Part 1 alone breaks requirement 3. The shared curve passes the one handed sword limit near
tier 6, so every tier 6 to 10 swordsman would carry the same 83 DPS sword, and the band gate
would call them all under-armed.

Proposal: keep the SHAPE of the shared curve and scale it to each class, so tier 10 of the best
culture lands on the class maximum.

`target(class, tier, culture) = class_max_dps x curve[tier] / curve[10] x (1 + offset[culture]) / (1 + max offset)`

`class_max_dps` is the larger of the class's `swing_dps` and `thrust_dps` that the weapon can
use. `curve[tier] / curve[10]` is 0.22 at tier 0, 0.61 at tier 5, 1.0 at tier 10. The culture
offsets (Rivendell +10% down to Dol Guldur -3%) stay as ratios, so cultures still differ at
tier 10 and none passes the class maximum.

Resulting targets for the top culture (DPS):

| Class | T1 | T3 | T5 | T7 | T10 | Vanilla median, max |
|---|---|---|---|---|---|---|
| OneHandedSword | 25 | 38 | 50 | 63 | 83 | 60, 83 |
| OneHandedAxe | 21 | 32 | 42 | 53 | 70 | 62, 70 |
| Mace | 18 | 27 | 36 | 46 | 60 | 48, 58 |
| TwoHandedSword | 39 | 60 | 80 | 101 | 132 | 99, 132 |
| TwoHandedAxe | 31 | 48 | 64 | 80 | 105 | 92, 103 |
| TwoHandedMace | 24 | 36 | 48 | 61 | 80 | 52, 59 |
| TwoHandedPolearm, swing | 41 | 63 | 84 | 105 | 138 | 97, 138 |
| Polearm and pike, thrust only | 13 | 21 | 27 | 34 | 45 | 34, 43 |

Consequences:

- Tiers 1 to 6 get weaker than today and than vanilla's middle. A tier 5 swordsman goes from a
  69 DPS target to about 50. Fights between low and middle troops last longer.
- The steps are small for the low classes (a one handed sword gains about 6 DPS per tier), so
  the band must narrow from 25% to about 10%, or adjacent tiers overlap completely.
- The blow limits stay as absolute ceilings. With a fixed blade speed, blow scales with DPS, so
  a weapon on its tier target is under the blow limit unless it is unusually slow.
- Placeholders regain a spread: the T5 and T8 Gondor spears and the T8 and T10 Mirkwood swords
  stop being equal.

Alternative the reviewer should weigh: a linear curve from the vanilla median at tier 3 to the
maximum at tier 10, flat below tier 3. It keeps the middle tiers at vanilla strength and gives
the spread only to tiers 3 to 10.

Changes: `melee_ladder.target` and `band` take the weapon's template; `findings` receives it in
each kit row; `fix_melee_ladder.py`, `restat_melee_blades.py`, `generate_placeholder_weapons.py`,
`taom_schema.py` and `analyze_melee_ladder.py` pass it. The `ceiling` of 140 stays as a
backstop. Tests first, in `tools/tests/test_melee_ladder.py`.

## Part 3, not built: hero blades

Rule: a hero blade's DPS limit is the class maximum plus a culture bonus of 5 to 10. Its blow
limits rise by the same proportion (one handed sword at +10: 83 becomes 93 DPS, and 95 becomes
106 swing). A hero blade is exempt from the tier curve but not from this limit.

Proposed bonus by maker, for Mike to correct:

| Bonus | Makers | Hero blades today |
|---|---|---|
| +10 | High Elven (Noldor, Rivendell, Lindon, Lorien, Gondolin heirlooms); Dwarven (Erebor, Ered Luin) | Glamdring, Galadriel, Aranruth (3), Tuor's axes; candidates Celegorm, Finarfin, Voronwe, Ingwe, Turin |
| +8 | Numenorean (Gondor, Arnor); Silvan and Sindar (Mirkwood); Sauron and the Nazgul | Anduril, Strider, Boromir, Faramir, Legolas, Thranduil, Sauron's Mace, Witch King, Nazgul; candidates Khamul set |
| +6 | Rohan, Dale | Theoden, Eomer, Eowyn |
| +5 | every other culture | none today |

Spec shape: `hero_blades` becomes a mapping of blade piece to maker group, and `hero_bonus` a
mapping of group to DPS. The restat tool lowers a hero blade above its limit and leaves one below
it alone unless Mike says heroes should be raised.

Facts the reviewer should check against this rule (live Armory, 2026-11-14):

- Above the hero limit, so they come down: `wm_sauron_mace` 165 DPS and 193 swing (limit 68);
  `wm_tuors_axe` 119 DPS and 174 swing (limit 115); `wm_turin_sword` 139 DPS, 86 thrust if it
  becomes a hero blade (limit 142, thrust above).
- Below the plain class maximum already: `nazgul_sword` 81 and `witchking_sword` 80 against 132;
  `wm_legolas_sword` and `wm_thranduil_sword` 64 against 83. The rule permits a raise and does
  not require one.
- Hero thrusts are above the thrust limits even with the bonus: Galadriel 72 thrust (62 DPS),
  Legolas 76 (66 DPS), against a hero limit of about 61 thrust and 57 DPS.
- `wm_gondor_boromir_sword` is on a hero blade and 14 Gondor line troops carry it. That is a
  ladder fault already. Those troops need a non-hero sword, or the blade leaves the hero list.
- Candidates not in `hero_blades`: the five Noldor swords and the Khamul greatswords are carried
  by no troop. `sm_dg_khml_1h_mace_b` is carried by 6 Dol Guldur troops and
  `wm_gf_knight_longsword` and `wm_gf_knight_spear` by 13 and 4 Elven troops, so those three are
  troop weapons and take the normal limits.

## Part 4, not built: the Armory write

The Armory (`LOTRLOME_Armory`) is unversioned and lives in the game install. Order:

1. Give the two Erebor one handed axes their own blades. `sm_dwarf_erebor_1h_axe_a2` and `_b2`
   use two handed blade pieces, so lowering those blades to the one handed limit would also take
   the damage from the two handed axes on them. Clone the blades in the repo, as the placeholder
   generator does.
2. Add `--limits-only` to `restat_melee_blades.py`: lower only weapons above a class or hero
   limit, raise nothing. In the dry run of Part 1 that is 61 of 89 blades. The other 28 are older
   ladder drift and must not ride along.
3. Dry run, read the list, then `--apply` (backup file first, never a `.xml` extension), on
   Mike's word.
4. After Part 2 lands, a second restat moves blades onto the per-class tier targets. This is the
   large one: it touches most worn blades. Then `fix_melee_ladder.py`,
   `generate_career_kits.py --apply`, `wire_starter_kit_rosters.py --apply`,
   `generate_starter_kit.py --apply`, `generate_placeholder_weapons.py --apply`.
5. Gates: `validate_moduledata.py` shows no `MELEE_BLOW_CEILING`; `data_gates.py`; both test
   suites; `/deep-review`; then the game check (full restart, tooltips of a Mirkwood tier 9
   swordsman, a Rivendell black sword and an Erebor axe, and one custom battle).

A reinstall of the Armory reverts every blade. The gate is the only guard, and it is a warning.

## Part 5, not built: ranged and thrown

- Bows and crossbows: the ranged ladder (`tools/ranged_ladders.json`) has its own ten tier curve
  and already passes vanilla (bow 133 against 95, crossbow 126 against 100) and its own
  `hero_ceiling` (bow 90, crossbow 105). A plain cap would flatten its top tiers. Proposal: rescale
  the damage rows so the best line at tier 10 lands on the vanilla maximum (bow 95, crossbow 105),
  keeping the rule that a crossbow out-hits its kingdom's bows.
- Thrown weapons (javelin, throwing axe, throwing knife) are crafted, and `melee_catalogue` does
  not price their templates. They have no baseline yet.

## Part 6, not built: reporting

`analyze_melee_ladder.py` prints the vanilla envelope per class with blow and both DPS columns,
so the baseline table in `docs/features/melee-damage-model.md` regenerates from the tool.

## Armour baseline (added 2026-11-14, read-only analysis, nothing changed)

Measured from item XML and troop rosters on the live install. Vanilla is Native, SandBoxCore,
SandBox and StoryMode without multiplayer items; TAOM is `LOTRLOME_Armory` plus the TAOM module,
hero and troll items included. Values are min / median / 90th percentile / max.

Troll kit is out of scope (Mike, 2026-11-14): troll weapons and troll armour are made for troll
units only and take no class limit and no armour cap. The weapon ladder already exempts
`cave_troll` and `hill_troll` (`exempt_troops`), and the troll armour folder has no kingdom cap.
In the tables below, the TAOM maximum of 95 is the troll set; the highest non-troll values are
named hero sets at 80 to 90, then troop kit (helmet 63, body 70, gloves 50, boots 45).

### Per item, by slot

| Slot (stat) | Vanilla n | Vanilla | TAOM n | TAOM |
|---|---|---|---|---|
| Helmet (head) | 264 | 1/32/48/55 | 1067 | 8/41/53/95 |
| Body (body) | 260 | 1/14/39/57 | 844 | 5/37/57/95 |
| Body (arm) | 241 | 1/3/14/55 | 780 | 1/16/38/95 |
| Body (leg) | 256 | 1/5/16/55 | 327 | 5/24/43/95 |
| Boots (leg) | 41 | 1/5/20/26 | 363 | 5/18/34/46 |
| Gloves (arm) | 36 | 3/15/24/28 | 362 | 6/27/40/95 |
| Shoulder or cape (body) | 94 | 1/6/14/16 | 530 | 4/28/41/56 |
| Shoulder or cape (arm) | 84 | 1/7/12/14 | 530 | 1/22/34/47 |
| Shield (hit points) | 99 | 80/300/510/750 | 268 | 220/400/600/1000 |
| Horse harness (body) | 34 | 8/16/68/77 | 69 | 10/35/68/80 |

Weights: vanilla body armour median 2.3 (max 40), TAOM 13.0 (max 27). The largest gap is the
shoulder slot: the best vanilla shoulder gives 16 body armour, the best TAOM pauldron 56 (the
kingdom-cap model sets a pauldron at 0.6 of the chest, `docs/features/armor-balance.md`).

### What a troop wears: the sum of its pieces per body part, by tier

Engine tier is `clamp(ceil((level - 5) / 5), 0, 10)`. Each troop is the mean of its battle
rosters. Values are min / median / max. Vanilla has 268 troops, TAOM 1119.

| Tier | Vanilla n | Vanilla head | Vanilla body | Vanilla arm | Vanilla leg | TAOM n | TAOM head | TAOM body | TAOM arm | TAOM leg |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 8 | 0/2/10 | 4/5/12 | 1/2/8 | 6/7/10 | 51 | 0/14/25 | 6/23/29 | 0/5/24 | 6/9/27 |
| 2 | 57 | 0/5/22 | 3/9/24 | 0/4/13 | 0/9/14 | 129 | 0/16/43 | 14/25/50 | 0/19/33 | 8/11/37 |
| 3 | 87 | 0/8/40 | 8/19/48 | 3/13/35 | 5/11/35 | 185 | 12/23/43 | 18/41/77 | 0/35/90 | 8/18/52 |
| 4 | 61 | 0/33/52 | 6/30/61 | 2/21/48 | 7/17/42 | 181 | 14/33/53 | 24/52/94 | 0/46/110 | 8/18/62 |
| 5 | 49 | 0/42/54 | 20/37/67 | 10/32/59 | 13/34/49 | 193 | 14/39/53 | 33/61/94 | 8/58/113 | 8/24/69 |
| 6 | 6 | 43/50/54 | 52/60/67 | 27/49/53 | 19/39/47 | 152 | 16/43/63 | 38/82/112 | 25/71/133 | 12/29/72 |
| 7 | none | | | | | 114 | 23/51/63 | 39/91/112 | 25/80/132 | 8/38/83 |
| 8 | none | | | | | 61 | 35/51/63 | 63/91/112 | 55/87/137 | 13/51/82 |
| 9 | none | | | | | 32 | 35/52/63 | 63/91/112 | 56/88/137 | 13/58/82 |
| 10 | none | | | | | 16 | 18/30/95 | 44/55/109 | 46/69/190 | 13/51/95 |

Vanilla's most armoured troop has 67 on the body (`aserai_vanguard_faris`). TAOM's has 112 (the
Erebor noble and oathsworn lines). Median body armour of TAOM troops at tier 8 and above, by
culture id: erebor 112, lindon 109, rivendell 109, gondor 91, mordor 91, khuzait (Rhun) 91,
lothlorien 90, gundabad 78, dolguldur 74, isengard 72, vlandia (Rohan) 66, aserai (Harad) 64,
mirkwood 64. Heads and legs are close to vanilla; body and arm are far above it.

Limits of this measure: it ignores troop-level `<equipment>` overrides, it averages the battle
sets, and tier 10 holds only 16 troops (trolls among them).

### Weapons against that armour

Damage that reaches the troop, from the formula in `docs/modding/balance-levers.md` ("Armour into
damage"; `SandboxStrikeMagnitudeModel.ComputeRawDamage`), for a standing blow with no skill or
speed bonus:

| Blow | No armour | Vanilla T3 (19) | Vanilla T5 (37) | Vanilla T6 (60) | TAOM T5 (61) | TAOM T7 to T9 (91) | Erebor elite (112) |
|---|---|---|---|---|---|---|---|
| One hand sword, cut 71 (vanilla median) | 71 | 43 | 24 | 5 | 5 | 3 | 2 |
| One hand sword, cut 95 (class limit) | 95 | 60 | 38 | 16 | 15 | 3 | 3 |
| Two hand sword, cut 140 (class limit; the old placeholder sword) | 140 | 93 | 64 | 37 | 36 | 9 | 4 |
| Polearm, cut 185 (class limit) | 185 | 126 | 90 | 57 | 56 | 25 | 7 |
| One hand mace, blunt 75 (class limit) | 75 | 53 | 40 | 29 | 29 | 19 | 14 |
| Two hand mace, blunt 110 (class limit) | 110 | 78 | 60 | 45 | 45 | 32 | 25 |
| Spear, pierce 60 (class limit) | 60 | 39 | 25 | 12 | 12 | 5 | 5 |
| Bow, pierce 95 (vanilla maximum) | 95 | 64 | 45 | 28 | 28 | 11 | 7 |
| TAOM bow, pierce 133 | 133 | 92 | 67 | 46 | 45 | 25 | 13 |

### Reading

1. TAOM body armour passes the vanilla maximum (67) at tier 4 for the best troops and at tier 6
   for the median troop. From tier 7 the median troop wears 91, one and a half times vanilla's best.
2. Against 91 body armour a cut at the vanilla weapon limits does almost nothing: 3 from a one
   hand sword, 9 from a two hand sword. A 100 health elite takes more than 30 sword blows on the
   body. This is why the weapon ladder was pushed to 119 DPS and a 140 swing: the high weapon
   numbers were compensating for the armour.
3. So the weapon limits cannot be set from vanilla alone while the armour stays where it is. The
   two must move together. Either the armour comes down toward the vanilla envelope (the body sum
   of the best troop near 67 to 75, which mostly means the pauldron's body armour), or the weapon
   limits scale up with the armour, which brings the one-blow kill on unarmoured troops back.
4. The one-blow problem and the armour are the same problem seen from two sides: low tier troops
   (body 23 to 41) die to weapons tuned to hurt high tier troops (body 91 to 112).

## Direction, 2026-11-14: back to the vanilla range, ranked by craft

Mike's direction: bring weapons AND armour back to the vanilla range, and let each culture's
smithing decide where it sits in that range. Dwarven and Elven smiths are the best; every culture
has strengths and weaknesses. Trolls stay out. The three recommendations below are the author's
and wait for Mike's confirmation. This section replaces the idea of raising weapon limits to
match the armour.

`stat = vanilla class maximum x tier step x craft factor`, with the craft factor 1.00 for the best
culture in a category. Hero items add 5 to 10 above the class maximum by the same rank.

Engine limits that shape the numbers:

- Armour values and displayed damage are whole numbers. A 3% craft step is invisible on a small
  stat (a 16 point shoulder), so the ranking shows on the chest and helmet, and craft steps under
  about 5% do nothing on most items.
- Armour is not linear, and cut damage is the most sensitive. Against a 95 cut, a 73 point body
  takes about 6 and a 51 point body about 24. A modest gap in armour is a large gap in
  survival against swords and a small one against maces. A wide craft spread on armour is a
  much stronger lever than the same spread on weapons.
- Item price and item tier are computed from the stats (`DefaultItemValueModel`), so shops and
  troop costs follow.
- Item modifiers, skills, perks and player smithing multiply on top; the limits are for the
  base item.
- Weapon damage sits on the blade piece, one blade can back several weapons, and the item's
  `culture=` is unreliable in the Armory. The craft factor is read from the troop culture that
  carries the weapon, as `kingdom_offset` is today.

### Correction, later on 2026-11-14: the target is the troop's total, not the item maximum

Mike: a player with a very good sword who does 6 damage to an elite would be angry. The table in
Recommendation 2 stacks the vanilla chest maximum (57) and shoulder maximum (16) and so gives 73,
above vanilla's best troop (67). The target is now a body SUM of 60 for the best troop (vanilla's
tier 6 median): top chest 47, same slot ratios, same craft factors. A 95 cut then does 16 to a
Dwarven elite and about 33 to an orc elite. The current numbers are in
`weapon-armour-rebalance-proposal-2026-11-14.md`, part 1; read the tables below with chest 47
in place of 57.

### Recommendation 1: Dale's armour rises to 0.82

Dale sits at 0.63 today, level with Harad. The Men of Dale traded with Erebor and worked beside
its smiths, so they rank above the other Men of the north and east and just below the Numenorean
realms.

### Recommendation 2: keep the armour order, raise the floor to 0.70

Top chest 57 (the vanilla maximum), shoulder 0.28 of the chest, bracer 0.5, greaves 0.45,
helmet 0.9. The existing kingdom order stays; the factors are compressed so the weakest kingdom
is at 0.70 and not 0.54: `new = 0.70 + (old - 0.54) x 0.30 / 0.46`. Elite band values:

| Culture | Factor today | Proposed | Chest | Body sum (chest + shoulder) | 95 cut does | 140 cut does | 75 blunt does |
|---|---|---|---|---|---|---|---|
| Erebor, Iron Hills | 1.00 | 1.00 | 57 | 73 | 6 | 24 | 25 |
| Rivendell, Lindon | 0.97 | 0.98 | 56 | 72 | 7 | 25 | 25 |
| Mirkwood | 0.90 | 0.93 | 53 | 68 | 10 | 29 | 26 |
| Lothlorien | 0.86 | 0.91 | 52 | 67 | 10 | 30 | 27 |
| Gondor, Arnor, Rhun, Black Numenoreans | 0.81 | 0.88 | 50 | 64 | 13 | 33 | 28 |
| Dale | 0.63 | 0.82 | 47 | 60 | 16 | 37 | 29 |
| Gundabad | 0.70 | 0.80 | 46 | 59 | 17 | 38 | 30 |
| Dol Guldur | 0.66 | 0.78 | 44 | 56 | 20 | 41 | 31 |
| Isengard | 0.64 | 0.77 | 44 | 56 | 20 | 41 | 31 |
| Harad, Umbar | 0.63 | 0.76 | 43 | 55 | 20 | 42 | 31 |
| Mordor Black Uruks | 0.61 | 0.75 | 43 | 55 | 20 | 42 | 31 |
| Rohan, Dunland | 0.57 | 0.72 | 41 | 52 | 23 | 45 | 33 |
| Mordor orcs, goblins | 0.54 | 0.70 | 40 | 51 | 24 | 46 | 33 |
| Thenn | 0.50 | 0.67 | 38 | 49 | 26 | 49 | 34 |

"Does" is the damage that reaches the body from a standing blow. A Dwarven elite takes about 6
from a 95 cut. With the floor left at 0.54 an orc elite (body 40) would take about 35, nearly
six times as much; at 0.70 it takes about 24, four times as much. Vanilla's own top tier spans
body 52 to 67, which is 23 against 10 from the same cut, so a gap of this kind is normal in the
engine, but a floor below 0.70 makes the weak kingdoms' elites die like recruits. The orc
kingdoms answer with numbers, and that already holds in the party sizes. Blunt damage barely
sees the spread (25 against 34), so maces and hammers are the answer to Dwarven plate.

### Recommendation 3: three melee craft columns, and the ranged ranks that already exist

One weapon rank per culture makes the Dwarves second at everything. Three columns give each
culture a profile at a small cost: swords and daggers; axes, maces and hammers; spears and
polearms. The restat already prices each weapon by its crafting template, so the column is one
lookup. Bows and crossbows already have their own per-line ranks in `tools/ranged_ladders.json`
and are not duplicated here. The spread is about 20% (1.00 to 0.80), in steps of 3 to 5%.

| Culture | Swords | Axes, hammers | Spears, polearms |
|---|---|---|---|
| Rivendell, Lindon | 1.00 | 0.90 | 0.97 |
| Lothlorien | 0.97 | 0.85 | 0.95 |
| Erebor, Iron Hills | 0.95 | 1.00 | 0.92 |
| Gondor, Arnor | 0.93 | 0.88 | 0.93 |
| Black Numenoreans | 0.93 | 0.88 | 0.90 |
| Mirkwood | 0.92 | 0.85 | 0.95 |
| Dale | 0.88 | 0.88 | 0.88 |
| Rohan | 0.88 | 0.85 | 0.95 |
| Rhun, Harad | 0.88 | 0.85 | 0.90 |
| Isengard | 0.87 | 0.87 | 0.90 |
| Mordor, Gundabad, Dol Guldur | 0.82 | 0.85 | 0.82 |

## Where this plan is weakest

0. **Armour, the largest gap.** See "Armour baseline": at the vanilla weapon limits an elite
   swordsman does 3 damage per blow to an elite's body. Parts 1 to 3 must not go to the Armory
   before the armour question is decided.
1. **DPS is a relative index.** `docs/features/melee-damage-model.md` says the absolute seconds
   are not established in managed code. The limits compare TAOM with vanilla inside the same
   model, which is sound, but "83 DPS" is not a measured kill rate.
2. **One blow against 100 health ignores armour and the speed bonus.** The displayed blow is the
   best case of a standing swing. Armour cuts it (`50 / (50 + armour)` before the type pass);
   closing speed on horseback raises it. Is 95 the right ceiling, or should the rule be stated
   against a lightly armoured tier 1 troop?
3. **Two handed weapons one-shot in vanilla too** (140 swing). The plan accepts that. Mike's rule
   names one handed weapons only.
4. **Part 2 weakens tiers 1 to 6 across the whole mod.** It also interacts with armour tiers,
   the troop skill curve and autoresolve power, none of which this plan measures.
5. **Thrust-only troops.** An elite spearman at 45 DPS against a swordsman at 83. Vanilla has
   the same gap, but TAOM gives many elite infantry a spear and shield.
6. **Limits keyed by template.** A two handed sword that also has a one handed mode
   (bastard swords) is judged by its two handed limits. `Priced.swing_damage` takes the hardest
   swing among the modes. Is a one handed mode ever above the one handed limits?
7. **The hero table is the author's reading of the lore**, and the +8 for Sauron's own mace is
   the least defensible row.
8. **The `max_factor` half point margin** assumes the engine rounds displayed damage the way
   `melee_damage.py` does (`round`). If the engine truncates, the margin is larger than needed
   and harmless; check it is never too small.
9. **The validator gate does not price the `taom_ph_` placeholders** (the catalogue cannot build
   them from the install alone). Only the generator's own refusal guards them.
10. **Band width.** 10% is a guess. Too narrow and every roster is a finding; the 25% band was
    chosen because it left 46% of troops already correct.

## How to check the numbers

```bash
python tools/generate_placeholder_weapons.py          # dry run, prints each placeholder
python tools/restat_melee_blades.py                   # dry run, prints the blade plan
python tools/validate_moduledata.py | grep MELEE_BLOW_CEILING
python tools/analyze_melee_ladder.py --stdout         # full ladder report, read-only
python -m pytest tools/tests/test_melee_ladder.py tools/tests/test_restat_melee_blades.py -q
```

Per-class vanilla numbers: load `melee_catalogue.load(MODULES, item_modules=VANILLA_MODULES)`
(both names from `tools/analyze_melee_ladder.py`), call `price_all`, and group `swing_damage`,
`thrust_damage`, `swing_dps` and `thrust_dps` by `item.template`.

Read first: `docs/features/melee-damage-model.md` (the model, its limits, the ladder history),
`docs/features/placeholder-weapons.md`, `tools/melee_ladders.json` (the spec and its comments).
