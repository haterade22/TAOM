# Mûmakil (Harad giant war beast)

> **Status: BUILT + WIRED + IN-GAME CONFIRMED (2026-06-29).** A scaled-up clone of the
> [War Elephant](elephant.md): a ridden Harad mount that auto-attacks (trample + tusk) and **charges** into melee.
> Phase 2 (#627) put a crew on the war tower: eight archers across its three decks, standing on an attached
> navmesh. **All eight confirmed drawing and loosing in game, 2026-09-22.**

## Overview

The Mûmakil (the Oliphaunt) is a giant ridden Harad mount. Mechanically it is the War Elephant at **3× scale** with
a distinct mesh + war-platform: it reuses the elephant rig (`elephant_skeleton`), the `as_elephant` action set, and
**every** elephant animation clip — including the `act_elephant_attack_*` trample/tusk clips its behavior tree plays.
It is recruitable only from Ayerikkä (`clan_aserai_1`).

## Why this exists — and why it was cheap to build

The elephant/spider/warg/chariot work already proved the hard parts (non-humanoid ridden mounts, the `quad_movement`
gait requirement, the mount-lock, the per-agent attack behavior tree). Because the Mûmakil **shares the elephant
skeleton**, it inherited all of that for free: **no new animation authoring, no `quad_movement` work, no action-set /
monster-usage authoring, no native crash guards.** The only genuinely new pieces are a mesh, a Monster id, an item, a
troop, and a thin C# clone of the elephant attack feature.

## Architecture

### Size lives on the Horse item, not the Monster

`[Certain]` The `Monster` class has **no scale field**. The engine scales a mount at build via
`Mission.cs:4019 → agent.SetInitialAgentScale(0.01f * HorseComponent.BodyLength)`. So **size = Horse-item
`BodyLength` / 100**: elephant `BodyLength=100` → 1.0×; Mûmakil **`BodyLength=300` → 3.0×**. `SetInitialAgentScale`
scales the whole agent uniformly — skeleton, rider-attach bone, **ragdoll bodies, and the collision capsule** all go
3× (confirmed in-game: the rider sits correctly high on the 3× back).

#### RESOLVED (2026-09-22): `body_length` does NOT scale the rider

**Mike, 2026-09-22: the rider is not scaled.** This section used to say `[Likely]` the opposite and
owed an in-game look. What settled it is the comparison it asked for: since #627 phase 2 every
mumakil carries eight crew archers, 1.0x foot troops with no Horse slot, standing in the same frame as
its rider. A 3x rider beside them would be unmissable in a way that a 3x rider on a 3x beast, seen
alone, is not.

The managed-code trace below is still accurate, and it is worth keeping because it explains why this
was ever in doubt. Read on its own it predicts a scaled rider:

- `EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are **the same value, 10**
  (`TaleWorlds.Core`, `EquipmentIndex`). The slot the scale block reads *is* the Horse slot.
- The block in `Mission.BuildAgent` has **no `IsMount` guard**. It scales any agent whose
  `SpawnEquipment[EquipmentIndex.ArmorItemEndSlot]` holds an item with non-zero
  `HorseComponent.BodyLength`.
- `Mission.SpawnAgent` runs `BuildAgent` for the **rider as well as** the mount, and only the mount
  gets a trimmed equipment set: the mount agent is given a fresh `Equipment` holding slots 10 and 11
  alone, while the rider keeps the full set with the Horse item still in slot 10.

So whatever stops the rider scaling is not in the managed layer. `SetInitialAgentScale` is a one-line
call into native (`MBAPI.IMBAgent.SetAgentScale`), and the native side evidently ignores it for a
mounted human or undoes it on mounting. The lesson that outlives this case: **a clean managed trace is
not a prediction of behaviour when the last call in it crosses into native.** It was carried as
`[Likely]` for almost a month on the strength of three correct bullet points.

This matters beyond the mumakil: it is why the war elephant could be resized to 1.3x on 2026-09-22
with no work on its mahout.

### Shared skeleton + animation reuse

- The Mûmakil mesh (`sk_mumakil_basemesh_a1` + the `sk_mumakil_platform_a1` war-tower) is skinned to
  **`elephant_skeleton`** (60 bones). The source FBX's armature was renamed `elephant_skeleton_unused →
  elephant_skeleton` and re-exported **mesh-only** (no bundled skeleton — it references the shared skeleton that lives
  in the elephant's `adod_elephant_geo.tpac`, mirroring `sk_elephant_armor_geo.tpac`).
- The Monster reuses `action_set="as_elephant"` + `monster_usage="elephant"` verbatim — multiple monsters can share
  an action set/usage/skeleton (vanilla does this for horses).

### Collision capsule (scaled-mount gotcha)

The elephant's `<body_capsule>` (radius 0.9, length 2.57) is **elephant-proportioned**. Copied verbatim onto the
longer/bulkier Mûmakil mesh and scaled 3×, it left the body's front/back uncovered — enemies ran *into* the visual
mesh before colliding with the central capsule. Fixed by enlarging the **1× base** capsule (radius **1.1**, Y spread
**+2.0 … −2.6**, length 4.6 → ~radius 3.3 / length 13.8 in-game) to match the Mûmakil's footprint. The ragdoll is the
shared `elephant_skeleton` per-bone physics — it scales 3× automatically but cannot be enlarged *independently* of the
elephant without forking the skeleton.
The elephant's own body capsule was refitted on 2026-09-18 (radius 1.05, full body length), and the shared
skeleton's per-bone hit capsules were refit to the mesh the same day (neck, legs and haunches had been thin Kit
defaults), so the Mumakil's hit capsules grew with them at its 3x scale (`elephant.md`, "Collision"; in-game test
owed).

### Charge, not horse-archer (2026-06-29)

Both the Mûmakil **and** the elephant rider were switched from `default_group="HorseArcher"` to **`Cavalry`** and
re-armed with a spear (`eastern_spear_4_t4`) + sword, dropping the bow + quivers. As `HorseArcher` the formation
skirmished at range so the mount never closed to trample; as `Cavalry` it charges in and the auto-trample/tusk BT
actually fires. (This reverses the elephant's 2026-06-15 bow-rider experiment.)

### C# (clone of the elephant attack feature, minus howdah)

`Main/Features/Mumakil/` mirrors `Main/Features/Elephant/` without the howdah. **Since the 2026-07-01 ElephantLike
unification (#305) the formerly-cloned internals are SHARED:** `MumakilAttackService` is a thin binding of
[`ElephantLikeAttackService`](../../Main/Features/ElephantLike/ElephantLikeAttackService.cs) (ctor passes the
`MumakilConfig` constants; `IsCreatureMonster` / `ShouldEngage` / `IsOffCooldown` / `ComputeInflictedDamage`,
24 unit tests) behind the marker interface `IMumakilAttackService : IElephantLikeAttackService`; the per-agent
`MumakilBehaviorTree` (trample on a 10s cooldown → left/right tusk swing on a 4s cooldown → idle) builds the shared
`ElephantLike` BT nodes bound to [`MumakilCombat.Profile`](../../Main/Features/Mumakil/MumakilCombat.cs), attached by
`MumakilMissionBehavior` keyed on `Monster.StringId == "taom_mumakil"`. The mount-lock lives in the shared
`TaomAgentStatCalculateModel` (a 4th injected `IMumakilAttackService` → `CanAgentRideMount=false` +
`MountDifficulty=999`). Attack clips reuse the elephant's `act_elephant_attack_1..4` (shared `as_elephant` set);
`MumakilCombat.Profile.AnyUnresolved()` logs at mission start if a future Armory rename breaks them.

## Configuration / data

| Where | What |
|-------|------|
| `MumakilConfig.cs` | `MumakilMonsterId="taom_mumakil"`, `MountDifficulty=999`, attack gates/cooldowns/damage, clip names (= elephant's). Trample reach scaled ~3× for the 3.0× body (`TrampleTriggerRange=9`, `TrampleRadius=12`). |
| `lotr_monster_mumakil.xml` (LOTRLOME_Armory) | Monster `id="taom_mumakil"`, `action_set="as_elephant"`, `monster_usage="elephant"`, enlarged `<body_capsule>`. Registered in `LOTRLOME_Armory/SubModule.xml`. |
| `LOTRAOM_horses.xml` (LOTRLOME_Armory) | Horse item `taom_mumakil`: mesh `sk_mumakil_basemesh_a1`, platform via `<AdditionalMeshes>`, **`body_length="300"`** (= 3.0×), no HorseHarness. |
| `troops_harad.xml` | `harad_mumakil_rider` — `Cavalry`, spear + sword, Horse=`Item.taom_mumakil`, no HorseHarness. |
| `VolunteerRecruitmentService.InitializeHaradClans` (in [`RecruitmentPools/VolunteerRecruitmentService.Harad.cs`](../../Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Harad.cs) since the 2026-07-01 pool split, #308) | `harad_mumakil_rider` in `clan_aserai_1` (weight 1 of 12). |

> **The Monster XML, Horse item, mesh, and SubModule.xml registration live in the external `LOTRLOME_Armory` module
> (the game install), NOT this repo** — same as the elephant. They are not version-controlled here.

### Player-ridden mûmakil attack too (#643, 2026-09-23)

The trample and the tusk swings fire under ANY rider, the player included, as the warg's bite does (Mike: "All trees like the elephant, mumakil, the Rams, and Elk should also do these things when controlled by a player. Like the warg"). The tree used to gate its attack branch on `IsAiControlledDecorator`, so a player-ridden mount fell through to a one-second sleep and never attacked; the branch now sits directly under `HasRiderDecorator`. The attack stays automatic (enemy in front, in range, off cooldown), the rider keeps steering, and the knockdown rule is unchanged: anyone not shield-blocking goes down, a mounted victim is dismounted.

**The blow is the rider's**, the creature's only once the rider has gone (the warg's rule), and the combat log reads Pierce instead of Blunt; the engine evidence for both is in [elephant.md](elephant.md), "Player-ridden elephants attack too". On a 3x beast the rider sits high, which lifts the hit's position, and with it the hit sound, well above the victim.

The mûmakil stays mount-locked, so a player only rides one they spawned on.

## Key files

| File | Role |
|------|------|
| `Main/Features/Mumakil/MumakilConfig.cs` | Tuning constants (id, mount-lock, gates, cooldowns, damage, clips). |
| `Main/Features/Mumakil/IMumakilAttackService.cs` + `MumakilAttackService.cs` | Pure decision logic (no TaleWorlds deps) — thin binding of the shared `ElephantLikeAttackService` since 2026-07-01 (#305). |
| `Main/Features/Mumakil/MumakilMissionBehavior.cs` | Boundary: registers + attaches the per-agent BT (keyed on Monster id). |
| `Main/Features/Mumakil/MumakilBehaviorTree.cs` + `MumakilCombat.cs` | The trample/tusk behavior tree — builds the SHARED nodes in `Main/Features/ElephantLike/BehaviorTreeElements/` bound to `MumakilCombat.Profile`. |
| `Main/Features/Mumakil/MumakilIoC.cs` | Registers `IMumakilAttackService` (Singleton). |
| `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` | Mount-lock (shared with elephant/spider). |
| `Main/IoC.cs`, `Main/SubModule.cs` | Wiring (IoC reg, service resolve + ctor arg, `AddMissionBehavior`). |
| `TAOM.Tests/Features/Mumakil/MumakilAttackServiceTests.cs` | 24 service tests. |
| `tools/audit_mount_parity.py`, `tools/verify_mount_assets.py` | Extended for `mumakil` (the latter searches the shared elephant asset dir via `extra_asset_dirs`). |

## Tests

- `MumakilAttackServiceTests` — 24 tests (IsCreatureMonster, ShouldEngage, IsOffCooldown, ComputeInflictedDamage),
  mirroring the elephant's. BT elements + mission behavior are tested via game (warg/elephant/ADR-008 precedent).
- `VolunteerRecruitmentServiceTests` — the `clan_aserai_1` pool tests cover the new Mûmakil bucket (`Next(12)`).

## How-to

- **Resize:** edit `body_length` on the `taom_mumakil` Horse item (`/100` = scale). Data change — reload the game,
  no rebuild. Large scales can produce rider-perched-high / navmesh (gate/bridge) / collision quirks.
- **Tune the collision** to the size: the `<body_capsule>` in `lotr_monster_mumakil.xml` is 1× base, scaled by
  `AgentScale`. Bigger `radius` = wider/taller block; wider `pos1`/`pos2` Y spread = longer block.
- **Tune the trample reach/feel:** `TrampleTriggerRange` / `TrampleRadius` / cooldowns / damage in `MumakilConfig.cs`
  (C# — needs a rebuild).
- **Verify assets after any mesh re-export:** `python tools/verify_mount_assets.py mumakil` +
  `python tools/tpac_skeleton_scan.py <mumakil tpac>` (must reference `elephant_skeleton`, no `_unused`).

## v1.4.8 exposure (2026-08-10) — zero rein attributes; mounted-death test owed

`lotr_monster_mumakil.xml` declares **0 of the 12 rein attributes** the engine reads, while being
`Mountable="true"` (`family_type="10"`) — inherited verbatim from the elephant it clones. Every
vanilla `Mountable` monster carries all twelve. v1.4.8 fixed a "horse rein visual bug when a mounted
agent died" — native, no managed diff, in a path that runs on **mounted-agent death**
([v1.4.8-impact.md](../migration/v1.4.8-impact.md) row N7).

**UNVERIFIED — no crash is predicted, and nothing offline settles it.** `tools/audit_mount_parity.py`
does not check reins (zero occurrences of "rein"; it always exits 0), and the Mûmakil is not in its
Section A attribute-presence comparison at all, which compares spider vs warg vs elephant only.
**Owed:** a battle where a *ridden* Mûmakil is killed and where its rider dies while mounted. Full
measured table: [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) "The
rein-attribute invariant"; sibling entry in [elephant.md](elephant.md) "v1.4.8 exposure".

## Phase 2: platform crew (working in game 2026-09-22)

**The old blocker is gone.** This section used to say crew were deferred until a crew-versus-mount collision fix
(a shared `FaceGroupId`), because force-spawned crew inside the mount capsule caused the slide. #627 solved that on
the elephant a different way: the platform's floor underside sits clear of the mount's body capsule, and the seat
only corrects an archer's position once it has drifted past a deadband. Measured on the elephant, 2026-09-20:
`drift=0.000` on every sample, `carriedV` tracking the mount's own speed, and the crew shooting standing and moving.
Nothing about the mumakil reintroduces the old failure.

### What the asset actually is (Blender, read-only, 2026-09-20)

`LOTRLOME_Armory/AssetSources/creature/mumakil/sk_mumakil_harad_01.fbx`. The war tower is
`sk_mumakil_platform_a1`, carried as an `AdditionalMesh` on the Horse item `taom_mumakil`, not on a harness. The FBX
is authored at ELEPHANT scale (basemesh x +-0.83, z to 3.00, the same as the elephant's); the 3.0x comes from the
Horse item's `BodyLength=300` at runtime.

| Deck | z authored | z in game (3.0x) | Standable area inside the walls, in game | Capacity at the 0.74 m rule |
|---|---|---|---|---|
| Main | 3.00 m | 9.00 m | 15.0 m2 | 47 |
| Upper | 3.80 m | 11.40 m | 2.6 m2 | 14 |
| Crow's nest | 4.60 m | 13.80 m | 1.9 m2 | 5 |

Those capacities are geometric CEILINGS from packing 0.37 m capsules 0.74 m apart across the deck faces that are
both walkable and clear of a wall (the rule #627 established: crew count is decided by capsule spacing, not by deck
area). They are not proposals. **Mike's decision, 2026-09-20: eight archers, five on the main deck, two on the upper,
one in the crow's nest.**

### Decisions taken

- **The platform prefab is authored mount-local (1:1 with the FBX) and scaled at runtime from `Agent.AgentScale`**,
  never with 3.0 baked in. `AgentScale` is public on Agent, and the mount's scale comes from `BodyLength`, so a
  future size tweak would otherwise drop every archer inside the beast with no error.
- **The code is CLONED into `Main/Features/Mumakil/`**, matching the one-feature-per-creature convention the spider
  and chariot set (Mike, 2026-09-20). The risk that buys is drift: the howdah's four engine rules took nine in-game
  rounds to find, and a clone can regress them independently. Mitigation, and it is not optional: the clone
  references the PURE helpers rather than copying their numbers (`HowdahSeatMotion` for the deadband and the 0.74 m
  spacing, `HowdahCrewBehaviourCurves` for the AI curve rows, `HowdahDiagnostics`/`HowdahSampleClock`/
  `HowdahRunStats` for the log maths). Those carry measured constants, not creature behaviour.
- **The crew trigger is the mount item, not a harness.** The elephant gates on `sk_elephant_armor_howdah_elite`;
  the mumakil's tower is on `taom_mumakil` itself, so every mumakil carries crew.
- **The crew troop is `harad_howdah_crew`** (bow and two quivers, no melee weapon, Bow 95 on its ladder cell,
  hidden from the Encyclopedia). No new troop unless the mumakil should field a distinct name.

### What shipped

| Piece | Where |
|---|---|
| `taom_mumakil_platform` prefab: three 3 cm floor slabs, eight crew frames, nothing rendered | `LOTRLOME_Armory/Prefabs/`, byte-snapshotted to `docs/reference/lotrlome-armory-snapshot/Prefabs/` |
| `taom_mumakil_platform_navmesh.bin`: eight 1.6 m walkable squares, face group 1 | `Main/_Module/NavMeshPrefabs/` (git-tracked; deploys to `Modules/TAOM/`) |
| `TaomMumakilPlatform` | places the entity on the beast each tick, attaches the navmesh, releases seats before vanilla deactivates them |
| `TaomMumakilStandingPoint` | one seat: keeps the formation, rewrites the AI curves, pins locomotion, corrects drift past a deadband |
| `MumakilCrewSpawner` | owns the platform's lifecycle; builds on the rider's `OnAgentBuild`, spawns crew from the next `OnMissionTick` |
| `MumakilCrewAgentOrigin` | a no-op casualty surface per archer |
| `MumakilPlatformTests` | 15 gates: prefab name, decks, spacing, headroom, containment, deadband margin, tag-vs-script parity, navmesh declaration, `body_length`, live-copy match, IL release |

### The four things that had to be true at once

None of these was obvious, and the feature is dead without any one of them. They took 2026-09-21 and
2026-09-22 to find, mostly because each one masks the next.

**1. A navmesh, because a teleport cannot hold an agent up.** This is the big one. `Agent.TeleportToPosition`
is a bare native `SetPosition` with no ground snapping, so it looks like it should work, and at the
elephant's 3.2 m it does. At 9 m it does not: all eight archers were placed on their seats correctly, to the
centimetre in x and y, and fell straight to the ground, 25,000 teleports a battle not holding them. Searching
the engine for how vanilla does it turns up exactly two classes, `SiegeTower` and `MissionShip`, and **neither
moves an agent at all**. Both import a navmesh prefab and attach its faces to their own entity so the walkable
surface travels with them. That is the mechanism; ours was a hack that happened to survive at low altitude.

**2. Physics as well as navmesh.** Stripping the floor slabs while keeping the navmesh dropped every archer to
the ground again. Navmesh makes a position VALID to stand at; a physics body is what an agent stands ON. A
siege tower has both and so does this.

**3. The prefab authored at final size, with no runtime scale.** It was originally authored mount-local and
multiplied by `Agent.AgentScale` every tick, which was elegant (change `BodyLength` and the crew follow) and
wrong: no vanilla object carrying a physics body or a navmesh is ever runtime-scaled. `MumakilPlatformTests`
pins `body_length="300"` so the assumption these numbers are final cannot break silently.

**4. Frames inboard, and off the beast's sightline.** Covered below; it is the part most likely to catch the
next creature.

### Geometry rules for a crew frame

Learned the hard way, and all four are now gates in `MumakilPlatformTests`.

- **Headroom.** A frame under a higher deck needs the human capsule's full 1.92 m (radius 0.37, `pos1` z 1.55,
  `Native/monsters.xml`). `BodyFlags.Barrier` is in the engine's missile-exclusion mask but NOT its agent mask,
  so an archer whose capsule reaches into the deck above is shoved every frame. Deck undersides here sit 1.79 m
  apart, so a frame under one can never pass: move it out from under.
- **Footing beats separation.** Frames were first spread to the deck edges to maximise spacing. That is the
  wrong objective. An archer needs its whole 0.74 m body on walkable ground, and the three nearest an edge sat
  at a fixed 0.57 and 1.04 m above their seats, unmoved by 7,619 teleports, re-nocking forever. Every frame is
  now at least a body radius plus margin inside its deck, and every navmesh square is 1.6 m, giving 0.43 m of
  spare on each side.
- **Navmesh islands must be bigger than a body.** The first hand-painted islands were 0.37 to 0.65 m across
  against a 0.74 m body. An agent overhanging its island on every side has no stable footing. This was a
  briefing error, not an authoring one: small islands were meant to stop wandering, and `SetMaximumSpeedLimit(0)`
  already does that.
- **Nothing forward.** See below.

### The mount's own head blocks its crew's shots

`[Likely]` The most forward frame on each deck failed, three times, in three separate battles: `main_1` at
y -0.11, `main_4` at y -1.50, then `upper_1` at y -2.10. Each drew to 0.85 and re-nocked forever while every
other seat on the same deck, taking the same teleports, shot normally. Moving each one back fixed it every
time. The beast's head is at their height directly ahead and the mahout sits just under the sightline, and the
ranged AI will not loose past a friendly. **Keep crew frames behind roughly y -3 in platform-local metres**,
which on this tower means behind the shoulders.

Not proven to the level of a decompiled clear-shot check, but three for three with a clean control each time.

### Why locomotion is pinned

`SetMaximumSpeedLimit(0f, isMultiplier: false)` at `OnUse`, cleared with `-1f` on release. Until the platform
carried a navmesh the crew physically could not walk, so nothing ever had to stop them; the first battle with
a navmesh under them they wandered up to 7.06 m, because a ranged agent that CAN reach a better firing
position will go and take it. Turning and shooting are unaffected.

It is applied ONCE at `OnUse` and deliberately NOT on the half-second stance clock. Re-applying it every 0.5 s
on eight seats in lockstep matched the draw dying at 0.85 on all eight in lockstep, while the teleport count
did not (340 teleports against 27 restarts in the same window).

### What the log tells you

`[Mumakil#n] status` carries, per seat, `action/max/re/tp/dz`:

| Field | Healthy | What a bad value means |
|---|---|---|
| `dz` | 0.00 | non-zero and CONSTANT is the killer: the archer is standing on something that is not its seat, and no number of teleports will move it |
| `max` | 1.00 | 0.85 is the draw dying at the release handoff, i.e. the re-nock loop |
| `re` | any | restarts only mean "stuck" when `max` is 0.85. With `max` 1.00 a high count is a busy archer; the crow's nest ran 88 |
| `tp` | tens | thousands means the seat is fighting something. 7,619 in one battle was an archer pinned a metre up |

### The reference-frame mismatch, and why it is the first thing to watch
### The reference-frame mismatch, and why it is the first thing to watch

`[Likely]` The tower the player SEES and the platform the crew STAND ON are placed by different mechanisms. The
tower is an `AdditionalMesh` on the Horse item, and `MountVisualCreator.AddMountMeshToAgentVisual` hands it to
`agentVisual.AddMultiMesh`, i.e. onto the beast's SKELETON, so it deforms with the walk cycle. The crew platform is
framed from `Agent.Position` and `Agent.Frame`, which is root space and does not bob. The same mismatch exists on
the elephant and is the stated reason `TaomHowdahMachine` carries a deferred `Spine1_05` bone-tracking path. At
3.0x, with decks 9 to 13.8 m up and frames up to 8 m behind the origin, a given spine rotation moves the visible
deck several times further than it moves on the elephant.

This is not a code defect and no deadband can fix it: chasing the bob recreates the velocity failure the deadband
exists to prevent. It is a measurement. Stand a mumakil still, walk it, then turn it hard, and watch whether the
archers sink into or float above the tower floor. If they do, bone tracking becomes phase 2b.

**Verified clear, separately:** the main deck's floor underside sits 0.59 m above the beast's body capsule
(capsule top 2.6 authored, floor underside about 2.80), against 0.33 m on the rebuilt howdah. The vertical half of
the old "slide" class does not reproduce here.

**Do not equip a HorseHarness on `harad_mumakil_rider`.** The tower is an `AdditionalMesh`, and `taom_mumakil` sets
`affected_by_cover="true"`, so equipping a harness routes the mesh down the `ManeCoverType` branch where a covered
mount drops it entirely: eight archers standing on nothing. `tools/taom_schema.py` exempts the troop in
`_HARNESSLESS_BY_DESIGN` for exactly this reason, and the comment in `troops_harad.xml` now says so.

### Risks to settle before or during the smoke

- **Agent budget.** Eight crew across four mumakil is 32 extra agents, on top of any crewed elephants in the same
  battle. The engine's cap is native and unverified, and #627's RCA already records a reinforcement-headroom guard as
  NOT APPLIED for the same reason.
- **Formation averaging.** Crew are ordinary members of their side's Ranged formation and count toward its average
  and median position (#627, data-flow F-4). A mumakil's crew is four times an elephant's, so this scales with the
  feature. Unmeasured; watch the archer formation's behaviour with several beasts on the field.
- **Shooting downward from 9 m.** Horizontal shooting is confirmed on the elephant at 3.2 m. Whether an archer will
  loose at an enemy directly below is the open combat-mask question from #627 (missiles ignore `barrier`, but the
  mask a native clear-shot check would use does not, and no managed code reads it). Three times the height makes the
  angle steeper and the question sharper. Check it in the first crew smoke.
- **Rider clearance.** The deck runs y -8.6 to +1.1 m in game; the mahout sits forward on the neck. No crew frame
  is forward of y -0.036 authored, but the mahout's seat is on the beast's skeleton and the crew's is not, so this
  is a look rather than a calculation.

### Owed in-game checks

Custom Battle, one mumakil, Crew Platform Diagnostics ON, in this order. The first three are cheap and each one
invalidates the rest if it fails.

1. Read `scale=` and `frameScale=` in the first `[Mumakil#1] status` line. `scale=3.00` always; `frameScale=1.00`
   means `Agent.Frame` carries no scale and `frameScale=3.00` means it does and the orthonormalise is load-bearing.
   Either answer is fine; record it and the probe can be deleted.
2. Read `topSeatZ`. It should be about 13.80. Near 4.6 means the scale never reached the platform; near 41 means it
   was applied twice.
3. Confirm the archers stand ON the decks, not above or inside them, and that `seated=8/8`.
4. Stand still, walk, then turn hard, watching for archers sinking into or floating off the deck (the
   reference-frame mismatch above) and for `drift` and `carriedV` in the log.
5. Confirm arrows actually leave the bows at range, and separately whether an archer will loose at a target
   directly BELOW it. That is the open combat-mask question from #627, and 9 to 14 m makes the angle sharper.
6. End the battle WITH archers still aboard. That is the mission-end hang repro, and the IL test can only prove
   the release is wired, not that it runs.
7. A campaign battle with several mumakil, watching the archer formation: crew count toward their formation's
   average position, and eight per beast scales that concern with the feature.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/elephant.md](./elephant.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
