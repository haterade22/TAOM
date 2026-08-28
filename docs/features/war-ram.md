# Dwarven War Ram

## Overview

A rideable war ram for the Dwarves: the first mount TAOM has ever given a dwarf, and the first
creature mount built as a **reskin of the vanilla horse skeleton** rather than on a rig of its own.
It carries an Ironpass cavalry branch, wears an eight-piece barding ladder, and headbutts on its own
using a vanilla horse animation.

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
stock vanilla horse skeleton bone for bone, so the Monster is seven attributes:

```xml
<Monster id="taom_war_ram" base_monster="horse" action_set="as_horse"
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
  skipped entirely.** No clips, no `quad_movement` tagging, no `action_types`, no `action_sets`, no
  `monster_usage_sets`, no rider partial. No animation data is authored anywhere in this feature.
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
lotr_monster_war_ram.xml  base_monster="horse", action_set="as_horse"
      v
engine: vanilla cavalry spawn. Movement, blows, deaths and rider seating are all
        the horse's. No spawn patch, no detached combatant.
      +
WarRamMissionBehavior : MissionLogic
      -> attaches WarRamBehaviorTree per agent, keyed on Monster.StringId
      -> kick attack plays act_horse_kick (a vanilla clip), damage via the shared
         ElephantLike attack service
```

## Configuration

### Scale

`body_length="100"` on the Horse item, so `SetInitialAgentScale(1.0)`: **the ram ships at its
authored size and is deliberately not shrunk.**

**It does NOT scale the mount only, contrary to what this doc previously said.**
`EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are the same value (10), the scale block
in `BuildAgent` has no `IsMount` guard, and `BuildAgent` runs for the rider as well as the mount with
the Horse item still in the rider's spawn equipment. So any `body_length` other than 100 scales the
**dwarf** too. 100 is identity, which is why it is safe, and why it should not be changed without
accounting for the rider. This also affects the mumakil (300) and the wargs (110/115), which is
pre-existing and not this feature's to fix.

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
| `AttackMinDamage` / `AttackMaxDamage` | 18 / 28 | Below both references: the elephant trample is 50-100 (a multi-ton beast flattening a formation) and the warg bite tops out near 60. The ram lands one headbutt as a bonus on top of its rider's own attacks |
| `AttackCooldownSeconds` | 6.0 | Between the elephant's 10s trample and its 4s tusk fallback |
| `AttackBlowMagnitude` | 35f | Below the elephant's 50f: a horse-scale creature should stagger, not launch |
| `AttackTriggerRange` / `AttackRadius` | 2.5f / 3.5f | Just under the unscaled elephant's 3f / 4f, because the ram is horse-scale |
| `AttackActionName` | `act_horse_kick` | **A vanilla clip**, bound in `as_horse` and typed `actt_kick` (`ActionCodeType.Kick = 28`). All four profile slots hold this one action, so `IsAttack` means exactly "mid-kick" |

**Two clips were rejected before this one, and the reasons are worth keeping.** The horse rig has no
headbutt, because vanilla horses have no attack animation at all: they damage by charge collision, so
`monster_usage_strikes` is the mount's hit-REACTION table rather than an attack table.

- **`act_horse_rear`** is typed `actt_rear` (`ActionCodeType.Rear = 47`). The inherited `horse` usage
  set declares `rear_action="act_horse_rear"`, so the engine fires it on a damaged mount, and
  `Agent.Mount` refuses a mount whose channel-0 action is `Rear`. Forcing it every cooldown would have
  made the ram briefly **unmountable mid-fight**, on the one mount built to be player-rideable.
- **`act_horse_strike_front`/`_back`** are typed `actt_mount_strike` (`ActionCodeType.MountStrike = 52`),
  inside the `StrikeBegin = 48 .. StrikeEnd = 52` band that `Agent.IsInBeingStruckAction` reads as
  **being struck**. The clips are named `horse_hit_from_front`/`_back`. The ram would have flinched as
  though hit while we emitted damage.

`act_horse_kick` (`actt_kick`, `ActionCodeType.Kick = 28`) sits outside both bands and is the rig's
only genuinely offensive action. It reads as a buck rather than a head strike: an accepted compromise,
since a correct headbutt means authoring a clip and reopening the animation pipeline this reskin
exists to avoid. Both rejections were caught in review; RCA in
[rca-war-ram-2026-08-28.md](../reviews/rca-war-ram-2026-08-28.md).

**No mount-lock.** `TaomAgentStatCalculateModel` gates the elephant, spider and mumakil with
`CanAgentRideMount=false` and `MountDifficulty=999` so players cannot steal them. The war ram is a
player-rideable culture mount with a career built around it, so it is deliberately not gated. This
matches the chariot, which is also left remountable on purpose.

**No Patch47 entry.** The spider and elephant needed the dismount-before-death patch because their
Monsters lacked vanilla's rider-death surface. The ram inherits that surface whole through
`base_monster="horse"`. A ridden-death in-game test is what would justify revisiting this.

## Key Files

| Path | Role |
|---|---|
| `Main/Features/WarRam/WarRamConfig.cs` | Monster id and all tuning |
| `Main/Features/WarRam/IWarRamAttackService.cs` | Marker sub-interface of `IElephantLikeAttackService` (the documented per-creature IoC pattern) |
| `Main/Features/WarRam/WarRamAttackService.cs` | Ram-tuned damage bands over the shared service |
| `Main/Features/WarRam/WarRamCombat.cs` | Static `ElephantLikeCombatProfile` wiring the vanilla horse clip names |
| `Main/Features/WarRam/WarRamBehaviorTree.cs` | Mumakil shell with only the butt branch wired, no new node classes |
| `Main/Features/WarRam/WarRamMissionBehavior.cs` | **`: MissionLogic`**, attaches keyed on `Monster.StringId` |
| `Main/Features/WarRam/WarRamIoC.cs` | Registration helper |
| `Main/IoC.cs:114`, `Main/SubModule.cs:1559` | The two single-owner wiring lines |
| `Main/_Module/ModuleData/troops/troops_erebor.xml` | The four Ironpass cavalry troops |
| `tools/taom_schema.py` | `WAR_RAM_MOUNT_IDS` allowlist for `MOUNTED_DWARF` |
| **`docs/reference/lotrlome-war-ram-changes.md`** | **The external-module ledger. `LOTRLOME_Armory` is not tracked by this repo and a reinstall reverts it** |

## Troop tree

A third branch off `ironpass_warrior` (16), at the same depth as the axeman branch:

```
ironpass_warrior (16)
 |- ironpass_infantry (21)  [existing]
 |- ironpass_arbalest (21)  [existing]
 `- ironpass_ram_rider (21) -> ironpass_goat_charger (26)
                            -> ironpass_ram_breaker (31)
                            -> ironpass_ram_vanguard (36)
```

