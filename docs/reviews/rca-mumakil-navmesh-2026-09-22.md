# RCA: why the Mumakil's crew fell off its tower, and what held them up (#627)

**Scope.** Two days of in-game diagnosis, 2026-09-21 and 2026-09-22, on the crew platform built the day
before. The feature shipped structurally complete, with 15 tests green and a clean review, and did not work
at all: the archers spawned onto their seats and dropped 9 m to the ground. By the end all eight draw and
loose. This is the record of what the difference was, because almost none of it was visible from the code.

**Outcome.** Eight of eight archers confirmed firing in game. The mechanism is navmesh plus physics plus a
pinned speed plus frame placement, and the design the feature shipped with (runtime-scaled entity, teleport-held
agents) was wrong in a way no test could have caught.

## The headline

`Agent.TeleportToPosition` is a bare native `SetPosition` with no ground snapping. Read the decompile and it
looks like it must work. **At the elephant's 3.2 m it does. At the mumakil's 9 m it does not**, and the failure
is total: correct x and y to the centimetre, all the z gone, 25,000 teleports a battle not holding a single
archer up.

The engine's own answer was one grep away and nobody had run it. `NavMeshPrefabName` appears in exactly two
classes in v1.5.3, `SiegeTower` and `MissionShip`, and **neither contains a single call that moves an agent**.
No `TeleportToPosition`, no `SetScriptedPosition`. They import a navmesh prefab and attach its faces to their
own entity so the walkable surface rides along, and the agents simply stand on it.

So the feature was built on a mechanism the engine does not use for this, which happened to survive at low
altitude on the reference implementation.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | CRITICAL | No navmesh: every archer fell 9 m and no teleport held it | The howdah works at 3.2 m with the same code and no navmesh, so the mechanism looked proven. Height was never a variable anyone was tracking | `NavMeshPrefabName` on the prefab, `AttachDynamicNavmeshToEntity` overridden to attach face group 1 |
| 2 | CRITICAL | Removing the floor slabs, with the navmesh in place, dropped everyone again | I had assumed navmesh replaced physics. It does not: navmesh makes a position valid, a body is what you stand on | Slabs kept, squashed to 3 cm |
| 3 | HIGH | The floor body is a door lintel whose raised ends stood 70 cm proud at the platform's z-scale. Four archers stood on the ends, 70 cm above their navmesh, snapped down seven times a second | The body was chosen for its footprint; nobody asked what its cross-section looked like once scaled | z-scale 0.316 to 0.050, tops unchanged. Ends now 4 cm |
| 4 | HIGH | Navmesh islands 0.37 to 0.65 m across against a 0.74 m body. Three archers pinned at a constant 0.57 / 1.04 m above their seats, unmoved by 7,619 teleports | **My briefing error.** I specified small islands to stop wandering, having already solved wandering another way, and never checked the number against a body width | Eight 1.6 m squares, 0.43 m of margin per side |
| 5 | HIGH | Frames spread to the deck edges for maximum separation, so bodies overhung the deck | Separation was treated as the objective because it was the rule we already had. Footing was not a rule yet | All frames a body radius plus margin inside their deck |
| 6 | MED | With a navmesh finally under them, the archers walked up to 7.06 m | Nothing had ever needed to stop them: without navmesh they physically could not move | `SetMaximumSpeedLimit(0f)` at `OnUse`, cleared on release |
| 7 | MED | Applying that speed limit on the 0.5 s stance clock matched the draw dying at 0.85 on all eight seats in lockstep | It was added to the re-assert path by symmetry with the AI curves, without asking whether a movement-limit reset mid-draw aborts the draw | Applied once at `OnUse`, off the clock |
| 8 | MED | The most forward frame on each deck re-nocked forever while its deckmates shot. Three occurrences, three separate battles | Presented as an animation bug, so it was investigated as one. It is a line-of-sight problem: the beast's head is at their height and the mahout is under the sightline | Frames moved behind roughly y -3 |
| 9 | LOW | The runtime 3x scale on an entity carrying physics, and about to carry navmesh | It was the design's best feature (change `BodyLength` and the crew follow) which made it hard to see as a liability | Prefab authored at final size; a test pins `body_length="300"` |

## Root cause: the reference implementation was the least informative thing available

Every wrong turn traces to one assumption: **the howdah works, so the howdah's mechanism works.** It was
treated as proven design when it was a hack inside its envelope. Concretely, the howdah is 3.2 m up, scale
1.0, one deck, two archers; the mumakil is 9 to 13.8 m, scale 3.0, three decks, eight archers. Every single
one of those differences produced a defect, and the clone inherited none of the rules for them because none
existed.

Yesterday's RCA already named this pattern and wrote the preventive action: *"when cloning a feature onto a
bigger instance, write the difference list FIRST, and ask of each entry what rule the original never needed."*
That lesson was correct and I did not apply it, because I read it as being about geometry. It is about
mechanism too. **The howdah's teleport is not a smaller version of what the mumakil needs. It is a different
thing that resembles it.**

The second-order cause is cheaper to fix: **nobody asked the engine how it does this** until day two.
One grep for `NavMeshPrefabName` returned two classes and settled the architecture in about a minute. It
should have been the first question asked of a feature whose whole job is carrying agents on a moving object.

## Why the tests could not catch any of it

All 15 gates were green throughout, including the battle where every archer was standing on the ground. That
is not a gap to close, it is the boundary of what a test can see: every gate reasons about the prefab's
geometry, and the prefab was correct the whole time. The failure was in what the ENGINE did with it.

The thing that actually made the two days tractable was instrumentation, not testing. The per-seat log line
(`action/max/re/tp/dz`) is what turned "they look stuck" into "dz is constant at 1.04, so they are standing on
something and the teleport is irrelevant". Two probes each answered a question that was unanswerable from
managed code and are now deletable: `frameScale` proved `Agent.Frame` carries no scale, and `boneL` proved the
platform sits at its bone's rest height.

**Preventive action:** for any feature whose behaviour lives across the managed/native boundary, budget for a
diagnostic line before budgeting for tests. Here the tests proved the data and the log proved the feature.

## Not applied

| Thing | Why not |
|---|---|
| Invisible-mount ("watermelon") locking, as ADOD used | Would double the agent count and needs the crew's bows reworked for mounted use, to solve a problem that no longer exists: the remaining stuck archer had `dz 0.00` and 35 teleports, so it was never a positioning failure |
| Navmesh blocker faces (group 4) | `finalizeBlockerConvexHullComputation` says blockers resolve into a convex hull, and one hull around eight islands spread over three decks could swallow the walkable area. Not needed anyway once locomotion was pinned |
| Converting the elephant howdah to the same mechanism | It works at 3.2 m with `drift=0.000`. Its real defect is a frame 1.7 cm from the deck edge, which is a two-line prefab fix and does not need the conversion |

## Owed

A campaign battle with several mumakil (crew count toward their formation's average position), the mission-end
hang path with crew aboard, and downward shooting at a target directly below. The howdah's edge margin.
