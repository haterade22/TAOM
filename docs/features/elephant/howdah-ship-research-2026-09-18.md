# Howdah archers: what War Sails teaches, and the recommendation (2026-09-18)

Written for: whoever resumes the howdah crew feature (the elephant's archers, deferred since 2026-06-10).

**Question (Mike).** The ADOD_Beasts howdah (Artem's) is a platform re-framed onto the elephant every tick, with four
seat markers (watermelon meshes) that archers are locked to. Bannerlord's War Sails DLC puts troops on moving ships
that walk, fight and board. How do ships do it, and what should TAOM build?

**Constraint.** TAOM does not and will not require the DLC. NavalDLC was read as a design reference only
(`comparison-only`); every mechanism below is either base-game engine API or TAOM's own code and assets. Where a
base-game precedent exists (the siege towers in `Native`), it is cited instead of the ship.

Sources: four research passes on 2026-09-18 over `E:\Decompiled_Bannerlord\_modules_build\NavalDLC__NavalDLC.cs`, the
v1.4.8 and v1.5.3 base decompiles, the installed 1.5.3 DLLs (`taom-src`), `NavalDLC\AssetPackages\nested_prefabs_packed.xml`,
`Native\Prefabs` and `NavMeshPrefabs`, and TAOM's howdah code and prefab. The load-bearing engine facts were
re-checked on the installed DLLs before this was written. Anything not read in code is marked UNVERIFIED.

## How a ship carries its crew

| Piece | How the ship does it | Base-game or DLC |
|---|---|---|
| Something to stand on | The hull is a native dynamic rigid body; the crew stand on a child deck body flagged `moveable` (kinematic), not on the hull | Physics flags: base |
| Walkable surface | A navmesh prefab (`NavMeshPrefabName=southern_fishingship_navmesh`) imported and attached to the entity by `MissionObject.AttachDynamicNavmeshToEntity`; the deck is a separate navmesh island that moves with the ship | Base: `MissionObject` (`NavMeshPrefabName`, `DynamicNavmeshIdStart`), `Scene.ImportNavigationMeshPrefab`, `WeakGameEntity.AttachNavigationMeshFaces`, all unchanged since 1.4.8. Vanilla siege towers use the same calls |
| Where crew stand | Prefab-authored frames tagged `sp_troop_outer_deck` / `sp_troop_inner_deck` (bare entities, no script, no physics). Crew are spawned at the frame's current world position with `Mission.SpawnTroop` | Spawn call: base. Frames: plain prefab entities |
| Keeping them there | Fighting crew are NOT machine users. They sit in a detachment (`ShipPlacementDetachment : IDetachment`) whose slots are stored in ship-local space and recomposed to world space every call; normal locomotion walks them to the slot | `IDetachment`: base. The detachment class is DLC logic, reimplementable |
| Machines (oars, ballista, rudder) | `StandingPoint`s, children of the hull; the base class re-targets the user from the object's live frame (`SetTargetPositionAndDirection`), no teleport | Base: `UsableMissionObject` |
| Shooting from a moving deck | No motion compensation anywhere; naval battles only apply a flat accuracy penalty in their agent-stat model | Stat model: DLC numbers, base mechanism |
| Not shoving the ship | The hull is not an agent. No managed API excludes one agent's capsule from another's; `Agent.SetAgentExcludeStateForFaceGroupId` is a navmesh pathing exclusion (13 base callers, all navmesh ids) | n/a |
| Battle end | Crew in a detachment and machine users released by `UsableMachine.OnMissionEnded`, which fires the moment the mission ends, before any exit sequence | Base |

**The one gap in the analogy.** A ship's deck sits on water; a howdah sits on another agent, whose own collision
capsule is right underneath. Nothing on a ship has to stay clear of an agent capsule. That is the howdah's own
problem, and it is geometry: the elephant's body capsule now tops out at 2.7 m and the howdah floor sits at 3.2 m.

## What is wrong with TAOM's howdah today (measured)

From `Main/_Module/Prefabs/taom_howdah_agent.xml` (identical to the installed copy) and the C#:

1. **No physics body is `moveable`**, yet the entity is re-framed every tick (floor lines 23-27, walls 150-154).
   Every vanilla moving platform flags every body `moveable`: the siege tower's root, deck, ramp and rails, and the
   ship's deck and edge strips (`props_siege_siegetowers.xml:9-12, 69-72`). What the engine does with a static
   body moved every tick is UNVERIFIED, and it is the leading candidate for the slide: both slide sources placed a
   physics body where the elephant is.
2. **The walls are 1 m tall, not 20 m.** `bo_barrier` is a zero-thickness 1 x 1 m plane; the prefab scales its zero
   dimension by 20, which does nothing. Vanilla scales it (width, 1, height).
3. **The walls carry no `barrier` or `ai_limiter` flag**, so they are solid to everything, the crew's own arrows
   included (read from the flag names; the missile mask is UNVERIFIED).
4. **The seats sit inside the floor slab**: seat frames at z 0.273, slab top at z 0.32.
5. **Archers are teleported to the seat every tick** (`TaomHowdahStandingPoint.OnTick`, `TeleportToPosition`), their
   formation is set to null, and seats are released from the LATE `OnEndMission` hook plus a poll. Both 2026-06-09
   end-of-battle freezes came from exactly this area.
6. `NavMeshPrefabName` is empty everywhere.

## Recommendation

**Build the ship model, in four gated steps, and keep the seat model as the fallback.** Each step changes one thing
and has a pass/fail test in one Custom Battle, so a failure points at its cause.

### Step 1: make the platform a proper moving body (prefab only, no C#)

Add `moveable` to the floor and wall bodies; rebuild the walls as `bo_barrier` scaled (width, 1, ~1.2) flagged
`barrier|moveable` so agents are held and arrows pass; lift the seats above the slab. Then re-enable ONE deferred
slide source at a time (bone tracking first: it put the floor at the spine). **Pass:** the elephant walks and turns
without sliding with that source on. This settles the slide mechanism for the price of an XML edit. If it passes,
bone tracking (the visually correct placement) comes back.

### Step 2: release the crew the way the engine expects (small C#)

Override `OnMissionEnded` on `TaomHowdahMachine` (immediate, the hook every vanilla machine uses) and release through
`StopUsingGameObject`; stop nulling the archers' formation. **Pass:** ten battles won and lost with crew aboard end
without a freeze, and the crew leave with their formation.

### Step 3: a walkable deck (the ship model)

- **Asset:** `Main/_Module/NavMeshPrefabs/taom_howdah_navmesh.bin`, one walkable face group relative to the howdah
  root, exported from the Kit ("Export faces as navigation mesh prefab"; TAOM_Map's `Gondor_Mesh.bin` proves the 1.5.x
  export works here). Name it `taom_`-prefixed: navmesh prefab names are global across modules (UNVERIFIED which wins).
- **Wiring:** `NavMeshPrefabName` on `TaomHowdahMachine`. The base attach expects the siege layout (groups 1 inside,
  2 enter, 3 exit, 4 blocker); a howdah has no enter or exit, so the machine likely needs a ship-style override of
  `AttachDynamicNavmeshToEntity` (`/research` before writing it).
- **Crew:** spawn at four TAOM-tagged deck frames with `Mission.SpawnTroop` and hold them with a TAOM
  `IDetachment` (howdah-local slots recomposed each call), not a `StandingPoint`: seat users get the engine's
  no-attack scripted flag, which is why TAOM calls `OnUse` directly today.
- **Pass:** four archers stay aboard while the elephant walks and turns, shoot at enemies, and the elephant does not
  slide. **Watch:** whether the attached navmesh follows a `SetFrame`-driven entity every tick. The vanilla tower
  never carries agents while moving and the ship moves by native physics, so this is the one untested link; if the
  deck lags, call `UpdateAttachedNavigationMeshFaces()` in the reposition tick, and move the howdah with
  `SetFrame(..., isTeleportation: false)` (TAOM uses the default `true` today, the opposite of the siege tower).

### Step 4: tuning

Optional: a flat accuracy penalty for howdah archers through TAOM's own agent-stat model (TAOM's numbers, not the
DLC's). Four archers per elephant, as ADOD_Beasts ships.

**Fallback.** If Step 3's deck will not follow the elephant, keep today's seat model with Steps 1 and 2 applied:
moveable bodies, immediate release, formation kept. That already answers the two failures the feature was parked for.

## What this does not settle

- Whether the physics engine carries an agent standing on a kinematic body moved by `SetFrame` (ships use native
  physics; UNVERIFIED for a scripted frame).
- Whether a navmesh island with no body under it is usable (every vanilla example keeps one).
- The Kit's exact steps for assigning face groups on export.

Per-pass notes with citations (scratch, not committed): `1-crew-on-deck.md`, `2-engine-platform-apis.md`,
`3-machines-and-combat.md`, `4-ship-assets.md`. The mechanism TAOM ported first:
[howdah-crew-mechanism.md](howdah-crew-mechanism.md).
