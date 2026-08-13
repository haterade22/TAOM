# Bannerlord mount / rider runtime — Agent.Mount / MountAgent / RiderSitBone (Phase 14)

> **One process, traced from the decompile** (v1.4.5): how a rider and a mount become two linked agents at runtime —
> the in-mission half of Phase 10's `HorseComponent` + Phase 3's `Monster`. Closes the creature picture: a TAOM
> **cavalry trooper** rides a horse-agent; the **elephant** is a ridden mount whose *crew* sit on a UsableMachine
> (Phase 12), not via `Mount`; the **new spider** is a *riderless* combatant (`Mountable="false"`). Part of the phased
> engine study.

## WHAT it is

A mount is **its own `Agent`** (spawned from the `Equipment[Horse]` item's `HorseComponent.Monster` via the
`FromHorseObj` path — Phase 1/10). A rider is **another `Agent`**. At runtime the two are linked: the rider
**`MountAgent`** points at the mount, the mount **`RiderAgent`** points back, and the rider is visually attached at the
mount Monster's **`RiderSitBoneIndex`**. The link is requested in managed code (`Agent.Mount`) and executed by native.

## HOW it works

### The Monster side (mount data — Monster.cs, Phase 3)
- **`RiderSitBoneIndex`** (Monster.cs:168) — from `rider_sit_bone` (deserialized :551 with
  **`validateHasParentBone: false`** — lenient, won't reject on a missing parent). **The rider is seated at this bone.**
- **Rein bones** (Monster.cs:152-184) — `ReinHandle*`/`ReinCollision*`/`ReinHead*`/`Rein{Left,Right}Hand*BoneIndex` +
  `ReinSkeleton`/`ReinCollisionBody`: the reins mesh + physics. Resolved **by name** → `-1` if absent (Phase 3 trap).
- **`MonsterUsage`** (:44, e.g. `"horse"`/`"elephant"`/`"camel"`) — selects the action set + AI behaviors;
  **`FamilyType`** (:86) — groups monsters (all horses one family, humans another) for AI/interaction logic.
- The **`Mountable`** XML flag becomes the agent's **`AgentFlag.Mountable`** (see `IsMount` below).

### The Agent side (runtime link — Agent.cs)
- **`IsMount => (GetAgentFlags() & AgentFlag.Mountable) != 0`** (Agent.cs:642) — *can this agent be ridden?*
- **`MountAgent`** (:1001) — the rider's mount (`get` = native `GetMountAgentAux`; `private set` = native
  `SetMountAgent` + `UpdateAgentStats`). **`RiderAgent`** (:718, native `GetRiderAgentAux`). **`HasMount => MountAgent
  != null`** (:720).
- **`Mount(Agent mountAgent)`** (:4787) — the **two-phase** request:
  ```
  if (MountAgent == null && mountAgent.RiderAgent == null
      && CheckSkillForMounting(mountAgent) && !rearing && GetCurrentAction == act_none)
      EventControlFlags |= EventControlFlag.Mount;  SetInteractionAgent(mountAgent);   // → native completes NEXT tick
  else if (MountAgent == mountAgent && !rearing)
      EventControlFlags |= EventControlFlag.Dismount;                                   // same call dismounts
  ```
  Managed code only **flags intent** (`EventControlFlag.Mount`/`Dismount`); the engine performs the actual attach
  (rider → `RiderSitBoneIndex`, reins → Rein bones) **on the next tick** in native.
- Mounting changes the rider's stats → the setter calls `UpdateAgentStats()` (Phase 15 territory).

### Runtime flow (cavalry)
```
spawn: trooper Agent (FromCharacterObj) + horse Agent (FromHorseObj from Equipment[Horse].HorseComponent.Monster)
  → trooper.Mount(horse)  → EventControlFlag.Mount  → (next native tick) trooper seated at horse.RiderSitBoneIndex, reins wired
  → trooper.MountAgent == horse, horse.RiderAgent == trooper, UpdateAgentStats (mounted speed/charge/etc.)
```

## WHY it's shaped this way

