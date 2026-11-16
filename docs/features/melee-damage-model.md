# Melee damage model and the armoury ladder report

## Overview

Bannerlord stores no damage number on a crafted melee weapon. It runs a physics simulation on
the four crafting pieces every time the item loads, and the number on the inventory screen is
the output. TAOM now reproduces that simulation in Python, so any tool can price a melee weapon
without launching the game.

Three pieces:

- **`tools/melee_damage.py`** is the physics, ported from the v1.5.3 decompile.
- **`tools/melee_catalogue.py`** is the wiring: it reads the installed modules the way the
  engine merges them, works out which usage modes each item has, and prices them. Since #1372 it
  applies the stylesheets each module's `SubModule.xml` `<XmlName>` rows name and leaves out the
  modules TAOM lists as incompatible (NavalDLC), by the shield gate's rule.
- **`tools/analyze_melee_ladder.py`** is a read-only report over the result. It has no
  `--apply` and no write path into any ModuleData.

## Why this exists

- **Vanilla behaviour:** damage, speed and reach are simulated from the pieces. There is no
  authorable field to read or grep.
- **TAOM requirement:** the Armory ships 364 crafted melee weapons across 74 named progression
  lines, and nothing in the repo could say what any of them actually hit for. Every melee
  balance decision to date was made against either the crafting piece's `damage_factor` (which
  is one multiplier at the end of a long pipeline) or a frozen snapshot of a calculator living
  in a different repository.
- **Without it:** a line named `I / II / III` can get weaker as it climbs and nothing notices.
  It does: see the findings below.

## The pipeline, with its engine citations

All line numbers are `E:\Decompiled_Bannerlord\_categories_v1.5.3\Core\TaleWorlds.Core\`.

| Step | What happens | Source |
|---|---|---|
| 1. Piece physics | `length` (cm) becomes metres and splits into two half distances; `Inertia = 1/12 · m · L²`; `CenterOfMass = Length × center_of_mass` (default 0.5); `full_scale` defaults true for Guard and Pommel | `CraftingPiece.cs:167-203` |
| 2. Scaling | `<Piece scale_factor>` scales length and offsets linearly. Weight scales linearly too, **except** a `full_scale` piece, whose factor is **cubed** (it scales as a volume) | `WeaponDesignElement.cs:16-129` |
| 3. Assembly | Pieces are laid out along the axis in the template's build order; the handle anchors at the grip. Centre of mass is weight-weighted with a grip correction; total inertia is the parallel axis theorem | `Crafting.cs:247-294`, `508-519` |
| 4. Reach | `CraftedWeaponLength` from the pivot walk, **not** the sum of the piece lengths | `WeaponDesign.cs:151-290` |
| 5. Speed | Three muscle layers, each integrating torque against inertia with drag at a 0.01 s timestep until the swing covers 1.5 rad or the thrust covers 0.6 m. Swing speed is `20.8 / avg time`, thrust `3.85 / avg time` | `Crafting.cs:296-361`, `521-568` |
| 6. Swing damage | Rotational kinetic energy transferred at impact, best of 5 sample points, swept back from the 0.93 sweet spot; `0.067 × ΔKE` | `CombatStatCalculator.cs:21-36`, `64-83`; `Crafting.cs:363-375` |
| 7. Thrust damage | Linear kinetic energy with 2.5 kg of arm added to the weapon; `0.125 × ½mv²` | `CombatStatCalculator.cs:38-51` |
| 8. Damage factor | The physics magnitude times the blade's `damage_factor`. This is the only authorable lever | `Crafting.cs:374`, `380` |
| 9. Sustained damage | Not an engine stat: derived. `raw_speed = CONST / simulated_time` inverts back into an attack time, plus the attacker's recovery (`StunPeriodAttackerSwing` 0.1 s, `StunPeriodAttackerThrust` 0.67 s) | `Crafting.cs:330`, `:360`; `managed_core_parameters.xml` |
| 10. Armour (combat) | `scaled = magnitude × 50/(50+armour)`, then a blunt pass-through plus a thresholded remainder. Cut 0.1/0.5, Pierce 0.25/0.33, Blunt 0.6/0.2 | `docs/modding/balance-levers.md` |

### The model is stable across engine bumps

Measured 2026-09-20 by diffing the preserved decompile baselines:

- `CombatStatCalculator.cs`: **byte-identical** v1.4.5 through v1.5.3.
- `WeaponDesign.cs`: **byte-identical** v1.4.5 through v1.5.3.
- `Crafting.cs`: one line differs, a debug-export string (`CraftedItem id=`).

So the pipeline first derived on v1.3.12 is still exactly right on v1.5.3. **On the next engine
bump, re-diff those three files.** That check is the entire maintenance burden of this model;
`/engine-bump` should run it.

### Two engine quirks that must never be "fixed"

Both are pinned by name in `tools/tests/test_melee_damage.py` as `test_engine_quirk_*`.

1. **`ParallelAxis` mixes scaled and unscaled inputs.** `Crafting.cs:508-513` reads the piece's
   *unscaled* `Inertia` and `CenterOfMass` but its *scaled* mass. Halving a piece's scale
   therefore does not quarter its rotational contribution the way real physics would.
2. **The inertia walk ignores build-order sign.** `Crafting.cs:290` advances by
   `+= ScaledLength` for every piece, so a pommel at order -1 is walked forward past the blade
   even though the centre-of-mass pass correctly places it behind the grip.

Shipped stats depend on both. Correcting either would make this model disagree with the game.

## What a crafted weapon actually is

**An item is not one weapon.** `Crafting.GenerateCraftedItem` (`Crafting.cs:571-608`) walks the
template's `WeaponDescription`s in order and emits a `WeaponComponentData` for **every** one
whose `<AvailablePieces>` covers every piece the item uses. The first is the primary; the rest
are alternate modes the wielder switches between.

This is not pedantry, and getting it wrong is the single biggest error available here:

- `TwoHandedPolearm` lists **five** descriptions, with `OneHandedPolearm` **first**. 107 of
  TAOM's 120 polearms resolve their primary to `OneHandedPolearm`, because TAOM deliberately
  registers polearm pieces into it (`tools/register_one_handed_polearms.py`) to make them
  shield-compatible.
- `OneHandedPolearm` carries `WideGrip` **without** `NotUsableWithOneHand`, so it simulates
  one-handed: same pieces, a completely different speed and therefore damage.
- Its resolved usage set, `onehanded_polearm_block_long_rshield_thrust`, permits **thrust
  only**. The swing damage the physics computes in that mode is a number the game can never
  use. A halberd reads 12 swing there and 161 in its two-handed mode.

**So judge each attack on the modes that actually permit it.** `Priced.swing_damage` takes the
best swing among swing-capable modes and `Priced.thrust_damage` the best thrust among
thrust-capable ones, rather than reading the primary and calling it the weapon.

### Where attack capability really lives

Not in `item_usage_features`. Those are composition tokens: `OneHandedAxe` is
`onehanded:shield:axe` and obviously swings. The capability is `<usage strike_type="...">`
inside the resolved `item_usage_set` in `Native/ModuleData/item_usage_sets.xml`, and it must be
resolved by **unioning the whole `base_set` chain**. `onehanded_shield_axe` declares no usages
of its own and inherits swing from `onehanded_block_shield_swing`; taking the first chain entry
that declares anything says an axe cannot swing, which is exactly what an earlier pass of this
tool concluded about 100 weapons.

The resulting picture matches Bannerlord: axes and maces swing and never thrust, pikes and
spears thrust and never swing, swords do both.

## Accuracy

The one in-game verified anchor we have is the Galadriel Sword, recorded in the sibling
repository's own test as **Game = 87 swing / 72 thrust**.

| Source | Swing | Thrust | Reach |
|---|---|---|---|
| In game | 87 | 72 | - |
| `tools/melee_damage.py` | **87** | **72** | 99 |
| `taommod/scripts/tw-damage-calc.mjs` | 86 | 72 | 111 |

The JS calculator's own doc comment calls its 1-point gap "a rounding diff". It is not: it is
the reach error. The JS sums forward piece lengths where the engine walks pivot distances, and
reach feeds swing drag, the impact-point window and the lever arm.

**More in-game spot checks are owed** across the other templates before the absolute numbers
are treated as settled. The relative findings below do not depend on that, because every weapon
is priced by the same model.

### Known divergences from the published calculator

`https://taommod.com/mod-info/weapon-balancing/` is generated from
`e:\repos\taommod`, which has three defects this model does not:

