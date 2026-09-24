# Great Elk

## Overview

The great elk: a rideable mount for Mirkwood's top cavalry troop, `mirkwood_beleglas`. Until 2026-09-23 it also
carried Thranduil, the Mirkwood lords, the lower cavalry and the `elk_rider` career start; Mike then gave Thranduil
and the lords the Animalia moose, and the lower cavalry and the career start the Animalia elk (#646,
[animalia-elk-moose.md](animalia-elk-moose.md)). It is built exactly as the [war ram](war-ram.md): the elk mesh is skinned to
the vanilla horse skeleton, so the engine rides it as a horse, and a per-agent behavior tree gives it one
attack, an **antler charge**, which is the ram's head-butt clip playing on the elk.

## Why This Exists

- **Vanilla behaviour:** nothing; the elk is TAOM content.
- **TAOM requirement:** Mirkwood already shipped an `elk_rider` career (`career_system/taom_careers.xml`,
  `CareerArchetype.Cavalry` in `CareerSystemIoC.cs`): "a mounted warrior of Thranduil's guard who rides great
  forest elk into battle", with an "Antler Crash" ability. No elk existed. Its starting rosters,
  `player_career_mirkwood_cavalry_m` / `_f`, handed the player `saddle_horse` with `light_harness`, the same gap
  the ram closed for Erebor's `ram_rider` (#515).
- **Without it:** the elk career and Thranduil ride vanilla horses, and the art imported into the Armory
  (`elk_001`, `elk_saddle_001`) is unused.

## Architecture

### Design Challenge

Almost none, and that is the point of the reskin route. `elk_001.fbx` carries only the vanilla horse bones: 39
bones under `horse_skeleton_notused` (`horsepelvis` to `horse_head`), the same bone set and parent links as the
ram's goat. So Phases 1 to 5 of [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
(clips, `quad_movement`, action types, action sets, usage sets, the rider partial) do not apply. What remained
was deciding how the elk gets its attack without authoring a clip.

### Solution Approach

**The Monster is the vanilla horse shape** (`ModuleData/Monsters/LOTR/lotr_monster_elk.xml` in the Armory):

```xml
<Monster id="taom_elk" base_monster="horse" action_set="as_war_ram"
         weight="500" hit_points="250" />
```

