# How ADOD puts a CREW on a moving elephant — the howdah mechanism (+ v1.4.5 portability)

> Research findings (2026-06-06) from decompiling `ADOD_Beasts.dll` + verifying every TaleWorlds API against
> the installed **v1.4.5** DLLs (`taom-src`). Answers the question: *"how does ADOD get multiple AI troops on the
> back of the elephant?"* ADOD_Beasts is built for ~1.2.12, so this also records the **API drift** a TAOM port
> must fix. Source workflow: `wzr1vofux` (3 agents: howdah, wiring, v1.4.5-verify).

## TL;DR

The crew is a **howdah** = a Bannerlord **`UsableMachine`** (like a ballista/siege-tower crew platform) whose
**`StandingPoint` children are the seats**. It is **not** parented to the elephant — it's a free scene entity
whose world-frame is **manually copied onto the elephant's neck point every tick**. The seats **force-spawn**
their own AI ranged crew (they don't path in), lock them to the seat, and make them shoot. The mahout (the troop
riding the elephant) is a **separate** normal mount-rider, not a howdah seat.

## The mechanism, step by step

1. **The elephant enters battle as a vanilla MOUNT.** A normal troop (`volantene_elephant_rider_tN`,
   `default_group=HorseArcher`) carries `Horse=Item.elephant` + `HorseHarness=Item.volantene_elephant_armor_tier_N`.
   The stock TaleWorlds reinforcement pipeline spawns it mounted — ADOD adds nothing here. An elephant appears in
   a random battle *only because that troop is in a party roster.*

2. **`ADODBeastsMissionLogic.OnAgentBuild(agent, banner)`** fires per agent. When
   `agent.IsHuman && agent.HasMount && IsElephant(agent.MountAgent)` **and** the rider's
   `Equipment[EquipmentIndex.HorseHarness (11)]` armor id is a key in `ElephantCharacters.ArmorToCharacterMap`, it:
   - `GameEntity.Instantiate(Mission.Current.Scene, "<prefab>", true)` — prefab chosen by a switch on the armor tier
     (`tier_2→adod_howdah_1_agent`, `tier_3→adod_howdah_2_agent`, `tier_4→adod_howdah_4_agent`);
   - `SetVisibilityExcludeParents(true)`;
   - sets `GetFirstScriptOfType<ADODHowdahObject>().elephantAgent = agent.MountAgent` and `.elephantRider = agent`;
   - attaches `ADODBeastsElephantAgentComponent` to the elephant (the trample AI — **separate** from the howdah).

3. **The howdah is a `UsableMachine`.** `ADODHowdahObject : ADODHowdah : UsableMachine`. The prefab's root
   `<game_entity>` carries `<script name="ADODHowdahObject">`; each **seat** is a child `<game_entity>` carrying
   `<script name="ADODHowdahStandingPoint">` (`: StandingPoint`). Base `UsableMachine.OnInit` auto-collects the seat
   scripts into `MBList<StandingPoint> StandingPoints` via `CollectScriptComponentsIncludingChildrenRecursive<StandingPoint>()`,
   and `MaxUserCount => StandingPoints.Count`. So **seat count = number of StandingPoint children declared in the prefab**
   (not a code array). Prefabs also carry `_barrier_04x04m` physics walls (railings) + `foods_watermelon_a` meshes as
   editor-only seat markers.

4. **Attach = per-tick manual frame copy** (NOT bone-parenting, NOT a child of the elephant entity).
   `ADODHowdah.OnTick → UpdateHowdahMovement(dt)` each frame:
   ```
   MatrixFrame frame = elephantAgent.Frame;                                  // elephant world transform
   Vec3 neck = elephantAgent.AgentVisuals.GetGlobalStableNeckPoint(true);    // smoothed neck/withers world point
   frame.origin = neck + (-0.3f * frame.rotation.f) + new Vec3(0,0,0.2f);    // slightly behind + above the neck
   GameEntity.SetFrame(ref frame);                                           // move the whole howdah root
   ```
   The seats are children of the howdah root, so they ride along; each seat then re-snaps its occupant. Both classes
   OR `TickOccasionally/Parallel` into `GetTickRequirement()` so the follow is guaranteed each tick.

5. **Crew are FORCE-SPAWNED by each seat** (not formation troops pathing to the machine).
   `ADODHowdahStandingPoint.OnTick`, when the seat is enabled + empty + `elephantRider` set:
   reads `elephantRider.Character.Equipment[11]` armor id → `ElephantCharacters.ArmorToCharacterMap` →
   `(crewCharacterId, count)` → `MBObjectManager.GetObject<CharacterObject>` →
   `AgentBuildData(... Team=rider.Team, Controller=AI, NoHorses(true), IsReinforcement(true),
   TroopOrigin=PartyAgentOrigin(rider.Origin.BattleCombatant,...))` → `Mission.Current.SpawnAgent` →
   `TeleportToPosition(seat.GlobalPosition)` → `UseGameObject(this,-1)` + `AddMovingAgent` + `Formation = null`.
   Then every tick it re-pins them: `SetPosition(seat.GlobalPosition)` + `SetOnLandState(NotOnLand)` +
   `SetActionChannel(0, "act_howdah_stand_bow")` + refills ranged ammo. **Crew = ranged troops that stand and shoot
   from the moving platform.**

