# Bannerlord usable mission objects — MissionObject / UsableMachine / StandingPoint (Phase 12)

> **One process, traced from the decompile** (v1.4.5): the usable-mission-object hierarchy — how agents occupy +
> operate scene objects (siege engines, ladders, the howdah crew platform). Generalizes
> [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md) into the reusable engine pattern for any
> future TAOM usable machine. Part of the phased engine study; builds on Phase 8 (these ARE `ScriptComponentBehavior`s).

## WHAT it is

A family of scene objects an agent can *use* (stand on, operate, fire). The hierarchy (each a richer kind of the
previous):
```
ScriptComponentBehavior            (Phase 8 — engine-discovered script on a prefab entity)
  └─ MissionObject                 (a mission-scoped script object; MissionObject.cs:10 — abstract : ScriptComponentBehavior)
       └─ SynchedMissionObject     (network-synched state)
            └─ UsableMissionObject (a single USABLE point — OnUse, a MovingAgent, lockable frames)
                 ├─ StandingPoint  (a user position; + subtypes)            StandingPoint.cs:11 — : UsableMissionObject
                 └─ UsableMachine  (a MACHINE grouping StandingPoints)      UsableMachine.cs:13 — : SynchedMissionObject, IFocusable, IOrderable, IDetachment
                      └─ SiegeWeapon (ballista/mangonel/ram/tower)          SiegeWeapon.cs:11 — : UsableMachine, ITargetable
```

## HOW it works

### `MissionObject : ScriptComponentBehavior` (MissionObject.cs:10)
**A mission object IS a scene script** (Phase 8) — attached to a prefab entity via `<script name="X">`, with
`[EditableScriptComponentVariable]` config, `OnInit`/`OnTick` driven by the engine. So everything in Phase 8 applies
(engine-discovery by class name; editable vars = config-must-validate). `SynchedMissionObject` adds MP state sync.

### `UsableMissionObject` — a single usable point
The base for "an agent uses this." Key surface (per the howdah research): **`OnUse(Agent userAgent, sbyte
agentBoneIndex)`** (UsableMissionObject.cs:360 — **2 params** in v1.4.5), `OnUseStopped(Agent, bool, int)`,
`MovingAgent` (the occupant), `AddMovingAgent`/`RemoveMovingAgent`, `LockUserFrames`/`LockUserPositions` (pin the user
to the object's frame), `IsDeactivated`/`SetDisabled`.

### `StandingPoint : UsableMissionObject` (StandingPoint.cs:11) — a user position
One slot an agent stands in/at. Subtypes constrain who/how:
`StandingPointWithTeamLimit`, `StandingPointWithAgentLimit`, `StandingPointForRangedArea`,
`StandingPointWithWeaponRequirement`, `StandingPointWithVolumeBox`. (A howdah seat is a `StandingPoint` subclass.)

### `UsableMachine : SynchedMissionObject, IFocusable, IOrderable, IDetachment` (UsableMachine.cs:13) — a machine
Groups several `StandingPoint`s into one operable machine. In `OnInit` it **auto-collects** its `StandingPoint`
children via `CollectScriptComponentsIncludingChildrenRecursive<StandingPoint>()` into **`StandingPoints`**
(UsableMachine.cs:55); `MaxUserCount => StandingPoints.Count`. Implements:
- **`IDetachment`** — the AI treats the machine as a "detachment" agents can be assigned to (the
  `UsableMachineAIBase`, UsableMachineAIBase.cs:12, drives agents to path-to + occupy the points).
- **`IOrderable`** — the player can order a formation to use it.
- **`IFocusable`** — the player can focus/interact with it.
- Virtuals: `GetActionTextForStandingPoint(UsableMissionObject)`, `GetDescriptionText(WeakGameEntity)` (→`TextObject`),
  `GetTickRequirement`, `OnMissionEnded`, `GetDetachmentWeightAux`.

### How agents come to use it
Two paths: (a) **AI** — `UsableMachineAIBase` scores + assigns agents to the machine's standing points (they path to
and `OnUse`); (b) **forced** — code spawns/teleports an agent onto a point and calls `UseGameObject`+`AddMovingAgent`+
`Formation=null` (the howdah's force-spawn crew). A `StandingPoint` keeps its occupant pinned each tick via the
machine's frame + `SetPosition`/`LockUserFrames`.

## WHY it's shaped this way

Layering `StandingPoint`(s) under a `UsableMachine` lets one machine (a ballista, a siege tower, a ship deck, a
howdah) expose N crew positions with shared operate-logic, while `IDetachment`/`IOrderable` plug it into the existing
formation-AI + order systems for free. Because they're `ScriptComponentBehavior`s, map authors place + configure them
in the Kit with no code.

## TAOM relevance + gotchas
- **The howdah** (`ADODHowdah : UsableMachine`, `ADODHowdahStandingPoint : StandingPoint`) is the worked example —
  full line-level port spec + the moving-machine "manual per-tick frame-copy to the elephant neck" pattern are in
  [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md). Any future TAOM usable machine (deployable,
  siege-defense object, ridden platform) follows this hierarchy.
- **v1.4.5 drifts** (the upstream pack is 1.2.12 — confirmed): `OnUse(Agent)` → **`OnUse(Agent, sbyte agentBoneIndex)`**
  (UsableMissionObject.cs:360 — add the param or the override is dead); `MissionObject.SetDisabled(bool)` — the bool is
  **`isParentObject`**, the call **always disables** (no toggle); `UsableMachine.GetDescriptionText` →
  `(WeakGameEntity)→TextObject` (not `(GameEntity)→string`).
- **It's a `ScriptComponentBehavior`** (Phase 8) — engine-discovered by class name; `[EditableScriptComponentVariable]`
  fields are **config-must-validate** (NaN/range); prefab `<script name>` must match the class.
- **Moving machine** (howdah/elephant) needs the manual per-tick frame copy (read the carrier's bone frame →
  `SetFrame`); vanilla machines are static or engine-moved (siege tower). The seats re-pin occupants each tick.
- **Vanilla templates to copy:** `SiegeWeapon` subclasses (ballista/mangonel/ram/tower) — decompile the closest one
  for the operate/crew pattern when authoring a TAOM machine.

## The native boundary
`UsableMachine`/`StandingPoint` are **managed** `ScriptComponentBehavior`s (the operate-logic + AI assignment are
C#), running on `GameEntity`s whose transforms/physics are native (Phase 8). The user-pinning calls (`SetPosition`,
`SetFrame`) cross to native; the orchestration is managed.

## Evidence (file:line, v1.4.5)
- `MissionObject.cs`:10 (`abstract : ScriptComponentBehavior`); `UsableMissionObject.cs`:360 (`OnUse(Agent, sbyte)`); `StandingPoint.cs`:11 (`: UsableMissionObject`) + subtypes (`StandingPointWith{TeamLimit,AgentLimit,WeaponRequirement,VolumeBox}`, `StandingPointForRangedArea`).
- `UsableMachine.cs`:13 (`: SynchedMissionObject, IFocusable, IOrderable, IDetachment`), 55 (`StandingPoints` MBList); `UsableMachineAIBase.cs`:12 (the AI driver); `SiegeWeapon.cs`:11 (`: UsableMachine, ITargetable` — vanilla template).
- Worked example + drift table: [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
