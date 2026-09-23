# Dwarven War Ram

## Overview

A rideable war ram for the Dwarves: the first mount TAOM has ever given a dwarf, and the first
creature mount built as a **reskin of the vanilla horse skeleton** rather than on a rig of its own.
It carries an Ironpass cavalry branch, wears an eight-piece barding ladder, and headbutts on its own
with a bespoke clip: the vanilla horse kick stood in until 2026-09-18, when the head-butt authored on the
engine's `horse_skeleton` went through the Kit and was bound as `act_war_ram_butt` in the ram's own action set.

## Why This Exists

TAOM already shipped a **`ram_rider` career** with nothing to ride.
`Main/_Module/ModuleData/career_system/taom_careers.xml:637` defines it: ranks Ram-Breaker,
Goatback Charger and Vanguard of Dáin, gated to `Culture.erebor`, with a "Mountain Charge" ability
(+25% charge damage, +20% mount speed, `taom_ability_templates.xml:276`) and
`CareerArchetype.Cavalry` in `Main/Features/CareerSystem/CareerSystemIoC.cs:134`. The career was
reachable and its mount did not exist.

Erebor was otherwise a deliberate no-mount culture: before this change `troops_erebor.xml` contained
**zero** `slot="Horse"` entries, and `Patch20_NarrativeHorseGuard` exists specifically so character
creation survives horseless dwarves. The war ram is the single, deliberate exception. Dwarves still
never ride horses.

## Architecture

### Design Challenge

Three problems, only one of which was the obvious one.

1. **Dwarves were flagged unrideable.** `Monster.dwarf` shipped `CanRide="false"`, the only race
   besides `cave_troll`.
2. **TAOM's own validator treats a mounted dwarf as a hard error.** `MOUNTED_DWARF` fires in
   `tools/taom_schema.py` both on a cavalry `default_group` and on any reachable Horse-slot item.
3. **The dwarf rider bone is misaligned**, which is why 1 and 2 exist. The root cause turned out to
   be measurable: `Monster.dwarf`'s height fields are a **byte-for-byte copy of `human`'s**
   (`standing_eye_height="1.70"`, `crouch_eye_height="1.10"`, `mounted_eye_height="0.75"`,
   `arm_length="0.9"`; only `weight` differs, 100 against 80), while `dwarf_skeleton_a` renders at
   roughly **82% of human height**. Measured off armour meshes in one coordinate space: dwarf chest
   armour tops at z=1.30, human (Gondor Anfalas) at z=1.59. The engine seats and measures a rider it
   believes is human-sized.

### Solution Approach

**The ram is the vanilla `horse_2` shape.** Both ram bodies and all eight bardings are skinned to the
stock vanilla horse skeleton bone for bone, so the Monster is seven attributes (`action_set` was
`as_horse` until 2026-09-18; `as_war_ram` is a three-line child of it that adds the bespoke head-butt):

```xml
<Monster id="taom_war_ram" base_monster="horse" action_set="as_war_ram"
         weight="320" hit_points="160"
         jump_acceleration="7.5" relative_speed_limit_for_charge="4.0" />
```

`base_monster="horse"` inherits `Flags`, `ActionSetCode`, `MonsterUsage` and every capsule field
(`Monster.Deserialize`, TaleWorlds.Core v1.4.8), and attributes not named here keep the inherited
value because the deserialiser guards its defaults behind `if (!flag)`. That buys `Mountable`,
`family_type="1"`, `monster_usage="horse"`, `num_paces="6"`, every bone name, the slope block and
**all twelve rein attributes**.

Two consequences worth stating plainly:

- **Phases 1 to 5 of [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) are
  skipped, with one exception since 2026-09-18.** No `quad_movement` tagging, no `monster_usage_sets`, no
  rider partial: locomotion is the horse's. The exception is the head-butt: one clip, one `action_types.xml`
  entry and three thin action sets over the vanilla horse sets (`as_war_ram`, `_town_and_village`, `_map`),
  recorded in [lotrlome-war-ram-changes.md](../reference/lotrlome-war-ram-changes.md) section 5.
- **The war ram is the only TAOM mount with vanilla's complete rein surface.** Gotcha #18 of that
  doc records that the spider and warg declare 5 of 12 rein attributes and the elephant and mumakil
  declare 0, and that v1.4.8 changed the native rein path that runs on mounted-agent death. The ram
  inherits all twelve, so it sidesteps that open risk rather than adding to it.

**`monster_usage="horse"` is load-bearing for the dwarf rider specifically.** `as_dwarf_warrior` has
no `base_set`, so the "rider partial at the top of the file" trick that serves the spider and warg
does not reach dwarves at all. It does already carry **203 `act_horse_*` / `act_ride*` rows, the
identical count to vanilla `as_human_warrior`**, so inheriting the horse usage set hands the dwarf
rider a complete authored overlay for free. Any custom usage set would have meant hand-authoring a
second rider partial into a 4,843-action set.

