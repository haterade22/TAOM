# Scripted melee strikes: vanilla damage, contact, reactions and off-hand items

> **Engine:** Bannerlord v1.5.3. **Source:** yotthani's DualWield mod, read in the private repository
> `yotthani/bannerlord` (`HoN/DualWield`, commit `8e040ab`) on 2026-09-30. Its source notes date from 2026-09-14 to
> 2026-09-30 and target Bannerlord 1.4.8 and 1.5.3. Nothing on this page is DualWield code: each fact is restated in
> TAOM's words and tagged. Review: [adopt-yotthani-bannerlord-2026-09-30.md](../reviews/adopt-yotthani-bannerlord-2026-09-30.md);
> provenance: the "Yotthani bannerlord repository" row in [provenance-register.md](provenance-register.md).
>
> **Tags:** **[Certain]** a name, signature or value read in the v1.5.3 decompile cache (`~/.taom-src/v1.5.3/`), the
> native decompile or Native's ModuleData on 2026-09-30. **[yotthani]** measured by yotthani in game and recorded in
> the DualWield source; TAOM has not reproduced it. **UNVERIFIED** nobody has established it.

**Why TAOM cares.** TAOM's scripted strikes (spider, elephant-likes, elk, the Animalia mounts, creature bandits and
Sauron's signature strikes) all end in `CustomAttacksUtils.TakeDamage`, which builds a `Blow` with a fixed
`InflictedDamage` and `BaseMagnitude` and registers it through `Mission.RegisterBlow`
(`Main/Features/AdvancedCombat/CustomAttacksUtils.cs`). Armour, a raised shield, the attacker's skill and the speed of
the swing never enter. DualWield met the same wall: its left-hand strike is a clip it plays itself, so the engine
never runs a melee collision for it. It hands a collision of its own making to the managed function the engine calls
after its own collisions, and everything after "did it hit, was it blocked" is then vanilla. This page records how,
what that buys and what it costs, so a TAOM decision (Tier 2 in
[adopt-yotthani-animation-handoff-2026-09-18.md](../reviews/adopt-yotthani-animation-handoff-2026-09-18.md)) starts
from facts. Nothing here is adopted; a port is a feature with an issue, `/research` and TDD.

## 1. Handing a collision to vanilla

- **The function** [Certain]: `internal void Mission.MeleeHitCallback(ref AttackCollisionData collisionData, Agent
  attacker, Agent victim, GameEntity realHitEntity, ref float inOutMomentumRemaining, ref MeleeCollisionReaction
  colReaction, CrushThroughState crushThroughState, Vec3 blowDir, Vec3 swingDir, ref HitParticleResultData
  hitParticleResultData, bool crushedThroughWithoutAgentCollision)` (`TaleWorlds.MountAndBlade.Mission.cs:5347`). It
  is internal, so the call is reflection. Its native callback `Mission_MeleeHitCallback` is one of the ten combat
  callbacks the crash reporter attaches to ([crash-report.md](../features/crash-report.md) row 6).
- **The data** [Certain]: the public factory `AttackCollisionData.GetAttackCollisionDataForDebugPurpose(...)` builds
  the struct from its fields (DualWield passes 37 positional arguments: the block and shield flags, the collision
  result, slot, strike type, damage type, victim bone and body part, attack bone, direction, progress, distance on
  weapon, both stun periods, and the positions and directions).
- **What vanilla then does** [yotthani]: damage through `GetAttackCollisionResults` (armour counts), shield damage on
  a shield block, `CreateMeleeBlow`, `RegisterBlow` with shrug-off, knock-back, knock-down and dismount, the combat
  log, perks, the blood decision, the weapon's collision reaction, and `OnMeleeHit` for every mission behaviour. The
  results come back through the `ref` arguments: `InflictedDamage`, `AbsorbedByArmor`, `BaseMagnitude`, the momentum
  left and the reaction.
- **A blow on a friend** [Certain]: `CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase` cancels the damage and the
  callback sets `AttackerStunPeriod` to the managed parameter `StunPeriodAttackerFriendlyFire`
  (`Mission.cs:5382`), 0.4 s in Native's `managed_core_parameters.xml`. So pass friends as targets: vanilla stops a
  blade on a friend. Filtering them out let DualWield's left blade cut through its own line [yotthani].
