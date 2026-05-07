# Codex Adversarial Review: Career System (TAOM vs TOR_Core)

**Date:** 2026-04-07
**Target:** working tree diff
**Verdict:** needs-attention

**No-ship. The TAOM career system does not exist yet.** The repository contains TOR research documentation (`docs/research/tor-career-system.md`) and a review prompt (`docs/research/codex-career-system-review-prompt.md`) describing a planned implementation, but no actual `Main/Features/CareerSystem/` directory or production code exists.

## Summary

Codex searched the entire `Main/` and `TAOM.Tests/` trees for career-related symbols (`CareerAbility`, `CareerScreen`, `PassiveEffectType`, `CareerID`, `CareerSwitch`, etc.) and found none. The only career-adjacent runtime code is in `CharacterCreation/NarrativeMenuBuilder.cs` which writes occupation/title/equipment during character creation — it does not establish persisted career state.

The project's own documentation confirms this: `docs/research/tor-resource-system.md` explicitly states "TAOM has no career system yet" and notes TOR's career coupling "won't exist in TAOM (unless career system is implemented)."

## Findings

### [CRITICAL] Career system stack is absent from the repository

**Evidence:** No `Main/Features/CareerSystem/` directory exists. Grep for `CareerAbility`, `CareerScreen`, `PassiveEffectType`, `CareerID`, `CareerSwitch` returns zero results in production code. The prompt described a full implementation but it has not been built.

**Impact:** Every comparison section (progression, save/load, mutations, abilities, passives, UI, battle, switching, events) is N/A — there is nothing to compare against TOR.

**Recommendation:** Build the career system before requesting a comparison review. The TOR research at `docs/research/tor-career-system.md` provides the reference architecture.

### [CRITICAL] Character creation only stores title/equipment, not career identity

**File:** `NarrativeMenuBuilder.cs:105-109`

**Evidence:** On selection, the narrative menu calls `SetParentOccupation`, writes `SelectedTitleType`, and swaps youth equipment. No `CareerId`, root-node selection, choice list, mutation seed, or ability assignment exists.

**Impact:** No durable career object is created. TOR's root auto-select, duplicate prevention, tier gating, free-point accounting, and cache refresh are all impossible without a career identity.

**Recommendation:** Introduce explicit career assignment via a dedicated `ICareerService.AssignCareer()` call from character creation that persists `CareerId`, adds the root choice, and initializes ability/passive state.

### [HIGH] No persistence path for career state across saves

**File:** `CharacterCreationRegistrationBehavior.cs:29-31`

**Evidence:** `SyncData` is explicitly empty. No per-hero career storage found in `Main/`. The only `SyncData` implementations belong to unrelated features (banner injection, hero race, special resources).

**Impact:** Even if career assignment were added to CC, it would not survive save/load.

**Recommendation:** Add a dedicated `CareerPersistenceBehavior` (or integrate into an existing behavior) that serializes per-hero career state and handles missing/removed careers defensively on load.

## What This Review CAN Provide

The TOR research document at `docs/research/tor-career-system.md` is comprehensive (class hierarchy, 22 careers, 44 PassiveEffectTypes, 16 patches, save/load patterns, UI flow). When the career system is built, re-run this review to compare the actual implementation against TOR's production patterns. The research doc serves as the design specification.

## Recommended Next Steps

1. Create `Main/Features/CareerSystem/` with the TAOM feature scaffold (`/new-feature CareerSystem`)
2. Implement core domain: `CareerObject`, `CareerChoiceObject`, `CareerChoiceGroupObject`
3. Implement services: `ICareerService`, `CareerService`, `ICareerStorageService`
4. Implement persistence: `CareerPersistenceBehavior` with `SyncData`
5. Wire into character creation
6. Add career screen UI
7. Re-run this adversarial review against the implementation
