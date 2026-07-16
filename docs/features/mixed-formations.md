# Mixed Formations

## Overview

Re-orders the melee + ranged units within a single formation while it holds position, so a formation that contains both classes can deploy in 4 layouts: infantry-in-front (default), ranged-in-front, ranged-on-the-wings (infantry center), or checkerboard. Active during `MovementOrder.MovementStateEnum.Hold` only — vanilla logic owns charge/retreat/stand-ground.

## Why This Exists

LOTR battles often produce mixed formations (a Rohirrim line with archers tucked in among riders, or a Gondor company mixing spearmen and crossbowmen). Vanilla Bannerlord positions every unit by `Agent.Index` order with no class awareness, so a 60-archer + 40-pikeman formation interleaves randomly — pikemen end up scattered behind archers and archers end up in melee range.

- **Vanilla behavior:** `Formation.GetOrderPositionOfUnit` uses arrangement-class-only positioning; mixed-class formations have melee + ranged interspersed in arbitrary index order.
- **TAOM requirement:** When a formation contains both melee and ranged, position them per a deliberate layout. Players can pick a default and cycle layouts on the fly via hotkey.
- **Without this feature:** The player has to manually split mixed formations into pure-class formations every battle, which loses the visual identity of mixed companies and adds tedium to deployment.

## Architecture

### Design Challenge

Three constraints:

1. **Position calculation must run inside vanilla's `GetOrderPositionOfUnit`** — that's the single point in the engine where each unit's intended position is queried. Patch the method, prefix-skip vanilla when our layout produces a position, return `true` otherwise.
2. **Apply only during `Hold` movement state** — during charge, the engine's free-form combat positioning dominates; trying to override would fight the engine. The original developer's `(int)GetMovementState() != 1` check is preserved (`MovementStateEnum.Hold = 1`).
3. **Cache assignments per formation** — recomputing the slot grid for every unit on every position query would be O(N²) per formation per tick. Build the assignment once when the layout is set or the formation appears, then look up by `Agent.Index`.

### Solution Approach

The feature splits into three layers:

- **`LayoutPositioner` (pure function)** — given formation geometry (width, interval, units list) and a layout type, computes a fresh `SlotAssignment` mapping each unit's `Agent.Index` to a (row, file) offset. Pure math, fully unit-testable without a live mission.
- **`FormationLayoutService` (singleton)** — holds the per-formation layout choice (`Dictionary<formationKey, FormationLayoutType>`) and the per-formation cached assignment (`Dictionary<formationKey, SlotAssignment>`). Drives the `IsMixedFormation` heuristic for the auto-default-applier and the `CycleLayouts` rotation.
- **`MixedFormationsMissionBehavior` (engine bridge)** — ticks every frame: every 1s, asks the service to apply default layouts to mixed formations on the player team; every frame, polls the configured cycle hotkey and rotates layouts on the selected formations (or all if none selected). Resolves player team and selected-formations adapters; passes them to the service.
- **`Patch30_FormationGetOrderPositionOfUnit` (Harmony Prefix)** — intercepts vanilla; calls the service; if the service returns a `Vec2` plane position, queries `Mission.Current.Scene.GetGroundHeightAtPosition` for ground Z, builds a `WorldPosition`, and returns `false` to skip vanilla. Otherwise returns `true`.

**Mission-type scope (open-field-only, siege-CTD guard 2026-07-15):** both entry points short-circuit when `Mission.Current?.IsFieldBattle != true` — the `Patch30` prefix returns `true` (vanilla positioning) on its first line, before the ~40,000×/frame hot path resolves the service or allocates an adapter; `MixedFormationsMissionBehavior.OnMissionTick` returns early, so both the 1s auto-layout apply AND the manual cycle hotkey are inert. `Mission.IsFieldBattle` is true ONLY for `MissionTeamAIType == FieldBattle`, so mixed-formation repositioning never runs in a siege / sally-out / hideout / naval / settlement mission (the guard is a live read — team-AI type is assigned after spawn, so it must not be cached at `OnBehaviorInitialize`). Per ADR-008 these entry-point guards are game-tested, not unit-tested.

### Component Diagram