- **What vanilla does NOT do for a made-up collision** [yotthani]: whatever native plays after its own collision.
  Hit particles, the impact sound and the attacker's bounce or blocked animation are the caller's (section 4).
- **Re-entry.** Every `OnMeleeHit` handler sees the scripted hit, TAOM's included (`SignatureStrikesMissionLogic`,
  [signature-strikes.md](../features/signature-strikes.md)). DualWield sets a flag around the call so its own
  magnitude patch knows which hand struck; TAOM's `CustomAttacksUtils` already keeps such a flag around its
  `RegisterBlow`.

## 2. Inputs that must be right, or the damage collapses

| Input | The right value | What a wrong one did [yotthani] |
|---|---|---|
| `AttackProgress` | The release clip's progress, `Agent.GetCurrentActionProgress(channel)`, 0 to 1 [Certain name] | `Skeleton.GetAnimationParameterAtChannel` is the animation parameter, not the progress: mid-swing hits read 0.02. Tip speed / 9 m/s (1.0 for any real swing) put every hit at the end of `SpeedGraphFunction`, which peaks mid-swing and falls to 0 at 1: magnitude 0.0 to 1.3, 1 damage |
| Attack bone | `Monster.GetBoneToAttachForItemFlags(item.ItemFlags)` [Certain: no attachment flag gives `MainHandItemBoneIndex`, `ForceAttachOffHandPrimaryItemBone` gives `OffHandItemBoneIndex`, `...SecondaryItemBone` gives `OffHandItemSecondaryBoneIndex`] | Any other bone makes `MissionCombatMechanicsHelper.HitWithAnotherBone` true, and the blow counts as a hit with the arm (`SwingHitWithArmDamageMultiplier`) |
| Stun periods | `internal static MissionCombatMechanicsHelper.GetDefendCollisionResults(attacker, defender, collisionResult, slot, isAlternativeAttack, strikeType, attackDirection, collisionDistanceOnWeapon, attackProgress, attackIsParried, isPassiveUsageHit, isHeavyAttack, ref defenderStun, ref attackerStun, ref crushedThrough, ref chamber)` [Certain], the method native reaches through the `Mission.GetDefendCollisionResults` callback | Both periods 0: a victim that is not held does not flinch. It throws a NullReferenceException for a non-human defender (a warg, 2026-09-27); catch it per blow, never switch stuns off for the session |
| Distance on the weapon | How far along the weapon the contact sits | The engine divides it by `WeaponComponentData.GetRealWeaponLength()` [Certain, `MissionCombatMechanicsHelper.cs:254`]; see flails below |
| Momentum | Pass 1, read `inOutMomentumRemaining` back, and cut on only while some is left | Ignoring it let one left swing kill three or four men |

## 3. Finding the contact

No managed API runs a melee collision for a clip you play yourself, and none says where a wielded weapon's head is
[yotthani, read in the 1.4 decompile].

- **Limb capsules.** `Mission.RayCastForClosestAgentsLimbs` [Certain signature, see
  [bannerlord-skeleton-authoring.md](bannerlord-skeleton-authoring.md) "Testing them in a mission"] casts against the
  shapes a vanilla swing collides with. DualWield casts several blade poses per tick plus the tip's arc. Its earlier
  test, bone origins within 13 cm, landed 2 hits in a whole round, because a blade through a chest passes between
  joints [yotthani].
- **The per-agent ray is a trap** [yotthani]: hits from `RayCastForGivenAgentsLimbs` broke the damage call with an
  access violation (2026-09-29).
- **Cost** [yotthani]: 65 to 70 µs per limb ray with 1,600 agents on the field. Four rays a tick per strike cost 31 to
  36 ms per second; two cost 14 ms but lost 16 % of the hits. DualWield settled on three rays for an NPC strike and
  nine plus a one-tick look-ahead for the player's.
- **No ray hits a shield or a weapon** [yotthani]: the engine's agent rays are `RayCastForClosestAgent`,
  `RayCastForClosestAgentsLimbs` and `RayCastForGivenAgentsLimbs`, and a shield is an attachment, not a limb. DualWield
  models a guarding weapon as a line from its weapon bone along the bone's local +z and calls a parry when the swept
  blade passes close to it; a shield that is not actively raised still reads as a body hit.
