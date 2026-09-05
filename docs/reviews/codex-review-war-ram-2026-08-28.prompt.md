# Adversarial review: Dwarven War Ram (TAOM issue #515)

You are an adversarial reviewer on TAOM, a Mount & Blade II: Bannerlord **v1.4.8** total-conversion
mod. Assume this changeset contains bugs. Your job is to find them, not to praise it.

Repo root: `E:\repos\TAOM`. Installed game:
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\`.

**Verify against the INSTALLED DLLs, not the decompiled dump.**
`E:\Decompiled_Bannerlord\` may lag. Authoritative:
`ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/<Assembly>.dll" -t "Full.Type.Name"`
or `pwsh tools/taom-src.ps1 path <Type>`.

---

## What was built

A rideable war ram for the Dwarves. Unlike TAOM's other creature mounts (spider, war elephant,
mumakil, chariot), which each ship their own skeleton, clips, action sets and monster-usage tables,
this one is a **reskin of the vanilla horse skeleton**. Both ram body meshes and all eight bardings
are skinned to the stock vanilla horse rig, bone for bone (`horsepelvis`, `horsespine1-3`,
`horseneck1-2`, `horse_head`, `horsel/rfemur`, `horsetail1-3`, including the `_nub_notused` bones).

So the Monster is the vanilla `horse_2` shape and nothing animation-related was authored.

### Changed files, repo side

```
Main/Features/WarRam/WarRamConfig.cs                     (new)
Main/Features/WarRam/IWarRamAttackService.cs             (new)
Main/Features/WarRam/WarRamAttackService.cs              (new)
Main/Features/WarRam/WarRamCombat.cs                     (new)
Main/Features/WarRam/WarRamBehaviorTree.cs               (new)
Main/Features/WarRam/WarRamMissionBehavior.cs            (new)
Main/Features/WarRam/WarRamIoC.cs                        (new)
TAOM.Tests/Features/WarRam/WarRamAttackServiceTests.cs   (new, 24 tests)
Main/IoC.cs                                              (1 line)
Main/SubModule.cs                                        (1 line)
tools/taom_schema.py                                     (MOUNTED_DWARF allowlist)
tools/tests/test_validate_moduledata.py                  (9 new tests)
Main/_Module/ModuleData/troops/troops_erebor.xml         (4 new cavalry troops)
Main/_Module/ModuleData/taom_partyTemplates.xml
Main/_Module/ModuleData/TroopWeights/troop_weights.xml
Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml
Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Erebor.cs (comment only)
TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs (4 new tests)
docs/features/war-ram.md, docs/reference/lotrlome-war-ram-changes.md, CHANGELOG.md
```

### Changed files, EXTERNAL module (not in this repo, but shipped to players)

`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\`:

```
ModuleData/monsters.xml                          Monster.dwarf: CanRide false -> true
ModuleData/Monsters/LOTR/lotr_monster_war_ram.xml   (new)
SubModule.xml                                    one <XmlNode> registration
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml     10 new items
```

The Monster in full:

```xml
<Monster id="taom_war_ram" base_monster="horse" action_set="as_horse"
         weight="320" hit_points="160"
         jump_acceleration="7.5" relative_speed_limit_for_charge="4.0" />