```
TaomSettings.cs (4 MCM settings: Enable, DefaultLayout, CycleHotkey, Debug)
       │
MixedFormationsSettingsProvider
       │
   MixedFormationsMissionBehavior (per-frame tick)
       │ delegates to
   FormationLayoutService (per-formation state + cache)
       │ uses
   LayoutPositioner (pure math)
       │
       └── IFormationAdapter ── Formation (read-only view)

Patch30 (Harmony Prefix on Formation.GetOrderPositionOfUnit)
   ↓
   FormationAdapter wraps Formation
   FormationLayoutService.ComputeUnitPlanePosition(formation, agentIndex, isRanged)
   ↓
   Vec2 plane position OR null (fall through to vanilla)
```

The Harmony patch and `MissionBehavior` are the boundary classes (ADR-002); they construct `FormationAdapter` instances and pass them to the service. `Hero`, `Agent`, `Formation`, `Team` never cross the service boundary.

## Configuration

### MCM Group: `Battle Tactics / Mixed Formations`

| Setting | Type | Default | Description |
|---|---|---|---|
| `Enable Mixed Formations` | bool | `true` | Master toggle. When off, formations use vanilla positioning. |
| `Default Layout` | int 0–3 | `0` (InfFront) | Auto-applied to mixed formations during the per-second tick. 0=InfFront, 1=RngFront, 2=Wings, 3=Checkerboard |
| `Cycle Layout Hotkey` | string | `"L"` | Bannerlord `InputKey` name. Pressing while a formation is selected cycles its layout; pressing while no formation is selected cycles all formations. |
| `Mixed Formations Debug Mode` | bool | `false` | Show `[MixedFormations]` diagnostic messages on the in-game HUD. Off = file log only. |

### Layout Modes

| Mode | What happens | Best for |
|---|---|---|
| `InfantryFrontRangedBack` (0) | Melee fills rows 0..N starting from the formation's order-position; ranged fills rows N+1.. behind | Default — keeps archers protected |
| `RangedFrontInfantryBack` (1) | Ranged fills front rows; melee fills back | Ambushes / mixed picket lines |
| `RangedWingsInfantryCenter` (2) | Melee in the center column; ranged on left and right wings | Encircle-shooting formations |
| `Checkerboard` (3) | Melee + ranged alternate per square in a checkerboard pattern | Defensive concept formations; rarely useful but the dev built it |

### "Mixed" detection thresholds (matches developer's tested values)

A formation is auto-assigned the default layout only when ALL of:

- ≥ 10 total units
- minority class (whichever is fewer) has ≥ 5 units
- minority share ≥ 20% of total

Pure-class formations (only melee or only ranged) and tiny formations are left alone.

### Two MCM settings the original developer shipped were dead code — removed on port

The original `MixedFormations` module exposed `InfantryRowDepth` (1–10, default 3) and `RangedRowDepth` (1–10, default 2) settings with HintText promising "Rows of infantry when infantry is in front" — but no code in the module ever read either field. The actual `filesPerRow` is computed from formation `Width / (Interval + 1)`. Per the memory rule `feedback_user_facing_promise_must_match_code`, the dead settings were removed on port rather than ship a mismatch.

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/MixedFormations/LayoutPositioner.cs](../../Main/Features/MixedFormations/LayoutPositioner.cs) | Pure-function slot assignment math |
| [Main/Features/MixedFormations/FormationLayoutService.cs](../../Main/Features/MixedFormations/FormationLayoutService.cs) | Per-formation layout dict + assignment cache + cycle/auto-apply orchestration |
| [Main/Features/MixedFormations/IFormationLayoutService.cs](../../Main/Features/MixedFormations/IFormationLayoutService.cs) | Service interface |
| [Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs](../../Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs) | Wraps `TaomSettings.Instance` for testability |
| [Main/Features/MixedFormations/Models/FormationLayoutType.cs](../../Main/Features/MixedFormations/Models/FormationLayoutType.cs) | Enum (Vanilla / 4 layouts) |
| [Main/Features/MixedFormations/Models/SlotAssignment.cs](../../Main/Features/MixedFormations/Models/SlotAssignment.cs) | Cached (row, file) map per formation |
| [Main/Features/MixedFormations/Models/FormationUnit.cs](../../Main/Features/MixedFormations/Models/FormationUnit.cs) | (Index, IsRanged) tuple struct |
| [Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs](../../Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs) | Per-frame tick + hotkey handling + team-adapter construction |
| [Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs](../../Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs) | Harmony Prefix on `Formation.GetOrderPositionOfUnit` |
| [Main/Features/MixedFormations/MixedFormationsIoC.cs](../../Main/Features/MixedFormations/MixedFormationsIoC.cs) | DryIoc registrations |
| [Main/Adapters/IFormationAdapter.cs](../../Main/Adapters/IFormationAdapter.cs) + [FormationAdapter.cs](../../Main/Adapters/FormationAdapter.cs) | Wraps `Formation` (load-bearing for SmartCavalryAI feature 3 + CompanionTactics feature 7) |

