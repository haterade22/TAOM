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
3. **Cache assignments per formation**: recomputing the slot grid for every unit on every position query would be O(N²) per formation per tick. Build the assignment once, lazily: setting or cycling a layout only drops the formation's cached assignment, and the next position query builds it, on whichever thread asks (Patch30 runs on the engine's worker threads). Then look up by `Agent.Index`.

### Solution Approach

The feature splits into three layers:

- **`LayoutPositioner` (pure function):** given formation geometry (width, interval, units list) and a layout type, computes a fresh `SlotAssignment` mapping each unit's `Agent.Index` to a (row, file) offset. Pure math, fully unit-testable without a live mission.
- **`FormationLayoutService` (singleton):** holds the per-formation layout choice with the adapter that set it (a `ConcurrentDictionary` keyed by reference, written under the service's lock and read without it) and the per-formation cached assignment (`Dictionary<formationKey, SlotAssignment>`, under the lock). Drives the `IsMixedFormation` heuristic for the auto-default-applier and the `CycleLayouts` rotation. `FindLaidOutFormation` answers the worker-thread prefix for a formation with no layout without taking the lock.
- **`MixedFormationsMissionBehavior` (engine bridge):** ticks every frame: every 1s, asks the service to apply default layouts to mixed formations on the player team; every frame, polls the configured cycle hotkey and rotates layouts on the selected formations (or all if none selected). Resolves player team and selected-formations adapters; passes them to the service.
- **`Patch30_FormationGetOrderPositionOfUnit` (Harmony Prefix):** intercepts vanilla; returns `true` at once for a formation `FindLaidOutFormation` does not know; otherwise calls the service; if the service returns a `Vec2` plane position, queries `Mission.Current.Scene.GetGroundHeightAtPosition` for ground Z, builds a `WorldPosition`, and returns `false` to skip vanilla. Otherwise returns `true`.

**Mission-type scope (open-field-only, siege-CTD guard 2026-07-15):** both entry points short-circuit when `Mission.Current?.IsFieldBattle != true`: the `Patch30` prefix returns `true` (vanilla positioning) on its first line, before the per-unit worker-thread path resolves the service; `MixedFormationsMissionBehavior.OnMissionTick` returns early, so both the 1s auto-layout apply AND the manual cycle hotkey are inert. `Mission.IsFieldBattle` is true ONLY for `MissionTeamAIType == FieldBattle`, so mixed-formation repositioning never runs in a siege / sally-out / hideout / naval / settlement mission (the guard is a live read: team-AI type is assigned after spawn, so it must not be cached at `OnBehaviorInitialize`). Per ADR-008 these entry-point guards are game-tested, not unit-tested.

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
   fall through to vanilla when: not a field battle │ unit is a banner bearer
   ↓
   FormationLayoutService.FindLaidOutFormation(Formation)   no layout: fall through to vanilla
   ↓  (the adapter the mission behaviour passed when it set the layout)
   FormationLayoutService.ComputeUnitPlanePosition(formation, agentIndex, isRanged)
   ↓
   Vec2 plane position OR null (fall through to vanilla)
```

### Fall-through cases (do not remove)

Patch30 returns `false` — suppressing vanilla — for every unit it positions. That makes it a **silent monopoly** on `GetOrderPositionOfUnit`: any other feature that relies on the vanilla path breaks against it with no error and no crash. Two cases deliberately fall through:

| Case | Why |
|---|---|
| `Mission.IsFieldBattle != true` | Open-field only. Siege, sally-out, hideout, naval and settlement missions keep vanilla positioning. Placed first to short-circuit the hot path before any IoC resolve. |
| `unit?.Banner != null` | **Banner bearers own their slot.** `BannerBearerLogic` places them via `Formation.SwitchUnitLocations` into the engine's dedicated `RelativeFormationPosition[6]` banner positions; returning a mixed-formation position instead scatters the standards through the ranks. Added 2026-07-16 (Codex review 74, MED) — see [banner-bearers.md](./banner-bearers.md). `Agent.Banner` is `Equipment?.GetBanner()`, a single `_weaponSlots[4]` read with no loop or allocation, so it is cheap enough to sit on this path. |

**Rule for future work:** when another TAOM feature starts producing a new *kind* of unit the engine positions specially, add a fall-through here rather than letting Patch30 override it. Flagging the interaction as "untested" in a doc is not the same as resolving it — that exact gap shipped the banner-bearer misplacement past a 5-agent review that had been briefed on it.

The Harmony patch and `MissionBehavior` are the boundary classes (ADR-002). The `MissionBehavior` constructs the `FormationAdapter` instances and passes them to the service; the patch reuses the adapter the service holds for a laid-out formation and builds none. `Hero`, `Agent`, `Formation`, `Team` never cross the service boundary.

## Configuration

### MCM Group: `Battle Tactics / Mixed Formations`

| Setting | Type | Default | Description |
|---|---|---|---|
| `Enable Mixed Formations` | bool | `true` | Master toggle. When off, formations use vanilla positioning. |
| `Default Layout` | int 0–3 | `0` (InfFront) | Auto-applied to mixed formations during the per-second tick. 0=InfFront, 1=RngFront, 2=Wings, 3=Checkerboard |
| `Cycle Layout Hotkey` | string | `"L"` | Bannerlord `InputKey` name. Pressing while a formation is selected cycles its layout; pressing while no formation is selected cycles all formations. |
| `Mixed Formations Debug Mode` | bool | `false` | Gates two `LogDebug` lines (the auto-assigned default layout and the cycled layout), written to `Logs/taom_debug_*.log`. It prints nothing on the HUD; the cycle hotkey's yellow `[MixedFormations] Layout (...)` HUD message shows whatever this is set to. |

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
- [TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs](../../TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs): gating paths (disabled with a layout set, not-holding, invalid order, no-layout, vanilla-layout), layout get/set/cycle (full 4-step cycle wraparound, empty formation skip), mixed-detection thresholds (5 negative cases), default-applier paths, mission end cleanup; plan 032: the lock-free path for a formation with no layout (one key read, never waits for the lock), a position check for every layout (a composition check: the expectation reuses `LayoutPositioner`'s own slots and the service's pitch formula on one fake geometry, so it is not an independent oracle), `FindLaidOutFormation` across every writer and by reference, the locked re-read after a stale lookup, the cavalry gate across an expired flag in both directions (a fake that follows `QueryData`'s refresh rule), and both fallback log lines pinned literally
- [TAOM.Tests/Features/MixedFormations/Patch30ThreadSafetyIlTests.cs](../../TAOM.Tests/Features/MixedFormations/Patch30ThreadSafetyIlTests.cs): structural IL checks only. They show that the prefix contains no `FormationAdapter` construction, a call to `FindLaidOutFormation` and a call to `NoteFallback`, that `FormationKey` returns the wrapped `Formation`, and that `RepresentativeIsCavalry` reads the evaluating `IsCavalryFormation`. A call being present does not prove its order or the control flow around it (that the catch makes the `NoteFallback` call, say); the prefix itself is game-tested (ADR-008)

The adapter's behaviour (`FormationAdapter`) is tested only via integration since `Formation` requires a live `Mission`; the IL checks above only show which engine members two of its getters read.

## How to add a new layout type

1. Append a new value to [FormationLayoutType.cs](../../Main/Features/MixedFormations/Models/FormationLayoutType.cs).
2. Add a `case` to the `switch` in [`LayoutPositioner.BuildInitialAssignment`](../../Main/Features/MixedFormations/LayoutPositioner.cs) and a private method that fills the assignment.
3. Add a `case` to [`FormationLayoutService.NextLayout`](../../Main/Features/MixedFormations/FormationLayoutService.cs) so cycling reaches the new layout.
4. Update the MCM `MixedFormationsDefaultLayout` integer range (currently 0–3) and the hint text.
5. Add `LayoutPositionerTests` and `FormationLayoutServiceTests` rows for the new layout.

## Performance + Thread Safety

- Service state is per-mission, cleared on `OnEndMission`. Two maps: the layouts (`ConcurrentDictionary<object, (IFormationAdapter, FormationLayoutType)>`, ~4 entries) + `Dictionary<object, SlotAssignment>` (~4 entries), both keyed by reference (`ReferenceIdentity`). Negligible.
- Per-frame work: `OnMissionTick` accumulates dt; once per second calls `ApplyDefaultsToFormations` (one pass over player team formations, ~4 formations); every frame polls `Input.IsKeyDown` for the cycle hotkey and parses its setting string with `Enum.TryParse`, which this line does not cache and which allocates (about 112 bytes per call, measured on the .NET Framework 2026-11-05, so about 7 KB a second at 60 FPS). The once-per-second pass allocates a list and a `FormationAdapter` per player formation; when a default layout is set, each formation with no layout also walks `FormationAdapter.Units`, a LINQ query that ends in `ToList`.
- Per-position-query work: a formation with no layout costs one lock-free lookup and nothing else; a laid-out formation takes the lock for the dictionary lookup and 1 conditional `BuildInitialAssignment` on a cache miss, then does the `Vec2` math (lock-free). The laid-out path still reads engine query state on the worker, as it did before this change: its cavalry gate reads the evaluating `IsCavalryFormation`, and the pitch (`Math.Max(1, formation.Interval + 1)`) reads `Formation.Interval`, which reads the evaluating `CavalryUnitRatio` (2.5 s lifetime; v1.4.8 `Formation.cs:512-534`, `FormationQuerySystem.cs:431`). A read of an expired member refreshes the formation's whole class-ratio sync group (`FormationQuerySystem.cs:440`) on whichever thread asks first, and while the group is fresh the gate is a clock check. The gate keeps the evaluating read on purpose: a mass transfer or split only expires the group (`QuerySystem.Expire()` in `Formation.OnMassUnitTransferEnd`, `TransferUnits` and `Split`), so a cached flag would answer from the old composition and the pitch's `Interval` read would then refresh it after the call had already returned a position (1.5.x Codex review of plan 032, finding 1). Vanilla reads `Interval` on these workers too: `HumanAIComponent.ParallelUpdateFormationMovement` calls `Formation.GetCurrentGlobalPositionOfUnit` (v1.4.8 `HumanAIComponent.cs:685`), which reads it (`Formation.cs:1284`). The `BuildInitialAssignment` is the only ω(N) operation and runs once per layout change per formation.
- `Patch30` caches the `IFormationLayoutService` resolve via static `??=` field — fires once per process, then the dict lookup is bypassed on every subsequent per-unit Prefix call. Per `harmony-patches.md` hot-path-reflection-caching pattern. Caught by deep-review Agent 3.
- **Thread safety:** Bannerlord runs Formation positioning queries across worker threads (vanilla `Formation.MovementOrderPositionLock`, `IsFormationUnitPositionAvailableAuxMT` uses `TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock)`, `_MT` suffix on `CreateNewOrderWorldPositionMT` etc.). Patch30 fires from those threads, so every layout write, the slot cache and `SlotAssignment.ByAgentIndex` in `FormationLayoutService` are protected by a `private readonly object _lock`. The slot lookup for a laid-out formation locks briefly and re-reads the layout under the lock; a formation with no layout is answered with no lock from the layouts' `ConcurrentDictionary`; the pitch read and the plane transform run outside the critical section, and the pitch read reaches the engine's query system, which this lock cannot protect (see the previous bullets). Caught by Codex review #36; codified in memory entry `feedback_detect_engine_threading_via_mt_suffix`.
- **Vanilla safety gate replicated:** Patch30 calls `Mission.IsFormationUnitPositionAvailable(ref candidate, team)` before setting `__result`. Vanilla Hold path delegates to `GetOrderPositionOfUnitAux` which validates the candidate against the navmesh and falls back to `unit.GetWorldPosition()` if unavailable. Our skip would have dropped that gate — custom layout positions could land on cliffs, walls, siege props, or non-navigable terrain. Caught by Codex review #36; codified in memory entry `feedback_replicate_vanilla_safety_gates_in_prefix`.

## Log lines

New in plan 032 (`Logs/taom_debug_<timestamp>.log`, one file per launch, through `IModLogger`):

| Line | Level | When | Fields |
|---|---|---|---|
| `[MixedFormations] Patch30 position prefix threw; this unit and every later failing one this mission fall back to vanilla positioning (the first is logged in full, the count at mission end): <exception>` | WARNING | The first time the `Patch30` prefix body throws in a mission; the unit takes vanilla's position, and so does every later unit whose call throws, counted for the summary below | `<exception>` is the whole `Exception.ToString()` of the mission's first failure: type, message and stack |
| `[MixedFormations] mission ended: <count> unit position(s) fell back to vanilla after a Patch30 throw` | INFO | At mission end, when at least one call threw that mission; the count then resets, so the next mission's first throw is logged in full again | `<count>`: every prefix call that threw and fell back to vanilla this mission, the first included |

Examples: `[MixedFormations] Patch30 position prefix threw; this unit and every later failing one this mission fall back to vanilla positioning (the first is logged in full, the count at mission end): System.InvalidOperationException: boom` and `[MixedFormations] mission ended: 3 unit position(s) fell back to vanilla after a Patch30 throw`. `FormationLayoutService.NoteFallback` writes the first and `OnMissionEnd` the second; `FormationLayoutServiceTests.FallbackLine_NamesTheConsequenceAndCarriesTheWholeException` and `FallbackSummaryLine_CountsTheMissionsFallbacks` pin both formats. Before plan 032 the same throw fell back silently.

## Known Limitations

Both surfaced by deep-review Agent 5 (Data Flow):

1. **Once a layout is assigned to a formation, it persists for the entire mission regardless of subsequent unit-composition changes.** A formation that started as 8 melee + 6 ranged (mixed) gets assigned the default layout, then loses all 6 ranged in combat, then continues to be positioned by TAOM (`InfantryFrontRangedBack` with no ranged block, geometrically equivalent to vanilla in that case). The auto-applier (`ApplyDefaultsToFormations`) checks `_layouts.ContainsKey(...)` and skips already-assigned formations, so it never re-evaluates `IsMixedFormation` after the first assignment. Acceptable behavior: the alternative (re-evaluate every tick and remove layout if no longer mixed) would burn CPU and could cause visual jitter as units snap between layouts. Layout is cleared only by `OnMissionEnd` or an explicit `SetLayout` call.

2. **Pressing the cycle hotkey within the first ~1 second of a mission silently does nothing.** `CycleLayouts` only cycles formations already in `_layouts`; the auto-applier runs once per second; until the first auto-apply fires, no formations are in the dict. After 1s, cycling works normally. Not surfaced to the user via any HUD message.

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
2. Confirm one `[MixedFormations] disabled via MCM — patches inert` line in the newest `Logs/taom_debug_*.log`.
3. Order a mixed formation to hold — should use vanilla positioning (units intermixed by Index order).

Debug-mode round-trip:

1. MCM → set `Mixed Formations Debug Mode = true`.
2. Trigger an auto-default-apply or a hotkey cycle.
3. Confirm `[MixedFormations] auto-assigned default layout ...` and `[MixedFormations] cycled N formation(s) → ...` lines appear in the newest `Logs/taom_debug_*.log` (they are `LogDebug` lines, so they never reach the HUD or `rgl_log.txt`). With the toggle off they are absent, while a hotkey cycle still prints its HUD message.

## Changelog

- 2026-11-05 (port of 1.5.x plan 032, #714): almost no formation has a layout, so
  `FindLaidOutFormation` answers such a formation with no lock, no adapter allocation and no
  `FormationQuerySystem` read. The layouts live in one `ConcurrentDictionary` keyed by reference
  (`ReferenceIdentity`); every write and the laid-out slot lookup still hold `_lock`, so the #595
  rule below holds for every write. The cavalry gate keeps its evaluating read. A throw inside the
  prefix, silent before, logs its first occurrence per mission in full and a count at mission end.
- 2026-11-04 (#606, ported from the 1.5.x fix of 2026-09-16): the per-mission MCM log moved from `OnBehaviorInitialize` (which the engine never
  dispatches to a behavior added from `SubModule.OnMissionBehaviorInitialize`) to `AfterStart`. Diagnostic
  only; the tick and the hotkey never depended on it.
- 2026-09-13 (#595): `FormationLayoutService.ForgetAgent(agentIndex)` drops the index from every cached
  `SlotAssignment.ByAgentIndex`, called from `MixedFormationsMissionBehavior.OnAgentDeleted`, because the engine
  hands a deleted agent's index to the next agent it builds and a reinforcement was inheriting the dead unit's
  slot. Codex review 109 the same day: forgetting the mapping alone left `NextMeleeIndex`/`NextRangedIndex`
  growing, so each replacement took a row deeper and, in the front/back layouts, landed on the other class's
  rows; `SlotAssignment` now records each unit's class and holds a vacated slot for the next unit of that
  class, and `AssignNextSlot` reclaims it before any counter advances (`RepeatedTurnover_...` pins it). Also
  recorded: Patch30 runs on the TWParallel WORKER POOL (`HumanAIComponent.ParallelUpdateFormationMovement`
  -> `Agent.GetBaseFormationFrame` -> `Formation.GetOrderPositionOfUnit`), not only the async tick thread; the
  service's `_lock` is what keeps that safe. Never remove it.
- 2026-05-13 — Added SmartCavalryAI × MixedFormations handshake tests (3 tests pinning the `RepresentativeIsCavalry` guards in `FormationLayoutService` so a refactor can't re-introduce the charge-line overwrite).
- 2026-05-06 — Ported the external `MixedFormations` sibling module into `Main/Features/` (Patch30 Prefix on `Formation.GetOrderPositionOfUnit`, adapter/service/IoC pattern, 4 MCM settings, 36 unit tests); fixed Codex review findings (navmesh validation + thread-safety lock) and deep-review findings (hot-path service caching in Patch30 + `default:` guard in `LayoutPositioner` switch).

## GitHub Issue

- **Issue:** #714 (perf: make the worker-thread formation patch lock-free for formations without a layout)
- **Status:** The port (2026-05-06) and the lock-free lookup for formations with no layout (`FindLaidOutFormation`, plan 032) are in the code. This file does not record whether #714 is closed or the in-game check done: run `gh issue view 714`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/banner-bearers.md](./banner-bearers.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
