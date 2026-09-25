# Melee damage model and the armoury ladder report

## Overview

Bannerlord stores no damage number on a crafted melee weapon. It runs a physics simulation on
the four crafting pieces every time the item loads, and the number on the inventory screen is
the output. TAOM now reproduces that simulation in Python, so any tool can price a melee weapon
without launching the game.

Three pieces:

- **`tools/melee_damage.py`** is the physics, ported from the v1.5.3 decompile.
- **`tools/melee_catalogue.py`** is the wiring: it reads the installed modules the way the
  engine merges them, works out which usage modes each item has, and prices them.
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

Three constraints the swap respects, each of which cost something to learn:

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
- `generate_enlistment_rosters.py`: its hand-authored `DEFAULT_ROSTER_ITEMS` ships as the
  `neutral_culture` last-resort rosters, and 14 of its weapon entries now sit under the band
  their rank implies. Ten cannot be fixed from that family at all: Rohan's one-handed swords
  top out at 43 DPS and a sergeant's band starts at 58. Documented in the file rather than
  patched, because the fix is a decision (widen the culture-neutral table past one kingdom, or
  author Rohirric weapons) rather than a substitution.

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
