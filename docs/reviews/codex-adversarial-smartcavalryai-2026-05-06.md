# Codex Adversarial Review — SmartCavalryAI (2026-05-06)

## Context

Feature #3 of the 7-feature port. After 5 Claude /deep-review agents collectively flagged + we fixed:
- Recursion guard converted from bool to depth counter (nested-scope safe)
- `SmartCavalryRecursionGuard.Reset()` for abnormal mission termination
- Per-instance `_scratchBuffer` in `BattlefieldQueryAdapter` (was static, latent re-entrancy hazard)
- Per-formation adapter cache in `MissionBehavior` (was creating two adapters per cavalry per tick)
- `IsAligned` LINQ allocation reduced (single pass, reused static scratch list)
- `UpdateForming` checks `IsTargetAlive` before transitioning (was unconditionally Charging)
- `LengthSquared` instead of `Length` in distance comparisons (sqrt avoided)
- `HandleChargeOrder` short-circuits when state already non-Idle (player double-tap idempotency)
- `InitiateLineCharge` early-returns on zero-length direction BEFORE state mutation
- `Patch31` IoC.Resolve calls cached in static lazy fields; cheapest-first guard ordering
- 6 new tests covering all the above

…the adversarial reviewer was sent in to find what those 5 agents MISSED. They had the same biases I did.

## Adversarial findings

### HIGH — NaN strictness setting freezes state machine forever

`SmartCavalryAISettingsProvider.cs:22-23` (now :30+ after fix). `Clamp(NaN, min, max)` returns NaN because both `NaN < min` and `NaN > max` are false. The NaN flows into `UpdateForming`/`UpdateReforming` via `cav.IsAligned(strictness)`; in `FormationAdapter.IsAligned`, `tolerance = 5f * (1f - NaN)` is NaN, and `maxDeviation < NaN` is always false. The formation is permanently stuck in Forming or Reforming for the rest of the battle.

**Why /deep-review missed it:** the 5 agents mechanically verified the clamp range (0..1, 10..80, 0.8..3.0) but didn't enumerate the NaN/Infinity edge case. Same lesson was previously codified in `feedback_validate_before_lookup_with_fallback.md` and the "Config Providers MUST Validate" rule but not applied to MCM-backed settings providers.

**Fix applied:** `SafeClamp(value, defaultValue, min, max)` now returns `defaultValue` when `float.IsNaN(value) || float.IsInfinity(value)`. See `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs`.

**Generalizable lesson:** see new feedback memory `feedback_clamp_nan_infinity_propagates.md`.

### HIGH — Cross-feature collision: MixedFormations overrides charge-line layout for horse-archer cavalry