**The dwarf seat correction belongs on the ram, not on the dwarf.** Because the ram is ridden only by
dwarves, `rider_eye_height_adder`, `rider_body_capsule_height_adder` and `rider_camera_height_adder`
can be tuned on `Monster.taom_war_ram` without moving every dwarf on foot. They are currently left
inherited from the horse, pending the in-game seat check.

### Component Diagram

```
troops_erebor.xml  ironpass_ram_rider (Cavalry, race="dwarf")
      |  slot="Horse"        -> Item.taom_war_ram_a
      |  slot="HorseHarness" -> Item.taom_ram_barding_light_a
      v
LOTRAOM_horses.xml  <Horse monster="Monster.taom_war_ram" body_length="100">
      v
lotr_monster_war_ram.xml  base_monster="horse", action_set="as_war_ram" (child of as_horse)
      v
engine: vanilla cavalry spawn. Movement, blows, deaths and rider seating are all
        the horse's. No spawn patch, no detached combatant.
      +
WarRamMissionBehavior : MissionLogic
      -> attaches WarRamBehaviorTree per agent, keyed on Monster.StringId
      -> head-butt plays act_war_ram_butt (bespoke clip war_ram_butt, bound in
         as_war_ram), damage via the shared ElephantLike attack service
```

## Configuration

### Scale

`body_length="100"` on the Horse item, so `SetInitialAgentScale(1.0)`: **the ram ships at its
authored size and is deliberately not shrunk.**

