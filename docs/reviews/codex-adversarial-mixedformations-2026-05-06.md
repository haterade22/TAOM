# Codex Adversarial Review — MixedFormations

**Target:** working tree diff
**Verdict:** needs-attention
**Date:** 2026-05-06
**Output recovery note:** Codex could not write this file directly (`apply_patch` rejected by read-only sandbox); the verdict and findings below are reconstructed from Codex's stdout. `ilspycmd` was also blocked by shell policy during the run, so vanilla code blocks below were verified separately by Claude via direct `ilspycmd` invocation outside the sandbox.

## Summary (verbatim from Codex)

> No-ship: Patch30 can replace vanilla Hold positioning with unvalidated world positions, and the per-formation cache is still mutable from a hot Prefix path. Requested review-file write was rejected by the read-only sandbox.

## Findings

### [HIGH] Custom Hold positions bypass vanilla navmesh availability checks

**Location:** [Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs:38-41](../../Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs)

Patch30 returns false after constructing a `WorldPosition` directly from the computed `Vec2` and `Scene.GetGroundHeightAtPosition`. The decompiled vanilla Hold branch returns `GetOrderPositionOfUnitAux`, which uses `Arrangement`-owned world positions and, when synthesizing a fallback, routes through `CreateNewOrderWorldPositionMT(...NavMeshVec3)` plus `unit.Mission.IsFormationUnitPositionAvailable` before returning it. This TAOM path skips those availability/boundary gates, so custom rows/wings can order units into non-navigable or blocked terrain near walls, cliffs, siege props, or map boundaries.

**Recommendation:** Before returning false, validate the candidate through Formation/Mission world-position APIs, including navmesh/boundary availability. If the custom position is not valid, return true and let vanilla handle the unit.

**Claude verdict:** CONFIRMED. Verified via `ilspycmd` against installed v1.3.15 `TaleWorlds.MountAndBlade.dll`:

```csharp
// Vanilla Formation.GetOrderPositionOfUnit (Hold case):
case MovementOrder.MovementStateEnum.Hold:
    return GetOrderPositionOfUnitAux(unit);

// Vanilla GetOrderPositionOfUnitAux:
private WorldPosition GetOrderPositionOfUnitAux(Agent unit)
{
    WorldPosition? worldPositionOfUnitOrDefault = Arrangement.GetWorldPositionOfUnitOrDefault(unit);
    if (worldPositionOfUnitOrDefault.HasValue)
        return worldPositionOfUnitOrDefault.Value;
    // ...
    WorldPosition unitPosition = _movementOrder.CreateNewOrderWorldPositionMT(this, WorldPosition.WorldPositionEnforcedCache.NavMeshVec3);
    if (unit.Mission.IsFormationUnitPositionAvailable(ref unitPosition, Team))
        return unitPosition;
    return unit.GetWorldPosition();  // ← fallback when navmesh-unavailable
}
```

The vanilla path falls back to the unit's current position if the candidate fails the navmesh check. Our patch did not replicate this safety. Real risk on rough terrain (cliffs, walls, siege props near formations).

**Fix applied:** [`Patch30_FormationGetOrderPositionOfUnit.Prefix`](../../Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs) now calls `mission.IsFormationUnitPositionAvailable(ref candidate, team)` before setting `__result`. If unavailable → returns `true` (vanilla handles via its own fallback to `unit.GetWorldPosition()`).

### [MEDIUM] Assignment cache is mutated from the hot Prefix path without synchronization

**Location:** [Main/Features/MixedFormations/FormationLayoutService.cs:153-159](../../Main/Features/MixedFormations/FormationLayoutService.cs)

`FormationLayoutService` uses regular `Dictionary` caches and mutable `SlotAssignment` state. `ComputeUnitPlanePosition` can call `EnsureAssignment` on cache miss, and lines 61-64 mutate `SlotAssignment.ByAgentIndex` for new agents. Because Patch30 can fire per unit during formation position recalculation, a first-frame cache miss or mid-mission reinforcement can produce concurrent writes if Bannerlord evaluates unit positions from worker contexts; the vanilla code path includes MT-named positioning helpers, so this cannot be dismissed without installed-DLL/runtime evidence. Impact is battle-time exceptions or corrupted layout state.

**Recommendation:** Make the Prefix read-only by precomputing assignments on the mission tick, or guard all layout/cache and `SlotAssignment` mutations with a lock. Prefer immutable per-formation assignments for the Prefix hot path.

**Claude verdict:** CONFIRMED. Verified via `ilspycmd` — engine-side threading is real:

- `Formation.OrderPositionLock { get; private set; } = new object();` — vanilla Formation has its own lock object for positioning state.
- `Mission.IsFormationUnitPositionAvailableAuxMT` uses `using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))` — confirms multi-threaded position queries against physics.
- The `_MT` suffix on `CreateNewOrderWorldPositionMT`, `IsFormationUnitPositionAvailableMT`, `GetNavMeshMT` is consistent: all are multi-thread-safe variants meant to be called from worker threads.

Bannerlord absolutely runs Formation positioning queries across worker threads. Our Patch30 fires from those threads. The dict + `ByAgentIndex` mutations on the hot path can race against `OnMissionTick`'s `CycleLayouts` / `ApplyDefaultsToFormations` from the main thread.

**Fix applied:** Added `private readonly object _lock = new();` to [`FormationLayoutService`](../../Main/Features/MixedFormations/FormationLayoutService.cs). All dict + `SlotAssignment.ByAgentIndex` mutations (and reads on the hot path) now hold the lock. Single uncontended lock acquisition is ~25ns on x86. Reading the slot inside the lock then doing pure math outside the lock keeps the critical section minimal. Two regression tests added: `ConcurrentTaskBattery_SetLayoutAndCompute_DoesNotThrowOrCorruptCache` (8 tasks × 100 ops) and `ComputeAndCycle_RapidSequentialAlternation_RemainsCoherent` (sequential 100-iteration cache miss/hit alternation). True concurrency stress is in-game integration only.

