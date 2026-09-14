# Execution

## Overview

The Execution feature replaces vanilla Bannerlord's one-size-fits-all lord execution penalties with a LOTR-thematic system. When a Free Peoples lord executes a servant of Sauron there is no dishonor; when a lord executes one of their own allies it is **kinslaying**, punished more harshly than vanilla. The feature sits on the engine's blood-feud execution fallout (Bannerlord v1.5.x): a Harmony prefix gates the Honor-trait penalty by the executor's alignment and a postfix reshapes the per-clan relation penalty.

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

Since Bannerlord v1.5.0 an execution lands in the engine as a **blood feud**. `KillCharacterAction.ApplyInternal` kills the hero; for a player execution `ExecutionCampaignBehavior` then starts a feud with the victim's clan: that clan's relation drops to the floor, every OTHER clan receives a per-clan penalty computed by the static `GetBloodFeudStartRelationPenaltyToOtherClan(Hero dyingHero, Clan otherClan)`, and `TraitLevelingHelper.OnBloodFeudStarted(Hero executedHero)` applies the Honor hit. Nothing in that chain knows about alignment, and none of it is a GameModel any more: v1.5.0 deleted `ExecutionRelationModel` outright, and with it the argument-less `OnLordExecuted()` that once forced TAOM to smuggle the participants through a thread-local snapshot taken in an `ApplyInternal` prefix. TAOM needs:

1. The Honor hit skipped when executor and victim stand on opposing sides, without touching the feud itself.
2. The per-clan penalty reshaped per evaluating clan: zero for clans on the executor's side, vanilla for the victim's side, 1.5x for kinslaying.

### Solution Approach

Two thin Harmony patches, one per seam. Each converts the sealed engine objects to `ExecutionParticipant(kingdomId, cultureId)` at the boundary and delegates to `IOnExecutionAction`; the relation half of that hook delegates on to `IExecutionRelationService`, the one owner of side resolution, cross-alignment zeroing and the kinslaying multiplier.

```
Main/_Module/ModuleData/execution/alignment.json
        |
  AlignmentConfigProvider (loads id -> "free"/"evil"/"neutral")
        |
  AlignmentService (ResolveSide with culture fallback,
                    AreEnemyAlignments / AreSameAlignment)
        |
        +---------------------------+
        |                           |
  ExecutionActionHook  ------>  ExecutionRelationService
  : IOnExecutionAction          : IExecutionRelationService
        |                       (side resolution, cross-alignment
        |                        zeroing, kinslaying x1.5)
        +-----------------------------------+
        |                                   |
  TraitLevelingHelper_                ExecutionCampaignBehavior_
  OnBloodFeudStarted_Patch            BloodFeudRelationPenalty_Patch
  (Prefix: returns                    (Postfix on the static per-clan
   ShouldApplyHonorPenalty;            penalty: rewrites the int through
   false skips the Honor hit)          GetRelationModifier; 0 = no hit)
```

Both patches take the executor from `IPlayerContextAdapter` and the victim from the `Hero` the engine passes in; the relation postfix takes the evaluator from the `Clan`. The Honor seam is player-only. The relation seam is not: when an AI clan executes a member of the player's clan, vanilla starts the feud the other way round and still runs the same loop against the player's relations, so the hook's `IsPlayerTheBereaved` (the victim's clan is the player's clan) sends that path back to vanilla's number untouched. The victim's clan may already be destroyed when the Honor prefix runs (`ApplyInternal` destroys it before dispatching `OnHeroKilled`), which nulls its kingdom; that is why every participant carries the culture id as well and `AlignmentService.ResolveSide` falls back to it. There is deliberately no "unknown, defer to vanilla" early return (#556). The relation postfix returns whatever the service decides: a zero trips the engine's own `!= 0` guard, so that clan takes no hit and is not counted in the summary notice. The same static method feeds the pre-execution "this will hurt your relations with N clans" tooltip, so the warning and the outcome cannot disagree. The feud itself, the victim clan's relation floor, is left to vanilla: a clan whose kinsman you beheaded is entitled to hunt you whatever side either of you is on.

There is no snapshot any more. The v1.4.x design captured executor and victim in a thread-local `ExecutionContext` at the top of `KillCharacterAction.ApplyInternal` because the honour seam had no parameters and the relation pass ran after the victim's clan was torn down; v1.5.0 gave `OnBloodFeudStarted` the executed hero and moved the per-clan penalty into a static method that receives the dying hero and the evaluating clan, and vanilla reaches that loop only while the feud clan still has a kingdom, so every read is live (`Clan.Kingdom`, `Hero.Culture`), with the culture fallback covering a kingdom-less side. The old ordering is kept for the record in `alignment-aware-execution.md` (#556).

