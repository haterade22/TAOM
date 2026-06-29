# War Elephant (Harad rideable mount)

> **Status: C# BUILT + WIRED (2026-06-05); ACTION-SETS SELF-CONTAINED IN LOTRLOME (2026-06-08); IN-GAME BATTLE CONFIRMED (2026-06-08).**
> The mount-lock + the structural trample/tusk mechanic are a behavioral port of the donor mod's elephant, adapted to
> **v1.4.5**, fully wired (IoC + SubModule) and green. The attack **cadence** (deterministic cooldowns, 2026-06-10) and
> the per-kind randomized **damage** (trample 50-100, tusk 50-75, 2026-06-15) are TAOM's own rebalance — NOT 1-for-1 with
> the donor. Supersedes the paused [Giant Spider](spider.md).
>
> **Upstream-pack dependency ELIMINATED (2026-06-08).** After resolving two data-pipeline crashes during the LOTRLOME-
> standalone deployment (see "Action-sets deployment crash history" below), the donor `as_elephant` action-set block
> was ported into `LOTRLOME_Armory/ModuleData/action_sets.xml` (merged into the module's single `soln_action_sets`
> entry in `project.mbproj` — the same pattern as `as_spider`). The game now loads and elephants spawn in battle
> without the upstream beasts pack in the load order.
>
> **IN-GAME BATTLE CONFIRMED (2026-06-08).** Multiple war elephants with Harad riders spawned and fought correctly
> in battle (screenshot: 5 elephants, riders visible, correct mesh, formation movement). Animations run from the
> donor `as_elephant` clips already in `LOTRLOME_Armory/Assets/creature/elephant/animations/` (the 35 tpacs copied
> during self-contained clip consolidation). Remaining steps: author TAOM-owned `as_war_elephant` action-set with
> the TAOM-authored clip names, revert the TEMP `troops_harad.xml` test entry, author the actual Harad rider troop.
>
> **First-test deployment (2026-06-06) — CONFIRMED.** The Monster + Item were deployed to live `LOTRLOME_Armory`
> with the Monster's `action_set="as_elephant"` / `monster_usage="elephant"` (both from the donor pack).  Deployed/changed:
> - `LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_elephant.xml` (NEW, de-risked refs) + registered in `LOTRLOME_Armory/SubModule.xml`.
> - `<Item id="taom_war_elephant">` added to live `LOTRAOM_horses.xml`.
> - **TEMP** `Horse`-slot mount on `harad_militia` (`Main/.../troops_harad.xml`, marked `TEMP-ELEPHANT-TEST`, REVERT before commit) so a Harad party fields an AI-ridden elephant.
> - Backups: `*.bak-elephant` beside the two edited LOTRLOME files.
>
> **Trample tick VERIFIED 1.4.5-safe (2026-06-06).** The howdah workflow proved `AgentComponent.OnTickAsAI` (the
> virtual override the donor mod uses for trample/crew AI) **does not exist in v1.4.5** — so the donor's *own* trample is dead on our
> engine. TAOM's port pre-fixed this: `ElephantMissionBehavior : MissionLogic`, trample runs in `OnMissionTick`
> iterating `Mission.Current.AllAgents` ([ElephantMissionBehavior.cs:60](../../Main/Features/Elephant/ElephantMissionBehavior.cs#L60)), not on an AgentComponent. The trample will fire.
>
> **Self-contained clip consolidation — DONE (2026-06-06/08).** LOTRLOME's `elephant_harad_armor_01_geo.tpac`
> carries `elephant_skeleton`. The clip set (**31 `*_anm.tpac`** + **4 `elephant_anims_all_*_geo.tpac`**, 35 total)
> was copied to `LOTRLOME_Armory/Assets/creature/elephant/animations/`. The donor `as_elephant` action-set block
> was ported to `action_sets.xml` (2026-06-08, same pattern as `as_spider`). Monster still points at `as_elephant`
> (donor id) + `monster_usage="elephant"` — next step: rename to `as_war_elephant` + `war_elephant` usage when
> authoring the full TAOM-owned action-set with TAOM clip names.
>
> **CREW / HOWDAH — slide root cause DIAGNOSED (2026-06-10); fix DEFERRED by project-owner decision.**
> `TaomHowdahMachine` + `TaomHowdahStandingPoint` ship 4 seats in a 2×2 grid. Crew is force-spawned at battle
> start (sealed-package model — `harad_archer` ×4, not drawn from party roster).
>
> **CURRENT SHIPPED STATE (2026-06-10): the elephant rides, tramples, and walks/charges WITHOUT sliding** — but with
> **no howdah crew** and **no spine bone-tracking**. The isolation ladder confirmed TWO independent slide sources —
> (1) the force-spawned crew and (2) bone-tracking — both of which physically shove the elephant. Both are **disabled
> for now** (project-owner decision: defer the fix). Cavalry-reassignment + trample are confirmed innocent and stay
> enabled. The empty howdah still instantiates and follows the elephant via the fixed-offset path. To resume the
> crew+howdah feature, implement the physics-contact fix and re-enable the two disabled paths — see
> "### Slide root-cause isolation" below for the confirmed diagnosis + the planned fix.
>
> **2026-06-10 — what this session changed (some confirmed, some under test):**
> - **One slide source fixed — action codes:** `act_war_elephant_attack_*` resolved to `act_none` (channel 0 =
>   locomotion channel → kills walk/run cycle while engine keeps translating = slide). Fixed: renamed to
>   `act_elephant_attack_1/2/3` (verified live in LOTRLOME action_types.xml + action_sets_elephant.xml). This
>   removed a *constant* slide but a residual slide remained — see the isolation section for the two further sources.
> - **Trample-into-empty-air gate restored:** the donor's `target != null && distance < 3m` gate was missing from the
>   1-for-1 port. Trample fired ~1–2×/sec into empty air overriding channel 0. Fixed: `TrampleTriggerRange = 3f`
>   proximity + facing scan before firing.
> - **Bone tracking (Spine1_05) implemented — but is a SUSPECTED slide source (under test):** `TaomHowdahMachine`
>   resolves `Spine1_05` by case-insensitive name enumeration on first live tick, then `GetBoneEntitialFrameWithIndex`
>   + `TransformToParent` each tick. Load-time AV (bone APIs during `OnAgentBuild`) fixed by the `_liveTicking` flag.
>   BUT the isolation ladder shows bone-tracking is likely the second slide source — it SetFrames the howdah's `bo_`
>   physics floor *at the spine bone* (inside the elephant's collision capsule), and the solver shoves the elephant.
> - **SmartCavalryAI default changed to `false`** — user disabled in MCM during debugging; default updated to match.
> - **Archer "constant draw" fixed:** the per-tick `SetActionChannel(0, act_howdah_stand_bow)` pose-lock (added
>   earlier this session as the jitter fix) pinned the archer's upper body in a draw stance so the combat AI could
>   never fire. **Removed** (2026-06-10) — combat AI drives the bow animation; the archer is held by `TeleportToPosition`.
>   (`SetOnLandState`/`AgentOnLandFlags`, the donor's anti-fall API, do NOT exist in v1.4.5 — confirmed zero matches.)
>
> 1. **`GetTickRequirement()` missing** — `TaomHowdahStandingPoint` didn't override `GetTickRequirement()` so the
>    engine never called `OnTick`. Fixed by adding `TickRequirement.Tick | base` override (2026-06-08).
>
> 2. **Howdah at terrain level (archers invisible + elephants blocked)** — `RepositionToElephant()` used
>    `GetBoneEntitialFrame` on the `Spine1_05` bone, which returns Z ≈ 0 in entity-local space in v1.4.5
>    (bone origin is near the entity pivot, not the physical bone location). Fixed (2026-06-09): use simple
>    `elephantAgent.Position + Vec3(0,0,HowdahHeightAboveGround=3.2f)`.
>
> 3. **End-of-battle freeze (1st root cause)** — `LockUserPositions=true` was set on seated archers. Bannerlord's
>    end-battle sequencer immediately tries to move all agents to victory positions; locked agents busy-loop forever.
>    Fixed (2026-06-09): removed `LockUserPositions`/`LockUserFrames` entirely.
>
> 4. **Archers not shooting — `act_none` animation freeze** — `HowdahBowAnim = ActionIndexCache.Create("act_howdah_stand_bow")`
>    resolves to `act_none` when the tpac doesn't exist. The `OnTick` guard `!action.GetName().Contains("howdah")` is
>    always true for `act_none` → `SetActionChannel(0, act_none)` called every tick → action channel permanently
>    frozen → archers cannot perform ANY combat animations and cannot shoot. Fixed (2026-06-09): removed the
>    `SetActionChannel` block entirely. The engine's combat AI handles shooting animations without interference.
>
> 5. **Archers not responding to formation commands** — `OnUse` set `userAgent.Formation = null`, removing archers
>    from their formation entirely. Formation-level attack/fire commands never reached them. Fixed (2026-06-09):
>    removed the `Formation = null` call. Archers stay in their formation; `SetScriptedPosition` each tick overrides
>    movement targets (keeping them on the howdah) while attack orders still reach them through the formation.
>
> 6. **End-of-battle freeze (2nd root cause)** — `SetScriptedPosition` remained active on agents after the battle
>    ended. The end-battle sequencer issues movement orders that collide with the scripted position state → agents
>    cannot walk to exit positions → sequencer stalls. Additionally `TaomHowdahMachine.OnEndMission` nulled
>    `elephantAgent` and called `base.OnEndMission()` WITHOUT calling `ReleaseAllSeats()` first — so `ReleaseAgent()`
>    ran with a null elephant ref, couldn't call `TeleportToPosition`, and left scripted state active.
>    Fixed (2026-06-09): (a) `ReleaseAllSeats()` added to `OnEndMission()` before nulling refs; (b) `ReleaseAgent()`
>    now calls `agent.TeleportToPosition(elephantAgent?.Position ?? agent.Position)` before `ClearTargetZ()` —
>    snapping the agent to navmesh and clearing all elevated scripted position state.
>
> **Donor-pack XML analysis** — studied all 3 donor howdah variants (`adod_howdah_1_agent.xml`, `howdah_object.xml`,
> `adod_howdah_4_agents.xml`) 2026-06-09. Key findings vs our `taom_howdah_agent.xml`:
> - Every donor variant uses `TranslateUser="true"` on seats (base class positions agents via physics-level frame
>   translation each tick). Our prefab uses `TranslateUser="false"` with our custom `SetScriptedPosition`/`SetTargetZ`.
> - Every donor variant includes 4 `_barrier_04x04m` child entities with `missile_only` body flags — physical walls
>   keeping archers inside the howdah basket. Our prefab has NO barrier entities.
> - The donor's 4-seat variant: `AutoWieldWeapons="true"` (we have `false`), all seats tagged `<tag name="pilot"/>`.
>
> See how the donor mod implements this: **[howdah-crew-mechanism.md](elephant/howdah-crew-mechanism.md).**
>
> **Scope — this is a *standard* war elephant, NOT the giant mumakil / Oliphaunt.** A normal-scale ridden mount
> (one Harad crewman rides it). TAOM already represents the mumakil separately (the existing `mumak_rider` troop
> archetype + "Mumakil War Tower" framing in the Harad culture); this feature does not touch those.
>
> **⚠️ The upstream beasts pack is built for Bannerlord ~1.2.12, NOT 1.4.5.** So the donor mod is a *behavioral reference only* — its
> runtime DLL + its `action_sets.xml`/`monster_usage_sets.xml` (1.2.12 schema) must **not** be depended on at
> runtime. The C# was verified call-by-call against v1.4.5 (the one drift — `ActionIndexCache.Name` → `GetName()`
> — was caught by the compiler and fixed); the data is authored fresh for 1.4.5.

## Slide root-cause isolation (2026-06-10 — DIAGNOSED; fix DEFERRED)

The elephant "slides" — the body translates across the battlefield while the legs don't run a walk/run cycle.
The action-code fix above removed a *constant* slide; a residual slide remained. We ran a one-disable-at-a-time
isolation ladder. **Each row is a deployed build the user tested in a live battle; cumulative unless noted.**

| Build | Cavalry reassign (1) | Trample anim (2) | Bone-tracking (3) | Crew spawn (4) | Result |
|-------|:---:|:---:|:---:|:---:|--------|
| A (7:05) | off | on | on | on | **slides** (right) |
| B (7:14) | off | off | off | on | **slides** (left) |
| rung 4 (7:29) | off | off | off | **off** | **NO slide** |
| control (7:45) | **on** | **on** | **on** | off | **slides** (back) |
| bone test (7:52) | on | on | **off** | off | **NO slide** ✓ |

**Two independent slide sources — both CONFIRMED (2026-06-10). Both must be off for no-slide:**
- **Source 1 — the crew.** B (crew on, 1/2/3 off) slides; rung 4 (crew off, 1/2/3 off) does not. So the 4
  force-spawned crew agents cause a slide on their own.
- **Source 2 — bone-tracking.** control (bone on, crew off) slides; bone-test (bone off, crew off) does not.
  CONFIRMED by the single-variable bone-test.
- **Cleared — cavalry reassignment + trample animation are INNOCENT.** Both were ON in the bone-test build, which
  did not slide. They stay enabled.

**Leading mechanism (unconfirmed): physics bodies placed inside the elephant's collision capsule each tick.**
- Bone-tracking `SetFrame`s the howdah's `bo_empire_keep_a_door_top` physics floor *at the spine bone* (inside the
  elephant body). The fixed-offset fallback places it at `elephant.Position + 3.2 Z` — *above* the capsule, no
  contact (rung 4 used fixed-offset and did NOT slide). So bone-tracking's floor likely overlaps the elephant and
  the solver shoves the dynamic elephant agent.
- The crew likely shove via the same mechanism — teleported each tick onto/near the elephant, their capsules
  overlap the (scaled) elephant capsule.
- **If confirmed, the fix is unified:** stop the howdah floor + crew from physically contacting the elephant
  (drop the howdah's `bo_` physics — archers are teleported, they don't need a physical floor — and/or disable
  crew-vs-mount collision, and/or raise the bone-tracked frame so the floor clears the capsule).

**Latent bug surfaced during the ladder (separate from the slide):** with bone-tracking off, the fixed-offset
fallback (`RepositionToFixedOffset`) placed the archers at the elephant's *legs*, not on its back. So the
build-time + bone-failure safety path is positioning wrong and needs fixing regardless. **Bone-tracking is the
visually-correct placement** (archers on top) and must be restored once the slide is solved.

**Process lesson (logged):** two premature conclusions were made and then refuted by the user's control tests —
first "it's the `as_elephant` data" (refuted by rung 4: crew off then no slide), then "the crew is the *sole* cause"
(refuted by the control build: crew off + 1/2/3 on then slides). Cumulative-disable ladders confirm *necessity* but
not *sufficiency*; isolate a single variable and run the complementary control before concluding a root cause.

**Decision (2026-06-10): fix DEFERRED.** Both slide sources are left **disabled** for now (project-owner call —
come back to it later). The two disabled code paths are marked `DEFERRED` in source with a pointer back here:

| Source | Disabled where | Re-enable when |
|--------|----------------|----------------|
| Crew spawn | `ElephantMissionBehavior.TryInstantiateHowdah` — `TrySpawnHowdahCrew(...)` call commented | The crew↔elephant collision fix lands (e.g. give crew the elephant's `FaceGroupId` — the engine's own rider-vs-mount no-collision mechanism). `TrySpawnHowdahCrew` retained. |
| Bone-tracking | `TaomHowdahMachine.RepositionToElephant` — `TryRepositionToBone()` branch commented (fixed-offset only) | The floor-physics fix lands (drop the `bo_` floor's collision, or raise the bone frame so the floor clears the capsule). `TryRepositionToBone`/`ResolveBoneIndex` retained. |

**Planned fix (when resumed):** both sources are one mechanism — a physics body inside the elephant's collision
capsule. The likely unified fix: stop the howdah floor + crew from physically contacting the elephant. The
candidate API is `Agent.SetAgentExcludeStateForFaceGroupId` / shared `FaceGroupId` (how a rider already avoids
colliding with its own mount) for the crew, plus dropping or clearing the `bo_` floor's collision for the howdah.
Cavalry-reassignment + trample stay enabled (confirmed innocent). The TEMP `harad_militia` Horse-slot test entry in
`troops_harad.xml` remains for testing and must NOT be committed.

## Implementation — C# built + wired (2026-06-05)

The trample + mount-lock are **done, wired, and green** (build + tests). The mount-lock (`ADODAgentStatCalculateModel`)
and the structural attack mechanic from `ADODBeastsElephantAgentComponent.OnTickAsAI` are a behavioral port, re-implemented
in TAOM-clean architecture on the v1.4.5 API — but the attack **cadence** (deterministic cooldowns, 2026-06-10) and the
per-kind randomized **damage** (trample 50-100 / tusk 50-75, 2026-06-15) are TAOM's deliberate rebalance, NOT the donor's
per-tick random roll / fixed ~20 damage.

| File | Role |
|------|------|
| [`Main/Features/Elephant/ElephantConfig.cs`](../../Main/Features/Elephant/ElephantConfig.cs) | Constants — `ElephantMonsterId="taom_war_elephant"`, `MountDifficulty=999`, attack gates (`TrampleTriggerRange=3f` proximity gate restored from the donor mod, `TrampleFacingDot=0.25`, `TrampleCooldownSeconds=10` / `SideAttackCooldownSeconds=4`, `TrampleRadius=4f` damage radius) + per-kind damage bands (`TrampleMin/MaxDamage=50/100`, `TuskMin/MaxDamage=50/75`, `BlockedDamageMultiplier=0.25`) + the attack clip-name constants (`TrampleActionName` / `SideAttackLeft/RightActionName`). `TrampleTriggerRange` was the missing donor gate — without it the elephant swung into empty air and killed the locomotion channel. |
| [`Main/Features/Elephant/IElephantAttackService.cs`](../../Main/Features/Elephant/IElephantAttackService.cs) + [`ElephantAttackService.cs`](../../Main/Features/Elephant/ElephantAttackService.cs) | **Pure** logic (no TaleWorlds deps): `IsElephantMonster`, `ShouldEngage(facingDot, alreadyAttacking)` (facing gate; the BT scan passes -1 when no enemy in range), `IsOffCooldown(lastFired, now, seconds)` (inclusive ≥; future stamps read as ON cooldown), `ComputeInflictedDamage(kind, blocking, roll)` = `round((min + roll·(max−min)) · (blocking?0.25:1))` with the band chosen by `ElephantAttackKind` (Trample 50-100, SideAttack/tusk 50-75; roll is a [0,1] `MBRandom.RandomFloat` supplied per victim by the BT, clamped + NaN-guarded). Unit-tested. |
| [`Main/Features/Elephant/ElephantMissionBehavior.cs`](../../Main/Features/Elephant/ElephantMissionBehavior.cs) | Boundary `MissionLogic`: registers `"ElephantTree"` + attaches a per-agent `BehaviorTreeAgentComponent` to every elephant (first-tick scan + `OnAgentBuild` late-spawn + dead-agent pruning — the warg's exact wiring). `Initialize` logs an error if any attack action resolved to `act_none` (Armory-drift guard). Also instantiates the howdah when the mahout builds. **As of 2026-06-10 the attacks are BT-driven** — the old inline `TryAiTrample` loop was removed (see "Behavior tree" below). |
| [`Main/Features/Elephant/ElephantBehaviorTree.cs`](../../Main/Features/Elephant/ElephantBehaviorTree.cs) + [`BehaviorTreeElements/`](../../Main/Features/Elephant/BehaviorTreeElements/) | Per-agent behavior tree (warg pattern): `EnemyInTrampleRangeDecorator` (facing+range gate → `ShouldEngage`; writes `TargetBearing`), `AttackOffCooldownDecorator` ×2 (→ `IsOffCooldown`), `ElephantAttackTaskBase` → `ElephantTrampleTask`/`ElephantSideAttackTask` (→ `ComputeInflictedDamage`), `ElephantAttackActions` (shared eager-resolved `ActionIndexCache`s + Index-compare gate). Reuses the shared `HasRiderDecorator`/`IsAiControlledDecorator`/`HasNoRiderDecorator`. |
| [`…/CareerSystem/Models/TaomAgentStatCalculateModel.cs`](../../Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs) | EDITED — the shared `AgentStatCalculateModel` slot now also carries the **mount-lock**: `CanAgentRideMount`→false for the elephant + `MountDifficulty=999` (both via the injected `IElephantAttackService`, applied with ternaries per gamemodels.md rule 4). |
| `ElephantIoC.cs`, `IoC.cs`, `SubModule.cs` | Service registered (Singleton); `ElephantMissionBehavior` added to the mission list; the stat-model ctor takes the elephant service. (No new registration for the BT — it attaches inside the mission behavior; nodes resolve the service via IoC like `WargAttackTask`.) |
| [`TAOM.Tests/Features/Elephant/ElephantAttackServiceTests.cs`](../../TAOM.Tests/Features/Elephant/ElephantAttackServiceTests.cs) | 24 tests (IsElephantMonster ×3, ShouldEngage ×5 incl. the no-enemy −1 sentinel, IsOffCooldown ×6 incl. exact-boundary + future-stamp clock skew, ComputeInflictedDamage ×10 — both kinds × min/max/midpoint/blocking boundaries + NaN/out-of-range roll clamps). The BT calls these same pure methods, so they remain the attack decision's regression guard. |

**1.4.5 adaptations vs the donor mod's 1.2.12 decompile:** `ActionIndexCache.GetName()` (not `.Name`); the 2-arg
`SetActionChannel(0, anim)` (the donor's long arg list is exactly the engine defaults); `CustomAttacksUtils.TakeDamage`
(TAOM's clean damage primitive, with a `knockDown` flag that maps to the donor's knockdown BlowFlag) instead of a raw
`Blow`/`RegisterBlow`. Behaviorally identical; no native code (the donor's `NativeHook.dll` is dead anyway).

**Action code correction (2026-06-10):** the attack codes shipped as `act_war_elephant_attack_1..3` and were
claimed to degrade gracefully to `act_none`. This was **wrong** — `act_none` on channel 0 does NOT skip the
animation; it kills the **full-body locomotion channel**, so the elephant body freezes while the engine keeps
translating it (the "sliding" bug). The correct codes are **`act_elephant_attack_1/2/3`** — verified registered
in `LOTRLOME_Armory/ModuleData/action_types.xml` lines 99–102 and bound to real clips in `action_sets_elephant.xml`
lines 89–100. These resolve to valid animated attacks and do not interfere with the walk/run channel. The clip
caches now live in `ElephantAttackActions` (eager-resolved once, shared by the tasks + the engage gate), and
`ElephantMissionBehavior.Initialize` logs an error if any resolve to `act_none` — so a future Armory rename is
detected at mission start instead of silently re-introducing the slide (2026-06-10 review finding).

### Behavior tree (AI attacks) — 2026-06-10

The AI attacks run as a per-agent **behavior tree**, mirroring the warg (`WargMissionBehavior` + `WargBehaviorTree`)
— the foundation of the "rich AI-driven creatures" direction. Phase 1 landed as a behavior-preserving port of the
inline trample; **phase 1.5 (same day) replaced the stochastic model with the project owner's cooldown workflow**:

> enemy in range → **trample** if off cooldown (10s, priority) → else **left/right tusk swing** picked by the
> enemy's bearing, if off cooldown (4s) → else idle — the engine's regular mount AI (rider cavalry AI + native
> charge) always continues underneath; the BT only layers attacks on top.

This is a deliberate behavior CHANGE from the donor mod, verified by decompiling the live `ADOD_Beasts.dll`: the donor picks
randomly among `attack_1..3` at 0.001/tick with no left/right awareness, no cooldowns, and never uses `attack_4`
(which IS bound to a real clip — `action_types.xml` 99–102, `action_sets.xml` 59684–59687).

### Reverted to spear + Cavalry charge — 2026-06-29

The 2026-06-15 bow-rider experiment below was **reversed** (project-owner decision): as a `HorseArcher` the formation
skirmished at range so the elephant never closed to trample. Both the elephant **and** the Mûmakil rider are now
`default_group="Cavalry"` armed with a spear (`eastern_spear_4_t4`) + `aserai_sword_3_t3`, with the bow + both
`bodkin_arrows_b` quivers removed — so the formation **charges** and the auto-trample/tusk BT actually fires. The
per-kind damage bands (next section) are unchanged. See [mumakil.md](mumakil.md).

### Bow rider + lethal damage rebalance — 2026-06-15 (SUPERSEDED by the 2026-06-29 charge revert above)

In-game feedback (elephant confirmed working in battle): the rider was useless and the attacks too weak. Two changes:

- **Rider loadout** (`troops/troops_harad.xml`, `harad_elephant_rider`): the primary spear (`eastern_spear_4_t4`)
  couldn't reach ground targets from the elephant's back, so the `HorseArcher`-grouped rider melee-swung into air.
  The spear is replaced with a **second `bodkin_arrows_b` quiver** (Item0); the rider already carried
  `steppe_heavy_bow` (Item2) + arrows (Item3) + `aserai_sword_3_t3` (Item1, kept as melee backup). With no
  polearm the AI defaults to the bow and now shoots infantry; the two quivers give sustained fire.
- **Per-kind randomized damage:** the shared fixed `round(10·mult)·2 = 20` became distinct per-hit bands rolled per
  victim — **trample 50-100, tusk 50-75** (shield block still scales to ×0.25). The `× 2` doubling artifact is gone
  (damage is expressed directly); `ComputeInflictedDamage` now takes the `ElephantAttackKind` + a [0,1] roll. The two
  attack tasks supply their kind via a new `AttackKind` abstract; the BT passes `MBRandom.RandomFloat` per victim.

**Architecture (warg-consistent).** The BT elements are *boundary code* — they hold the raw `Agent` on the blackboard
and touch `Mission`/`Agent` directly, delegating only the **pure decisions** to the unit-tested
`ElephantAttackService` (`ShouldEngage` facing/anim gate + `IsOffCooldown` + `ComputeInflictedDamage`; 24 tests).
No adapter expansion, no new service. `ElephantMissionBehavior` registers `"ElephantTree"` and attaches a
`BehaviorTreeAgentComponent` per elephant (first-tick scan + `OnAgentBuild` late-spawn + dead-agent pruning); the
engine's `Agent.Tick` auto-ticks each tree.

**Tree shape** ([ElephantBehaviorTree.cs](../../Main/Features/Elephant/ElephantBehaviorTree.cs)):

```
main (Selector)
├─ has rider           [HasRiderDecorator]        ← reused base
│  ├─ ai controlled    [IsAiControlledDecorator]  ← reused base (AI-ridden only)
│  │  ├─ enemy in range [EnemyInTrampleRangeDecorator]  ← facing+3m gate; writes TargetBearing; blocks mid-anim
│  │  │  ├─ trample      [AttackOffCooldown(Trample, 10s)]    → ElephantTrampleTask (attack_1) → Sleep(300ms)
│  │  │  ├─ side attack  [AttackOffCooldown(SideAttack, 4s)]  → ElephantSideAttackTask (attack_2 L / attack_3 R) → Sleep(300ms)
│  │  │  └─ (both on cooldown → falls through to idle = regular AI continues)
│  │  └─ SleepTask(200ms)        ← idle; bounds the native scan cadence (~5/s)
│  └─ SleepTask(1s)              ← player-ridden (ai branch skipped); phase-2 player-trample branch goes here
└─ no rider            [HasNoRiderDecorator]       ← reused base
   └─ SleepTask(4s)
```

| BT element | Role | Pure logic it calls |
|------------|------|---------------------|
| `EnemyInTrampleRangeDecorator` | Engage gate: cheap already-attacking exit, then one radial scan for the best-facing live enemy within `TrampleTriggerRange`; writes the enemy's signed bearing to the blackboard. | `ShouldEngage` |
| `AttackOffCooldownDecorator` | One class, two instances — gates the trample (10s) and side-attack (4s) branches off the blackboard stamps. | `IsOffCooldown` |
| `ElephantAttackTaskBase` → `ElephantTrampleTask` / `ElephantSideAttackTask` | Shared template: play clip on channel 0, stamp cooldown, radial knockdown to all enemies in `TrampleRadius`. Side task picks left/right by bearing sign (positive = LEFT, Z-up right-handed cross product). | `ComputeInflictedDamage` |
| `IBTElephantBlackboard` | Cooldown stamps + `TargetBearing`, reflection-copied onto every node by the tree builder. | — |

**Clip-role mapping — VERIFIED numerically (2026-06-10, Blender trajectory analysis).** Nothing in the XML or
the donor's code identifies the clips (numbers everywhere, and the donor picked randomly), so the roles were measured: the
staged source FBX (`elephant_anims_all_left.fbx`, frame ranges from the pack0.tpac parse) was imported into a live
Blender session (temp datablocks, cleaned to baseline) and the `Head_08` bone's signed lateral excursion sampled
per clip window. Result — **`attack_1`** winds up right then strikes sweeping **toward the LEFT** (left-target
swing); **`attack_2`** is its mirror (right-target swing); **`attack_3` and `attack_4`** are near-identical full
**double-sweep thrashes** (right→left→right, 60 frames) — the natural trample visual for radial damage. Mapped in
`ElephantConfig`: trample = `attack_3` (alternating randomly with `attack_4` for variety — a clip the donor never
played), left swing = `attack_1`, right swing = `attack_2`. The stand-window control measured ~zero motion,
confirming the frame ranges. Final eyeball check in one battle is still welcome, but the mapping is no longer a guess.

**Phase 2 (future, not built):** a `player controlled` branch under "has rider" (the donor's Space/Input-57 trample), and
optional enrage/charge state (warg-style rage). Those add blackboard fields + branches without touching this baseline.

**Tested-via-game:** BT elements + wiring are not unit-tested (consistent with all warg BT elements + Harmony patches,
ADR-008); the pure decisions they call are covered by `ElephantAttackServiceTests` (24/24 green).

**Reviewed three ways, all clean (2026-06-10, all BEFORE commit):** a custom 13-agent adversarial workflow (below),
then the stock `/deep-review` (5 agents — Standards / 32-of-32 v1.4.5 APIs / 16-of-16 tests / 8-of-8 data-flow traces,
verdict READY) and `/review-codex` (gpt-5.5 xhigh — VERDICT CLEAN, 0 findings), which **agreed with the deep-review
on all 8 Known Suspects**. Codex independently decompiled `Vec2.LeftVec()` (handedness) and verified
`BTBlackboardValue<T>` is a class (cooldowns engage). Full record: REVIEW-LOG.md Review 52.

**Review notes (2026-06-10 adversarial workflow, 13 agents, 10 confirmed findings — 4 fixed, 5 recorded, 1 was the
stale table above).** Accepted-behavior observations, recorded so they aren't re-derived:
- **Side swings deal the full 360° radial damage** (same disc as the trample, only the clip + cooldown differ) —
  spec asked for animations + cooldowns; a bearing-cone victim filter is an "improve" item.
- **Left/right pick is near-arbitrary in crowds** (the max-facing-dot enemy is the most front-on one, bearing ≈ 0) —
  visually fine for a dead-ahead target. Flank/rear enemies (dot ≤ 0.25) never trigger ANY attack (the donor's gate);
  if flank coverage is ever wanted, pick the max-|bearing| in-cone enemy or relax the gate for side attacks only.
- **Bearing convention verified against the engine:** `Vec2.LeftVec()` = (-y, x) in the v1.4.5 decompile —
  positive cross-z = LEFT is TaleWorlds' own convention.
- **Effective trample period is ~10.7–12s, not a crisp 10s** — a side swing started just before the trample comes
  off cooldown blocks it for one clip length (the priority ordering is honored at every evaluation instant).
- **Cooldowns/sleeps are wall-clock `DateTime.Now`** (elapse during pause; real-time under slow-motion) — library +
  warg precedent (`SleepTask` itself is wall-clock). Switch to `Mission.Current.CurrentTime` only if pause-exploit
  behavior ever matters.
- `base(10)` is NOT a 10ms eval throttle (int division truncates <1000 to 0 → tree runs every component tick, warg
  same); the `SleepTask` leaves are the only real pacing knobs.
- `ElephantMissionBehavior` is 253 lines vs ADR-002's <150 guidance — ~65 lines are the deliberately-disabled
  `TrySpawnHowdahCrew` (deferred slide fix); extract a howdah-installer boundary class when the crew fix lands.

### Action-sets deployment crash history (2026-06-08)

Two crashes were encountered when deploying the elephant action-set self-contained inside LOTRLOME_Armory
(without the upstream beasts pack). Both trace to the same architecture finding about how Bannerlord loads animation data.

#### Core finding: `action_sets` is a native-only data type

- `GetMergedXmlForNative` iterates `XmlResource.MbprojXmls` — populated from `project.mbproj` `<file>` entries only.
- `GetMergedXmlForManaged` iterates `XmlResource.XmlInformationList` — populated from `SubModule.xml` `<XmlNode>` entries only.
- These are **completely separate pipelines** with no overlap.
- `action_sets` is **native-only**: processed exclusively through `project.mbproj → GetMergedXmlForNative → C++ animation engine`.
- `SubModule.xml` `<XmlName id="action_sets" ...>` entries are **meaningless** — vanilla `Native/SubModule.xml` has **zero** action_sets entries. Any LOTRLOME action_sets XmlNode in SubModule.xml was never doing anything.
- The correct pattern (confirmed by the spider precedent): merge all action_sets for a module into its **single** `soln_action_sets` file registered in `project.mbproj`.

#### Crash #3 — `KeyNotFoundException` at startup (`MBObjectManager.MergeElements`)

**Symptom:** Game crashed ~17.6s into startup (`System.Collections.Generic.KeyNotFoundException` in `MBObjectManager.MergeElements` during `Module.CreateProcessedActionSetsXMLForNative`).

**Root cause:** Adding `action_sets_elephant.xml` as a **second** `soln_action_sets` entry in `project.mbproj` caused `MergeTwoXmls` to be called with `element1` = the fully-accumulated action_sets from ALL previous modules (Native + SandBox + Alliance.Wargs + LOTRLOME's main 60K-line `action_sets.xml`). `MergeElements` builds a dictionary from `element1`'s children keyed on `elementSchema[GetFullXPathOfElement(element3)]` — if any child in the accumulated `element1` has an XPath not present in the action_sets XSD schema → `KeyNotFoundException`.

**Fix:** Remove the second `soln_action_sets` entry. Merge the elephant action_sets block into LOTRLOME's existing single `action_sets.xml` (no second `MergeElements` call).

#### Crash #4 — `AccessViolationException` in `Skeleton.TickAnimations` (thumbnail render)

**Symptom:** Game loaded but crashed when the party/character screen tried to render a portrait thumbnail for a troop with the elephant in its Horse slot — `System.AccessViolationException` in `Skeleton.TickAnimations`, called from `CharacterSpawner.SpawnMount`.

**Root cause:** After removing the second `soln_action_sets` entry (Crash #3 fix), the elephant action_sets were no longer registered in the **native animation engine** at all (they'd been dropped entirely, not yet merged into the main file). When the thumbnail system created an elephant skeleton and called `Skeleton.TickAnimations`, no animations were registered for `elephant_skeleton` → AV in native code.

**Fix:** Merge the three elephant action_set definitions (`as_elephant`, `as_elephant_town_and_village`, `as_elephant_map`) into LOTRLOME's main `action_sets.xml` (registered as the single `soln_action_sets` entry in `project.mbproj`). The native engine then registers `as_elephant` on load → `TickAnimations` finds a valid animation state → no AV.

**Resolution (2026-06-08):** Both crashes fixed. Elephant thumbnail renders correctly; elephants spawn and fight in battle (confirmed by screenshot). The standalone `ModuleData/Animations/action_sets_elephant.xml` still exists as a reference copy but is not registered anywhere — the live entries are the appended block at the bottom of `action_sets.xml`.

### Remaining seams (need the Modding Kit / Blender / you)

1. **Monster + Item data** — **ready-to-drop files authored**: [`elephant/lotr_monster_elephant.xml`](elephant/lotr_monster_elephant.xml)
   (copy to `LOTRLOME_Armory/ModuleData/Monsters/LOTR/` + register in its SubModule.xml) and
   [`elephant/horse_item_taom_war_elephant.xml`](elephant/horse_item_taom_war_elephant.xml) (paste the `<Item>` into
   `LOTRAOM_horses.xml`). Both validated well-formed; the Monster id stays `taom_war_elephant` to match `ElephantConfig`.
   (Corrected one donor 1.2.12-ism on the way: `bones_to_modify_on_sloping_ground_0` was `Spine2` — a bone our rig
   doesn't have — fixed to ` Spine_04`.)
2. **1.4.5 action-set + clips** — author `as_war_elephant` against the **vanilla 1.4.5** quadruped-mount schema
   (NOT the donor's 1.2.12 `as_elephant`), binding TAOM-compiled clips. This is the gating dependency for animation.
3. **Animations** — **idle + walk authored** (2026-06-06, in Blender via MCP — see below); remaining clips
   (run/turn/rear/death + the 3 attack clips) + the Modding-Kit compile of all clips are the next step.
4. **Harad rider troop + recruitment**, and an in-game smoke test.

### Authored animations — first pass (2026-06-06, Blender via MCP)

Authored two in-place, looping elephant clips directly on our `elephant_skeleton` rig via the Blender MCP
(procedural keyframing, no Cascadeur/visual-iteration loop — these are a **rough first pass to refine**, NOT
production-grade; validated via side-view renders that the rig deforms correctly + the gait alternates):

| Clip | Frames | Motion |
|------|--------|--------|
| `an_war_elephant_idle` | 1–49 (loop) | breathing (spine pitch) + head sway + a phase-offset wave down the 29-segment trunk + ear flick + tail sway; legs static |
| `an_war_elephant_walk` | 1–25 (loop) | 4-leg lateral-sequence gait (LH→LF→RH→RF, ±11° hip swing + slight knee lift) + subtle body bob + trunk/tail sway; **zero root translation** (engine supplies forward travel — `feedback_movement_anims_in_place_engine_driven`) |

- **Exported** (Modding-Kit-ready, verified: 1 Null + 60 LimbNodes + animation take, 0 meshes):
  `E:\LOTRAOMAssets\Elephant\clips\an_war_elephant_idle.fbx` + `an_war_elephant_walk.fbx`. Recipe:
  `object_types={ARMATURE}`, **`primary_bone_axis='Y'`** (lesson L13), `add_leaf_bones=False`,
  `bake_anim_use_nla_strips=True` (bare take names, lesson L11) — same axis as the body FBX so they bind.
- **Workspace saved** for refinement: `E:\LOTRAOMAssets\Elephant\elephant_anim_workspace.blend` (the 2 actions live on the rig).
- **Bone-axis reference for further authoring** (from the rig diff): forward=+Y, up=+Z; **local-Z rotation = fore/aft
  pitch** (leg swing, head nod, trunk/tail fore-aft) for nearly every bone (their local-Z ≈ world +X);
  **local-X = lateral sway**. +local-Z swings a leg forward. Legs: front = ` R/L UpperArm`→`Forearm`→`Hand`;
  back = ` R/L Thigh`→`Calf`→`Foot` (point −Z). The 29-seg trunk = ` Queue de cheval *`, the 7-seg tail = ` Tail*`.
- **Next:** author run/turn/rear/death + the 3 `act_war_elephant_attack_*` clips the same way (or refine these in
  Cascadeur), import all into the Modding Kit → `*_anm.tpac`, and bind them in the 1.4.5 `as_war_elephant` action-set.

### Animation refinement — full TAOM-owned locomotion set (2026-06-12, Blender-MCP)

Workflow + theory: [creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md).
Dedicated `elephant_rider_*` clips are located but not yet improved — to be assessed via the composite
method (master §4).

Superseding the 2026-06-06 rough first pass: extracted the donor's source gaits
(`elephant_anims_all_*.fbx`; per-clip frame ranges parsed from the compiled `_anm.tpac`
Source1/Source2 fields by `_refine_tools/tpac_clipinfo.py` — walk 680-740, canter/gallop 750-780,
turns 680-740 in the `_turn_*` FBX) onto our `elephant_skeleton` rig and corrected toward natural
elephant biomechanics, measured with a foot-trajectory analyzer (`_refine_tools/harness.py`).

- **walk** (`an_war_elephant_walk`, 61f): the donor's was a 2-beat **pace** (ipsilateral legs swing
  together — a waddle); re-phased the front legs +¼ cycle → **4-beat lateral sequence** BL→FL→BR→FR.
  Front-foot over-lift (1.02 vs back 0.26 — "circus march") **height-weight-damped to 0.48** (blend
  toward the planted-frame pose, weighted by foot height, so planted frames stay grounded). In-place
  (root net Y ≈ 0).
- **run** (`an_war_elephant_run`, 38f): the donor's "canter" was a diagonal **trot** (FL+BR then FR+BL —
  no elephant can, it implies an aerial phase); rebuilt as a **fast amble** (time-compressed refined
  walk → lateral sequence, no suspension).
- **trot / walk_backwards / turn_left / turn_right / idle** authored to complete the set.

7 Kit-ready FBX at `E:\LOTRAOMAssets\Elephant\clips_refine_20260612\` (armature
`elephant_skeleton_notused`, 60 bones, no mesh, Bannerlord axes, baked, take-named; round-trip
verified on Blender **5.1.2** — slotted-action API, `Action.fcurves` replaced by
layers/slots/channelbags). Work scene `elephant_refine_WORK_20260612.blend`; tooling + before/after
renders (`CMP_walk_src_vs_refined.png`) in sibling dirs; pristine backups in `_backups\`. **NOT
Kit-compiled / in-game-tested** (Blender→tpac is GUI-only). Compile steps + the **mandatory
`quad_movement` clip-usage tagging per clip** (else AV at `Skeleton.TickAnimations`):
`clips_refine_20260612\README_HANDOFF.md`. Faithful donor baselines kept (`*_SRC` actions) to A/B
in-game. Memory: `project-elephant-animation-refine-inflight`.

### Idle ear-fan (2026-06-13, Blender-MCP)

Baseline-analyzed all 7 refined clips with `analyze_gait`; the gaits were sound (lateral
sequence, in-place, secondary trunk/ear/tail motion present), but the **idle's ears were frozen**
(0.5° local rotation — a statue) while a standing elephant constantly fans its ears. Authored a
natural ear-fan: raised-cosine out-and-back about each ear bone's **local X** (the diagonal ear
rig fans the tip out+up / in+down on local X — the best horiz/vert tip ratio of the three axes,
measured; driver 14°, distal segment lags 4f at +6° floppy follow-through). ~19 cm ear-tip travel
per loop, clean cyclic seam (`loop_seam_rot_deg` 25.5 → 26.4), still in-place. The reusable
`add_ear_flap()` was added to `_refine_tools/harness.py`. Idle re-exported to
`clips_refine_20260613\an_war_elephant_idle.fbx`. Handoff +`quad_movement` reminder:
`clips_refine_20260613\README_HANDOFF.md`.

Same session, **canter + gallop rebuilt**: the deployed `as_elephant` binds the raw ADOD
`elephant_canter` / `elephant_gallop` for paces 4–5, and those are diagonal trots with an implied
aerial phase (impossible for a 5-ton elephant). Replaced with `an_war_elephant_canter` (43f) +
`an_war_elephant_gallop` (33f), time-compressed (`timescale_action`) from the verified refined
walk so they inherit the correct 4-beat lateral sequence, damped lift, in-place root, secondary
motion, and clean loop — faster ambles, the elephant's true top gait. Exported to
`clips_refine_20260613\`; bind canter→pace4, gallop→pace5 at the `as_war_elephant` rename. (trot +
strafe reuse the walk in the deployed set — no separate clips needed.) **Open lifts** (not done):
`turn_left`/`turn_right` are identical duplicates with no turn character; rear-foot stance slip on
the gaits; rider clip authoring (in progress separately).

> **Blender-session note (2026-06-13):** two Blender instances were open during this work; the
> Kit-ready **FBX in `clips_refine_20260613\` are the canonical deliverables** (instance-
> independent). The `.blend` got Save-As'd to `troll_anim_WORK_20260613.blend` (16.6 MB, full
> elephant scene + an in-progress rider composite) — naming is muddled but no data was lost.

### The real donor animation source — found, but unsliced (2026-06-06)

The production donor elephant clips exist as **source** at
`…/Modules/ADOD_Beasts/AssetSources/elephants/elephant_anims_all_{left,right,turn_left,turn_right}.fbx` (on
`elephant_skeleton` — all 52 non-leaf bones match our rig exactly). **But each is one ~1341-frame concatenated take
with no embedded clip boundaries** — no named sub-takes, no pose markers (the donor defined the per-clip frame ranges
inside the Modding Kit project, which isn't shipped). A leg-motion-profile heuristic couldn't separate them cleanly
(a 672-frame low-activity block then dozens of 8–12 frame fragments). So the source **can't be auto-sliced** — it
must be sliced by scrubbing in the Modding Kit (how the donor built it).

**The already-sliced real clips = the 31 compiled `elephant_*_anm.tpac`** in `…/ADOD_Beasts/Assets/elephants/animations/`
— skeleton-agnostic clip data that binds to `elephant_skeleton` (our rig). **Fastest path to the full 1-for-1 set:**
copy those tpacs into `LOTRLOME_Armory/Assets/creature/elephant/animations/` and reference them in `as_war_elephant`
(rename to `an_war_elephant_*` to avoid a donor id collision). Caveat: 1.2.12-era, but clip tpacs are skeleton-relative
keyframe data (no embedded skeleton — confirmed by the deep-dive), so they should bind on 1.4.5; verify in-game.

**Three animation paths — your call:** (a) **compiled tpacs** — real, full 31-clip set, fastest, slight 1.2.12 risk;
(b) **Kit-slice the source FBX** — fully re-authored/owned, but manual boundary definition; (c) **the hand-authored
idle+walk above** — fully owned + 1.4.5-clean, but only 2 rough clips. (a) is quickest to a working animated elephant;
(c) is the safest fully-owned starting point. The trample C# works regardless (codes degrade to `act_none`).

**Resolution (2026-06-06): duplicate into LOTRLOME_Armory, done in the Kit (project-owner choice).** The 5 donor
elephant animation-source FBX (`adod_elephant.fbx` + `elephant_anims_all_{left,right,turn_left,turn_right}.fbx`)
were **staged into `LOTRLOME_Armory/AssetSources/elephants/`** (same `elephant_skeleton` → binds to our rig). The
owner re-creates the 30 clips in the Modding Kit from these + the frame ranges (the Kit shows each clip's
`Source 1`/`Source 2`). **Why not auto-sliced:** the per-clip ranges DO ship — in `ADOD_Beasts/AssetPackages/pack0.tpac`,
where each clip resource is `[name][int32 size-marker ~120–300][+12 Duration f][+16 Source1 f][+20 Source2 f]…[+60/64/68
StepPoints −1,−1,−1]`. `elephant_attack_1` = 900–940 (matches the Kit), attacks 2/3/4 = 950–990 / 1000–1060 / 1070–1130,
death = 1255–1335, stand_2/3 = 540–600 / 610–670. But the **locomotion families** (walk/trot/turn, canter/gallop +L/R)
collide in a heuristic parse (names live in a shared table + the defs) — not reliably auto-extractable, so the Kit is the
source of truth for those. My hand-authored `an_war_elephant_idle`/`_walk` remain as the throwaway fallback.

## Overview

A rideable **Harad war elephant** — a `Mountable=true` mount (ridden by a Harad crewman) that also **auto-attacks**
(tramples / gores with its tusks) on its own, modelled on TAOM's working **warg** mount. The asset we import is **our
own FBX** (`E:\LOTRAOMAssets\Elephant\Meshes BL\elephant_harad_armor_01.fbx`); its **rig + mesh + textures** match
the donor mod's elephant (the asset was purchased from Artem, the donor-mod author, for use in TAOM), while the **animations are
TAOM-authored on that FBX** — *not* reused from the donor mod (project-owner decision, 2026-06-05). The data is re-authored
under TAOM ids and themed for Harad, held to TAOM standards (adapter pattern, tests, ADR compliance).

## Why this exists — and why it is tractable where the spider was not

The [Giant Spider](spider.md) was paused after an exhaustive 2026-06-05 investigation: spawned as a **non-mountable,
detached `FromHorseObj` agent** (`Mountable="false"`), it hard-crashes in native `Agent.PreloadForRendering` on its
62-bone skeleton, and the crash is **specific to the detached/non-mountable mount-render path** (every data-level fix
— mesh split, ≤4 influences, physics, skeleton integrity, shader cache, material binding — was refuted; see
[rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md) §2026-06-05).

The elephant **sidesteps that wall entirely** because it is a **ridden mount** (`Mountable=true`), which uses the
fully-proven horse/warg machinery — no detached-creature hacks, no `FromHorseObj`-mismatch, no native-wield guards.
This is confirmed, not hoped: **the donor mod ships the exact same 60-bone elephant rig as a working `Mountable=true` mount.**

### The asset facts (verified 2026-06-05)

| | TAOM's elephant FBX (`E:\LOTRAOMAssets\Elephant\...\elephant_harad_armor_01.fbx`) | The donor mod's shipped elephant (`ADOD_Beasts/Assets/elephants/adod_elephant_geo.tpac`) |
|---|---|---|
| Skeleton | `elephant_skeleton` (renamed from `…_unused` + re-exported, 2026-06-05; verified 60 bones / 0 leaf) | `elephant_skeleton`, **60 bones**, `Usage='horse'`, 59 D6 ragdoll joints |
| Body mesh | `SK_Elephant_Armor_A` (+ .base/.legs/.nose/.tusk/.head/.platform/.pillow/.cloth/.belt/.feather parts) | `sk_elephant_armor_a` + `elephant_mesh` |
| Rider bone | `" Spine1_05"` (leading space) | `rider_sit_bone=" Spine1_05"` |
| Animations | none shipped (mesh+rig only) | `elephant_anims_all` (bundled on the skeleton) |
| Textures | `t_creature_elephant_a1/a2` + 4 armor sets (d/n/s) | (same family) |

**Conclusion: it is the same rig.** So the bone count (60) is a **non-issue** — it works as a ridden mount. We adopt
the donor's **rig + mesh + textures** (which are our FBX) and the proven `Mountable=true` mount recipe, but **TAOM authors
its own elephant animations on our FBX** (the creature-animation pipeline) — *not* a reuse of the donor's `elephant_anims_all`
(project-owner decision, 2026-06-05). So it is a **rig/mesh adopt + custom-animation build**, with the proven mount recipe.

#### Verified bone roster (`tpac_skeleton_dump.py` on `adod_elephant_geo.tpac`, 2026-06-05)

`elephant_skeleton` — **60 bones, `Usage='horse'`, 59 D6 ragdoll joints**. The body chain (leading-space + `_NN`
Blender-export names): ` Pelvis_03` (root, parent −1) → ` Spine_04` → ` Spine1_05` (the rider-sit bone) → ` Neck_06`
→ ` Neck1_07` → ` Head_08`. Hanging off ` Head_08`: the **ear chains** (`ear_L_1_023`/`ear_L_2_024`,
`ear_R_1_025`/`ear_R_2_026`) and a long **trunk chain** rigged as ` Queue de cheval *` (French "ponytail" — ~19
segments, the deformable trunk). The body bodies carry TaleWorlds `body_type`s (`abdomen`/`chest`/`neck`) + masses
(pelvis 13.4, spine ~32.7). **This is the roster TAOM's imported `elephant_skeleton` must match bone-for-bone**
(open item #2) before the action-set copy / clip authoring rely on it. Note: the trunk + ears *do* have dedicated
bones (so trunk/ear motion is keyable), they're just not surfaced as `<Monster>` attributes — the Monster only names
spine/neck/head for rider-sit + look-direction.

## How this differs from the paused spider (the core lesson)

The engine supports exactly **two agent shapes**: a **humanoid combatant** (skin + weapons + formation, the normal troop
path) and a **ridden mount** (a rider sits on it; the horse/warg/camel path). There is no third "non-humanoid riderless
combatant" shape — and that, not the bone count, is what the spider session ran into.

| | **Donor-mod elephant** (works) | **TAOM spider** (paused, crashed) |
|---|---|---|
| Agent shape | **Ridden mount** — a supported lane | Non-humanoid **riderless combatant** — a shape the engine doesn't have |
| `Mountable` | `true` (a rider mounts it) | `false` (detached, no rider) |
| Spawn | Normal vanilla troop spawn (rider + mount, like cavalry) | Custom **detached `FromHorseObj`** spawn via a Harmony patch (`SpiderDetachedAgentSpawner`) |
| Render | Standard ridden-mount render — proven by every horse/warg | The **detached/non-mountable mount-render sub-path** → AVs in native `Agent.PreloadForRendering` |
| Native wield state | Rider carries the weapons; the mount has none → no uninitialised native-wield garbage | Riderless → garbage native-wield pointers (`0xee0`/`0xee4`) → needed a 3-method wield guard |
| AI / movement | The rider's normal cavalry AI drives the mount | Hand-driven via `SetScriptedPositionAndDirection` (a riderless-AI hack) |
| Auto-attack | The mount tramples/gores while ridden (the **warg** pattern) | A bespoke detached BT + move-node + bite service |
| Bone count | 60 — fine (ridden) | 62 — crashed, **but because of the detached path, NOT the count** — the donor's 60-bone elephant proves a 60-bone skeleton renders fine as a ridden mount |

**Takeaway:** the donor mod did the *supported* thing — a creature as a **ridden mount** — and never touched any of the five
crash layers we fought. We tried the *unsupported* thing — a creature as a **riderless autonomous combatant** — by
hacking a mount (`FromHorseObj`) into a riderless fighter. The elephant is trivial by comparison precisely because it is
*naturally a ridden mount that auto-attacks* — exactly the warg pattern. (If the spider is ever revived, the same
insight applies: make it a **ridden mount**, not a detached agent.)

## The recipe (from the donor mod's working elephant)

Extracted by the donor-mod deep-dive (workflow `w21npmp7s`, 4 agents, 2026-06-05) — decompiled `ADOD_Beasts.dll`
+ read `adod_beasts.xml`, `adod_beasts_items.xml`, `elephant_troop_tree.xml`, `action_sets.xml`,
`monster_usage_sets.xml`. The headline: **the donor's elephant is a STANDARD Bannerlord mount — the exact warg
pattern — not a detached agent.** The recipe has four data parts + one C# part:

**1. Monster** (`adod_beasts.xml` `<Monster id="elephant">`, lines 58–114):
- `Mountable="true"`, `CanRear="true"`, `CanCharge="true"`; **no rein bones**
- `action_set="as_elephant"`, `monster_usage="elephant"`, `family_type="10"`
- `sound_and_collision_info_class="bovine"` (a vanilla class)
- Bone block (leading-space + `_NN` Blender-export names — **copy verbatim**): `rider_sit_bone=" Spine1_05"`,
  `pelvis_bone=" Pelvis_03"`, `head_look_direction_bone=" Head_08"`, plus the full ragdoll roster
  (` Spine_04`, ` Neck_06`, ` Neck1_07`, ` L/R Clavicle`, ` L/R Thigh`, ` L/R UpperArm`).

**2. Item** (`adod_beasts_items.xml`, lines 79–182): a `Type="Horse"` item with
`<Horse monster="Monster.elephant" is_mountable="true" charge_damage="350" …>`; 4 `HorseHarness` items
(`family_type="10"`). The mount mesh hangs off this Horse item (and the howdah/armor off the HorseHarness),
exactly like a cavalry horse + barding.

**3. Rider troop** (`elephant_troop_tree.xml`): a **normal `NPCCharacter`** whose equipment fills the
`Horse` + `HorseHarness` slots with the elephant item — **there is NO non-humanoid "creature troop."** A
5-tier rider tree. This is the single most important structural fact: the elephant is ridden by an ordinary
humanoid, so the whole spider problem (a riderless non-humanoid body) never arises.

**4. Action set** (`action_sets.xml` `as_elephant`, lines 353–463): `skeleton="elephant_skeleton"`,
`movement_system="quadrupedal"`; binds the 31 elephant clips; defines custom `act_elephant_attack_1..4`;
**requires** the `as_elephant_town_and_village` + `as_elephant_map` child derivations. (Porting note: the donor's
`as_elephant_town_and_village` has a copy-paste bug — it sets `act_elephant_stand_1` four times instead of
`stand_1/2/3/4`; fix on port.) Plus a full `monster_usage_set` named `elephant` (`monster_usage_sets.xml`).

**5. Auto-attack C#** (decompiled `ADODBeastsElephantAgentComponent` + `ADODBeastsMissionLogic`): the trample
is **pure managed C#**, the same mechanic as TAOM's `WargAttackService` — `OnTickAsAI` (when a rider's target
is < 3 m, look-dot > 0.25, roll < 0.001) plays `act_elephant_attack_N` and builds a `Blow` + `RegisterBlow`;
the player triggers it on Space (Input 57). The heavy hit is the engine's native charge (`charge_damage=350`).
The component is attached in `OnAgentBuild` off the HorseHarness, mirroring `WargMissionBehavior`. A
mount-lockout (`CanAgentRideMount=false` + `MountDifficulty=999f` for non-rider agents) stops the AI stealing
the elephant. **`NativeHook.dll` / `EasyHook.dll` are DEAD in the donor mod** — a leftover `using`, zero call sites in
the 5029-line decompile, no `DependedModule`. **There is no native code to port.**

### Deep-dive decision: structure from the warg, data from the donor mod

The deep-dive's verdict (which I'm adopting): **clone the warg's architecture for the C#, re-author the donor's
data under TAOM/Harad ids, port no native code.** Concretely — `Main/Features/Elephant/` as a sibling of
`Main/Features/Warg/`; an adapter-pure `IElephantAttackService` (ADR-002/007) that uses the warg's
`CustomAttack` + `CustomAttacksUtils.TakeDamage` path (a clean bone-collision hit) **instead of** the donor's
radial `Blow` loop; `ElephantAttackServiceTests` (the donor ships none); `FiniteFloatValidator` on any config;
fold the `CanAgentRideMount` + `MountDifficulty 999` mount-lock into the existing TAOM `AgentStatCalculateModel`;
defer the howdah (YAGNI); and — per the faction-map update rule (memory `feedback_faction_map_update_with_cultural_feats`)
— update `factions.json` if the war elephant becomes a Harad identity element. Use a plain war-elephant id
(e.g. `taom_war_elephant`), `culture=Culture.harad` — **not** a mumakil/Oliphaunt id (those are a separate, giant
creature TAOM already frames via `mumak_rider`).

## Plan (port → re-theme → standards)

Ordered, following the deep-dive's step list (structure from warg, data from the donor mod, no native port):

1. **Verify the rig matches — DONE (2026-06-05).** Diffed our FBX armature against the donor's `elephant_skeleton`:
   **identical, bone-for-bone, 60 bones, same order** (` Pelvis_03` → ` Spine_04` → ` Spine1_05` → ` Neck_06`
   → ` Neck1_07` → ` Head_08`, then ears + trunk + 4 legs + tail). The rig is confirmed.
   **Skeleton renamed + FBX re-exported — DONE (2026-06-05):** the armature (object + data) was
   `elephant_skeleton_unused`; renamed to **`elephant_skeleton`** (the spider's `_notused` → `_` fix), the body mesh
   `elephant_mesh_unused` → **`elephant_mesh`** (matches the donor), and `elephant_harad_armor_01.fbx` re-exported with the
   proven recipe (`object_types={ARMATURE,MESH}`, `primary_bone_axis='Y'`, `secondary_bone_axis='X'`,
   `axis_forward='-Y'`, `axis_up='Z'`, `add_leaf_bones=False`, `bake_anim=False`). **Verified** via
   `tools/extract_fbx_bones.js`: root Null `elephant_skeleton` + **60 LimbNodes (0 leaf bones)** + 11 meshes
   (`elephant_mesh` + 10 `SK_Elephant_Armor_A.*`). Original backed up to `elephant_harad_armor_01.orig.fbx`.
   **The FBX is ready to import into the Modding Kit.** The `SK_Elephant_Armor_A.*` part names are fine as-is (the
   Monster/Item XML references whatever we author).
2. **Assets** — copy the donor's elephant rig/mesh/textures into TAOM (or `LOTRLOME_Armory`); keep the mount
   `Usage='horse'` on the skeleton; run `tools/tpac_skeleton_transplant.py <tpac> elephant_skeleton --usage horse`
   if physics needs re-applying after a re-import. (Animations: TAOM-authored on our FBX — step 4b, NOT the donor's clips.)
3. **Data — Monster + Item + usage + action_set** (re-authored under TAOM/Harad ids — a plain war-elephant name,
   e.g. `taom_war_elephant`, **not** a mumakil id; final id is your call):
   - **Monster** `taom_war_elephant`: copy the donor's `<Monster>` bone block verbatim, keep `Mountable/CanRear/CanCharge=true`,
     `action_set=as_war_elephant`, `monster_usage=…`, `sound_and_collision="bovine"`.
   - **Item** (Horse) `taom_war_elephant`: `Type="Horse"`, `culture=Culture.harad` (not empire), tuned `charge_damage`,
     body mesh + `<AdditionalMeshes>` for armor/howdah + `<Materials>` → the elephant textures,
     `<Horse monster="Monster.taom_war_elephant" is_mountable="true">`; + a `HorseHarness` (`family_type="10"`).
   - **action_set** `as_war_elephant` + the **required** `_town_and_village` + `_map` children (fix the donor's
     `stand_1`×4 copy-paste bug); the `monster_usage_set`. Rename `act_elephant_*` → `act_war_elephant_*` so we
     carry no runtime dependency on the donor mod being installed. Register all in `SubModule.xml`.
4. **Validate data** — `python tools/validate_moduledata.py` before any C# (the external LOTRLOME XML is
   out of its scope, but the Main-module refs are not).
4b. **Animations (TAOM-authored, on our FBX)** — author the elephant clips on our FBX (walk / run / idle /
   turn / charge / trample / tusk-gore / death + trunk + ear motion) via the creature-animation pipeline
   (Blender/Cascadeur → Modding-Kit compile), bound in the action set. The rig is a standard quadruped, so
   body+leg gaits can retarget from horse/warg; the **trunk + ears are the bespoke part** (the donor bakes trunk/ear
   motion into each clip — no separate bones — so TAOM must key them into our clips). Reference the donor's action
   *coverage* only (it reuses `elephant_walk` for trot + strafe — TAOM can author real trot/strafe for polish).
   See "Declined: reusing the donor's clips" below for why we author rather than reuse.
5. **C# — clone the warg.** `Main/Features/Elephant/` as a sibling of `Main/Features/Warg/`: an adapter-pure
   `IElephantAttackService` (ADR-002/007) using the warg's `CustomAttack` + `CustomAttacksUtils.TakeDamage`
   bone-collision path (**not** the donor's radial `Blow` loop); an `ElephantMissionBehavior` that attaches the
   attack component in `OnAgentBuild` (mirroring `WargMissionBehavior`); `FiniteFloatValidator` on config.
6. **Mount-lock** — fold `CanAgentRideMount=false` + `MountDifficulty=999f` (non-rider agents) into the existing
   TAOM `AgentStatCalculateModel` so the AI can't steal the mount.
7. **Recruitment** — author the Harad rider tier(s) + pool via `VolunteerRecruitmentService`; the rider is a
   **normal humanoid `NPCCharacter`** with the elephant in its `Horse`+`HorseHarness` slots (no creature troop).
8. **Tests** — service 100%, mission behavior 80%+ (ADR-008).
9. **Standards / ship** — `/verify` → `/deep-review` → `/review-codex` → issue + `docs/features/elephant.md` +
   CHANGELOG before commit.
10. **In-game smoke** — mount, charge, confirm trample + knockdown, confirm AI cannot steal the mount.

### Declined: reusing the donor's clips (recorded, not chosen)

The deep-dive found the donor mod ships **31 elephant-skeleton clips + 32 human-rider clips**, all *clip-only*
(skeleton-agnostic, TaleWorlds anim-clip type `506509c8-…`, no embedded skeleton), so they would technically
drop straight onto our `elephant_skeleton` if the bones match — a complete, production-grade set covering
walk/trot/canter/gallop(+turns)/idle/rear/dash/hit/death + 4 trample attacks. **The project owner chose to
author TAOM's own clips on our FBX instead (2026-06-05), so the donor's clip set is a declined fallback / coverage
reference only — not the shipping animations.** (Licensing is moot either way — the asset was purchased from
Artem; see below.)

## Key files

The **C# is built + wired + green** — see the "Implementation" section above for the file table. The remaining
files are data/assets:

| Component | Path | Status |
|-----------|------|--------|
| Mesh + skeleton tpac | `LOTRLOME_Armory/Assets/creature/elephant/mesh/elephant_harad_armor_01_geo.tpac` (+ textures) | **imported** (2026-06-05) |
| Monster XML | `…/LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_elephant.xml` (id `taom_war_elephant`) | **deployed** (2026-06-06) |
| Item XML | `…/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` (Horse item `taom_war_elephant`, mesh `elephant_mesh`) | **deployed** (2026-06-06) |
| Action set | `…/LOTRLOME_Armory/ModuleData/action_sets.xml` (`as_elephant` block appended; uses donor ids + clip names — pending rename to `as_war_elephant`) | **deployed** (2026-06-08); confirmed in-game |
| Animation clips | `LOTRLOME_Armory/Assets/creature/elephant/animations/` (35 donor tpacs — working; TAOM-authored clips pending) | **copied** (2026-06-06); animates in-game |
| Recruitment | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` (Harad pools) | TODO — using TEMP harad_militia entry |

## Reference: the upstream beasts pack

A shipped community beasts mod (wolves, wights, elephant) with `ADOD_Beasts.dll` + a `NativeHook.dll` +
`EasyHook.dll`. It is the working reference for the elephant. **Deep-dive verdict (`w21npmp7s`, 2026-06-05):**
the elephant is a standard Bannerlord mount driven entirely by data + managed C# — `OnTickAsAI` builds a
`Blow`/`RegisterBlow` (same mechanic as `WargAttackService`), a `CanAgentRideMount=false`/`MountDifficulty=999f`
GameModel locks out non-rider AI, and the attack component is attached in `OnAgentBuild`. **`NativeHook.dll` /
`EasyHook.dll` are dead** — a leftover `using`, zero call sites across the 5029-line decompile, no
`DependedModule` declaration. **TAOM ports no native code and ships neither DLL.** Structure is cloned from the
warg; data is re-authored from the donor mod; meshes/textures are our own FBX (purchased — see License below).

## License / provenance

The elephant asset was **purchased from Artem (the donor-mod author) for use in TAOM** — no clean-room re-derivation or
attribution gating required. (Confirmed by the project owner, 2026-06-05.)

## v1.4.6 exposure (2026-06-12) — jump table hardened; battle-test owed

Steam force-bumped the engine 1.4.5 → 1.4.6 on 2026-06-11; the spider campaign then proved
1.4.6's rewritten native usage/AI lookups **crash on missed keys** that 1.4.5 tolerated (full
story: [spider.md](./spider.md) "The v1.4.6 engine-bump campaign"; the distilled recipe:
[creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)). The elephant's
standing per the spider's three crash sites:

| Spider crash site | Elephant status |
|---|---|
| `CanAttack` → `Agent_ai::set_attack_entity` | **clean** — the elephant Monster never declared `CanAttack` (parity audit 2026-06-12) |
| jump-map miss (`monster_usage.cpp`) | **fixed proactively 2026-06-12** — `act_elephant_jump_start` was already `actt_dash`, but the jump rows were the same front+none-only template; expanded to the 45-row / 9-direction total table (same edit as the spider's) |
| mounted-death Die-path AV | **covered for spiders only** — Patch47 keys on `IsSpiderMonster`. If a mahout death on 1.4.6 reproduces the melee-death AV, generalize Patch47 to elephant mounts (one service predicate) |

**Owed:** an elephant battle on 1.4.6 (charge + melee + mahout deaths). Not fielded since the
bump as of 2026-06-12.

## Open items

- [x] Fold in the exact recipe + the donor code/NativeHook verdict from the deep-dive workflow. *(done 2026-06-05)*
- [x] Capture the donor's `elephant_skeleton` bone roster as the match reference (60 bones — see "Verified bone roster" above). *(done 2026-06-05)*
- [x] **Verify our FBX rig matches the donor's `elephant_skeleton` bone-for-bone** — DONE (2026-06-05, Blender diff:
      identical 60 bones, same order).
- [x] **Rename the armature → `elephant_skeleton` + body mesh → `elephant_mesh`, re-export `elephant_harad_armor_01.fbx`**
      — DONE (2026-06-05; verified 60 LimbNodes / 0 leaf bones / 11 meshes via `extract_fbx_bones.js`; original backed up).
      **Ready to import into the Modding Kit.** Remaining: re-confirm the rig in the Kit after the tpac compile.
- [x] Scaffold the `Main/Features/Elephant/` service core (clone of `Main/Features/Warg/`) — `IElephantAttackService`
      via the warg `CustomAttack` path + 19 unit tests. *(done 2026-06-05, build green)*
- [x] **C# trample + mission behavior + IoC/SubModule wiring** — DONE (2026-06-05, 1-for-1 donor port on v1.4.5, build green; 11 service tests).
- [x] **Mount-lock** (`CanAgentRideMount=false` + `MountDifficulty=999`) folded into `TaomAgentStatCalculateModel` — DONE (2026-06-05).
- [x] **Mesh + skeleton imported** to `LOTRLOME_Armory/Assets/creature/elephant/` — DONE (2026-06-05, by the project owner).
- [x] **Monster + Horse Item** (`taom_war_elephant`) deployed into LOTRLOME_Armory — DONE (2026-06-06). `lotr_monster_elephant.xml` + `taom_war_elephant` Item in `LOTRAOM_horses.xml` + SubModule.xml registrations.
- [x] **Action-set deployed self-contained** in LOTRLOME_Armory — DONE (2026-06-08). The donor's `as_elephant` / `as_elephant_town_and_village` / `as_elephant_map` merged into `LOTRLOME_Armory/ModuleData/action_sets.xml` (single `soln_action_sets` entry in `project.mbproj`). Two deployment crashes fixed (see "Action-sets deployment crash history" above).
- [x] **In-game battle smoke test** — CONFIRMED (2026-06-08). Multiple war elephants with Harad riders spawned, rendered, and fought correctly in battle. The upstream beasts pack NOT in load order.
- [ ] Revert **TEMP** `Horse`-slot entry in `Main/_Module/ModuleData/troops/troops_harad.xml` — marked `TEMP-ELEPHANT-TEST`, MUST revert before any commit.
- [ ] Author a **TAOM-owned action-set** `as_war_elephant` (rename from `as_elephant`, bind TAOM-authored clip names) to make the action-set fully TAOM-authored (currently uses donor ids / clip names verbatim).
- [ ] **Build the elephant animations on our FBX** (Blender → Modding-Kit compile) — TAOM-owned, NOT the donor's 1.2.12 clips. Gating seam for the rename to `as_war_elephant`.
- [x] Author the Harad rider troop + recruitment — DONE (2026-06-10). `harad_elephant_rider` (level 51, `Culture.aserai`, `HorseArcher`) recruitable ONLY by `clan_aserai_1` (Ayerikkä) via `VolunteerRecruitmentService.InitializeHaradClans` (clan pool copies the levy/noble fallback + adds the rider at weight 1). The TEMP `harad_militia` Horse-slot test entry was replaced by this dedicated troop. Remaining rider polish: not yet in any party template (AI Ayerikkä lords field it only when recruited); rider skills left at pre-level-51 values; recruitment weight is a rarity knob. Update `factions.json` if the war elephant becomes a Harad identity element.
- [x] Tune damage after in-game testing — DONE (2026-06-15): replaced the donor's fixed ~20 with TAOM per-kind randomized bands (trample 50-100, tusk 50-75, ×0.25 on shield block); gates unchanged. Also swapped the rider's primary spear (`eastern_spear_4_t4`) for a 2nd `bodkin_arrows_b` quiver so the mounted archer fires at ground targets instead of melee-swinging into air.
- [ ] **DEFERRED — re-enable howdah crew (slide source #1).** Crew spawn is disabled (`TrySpawnHowdahCrew` call
      commented in `ElephantMissionBehavior.TryInstantiateHowdah`). Re-enable with a crew↔elephant collision fix —
      candidate: give the crew the elephant's `FaceGroupId` via `Agent.SetAgentExcludeStateForFaceGroupId` (the
      engine's rider-vs-mount no-collision mechanism). See "Slide root-cause isolation".
- [ ] **DEFERRED — re-enable spine bone-tracking (slide source #2).** Disabled (`TryRepositionToBone` branch
      commented in `TaomHowdahMachine.RepositionToElephant`; fixed-offset only). Re-enable with a floor-physics fix —
      drop the `bo_empire_keep_a_door_top` collision (archers are teleported, don't need a physical floor) or raise
      the bone frame so the floor clears the elephant capsule. See "Slide root-cause isolation".
- [ ] **Fix the fixed-offset fallback (`RepositionToFixedOffset`)** — it places the howdah at the elephant's *legs*,
      not its back (surfaced during the slide ladder when bone-tracking was off). Harmless now (no crew), but it is
      the build-time + bone-failure safety path and must position correctly before crew are re-enabled.
- [ ] **Add physical barrier entities to `taom_howdah_agent.xml`** — all donor howdah variants include 4 `_barrier_04x04m`
      entities with `missile_only` body flag to physically wall archers inside the basket. Without them archers can be
      pushed/walk off the howdah. Match `adod_howdah_4_agents.xml` barrier layout.
- [ ] **Evaluate `TranslateUser = true`** — all donor howdah seats use `TranslateUser="true"` (physics-level frame
      translation by the base `StandingPoint.OnTick`). Our seats use `TranslateUser="false"` + custom `SetScriptedPosition`
      + `SetTargetZ`. If Z-snap issues persist with our approach, switching to the donor's pattern requires: (1) `TranslateUser="true"`
      on seat entities in `taom_howdah_agent.xml`, (2) calling `base.OnTick(dt)` at the end of `TaomHowdahStandingPoint.OnTick`.

---

## Changelog

- 2026-06-19 — War elephant (`harad_elephant_rider`) gated behind a special-resource recruit cost + per-day upkeep.
- 2026-06-15 — Bow-armed rider (spear → second bodkin quiver) + lethal per-kind randomized damage (trample 50-100, tusk 50-75); service tests 16 → 24.
- 2026-06-13 — Idle ear-fan authored + canter/gallop rebuilt as faster ambles from the refined walk (Blender-MCP).
- 2026-06-12 — Walk/run refined + full TAOM-owned locomotion set (trot/walk_backwards/turns/idle) authored on `elephant_skeleton` (Blender-MCP, source-side).
- 2026-06-10 — Behavior-tree-driven AI trample (warg pattern, phase 1) → cooldown-driven attack sequencing with directional tusk swings (BT phase 1.5); slide root-cause isolation; `TaomHowdahMachine._liveTicking` CS0414 fix.
- 2026-06-10 — War-elephant rider troop raised to level 51, recruitable only by `clan_aserai_1` (Ayerikkä).
- 2026-06-09 — Sealed-package howdah (force-spawned crew, 4 seats, dedicated rider); fixed howdah ground-level archers, ignored commands, and end-of-battle freeze.
- 2026-06-08 — Harad war elephant confirmed in-game; action-sets made self-contained in `LOTRLOME_Armory`; functional howdah seat via vanilla detachment.
- 2026-06-06 — First-pass idle + walk animations authored in Blender (via MCP); howdah crew mechanism documented + clip consolidation underway.
- 2026-06-05 — Harad war-elephant trample + mount-lock C# implemented (donor-mod behavioral port adapted to v1.4.5).

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)
- [docs/ai-includes/creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
- [docs/features/spider.md](./spider.md)
- [docs/features/volunteer-recruitment.md](./volunteer-recruitment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/adod-beasts-architecture-and-taom-port.md](../reference/adod-beasts-architecture-and-taom-port.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md)
- [docs/reviews/rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md)

<!-- backlinks-end -->