## Things Codex did particularly well

1. **Caught the missing vanilla gate that the prior `/deep-review` Agent 5 (Data Flow) cleared.** Agent 5 explicitly examined the vanilla side-effect question and concluded "for Hold-state formations, the vanilla path is essentially read-only — safe to skip." Codex went one level deeper into the call chain (`GetOrderPositionOfUnit` → `GetOrderPositionOfUnitAux`) and found the navmesh validation gate Agent 5 missed. The lesson: agents that say "vanilla path is read-only" need to trace not just the entry method but every helper it calls.
2. **Detected engine multi-threading from the `_MT` suffix and `TWSharedMutexReadLock` patterns**, even without runtime instrumentation. Pure-static reading produced the right hypothesis.
3. **Calibrated severity correctly**: HIGH for navmesh bypass (real player-visible bug), MEDIUM for thread-safety (theoretical race that's hard to reproduce reliably).

## Things Codex did less well

1. **Could not write the output review file due to sandbox `apply_patch` rejection.** Required Claude to reconstruct from stdout. Same as SiegeDismount review #34.
2. **Could not run `ilspycmd` due to shell policy** — vanilla decompilation code blocks were not produced inline. Claude verified separately. Codex did fall back to using the pre-decompiled tree at `E:\Decompiled_Bannerlord\` (which is v1.4) — and explicitly flagged this caveat: "This conclusion uses the allowed pre-decompiled tree because ilspycmd against installed DLLs was blocked by shell policy." Honest reporting; acceptable workaround.
3. **Did not engage with the Known Suspects format**, again. The prompt had 9 explicit Known Suspects (2 confirming /deep-review fixes, 7 new attack lines); Codex reported its own 2 findings instead of working through the suspect list. Suspects 3 (FormationAdapter.Units allocation), 5 (Input.IsKeyDown semantics), 6 (Width/Interval edge cases), 7 (Direction zero-vector), 8 (singleton lifecycle), 9 (TaomSettings cross-coordination) were never addressed. The prompt's emphasis "ENGAGE WITH KNOWN SUSPECTS THIS TIME" did not change Codex's behavior. **Lesson for future prompts:** the Known Suspects framing may not be respected; the high-value findings come from Codex's own deep-read regardless. Don't over-invest in writing exhaustive suspect lists — write 2-3 focused ones and leave Codex room.

## Root Cause Analysis (Phase 3e)

Per CLAUDE.md mandate "Phase 3e applies to EVERY confirmed bug, not just HIGH ones."

| # | Bug | Category | Why missed | Preventive action |
|---|-----|----------|-----------|------------------|
| 1 | Patch30 bypassed vanilla navmesh availability check | Missing vanilla gate / incomplete call-chain trace | The /deep-review Agent 5 traced `Formation.GetOrderPositionOfUnit` itself but not the helper `GetOrderPositionOfUnitAux` it delegates to in the Hold branch. The agent's verdict "for Hold-state formations the vanilla path is essentially read-only" was based on the entry method only; the safety gate (`Mission.IsFormationUnitPositionAvailable`) lives one frame deeper. | New feedback memory: `feedback_replicate_vanilla_safety_gates_in_prefix.md` — when a Harmony Prefix returns `false` to skip vanilla, decompile the FULL call chain including all delegate helpers, and replicate any safety gates (navmesh validation, bounds/team/season checks, fallback paths). The entry method's signature isn't enough — read the body and follow every method it calls. |
| 2 | Cache + assignment mutations from worker-thread Prefix without synchronization | Missing concurrency awareness / engine-threading inference | Did not notice the `_MT` suffix on Bannerlord positioning helpers (`CreateNewOrderWorldPositionMT`, `IsFormationUnitPositionAvailableMT`, `GetNavMeshMT`) which is the engine's convention for multi-threaded helpers. Did not search for `TWSharedMutexReadLock` or `Formation.OrderPositionLock` in the vanilla source. | New feedback memory: `feedback_detect_engine_threading_via_mt_suffix.md` — when patching a `Formation`/`Mission`/`Scene`/positioning method, grep the same vanilla type for `_MT` suffix methods and `TWSharedMutexReadLock`/`PhysicsAndRayCastLock` patterns. If present, the engine threads queries onto worker contexts; the patch (and any service it calls) must be thread-safe via lock or immutable state. |

Both root causes generalize beyond MixedFormations and apply to features 3 (SmartCavalryAI — also patches Formation), 4 (FiefManagement — patches MapScreen.OnFrameTick), 5 (QuickActions — patches SPInventoryVM, less likely threaded but worth checking), and 7 (CompanionTactics — patches multiple VMs and Mission.OnTick). Memory files codify both lessons for every future Claude session per this repo's auto-memory contract.

## Next steps

- Build green (verified — 1515/1515 tests pass after fixes).
- Update CHANGELOG.md with the two Codex-driven fixes.
- Update [docs/features/mixed-formations.md](../features/mixed-formations.md) — note the navmesh-validation behavior + thread-safety hardening; remove the "Pure math; lock-free" claim from the Performance section.
- Update [AGENTS.md](../../AGENTS.md) "What Codex does well" with the call-chain-tracing and `_MT`-suffix-detection lessons; bump review/bug counts.
- Add this review to [REVIEW-LOG.md](REVIEW-LOG.md) as Review #36.
- Codify both RCA preventive actions as feedback memories under `C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/`.