## Dependencies

- `IMixedFormationsSettingsProvider` (this feature) — wraps `TaomSettings`; testable
- `IFormationAdapter` (Adapters) — wraps `Formation`
- `IModLogger` (Core/Logging) — TAOM's file logger
- One Harmony patch: `Formation.GetOrderPositionOfUnit` (Prefix)

## Tests

- [TAOM.Tests/Features/MixedFormations/LayoutPositionerTests.cs](../../TAOM.Tests/Features/MixedFormations/LayoutPositionerTests.cs) — 11 tests covering all 4 layouts (block placement, wings, checkerboard parity, no slot overlap), narrow-formation fallback, mid-mission newcomer assignment paths
- [TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs](../../TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs) — 25 tests covering: gating paths (disabled, not-holding, invalid order, no-layout, vanilla-layout), layout get/set/cycle (full 4-step cycle wraparound, empty formation skip), mixed-detection thresholds (5 negative cases), default-applier paths, mission end cleanup

Adapter (`FormationAdapter`) tested only via integration since `Formation` requires a live `Mission`.

## How to add a new layout type

1. Append a new value to [FormationLayoutType.cs](../../Main/Features/MixedFormations/Models/FormationLayoutType.cs).
2. Add a `case` to the `switch` in [`LayoutPositioner.BuildInitialAssignment`](../../Main/Features/MixedFormations/LayoutPositioner.cs) and a private method that fills the assignment.
3. Add a `case` to [`FormationLayoutService.NextLayout`](../../Main/Features/MixedFormations/FormationLayoutService.cs) so cycling reaches the new layout.
4. Update the MCM `MixedFormationsDefaultLayout` integer range (currently 0–3) and the hint text.
5. Add `LayoutPositionerTests` and `FormationLayoutServiceTests` rows for the new layout.

## Performance + Thread Safety

- Service state is per-mission, cleared on `OnEndMission`. Two dictionaries: `Dictionary<object, FormationLayoutType>` (~4 entries) + `Dictionary<object, SlotAssignment>` (~4 entries). Negligible.
- Per-frame work: `OnMissionTick` accumulates dt; once per second calls `ApplyDefaultsToFormations` (one pass over player team formations, ~4 formations); every frame polls `Input.IsKeyDown` for the cycle hotkey. No allocations in the hot path.
- Per-position-query work: lock acquire + dictionary lookup + 1 conditional `BuildInitialAssignment` if the cache miss, lock release, then `Vec2` math (lock-free). The `BuildInitialAssignment` is the only ω(N) operation and runs once per layout change per formation.
- `Patch30` caches the `IFormationLayoutService` resolve via static `??=` field — fires once per process, then the dict lookup is bypassed on every subsequent per-unit Prefix call. Per `harmony-patches.md` hot-path-reflection-caching pattern. Caught by deep-review Agent 3.
- **Thread safety:** Bannerlord runs Formation positioning queries across worker threads (vanilla `Formation.OrderPositionLock`, `IsFormationUnitPositionAvailableAuxMT` uses `TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock)`, `_MT` suffix on `CreateNewOrderWorldPositionMT` etc.). Patch30 fires from those threads, so all dict + `SlotAssignment.ByAgentIndex` mutations in `FormationLayoutService` are protected by a `private readonly object _lock`. Reads on the hot path lock briefly (~25ns uncontended); pure math runs outside the critical section. Caught by Codex review #36; codified in memory entry `feedback_detect_engine_threading_via_mt_suffix`.
- **Vanilla safety gate replicated:** Patch30 calls `Mission.IsFormationUnitPositionAvailable(ref candidate, team)` before setting `__result`. Vanilla Hold path delegates to `GetOrderPositionOfUnitAux` which validates the candidate against the navmesh and falls back to `unit.GetWorldPosition()` if unavailable. Our skip would have dropped that gate — custom layout positions could land on cliffs, walls, siege props, or non-navigable terrain. Caught by Codex review #36; codified in memory entry `feedback_replicate_vanilla_safety_gates_in_prefix`.