1. **Reach** is a length sum, not the pivot walk. Every weapon is affected.
2. **The polearm build order drops the Guard.** Its hardcoded `TwoHandedPolearm` order is
   Handle/Blade/Pommel; the real template is Handle/Guard/Blade/Pommel.
3. **Usage modes are not resolved** except for `TwoHandedSword`, so all 120 polearms are priced
   under hardcoded two-handed wide-grip flags.

Its data mirror is also stale by file hash, though the 723 crafting pieces themselves are
physically identical to the live install, so that particular drift does not move any number.
Nothing here was changed in that repository; this is recorded so the difference is not
mistaken for a bug in this model later.

## One blow is not the weapon: sustained damage

A weapon that hits for 48 and swings fast can beat one that hits for 56 and swings slow.
Ranking a ladder on the displayed damage number alone gets those backwards, so the model
computes damage per second as well.

**Speed is already in the damage number once.** A swing blow is kinetic energy, and energy goes
as velocity squared, so a faster weapon of the same blade already shows a bigger number. Speed
then helps a **second** time by shortening the attack cycle. Both effects are real and both are
in the game, so a fast weapon genuinely compounds. That is not double counting.

**Where the cycle time comes from.** `CalculateSwingSpeed` returns `20.8 / simulated_time`
(`Crafting.cs:330`) and `CalculateThrustSpeed` returns `3.85 / simulated_time` (`:360`). The time
therefore inverts straight back out of the speed stat, and that inversion is exact by
construction: a weapon with twice the speed stat takes half as long to swing, whatever the
engine's animation system does with the number. Added to it is the attacker's recovery after a
connecting blow, read from `managed_core_parameters.xml`:

| Parameter | Value | Effect |
|---|---|---|
| `StunPeriodAttackerSwing` | 0.1 s | Almost free |
| `StunPeriodAttackerThrust` | **0.67 s** | Longer than the thrust itself, roughly 0.5 s |

That asymmetry is the single biggest reason a thrust-only weapon gives up sustained damage, and
it is authored data rather than a derived quantity, so `melee_catalogue.load_combat_parameters`
reads the live file rather than trusting the shipped defaults.

**What this is not.** The absolute seconds assume the native attack animation runs for the time
the muscle simulation produced. That yields roughly 1.0 to 1.7 s per swing, which is plausible,
but it is not established anywhere in managed code. So read DPS as a **relative index**:
comparing two weapons within one attack type is robust because the ratio is exact, while
comparing a swing against a thrust leans on the two stun constants and on the absolute scale.

Deliberately excluded, because none of it is a property of the weapon: the agent's
`SwingSpeedMultiplier` (skill and passives scale every weapon alike, so it cancels in a
comparison), where in the arc the blow lands (`SpeedGraphFunction`, a trapezoid keyed on four
more managed parameters), movement speed bonuses, and the target's armour. For armour, compose
with `armour_reduction`.

**Confirmation that displayed damage is the peak.** In actual combat
`MissionCombatMechanicsHelper` line 258 re-runs the same `CalculateBaseBlowMagnitudeForSwing`
using `GetModifiedSwingSpeedForCurrentUsage() / 4.5454545`, scaled by `SpeedGraphFunction`. The
displayed number is that calculation at the plateau of the trapezoid, so it is the best case of
a single blow, not an average.