The relation postfix reads `dyingHero.Clan.Kingdom` / `dyingHero.Culture` for the victim, `otherClan.Kingdom` / `otherClan.Culture` for the evaluator, and the player's kingdom and culture (via `IPlayerContextAdapter`) for the executor, wraps each as an `ExecutionParticipant` (kingdom id plus culture id) and routes the engine's integer through `IOnExecutionAction.GetRelationModifier(executor, victim, evaluator, baseRelationDelta)`, which calls `IExecutionRelationService.GetRelationModifier` with `baseShowNotification: false`. The service returns:
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
| `Main/Features/Execution/Hooks/IOnExecutionAction.cs` | Boundary hook: `ShouldApplyHonorPenalty` (honor half) and `GetRelationModifier` (relation half) |
| `Main/Features/Execution/IExecutionRelationService.cs` | `ExecutionParticipant`, `ExecutionRelationResult`, relation contract |
| `Main/Features/Execution/ExecutionRelationService.cs` | Relation decision: side resolution, kinslaying, notification suppression |
| `Main/Features/Execution/Hooks/ExecutionActionHook.cs` | `IOnExecutionAction` implementation; honor half consults `IAlignmentService`, relation half delegates to `IExecutionRelationService` with notifications off |
| `Main/Features/Execution/Hooks/TraitLevelingHelper_OnBloodFeudStarted_Patch.cs` | Harmony Prefix on `TraitLevelingHelper.OnBloodFeudStarted(Hero)`; returns `ShouldApplyHonorPenalty`, `false` skips the vanilla Honor hit |
| `Main/Features/Execution/Hooks/ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.cs` | Harmony Postfix on the static `ExecutionCampaignBehavior.GetBloodFeudStartRelationPenaltyToOtherClan(Hero, Clan)`; rewrites the per-clan penalty through the hook |
| `Main/Features/Execution/ExecutionIoC.cs` | DryIoc registrations (singletons for all 3 services); `InitializeHooks` hands both patches the hook and `IPlayerContextAdapter` |
| `Main/_Module/ModuleData/execution/alignment.json` | Kingdom → alignment data |

## Dependencies

- `IAlignmentService` (Execution feature) — public alignment-query API; consumed by `ExecutionActionHook` and (indirectly) by anyone needing alignment context
- `IOnExecutionAction` (Execution feature): both decisions, consumed by `TraitLevelingHelper_OnBloodFeudStarted_Patch` and `ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch`
- `IExecutionRelationService` (Execution feature): relation decision, consumed by `ExecutionActionHook`
- `IPlayerContextAdapter` (Adapters): the executor's kingdom and culture ids, read by both patches
- `IPathService` (Core) — resolves the alignment.json path during config load
- `IModLogger` (Core) — used by `AlignmentConfigProvider` for load diagnostics

No feature-specific adapters: participants are `(kingdomId, cultureId)` string pairs built at the two patch entry points from the `Hero` and `Clan` the engine passes and from `IPlayerContextAdapter`. The sealed types are touched only there, which is what ADR-002 / ADR-007 allow for an entry point.

## Tests

- `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs`: **35 tests**, kingdom- and culture-to-side mapping, `ResolveSide` precedence and fallback, both truth tables in their string and `FactionSide` forms, unknown-id behavior.
- `TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs`: **9 tests**, `ShouldApplyHonorPenalty` per alignment pairing plus the kingdom-less executor and destroyed-victim-clan paths, and `GetRelationModifier` delegation: the service delta is returned unchanged (zero included), every participant reaches the service intact, notifications are requested off.
- `TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs`: **18 tests**, cross-alignment branching, kinslaying multiplier, notification suppression, and a kingdom-less participant in each of the three positions.
- `TAOM.Tests/Features/Execution/ShippedMainCultureAlignmentCoverageTests.cs`: **3 tests**, every playable TAOM culture has an alignment entry and resolves to its declared side through the kingdom-less path.

The two Harmony patches bind by attribute and are exercised in the live game; `HarmonyPatchBindingTests` proves both targets resolve on the installed engine, and `docs/reference/taleworlds-api-snapshot/patch-targets.md` records their v1.5.2 signatures.

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
3. Where the new decision applies, inject the interface into a single entry point (patch, GameModel, or behavior), the same pattern the two `Patch14_Execution` patches use. Resolve sides via `IAlignmentService.ResolveSide`, never `GetKingdomSide` alone.
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