**It scales the mount only** (corrected 2026-09-23). This doc used to say it scales the dwarf too:
`EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are the same value (10), the scale block in
`BuildAgent` has no `IsMount` guard, and `BuildAgent` runs for the rider with the Horse item in slot 10,
so the managed trace predicts a scaled rider. In game the rider is not resized: the 3x mumakil's rider
stands at 1x beside its 1x crew ([mumakil.md](mumakil.md), "RESOLVED"). The ram stays at 100 because that
is its authored size, not to protect the dwarf. What a resize would NOT scale is the head-butt's reach,
a fixed metre from the centre in `WarRamConfig` (the elk, built at 2x on the same shape, derives its reach
from `ElkConfig.AuthoredScale`).

It was 75 first, reasoning that since TAOM dwarves render at about 82% of human height, a horse-sized
ram would dwarf its rider. **That reasoning was backwards: the war goat is meant to dwarf its rider.**
The film reference (Dáin at the Battle of the Five Armies) is a beast taller at the horns than the
dwarf on it, with the rider perched high and his feet well clear of the ground. 75 read visibly too
small in game.

The mesh is authored at exactly vanilla horse scale, confirmed by measurement rather than assumption:
`horsepelvis` sits at **1.396 m** in this rig against vanilla horse's declared
`standing_pelvis_height="1.40"`. So 1.0 is the artist's intended size.

| | At `body_length="100"` | Against a ~1.47 m dwarf |
|---|---|---|
| Length | 2.39 m | |
| Width | 1.07 m | |
| Top of horns | 2.27 m | 154% of the dwarf |
| Back (seat) | 1.40 m | 95% of the dwarf |
| Chest | 1.30 m | |

For reference a vanilla horse's back is 1.40 m under a ~1.80 m human (78%), so the ram reads as a
markedly bigger animal relative to its rider than a horse does, which is the intent.

### Combat tuning: `Main/Features/WarRam/WarRamConfig.cs`

| Constant | Value | Rationale |
|---|---|---|
| `AttackMinDamage` / `AttackMaxDamage` | 40 / 50 | Since 2026-09-18 (Mike; was 18 / 28, set while the attack still swept every enemy in the radius). One enemy per butt, every 10 s. The butt IGNORES armor (`CustomAttacksUtils.TakeDamage` writes the inflicted damage directly), so 40-50 lands in full on an Armored Troll; for scale a mid-tier sword swings 25-35 before armor, the warg bite tops out near 60, the elephant trample is 50-100 |
| `AttackCooldownSeconds` | 10.0 | Level with the elephant's trample. Was 6.0 and played as overpowered, see below |
| `AttackBlowMagnitude` | 35f | Below the elephant's 50f: a horse-scale creature should stagger, not launch |
| `AttackTriggerRange` / `AttackRadius` | 1.5f / 2f | Was 2.5f / 3.5f, near the unscaled elephant's 3f / 4f. See below |
| `AttackSingleTarget` | true | The head-butt hits ONE enemy inside `AttackRadius`, the one the ram faces most squarely (nearer on a tie, `SingleVictimPick`). Radial until 2026-09-18; the elephant and mumakil tramples stay radial. A shield-blocker straight ahead takes the butt (12 damage at most, no knockdown) and nobody else is hit. A mounted enemy is picked as the rider, never the horse: in the area version the blow log shows the butt hitting mounted lords and never their horses |

**Since 2026-09-18 the head-butt hits one enemy** (#618, Mike: "only hit 1 person not AOE"). A Custom Battle
test logged 1,300 head-butt blows from 127 rams across two battles, several victims per butt. The profile's
`SingleTarget` switch makes the shared attack task collect the enemies inside `AttackRadius` and hit only the
one faced most squarely, the same best-facing rule the engage gate commits on; the elephant and mumakil
profiles pass nothing and keep the sweep. The history below is why the radius and cooldown are where they are.

**The kick was an AoE with a knockdown, and that is what made the per-hit numbers misleading.**
`ElephantLikeAttackTasks` scans `GetNearbyAgents(creature.Position, AttackRadius)` and sweeps **every**
enemy inside it, rolling damage independently per victim and passing `knockDown: !blocking`, so
everyone caught who is not actively shield-blocking goes prone. At the original 3.5f that was close to
the base war elephant's 4f, meaning one dwarf on a goat cleared a formation on a shorter cooldown than
the elephant had.

The compounding factor is population, not per-hit power. An elephant is a rare single unit; rams field
**fifteen to a stack** in an Erebor lord's party, so a short cooldown on a knockdown AoE multiplies
across the stack. Retuned 2026-09-04 to a 10s cooldown and a 2f radius, which keeps the kick to what
the ram is standing on top of. Damage, block scaling and the knockdown rule are unchanged.

`AttackTriggerRange` had to move with the radius, and the constraint is load-bearing rather than
advisory. `ElephantLikeEngageDecorator` runs **one** scan at `AttackRadius` and then filters those
results by `AttackTriggerRange`, so any trigger range above the radius is unreachable code and the
constant would read as a number the ram never uses. It is held at 75% of the radius, the ratio the
original 2.5/3.5 pair chose, and that margin matters more at 10s than it did at 6s: an attack
committed against a target loitering on the rim now wastes a ten-second window.
| `AttackActionName` | `act_war_ram_butt` | **The bespoke head-butt** (master `act_war_ram_butt`, clip `war_ram_butt`, 30 posed frames plus a 2.5 s head-down hold), bound in the Armory's `as_war_ram` and typed `actt_kick` (`ActionCodeType.Kick = 28`) in the Armory's `action_types.xml`. All four profile slots hold this one action, so `IsAttack` means exactly "mid-butt". Was `act_horse_kick` until 2026-09-18 |

**Why a separate action, and why `actt_kick`.** The horse `monster_usage_set` the ram inherits names
`act_horse_kick` as its `kick_action`, so the engine fires that action itself at whatever stands behind the
mount; re-pointing its animation would have made a rear kick play a forward head-butt. `actt_kick` is the
type the ram attacked with from #515 on, and the two candidates rejected then explain why the type matters
(vanilla horses have no attack animation at all: they damage by charge collision, so `monster_usage_strikes`
is the mount's hit-REACTION table rather than an attack table):

- **`act_horse_rear`** is typed `actt_rear` (`ActionCodeType.Rear = 47`). The inherited `horse` usage
  set declares `rear_action="act_horse_rear"`, so the engine fires it on a damaged mount, and
  `Agent.Mount` refuses a mount whose channel-0 action is `Rear`. Forcing it every cooldown would have
  made the ram briefly **unmountable mid-fight**, on the one mount built to be player-rideable.
- **`act_horse_strike_front`/`_back`** play the horse's hit reactions (`horse_hit_from_front`/`_back`), so
  the ram would have flinched as though hit while we emitted damage. Their type, `actt_mount_strike`
  (`ActionCodeType.MountStrike = 52`), is not the problem (corrected 2026-09-18): `Agent.IsInBeingStruckAction` tests `MBMath.IsBetween(type, 48, 52)`, which is half-open, so it reads 48 to 51 (`StrikeLight` .. `StrikeKnockBack`) as being struck and NOT `MountStrike` (52).

`act_horse_kick` (`actt_kick`, `ActionCodeType.Kick = 28`) sits outside both bands and was the rig's only
offensive action, so it stood in as a buck until the bespoke clip existed. Both rejections were caught in
review; RCA in [rca-war-ram-2026-08-28.md](../reviews/rca-war-ram-2026-08-28.md). The warg's own attacks are
typed `actt_mount_strike` and play, which the half-open band explains: 52 is outside it.

**The head-butt clip, 2026-09-18 (#618; plays correctly in the Kit since 12:36).** The clip authored on 2026-08-29 (`E:\LOTRAOMAssets\WarRam\clips\act_war_ram_butt.fbx`,
39-bone goat mesh rig, `horseneck1` under `horsetail3`) was re-authored on the engine `horse_skeleton` with
`tools/blender/transfer_clip_to_engine_rig.py` (rest-relative world-rotation transfer, Kit yaw baked, rest frame 0,
Y/X export; 0.0 deg transfer error on all 32 bones). Its first Kit import stood the ram on its head: the Kit stores
bone tracks in FBX node order and the engine reads them in `horse_skeleton`'s file order, which lists the neck
last (skeleton-authoring fact 4); the export now carries `horseneck1` under `horsetail3` (TaleWorlds' own goat
FBX does the same) so the two orders agree, with engine-relative locals. It replaced `AssetSources/creature/ram/animations/war_ram_butt.fbx`
(old file backed up under `E:\LOTRAOMAssets\WarRam\_armory_source_backup_20260918\`) and is compiled as master
`act_war_ram_butt` in `Assets/creature/ram/animations/war_ram_butt_geo.tpac` with the clip `war_ram_butt`
(`war_ram_butt_anm.tpac`, Source1 = 1, Source2 = 105, 3.5 s since the hold export's reimport and the rewire at
14:04; priority 34 and blends 0.2 / 0.4 since 15:26). A Kit reimport of the FBX keeps the master GUID while the take name matches; when it does not
(the `.001` mishap) the master is re-created and `tools/wire_anim_master_clip.ps1` re-points the clip and
sets `horse_skeleton` on the master. Bound the same day: `act_war_ram_butt` (`actt_kick`) in the Armory's
`action_types.xml`, `as_war_ram` / `_town_and_village` / `_map` over the vanilla horse sets, the Monster on
`as_war_ram`, `WarRamConfig.AttackActionName` switched, `WarRamConfigTests` pinning it. **Clip priority:** the
first Custom Battle (15:16) logged the butt firing 1,300 times while Mike could not see the head drop; the clip
still had the Kit's priority 0, so locomotion on channel 0 overrides it at once. It now carries vanilla
`horse_kick`'s priority 34 and blends 0.2 / 0.4, the action it replaced and one the engine plays visibly on this
rig. Priority alone did not show it in the second battle (15:47), so the clip now also carries the warg attack's
flags, which are `horse_kick`'s too: `enforce_lowerbody` + `enforce_all` (every vanilla horse clip read carries
`enforce_lowerbody`, even the priority-2 hit reaction). The clip is now `warg_attack_stand`'s recipe minus its
`CombatParameterId`, which drives the engine's own hit detection; the ram's damage comes from the attack task. The
legs follow the clip for the 3.5 s butt-and-hold, so watch for gliding while moving.

**Verifying from the logs (2026-09-18).** The attack task plays the action and applies damage in the SAME tick, so a
landed butt proves nothing about the animation. The butt has a unique fingerprint in the TAOM log
(`bin\Win64_Shipping_Client\Logs\taom_debug_<start>.log`): a `[BlowDiag] blow` line with `dmgType=Pierce`,
`mag=35` (`AttackBlowMagnitude`, no other constant is 35) and `missile=False`; grouping by `attackerIdx` and second
gives victims per butt. `[WarRam] ... act_none` at mission start means the binding did not load; `[MissionDiag]
ActionSet 'as_war_ram'` confirms the Monster's set. The engine log is `C:\ProgramData\Mount and Blade II
Bannerlord\logs\rgl_log_<pid>.txt`. Before a test, confirm the deployed `TAOM.dll` holds the change and the game
started after it was written, and that the Kit has loaded once after any on-disk tpac edit (it re-cooks the `.rdc`). Mike deleted the six experiment masters
and the five `new_animation_clip*` clips in the Kit the same day.

**No mount-lock.** `TaomAgentStatCalculateModel` gates the elephant, spider and mumakil with
`CanAgentRideMount=false` and `MountDifficulty=999` so players cannot steal them. The war ram is a
player-rideable culture mount with a career built around it, so it is deliberately not gated. This
matches the chariot, which is also left remountable on purpose.

**No Patch47 entry.** The spider and elephant needed the dismount-before-death patch because their
Monsters lacked vanilla's rider-death surface. The ram inherits that surface whole through
`base_monster="horse"`. A ridden-death in-game test is what would justify revisiting this.

### Player-ridden rams attack too (#643, 2026-09-23)

The head-butt fires under ANY rider, the player included, as the warg's bite does (Mike: "All trees like the elephant, mumakil, the Rams, and Elk should also do these things when controlled by a player. Like the warg"). The tree used to gate its attack branch on `IsAiControlledDecorator`, so a player-ridden mount fell through to a one-second sleep and never attacked; the branch now sits directly under `HasRiderDecorator`. The attack stays automatic (enemy in front, in range, off cooldown), the rider keeps steering, and the knockdown rule is unchanged: anyone not shield-blocking goes down, a mounted victim is dismounted.

**The blow is the rider's**, the ram's only once the rider has gone (the warg's rule), and the combat log reads Pierce instead of Blunt; the engine evidence for both is in [elephant.md](elephant.md), "Player-ridden elephants attack too". The head-butt keeps its own Pierce band and does not scale with the rider's charge bonus; the elk's antler charge does ([elk.md](elk.md)).

## Key Files

| Path | Role |
|---|---|
| `Main/Features/WarRam/WarRamConfig.cs` | Monster id and all tuning |
| `Main/Features/WarRam/IWarRamAttackService.cs` | Marker sub-interface of `IElephantLikeAttackService` (the documented per-creature IoC pattern) |
| `Main/Features/WarRam/WarRamAttackService.cs` | Ram-tuned damage bands over the shared service |
| `Main/Features/WarRam/WarRamCombat.cs` | Static `ElephantLikeCombatProfile` wiring `act_war_ram_butt` into all four slots |
| `TAOM.Tests/Features/WarRam/WarRamConfigTests.cs` | Pins the action name, the four equal slots and cooldown > clip length |
| `tools/blender/transfer_clip_to_engine_rig.py` | Authors the head-butt FBX on the engine rig (frames, order, rest frame 0, hold) |
| `tools/wire_anim_master_clip.ps1` | Re-points the clip and sets the master skeleton after a fresh reimport |
| `Main/Features/WarRam/WarRamBehaviorTree.cs` | Mumakil shell with only the butt branch wired, no new node classes |
| `Main/Features/WarRam/WarRamMissionBehavior.cs` | **`: MissionLogic`**, attaches keyed on `Monster.StringId` |
| `Main/Features/WarRam/WarRamIoC.cs` | Registration helper |
| `Main/IoC.cs:114`, `Main/SubModule.cs:1559` | The two single-owner wiring lines |
| `Main/_Module/ModuleData/troops/troops_erebor.xml` | The six Ironpass ram cavalry troops (16 to 41) |
| `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Erebor.cs` | Pools `ironpass_ram_herder` as the branch root |
| `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | The Gems gate at 31+ |
| `Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml` | Erebor emissary offers for the three gated tiers |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | 2.0 for the armoured rungs, herder left at the 1.0 default |
| `tools/taom_schema.py` | `WAR_RAM_MOUNT_IDS` allowlist for `MOUNTED_DWARF` |
| **`docs/reference/lotrlome-war-ram-changes.md`** | **The external-module ledger. `LOTRLOME_Armory` is not tracked by this repo and a reinstall reverts it** |