**Where the swings go in a crowd (measured, not TAOM's model).** yotthani recorded 14 battles
(79,924 strike events) of the default AI and sorted how each strike ended: 38 % wounded an enemy,
34.5 % hit a friend (cancelled, no damage), 18 % were taken by a shield, 3.3 % were warded off by a
weapon and 1.3 % hit the world; 0.87 events per release. A strike that does reach a body does no
damage in 4 to 23 % of cases (armour), and a soldier's strikes per second depend on the distance to
the nearest enemy and on crowding (0.39 per second with fewer than 10 friends within 5 m, 0.19 with
more than 55); troop and level explain almost nothing beyond that (a
per-troop factor of 0.65 to 1.4, mostly 0.8 to 1.2, which neither level nor any of the 53 AI values correlates with,
|r| at most 0.28 over 37 troops). So sustained damage in a crowd sits well under the per-swing
figure above (TAOM's inference from those shares; the recordings did not measure damage per weapon). Source: yotthani, MithrilForge `docs/engine/battle-behaviour-measured.md`@0a2f4761
lines 42-62, measured in recordings (evidence kind [M], n as stated), measured with TAOM 2.0.34
loaded (plus Armory and DualWield), v1.5.5, custom battles, both sides AI. The friend share
includes the cancelled friendly-fire case described in `signature-strikes.md`.

## The report

```bash
python tools/analyze_melee_ladder.py              # writes tools/reports/melee-balance/
python tools/analyze_melee_ladder.py --stdout     # print instead
python tools/analyze_melee_ladder.py --top 40 --min-deficit 10
```

Outputs `LADDER.md` and `melee_weapons.csv` (one row per weapon, 29 columns). Sections:
provenance with per-file hashes, the catalogue at a glance, the numbered lines, roster
placement, tier inversions, the vanilla envelope, orphans, and the restat worksheet.

It reads the **live install**, because the Armory is unversioned and not in git: there is no
in-repo copy of `LOTRAOM_weapons.xml` or `LOTRLOME_crafting_pieces.xml` to fall back to.
Without the install it reports that it cannot run rather than printing a clean report over no
data. It lists every weapon it failed to price, for the same reason.

### Findings, first run (2026-09-20)

Against 364 Armory melee weapons, 261 vanilla ones and 858 troops.

| Finding | Number |
|---|---|
| Melee weapons priced (0 failures) | 364 |
| Equipped by at least one troop | 223 |
| **Equipped by nobody** | **141** |
| Numbered lines | 74 (213 weapons) |
| **Numbered lines that genuinely regress** | **25 of 74**, 39 steps worse on both one blow and DPS |
| Steps that lose damage but hold DPS (speed trades, not faults) | 10 |
| **Tier inversions at 5 DPS or worse** | **202** (295 at any margin) |
| Weapons out-sustaining every vanilla weapon of their class | 65, of which 4 are hero weapons |
| Pairs where the bigger blow is the worse weapon over time | 1,150 |

The headline cases:

- **`[Gundabad] Spear II` (`wm_gundabad_spear_a02`) hits 210 for 164 DPS**, above every vanilla
  polearm, and it is equipped by **13 troops including tier-2 militia spearmen** across three
  cultures. Its own line then drops to 138 at III, which is a genuine regression: 164 DPS down
  to 118.
- **The cave troll's weapons are the opposite failure.** `wm_cave_troll_2h_mace_a` hits for 86,
  more than the 81 of `wm_dol_goldur_2h_mace_a04`, and sustains **25 DPS against that weapon's
  78**. It is so slow that a lesser mace triples its output. This is why the troll reads as the
  worst-armed unit in Mordor despite carrying a big number.
- **`[Dale] Dale Sword` splits on the two metrics, and that is the whole argument for DPS.** Its
  swing runs 56, 53, 48 down the line while its swing DPS runs 47, 47, 48: the line is trading
  blade weight for speed and its sustained output is flat to slightly rising. Its **thrust** is
  a real regression, 44 to 35 damage and 38 to 30 DPS. Judged on one blow the whole line looks
  broken; judged properly, only half of it is.
- **`wm_gondor_sword_a01` spans nine engine tiers**, carried by 26 troops from level 6 to 51.

`LADDER.md` has the full lists. These are measurements, not decisions: what to do about them is
a separate pass.

## The ladder (#631)

`tools/melee_ladders.json` is the spec and `tools/melee_ladder.py` the shared library, the
melee counterpart to `ranged_ladders.json` and `ranged_ladder.py`. The gate, the report and the
fixer all call `melee_ladder.findings()`, so they cannot drift apart.

**The curve.** Anchored at tier 5, whose TAOM median of 69 DPS already matched vanilla's tier-5
median of 67, and fanned out at vanilla's own slope of roughly +9 per tier:

`T0:25 T1:34 T2:43 T3:52 T4:60 T5:69 T6:78 T7:87 T8:96 T9:105 T10:114`

Per-kingdom percentage offsets carry over from `rebalance_weapons.py` CULTURAL_POINTS so
faction craftsmanship survives the fix. The band is +/-25%. The ceiling is 140 DPS, which is
vanilla's own demonstrated maximum (its hardest-sustaining melee weapon measures 138), and it
is what "no weapon too OP" means numerically. Hero blades are exempt from the ceiling but not
from the band: a line troop holding a hero weapon is still a ladder fault.

Since 2026-11-14 this curve, the kingdom offsets and the 25% band price only a weapon judged without its
crafting template, or whose template has no class limit. Every other weapon takes the per-class target, the
craft table and the 15% band of the next sections.

### Class limits: one blow and sustained damage per class (2026-11-14)

The curve is one DPS curve for every class, and nothing limited one blow. A placeholder one handed sword
met its tier-10 target of 119 DPS with a 140 swing, and shield spears reached 123 per thrust. Most troops
have 100 health, so a one handed weapon must not kill one in a single blow (Mike, 2026-11-14).

`class_limits` in `tools/melee_ladders.json` holds four numbers per crafting template: `swing` and `thrust`
(the displayed damage of one blow) and `swing_dps` and `thrust_dps`. A weapon must be inside all four. Hero
blades are held to a hero limit (below). The numbers are the vanilla maximum per class, rounded; the spec comment records the
exceptions. Vanilla, measured 2026-11-14 with `melee_catalogue` over SandBoxCore and SandBox (261 weapons,
no failures), as min / median / 90th percentile / max:

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

The low ends are practice and tournament weapons.

A class limit is a limit on the DISPLAYED blow, the number on the weapon's tooltip. The damage a troop takes
also carries the wielder's weapon skill multiplier, the head multiplier on a head hit and the speed of the
swing, so the in-game check assumes a body hit on an armoured troop.

`melee_ladder.max_factor` returns the largest `damage_factor` multiplier that keeps a weapon inside its
limits, and `limit_breaches` lists the stats above them. The engine rounds damage to a whole number, so DPS
moves in steps of one damage point: the DPS cap is the largest whole damage inside the DPS limit, with the
same half point of margin the blow cap keeps, and a weapon at the cap cannot round up past its limit. `generate_placeholder_weapons.py` and
`restat_melee_blades.py` stop a raise at that multiplier: the unworn pass and `--limits-only` at the class
limit, a worn blade and a placeholder at the tier limit (below). A class with a low limit no longer climbs
with the tier: a one handed sword stops near 83 DPS, where the shared curve passes tier 6.

The restat holds each factor by its own limits: `swing_damage_factor` by the swing numbers and
`thrust_damage_factor` by the thrust numbers. One ratio for both took swing damage off a sword whose thrust
was at its limit (`wm_rivendell_sword_a02_black_blade` dropped from 96 to 49 DPS in that dry run, and to 80
with separate ratios). The placeholder generator still uses one ratio for both factors.

### Per-class tier targets and craft columns (2026-11-14)

The limits alone break the ten tier spread: the shared curve passes the one handed sword limit near tier 6, so
tiers 6 to 10 would carry the same sword. TAOM has ten troop tiers where vanilla has six, and every class must
spread over all of them. `melee_ladder.target` therefore takes the weapon's crafting template and attacks:

`target(class, tier, culture) = class_top x class_tier_fraction[tier] x craft[culture][column]`

- `class_top` is the larger of the class's `swing_dps` and `thrust_dps` the weapon can use, null ignored. A
  weapon "swings" when `Priced.swing_damage > 0` and "thrusts" when `Priced.thrust_damage > 0`, except that a
  polearm which can do both takes its top from the swing only when its swing DPS is at least its thrust DPS
  (`melee_ladder.dominant_flags`, below). A thrust-only spear of template TwoHandedPolearm tops at 45, a
  swinging polearm at 138.
- `class_tier_fraction[tier]` is the share of the class top a tier may reach (`melee_ladder.tier_share`):
  0.58, 0.62, 0.66, 0.72, 0.77, 0.81, 0.88, 0.91, 0.94, 0.97 and 1.0 for tiers 0 to 10. It replaced the shared
  curve's `curve[tier] / curve[10]` (0.22 at tier 0, 0.61 at tier 5) on 2026-11-14, because that shape
  starved the low tiers of every class. A spec without the key falls back to the curve share.
- `craft` ranks each culture's smithing in three columns (`sword`: Dagger, OneHandedSword, TwoHandedSword;
  `axe`: OneHandedAxe, TwoHandedAxe, Mace, TwoHandedMace; `polearm`: TwoHandedPolearm, Pike). The best culture
  in a column is 1.00 and sits on the class top at tier 10. `craft_column` in the spec maps template to column.

| Culture ids | Sword | Axe | Polearm |
|---|---|---|---|
| rivendell, lindon | 1.00 | 0.90 | 0.97 |
| lothlorien | 0.97 | 0.85 | 0.95 |
| erebor, eredluin | 0.95 | 1.00 | 0.92 |
| gondor, arthedain, cardolan, rhudaur | 0.93 | 0.88 | 0.93 |
| mirkwood | 0.92 | 0.85 | 0.95 |
| sturgia (Dale) | 0.88 | 0.88 | 0.88 |
| vlandia (Rohan) | 0.88 | 0.85 | 1.00 |
| khuzait (Rhun), rhun, aserai (Harad), battania (Khand) | 0.88 | 0.85 | 0.90 |
| isengard | 0.87 | 0.87 | 0.90 |
| mordor, gundabad, goblin, dolguldur, feral_orcs | 0.82 | 0.85 | 0.82 |
| mordor_numenorean (troop ids starting `mordor_num_`) | 0.90 | 0.88 | 0.90 |
| umbar, shaghana | 0.88 | 0.85 | 0.90 |
| empire (Dunland) | 0.82 | 0.85 | 0.85 |
| any other id (`craft_default`) | 0.85 | 0.85 | 0.85 |

`craft` replaces `kingdom_offset`, and `class_band` (0.15) replaces `band` (0.25), wherever the template has a
class limit. A caller that passes no template, or a template without a class limit, gets the shared curve and
the old band. The `ceiling` of 140 stays for that legacy path. Callers that pass the template and the attack
flags: `taom_schema` (the `MELEE_LADDER_INVERSION` kit rows), `fix_melee_ladder.py` (each swap candidate is
judged by its own class band), `restat_melee_blades.py` and `generate_placeholder_weapons.py`. Every one of
them takes the craft row from `melee_ladder.craft_key(troop id, culture, spec)` and the flags from
`melee_ladder.dominant_flags`, so the four cannot price one weapon differently.
`analyze_melee_ladder.py` computes no band or target; its `HERO_BLADE_IDS` is the spec's `hero_blades` list
(it kept its own copy of 18 blades while the spec listed 26).

**Craft by troop id.** The Black Numenoreans (`mordor_num_*`, 13 troops, `troops_mordor.xml`) are filed under
the `mordor` culture, but their smithing is not orc smithing. `craft_troop_prefix` in the spec maps the id
prefix `mordor_num_` to the `mordor_numenorean` row. The engine still reads the culture id; only the ladder
reads the prefix. The prefix table is checked by `validate_spec`: each value must be a `craft` row.

**Dominant attack.** A polearm that both swings and thrusts took its class top from the 138 swing DPS even when
it is a spear in the hand. `dominant_flags(template, swing_damage, thrust_damage, swing_dps, thrust_dps,
spec)` returns `(swings, thrusts)` for `target`, `band` and `class_top`: for a template in the `polearm` craft
column with both attacks it drops the swing when the thrust DPS is higher. Swords and axes are untouched, and
so is a polearm with one attack. A tie stays swing-dominant.

### Tier limits (2026-11-14, #1271)

The class limits are the tier 10 numbers. A tier 3 troop must not carry what only a tier 10 troop may swing, so
`melee_ladder.tier_limit(template, tier, culture, spec)` scales all four limit fields (null stays null) by
`class_tier_fraction[tier] x craft[culture][column]`, the factor the DPS target uses. For a swinging weapon the
DPS limit and the DPS target are then one number, and a restat lands on it, or up to about one damage
point under it where the whole-point DPS cap binds. For a one handed sword at tier 5
with a craft of 0.93 the swing limit is 95 x 0.81 x 0.93 = 71.6 and the swing DPS limit 62.5. At tier 10 with
craft 1.00 it is the class limit.

- `restat_melee_blades.class_ratios(p, spec, tier, culture)` holds a weapon to the tier limit of one carrier's
  tier and craft key. The restat applies it to every non-exempt carrier of every weapon on the blade and takes
  the tightest, and prices the blade from the weakest carrier's own tier and craft row, not from whichever troop
  loaded first. Without the tier it uses the class limit, which is what the unworn pass and `--limits-only`
  want. A hero blade keeps its hero limit at every tier.
- `generate_placeholder_weapons.py` caps k by the tightest tier limit over the placeholder's real carriers
  (every troop of a listed culture inside `tiers` that holds the donor), and by the spec's own tier and first
  culture, which `carrier_rows` groups as `(tier, craft key)` rows. Capping at the spec's tier and first
  culture alone left 11 of 16 placeholders 1 to 5 percent over their lowest carrier's limit. The post-price
  check reads the same rows and names every carrier of a breached row. Each written `damage_factor` is rounded
  down, as in the restat. Run it after the restat: k is relative to the donor, so a restat that moves the donor
  moves k, although the placeholder's own DPS stays on its target.
- `fix_melee_ladder.py` accepts a swap candidate only inside the carrier's tier limit, with the carrier's craft
  key. The band is a DPS window, and a slow heavy weapon sits in it while showing a blow the tier may not carry.
- The restat moves the two attacks of a weapon by one multiplier: the ratio of the primary attack, the one
  with the higher DPS, after that attack's own limits. A held primary does not send the other attack up to
  chase a target the primary cannot reach, and a move under 2 percent or under one displayed damage point is
  not planned, so a second run on the output plans nothing. Before the one-point rule, the rounding of small
  blows kept a second run busy with 2.01 percent moves.
- A written factor is rounded down to its two decimals, never to nearest (`melee_ladder.floor_factor`), in the
  restat and in the placeholder generator: the tier limit's DPS is the tier target, so a factor rounded up
  lands above the limit that set it. The tolerance judges the factor as written, after the rounding down,
  because a small raise shrinks when rounded down and would otherwise find work on a second run.
- A blade that only ladder-exempt troops carry is left alone in every pass: the tier pass, the mixed blade
  pass, the unworn pass, the hero pass and `--limits-only` share one rule (`lower_to_limit`). An unworn plan
  lists every weapon on its blade and counts every wearer, since the edit moves them all.
- `blow_findings` takes an optional trailing `(carrier tier, craft key)` on each carried row. With it an
  ordinary weapon is judged by the tier limit and the finding says "tier limit"; a hero blade is judged by its
  hero limit either way; without it the class limit applies as before. A weapon carried at several tiers is
  reported once, with the breach of the carrier it overshoots most.
- `validate_spec` rejects a `class_tier_fraction` that lacks a tier from 0 to 10, is not strictly increasing,
  has a value outside (0, 1] or does not end at 1.0, and a `craft_troop_prefix` that names a missing row.

### Hero limits (2026-11-14)

A hero blade is exempt from the tier target and the ceiling, never from a limit. Its limit is the class limit
with each DPS raised by the maker group's bonus and each blow limit raised in the same proportion, rounded: a
one handed sword at +10 goes from 83 to 93 DPS and from 95 to 106 swing. `hero_blades` stays the list of exempt
pieces; `hero_makers` maps a piece to a group and `hero_bonus` a group to DPS. A hero blade with no maker takes
`other`.

| Group | Bonus | Blades |
|---|---|---|
| `high_elven_dwarven` | +10 | Glamdring, Galadriel, the three Aranruth, Tuor's axes, and the Celegorm, Finarin, Voronwe, Ingwe and Turin swords |
| `numenorean_sindar_maia` | +8 | Anduril, Strider, Boromir, Faramir, Legolas, Thranduil, Sauron's mace, the Witch King, the Nazgul, and the two Khamul greatswords and the Khamul two handed mace |
| `rohan_dale` | +6 | Theoden, Eomer, Eowyn |
| `other` | +5 | none today |

The eight blades added on 2026-11-14 (Celegorm, Finarin, Voronwe, Ingwe, Turin, `sm_dg_khml_sword_2h_b`,
`sm_dg_khml_sword_2h_c`, `sm_dg_khml_2h_mace_b`) are carried by no troop and share their blade piece with no
other weapon. The grouping is the author's reading of the lore and waits for the maintainer.

`MELEE_BLOW_CEILING` judges a carried hero blade against its hero limit. `restat_melee_blades.py` lowers a hero
blade above its hero limit, each factor by its own limits, and leaves one below it alone, or one that only
ladder-exempt troops carry.

### `--limits-only`

`python tools/restat_melee_blades.py --limits-only` lowers only the blades above a class or hero limit. It
raises nothing and ignores the tier target and the ceiling, so older ladder drift does not ride along with a
limit fix. It is a dry run unless `--apply` is given, and cannot be combined with `--culture`.

### `--culture` names two different things

`restat_melee_blades.py --culture` filters the tier pass by craft key (`ladder.craft_key`): a troop's culture,
except that the Black Numenoreans are `mordor_numenorean`, not `mordor`. `fix_melee_ladder.py --culture` filters
by the engine culture id of the troop (`empire` is Dunland, `battania` Khand). The help texts say which. The
restat takes no engine culture id, which would need a second mapping for the same filter; the unworn, mixed
blade and hero passes ignore the filter in both modes.

### Dry runs before the tier limit (superseded)

The first 2026-11-14 dry runs judged every weapon against the class limit only: 190 blades in the full
restat and, after a simulated restat, 523 of 965 troops inside their band. The tier limit below replaced them,
and so did the figures: read the next section for the plan as it stands.

### Dry runs with the tier limit (2026-11-14, #1271; live Armory read, nothing written)

`python tools/restat_melee_blades.py` and `--limits-only` print the plan counts; the rest is a one-off in-memory
simulation that scales each attack of a plan's weapons by the plan's factor ratio. Counts below are from the
final run on 2026-11-14, after the deep review fixes. "Today" in this section means the Armory before the
restat of the same day; the restat has since been applied (see "Not done, and known problems").

- Full restat: 203 blades (55 raise a factor, 148 only lower) reaching 264 weapons and 1448 troop slots,
  because an unworn plan now lists every weapon on its blade. `--limits-only`: 69 blades reaching 98 weapons
  and 331 troop slots, none raised. Nine of the 203 are couchable blades that need a cut but not a raise (see below).
- Seven blades back both one handed and two handed weapons. The restat prices none of them by tier. It lowers
  three of them, only as far as a class or hero limit needs: `sm_dwarf_erebor_axe_blade_2h_a2` and `_b2`
  (two handed axes at 159 and 242 swing) and Tuor's axe. The run lists all seven with every weapon, its class
  and the damage it shows.
- No non-hero one handed weapon displays a blow of 100 or more after the restat (four do today).
- Troops whose best weapon sits inside its class band (965 troops with a melee weapon, trolls excluded): 382
  today (339 under, 244 over); 599 after the restat (366 under, none over). The remaining troops are under
  because a shared blade is priced for its weakest wearer. The whole-point DPS cap costs 18 of them in this
  scale-only simulation (617 were inside with a cap that read the displayed damage as exact, but six
  weapons then ended above their limit); re-priced through the engine port, which rounds the damage
  again, the cost is 9 (604 against 613).
- Median best DPS per tier, tiers 0 to 10: today 25, 34, 43, 50, 54, 60, 71, 78, 86, 94, 96; after the
  restat 39, 43, 44, 48, 52, 55, 60, 69, 80, 70, 70.
- Weapons a troop carries above the tier limit of that troop, repriced in memory after the restat: 160 today,
  8 after. All eight are weapons on the Erebor shared one handed and two handed blades, which the restat does
  not price by tier. Rounding the written factors down and the whole-point DPS cap closed the other eleven.
- A second simulated run on the output of the first plans nothing.
- `fix_melee_ladder.py` against today's Armory: 189 swaps across 175 troops, none above its carrier's tier
  limit. Before the tier limit check it planned 582 swaps and 430 of them landed above the carrier's limit.
  803 weapon slots stay unresolved, 456 of them because every in-band candidate is over the carrier's tier
  limit.
- `generate_placeholder_weapons.py`: priced against the Armory as the restat leaves it, all 16 placeholders sit
  inside the tier limit of every troop that will carry them. Against the Armory before the restat the dry run
  stopped at `taom_ph_gondor_spear_b_t5`, whose donor already sat at the cap, so the tool runs after the
  restat; it did, on 2026-11-14.
- `python tools/validate_moduledata.py` reported 546 `MELEE_LADDER_INVERSION` and 171 `MELEE_BLOW_CEILING`
  warnings against the Armory before the restat (2026-11-14); 11 of the 171 named a `taom_ph_` placeholder,
  which the gate prices too. The gate builds its rows with the same `craft_key` and `dominant_flags` calls as
  the restat. After the restat and the rest of the apply the same run reports 318 `MELEE_LADDER_INVERSION`
  and 8 `MELEE_BLOW_CEILING` (the blades the Erebor one handed and two handed axes share).

### Not done, and known problems

- **The Armory blades were restated on 2026-11-14; the troop swaps were not made.** The restat wrote 203 blade
  pieces and a second run plans 0. The counts above are the dry runs and simulations made before it.
  `generate_career_kits.py`, `generate_starter_kit.py` and `generate_placeholder_weapons.py` ran after it.
  `fix_melee_ladder.py` did not: it breaks the weapon category rule ("A troop keeps its weapon category", below), so the 318
  `MELEE_LADDER_INVERSION` warnings it was meant to remove stay. The order of the apply and its results are in
  [armor-balance.md](./armor-balance.md#the-apply-sequence-2026-11-14-1271).
- A blade is priced for its lowest wearer and held to that wearer's tier limit, so a blade worn across more
  than two tiers leaves its upper wearers light. 62 of the 204 worn non-hero blades span more than two tiers
  (`wm_mirkwood_spear_a01_blade` T2 to T10, `wm_gondor_sword_a03_blade` T3 to T10). The cure is a cloned blade
  for the upper wearers, as `generate_placeholder_weapons.py` makes for its placeholders; nothing clones one
  for the restat yet.
- A couchable blade is never raised, since a couch charge multiplies the hit the model cannot see. Before
  2026-11-14 a plan that raised one attack and cut the other was dropped whole, which left `he_spear` at a
  thrust of 74 against a limit of 60. The raise is now cut from such a plan and the cut kept.
- A blade that backs both one handed and two handed weapons is not priced by tier, because its weakest wearer
  is the one handed troop and its target took the two handed twins down with it (an Erebor two handed axe
  swing of 126 fell to 75). The restat leaves such a blade alone unless a weapon on it is above its CLASS
  limit, and then lowers it only that far. The Erebor one handed axes use two handed blade pieces
  (`sm_dwarf_erebor_axe_blade_2h_a`, `_a2`, `_b`, `_b2`, `_c`), so those one handed axes stay above their
  carriers' tier limits (they show swing 62 to 137 today) until the blades are cloned. Seven blade pieces carry both a one
  handed and a two handed weapon, and every run of the restat lists them. Nothing clones them yet.
- Tuor's axe and its one handed variant share a blade piece, so the lowering for the two handed axe also
  lowers the one handed one.
- `wm_gondor_boromir_sword` (hero blade `wm_boromir_sword_blade`) is carried by ordinary Gondor t7 and t8
  troops and judged at its hero limit (thrust 64 against an ordinary t7 limit of about 46.5). The decided fix
  is a roster swap to a non-hero blade, not done yet.

### Pass 1, the roster: applied 2026-09-20

`tools/fix_melee_ladder.py` swaps the weapon a troop carries. It writes only
`Main/_Module/ModuleData/troops/*.xml`, never the unversioned Armory, so a module reinstall
cannot revert it.

| Measure | Before | After |
|---|---|---|
| Troops on curve | 46% | **65%** |
| Tier inversions at 5 DPS or worse | 202 | **132** |
| Weapons over the ceiling that a troop can reach | 13 | **5** |

243 swaps across 164 troops in 13 roster files.

**The median per tier barely moved, and that is expected.** A median is insensitive to fixing
the tails, and the tails are what was wrong. The share of troops inside their band is the
metric that answers the question; the median is not.

Four constraints the swap respects, each of which cost something to learn:

1. **Culture is a hard filter, not a tiebreak.** The first version ranked it as a preference,
   and when a faction owned nothing in band the ranking fell through: Gondor's Swan Knights
   were handed Uruk-hai halberds and Erebor's royal wardens orc spears. In a Middle-earth total
   conversion that is not a rebalance. A faction with no in-band weapon of a class is now an
   unresolved case for pass 2. A culture's pool is every weapon whose id family appears in its
   own rosters, because the `culture=` attribute on an Armory item is unreliable (45 items
   claim `Culture.khuzait`, 38 `Culture.empire`, 21 carry none).
2. **Shield parity.** A shield-carrying troop is never handed a polearm whose primary usage is
   `requires_no_shield`. That pairing has shipped three times.
3. **One swap per slot.** Six melee weapons sit in two different slots on the same troop, and a
   swap keyed on (slot, old item) would have replaced one and left the other.
4. **Different weapons stay different (2026-11-07).** The 2026-09-21 run mapped each
   out-of-band weapon to the single best in-band one, so two weapons of one class in a troop
   became one id. The Black Uruk Barad-Dur Guard's spear and spare halberd both became Uruk
   Halberd IX, the Shield Guard got two Gundabad spears, and Orc Polearms II to IV on the
   Black Uruk Grunt all became the Black Numenorean lance. `pick_substitute` now skips any
   candidate the troop keeps or was already handed, and reports the slot unresolved when no
   other in-band weapon is left. The check covers the whole troop, every set at once,
   because the writer applies one swap to every set. On the 2026-11-07 data it refused 15
   swaps the old planner made: 11 named a weapon found only in a `civilian="true"` set,
   which the writer skips anyway, 2 would have duplicated a weapon inside one set, 1 was a
   collapse, and 1 (`lorien_gl_marksman`) only repeated a weapon another set carries.
   The rosters were repaired by hand; the Grunt keeps the lance in 7 of its 14 sets
   because the Mordor career kits take the lowest-level, then most-carried, Mordor
   polearm, and Orc Polearm II is `requires_no_shield` beside those kits' shield.

### Pass 1 changes career starting kits

Career kits are derived from each culture's LOWEST troop gear (#629), so moving low-tier
weapons moves them. After any roster pass, run `generate_career_kits.py --apply` then
`wire_starter_kit_rosters.py --apply`. `test_generate_career_kits.py` catches it if you forget;
it caught it here, 30 ids adrift.

### Pass 2, the restat: applied 2026-09-21

`tools/restat_melee_blades.py` writes `damage_factor` on blade pieces in the **unversioned**
LOTRLOME_Armory. Dry-run by default, `--apply` explicit, a `.bak-<tag>` backup before every
write (never `*.xml`, which the engine globs), the document parsed before anything lands, and
the target absolute rather than a multiplier so a second run is a no-op.

The multiplier needs no search: displayed damage is `magnitude * damage_factor` and the attack
cycle does not depend on the factor, so **DPS is exactly linear in it** and the ratio is
`target / current`. Both factors scale together, which moves the weapon's power without
changing its character.

**Noble lines are judged one tier up (2026-09-25).** `analyze_melee_ladder.Troop.tier` adds one
tier for a troop in `taom_schema.Validator._NOBLE_LINE_TROOPS` (Mike: nobles carry better kit
than regular troops of their level), and a weapon's anchor tier is its lowest wearer's *tier*
rather than its level, so the gate, `fix_melee_ladder.py`, `restat_melee_blades.py` and the report
all see the noble band. Without it the Lossarnach nobles' poleaxes dragged the shared blades
down: the level-16 noble anchored the medium blade (`sm_ar_art_poleaxe_blade_b`) at tier 3 for
the Ringlo guardsman, and the level-26 sergeant anchored the heavy blade (`_blade_a`) at tier 5,
under the Ringlo spearman and warden at tier 6 and 7. The set stops at tier 7 (Mike kept it
there), so a tier-7 noble is judged at tier 8, level with its tier-8 promotion: twelve Gondor
upgrade edges (the Arndir, Ithil, Minas Tirith, Dol Amroth and Pelargir captains among them) no
longer raise the melee target.

**A roster pass can reach for a blade another line depends on.** On the Gondor run after
KEYforce's Lamedon drop, pass 1 offered the tier-4 and tier-5 Pinnath Gelin archers the
Numenorean poleaxe as a sidearm; they became its lowest wearers and pass 2 dropped the blade from
80 to 71 DPS for the Ringlo nobles. The swap was reverted by hand and the blade restored. Read pass
1's plan for weapons that belong to another line before `--apply` (the gate's own repair line
still suggests the reverted archer swaps), and stop the loop when it
starts trading the same blades back and forth: ten such suggestions were left unapplied that day,
6 of them "upgrades" from the elite to the medium Numenorean bastard sword that only score higher
because their blades are anchored by different wearers.

**The two passes iterate.** A weapon's tier anchor is its lowest wearer, so a restat moves the
anchors and a roster pass then finds new options, which moves them again. Four rounds of
restat, roster, regenerate career kits:

| | Start | Final |
|---|---|---|
| Troops on curve | 46% | **81%** |
| Over-armed troops | 244 | **0** |
| Tier inversions at 5 DPS or worse | 202 | **38** |
| Troops reaching an over-ceiling weapon | 13 | **0** |
| Climb, T1 to T10 median | 1.04x | **2.68x** |

The curve now tracks its target: 25/34/42/50/53/60/69/80/86/88/91 against a target of
25/34/43/52/60/69/78/87/96/105/114. It runs under target from tier 4 up, which is the
shared-blade drag: a blade is priced for the weakest troop that can reach any weapon on it, so
a weapon still spanning several tiers holds the higher ones down.

**It oscillates rather than converging.** Round three came back slightly worse than round two
(78% against 79%) before rounds four and five settled at 81%. Further rounds trade a point
either way; this is the fixed point, not a stopping place chosen early.

### Three guards, each of which was wrong once

1. **Couchable weapons, asymmetrically.** `ComputeBlowMagnitudeMelee` feeds the attacker's
   closing speed into `CalculateStrikeMagnitudeForThrust` as `extraLinearSpeed`, where it is
   added to the thrust speed and the sum is **squared**. A couched lance therefore already
   lands several times the standing-thrust magnitude this model computes, so RAISING one is
   refused. The first version refused to move them at all, which protected
   `wm_gundabad_spear_a02` at 164 DPS in the hands of tier-3 militia, the single worst weapon
   the audit found. Lowering is safe and is now allowed.
2. **Ladder-exempt troops.** The cave troll's club is scenery-scale and deliberately slow. The
   first version priced it for tier 10 and would have quadrupled a monster's damage.
3. **Every weapon on a shared blade, not just the worn ones.** The first version took the ratio
   from the weapons troops carry, so raising a blade sent an unworn Erebor axe sharing it to
   186 DPS while its worn sibling landed on target. The ratio is now computed against every
   weapon on the blade and capped so the strongest cannot breach the ceiling.

Unworn weapons get no tier target, since no troop anchors them, but they **do** get the
ceiling: they reach the player through merchants and loot, and two Rohan two-handed axes sat at
153 DPS in every market with no roster-driven pass able to see them.

### What the generators needed

Re-checked after the passes:

- `generate_starter_kit.py --verify`: **no drift**, both Armory copies.
- `check_generator_item_refs.py`: **passes**, every id a generator would write is defined.
- `generate_career_kits.py`: derives from each culture's lowest troop gear, so it is
  regenerated after every roster pass. `wire_starter_kit_rosters.py` follows it.
- `generate_gondor_troops.py`, `generate_rhun_troops.py` and the `apply_*_troop_revamp.py`
  family: **historical scaffolds, not live generators.** `generate_gondor_troops.py` was
  already 14,804 lines out of sync with the committed roster before any of this work, measured
  against `HEAD`. Patching their hardcoded weapon tables would be work on dead code and would
  imply they are safe to re-run, which they are not.
- A test re-renders each live troop generator this pass touched. `test_generator_regions_in_sync.py`
  runs `--check` for each realm in `generate_new_faction_kingdoms.REGENERATABLE` (Cardolan and
  Rhudaur among them), and `test_generated_files.py` requires `troops_lothlorien.xml` to equal the
  `generate_lothlorien_troops.py` render with the placed skills carried over. A roster swap on one
  of their troops goes into the generator's table, or the test fails.
- After a pass 2 restat, re-run `generate_starter_kit.py --apply`: a crafted starter is capped at
  the melee DPS of its weakest same-culture shop sibling, so moving a sibling moves the cap.
  `--verify` compares the whole starter blade block with the plan, so a stale factor shows as drift.
- `generate_enlistment_rosters.py`: its hand-authored `DEFAULT_ROSTER_ITEMS` ships as the
  `neutral_culture` last-resort rosters, and 14 of its weapon entries now sit under the band
  their rank implies. Ten cannot be fixed from that family at all: Rohan's one-handed swords
  top out at 43 DPS and a sergeant's band starts at 58. Documented in the file rather than
  patched, because the fix is a decision (widen the culture-neutral table past one kingdom, or
  author Rohirric weapons) rather than a substitution.

### Upgrade paths: 2026-11-13

The band check judges each troop alone, so a troop can sit inside its band and still carry less
than the troop it upgrades from (#631). A one-off walker followed every `upgrade_target` path. It
built each battle set the way `BasicCharacterObject.Deserialize` builds it and priced Armory and
vanilla weapons with `melee_catalogue`. It counts a pair when a troop's melee DPS sits 5 or more
below an ancestor's, in two ways:

- **Best set**: each troop's strongest weapon over all its battle sets.
- **Weakest set**: each troop's weakest set. The engine draws each slot from its own battle set
  (`Equipment.cs:573-578`), so a troop with one weak set carries the weak weapon in some spawns.
  The best-set count hides that.

Mike's rule for the fix: lower tiers take vanilla weapons, unique weapons stay with the higher tiers.
Applied as roster swaps, never as new items, and in every battle set of the troop:

1. The higher troop takes a stronger weapon of the same class that its culture already fields
   (the pass 1 pool), no higher than its band's top, not a hero blade, shield-safe.
2. Failing that, the lower troop drops to a vanilla weapon of the same class whose id prefix its
   culture already fields (`empire_` for Gondor, `vlandia_` for Rohan, `khuzait_` for Rhun), never a
   `_blunt` tournament weapon and never below its own band.

Three of the swaps took the one-handed Berserker sword off three tier-1 Isengard troops
(`urukhai_recruit`, `isengard_orc_warg_scout_v2`, `isengard_orc_brawler`). A pass 2 run
(`--culture isengard --culture gundabad --culture anduinvale`) could then anchor its blade at the
champions' tier: `wm_isengard_berserker_sword_a01_blade` went from swing 1.21 to 2.44, and the
two-handed Berserker sword from 34 to 69 DPS. A raise in the item's tier and price follows from the
higher damage; nobody measured it. The same run lowered `wm_gundabad_sword_a01_blade` from
2.88 / 1.03 to 2.37 / 0.85.

The run also lowered four Dale blades (`wm_dale_ws_1h_axe_a01` to `a03`, `wm_dale_ws_sword_a02`),
and those were put back. `--culture` labels an item with its first wearer's culture, and
`troops_anduinvale.xml` loads before the Dale file, so Dale's own weapons were labelled `anduinvale`
(#1241). Until that is fixed, read a `--culture` dry run's blade list before `--apply`.

Measured on 2026-11-13 with the live Armory pieces on both sides, `HEAD` rosters against the working
tree:

| Count (gap of 5 DPS or more) | Before | After |
|---|---|---|
| Best-set pairs | 69 | 38 |
| Best-set troops | 51 | 25 |
| Weakest-set pairs | 133 | 101 |
| Weakest-set troops | 91 | 64 |

No pair is new. `validate_moduledata.py` reports 129 `MELEE_LADDER_INVERSION` warnings after the
pass, and no longer warns the Anduin Vale and Angaladh woodman archers or the Dorwinion vintner
archer. The Cardolan, Rhudaur and Lorien swaps are also in their generators' tables
(`generate_cardolan_troops.py`, `generate_rhudaur_troops.py`, `generate_lothlorien_troops.py`).

Four troops now carry a vanilla weapon above their band's top: the Gondor Har conscript and three
Westfold troops. The gate cannot see them, since it prices Armory items only (#1242, which also
covers per-set scoring and `civilian="true"` rosters).

Left on purpose: the lance and spear role changes (couched damage is outside this model; a spear
trades DPS for reach). Left for a decision under #631, the class changes no roster swap can fix, each needing a
stronger weapon the culture does not own: the Gondor Pavise line's maces (54 to 58 DPS under a
crossbowman's 80 sword), the Gundabad Chosen and Ironfang maces (43 under 60), the Gundabad Guardian's
pike, the Knight of Arthedain and the Captain of Cameth Brin with a 71 bastard sword under an 80
poleaxe, and the Kharaghul Karash Keshig's sword under its parent's 116 vanilla polearm.

### What is left

140 troops are still under-armed, and that is the same catalogue-depth problem: a culture
cannot field a ladder it has no weapons for. Rohan owns one one-handed axe for seven tiers.
Closing it means authoring weapons or deciding some tiers field a different class.

## The constraint on any fix

**Damage lives on the blade piece, never on the item.** A `<CraftedItem>` carries no stats at
all; it names pieces. So restatting a weapon means editing its blade's `damage_factor`, and a
blade shared by N weapons moves all N at once, across every troop that carries any of them.

That is what report section 8 exists for: blade piece to weapons to troops to tier span. A
blade with a fan-out of one is a free edit; a blade backing nine weapons across six tiers is a
decision someone has to make deliberately.

The second constraint is the **name-versus-id** question. The numeral lines are declared in
display names (`[Mordor] Uruk Sword III`) while the ids run on letters (`sm_uruk_sword_c`).
Section 3 lists the lines where the two orders already disagree, which have to be resolved
before anything automated keys on either.

## A troop keeps its weapon category

The maintainer's rule (2026-11-14): "Extremely important, every troop was made for a specific category,
sword and shield, two handed sword or axe, spear and shield, spear that lances, polearms, pikes, bows."

A balance pass changes a weapon's numbers, or swaps it for a weapon of the same category. The categories
are: one handed weapon with shield, two handed sword, two handed axe or mace, spear with shield,
couchable lance, swinging polearm, pike, bow and crossbow. A swap never crosses categories and never
moves a troop between a shield kit and a two handed one. The rule binds every tool that fits a troop to a
number, not only the melee ladder.

`fix_melee_ladder.py` does not hold this rule yet. On 2026-11-14 its plan of 176 swaps across 162 troops
(against the restated Armory) replaced poleaxes with short spears, for example `rhudaur_lord_of_the_angle`
from `numenorean_poleaxe_heavy` at 92 DPS to `dunland_caerdh_short_spear_e` at 33, so it was not applied.
Do not run it with `--apply` until it picks only same-category candidates and never lowers an
under-armed troop's damage.

## Key Files

| File | Purpose |
|------|---------|
| `tools/melee_damage.py` | The engine's crafting physics: piece geometry to damage and speed |
| `tools/melee_catalogue.py` | Loads the installed modules as the engine merges them and prices every usage mode |
| `tools/melee_ladders.json` | The ladder spec: class limits, tier shares, craft table, hero blades and bonuses, exempt troops |
| `tools/melee_ladder.py` | Targets, bands, tier and hero limits, `max_factor`, `floor_factor`, the findings the gate and the report share |
| `tools/restat_melee_blades.py` | Pass 2: restats blade `damage_factor`s in the Armory (`--limits-only`, `--culture`, `--apply`) |
| `tools/fix_melee_ladder.py` | Pass 1: roster swaps inside each carrier's tier limit |
| `tools/generate_placeholder_weapons.py` | The `taom_ph_` placeholder weapons, capped by every carrier's tier limit |
| `tools/analyze_melee_ladder.py` | The read-only report |
| `tools/taom_schema.py` | The `MELEE_LADDER_INVERSION` and `MELEE_BLOW_CEILING` gates |
| `tools/tests/test_melee_damage.py`, `test_melee_ladder.py`, `test_restat_melee_blades.py`, `test_generate_placeholder_weapons.py`, `test_analyze_melee_ladder.py`, `test_melee_catalogue_loading.py` | The tests |

## How to verify in game

Not verified in game for #1271. The checks read the displayed blow, so each assumes a body hit on an
armoured troop (see "Class limits"). After the apply and a full restart:

1. In a Custom Battle, check the tooltip swing of one handed swords against their tier limit: at most 95
   for a tier 10 Rivendell troop, at most 64 for a Gondor tier 3 troop. No ordinary one handed weapon
   shows 100 or more.
2. Field same-tier heavy or elite infantry of two kingdoms: a one handed body hit must not kill in one blow,
   and a same-tier sword swing on the body does at least about 10 damage (R2).
3. Field elite (tier 6 or more) swordsmen against tier 1 to 3 recruits: a one-blow kill there is wanted.
4. Check that a thrust-dominant spear (a Gondor spear) and an Erebor two handed axe still feel strong. The
   two handed axes share blade pieces with one handed ones and are no longer priced by tier; the one handed
   twins stay heavier than their tier allows until the blades are cloned.

## Changelog

- 2026-11-14: class limits on one blow and on DPS, per-class tier targets over ten tiers with a craft table,
  tier limits, hero limits by maker group, `--limits-only`, and a DPS cap in whole damage points (#1271).
  Built and dry run; the Armory is not yet restated.
- v2.0.40 (16 Nov 2026, #1372): the catalogue finds stylesheets through `SubModule.xml` rows and leaves out
  NavalDLC. NavalDLC's 149 crafting pieces and its items left the catalogue. TAOM's own placeholder sheets
  stay out of the chain, because the callers that price placeholders add them from the tree they check. The
  ladder, the generators and the validator gave the same output on TAOM's weapons; the troop roster page now
  reads items without NavalDLC's stubs, so its Umbar `northern_round_shield` shows 300 hp (page regenerated
  under #1384).

## GitHub Issue

- **Issue:** #1271: Weapons and armour sit far above the vanilla range: a one handed sword at 140, elite
  armour at 112
- **Status:** Open
- The melee ladder umbrella is #631.
- #1372: the catalogue loads modules and stylesheets by the shield gate's rule.

## Related

- `docs/modding/items-weapons-and-crafting.md`: the authorable `<CraftingPiece>` fields and
  which six stats are not authorable at all
- `docs/modding/balance-levers.md`: the armour reduction formulas and survivability tables
- `docs/features/ranged-ladders.md`: the same problem solved for bows and crossbows, whose
  `tools/ranged_ladder.py` supplies `engine_tier()` and the trailing-numeral regex this reuses
- `docs/features/armor-balance.md`: `derive_armor_tiers.py` established the anchor-to-lowest-
  wearer rule reused here
- `tools/audit_polearm_shield_parity.py`: the existing gate on the same description-matching
  rule, and the source of the usage-resolution approach
- `tools/rebalance_weapons.py`: the existing per-culture `damage_factor` applier, and the
  source of the hero exemption list. Note it writes to a sibling repository and a hardcoded
  Steam path, and its `CURRENT_AVG_MELEE` constants are a frozen snapshot
- `tools/BannerlordCraftingTool/MainWindow.xaml.cs:457-524`: an independent C# port of the
  same pivot-distance algorithm, which this model agrees with

---

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/ranged-ladders.md](./ranged-ladders.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
