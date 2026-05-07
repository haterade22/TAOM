# Codex Adversarial Review: AdvancedCombat + Warg

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship. The core hit-registration loop is throttled to 2s, which makes animation-window bone checks effectively miss in normal play; the Warg BT also fails to cover late-spawned wargs and has rage-state bugs that can waste or repeatedly reapply control transitions.

## Section 1: Mission Behavior Lifecycle

### AdvancedCombatBehavior

Registered via IoC for all mission types. `OnMissionTick` is gated by a 2-second timer (`_timeSinceLastUpdate >= 2f`). SpatialGrid update and bone collision checks both sit behind this gate. Cleanup on mission end clears the grid and agent references.

### WargMissionBehavior

Registered via IoC. Identifies warg agents by checking for the warg Monster type on agent build. BT components are attached in a one-time scan after mission start — see Finding 2.

### Per-frame allocations

No per-tick `new List` or `new Dictionary` allocations found in either behavior's tick methods. LINQ usage is minimal and pre-cached.

## Section 2: Spatial Grid Correctness

### Grid sizing and update

Grid is sized from mission bounds. Updated every 2 seconds (tied to the same timer as bone checks — see Finding 1). Agents are re-bucketed on each update.

### Dead agent handling

Dead agents are filtered during grid rebuild. Between rebuilds, stale entries can exist but are checked for `IsActive` before use.

### Thread safety

SpatialGrid is not thread-safe. Mission tick in Bannerlord is single-threaded for MissionBehaviors, so this is acceptable for the current usage pattern.

## Section 3: Warg BT Analysis

### Main BT loop

PrepareOnFirstCall -> PeriodicallyCheckIfCanAttackAnyone -> WargAiControlledGetToEnemy -> WargAiControlledIsNotFacingEnemy -> WargAttackTask -> WargTryToGoRage -> SetRageAttackTimer -> FinishRageMode. CleanIfEnemyDied and OnWargDied handle cleanup paths.

### Rider death handling

`OnAgentDismount` fires when a rider dies or dismounts. The BT checks `HasNoRider` decorator to switch to riderless behavior. However, the BT is only attached during the initial scan — see Finding 2.

### OnWargDied cleanup

Removes the warg from `_wargAgents` dictionary and disposes the BT component. Does not explicitly remove from SpatialGrid, but the next grid rebuild will exclude the dead agent.

### WargAttackTask

Attack timing uses configurable cooldowns. Target selection uses nearest-agent from SpatialGrid but does not filter by team — see Finding 4.

## Findings

### [HIGH] Animation-window bone checks are only advanced every 2 seconds — custom attacks register no hits

**File:** `AdvancedCombatBehavior.cs:20-32`

**Evidence:** `OnMissionTick` returns until `_timeSinceLastUpdate >= 2f`, and `_boneCollisionService.TickBoneChecks(dt)` sits after that gate. `BoneCheckDuringAnimation` expires once action progress reaches `actionProgressMax`, and Warg attacks create windows of only `0.5f` to `0.7f`. Most bite checks will never be sampled during the active animation window.

**Impact:** Warg custom attacks play the animation but register no hit because the bone check isn't ticked during the 0.5-0.7s attack window.

**Remediation:** Tick active bone checks every mission tick. Keep the 2s throttle only on `SpatialGrid.UpdateGrid` and debug rendering.

### [HIGH] Warg BTs are attached exactly once — late-spawned/reinforcement wargs never get AI

**File:** `WargMissionBehavior.cs:89-112`

**Evidence:** Flips `_treesAdded` after the first post-start scan and never revisits `Mission.Current.AllAgents`. No `OnAgentBuild` or spawn hook to attach a tree later. Any warg spawned after the initial 1s window enters the mission without a `BehaviorTreeAgentComponent`.

**Impact:** Reinforcement wargs have no combat AI — they stand idle on the battlefield.

**Remediation:** Attach BT components on agent-spawn/build events, or rescan for uninitialized wargs each tick.

### [MEDIUM] FirstAttack is never cleared — rage-entry side effects replay every BT pass

**File:** `PrepareOnFirstCall.cs:28-45`

**Evidence:** `FirstAttack` is set to `true` when rage starts but never reset. Neither `FinishRageMode` nor `CleanIfEnemyDied` clears it. Every BT pass through the rage branch re-runs controller switching, player messaging, and AI rider formation detachment.

**Remediation:** Consume the flag inside `PrepareOnFirstCall` (`FirstAttack.SetValue(false)`) or replace with a dedicated one-shot entry task.

### [MEDIUM] Rage target acquisition ignores team — burns rage attacks on allies

**File:** `PeriodicallyCheckIfCanAttackAnyone.cs:19-35`

**Evidence:** Returns true for any nearby non-mount/non-rider agent without team filtering. `AgentAdapter.CustomAttack` builds target list from nearby active agents without team filter. `WargAttackTask` decrements `RageAttackAmount` before the hit callback. Friendly targets are only discarded later in `HandleWargTargetHit`, after the attack is spent.

**Impact:** A raging warg can repeatedly attack allies standing in front of it, consuming limited rage attacks without hitting enemies.

**Remediation:** Filter candidate targets by hostile team before the BT says an attack is available. Pass only hostile targets into `CustomAttack`.

## Observations

- SpatialGrid is single-threaded, which is correct for MissionBehavior tick context
- No per-frame allocations found in tick methods — GC pressure is low
- OnWargDied cleanup removes from `_wargAgents` but relies on next grid rebuild to clear SpatialGrid — acceptable but could leave a 2s window of stale grid entries
- Current test coverage does not exercise reinforcement spawning or rage state transitions

## Recommended Next Steps

1. Decouple bone check tick from grid rebuild — tick bone checks every frame, rebuild grid on 2s cadence
2. Add agent-spawn hook for warg BT attachment
3. Fix `FirstAttack` flag consumption in `PrepareOnFirstCall`
4. Add team filter to rage target acquisition
5. Add tests for reinforcement warg spawning and rage enter/exit transitions