- **The weapon as a line.** The hand bone's local +z for the weapon's length. Measured against the engine's own hits
  (a postfix on `MeleeHitCallback`, 2026-09-22), the hit point lies on that axis within 1 to 12 degrees, even for a
  flail [yotthani]. **The length is wrong for flails:** `GetRealWeaponLength()` says 0.65 m for vanilla's flail while
  the engine reported its hits at 1.03 to 1.55 m. A flail item carries its own skeleton (`flail_skeleton`, the only
  one in vanilla), so DualWield takes a skeleton-bearing item's reach from the longest side of its mesh's bounding
  box [yotthani].
- **The scene.** A scene ray with `BodyFlags.CommonCollisionExcludeFlagsForCombat` [Certain name], the engine's own
  mask for weapon collisions (grass, non-colliding props and ragdolls are already out). To stop a blade in front of a
  tree rather than a tick inside it, extrapolate one tick ahead, keep the weapon's length on the extrapolated pose
  (interpolating the two ends separately shortens a turning weapon to its chord), and apply damage only on contact
  actually reached [yotthani].
- **The collision window** [Certain, data]: a swing may collide only between `collision_check_starting_percent` and
  `collision_check_ending_percent` of the combat parameter the clip names (`CombatParameterId`,
  `Native/ModuleData/combat_parameters.xml`); only native reads that file. `1h_up` is 0.38 to 0.50, `1h_up_flail` 0.60
  to 0.88, the flail's side swings 0.47 to 0.93 and 0.55 to 0.94: the head trails the hand. Without the window,
  DualWield's wind-up hit a tree at 7 to 13 % and the strike stopped before it began [yotthani]. The file's census is
  in [bannerlord-animation-system-map.md](bannerlord-animation-system-map.md) "Combat parameter".

## 4. After the contact

