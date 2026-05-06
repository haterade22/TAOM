# RCA — ShaderPrecompilation Loading-Screen Latch Killed By Initial Zero

**Date:** 2026-05-04
**Surfaced by:** in-game test after `2700f53` shipped
**Fixed in:** `2ce453f`
**Severity:** HIGH (user-visible — feature appeared completely broken on warm-cache machines)

---

## What happened

Commit `2700f53` shipped four fixes for the "Pre-compile Shaders doesn't work" report (silent character drop, premature 120 s abort, static-state retry leak, abort-branch latch leak). It went through `/verify` (1227/1227 tests), 5-parallel-agent `/deep-review`, and a Codex adversarial review on 2026-04-14. All passed. The earlier shader work even had its own dedicated Codex review four weeks ago.

Six hours after merge, the user ran the precompile and reported: *"All I see is a loading screen and that is it."* The new file-log markers proved the battle started, all 3,250 characters got loaded, and the deployment phase opened — meaning the engine compiled shaders successfully — but the loading-screen text update never appeared.

Tracing the patch logic against the user's `taom_debug_*.log`:

```
07:58:41 Starting shader pre-compilation battle
07:58:41 3250 characters from 28 cultures
07:58:41 Loaded 3250 characters — player: 2100, enemy: 2100
07:58:42 [CustomBattles] Adding CustomBattleTeamFixBehavior to custom battle mission
07:58:59 BannerColorConfigProvider: Loaded banner_color_config.json
07:58:59 CustomBattleTeamFix: Team relationships verified
─── 2 min 8 s gap (loading screen up, shaders compiling, NO progress logs) ───
08:01:07 [Spider] Initialized               ← deployment phase opens
08:01:07 [Warg] Initialized
08:01:07 Initializing RaceManager with 14 races from game data
```

The 2 min 8 s gap was the actual shader compilation, with no file-log evidence and (per the user) no on-screen text.

---

## Root cause

`LoadingScreen_ShaderProgress_Patch.cs` used `_lastShaderCount = -1` as both:

1. The "uninitialized — never observed a value" sentinel (set by `ResetForNewBattle()`)
2. The comparison value for change detection (`if (remaining != _lastShaderCount)`)

These two roles collide on the **first frame** the patch runs after `IsShaderBattleActive` flips on. The shader queue is driven by mesh rendering. On a warm cache and a fast machine, the engine often hasn't touched a single character mesh yet by the time our postfix first fires. `Utilities.GetNumberOfShaderCompilationsInProgress()` returns `0`. Then:

```csharp
if (remaining != _lastShaderCount)        // 0 != -1 → TRUE
{
    _lastShaderCount = remaining;          // _lastShaderCount = 0
    if (remaining > 0) { ... }             // FALSE
    else
    {
        TaomShaderGameManager.ResetShaderBattleActive();   // ← LATCH KILLED HERE
        ...
    }
}
```

Subsequent frames where the count actually rose hit `if (!IsShaderBattleActive) return;` at the top of the postfix and short-circuited. The loading screen never received our text.

Same shape as the abort-branch latch leak we fixed in the same commit — a **state-machine sentinel collision**, where the "before first observation" state is indistinguishable from a real terminal value (`0`).

---

## Why the review process missed it

### Deep-review Agent 5 (Data Flow Tracing)

Agent 5 traced static state explicitly. Its actual write-up included:

> **Trace 2 — New Tail-Stuck Guard Interaction**
> Walkthrough:
> - **Frame A:** count=100, previous=-1 → change detected → `_stuckSinceMs = now_A`. Count > 5, so stuck logic skipped.

The walkthrough started at `count=100`. Every subsequent frame in the trace assumed the engine had already queued shaders. The agent never enumerated the **first-frame state where `count = 0`** — the one state in the input space where the bug fires.

Agent 5's prompt at the time of review included:

> 5. **Lifecycle Completeness (State Matrix):** For every "set" operation, verify there is a corresponding "clear" for ALL entity lifecycle states.
> Entity states to check: alive, killed, unconscious, removed, mission-end, screen-close, session-end.

This is a **lifecycle** matrix (entity exists / dies / gets removed). It does not enumerate **observation values** for state machines driven by polling external state (engine count, file size, network response). The two are different reviews and the deep-review only had the first.

### Codex 2026-04-14 review

