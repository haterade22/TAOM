# Mûmakil (Harad giant war beast)

> **Status: BUILT + WIRED + IN-GAME CONFIRMED (2026-06-29).** A scaled-up clone of the
> [War Elephant](elephant.md): a ridden Harad mount that auto-attacks (trample + tusk) and **charges** into melee.
> Phase 1 = one rider, no platform crew/howdah (the war-tower is baked into the mesh, visual only).

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

## Phase 2 (not built)

Platform crew (archers on the war-tower) — deferred for the same physics-contact reason the elephant howdah crew is
disabled (force-spawned crew inside the mount collision capsule cause the "slide"). Re-enabling requires the
crew↔mount collision fix (shared `FaceGroupId`). See [elephant.md](elephant.md) "Slide root-cause isolation".