- **Impact sound** [yotthani]: vanilla's family is `event:/mission/combat/impact/{weapon physics material}/{surface
  physics material}`, the weapon's from its item XML (`physics_material="metal_weapon"`, `wood_weapon`,
  `punch_weapon`, `sling_weapon`), the surface's from what was hit (wood, stone, flesh, rustle, straw, soil, grass,
  metal, fabric, mud, snow, sand). `event:/physics/swordlike/*` and `event:/physics/shieldlike/*` are the sounds of an
  item DROPPED on a surface and need a `Force` parameter; without it they are silent.
- **The surface's material** [yotthani]: a ray returns none. Take the hit entity's; flora has no entity, so a hit
  clearly above the terrain is a plant (wood) and one on it is the ground. `physics_materials.xml`
  `AttacksCanPassThrough` marks materials a blade passes.
- **A block or a bounce** [yotthani]: vanilla plays the usage's `quick_blocked_action`, the release window played
  backwards (DualWield's example: frames 135 back to 35). Start it at 1 minus the release's progress; started at 0 it
  jumps to the end of the swing first and the blade seems to pass through the target.
- **Silent non-damaging blows** [yotthani]: a blow on armour, a lowered shield or a friend still happened; play the
  impact, keep the blood and the damage-graded sound for real damage.
- **Friendly-fire stun**: hold the attacker for the `AttackerStunPeriod` the callback returned (section 1).

## 5. Playing your own strike clip

- **A typeless clip is invisible to the AI** [yotthani]. Played through `SetActionChannel`, it never counts as an
  attack (typed as one, the engine would hit with the right-hand weapon), so the native AI never blocks it. DualWield
  rolls the block itself from the defender's skill and plays vanilla's parry for the blow's side.
- **Vanilla's attack covers a channel-0 clip** [yotthani]. The real attack runs as an upper-body overlay on channels
  1 to 3; overriding those channels crashed. Prevent the attack instead: `MissionMainAgentController` raises one only
  while the agent has `AgentFlag.CanAttack`, so clear it for the clip and hand it back on every way out (a flag left
  cleared left the player unable to shoot a bow). For an NPC, the AI puts its guard or next attack on channel 1 at
  once: 686 left strikes and 108 hits in one battle, none of them visible. Clear `CanAttack` and `CanDefend` and
  channel 1 while the strike runs.
- **Priority** [yotthani]. Without a priority flag (DualWield uses `amf_priority_kick`) the action system reasserts
  the stance on channel 0 a few frames in. Vanilla's struck actions outrank that priority, so a strike must notice
  when a hit reaction has taken the body and stop sweeping.
- **`SetAnimationAtChannel` holds its last frame** [yotthani]: it is a raw one-shot with no blend-out. Hand the
  channel back by starting a real idle action.
- **The spine bends by pitch only for the engine's own attacks** [yotthani; the attributes are Certain]: combat
  parameters carry `vertical_rot_limit_multiplier_up`/`_down` (`1h_up` 0.3). A typeless clip gets no bend, so
  DualWield bakes bent variants (`_lo`, `_hi`) and picks one by the look pitch.
- **Speed** [yotthani]: `Agent.SetMaximumSpeedLimit` limits the AI's movement orders and does nothing for the player
  (left strikes ran at 3.6 to 4.7 m/s under a 2.8 m/s cap); slow the player through driven properties.
- **Every stat model** [yotthani]: a patch on `SandboxAgentStatCalculateModel` alone never runs in a custom battle
  (`CustomBattleAgentStatCalculateModel`) or under a mod's own model; TAOM registers `TaomAgentStatCalculateModel`.
- **The AI's skill level** [Certain]: `AgentStatCalculateModel.SetAiRelatedProperties` stores the melee AI level
  (`CalculateAILevel` times the model's multiplier, clamped 0 to 1) as `AIParryOnAttackAbility`, so any model's level
  can be read back from it. DualWield reads it as skill / 300 times 0.96 on realistic, 0.32 on normal and 0.1 on easy
  [yotthani].

## 6. Off-hand items

- **The equip-time copy decides the hand** [yotthani]. The engine copies `ItemFlags` and the usage into
  `WeaponStatsData` when a weapon is equipped (`MissionWeapon.GetWeaponStatsData`, then `Agent.WeaponEquipped` into
  native). That copy decides which bone the weapon hangs on and whether it is held in the off hand, so a per-agent
  change there leaves the shared `ItemObject` alone. Setting flags on the item at attach time is too late.
- **Bone and turn** [Certain for the bone]: the bone is `GetBoneToAttachForItemFlags` (section 2) and the turn is the
  item XML's `rotation` (`WeaponComponentData.Frame`). `ItemUsageSetFlags` holds only `RequiresMount`,
  `RequiresNoMount`, `RequiresShield`, `RequiresNoShield` and `PassiveUsage`: nothing about orientation.
- **The engine asks for the left hand's ROOT usage set** [yotthani]. `require_left_hand_usage_root_set` compares the
  root of the left item's `base_set` chain; vanilla asks for `hand_shield`, `shield` and `banner`, three sets with no
  base. An off-hand usage deriving from `hand_shield` therefore makes the left hand a hand shield everywhere: the
  movement set (`1h_with_hand_shield`), the formation's passive shield cover (`EnforceShieldUsage`) and the buckler
  guard. Within a usage set the first entry whose conditions hold wins, so an override is prepended.
  `ModuleData/item_usage_sets.xslt` is applied by file name through `MBObjectManager.HandleXsltList`.
- **The native AI keeps a left-hand item only if it is a shield** [yotthani, a Ghidra read of the native weapon
  choice, `FUN_1806afec0` on their build, 2026-09-25]: its copy must carry `HeldInOffHand` and `CanBlockRanged`; otherwise the AI wants an empty left hand and
  the agent tick sheathes it every 4 to 5 s. Faking `CanBlockRanged` crashed at the first arrow (the missile defence
  reads the item's `shield_body_name` collision body, null for a weapon), and handing it a blade body crashed
  `WeaponEquipped` at spawn. The one exception: while the extra slot 4 (`ExtraWeaponSlot`) holds an item with
  `HeldInOffHand`, the AI leaves the left hand alone. That is how banner bearers keep their banner.
- **Banners** [yotthani]: `BannerBearerLogic` puts the banner in `ExtraWeaponSlot` and the off hand; unwielding it
  crashed one call later in `BannerBearerLogic.UpdateAgent` (`GetWeaponEntityFromEquipmentSlot`). A banner or shield
  in the off hand is not yours to take. TAOM's [banner-bearers.md](../features/banner-bearers.md) owns that feature.
- **Spawn equipment** [yotthani; the name is Certain]: `Mission.BuildAgent` draws the equipment inside `SpawnAgent`,
  per slot, from all of the troop's equipment sets (`Equipment.GetRandomEquipmentElements`), and puts a banner into
  slot 4 before it goes on, so a patch on the finished spawn equipment sees exactly what the agent will carry.
- **Wielding** [yotthani]: `SetWieldedItemIndexAsClient(HandIndex.OffHand, ...)` renders one weapon and the engine
  guards with it (it has its own driven property, `OffhandWeaponDefendSpeedMultiplier` [Certain]);
  `AttachWeaponToBone` creates a second entity, a duplicate weapon.
- **Never enable `Skeleton.EnableScriptDrivenPostIntegrateCallback`** to pose bones at runtime: it is one-way
  (managed `Skeleton` has no disable [Certain]) and breaks the engine's bone-to-entity attachment for the whole
  skeleton, freezing every weapon, wielded or sheathed [yotthani, Bannerlord 1.4.5].

## 7. Cost at scale

- **Engine calls add up** [yotthani]: `GetCurrentActionType` and `FindAgentWithIndex` go into the engine. With 936
  dual wielders, calling them every tick meant about 45,000 and 56,000 calls a second respectively; DualWield looks at
  each agent every third tick and sweeps for the dead once a second.
- **`ActionIndexCache.Create` caches nothing** [Certain]: each call constructs a new cache whose constructor calls
  `MBAnimation.GetActionCodeWithName` with the string. Resolve action codes once and keep them; re-ask a -1, which can
  come from a lookup made before the action sets loaded.
- **Snapshot before iterating** [yotthani]: a kill inside the loop removes entries through `OnAgentRemoved`
  (collection modified, a crash).
- **Budget by frame share, not a fixed cap** [yotthani]: a fixed 32 concurrent NPC strikes let 40 to 60 % of the
  chances lapse in an 800 against 800 test; DualWield adjusts its cap every half second from the frame rate and its
  own share of the frame.

## 8. The measuring kit

DualWield's method, worth copying for any creature strike (the pose-probe proposal in
[adopt-mithrilforge-2026-09-29.md](../reviews/adopt-mithrilforge-2026-09-29.md) Phase 3):

- **Measure your model against the engine's own hits.** A postfix on `MeleeHitCallback` sees every vanilla hit;
  compare the hit point with each candidate direction from the hand bone and let the log answer with angles. Skip the
  hits you fabricated yourself: a model cannot be its own reference.
- **Capture per channel.** `Skeleton.GetBoneEntitialFrameAtChannel` [Certain name] gives each channel's pose and the
  blended result, so one session shows whether the engine plays your clip (channel 0 against the decoded clip),
  whether a movement animation fights the legs (channel 1) and how much anything else moves the figure. Record weapon
  frames with their rotation, in their own file.
- **Never overwrite a capture** (a crashed round once truncated the only dense recording to 0 bytes); sample at the
  full frame rate (30 Hz gives about 36 samples on a 1.2 s clip) and record until the next clip starts.
- **Probe registration without playing.** `MBAnimation.GetAnimationIndexWithName` [Certain name] is the lookup
  `SetAnimationAtChannel` uses, so an index of 0 or more means the name will resolve. A TpacTool-written animation
  registered with -1 and faulted only when played; DualWield's self-written animation died with an access violation
  in `MissionScreen.UpdateCamera` on both trigger paths [yotthani].
- **A clean test harness.** Start every test clip from the same settled idle, and keep an unmirrored control replay:
  root motion moves the agent in the world, where a pose reset cannot undo it.

## 9. What it would mean for TAOM

The decision is Mike's; these are the trade-offs as the facts stand.

- **Vanilla damage for creature strikes** (routing `CustomAttacksUtils` through `MeleeHitCallback`). Win: armour,
  shields, skill and swing speed would count, with vanilla's reactions and perks. Cost: reflection into an internal
  method that an engine bump can move (it would join `/verify-bindings`), a 37-argument collision to build, the
  particles and sounds to play ourselves, TAOM's own `OnMeleeHit` handlers seeing every creature hit, and
  `GetDefendCollisionResults` throwing for a creature defender. By [simplicity-criterion.md](../../.claude/rules/simplicity-criterion.md)
  it is a large win that needs its own issue and a stated trade-off.
- **Limb rays for creature contact** instead of distance to bone origins: the cost numbers in section 3 bound a
  crowd.
- **Banner bearers**: the `ExtraWeaponSlot` and off-hand facts in section 6 explain why the AI never sheathes a
  banner.