## Known Limitations

Both surfaced by deep-review Agent 5 (Data Flow):

1. **Once a layout is assigned to a formation, it persists for the entire mission regardless of subsequent unit-composition changes.** A formation that started as 8 melee + 6 ranged (mixed) gets assigned the default layout, then loses all 6 ranged in combat, then continues to be positioned by TAOM (`InfantryFrontRangedBack` with no ranged block — geometrically equivalent to vanilla in that case). The auto-applier (`ApplyDefaultsToFormations`) checks `_layoutByFormation.ContainsKey(...)` and skips already-assigned formations, so it never re-evaluates `IsMixedFormation` after the first assignment. Acceptable behavior — the alternative (re-evaluate every tick and remove layout if no longer mixed) would burn CPU and could cause visual jitter as units snap between layouts. Layout is cleared only by `OnMissionEnd` or an explicit `SetLayout` call.

2. **Pressing the cycle hotkey within the first ~1 second of a mission silently does nothing.** `CycleLayouts` only cycles formations already in `_layoutByFormation`; the auto-applier runs once per second; until the first auto-apply fires, no formations are in the dict. After 1s, cycling works normally. Not surfaced to the user via any HUD message.

## Verification

In-game golden path:

1. Start a campaign, recruit a mixed party (≥10 troops with ≥5 archers/crossbowmen and ≥5 melee).
2. Enter a battle. Press `F1+F1` to deploy the troops as one formation.
3. MCM → TAOM → "Battle Tactics / Mixed Formations" → confirm `Enable=true`, `DefaultLayout=0 (InfFront)`, `CycleHotkey=L`.
4. Order the formation to a position (right-click). Wait until they hold position.
5. Confirm the layout: melee in front rows, ranged behind. (Visual: archers should be a row or two behind the front-rank polearms/sword troops.)
6. Press `L` while the formation is selected — confirm a `[MixedFormations] Layout (selected) → Ranged front, Infantry back` HUD message and the formation re-orders with archers in front.
7. Press `L` two more times — should cycle to Wings then Checkerboard.

Disable round-trip:

1. MCM → set `Enable Mixed Formations = false` → reload campaign / re-enter battle.
2. Confirm one `[MixedFormations] disabled via MCM — patches inert` line in `rgl_log.txt`.
3. Order a mixed formation to hold — should use vanilla positioning (units intermixed by Index order).

Debug-mode round-trip:

1. MCM → set `Mixed Formations Debug Mode = true`.
2. Trigger an auto-default-apply or a hotkey cycle.
3. Confirm `[MixedFormations] auto-assigned default layout ...` and `[MixedFormations] cycled N formation(s) → ...` lines appear both in `rgl_log.txt` and on the in-game HUD with the `[MixedFormations]` prefix.

## Changelog

- 2026-05-13 — Added SmartCavalryAI × MixedFormations handshake tests (3 tests pinning the `RepresentativeIsCavalry` guards in `FormationLayoutService` so a refactor can't re-introduce the charge-line overwrite).
- 2026-05-06 — Ported the external `MixedFormations` sibling module into `Main/Features/` (Patch30 Prefix on `Formation.GetOrderPositionOfUnit`, adapter/service/IoC pattern, 4 MCM settings, 36 unit tests); fixed Codex review findings (navmesh validation + thread-safety lock) and deep-review findings (hot-path service caching in Patch30 + `default:` guard in `LayoutPositioner` switch).

## GitHub Issue

- **Issue:** TBD (create with `/issue feature MixedFormations integration` before commit)
- **Status:** In progress — Phase 1 (port to Main/Features/) complete; awaiting in-game verification.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