Riding climbs 120 / 160 / 200 / 240 (the mount item's `difficulty="0"` imposes no floor). Barding grade climbs with tier: light, mail, plated, elite. Each troop carries four equipment
rosters alternating both pelts and both barding variants, so all ten items are reachable in play.

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

Lords get **heavy** barding, not elite, leaving the top of the ladder as headroom. **Dáin II Ironfoot
stays on foot** by decision; his bespoke `dain_bat_equipment` is untouched. Civilian sets are
untouched throughout, so no lord rides a war ram into a tavern.

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

## Tests

`TAOM.Tests/Features/WarRam/WarRamAttackServiceTests.cs`, 24 tests mirroring
`MumakilAttackServiceTests` one for one: `IsCreatureMonster` x3, `ShouldEngage` x5, `IsOffCooldown`
x6, `ComputeInflictedDamage` x10 including NaN, clamp and future-timestamp cases.

`tools/tests/test_validate_moduledata.py` gained 9 `MOUNTED_DWARF` cases, including the one that
matters most: **a dwarf carrying both a ram and a horse still errors on the horse**. The allowlist
work also fixed a latent hole, since the old `_first_mount` was first-wins and a ram listed ahead of
a horse would have masked it.

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

- **The eight barding meshes are not in the cooked asset packages**, so every ram currently spawns
  with a bare body and no barding. The mounts themselves are cooked and render fine; the barding
  items are live data pointing at meshes the shipped packs do not contain, so this is a real defect
  rather than merely absent content. `python tools/validate_mesh_refs.py` fails on it (exit 1, 10
  errors). Confirmed three independent ways; see the ledger.
- **The rider seat is untuned**, pending the in-game check.
- **`body_length="100"` was set after an in-game look**, replacing a derived 75 that read too small. The ram now ships at authored size.
- **Item and troop names are not yet translated.** English works through the inline `{=KEY}default`.

## GitHub Issue

[#515](https://github.com/haterade22/TAOM/issues/515)