6. **Teardown** (per-mission; instantiated fresh each battle): when a seat is disabled it restores the crew's
   `previousFormation`, `DisableScriptedMovement`, `SetOnLandState(Falling)`, and `GameEntity.Parent.Remove(0)`
   removes the howdah child. At mission end the elephant refs are nulled.

## Crew capacity (by elephant-armor tier)

| Harness armor | Prefab | Seats | Crew (from `additional_elephant_characters.xml`) |
|---|---|---|---|
| `volantene_elephant_armor_tier_2` | `adod_howdah_1_agent` | 1 | 1× `brotherhood_of_woods_tier_1` |
| `volantene_elephant_armor_tier_3` | `adod_howdah_2_agent` | 2 | 2× `brotherhood_of_woods_tier_2` |
| `volantene_elephant_armor_tier_4` | `adod_howdah_4_agent` | 4 | 4× `brotherhood_of_woods_tier_3` |

Top-tier manned strength = **1 mahout (mount rider) + 4 howdah archers.** A `CharacterObject.GetPower` Harmony
postfix inflates the rider troop's strength so the campaign AI values it.

## v1.4.5 portability — buildable, but ADOD's 1.2.12 code has 4 drifts to fix

The mechanism **ports to 1.4.5** — `UsableMachine`, `StandingPoint`, `GameEntity.Instantiate`, `SpawnAgent`,
`AgentVisuals.GetGlobalStableNeckPoint`, `Frame`/`SetFrame`, `AddMovingAgent`, etc. are all confirmed present with
matching signatures. But a verbatim port would silently fail on these (verified against `taom-src` v1.4.5):

| # | Sev | API | ADOD (1.2.12) | v1.4.5 | Fix for a TAOM port |
|---|-----|-----|---------------|--------|---------------------|
| 1 | HIGH | `AgentComponent.OnTickAsAI(float)` | overridden for trample/crew AI | **does not exist** (only `OnTick`/`OnTickParallel`) | move logic to `OnTick`/`OnTickParallel`. **TAOM's elephant already did this** — `ElephantMissionBehavior : MissionLogic.OnMissionTick`, not an AgentComponent. |
| 2 | HIGH | `StandingPoint.OnUse(Agent)` | 1-param override | `OnUse(Agent, sbyte agentBoneIndex)` (StandingPoint.cs:269) | port to the **2-param** override or seat-entry is dead (ADOD limps via the OnTick self-spawn instead). |
| 3 | HIGH | `MissionObject.SetDisabled(bool)` | used as enable/disable toggle | bool is `isParentObject`; call **always disables** (MissionObject.cs:261) | re-architect seat enable/disable — `SetDisabled(false)` does NOT re-enable. |
| 4 | MED | `UsableMachine.GetDescriptionText` | `string GetDescriptionText(GameEntity=null)` | `abstract TextObject GetDescriptionText(WeakGameEntity)` (UsableMachine.cs:1173) | change return type + param or it won't satisfy the abstract (compile error). |

## What TAOM would need to build a howdah (gaps)

1. **3 engine-discovered script classes** (`ScriptComponentBehavior`/`UsableMachine`/`StandingPoint` subclasses):
   a `Howdah : UsableMachine` (per-tick neck-frame follow), a `HowdahStandingPoint : StandingPoint` (fighting seat
   with force-spawn + lock + `SetActionChannel`), and a thin `HowdahObject : Howdah` for the prefab `<script name>`.
2. **Howdah prefabs** (`Prefabs/*.xml`) with N `HowdahStandingPoint` child entities + railing physics. LOTR-themed.
3. **An `OnAgentBuild` hook** (in the existing `ElephantMissionBehavior` or a sibling) that instantiates the
   tier-appropriate prefab per elephant-rider and wires `elephantAgent`/`elephantRider`.
4. **Armor→crew→count config** — a TAOM-native equivalent of `additional_elephant_characters.xml` (which LOTR crew
   troop, how many seats per tier). Route the decision through a service + adapter per ADR-007/002.
5. **A stand-and-shoot action** — `act_howdah_stand_bow` + its clip are ADOD assets; TAOM needs its own action-set
   entry / anim (or to reuse a vanilla "ranged from standing point" action).
6. **Apply the 4 drift fixes above** from the start.

## Relationship to TAOM's current elephant

TAOM's elephant today is **mount + trample + mount-lock only** — no crew platform. The trample (`ElephantMissionBehavior`)
already runs on a live `MissionLogic.OnMissionTick` (drift #1 pre-fixed). Adding a howdah is a **new, self-contained
sub-feature** layered on top; it does not change the existing trample/mount-lock. It is the bigger lift of the two
(3 engine classes + prefabs + crew config + 4 drift fixes), so sequence it **after** the single-rider trample is
confirmed in-game.
