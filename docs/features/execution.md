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

A two-pronged hook: thread-local context for the trait-penalty patch, and a GameModel override for the relation calculations. Both resolve their participants through the same `IAlignmentService`, the honor patch via `IOnExecutionAction` and the model via `IExecutionRelationService`.

```
Main/_Module/ModuleData/execution/alignment.json
        |
  AlignmentConfigProvider (loads kingdomId -> "free"/"evil"/"neutral")
        |
  AlignmentService (ResolveSide, GetKingdomSide / GetCultureSide,
                    AreEnemyAlignments / AreSameAlignment)
        |
        +---------------------------+
        |                           |
  ExecutionActionHook         ExecutionRelationService
  : IOnExecutionAction        : IExecutionRelationService
        |                           |
        |                    TaomExecutionRelationModel
        |                     (override of vanilla
        |                      GetRelationChangeForExecutingHero)
        |                           |
        +----- ExecutionContext ----+
                    |
      KillCharacterAction_ApplyInternal_Patch
        (Prefix snapshots victim + executor,
         kingdom AND culture; Finalizer clears)
                    |
      TraitLevelingHelper_OnLordExecuted_Patch
        (Prefix reads the snapshot; skips the
         vanilla Honor penalty when cross-alignment)
```

`ExecutionContext` holds thread-local kingdom **and** culture ids for both victim and executor, plus an explicit active flag (a hero legitimately has no kingdom, so the flag cannot be inferred from a null id). The outer Prefix on `ApplyInternal` populates it; the Finalizer clears it. Inside that scope, the inner Prefix on `OnLordExecuted` reads the snapshot and consults `IOnExecutionAction.ShouldApplyHonorPenalty(...)`. Returning `false` skips the vanilla Honor-XP loss.

The snapshot is taken at the top of `ApplyInternal` for a reason: the method destroys the victim's clan, which nulls `Clan.Kingdom`, before it fires the event that drives the relation pass. Full ordering in `alignment-aware-execution.md` (#556).

`TaomExecutionRelationModel` overrides `GetRelationChangeForExecutingHero` and routes through `IExecutionRelationService.GetRelationModifier(executor, victim, evaluator, baseRelationChange, baseShowNotification)`, passing an `ExecutionParticipant` (kingdom id plus culture id) for each. Executor and victim come from the snapshot when a kill is in flight, live otherwise; the evaluator is always live. The service returns:
- `0` for cross-alignment evaluators who share the executor's alignment
- `baseRelationChange` for evaluators who share the victim's alignment
- `baseRelationChange × 1.5` (kinslaying) when executor and victim share the same alignment

## Configuration

### Config File: `Main/_Module/ModuleData/execution/alignment.json`

A flat JSON object mapping a `StringId` to an alignment string. Keys are read as **both** kingdom ids
and culture ids: `AlignmentService` uses one table for `GetKingdomSide` and `GetCultureSide`, and
`ResolveSide(kingdomId, cultureId)` tries the kingdom first and falls back to the culture. An id that
appears in neither role resolves Neutral, which is nobody's ally and everybody's enemy.

| Field | Type | Description |
|-------|------|-------------|
| `<kingdom_or_culture_id>` | `"free"` \| `"evil"` \| `"neutral"` | Alignment of that faction. An unlisted id resolves Neutral, which is indistinguishable from an explicit `"neutral"` at runtime. That is why the coverage gaps are closed by build-time gates rather than at runtime: `ShippedMainCultureAlignmentCoverageTests` (playable cultures) and `ShippedCultureAlignmentCoverageTests` (cultures used by `lords.xml`). |

### Current Values

Kingdom ids are what `Hero.Clan.Kingdom.StringId` returns at runtime, so most of them are the vanilla
ids that `Main/_Module/ModuleData/spkingdoms.xslt` renames rather than replaces. The in-game names
below are taken from that XSLT, not from the id.

| Kingdom id | In-game name | Alignment | Rationale |
|---|---|---|---|
| `empire_w` | Gondor | free | |
| `vlandia` | Rohan | free | |
| `sturgia` | Dale / the North | free | |
| `erebor` | Erebor | free | |
| `rivendell` | Rivendell | free | |
| `lothlorien` | Lothlorien | free | |
| `mirkwood` | Mirkwood | free | |
| `lindon` | Lindon | free | |
| `empire` | Dunland | evil | sided with Saruman in the books. Counter-intuitive id, see memory entry `kingdom-culture-mapping` |
| `empire_s` | Mordor | evil | |
| `aserai` | Harad | evil | |
| `khuzait` | Rhun (Easterlings) | evil | |
| `isengard` | Isengard | evil | |
| `gundabad` | Gundabad | evil | |
| `dolguldur` | Dol Guldur | evil | |
| `goblin` | Goblin-town | evil | |
| `mistymountainorcs` | Misty Mountain orcs | evil | |
| `bluecraig` | Blue Craig | evil | |
| `battania` | Khand | neutral | tribal and mercenary, so both sides can target it |
| `umbar` | Umbar | neutral | corsair and mercenary, so both sides can target it |
| `shaghana` | Shaghana | neutral | tribal, so both sides can target it |
| `abanissa` | Abanissa | neutral | tribal, so both sides can target it |

Two further keys are **culture** ids, not kingdom ids. Every other playable culture shares its id
with its kingdom, but Gondor and Mordor do not, so they need their own entries for the culture
fallback to place a kingdom-less hero:

| Culture id | Kingdom | Alignment |
|---|---|---|
| `gondor` | `empire_w` | free |
| `mordor` | `empire_s` | evil |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Execution/AlignmentService.cs` | Maps kingdom IDs to alignments; answers `AreEnemyAlignments`, `AreSameAlignment`, `GetKingdomSide` |
| `Main/Features/Execution/IAlignmentService.cs` | Alignment query interface |
| `Main/Features/Execution/AlignmentConfigProvider.cs` | Loads + parses `alignment.json`; `Reuse.Singleton` (cached for process lifetime) |
| `Main/Features/Execution/IAlignmentConfigProvider.cs` | Config provider interface |
| `Main/Features/Execution/FactionSide.cs` | Enum: `Free`, `Evil`, `Neutral` |
| `Main/Features/Execution/Hooks/IOnExecutionAction.cs` | Honor-penalty decision hook: `ShouldApplyHonorPenalty` only |
| `Main/Features/Execution/IExecutionRelationService.cs` | `ExecutionParticipant`, `ExecutionRelationResult`, relation contract |
| `Main/Features/Execution/ExecutionRelationService.cs` | Relation decision: side resolution, kinslaying, notification suppression |
| `Main/Features/Execution/Hooks/ExecutionActionHook.cs` | `IOnExecutionAction` implementation; consults `IAlignmentService` |
| `Main/Features/Execution/Hooks/ExecutionContext.cs` | `ThreadLocal<string>` victim/executor kingdom-ID pair; bridges the outer-Prefix to the inner-Prefix |
| `Main/Features/Execution/Hooks/KillCharacterAction_ApplyInternal_Patch.cs` | Outer Harmony Prefix + Finalizer; sets / clears `ExecutionContext` |
| `Main/Features/Execution/Hooks/TraitLevelingHelper_OnLordExecuted_Patch.cs` | Inner Harmony Prefix; skips vanilla Honor penalty when cross-alignment |
| `Main/Features/Execution/Models/TaomExecutionRelationModel.cs` | `DefaultExecutionRelationModel` override; routes relation deltas through `IExecutionRelationService` |
| `Main/Features/Execution/ExecutionIoC.cs` | DryIoc registrations (singletons for all 3 services); `InitializeHooks` wires the manual-patch's hook reference |
| `Main/_Module/ModuleData/execution/alignment.json` | Kingdom → alignment data |

## Dependencies

- `IAlignmentService` (Execution feature) — public alignment-query API; consumed by `ExecutionActionHook` and (indirectly) by anyone needing alignment context
- `IOnExecutionAction` (Execution feature): honor-penalty decision, consumed by `TraitLevelingHelper_OnLordExecuted_Patch`
- `IExecutionRelationService` (Execution feature): relation decision, consumed by `TaomExecutionRelationModel`
- `IPathService` (Core) — resolves the alignment.json path during config load
- `IModLogger` (Core) — used by `AlignmentConfigProvider` for load diagnostics

No `Adapter` interfaces — the feature operates entirely on kingdom `StringId` strings extracted at the patch entry point. The sealed types (`Hero`, `Clan`, `Kingdom`) are touched only in the patches and in the `TaomExecutionRelationModel` override body, both of which are entry-point classes per ADR-002 / ADR-007.

## Tests

- `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs`: **35 tests**, kingdom- and culture-to-side mapping, `ResolveSide` precedence and fallback, both truth tables in their string and `FactionSide` forms, unknown-id behavior.
- `TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs`: **5 tests**, `ShouldApplyHonorPenalty` per alignment pairing, plus the kingdom-less executor and destroyed-victim-clan paths.
- `TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs`: **18 tests**, cross-alignment branching, kinslaying multiplier, notification suppression, and a kingdom-less participant in each of the three positions.
- `TAOM.Tests/Features/Execution/ShippedMainCultureAlignmentCoverageTests.cs`: **3 tests**, every playable TAOM culture has an alignment entry and resolves to its declared side through the kingdom-less path.

Manual Harmony Prefix + Finalizer wiring on `KillCharacterAction_ApplyInternal_Patch` and `TraitLevelingHelper_OnLordExecuted_Patch` is exercised via the live game; no unit-test coverage for the patch binding itself today. (Audit gap class — see issue #192 / #193 for the analogous wiring-regression-test pattern.)

## How to Re-tune a Kingdom's Alignment

Editing `alignment.json` is the only required change:

1. Open `Main/_Module/ModuleData/execution/alignment.json`.
2. Set the kingdom's value to `"free"`, `"evil"`, or `"neutral"`.
3. **Restart the game** — `AlignmentConfigProvider` is `Reuse.Singleton`, so the JSON is read once per process. Save-load is not enough; a new campaign is not enough; you need to relaunch the executable.
4. No C# changes needed. The next execution chain will pick up the new alignment immediately.

## How to Add a New Alignment-Aware Decision

If a new gameplay rule needs to gate on alignment (e.g., "different relation rules for prisoner negotiation"):

1. Add a method to the relevant interface: `IExecutionRelationService.cs` for a relation decision, `IOnExecutionAction.cs` for a trait or penalty decision.
2. Implement it in `ExecutionActionHook.cs`, consulting `_alignmentService` as the existing methods do.
3. Where the new decision applies, inject the interface into a single entry point (patch, GameModel, or behavior), the same pattern used by `TaomExecutionRelationModel`. Resolve sides via `IAlignmentService.ResolveSide`, never `GetKingdomSide` alone.
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
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/modding/kingdoms.md](../modding/kingdoms.md)

<!-- backlinks-end -->
