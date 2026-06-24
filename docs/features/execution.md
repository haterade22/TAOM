# Execution

## Overview

The Execution feature replaces vanilla Bannerlord's one-size-fits-all lord execution penalties with a LOTR-thematic system. When a Free Peoples lord executes a servant of Sauron there is no dishonor; when a lord executes one of their own allies it is **kinslaying**, punished more harshly than vanilla. The feature wraps Bannerlord's existing execution kill chain with thread-local context propagation, a `DefaultExecutionRelationModel` override, and Harmony patches that gate the Honor-trait penalty by the executor's alignment.

The alignment subsystem itself (kingdom-to-alignment mapping, relation-modifier rules) is documented in detail at [`alignment-aware-execution.md`](alignment-aware-execution.md). This page covers the feature's overall wiring; that page covers the per-rule decision logic.

## Why This Exists

Vanilla Bannerlord applies the same massive penalties to every execution regardless of context:

- **−1000 Honor XP** to the player's Honor trait
- **−60 relation** with the victim's clan
- **−30 relation** with friends of each evaluating clan leader
- **−10 relation** with every same-faction lord and every honorable noble worldwide

That breaks LOTR immersion. Aragorn executing the Mouth of Sauron should not make him dishonorable. Théoden executing a captured Uruk-hai warlord should not turn Gondor against him. Conversely, Denethor executing a Rohan lord *is* kinslaying — a graver act than the vanilla system recognizes.

- **Vanilla behavior:** uniform penalty + uniform Honor loss for every execution.
- **TAOM requirement:** alignment-aware penalties (Free, Evil, Neutral kingdoms); no Honor loss for cross-alignment executions; 1.5× relation penalty for kinslaying.
- **Without this feature:** the player who executes Sauron's lieutenants is treated as a dishonorable kinslayer by every honorable noble in Middle-earth.

## Architecture

### Design Challenge

`KillCharacterAction.ApplyInternal` is a `private static` method on a `public static class`. The two execution entry points (party screen, post-battle conversation) both flow through it, and the vanilla Honor penalty is applied **inside** that method via `TraitLevelingHelper.OnLordExecuted`, *after* the kingdoms involved have been forgotten by the call stack. To gate the Honor penalty on the executor's vs. victim's alignment, TAOM needs:

1. The victim and executor kingdom IDs available at the moment `TraitLevelingHelper.OnLordExecuted` fires (which has no kingdom parameters).
2. A way to override `DefaultExecutionRelationModel.GetRelationChangeForExecutingHero` for the relation-preview UI and the actual relation deltas.

### Solution Approach

A two-pronged hook: thread-local context for the trait-penalty patch, and a GameModel override for the relation calculations. Both reuse a single `IOnExecutionAction` decision hook that wraps the `IAlignmentService`.

```
Main/_Module/ModuleData/execution/alignment.json
        |
  AlignmentConfigProvider (loads kingdomId -> "free"/"evil"/"neutral")
        |
  AlignmentService (decides AreEnemyAlignments / AreSameAlignment / GetKingdomSide)
        |
  ExecutionActionHook : IOnExecutionAction
        / \
       /   \
KillCharacterAction_ApplyInternal_Patch        TaomExecutionRelationModel
  (Prefix sets ExecutionContext;                 (override of vanilla
   Finalizer clears it)                          GetRelationChangeForExecutingHero)
       |
  TraitLevelingHelper_OnLordExecuted_Patch
  (Prefix reads ExecutionContext;
   skips vanilla Honor penalty when cross-alignment)
```

`ExecutionContext` is a `ThreadLocal<string>` pair. The outer Prefix on `ApplyInternal` populates it with the victim + executor kingdom IDs; the Finalizer clears it. Inside that scope, the inner Prefix on `OnLordExecuted` reads the context and consults `IOnExecutionAction.ShouldApplyHonorPenalty(...)`. Returning `false` from the inner Prefix skips the vanilla Honor-XP loss.