```

Items: `taom_war_ram_a` / `_b` (`Type="Horse"`, `monster="Monster.taom_war_ram"`, `maneuver="75"
speed="42" charge_damage="18" body_length="75" extra_health="20"`, `difficulty="30"`,
`culture="Culture.erebor"`, `is_merchandise="true"`) and eight `Type="HorseHarness"` bardings
`taom_ram_barding_{light,med,heavy,elite}_{a,b}` with `family_type="1"`, `body_armor` 20 to 54.

Troops: `ironpass_ram_rider` (21) -> `ironpass_goat_charger` (26) -> `ironpass_ram_breaker` (31) ->
`ironpass_ram_vanguard` (36), all `race="dwarf"`, `default_group="Cavalry"`, four equipment rosters
each, branching off the existing `ironpass_warrior` (16) as a third upgrade target.

---

## Claims made during implementation. Attack every one of these.

Each of these was asserted and acted on. Verify or refute against the installed DLLs and the real
XML. **A refuted claim here is a P1.**

1. **`base_monster` inheritance.** Claim: `Monster.Deserialize` copies `Flags`, `ActionSetCode`,
   `FemaleActionSetCode`, `MonsterUsage` and all capsule fields from the base, and attributes NOT
   named on the derived monster keep the inherited value because the deserialiser guards its
   defaults behind `if (!flag)` where `flag` means "has base_monster". Therefore the ram inherits
   `Mountable`, `family_type="1"`, `monster_usage="horse"`, `num_paces="6"`, every bone name, the
   slope block and all twelve rein attributes. **Verify in `TaleWorlds.Core.Monster.Deserialize`.**
   If any attribute the ram needs is in fact reset to a default rather than inherited, that is a P1
   (a `weight`/`hit_points`/`num_paces` of 0 or 1 on a mountable monster).

2. **`CanRide` semantics.** Claim: `AgentFlag.CanRide` gates `Agent.CheckSkillForMounting` (and thus
   interactive mounting, remounting, and the "riding skill not adequate" tooltip) but does NOT gate
   AI spawn, because `MissionAgentSpawn`/`BuildAgent` only checks
   `item.HasHorseComponent && item.HorseComponent.IsRideable`. Verify. Also check whether flipping
   `Monster.dwarf` `CanRide` to `true` has side effects anywhere else in the engine (formation
   assignment, AI behaviour selection, `AgentDrivenProperties`, tournament/arena code).

3. **`body_length` is the scale knob and scales the MOUNT only.** Claim:
   `SetInitialAgentScale(0.01f * HorseComponent.BodyLength)` is called on the mount agent, reading
   the mount agent's own spawn equipment, so the rider is unaffected. Verify. Also check what else
   consumes `BodyLength` (item value formula? tier? mount collision?) and whether 75 has unintended
   consequences there.

4. **Harness fit compares the MONSTER's family type**, not the Horse component's:
   `Item.HorseComponent.Monster.FamilyType != Item.ArmorComponent.FamilyType`. Verify, and check
   whether the ram's inherited `family_type="1"` means vanilla horse caparisons are equippable on
   the ram and ram bardings on horses, and whether anything beyond the inventory UI cares.

5. **`monster_usage="horse"` gives the dwarf rider a free animation overlay.** Claim:
   `as_dwarf_warrior` has NO `base_set` but already contains 203 `act_horse_*` / `act_ride*` rows,
   the same count as vanilla `as_human_warrior`, so a dwarf riding a horse-usage mount has a
   complete rider overlay. Verify by parsing
   `Modules/LOTRLOME_Armory/ModuleData/action_sets.xml` and `Modules/Native/ModuleData/action_sets.xml`.
   **If any action the `horse` monster_usage set references is missing from `as_dwarf_warrior`, say
   which** - a missed lookup key is an AV on 1.4.6+.

6. **`action_set="as_horse"` shared with vanilla horses is safe**, and `as_horse_map` /
   `as_horse_town_and_village` already exist so the elephant "Crash #4" class (missing `_map` /
   `_town_and_village` child) does not apply. Verify both children exist and that sharing one action
   set between two Monsters is actually fine (the elephant and mumakil both use `as_elephant`, which
   is the cited precedent).

7. **`act_horse_rear` is a valid BT attack clip.** Claim: it is bound inside
   `action_set id="as_horse"` to `animation="horse_rear"` and typed `actt_rear` in
   `Native/ModuleData/action_types.xml`. The BT plays it via `ActionIndexCache`. **Attack this
   hard:** `actt_rear` is the type the ENGINE uses for the monster-usage `rear_action` /
   `rear_damaged_action` verbs, which fire natively when a mount takes damage or rears. Does a
   behavior-tree-driven `SetActionChannel` of an `actt_rear`-typed action conflict with the engine's
   own rearing? Could it kill the locomotion channel (the documented "slide" failure class, where an
   attack action resolving on channel 0 stops the mount moving while the engine keeps translating
   it)? Compare against how the warg/elephant/mumakil play THEIR attack clips (all custom,
   untyped or `actt_mount_strike`).

8. **No Patch47 dismount-before-death entry is needed.** Claim: the spider and elephant needed
   `Agent_Die_SpiderDismount_Patch` because their Monsters lacked vanilla's rider-death surface,
   while the ram inherits it whole via `base_monster="horse"`. Verify what Patch47 actually guards
   against and whether inheriting from the horse genuinely avoids it.

9. **No mount-lock is needed.** The elephant, spider and mumakil are gated in
   `TaomAgentStatCalculateModel` with `CanAgentRideMount=false` / `MountDifficulty=999`. The ram
   deliberately is not, because it is a player-rideable culture mount. Check for consequences:
   can an enemy AI now steal a riderless ram? Is that acceptable? Does `difficulty="30"` on the item
   interact correctly with `CheckSkillForMounting` given the dwarf `Riding` values (120/160/200/240)?

10. **The `MOUNTED_DWARF` allowlist is sound.** `tools/taom_schema.py` now allowlists exactly
    `taom_war_ram_a` and `taom_war_ram_b`. The implementation changed `_first_mount` (first-wins)
    into `_mounts_in` (all mounts) so a ram cannot mask a horse. **Try to defeat it:** a dwarf with a
    ram AND a horse; a ram in an inline roster and a horse in a named `<EquipmentRoster>`; a Horse
    slot whose id lacks the `Item.` prefix; `default_group="HorseArcher"`; a non-dwarf race.

---

## Known TAOM traps this changeset sits near. Check each.

- **Shield plus a polearm the AI will not draw.** A crafted weapon's primary usage is the FIRST
  `WeaponDescription` listing every piece it uses. A polearm absent from `OneHandedPolearm` resolves
  `requires_no_shield`, so a shield-carrying troop holds it until combat starts then never draws it.
  Silent, no log. **The ram rosters pair `sm_dwarf_erebor_spear_a`/`_b` (a
  `<CraftedItem crafting_template="TwoHandedPolearm">`) with `sm_dwarf_erebor_shield_metal_*`.**
  `tools/audit_polearm_shield_parity.py` reports PASS. Verify that PASS is real and that the audit
  actually covers the mounted case. Also consider: these are CAVALRY. Is the spear usable from
  horseback (couched/overhead)? Is there a `WeaponDescription` gap specific to mounted use?
- **A new item XML file only loads at process launch**, and the glob is `GetFiles("*.xml")`, so a
  backup ending in `.xml` is parsed as real data and duplicates ids. Backups here were written as
  `.bak-warram-20260828`. Confirm no `.xml`-suffixed backup was left anywhere in the Armory.
- **Culture party templates**: an XSLT `Culture[@id=]` block inherits vanilla for every attribute it
  never names. `erebor` is defined wholly in `taom_spcultures.xml` and has zero hits in
  `spcultures.xslt`, so this should not apply. Verify.
- **Party template sizing**: `max_value` is not party size. The engine draws ONE uniform ratio per
  party and fills every stack to `RoundRandomized(min + (max-min)*r)`, so expected spawn is the
  midpoint of the min-sum and max-sum. The claim is that every erebor clan template still sums to
  exactly 2000 max so expected roster size is unchanged. **Verify the arithmetic in
  `taom_partyTemplates.xml` directly.**
- **`BehaviorTreeMissionLogic` must be `: MissionLogic`, never `: MissionBehavior`.** Check
  `WarRamMissionBehavior`.
- **Do not double-tick.** Vanilla `Agent.Tick` auto-ticks attached agent components since v1.4.5, so
  a manual `comp.OnTick(dt)` in the mission behavior double-ticks the tree. Both the warg and spider
  had this removed. Check `WarRamMissionBehavior`.
- **`ActionIndexCache` drift.** If an attack clip name does not resolve it silently becomes
  `act_none`, which kills the locomotion channel. `WarRamConfig` points two "unused" profile slots at
  `act_horse_strike_front` / `act_horse_strike_back` specifically so `AnyUnresolved()` stays clean.
  Verify that reasoning and that the profile's "already attacking" check (which ORs across all four
  slots) is not broken by the primary and alt slots being the SAME clip (`act_horse_rear` twice).

---

## Also review normally

- ADR-002 thin entry points, ADR-007 adapter pattern, ADR-008 test coverage. No `#region`, no
  `[Obsolete]`, no `#if DEBUG`.
- `IoC.Resolve` outside boundary classes; hot-path allocation in per-tick/per-frame code.
- The Python allowlist change in `tools/taom_schema.py` for correctness and for over-permissiveness.
- Whether the 24 C# tests and 9 Python tests actually pin the behaviour they claim to.
- Anything in the four external Armory files that would break OTHER content (the `CanRide` flip is
  global to the dwarf race).

## Output

Findings ranked P1 (ship-blocking) / P2 (should fix) / P3 (nit). For each: file and line, what is
wrong, why it matters at runtime, and the minimal fix. **If you cannot verify a claim, say
UNVERIFIED rather than guessing.** Explicitly list any claim above that you CONFIRMED, so the
confirmations are usable as evidence. If you believe the changeset is sound, say so plainly and name
what you checked.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
