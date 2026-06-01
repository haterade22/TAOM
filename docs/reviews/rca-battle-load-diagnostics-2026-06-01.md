# RCA — BattleLoadDiagnostics deep-review (2026-06-01)

## Top-line

New feature `BattleLoadDiagnostics` (Patch43) + companion tool `tools/validate_mesh_refs.py`. Deep-review ran 5 core agents (Standards / Compat / Efficiency / Completeness / Data-Flow). Result: **0 CRITICAL, 0 HIGH, 1 MEDIUM, 1 LOW**, plus 1 administrative gap (no GitHub issue). No HIGH/CRITICAL — the feature is sound. Both confirmed findings are recorded below with why-missed + preventive action per the Phase-3e rule (applies to EVERY confirmed finding, not just HIGH).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MEDIUM | The loading window opens in a `Mission.Initialize` prefix, which fires for **all** mission types (town, conversation, arena, hideout), not just battles. A slow (>45s) non-battle scene load could fire the stall watchdog + bundle under a "battle load" framing. | Scope enumeration | Designed the window around "open at Initialize, close on first tick" and verified the *close* path, but never enumerated *which mission types* `Mission.Initialize` fires for. The feature name ("battle") created an implicit, unverified assumption that the patched method was battle-scoped. | **Resolved as intentional scope, NOT gated** — see decision below. Generalizable rule candidate: *when patching a universal engine method (`Mission.Initialize`, `Agent.*`) for a feature named after a subset, explicitly enumerate which invocations are in/out of scope before assuming the patch is subset-scoped.* |
| 2 | LOW | `IsEnabled` is read live from MCM on every call; if the toggle flipped between `OnMissionBehaviorInitialize` (adds the closing behavior) and `Mission.Initialize` (opens the window), the window could open without a behavior to close it. | TOCTOU race | Two reads of the same live property in adjacent lifecycle callbacks. | No action — both callbacks run synchronously in the same engine frame; an MCM flip cannot interleave. Documented here for completeness. The window also closes via `OnEndMissionInternal` if the behavior IS present. |
| — | (admin) | No GitHub issue for the investigation. | Process | The work started from a user bug report, not an issue. | Created this session (Agent 4 flagged it; orchestrator creates the issue before the closing commit per the CLAUDE.md completion workflow). |

## Decision on finding #1 (evidence-over-claims: finding verified, suggested fix rejected)

The Data-Flow agent confirmed the finding is real **and** recommended a fix: gate the window to battle missions only. I verified the claim (read `Mission.Initialize` is universal — correct) but **rejected the suggested fix**, because:

1. **Gating risks a false-negative on the exact case we are hunting.** The whole purpose is to capture an *early* battle-load freeze. If mission-type detection is unreliable at the moment of an early `Mission.Initialize`-time hang, a battle-only gate would *suppress* the data. For a diagnostic, a false-negative (missing the hang) is strictly worse than a false-positive (an extra bundle on a slow town load).
2. **The false-positive is rare and self-documenting.** It only fires on a >45s non-battle load (pathological), and the watchdog marker embeds the scene name (`scene='battle_terrain_b'` vs `scene='town_ES2'`), so a fired bundle identifies its own mission type.
3. **Broader coverage is a feature, not a bug, for a "why does loading hang" tool** — catching *any* mission-load hang is more useful than battle-only.

Changes made instead (minimal, zero-risk): (a) the watchdog's bundle exception message reworded "Battle load stalled" → "Mission load stalled" for accuracy; (b) a "Scope: instruments ALL mission loads, by design" section added to `docs/features/battle-load-diagnostics.md`. This is an explicit-documented decision, not a silent dismissal (the finding is MEDIUM, below the HIGH "no silent deferral" bar, but recorded regardless).

## Why each agent's result was what it was

- **Standards / Compat / Efficiency:** clean — correctly, the feature follows ADR-002/003/004/005/007, every API verified against 1.4.5, hot path gated. Nothing to RCA.
- **Completeness:** caught the missing GitHub issue (correct) — acted on.
- **Data-Flow:** caught finding #1 (the scope gap) — exactly the cross-system class of bug this agent exists for. The per-file agents (Standards/Efficiency) could not have caught it because each file is individually correct; the gap is *which engine method the patch attaches to* vs *what the feature claims to scope*, which only a flow trace surfaces. Working as intended.

## Feedback memory candidate

Borderline. The "enumerate which invocations a universal-method patch actually fires for, when the feature is named after a subset" lesson is real but adjacent to existing rules (`feedback_substring_keyword_matches_external_data.md` = the data-matching cousin; `think-before-coding` = surface load-bearing assumptions). Given it produced only a MEDIUM that was resolved as intentional scope, this is logged here rather than promoted to a standalone feedback memory. If a *second* feature ships a universal-method patch under a subset name and hits a real bug from it, promote then.
