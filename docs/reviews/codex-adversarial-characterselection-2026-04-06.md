# Codex Adversarial Review: CharacterSelection (RefreshCharacterEntityAuxPatch)

**Date:** 2026-04-06
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. The transpiler is not anchored to a verified vanilla instruction window; it patches the first `new AgentVisualsData()` it sees and prepends an `ActionSet` call without proving that this is the instance consumed by `RefreshCharacterEntityAux` or that vanilla does not overwrite it later. The race-aware action lookup is also unguarded.

**Note:** Codex could not read `E:\Decompiled_Bannerlord\` from its sandbox, so vanilla evidence is limited to public Bannerlord API references. This lowers confidence but does not remove the underlying risk.

## Section 1: Vanilla Code

### BodyGeneratorView.RefreshCharacterEntityAux

**Not available from decompiled source** — `BodyGeneratorView.cs` was not found in `E:\Decompiled_Bannerlord\`. Analysis relies on API documentation and TAOM source.

### AgentVisualsData (from API docs)

- Two constructors: parameterless and copy constructor
- `ActionSet(MBActionSet)`: fluent setter returning `AgentVisualsData` — one of many fluent setters, not a terminal operation
- Multiple other fluent setters in the chain (`.BodyProperties()`, `.Equipment()`, `.Race()`, etc.)

## Section 2: Transpiler Analysis

### a) Stack State

After `newobj AgentVisualsData::.ctor()`, stack has `[AgentVisualsData]`. The injected sequence:
1. `ldarg.0` → `[AgentVisualsData, BodyGeneratorView]`
2. `call GetActionSet` → `[AgentVisualsData, MBActionSet]`
3. `callvirt ActionSet` → `[AgentVisualsData]`

**Stack is valid.** The fluent setter returns the same `AgentVisualsData` reference, preserving the stack for subsequent chained calls.

### b) Vanilla ActionSet Conflict

`ActionSet` is a normal fluent setter, not terminal. If vanilla `RefreshCharacterEntityAux` already calls `.ActionSet(...)` later in the fluent chain, TAOM's injected value is overwritten. **Cannot confirm or deny without decompiled source** — this is the core release risk.

### c) Multiple Newobj Instances

The transpiler patches only the first `AgentVisualsData::.ctor()` match. If the method constructs multiple instances, this might patch the wrong one. Match predicate is only `newobj == parameterless ctor` with no surrounding instruction verification.

### d) Unknown Race IDs

`GetActionSet` calls `FaceGen.GetBaseMonsterFromRace(bodyGeneratorView.BodyGen.Race)` with no validation or fallback. Elsewhere in the repo, `RaceManager` explicitly handles unknown race IDs with human fallback. This patch bypasses that pattern.

## Findings

### [HIGH] Transpiler patches first AgentVisualsData constructor without verifying it's the correct creation site

**File:** `RefreshCharacterEntityAuxPatch.cs:36-53`

**TAOM code:** Scans IL and stops at first `newobj` matching `AgentVisualsData::.ctor()` (lines 36-42), then injects `ldarg.0 -> call GetActionSet -> callvirt ActionSet` after it (lines 48-53).

**Evidence:** `AgentVisualsData` has multiple constructors. The match predicate is only `newobj == parameterless ctor` with no surrounding instruction verification. A game update adding an earlier scratch instance causes the transpiler to mutate the wrong object with no compile-time signal.

**Remediation:** Match a stable instruction window around the actual `AgentVisuals.Create` builder chain, not just the first ctor. Assert exactly one match. Fail closed if zero or multiple windows found.

### [MEDIUM] Injected ActionSet can be silently overwritten by vanilla's own later call

**File:** `RefreshCharacterEntityAuxPatch.cs:48-53`

**TAOM code:** Injects fluent `ActionSet(...)` immediately after `new AgentVisualsData()`.

**Evidence:** `AgentVisualsData.ActionSet` is a normal fluent setter among many. If vanilla calls `.ActionSet(...)` later in the chain, TAOM's value is overwritten. The patch becomes behaviorally dead while still applying cleanly. Cannot verify without decompiled source.

**Remediation:** Verify the exact vanilla method body. Patch the final `ActionSet` write or its argument instead of prepending. Document and assert surrounding IL sequence.

### [MEDIUM] Race-aware action lookup bypasses TAOM's unknown-race fallback pattern

**File:** `RefreshCharacterEntityAuxPatch.cs:16-19`

**TAOM code:** `GetActionSet` calls `FaceGen.GetBaseMonsterFromRace(bodyGeneratorView.BodyGen.Race)` with no validation/fallback.

**Evidence:** `RaceManager` elsewhere handles unknown race IDs with human fallback and logging. This patch does not use that pattern. A custom or mismapped race reaches character selection with no guard.

**Remediation:** Add explicit guard/fallback before `MBGlobals.GetActionSetWithSuffix`, reusing TAOM's existing race-validation path. If unknown race IDs are impossible here, prove with decompiled code and document.

## Observations

- IL stack discipline is correct — the 3 injected instructions leave the stack valid
- The `Late_Transpiler` category ensures this patches after other Harmony modifications
- The fundamental verification gap (no decompiled vanilla source for `BodyGeneratorView`) means all three findings carry uncertainty

## Recommended Next Steps

1. Decompile `BodyGeneratorView.RefreshCharacterEntityAux` from v1.3.15 and verify the exact IL
2. Anchor the transpiler to a stable instruction window, not just the first ctor
3. Verify whether vanilla already calls `.ActionSet()` later in the chain
4. Add race-ID fallback guard consistent with `RaceManager` pattern