`base_monster="horse"` inherits `Flags`, `family_type="1"`, `monster_usage="horse"`, `num_paces="6"`, every
bone, the slope block and all twelve rein attributes (the ram's ledger explains the deserialiser). Weight and
hit points are raised over the horse's 400 / 200 because the elk is the bigger animal.

**It names the ram's action set, `as_war_ram`.** The ram's head-butt, `act_war_ram_butt` (clip `war_ram_butt`,
typed `actt_kick`), is authored on the engine `horse_skeleton`, so it plays on any mesh skinned to that rig. On
the elk the same head-down pose lowers the antlers. `as_war_ram` is a three-line child of `as_horse` and already
ships its `_map` and `_town_and_village` children. Two monsters sharing one action set is proven (the elephant
and mumakil share `as_elephant`). A dedicated `as_elk` would add three sets and an action type for no behaviour
change; it earns its place only when an elk-specific clip is authored.

The trade is coupling: a change to `as_war_ram` or to the `war_ram_butt` clip reaches the elk too. `ElkConfig`
keeps the set and action as its own literals, matching what the elk's Monster names, so a future move of the
ram to a set of its own does not silently drag the elk's drift guard with it.

**The antler charge** is the ram's tree shape. `ElkBehaviorTree` runs one branch: a live enemy in front, within
1.65 m of the elk's centre (the ram's 1.5 m, scaled with the 1.1x body), charge off cooldown, then the shared trample
task plays the action and hits the one enemy the elk faces most squarely (the #618 single-target rule) with **one
60 Blunt blow** (Mike, 2026-09-23). He first asked for 40 blunt plus 20 pierce, then chose one blow once shown that
the two land alike when armour is ignored and the blunt part stays lethal. Armour does not reduce it; a shield block
quarters it (15) and saves the knockdown.

- **It stays lethal.** A Blunt killing blow wounds instead of killing unless its weapon carries `CanKillEvenIfBlunt`
  (`DefaultPartyHealingModel.GetSurvivalChance`, read by `SandboxAgentDecideKilledOrUnconsciousModel`, v1.5.3), so
  `CustomAttacksUtils.ComposeWeaponFlags` marks every Blunt synthetic blow with it (Mike: "keep it lethal"). With
  the flag a finishing charge is killed or wounded by the normal survival roll, exactly as a Pierce blow is.
- **The rider's career charge bonus scales it** (Mike: "scale the antler blows too"). The charge reads
  `ICareerAgentStatService.MountChargeMultiplier` when it fires: the rider hero's `MountChargeDamage` passive and the
  charge bonus of Antler Crash (the rider's own buff, or an ally buff under the rider's index). The elk's own
  `MountChargeDamage` is scaled by exactly that product, so the body charge and the antler blow move together for
  any product in (0, 10] (outside it the antler lands unscaled while the mount still applies it). The
  culture charge multiplier (#610) scales only the body charge.
- **The blow is the rider's**, the warg's rule: `Blow.OwnerId` is the rider's index, the player's included, and the
  elk owns the blow only if the rider has gone. A hit's affector is read from `OwnerId` (v1.5.3 `Agent.cs:5465`);
  the kill goes through native (`Agent.Die` hands the blow to `IMBAgent.Die`, `:4694`, which calls back
  `Mission.OnAgentRemoved` with the killer), and how native picks that killer is unread. The career's "+N from
  ability" line reads that owner too and would claim an ability share for a blow that skips the damage model, so it
  now skips TAOM's own blows (`CustomAttacksUtils.IsRegisteringSyntheticBlow`, true while the engine raises the hit
  callbacks for one); a punch or kick keeps its line, because the ability's damage bonus does reach it.
- **It fires under any rider, the player included** (#643, the warg's rule): the charge is automatic and the rider
  keeps steering. The engine's own charge collision carries on underneath.

**No mount-lock and no Patch47 entry**, as with the ram: Mirkwood's markets sell the elk (see Markets), so a
player can ride one, and the horse's rider-death surface is inherited whole.

### Component Diagram

```
troops_mirkwood.xml       mirkwood_beleglas (46, the top cavalry)
      |  slot="Horse"        -> Item.taom_elk_a        (mesh elk_001)
      |  slot="HorseHarness" -> Item.taom_elk_saddle_a (mesh elk_saddle_001)
      v
LOTRAOM_horses.xml  <Horse monster="Monster.taom_elk" body_length="100">   (placeholder: Items.xsd requires it)
      v
lotr_monster_elk.xml  base_monster="horse", action_set="as_war_ram" (the ram's set), taom_body_length="110"
      |  MonsterSizeService writes 110 into taom_elk_a's body_length at every game init
      v
engine: vanilla cavalry spawn. Movement, blows, deaths and rider seating are the horse's.
      +
ElkMissionBehavior : MissionLogic
      -> attaches ElkBehaviorTree per agent, keyed on Monster.StringId == "taom_elk"
      -> antler charge plays act_war_ram_butt; one 60 Blunt blow, owned by the rider and scaled by
         their career charge bonus; fires under any rider
```

## Configuration

### Combat tuning: `Main/Features/Elk/ElkConfig.cs`

Starts from the ram's; the reach grows with the elk's body and the damage is one 60 Blunt blow. Each lives in its own
config so they can be tuned apart. The size itself is not here: it is the Monster's `taom_body_length`
([monster-size.md](monster-size.md)).

| Constant | Value | Note |
|---|---|---|
| `AttackDamage` / `AttackDamageType` | 60 / `DamageTypes.Blunt` | One blow on one victim, lethal (`CanKillEvenIfBlunt`). Ignores armour: `CustomAttacksUtils.TakeDamage` writes the damage directly. A shield block quarters it (15); the rider's career charge bonus multiplies it, and a multiplier outside (0, 10] counts as 1 (`ElephantLikeAttackService.MaxRiderMultiplier`) |
| `AttackCooldownSeconds` | 10.0 | Must outlast the 3.5 s clip (butt plus head-down hold) |
| `ReachScalesWithBody` | true | The shared nodes multiply the two ranges below by the elk's live agent scale, which comes from the Monster's `taom_body_length` (110, so 1.65 m / 2.2 m). Replaced `AuthoredScale` (1.1f; 2.0f, then 1.2f, earlier on 2026-09-23) that evening |
| `AttackTriggerRange` / `AttackRadius` | 1.5 m / 2 m at 1.0x | Measured from the elk's centre, so they grow with the body. The engage decorator scans once at the radius and filters by the trigger range, so the range must stay inside the radius |
| `AttackSingleTarget` | true | One victim per charge |
| `AttackBlowMagnitude` | 35f | Staggers rather than launches |
| `AttackActionName` / `ActionSetId` | `act_war_ram_butt` / `as_war_ram` | All four profile slots hold the one action, so `IsAttack` means "mid-charge" |

### Items: Armory `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`

| Item | Stats | What it replaced |
|---|---|---|
| `taom_elk_a` "Great Elk" | maneuver 74, speed 62, charge 50 (40 until 2026-09-23, Mike), extra_health 20, `body_length="100"`, only the placeholder `Items.xsd` requires, since 2026-09-23 evening (its size, 110, is the Monster's `taom_body_length`; 200, then 120, earlier that day), `difficulty="0"`, value 1400 | troops: `noble_horse_southern` (78 / 68 / 24); lords: `charger` (67 / 48 / 22); career: `saddle_horse` (52 / 37 / 12) |
| `taom_elk_saddle_a` "[Mirkwood] Elk Saddle" | `body_armor="45"`, Leather, `family_type="1"` | troops: `saddle_of_aeneas` (76); lords: `chain_horse_harness` (55); career: `light_harness` (10) |

The saddle's 45 is lower than what the troops and lords wore, deliberately paired with more hit points: a mount
spawns at `Monster.HitPoints + extra_health`, so the elk has 270 (250 + 20) against 230 for `noble_horse_southern`
(200 + 30) and 220 for the `charger` (200 + 20). It is a first pass; tune both in the Armory.

**Two weights.** The engine's own blow math for a mount victim reads the item's `weight` (450, from
`SpawnEquipment[Horse].Weight`); TAOM's knockdown math reads `Monster.Weight` (500). Vanilla splits the same way
(`charger` 430 against `Monster.horse` 400), so retune the one that matters for the change you want.

**Size: `taom_body_length="110"` on the Monster, 1.1x: 1.72 m at the withers, 3.38 m to the antler tips.** Mike first
asked for 2x (2026-09-23 morning: "x2 the size ... maybe even bigger"), then reduced it that afternoon in Custom
Battles beside the Animalia elk and moose (#646): 120, then 110 ("still a bit too big"). That evening the size moved
off the item onto the Monster ("The monster xml should control the size of the animal"): `lotr_monster_elk.xml`
declares `taom_body_length`, TAOM copies it into `taom_elk_a`'s `body_length` at game init, and the item keeps only
the placeholder 100 the engine's `Items.xsd` requires ([monster-size.md](monster-size.md)). `elk_001` measures 1.56 m at the withers and 3.07 m overall at 1.0 (the
vanilla horse 1.57 / 2.18): its antlers are what make it tall. The engine scales the mount by `body_length / 100` at
build: skeleton, clips, capsules, and every mesh on it, the saddle included. **The rider is not scaled.** The managed
code reads as if the rider would be (`BuildAgent`'s scale block has no `IsMount` guard), but the 3x mumakil's rider
stands beside its 1x crew at 1x in game, so native ignores it ([mumakil.md](mumakil.md), "RESOLVED"). The elk shipped
at 100 for one night on the opposite belief, copied from the ram's docs. **The antler charge's reach follows the
body**: `ElkConfig`'s 1.5 m trigger and 2 m radius are the ram's at 1.0x, both measured from the elk's centre, and
the shared nodes multiply them by the elk's live agent scale (`ElkConfig.ReachScalesWithBody`), so the antlers strike
what they visibly reach at any size and a resize needs no rebuild. Going bigger still: raise the Monster's value, then
check the rider's legs against the wider back, gates and forest paths.

**Previews stay at 1x** (Mike, 2026-09-23: battle only). `body_length` scales the agent a battle builds. The
inventory and party-screen tableau scale a mount by the item's `scale_factor` instead (v1.5.3
`CharacterTableau.cs:1104`), and the map party icon by `scale_factor` x 0.3 (`MobilePartyVisual.cs:1135`).
`taom_elk_a` sets no `scale_factor`, so it reads 1 (`ItemObject.cs:566`). The elephant and mumakil preview at their
authored size the same way. `scale_factor="2"` on the item would change that; nobody has checked the preview camera or
the rider's seat at 2x.

**`body_length` is also a tournament stat, not an auto-resolve one.** A mount's `Effectiveness` is ((charge x speed +
maneuver x speed) + `body_length` x weight x 0.025) x (hit points + bonus) x 0.0001 (v1.5.3 `ItemObject.cs:945`),
cached at load, and its one reader is `CharacterObject.GetSimulationAttackPower`, whose one caller is the tournament
match simulator (`TournamentFightMissionController.cs:499`). So the size moves how an NPC tournament rates an elk
rider and nothing on the campaign. The size pass recomputes the cached value after it writes `body_length`
([monster-size.md](monster-size.md)).

**`difficulty="0"`** so any player can ride one they buy: `CheckSkillForMounting` compares Riding against
`MountDifficulty`. It was set when the career started a player on the elk; the start moved to the Animalia elk on
2026-09-23, which is `difficulty="0"` too.

### Who rides it

Since 2026-09-23 (Mike, #646) the great elk has one rider line. The rows it gave up are listed with where they went:

| Owner | File | Mount now | Reaches existing saves? |
|---|---|---|---|
| `mirkwood_beleglas` (46, top cavalry) | `troops/troops_mirkwood.xml` | **great elk** | Yes, after a restart: troop equipment is read from XML at launch |
| `mirkwood_rochenlas` (41, upgrades to beleglas) | same | Animalia elk | Yes, after a restart |
| Thranduil (`thranduil_bat_equipment`) | `equipmentsets/taom_equipment_sets_mirkwood.xml` | Animalia moose | **New campaign only**: a hero's battle equipment is saved (`Hero._battleEquipment`, `[SaveableProperty(210)]`), so an existing campaign keeps whatever he was created with: the `charger` in any campaign from a released build (v2.0.30 and earlier), the great elk only in one started on a development build between #636's lord wiring and this change |
| The 28 lords on `mirkwood_bat_template_medium_a..e` | same | Animalia moose | **New campaign only**, same reason and same two cases |
| Mirkwood heroes equipped from the lord and ruler templates: a hero coming of age, a new ruler after Thranduil, a companion raised to lord, and the spawned and rebel lords `spc_mirkwood_lord_1/2` | `equipmentsets/taom_lord_template_equipment.xml` (`taom_mirkwood_lord_battle_*`, `taom_mirkwood_ruler_battle_*`) | Animalia moose | Yes, for heroes equipped after the restart |
| `elk_rider` career start | `equipmentsets/taom_career_starting_equipment.xml` | Animalia elk | Character creation only |

**`taom_lord_template_equipment.xml` is generated, but was edited by hand.** `tools/generate_lord_template_equipment.py`
copies each culture's first `bat_template` roster (`mirkwood_bat_template_medium_a`) into those four battle rosters,
and its header says to regenerate after a source change. Do not: its culture list has drifted from the file, so
`--apply` today deletes 40 rosters (Bluecraig, Goblin, Lindon, Misty Mountain Orcs) and re-equips 78 more in
eight other cultures ([#637](https://github.com/haterade22/TAOM/issues/637)). A dry-run diff showed the elk
swap is the only slot difference in the Mirkwood rosters, so exactly those eight lines were changed. The moose
replaced the elk in the same Horse lines on 2026-09-23 (#646), and `AnimaliaMountWiringTests` pins them now.

Legolas keeps his horseless set. **The lords' civilian templates keep their `charger`**, as the ram's lords do,
so no lord rides an elk or the moose into a town. **The cavalry troops do**: their Horse and HorseHarness are
troop-level `<equipment>` overrides, and the engine writes those into every set the troop has, the civilian one
included (`BasicCharacterObject.Deserialize` adds the rosters, then `MBEquipmentRoster.AddOverriddenEquipments`
re-deserializes each override into every set). They rode `noble_horse_southern` into town the same way.

**On the campaign map a mount shows under a lord.** `SandBox.View.Map.Visuals.MobilePartyVisual` draws the party
leader from `CharacterObject.Equipment`, which for a hero is `HeroObject.BattleEquipment` (v1.5.3), and builds
the mount from `Equipment[Horse]` with `MBGlobals.GetActionSet(monster.ActionSetCode + "_map")`. That lookup
THROWS when the set is missing, so the Armory's `as_war_ram_map` backs every party icon carrying a great elk (a
player who rides one, or the lords of a campaign started on a development build that carried #636's lord wiring),
and `as_animalia_moose_map` the moose Thranduil and the lords ride in a new campaign. An Armory reinstall drops the
Monster, the items and the set together.

**Markets:** `culture_marketplace_config.xml` routes both items to Mirkwood with `min_stock="1"`. The Animalia
elk (the `elk_rider` start) is routed the same way since 2026-09-23, so a player who loses it buys the same animal.
The moose is not routed, but as a `Culture.mirkwood` item it sits in the Mirkwood culture pool and can be drawn into
a Mirkwood market like any Mirkwood item (Mike, 2026-09-23: it may be sold).

## Key Files

| Path | Role |
|---|---|
| `Main/Features/Elk/ElkConfig.cs` | Monster id, action and set names, all tuning |
| `Main/Features/Elk/IElkAttackService.cs`, `ElkAttackService.cs` | The shared elephant-like decision service bound to the elk's tuning |
| `Main/Features/Elk/ElkCombat.cs` | The `ElephantLikeCombatProfile` for the shared BT nodes: the Blunt type and `RiderChargeMultiplier`, which reads the rider's career charge bonus |
| `Main/Features/Elk/ElkBehaviorTree.cs` | The antler-charge tree |
| `Main/Features/Elk/ElkMissionBehavior.cs` | **`: MissionLogic`**; attaches keyed on `Monster.StringId`; drift guard for the shared action and set |
| `Main/Features/Elk/ElkIoC.cs`, `Main/IoC.cs`, `Main/SubModule.cs` | Registration |
| `TAOM.Tests/Features/Elk/` | `ElkConfigTests`, `ElkAttackServiceTests`, `ElkMountWiringTests` |
| **[lotrlome-elk-changes.md](../reference/lotrlome-elk-changes.md)** | **The external-module ledger. `LOTRLOME_Armory` is not tracked by this repo and a reinstall reverts it** |

## Dependencies

- **`LOTRLOME_Armory`** (external, untracked): the Monster, both items, the `SubModule.xml` registration, and the
  ram's `as_war_ram` / `act_war_ram_butt` / `war_ram_butt` clip, which the elk borrows.
- **Vanilla `Native`**: `horse_skeleton`, `as_horse`, `monster_usage_set id="horse"`, `Monster.horse`.
- **`Main/Features/ElephantLike/`**: the shared attack service and BT nodes. The elk added two things to the
  profile, a damage type and a rider multiplier, which the Animalia elk and moose use too (#646) and the ram,
  elephant and mumakil leave at Pierce and none; #646 added a third, `reachScalesWithBody`, which the elk sets.
- **`Main/Features/MonsterSize/`**: copies the Monster's `taom_body_length` into `taom_elk_a` at game init
  ([monster-size.md](monster-size.md)).
- **Dependents of this feature (#646):** the Animalia elk and moose sit on `taom_elk_saddle_a` too, in 13 real
  rosters and the two test riders. Rolling `LOTRAOM_horses.xml` back to `.bak-elk-20260922` (the ledger's
  rollback) would also delete both Animalia items and take the seat from under those rosters: restore from a later
  backup, or redo the Animalia items after (`lotrlome-animalia-changes.md`).
- **`Main/Features/CareerSystem/`**: `ICareerAgentStatService.MountChargeMultiplier`, the same product
  `ApplyMountStatModifiers` applies to the mount.

## Tests

- `ElkConfigTests` (11): the Monster id, and that it differs from the ram's so the two behaviors never attach a
  tree to one agent; the shared action and set; the four equal slots; single target; cooldown longer than the
  clip; trigger range inside the radius; the reach at 1.0x with `ReachScalesWithBody` set; the live Monster
  declaring a valid `taom_body_length` while `taom_elk_a` holds the placeholder `body_length` 100; one 60 Blunt blow.
- `ElkAttackServiceTests` (30, one six-row data test among them): the gate and cooldown tests mirror the ram's; the
  charge is 60 whatever the roll and 15 shield-blocked; a rider bonus scales it (75 at +25%, 19 on a block, 30 at a
  -50% malus, 600 at the 10x cap); a NaN, infinite, zero, negative or past-the-cap multiplier lands it unscaled.
- `CareerAgentStatServiceTests`: eight `MountChargeMultiplier` tests, one pinning that it equals what
  `ApplyMountStatModifiers` applies to the mount and one that a NaN or infinite passive yields 1, plus
  `ApplyMountStatModifiers_NaNPassive_LeavesTheMountsChargeUnscaled`. `CustomAttacksUtilsBlowFlagsTests`: a Blunt blow carries
  `CanKillEvenIfBlunt`, Pierce and Cut carry nothing, and `TakeDamage`'s type defaults to Pierce.
- `ElkMountWiringTests` (2): `mirkwood_beleglas` rides the great elk with the saddle and upgrades no further (so it
  stays the top cavalry); every great elk, Animalia elk or moose anywhere in `troops/` and `equipmentsets/`
  carries the elk saddle, and the check fails if it finds none at all. A troop's `<Equipments>`-level slot is read
  before a roster's own, the engine's precedence. The Thranduil, lord-template, lower-cavalry and career pins
  moved to `AnimaliaMountWiringTests` with the moose and the Animalia elk (#646).

Per ADR-008 the BT and mission behavior are tested in game.

## How to verify in game

**A full restart is mandatory**: new item XML registers only at process launch.

1. Inventory tableau: the elk and saddle render, and the saddle sits on the back
2. Custom Battle with Mirkwood: beleglas spawns **mounted** on the great elk (a riderless elk or an elf on foot is
   the silent failure); rochenlas rides the Animalia elk since #646
3. The elf sits on the saddle, not inside the elk
4. **The front legs during a gallop.** The elk's FBX binds its front legs lower than the ram's goat (see Known
   gaps); look at the front hooves in motion
5. The antler charge, ridden by you AND by the AI: the head drops and ONE enemy takes one blow. First turn on
   `Enable Blow Diagnostics` on the MCM page `TAOM — Blow Diagnostics` (off by default). Log fingerprint: one
   `[BlowDiag] blow victim='...' ... dmgType=Blunt dmg=60 mag=35 ... attackerIdx=N` per charge, where N is the
   RIDER's agent index, not the elk's (the ram's head-butt logs `dmgType=Pierce`); `dmg` reads above 60 while the
   player's Antler Crash is active. Plus `[Elk] Attached behavior trees to N elk(s)` and no `[Elk] ... act_none`
   line. The combat log names the rider as the attacker and says Blunt, and no "+N from ability" line follows your
   own charges while an ability is active, and no "Crushed through!" line on an undefended victim (it followed every
   creature knockdown the player saw until 2026-09-23). The impact sounds like a horse's charge hitting, never a punch
   (a rider-owned weaponless blow would pick the punch; TAOM replays the charge sound)
6. **In a campaign battle**, the charge can kill. The flag only lifts the rule that a Blunt blow always wounds; a
   troop it finishes is then killed or wounded by the normal survival roll, as after a Pierce blow (the party's
   surgeon, the enemy's Doctor's Oath). So a single wound proves nothing: finish several troops with the charge
   alone (no body-charge hit on the same troop) and look for at least one kill, which a Blunt blow without the flag
   never produces. That is the proof native passes the scripted blow's `CanKillEvenIfBlunt` to the kill
   decision. Custom Battle proves nothing here: it registers `DefaultAgentDecideKilledOrUnconsciousModel`, which
   kills every downed agent (v1.5.3 `CustomGame.cs:102`)
7. Charge, including jumps
8. Rider dies while mounted, and the elk dies while ridden
9. The player dismounts and remounts
10. The map: no lord rides the great elk in a new campaign any more, so ride one yourself (bought in a Mirkwood
    town): your party icon shows the elk at horse size (see Size), with no "Invalid action set code"

Any CTD goes to `/native-crash-triage`, never a blind retry.

## Known gaps

- **Rest pose not verified against the engine skeleton.** Measured from the FBX, the elk's front legs bind
  lower than the ram's (front hooves at z 0.655 m against 0.740 m, `horselleg1` offset 0.142 m against 0) and
  its bones have different axes (a 3ds Max export; the ram's is Blender). Neither file shows which matches
  `horse_skeleton`'s own rest pose. Check it in the Kit (put `elk_001` on `horse_skeleton` and play a vanilla
  gallop) or in game.
- **`take 001`**, a one-frame, motionless animation from the FBX, sits in `elk_001_geo.tpac` with no skeleton
  assigned. Nothing references it and `check_rdc_entries.py` passes, but the Kit's import log shows
  `Overriding item take 001`, and 3ds Max names every export's take that, so the next Max import collides with
  it. Delete it in the Kit and save the Armory.
- **The package's `.rdc` may predate its last save.** The Kit wrote
  `RuntimeDataCache/91749DAC-63D4-4329-B052-4A141D472021.rdc` at 20:12:59, before `elk_saddle_001_mtl.tpac` existed
  and before the package's last save at 20:13:27 (the first pass logged `Unable to find material elk_saddle_001`,
  the reprocess was clean). Whether the client minds is unverified; the same Kit save refreshes it, and a textured
  saddle in game settles it.
- **No reins.** The saddle declares no `reins_mesh`, like the ram's bardings.
- **The antler charge is the ram's head-butt clip.** It reads as lowering the head; an elk-specific clip would
  need its own `as_elk` set (see Solution Approach).
- **Only the antler attacks scale with the rider's charge bonus** (the elk's, and since #646 the Animalia elk's and
  moose's). The ram's head-butt and the elephant's and mumakil's tramples do not (Mike asked about the elk); each is
  one `riderMultiplier:` argument in its `*Combat.cs`.
- **Previews show the elk at horse size** (see Size): battle only, by Mike's choice.
- **Item names are not translated.** The English rows are registered in the Armory's
  `Languages/loc_LOTRAOM_horses.xml` beside the ram's; the twelve languages need
  `python tools/translate_with_claude.py --lang <L> --module Armory --sync-ids --apply` per language, which calls a
  paid API.
- **The elk art's source and licence are not recorded.** It needs a row in
  [provenance-register.md](../reference/provenance-register.md) if it came from outside TAOM.
- **An existing campaign keeps Thranduil's and the lords' saved mount** (the `charger` in any campaign from a
  released build): the moose reaches them only in a new campaign (see Who rides it).

## GitHub Issue

[#636](https://github.com/haterade22/TAOM/issues/636) (the elk); [#643](https://github.com/haterade22/TAOM/issues/643)
(the attack under a player rider, and the rider owning the blow, for all four elephant-like creatures)
