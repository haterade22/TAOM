# RCA — Shader Pre-compilation 1.4.7 deployment-NRE (#336)

**Date:** 2026-07-11
**Feature:** `Main/Features/ShaderPrecompilation/` (1.4.7 fix)
**Trigger:** user report — "precompile shader is getting stuck for a long time on 1.4.7; worked fine on 1.4.6."
**Outcome:** root-caused to a 1.4.7 engine regression; fixed with a scoped `MissionLogic` guard + robustness package; in-game confirmed (13/13 items, 8m 6s, 0 NRE, 0 hang). Deep-review (5 agents) returned **0 functional defects**.

## Top-line

The bug was **not** TAOM's code and **not** the shader counter (the first hypothesis). It was a genuine **1.4.7 engine regression**: `DeploymentMissionController.SetupTeams()` (and `FinishDeployment()`) gained an **unconditional** deref of `Mission.InitialPlayerAgent` — the new `AgentControllerType` hand-control lines — which is `null` in TAOM's headless (no-human) precompile battle, so it NREs every mission tick. Once the NRE was guarded, a **second** symptom surfaced: the all-characters battle opened the Order-of-Battle deployment view and froze headless (no player to click *Deploy*).

The most valuable lessons are not the deep-review findings (which were docs + one comment nit) but two things the **in-game test** caught that offline review structurally could not.

## Findings table

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | HIGH (engine) | 1.4.7 `DeploymentMissionController.SetupTeams` unconditionally derefs null `InitialPlayerAgent` on a headless battle → NRE | Engine regression / State-lifecycle | The 1.4.7 bump dispositioned shader-precompile "unaffected" because binding tests pass (the method still *exists*); a behavior-only regression in an engine method TAOM's headless path uniquely triggers was never exercised in-game | Engine-bump checklist must in-game-exercise headless/unusual code paths, not just green the binding tests (below) |
| 2 | HIGH (self-inflicted) | Guard added from `TaomShaderGameManager.OnLoadFinished` via `Mission.Current?.AddMissionBehavior` silently no-op'd — `Mission.Current` isn't the battle mission yet → guard never registered, NRE still fired | Lifecycle / API-timing | Deep-review can't catch it: by review time the wiring was already corrected. Caught only in-game (guard absent from the `[MissionDiag]` behavior dump) | LESSONS-LEARNED rule: add mission behaviors to a freshly-opened mission via `OnMissionBehaviorInitialize`, never `Mission.Current.AddMissionBehavior` from a game manager's `OnLoadFinished` |
| 3 | MED | Warm shader cache masked the deployment-view hang — the character battle completed in 20s (shaders cached, settled before deployment mattered), so the force-finish path was never exercised in the "successful" run | Verification / test-environment | A fast completion was nearly read as full validation; only the per-item log detail (no seed, no force-finish, done in 20s vs the prior run's hang) revealed it | LESSONS-LEARNED rule: a warm-cache pass is not proof the cold path works; validate cold for render/compile/deployment fixes |
| 4 | LOW | Guard's "fail-safe... must never break the walk" comment overstated protection for the near-unreachable `SetValue`-throws / field-drift sub-case | Doc precision | Data-flow agent caught it | Comment corrected to name the real guard (the binding test), not the runtime catch |
| 5 | (process) | Feature doc + CHANGELOG entry owed | Completeness | Expected — queued for post-review | Both written this session |

## Root-cause pattern

**An engine bump can regress a feature whose managed *bindings* are unchanged, when the feature drives an engine code path in an *unusual* way.** TAOM's precompile is the only place that opens a battle with **no player-controlled agent**. Every normal battle spawns one, so `InitialPlayerAgent` is non-null and 1.4.7's new deref is harmless — which is exactly why TaleWorlds shipped it and why normal play (and TAOM's own campaign/siege/tournament battles) are unaffected. The regression is invisible to:
- **binding tests** — the method still exists with the same signature; only its *body* changed;
- **the decompile dump** — the category tree said "unaffected"; only a line-level diff of `SetupTeams` against the 1.4.6 baseline shows the new deref;
- **the 5 deep-review agents** — they reviewed the *corrected* code, which registers the guard and passes.

Only two things surfaced it: a user's live debugger (the exact NRE frame) and running the actual walk in-game.

## Why each deep-review agent "missed" findings #1–#3

Findings #1–#3 are **not** deep-review misses — they were found and fixed *before* the review, by the user's in-game runs. The review (run on the corrected code) correctly reported them resolved:
- **Standards / Efficiency / Compatibility** — reviewed the final guard; all passed. Agent 2 (Compatibility) independently *confirmed* the engine NRE by decompiling `SetupTeams():174-177`, corroborating the root cause.
- **Completeness** — correctly flagged the docs still owed (finding #5).
- **Data Flow** — the highest-value agent — verified the seed-before-force-finish ordering is *causally* guaranteed by the engine's own `SetupTeams`/`TeamSetupOver` sequencing (not merely probabilistic), and caught finding #4.

The takeaway: for engine-regression + mission-lifecycle-timing bugs, **in-game exercise is the irreplaceable gate**; offline review verifies the *fix* is sound but cannot discover a regression that only manifests at runtime in a headless path.

## Lessons codified

Appended to `docs/reviews/LESSONS-LEARNED.md`:
- **Adapters & TaleWorlds API** — add a mission behavior to a freshly-opened mission via `OnMissionBehaviorInitialize`, not `Mission.Current.AddMissionBehavior` from a game manager's `OnLoadFinished` (Mission.Current isn't the battle mission yet → silent no-op).
- **State/Lifecycle/Save** — an engine bump can regress a feature with unchanged bindings via a behavior-only change in an engine method the feature drives unusually (headless battle → new unconditional `InitialPlayerAgent` deref); binding-test-green ≠ behavior-verified. In-game-exercise headless/unusual paths on every bump.
- **Testing & QA** — a warm-cache pass is not proof the cold path works; validate cold for render/compile/deployment fixes.