Codex's earlier review found three of the four bugs that shipped in `2700f53` (sized them MEDIUM/LOW; we re-rated them HIGH on user impact). It missed the same first-frame initial-zero case for the same reason: its `_lastShaderCount` analysis traced "value changes between two real observations," not "sentinel transitions to first real observation."

### Tests

Per ADR-008, Harmony patches are entry points and not unit-tested. The `LoadingScreen_ShaderProgress_Patch` Postfix has zero test coverage. A unit test that constructed a fake `LoadingWindowViewModel`, invoked `ResetForNewBattle()`, then called Postfix with a stub returning `0` would have caught this in seconds. We don't have the harness to do that today.

### The previous abort-branch fix

Agent 5 *did* find one related state-machine bug in the same review — the abort-branch leaving `IsShaderBattleActive = true` after `EndGame()`. We fixed that one. The review explicitly traced "count drops from positive to zero" but did not also trace "count starts at zero, goes positive." The asymmetry is the gap.

---

## Fix

Commit `2ce453f`:

- Added `_hasObservedWork` bool, set true the first time `remaining > 0` is observed.
- `ResetShaderBattleActive()` is only called when `remaining == 0 && _hasObservedWork`. The first-frame-zero case is now a no-op wait (just records `_lastShaderCount = 0` and returns).
- Sentinel collision eliminated: the change-detection comparison still uses `_lastShaderCount`, but completion detection now requires the additional `_hasObservedWork` flag, so initial vs terminal zero are distinguishable.

Adjacent improvements shipped in the same commit:

- 1 Hz text update with `(elapsed: Xm Ys)` and animated dots so users see liveness even when count holds steady.
- 30 s file-log progress lines so post-mortem grep doesn't depend on the loading-screen frames having been observed live.
- Throttling makes string allocation bounded (was per-change, is now per-second).

---

## Prevention — actions taken in same commit set

1. **`.claude/rules/harmony-patches.md`** — new section *"Static state machines in patches: sentinel collision check."* Codifies the rule: when a static field uses a sentinel value (`-1`, `null`, `default`, etc.) for "uninitialized" *and* participates in change-detection or terminal-state comparisons, you must enumerate the sentinel-to-real-value transition explicitly. If the sentinel can collide with a real terminal value (e.g., `-1` is sentinel, `0` is "complete"), separate the two roles with an additional flag.

2. **`.claude/skills/deep-review/SKILL.md`** Agent 5 prompt — new trace category *"Observation state machines"*. Requires the agent to enumerate **all four** boundary states for any static field driven by external observation: (a) initial/sentinel, (b) first observation, (c) intermediate values, (d) terminal value. Each transition between adjacent states must be classified as a real signal or a false positive.

3. **Memory entry** `feedback_observation_state_matrix.md` — preserves the lesson across sessions so the next agent reviewing patch state machines knows to enumerate the observation matrix, not just the lifecycle matrix.

4. **CHANGELOG entry** for both the fix and this RCA — visible to future readers walking the project history.

## Prevention — actions deferred (out of scope here, captured for follow-up)

- **No unit tests for Harmony postfix logic.** A test harness for `LoadingWindowViewModel`-style VM patches would catch this whole class of bug at design time. Bigger lift; would require either (a) a stubbed VM scaffold in `TAOM.Tests`, (b) extracting the postfix decision logic into a pure service that's easy to mock, or (c) accepting that ADR-008's "entry points are not unit-tested" exception costs us this category of bug. Worth a discussion thread, not part of this fix.
- **Static-analysis lint for sentinel collisions.** Could write a Roslyn analyzer that flags any `int` static field initialized to a constant that's also compared against a real input range. Way out of scope; noting for the long tail.

---

## Lessons captured

- **Sentinel collision is its own bug class.** Don't reuse "uninitialized" sentinels in comparison logic that can match real terminal values. Use a separate flag.
- **Lifecycle matrix ≠ observation matrix.** "When does this entity die?" and "What are all the values this observation can take, in what order?" are different reviews. Both are needed for static-state machines driven by external polling.
- **Reviews that walk happy-path examples are insufficient.** The bug was at frame 1 of the input space; Agent 5 walked frame 2 onward. Future review prompts must explicitly enumerate boundary states, not pick representative midpoints.
- **In-game tests catch what process tests don't.** Even with a green build, green tests, green deep-review, green Codex, the only way to confirm a UI-text postfix is the user actually seeing the text. ADR-008's "test via game" caveat is a real gap, not a checkbox.