`TaomExecutionRelationModel` overrides `GetRelationChangeForExecutingHero` and routes through `IOnExecutionAction.GetRelationModifier(executorKingdomId, victimKingdomId, evaluatorKingdomId, baseRelationChange)`. The model returns:
- `0` for cross-alignment evaluators who share the executor's alignment
- `baseRelationChange` for evaluators who share the victim's alignment
- `baseRelationChange × 1.5` (kinslaying) when executor and victim share the same alignment

## Configuration

### Config File: `Main/_Module/ModuleData/execution/alignment.json`

A flat JSON object mapping kingdom `StringId` to alignment string.

| Field | Type | Description |
|-------|------|-------------|
| `<kingdom_id>` | `"free"` \| `"evil"` \| `"neutral"` | Alignment of that kingdom. Unknown kingdoms default to enemy-of-everyone via the lookup fallback (handled by `IAlignmentConfigProvider`). |

### Current Values

| Kingdom | Alignment | LOTR rationale |
|---|---|---|
| `empire_w` (Rohan) | free | — |
| `vlandia` (Gondor) | free | — |
| `erebor` | free | — |
| `sturgia` (Dale / North) | free | — |
| `rivendell` | free | — |
| `lothlorien` | free | — |
| `mirkwood` | free | — |
| `empire` (Dunland — note: counter-intuitive ID, see memory entry `kingdom-culture-mapping`) | evil | sided with Saruman in the books |
| `empire_s` (Mordor) | evil | — |
| `isengard` | evil | — |
| `gundabad` | evil | — |
| `dolguldur` | evil | — |
| `khuzait` (Easterlings) | evil | — |
| `battania` (Khand) | evil | — |
| `aserai` (Harad) | evil | — |
| `umbar` | neutral | corsair / mercenary — both sides can target |
| `shaghana` | neutral | tribal — both sides can target |
| `abanissa` | neutral | tribal — both sides can target |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Execution/AlignmentService.cs` | Maps kingdom IDs to alignments; answers `AreEnemyAlignments`, `AreSameAlignment`, `GetKingdomSide` |
| `Main/Features/Execution/IAlignmentService.cs` | Alignment query interface |
| `Main/Features/Execution/AlignmentConfigProvider.cs` | Loads + parses `alignment.json`; `Reuse.Singleton` (cached for process lifetime) |
| `Main/Features/Execution/IAlignmentConfigProvider.cs` | Config provider interface |
| `Main/Features/Execution/FactionSide.cs` | Enum: `Free`, `Evil`, `Neutral` |
| `Main/Features/Execution/Hooks/IOnExecutionAction.cs` | Decision hook interface — `ShouldApplyHonorPenalty`, `IsKinslaying`, `GetRelationModifier` |
| `Main/Features/Execution/Hooks/ExecutionActionHook.cs` | `IOnExecutionAction` implementation; consults `IAlignmentService` |
| `Main/Features/Execution/Hooks/ExecutionContext.cs` | `ThreadLocal<string>` victim/executor kingdom-ID pair; bridges the outer-Prefix to the inner-Prefix |
| `Main/Features/Execution/Hooks/KillCharacterAction_ApplyInternal_Patch.cs` | Outer Harmony Prefix + Finalizer; sets / clears `ExecutionContext` |
| `Main/Features/Execution/Hooks/TraitLevelingHelper_OnLordExecuted_Patch.cs` | Inner Harmony Prefix; skips vanilla Honor penalty when cross-alignment |
| `Main/Features/Execution/Models/TaomExecutionRelationModel.cs` | `DefaultExecutionRelationModel` override; routes relation deltas through `IOnExecutionAction.GetRelationModifier` |
| `Main/Features/Execution/ExecutionIoC.cs` | DryIoc registrations (singletons for all 3 services); `InitializeHooks` wires the manual-patch's hook reference |
| `Main/_Module/ModuleData/execution/alignment.json` | Kingdom → alignment data |

## Dependencies

- `IAlignmentService` (Execution feature) — public alignment-query API; consumed by `ExecutionActionHook` and (indirectly) by anyone needing alignment context
- `IOnExecutionAction` (Execution feature) — decision hook; consumed by both Harmony patches and `TaomExecutionRelationModel`
- `IPathService` (Core) — resolves the alignment.json path during config load
- `IModLogger` (Core) — used by `AlignmentConfigProvider` for load diagnostics

No `Adapter` interfaces — the feature operates entirely on kingdom `StringId` strings extracted at the patch entry point. The sealed types (`Hero`, `Clan`, `Kingdom`) are touched only in the patches and in the `TaomExecutionRelationModel` override body, both of which are entry-point classes per ADR-002 / ADR-007.

## Tests

- `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs` — **18 tests**: kingdom-to-side mapping (free/evil/neutral coverage), `AreEnemyAlignments` truth table (incl. neutral-vs-both), `AreSameAlignment` truth table, unknown-kingdom fallback behavior.
- `TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs` — **10 tests**: `ShouldApplyHonorPenalty` per alignment pairing, `IsKinslaying` per alignment pairing, `GetRelationModifier` four-way branching (cross-alignment-aligned, cross-alignment-against, same-alignment kinslaying, fallback path).

Manual Harmony Prefix + Finalizer wiring on `KillCharacterAction_ApplyInternal_Patch` and `TraitLevelingHelper_OnLordExecuted_Patch` is exercised via the live game; no unit-test coverage for the patch binding itself today. (Audit gap class — see issue #192 / #193 for the analogous wiring-regression-test pattern.)

## How to Re-tune a Kingdom's Alignment

Editing `alignment.json` is the only required change:

1. Open `Main/_Module/ModuleData/execution/alignment.json`.
2. Set the kingdom's value to `"free"`, `"evil"`, or `"neutral"`.
3. **Restart the game** — `AlignmentConfigProvider` is `Reuse.Singleton`, so the JSON is read once per process. Save-load is not enough; a new campaign is not enough; you need to relaunch the executable.
4. No C# changes needed. The next execution chain will pick up the new alignment immediately.

## How to Add a New Alignment-Aware Decision

If a new gameplay rule needs to gate on alignment (e.g., "different relation rules for prisoner negotiation"):

1. Add a method to `IOnExecutionAction.cs` (e.g., `int GetPrisonerExchangeRelationChange(...)`).
2. Implement it in `ExecutionActionHook.cs`, consulting `_alignmentService` as the existing methods do.
3. Where the new decision applies, route through `IoC.Resolve<IOnExecutionAction>()` from a single entry point (patch, GameModel, or behavior), the same pattern used by `TaomExecutionRelationModel`.
4. Add `ExecutionActionHookTests.cs` coverage for the new method's branches.

## Performance

`AlignmentService.GetKingdomSide` is an `O(1)` dictionary lookup; `AlignmentConfigProvider` caches the parsed `alignment.json` for the process lifetime. The decision hook is consulted once per execution kill chain and once per evaluating clan during relation preview (≤ 50 evaluations per execution UI render); no hot-path concerns.

`ExecutionContext.HasContext` is a single `ThreadLocal<string>` read — cheap and re-entrant-safe.

## Changelog

- 2026-05-14 — Phase 9b refactor (#147): extracted `IExecutionRelationService` returning `ExecutionRelationResult`, reduced `TaomExecutionRelationModel.GetRelationChangeForExecutingHero` to a single-call delegate, and replaced direct `Hero.MainHero.MapFaction.StringId` access with injected `IPlayerContextAdapter.GetPlayerKingdomId()`.
- 2026-03-25 — Introduced the Alignment-Aware Execution system: new `Main/Features/Execution/` override, `TaomExecutionRelationModel`, Harmony patches on `KillCharacterAction.ApplyInternal` + `TraitLevelingHelper.OnLordExecuted`, `execution/alignment.json` (16 kingdoms → Free/Evil/Neutral), zero penalty for cross-alignment kills, 1.5× kinslaying penalties, and 28 tests.

## GitHub Issue

- **Issue:** [#196](https://github.com/haterade22/TAOM/issues/196) — `audit-docs: Execution — docs/features/execution.md MISSING (Phase 0 #19 carryover)`
- **Status:** Closed by Phase 9b doc batch (this file)

## See also

- [`alignment-aware-execution.md`](alignment-aware-execution.md) — vanilla execution-flow reverse-engineering, decision-table rationale, kinslaying-multiplier derivation.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