## Troop tree

Two ways in. `ironpass_ram_herder` (16) is an `is_basic_troop` root at the same depth as
`ironpass_warrior`, and it is the one that is pooled for volunteer recruitment. The original edge off
`ironpass_warrior` is untouched, so the foot line still reaches the rams as well:

```
ironpass_recruit (11)  [pooled]
 `- ironpass_warrior (16)
     |- ironpass_infantry (21)  [existing]
     |- ironpass_arbalest (21)  [existing]
     `- ironpass_ram_rider (21) ...

ironpass_ram_herder (16)  [pooled, is_basic_troop]
 `- ironpass_ram_rider (21) -> ironpass_goat_charger (26)
                            -> ironpass_ram_breaker (31)   [Gems]
                            -> ironpass_ram_vanguard (36)  [Gems]
                            -> ironpass_ram_marshal (41)   [Gems]
```

Every rung steps by exactly 5. That is worth stating because it was not true in the first draft: the
herder shipped at 11 and jumped +10 into the rider. Seventeen such edges exist in the repo and none
is an error, but 656 edges are +5, so the ladder now follows the dominant pattern.

Each troop carries four equipment rosters alternating both pelts and, above the herder, both barding
variants of its grade, so all ten items are reachable in play. The barding ladder spreads eight items
over five armoured rungs, so `med_b` and `heavy_a` each serve two rungs. Average armour still climbs
monotonically:

| troop | level | tier | bardings | avg armour |
|---|---|---|---|---|
| `ironpass_ram_herder` | 16 | T3 | `light_a` on all four sets | 20 |
| `ironpass_ram_rider` | 21 | T4 | `light_a`, `light_b` | 22 |
| `ironpass_goat_charger` | 26 | T5 | `med_a`, `med_b` | 32 |
| `ironpass_ram_breaker` | 31 | T6 | `med_b`, `heavy_a` | 37 |
| `ironpass_ram_vanguard` | 36 | T7 | `heavy_a`, `heavy_b` | 42 |
| `ironpass_ram_marshal` | 41 | T8 | `elite_a`, `elite_b` | 52 |

### Skills: dwarves are the worst riders in the game

Athletics, OneHanded, Polearm, Bow and Crossbow sit exactly on `CAVALRY_BASELINES` in
`tools/rebalance_troops.py`. Two skills deviate, in opposite directions, and both deviations are
deliberate.

**Riding runs far below baseline at every rung**, by roughly a quarter throughout, so dwarves are not
merely the worst riders in the game but visibly so. Measured mean Riding by culture and level:

| culture | 16 | 21 | 26 | 31 | 36 | 41 |
|---|---|---|---|---|---|---|
| **Erebor (war ram)** | **65** | **90** | **125** | **165** | **205** | **245** |
| baseline | 95 | 120 | 160 | 210 | 270 | 320 |
| Dol Guldur | 90 | 115 | 155 | 205 | 265 | - |
| Isengard | 100 | 125 | 165 | - | - | - |
| Harad | 110 | 145 | 188 | 225 | - | - |
| Gundabad | - | 145 | 195 | 205 | 265 | 315 |
| Rohan | 119 | 148 | 190 | 241 | 300 | 340 |
| Lindon / Rivendell | - | - | - | - | 300 | 360 |

Erebor is the lowest at every level it fields a mount, and by a wide margin: at 41 the marshal rides
245 against Gundabad's 315, Rohan's 340 and the elves' 360. It was nominally lowest before this pass
too (120/160/200/240/300), but only by a handful of points, which read as a rounding difference
rather than a racial trait.

**TwoHanded and Throwing run above baseline**, and that is forced rather than chosen.
`ironpass_warrior` (16) carries TwoHanded 105 and Throwing 40, and it upgrades into
`ironpass_ram_rider` (21), whose baseline values are 60 and 30. Straight baseline would be a skill
regression across that edge and `TroopUpgradeSkillMonotonicityTests` fails it. Dwarves being heavy
two-handed axe fighters makes the forced floor coherent anyway, so the curve keeps it rather than
weakening the warrior to fit.

The herder originally filled no `HorseHarness` slot at all, and this doc called that deliberate.
It was a defect, corrected on 2026-09-06 after players reported dwarves riding bareback.

The reasoning was half right. The engine does draw each equipment slot from an independently
chosen set, so what matters is that all four sets agree: filling three of them would ship a ram
that sometimes spawns bare anyway. That constraint still holds, and the fix respects it by putting
`taom_ram_barding_light_a` in all four.

What the reasoning missed is that for this mount the harness is not only armour. `sk_eb_goat_a`
and `sk_eb_goat_b` are bare pelts, and the rider's seat is modelled on the eight
`sk_eb_goat_bard_*` meshes instead. So an unharnessed ram is not an unarmoured ram, it is an
unsaddled one.

The vanilla horse hides this, and so does the warg, which carries a seat on the mount body
(`warg_low_fur_with_saddle`) and ships a separate `warg_saddle` harness on top. Nothing in the
data distinguishes those from the ram, which is why review read the empty slot as a styling
choice. **So the rule is now universal: a `HorseHarness` is required beside every `Horse` slot,
and an exception has to be argued in `_HARNESSLESS_BY_DESIGN` rather than assumed.** A first
attempt scoped the check to a pinned set of mounts believed saddleless, which repeated the
original mistake by deciding from a desk which mounts were fine bare; it also rested on the false
belief that the spider carries its own seat, when in fact no spider harness item has ever been
authored and that rider is still unsaddled today.

Two gates hold it: `MOUNT_WITHOUT_HARNESS` in `tools/taom_schema.py` and
`EveryWarRamRoster_InTroopsErebor_FillsTheHarnessSlot` in `CareerCultureCoverageTests`. The
universal form immediately found a second case the scoped form would have missed:
`eomer_bat_equipment` carried the full Theoden kit on a bare `Item.charger` while all fourteen of
its Rohan siblings had a harness.

## Recruitment, and why the Gems gate starts at 31

Before this, the branch was in no volunteer pool at all. #515 left it out on the reasoning that a
village notable handing out an armoured level-21 rider would let a player skip the whole Ironpass
foot progression. The reasoning held; the effect was that players never saw rams. The herder answers
the objection instead of reversing it, because a player who picks the ram branch still starts at the
bottom of a branch.

The gate level is not a taste call. Vanilla `RecruitmentCampaignBehavior.UpdateVolunteersOfNotables-
InSettlement` only promotes a notable's slot while `Tier < MaxVolunteerTier`, and TAOM's
`VolunteerTierService.MaxVolunteerTier` is 6, which is levels 31-35. So a slot seeded with the herder
climbs to `ironpass_ram_breaker` and stops. Level 31 is exactly where notable promotion ends, which
makes it the natural place for the Gems cost to begin.

Three routes, all data-driven through `special_resources/troop_resource_costs.xml`:

| route | field | reaches |
|---|---|---|
| notable volunteer | `recruit_cost` | the breaker only; the two above it are unreachable by promotion |
| party-screen upgrade | `upgrade_cost` | breaker, vanguard, marshal |
| Erebor emissary at `town_E1` | `merchant_cost` | breaker, vanguard, marshal |

`upgrade_cost` and `recruit_cost` charge the **player** only (Patch26 hooks `PartyScreenLogic`,
Patch51 hooks `RecruitmentVM`), so AI lords still field and promote rams for free. That is the point:
the change is meant to put more rams in the world, not fewer.

`ironpass_ram_herder` is deliberately absent from `troop_weights.xml` and falls through to the 1.0
default. It is the rung a player recruits repeatedly, and a 2.0 entry unit would deflate the
party-size limit for exactly the players the branch is meant to reach. The five armoured rungs are
all 2.0. It stays at 1.0 now that it carries light barding too: the justification was always
recruitment economics, not how much armour the troop wears.

Loadout is spear plus metal shield plus axe sidearm, built only from item ids already present in
shipped Erebor rosters. That was deliberate: `sm_dwarf_erebor_spear_a` is a
`<CraftedItem crafting_template="TwoHandedPolearm">`, which is exactly the shield-plus-polearm defect
class CLAUDE.md records as having shipped three times. `tools/audit_polearm_shield_parity.py`
**passes** on the new rosters, and reusing already-shipped pairings is why.

## Lords, and being seen on the campaign map

**The campaign map icon draws only the party leader.** `MobilePartyVisual` calls
`AddCharacterToPartyIcon` exactly once, with `PartyBaseHelper.GetVisualPartyLeader(party)`. Troops in
the party contribute nothing to the icon. So a ram is visible on the map only when a LORD is riding
one, and before this change Erebor lord equipment carried zero Horse slots against Gondor's 52.

The rendering itself is free. `AddCharacterToPartyIcon` clones the character's full equipment
including the Horse and HorseHarness slots, and the mount resolves its animation through
`monster.ActionSetCode + "_map"`, which for the ram is `as_horse_map` and already exists in Native. A
custom-skeleton creature would have needed its own `_map` set authored, and a missing one is the
elephant "Crash #4" native AV class.

**Who rides:** the three tier-3 frontier clans, Bit Gror, Bit Róri and Bit Nórin
(`lord_E4_*`, `lord_E6_*`, `lord_E7_*`), 12 of 37 Erebor lords. `clans.xml` declares
`clan_erebor_N owner="Hero.lord_EN_1"`, so the lord id prefix is the clan.

Worth recording because it caused a false start: **"Iron Hills" and "Ironpass" are troop-line labels
only.** They are not clans, factions or settlements. All seven Erebor clans are "Bit …" named, all
home at `town_E1`, and all seven field both troop lines. The Erebor settlements are Erebor, Járnfast,
Skárhald, Azanûlibar-dûm and nine castles including Irongap. Any future "give X to the Iron Hills"
request needs the same translation into clans.

**How, and why not the obvious way:** all five `erebor_bat_template_medium_a..e` sets are shared
across every lord group (7 lords each), so adding a mount to one would have mounted lords in all
seven clans. Instead `erebor_bat_template_ram_a..e` are verbatim clones plus the two mount slots, and
only the 12 target lords are repointed at them. Each lord therefore keeps the exact gear he already
wore. Their `default_group` moved Infantry to Cavalry: for a hero that only drives the party-screen
icon and tooltips, since `GetFormationClass` reads `BattleEquipment` instead, but leaving it Infantry
would have shown a mounted lord as infantry.

Lords get **heavy** barding, not elite, leaving the top of the ladder as headroom. The re-spread did
not disturb that: heavy is now the `ironpass_ram_vanguard` (36) grade rather than the breaker's, and
elite moved up to the new `ironpass_ram_marshal` (41), so the lords keep exactly the items they had
and the headroom above them grew by a rung. `erebor_bat_template_ram_a..e` are unchanged.
**Dáin II Ironfoot stays on foot** by decision; his bespoke `dain_bat_equipment` is untouched.
Civilian sets are untouched throughout, so no lord rides a war ram into a tavern.

## The player: selecting the Ram Rider career

`CareerStartingEquipmentService` maps a career to a starting roster by archetype:
`player_career_{culture}_{archetype}_{m|f}` (`CareerEquipmentRosterIds.Build`). `ram_rider` is
`CareerArchetype.Cavalry`, and it is the **only** Erebor Cavalry career (`ironguard` is Infantry,
`crossbow_master` is Ranged), so `player_career_erebor_cavalry_m` / `_f` belong to it alone.

**That roster was equipping `Item.saddle_horse` plus `Item.light_harness`: a vanilla horse on a
dwarf.** It now equips `taom_war_ram_a` with `taom_ram_barding_light_a`.

This was a live bug, not a gap left by this feature, and it is worth understanding why nothing caught
it. `MOUNTED_DWARF` scans `NPCCharacter` definitions and the named rosters they reference. These
career rosters are applied to the player **at runtime**, so no `NPCCharacter` ever names one and the
schema sweep cannot reach them. Two shipped-data tests in
`TAOM.Tests/Features/CharacterCreation/CareerCultureCoverageTests.cs` now close that hole: one asserts
no `player_career_erebor_*` roster equips a non-ram mount, the other asserts the cavalry roster
actually grants a ram, so deleting the mount does not make the first pass trivially. Both were
mutation-tested by reintroducing the bug.

The mount's `difficulty` was also dropped from 30 to 0 for this reason: the career hands a starting
player the ram, `CheckSkillForMounting` compares effective Riding against `MountDifficulty`, and a
low-Riding character would have been unable to remount after a dismount.

**Not changed:** the roster's `Item.battered_kite_shield` is a vanilla shield on a dwarf starter.
Cosmetically odd and pre-existing, but out of scope for a mount change.

## Dependencies

- **`LOTRLOME_Armory`** (external, untracked): the Monster, the ten items, the `CanRide` flip and the
  `SubModule.xml` registration. See the ledger.
- **Vanilla `Native`**: `horse_skeleton`, `as_horse`, `monster_usage_set id="horse"`, and
  `Monster.horse` as the `base_monster`. Cross-module inheritance from Native into the Armory is
  already proven and shipping: `elf_child` and `sauron_child` both use `base_monster="human"`, which
  only Native defines.
- **`Main/Features/ElephantLike/`**: the shared attack service and BT nodes, reused unchanged.

**Dependent: the great elk (#636).** `Monster.taom_elk` names this Monster's action set, `as_war_ram`, and
plays `act_war_ram_butt` as its antler charge ([elk.md](elk.md)). Renaming the set, pointing the ram back at
`as_horse`, restoring the ledger's section 5 backups or re-cutting the `war_ram_butt` clip reaches the elk too;
a missing `as_war_ram_map` throws on every campaign-map icon that carries one.

## Tests

`TAOM.Tests/Features/WarRam/WarRamAttackServiceTests.cs`, 24 tests mirroring
`MumakilAttackServiceTests` one for one: `IsCreatureMonster` x3, `ShouldEngage` x5, `IsOffCooldown`
x6, `ComputeInflictedDamage` x10 including NaN, clamp and future-timestamp cases.

`tools/tests/test_validate_moduledata.py` gained 9 `MOUNTED_DWARF` cases, including the one that
matters most: **a dwarf carrying both a ram and a horse still errors on the horse**. The allowlist
work also fixed a latent hole, since the old `_first_mount` was first-wins and a ram listed ahead of
a horse would have masked it.

`TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` pins the recruitment
side: `EreborRamCavalry_IsNotOfferedByAnyVolunteerPool` (the five armoured ids stay upgrade-only),
`EreborRamCavalry_IsReachableFromAPooledRoot`, the two `RollsAgainstTotalWeightEighteen` guards, and
two `HighestRoll_ReturnsRamHerder` cases for the herder band. `TroopUpgradeSkillMonotonicityTests`
is what pins the dwarf cavalry curve, including the forced TwoHanded and Throwing floors.

Per ADR-008 the BT nodes and mission behavior are not unit-tested; they are tested in game.

## How to verify in game

**A full game restart is mandatory.** New item XML registers only at process launch, so a green
validator plus a naked or riderless troop means "file not loaded", not "data defect".

1. Inventory tableau and party-screen thumbnail
2. Custom battle: the ram spawns **with** its rider, not a lone dwarf on foot (a riderless mount is
   the silent skeleton-drop failure)
3. **Barding renders.** This is currently expected to FAIL until the asset packages are rebuilt: see
   the ledger's "Still owed"
4. Seat check: the dwarf sits on the ram, not inside it. Tune the rider adders on the Monster
5. Charge, including jumps
6. Prolonged melee: the butt attack fires and reads correctly. Watch the log for any `act_none`
   resolution
7. **Rider dies while mounted**, and **ram dies while ridden**. These are the paths that regress with
   zero managed diff, and the reason gotcha #18 exists
8. Player dwarf dismounts and remounts. This is what the `CanRide` flip buys

Any CTD goes to `/native-crash-triage`, never a blind retry.

## Known gaps

- **The rider seat is untuned**, pending the in-game check.
- **`body_length="100"` was set after an in-game look**, replacing a derived 75 that read too small. The ram now ships at authored size.
- **Item and troop names are not yet translated.** English works through the inline `{=KEY}default`.
  This now covers six troop names, not four: `aom_ironpass_ram_herder_name` and
  `aom_ironpass_ram_marshal_name` joined the backlog.

Resolved since the first draft: the eight barding meshes are in the cooked packs.
`python tools/validate_mesh_refs.py` exits 0 with no missing visual mesh, so the earlier
"barding does not render" entry no longer applies.

## GitHub Issue

[#515](https://github.com/haterade22/TAOM/issues/515)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
- [docs/features/moduledata-validation.md](./moduledata-validation.md)
- [docs/features/mumakil.md](./mumakil.md)
- [docs/features/no-mount-cultures.md](./no-mount-cultures.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/modding/recipe-add-a-race-or-creature.md](../modding/recipe-add-a-race-or-creature.md)
- [docs/reference/bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/lotrlome-war-ram-changes.md](../reference/lotrlome-war-ram-changes.md)
- [docs/reviews/lessons/xslt-moduledata.md](../reviews/lessons/xslt-moduledata.md)
- [docs/reviews/rca-war-ram-2026-08-28.md](../reviews/rca-war-ram-2026-08-28.md)

<!-- backlinks-end -->
