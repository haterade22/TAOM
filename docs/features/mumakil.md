# Mûmakil (Harad giant war beast)

> **Status: BUILT + WIRED + IN-GAME CONFIRMED (2026-06-29).** A scaled-up clone of the
> [War Elephant](elephant.md): a ridden Harad mount that auto-attacks (trample + tusk) and **charges** into melee.
> Phase 2 (#627, 2026-09-20) put a crew on the war tower: eight archers across its three decks, carried by an
> invisible platform entity scaled onto the beast from `Agent.AgentScale`. NOT yet smoke-tested in game.

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

#### UNVERIFIED (2026-08-28): `body_length` probably scales the RIDER too

`[Likely]` The scale block is not mount-only. Traced against the installed v1.4.8 decompile and
independently by a Codex pass during the [war ram](war-ram.md) review:

- `EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are **the same value, 10**
  (`TaleWorlds.Core`, `EquipmentIndex`). The slot the scale block reads *is* the Horse slot.
- The block in `Mission.BuildAgent` has **no `IsMount` guard**. It scales any agent whose
  `SpawnEquipment[EquipmentIndex.ArmorItemEndSlot]` holds an item with non-zero
  `HorseComponent.BodyLength`.
- `Mission.SpawnAgent` runs `BuildAgent` for the **rider as well as** the mount, and only the mount
  gets a trimmed equipment set: the mount agent is given a fresh `Equipment` holding slots 10 and 11
  alone, while the rider keeps the full set with the Horse item still in slot 10.

The Mûmakil ships `body_length="300"`, so this predicts a **3× rider**, not a normal-sized man on a
3× beast. Nothing settles it offline: `SetInitialAgentScale` is a one-line call into native
(`MBAPI.IMBAgent.SetAgentScale`), and nothing else in the managed layer resets an agent's scale
afterwards.

**Owed: an in-game look.** Spawn `harad_mumakil_rider` beside a foot troop and compare heights.
Nothing already written here settles it either way. The paragraph above records "the rider sits
correctly high on the 3× back", which is an observation about where the rider sits, not how big he
is, and a 3× rider on a 3× mount would look proportionate from any distance. The "How-to" line
below, "large scales can produce rider-perched-high quirks", may already be this same effect seen
from the outside rather than a separate problem, so treat a rider that looks wrong as evidence for
this rather than as a second bug. The war-ram RCA records the general shape of this miss (an
engine path assumed inert because TAOM's own code does not drive it):
[rca-war-ram-2026-08-28.md](../reviews/rca-war-ram-2026-08-28.md).

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

## Phase 2: platform crew (built 2026-09-20, not yet smoked)

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
| `taom_mumakil_platform` prefab: three floor bodies (`moveable` + `barrier`), eight tagged crew frames, nothing rendered | `LOTRLOME_Armory/Prefabs/`, byte-snapshotted to `docs/reference/lotrlome-armory-snapshot/Prefabs/` |
| `TaomMumakilPlatform` | places AND scales the entity onto the beast every tick; releases every seat before vanilla can deactivate it |
| `TaomMumakilStandingPoint` | one seat, carrying the howdah's four engine rules |
| `MumakilCrewSpawner` | owns the platform's whole lifecycle: builds it on the rider's `OnAgentBuild`, queues the crew, spawns them from the next `OnMissionTick` |
| `MumakilCrewAgentOrigin` | a no-op casualty surface per archer, so a crew death is not booked to the mahout |
| `MumakilPlatformTests` | 12 gates: prefab name, deck layout, spacing, headroom, containment, deadband margin, tag-vs-script parity, floor flags, visibility, no baked scale, live-copy match, IL release |

Three things were genuinely new here and none of them existed on the elephant.

**The scale is derived, never written down.** The prefab is authored mount-local, 1:1 with the FBX, and
`RepositionToMount` multiplies the entity's basis by the mount's own `Agent.AgentScale`. Change `BodyLength` and the
crew follow it. The basis is orthonormalised BEFORE the scale is applied, because `Mat3.ApplyScaleLocal` multiplies
an existing basis rather than setting one, and whether the native `GetRotationFrame` behind `Agent.Frame` already
carries `AgentScale` cannot be settled from managed code. Stripping it first makes the answer irrelevant; getting it
wrong would put the crow's nest at 41 m instead of 13.8. The diagnostics line prints `frameScale=` so the first
battle log answers the question permanently (1.00 = the basis is unit, 3.00 = it carried the scale).

**Headroom, the rule a multi-deck platform adds.** A crew frame under a higher deck's floor needs the human
capsule's full 1.92 m of clearance (radius 0.37, `pos1` z 1.55, `Native/monsters.xml`). `BodyFlags.Barrier` is in
the engine's missile-exclusion mask but NOT in its agent mask, so an archer whose capsule reaches into the deck
above is shoved by the engine every frame and put back by its seat, which reads as tens of m/s and stops every bow
draw. It is the spacing failure in a new direction, and just as silent. Every deck underside here sits 1.79 m above
the deck below, which means a frame under one can never pass: the only fix is to move it out from under, and the
footprint it must clear is inflated by the capsule's radius. `mumakil_crew_main_4` was authored under the upper
deck and moved beside it.

**The two rules pull against each other.** The first correction moved `main_4` forward to y -0.386, which cleared
the deck above but left it 1.05 m from `main_1`: at a 0.45 m deadband, two archers drifting at each other would
close to 0.15 m, inside the 0.74 m spacing rule. The kept position, (0.700, -0.500), clears both. That is why the
tests pin headroom, containment, spacing AND the deadband margin rather than any one of them.

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