Both features patch on `Formation`. After SmartCavalryAI's `IssueStop()` puts the cavalry formation in Hold state, MixedFormations' `Patch30_FormationGetOrderPositionOfUnit` Prefix runs per-unit — and if the formation has a layout assigned (which it WILL for horse-archer cavalry that satisfies MixedFormations' "≥10 units, ≥20% ranged" heuristic), it overrides SmartCavalryAI's wide-line `SetPositioning` per-unit. The wide-line cavalry charge — the central feature — is silently undone for exactly the factions where it matters most (Khuzait, Easterlings, Rohirrim with horse archers).

**Why /deep-review missed it:** the 5 agents reviewed SmartCavalryAI files in isolation. The data-flow agent verified within-feature flow but didn't explicitly cross-check against MixedFormations' assignment criteria. The MEDIUM rating it gave was conservative and didn't propose the architectural fix.

**Fix applied:** added `if (formation.RepresentativeIsCavalry) return false;` to MixedFormations' `IsMixedFormationInternal` (so cavalry never gets a layout assigned) AND `if (formation.RepresentativeIsCavalry) return null;` to `ComputeUnitPlanePosition` (so even a manually-cycled layout is skipped). See `Main/Features/MixedFormations/FormationLayoutService.cs`.

**Generalizable lesson:** see new feedback memory `feedback_cross_feature_handshake_via_shared_adapter.md`.

### MEDIUM — Self-charge possible when own.Team has zero non-friendly teams

`Patch31_FormationSetMovementOrder.cs:NearestEnemyFormation`. The friendly-team filter relies on `IsFriendOf(own.Team)` — this is true for self in standard setups but not guaranteed in custom-battle multi-team scenarios. A defensive `team == own.Team` early-skip (and a `ReferenceEquals(enemy, own)` belt-and-braces check) closes the gap.

**Why /deep-review missed it:** standards/correctness agents verified the friendly check exists; nobody questioned the `IsFriendOf(self)==true` invariant.

**Fix applied:** explicit team identity skip + per-formation reference identity skip. See `Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs`.

### MEDIUM — Stale `_states` entries on cross-mission singleton survival

`CavalryChargeService._states` is a Singleton-lifetime dictionary. `OnEndMission` clears it. But if a mission terminates without `OnEndMission` firing (custom-battle abort, user Alt-F4 then load), a stale entry persists into the next mission against a now-dead Formation reference. Bounded leak, not a correctness bug — formations are not pooled across missions in v1.3.15, so the stale reference is just dead weight.

**Why /deep-review missed it:** lifecycle audits assume the cleanup hook fires reliably. The "what if it doesn't" path wasn't enumerated.

**Status:** documented as known limitation, no code change. The defensive `SmartCavalryRecursionGuard.Reset()` call from `OnEndMission` already covers the more critical thread-local case; the dictionary leak is acceptable per scope.

### CONFIRMED CLEAN by adversarial pass

- Recursion guard async/await safety: `CavalryCommandAdapter` has zero `await`; ThreadStatic safe.
- Right-vector sign across non-cardinal charge directions: walked through SE, NE; sign flip is correct.
- `_states` leak within a single mission: bounded by formation count (~8 max).
- Settings live-reload mid-battle: provider reads `TaomSettings.Instance` live; toggle takes effect on next tick.
- Tick order vs MixedFormations: moot — Patch30 fires per-unit on every position query, not at tick-start.
- MovementOrder enum drift: `Charge=2, ChargeToTarget=3` confirmed v1.3.15; we use enum names not int casts.

## Root Cause Analysis

The pattern across the 4 found bugs: **/deep-review agents verify what they were told to look for. Adversarial review finds the angles they weren't told to look for.**

1. **Settings-NaN gap:** the existing rules library (`feedback_validate_before_lookup_with_fallback.md`, "Config Providers MUST Validate") covered loader-side validation. But the MCM provider is a CONSUMER of `TaomSettings.Instance`, not a loader. There was no rule for "providers that wrap MCM must NaN-guard the values they expose." Now there is.

2. **Cross-feature collision:** the deep-review skill has 5 tracks (standards / compatibility / efficiency / completeness / data flow). None of them is explicitly "cross-feature interactions on shared adapters." When MixedFormations introduced `IFormationAdapter`, both MixedFormations AND SmartCavalryAI use it; both modify `Formation` state via patches. No agent owned the question "if both feature's hooks fire on the same formation in the same mission, what does the state machine see?"

3. **Self-charge defense:** the invariant `IsFriendOf(self)==true` is undocumented in TaleWorlds source. Reviewers asked "does the friendly check exist" (yes) and stopped. The harder question — "is the friendly check sufficient if the invariant breaks" — was unasked.

4. **Cross-mission state leak:** lifecycle hooks are assumed to fire reliably. They mostly do, but exceptions (process-termination, custom-battle abort) are infrequent enough that the "what if not" branch is ignored.

## How to make sure this doesn't happen again

The adversarial-review-as-second-opinion pattern (independent reviewer with explicit "find what /deep-review missed" prompt) is the structural fix. It worked here — caught 2 HIGH and 2 MEDIUM findings the in-house pass missed.

But we can do better proactively. **Three new feedback memories codified from this RCA:**

1. **`feedback_clamp_nan_infinity_propagates.md`** — providers that clamp settings must NaN/Infinity-guard. `value < min ? min : value > max ? max : value` returns NaN when `value=NaN`. Fall back to defaults.

2. **`feedback_cross_feature_handshake_via_shared_adapter.md`** — when feature A and feature B both patch the same TaleWorlds class via shared `IXxxAdapter`, and at least one writes back state via `SetPositioning`/`SetMovementOrder`/etc., they need an explicit cross-feature handshake. Pre-flight design check: which feature owns the formation when both apply? Document it. Most often: the more-specific feature (cavalry-only) wins over the broader one (any-mixed-formation).

3. **`feedback_taleworlds_invariant_check_explicit.md`** — TaleWorlds invariants like `team.IsFriendOf(team) == true` are undocumented. When relying on them in a security-relevant comparison (friendly-fire avoidance), add an explicit identity-equality skip as belt-and-braces. The invariant is presumed true but not guaranteed.

These memories will be picked up automatically by future port sessions and adversarial-review prompts.

## Status

- All 4 adversarial findings fixed in same session.
- 50/50 SmartCavalryAI tests pass.
- Build clean (after temporary excludes for parallel-feature compile errors in CompanionTactics + FiefManagement that are unrelated to SmartCavalryAI).
- Awaiting in-game verification before commit.