Modeling the mount as a **separate agent** (not a property of the rider) lets the horse have its own skeleton,
animations, physics, HP, and AI (it can be killed, panic, wander riderless) while the rider is just *attached* to a
bone on it. The two-phase `Mount` (flag → native next-tick) keeps the actual skeletal attach + reins physics in the
engine where the animation/physics state lives, so managed code never has to manipulate bones directly.

## TAOM relevance + gotchas
- **Cavalry** (every mounted TAOM troop): trooper + horse agent linked via `Mount`; the horse item is
  `Equipment[Horse=ArmorItemEndSlot]` (Phase 10) → `HorseComponent.Monster` (Phase 3). The career starting-equipment
  `FillFrom` merge **must give non-cavalry archetypes empty `Horse`/`HorseHarness`** or they spawn mounted (CLAUDE.md).
- **Elephant** (ADOD_Beasts port): a **ridden mount** (the mahout rides it via the normal `Mount` path) **plus** a howdah
  **crew that sit on `UsableMachine` `StandingPoint`s — NOT via `Mount`** (Phase 12 / howdah-crew-mechanism.md). Don't
  conflate the two seating mechanisms: one rider via `RiderSitBoneIndex`, N crew via standing points.
- **New spider** (riderless combatant): spawned `FromHorseObj` but **`Mountable="false"` → `IsMount == false`** → it
  can't be mounted, has no `RiderAgent`, and its Monster **drops `rider_sit_bone`**. It *is* the agent — there is no
  rider. (The abandoned design rode a humanoid anchor on the spider → the AddSkinMeshes/native-AV crash; the riderless
  design avoids the rider entirely — see agent-spawn-and-render-pipeline.md + adod-beasts-architecture-and-taom-port.md.)
- **Bone-by-name** (Phase 3): `RiderSitBoneIndex`/`Rein*BoneIndex` resolve by name → `-1` on a typo/missing bone; a
  `-1` sit bone seats the rider at the origin (visible "rider floating at feet"). `rider_sit_bone` is *lenient*
  (`validateHasParentBone:false`) so a bad parent won't error — it silently mis-seats.
- **`MonsterUsage`/`FamilyType`** drive the action set + AI grouping; a creature mount needs a `monster_usage` whose
  action set exists (the elephant reuses `as_elephant`/`monster_usage="elephant"`; the spider needs its own).
- **Two-phase mount is async** — after calling `Mount`, `MountAgent` is **not** set until the next tick; don't read it
  in the same frame.

## The native boundary
**Managed:** the `Mount`/`Dismount` *request* (`EventControlFlag`), and all Monster/Equipment *data*. **Native:**
`GetMountAgentAux`/`GetRiderAgentAux`/`SetMountAgent`, the actual skeletal attach of rider → `RiderSitBoneIndex`, the
reins mesh + physics, and the per-tick mounted movement. So *who rides what* is decided in managed code; *the rider
actually sitting on the bone* is native — which is why `Mount` only flags intent and the link appears a tick later.

## Evidence (file:line, v1.4.5)
- `Agent.cs`:642 (`IsMount`=`AgentFlag.Mountable`), :718 (`RiderAgent`), :720 (`HasMount`), :1001-1011 (`MountAgent` get=`GetMountAgentAux`, set=`SetMountAgent`+`UpdateAgentStats`), :4787-4802 (`Mount` two-phase via `EventControlFlag.Mount`/`Dismount`+`SetInteractionAgent`).
- `Monster.cs`:168 (`RiderSitBoneIndex`), :551 (`rider_sit_bone` deserialize, `validateHasParentBone:false`), :152-184 (Rein bones), :44 (`MonsterUsage`), :86 (`FamilyType`).
- Linked phases: agent-spawn-and-render-pipeline.md (Phase 1, FromHorseObj), monster-model.md (Phase 3, bone-by-name), item-equipment-model.md (Phase 10, `Equipment[Horse]`/`HorseComponent`), usable-machines.md (Phase 12, howdah crew), [howdah-crew-mechanism.md](../../features/elephant/howdah-crew-mechanism.md), [adod-beasts-architecture-and-taom-port.md](../adod-beasts-architecture-and-taom-port.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-mount-authoring.md](../../ai-includes/creature-mount-authoring.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)

<!-- backlinks-end -->
