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

Two thin Harmony patches, one per seam. Each converts the sealed engine objects to `SideIds` pairs (`TAOM.Adapters.FactionSides`: the id and the culture of ONE entity) at the boundary and delegates to `IOnExecutionAction`; the relation half of that hook delegates on to `IExecutionRelationService`, the one owner of side resolution, cross-alignment zeroing and the kinslaying multiplier.

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

Both patches take the executor from `FactionSides.OfPlayer()` and the victim from the `Hero` the engine passes in (`FactionSides.OfHero`); the relation postfix takes the evaluator from the `Clan` (`FactionSides.Of(otherClan.MapFaction)`). The Honor seam is player-only. The relation seam is not: when an AI clan executes a member of the player's clan, vanilla starts the feud the other way round and still runs the same loop against the player's relations, so the hook's `IsPlayerTheBereaved` (the victim's clan is the player's clan) sends that path back to vanilla's number untouched. The victim's clan may already be destroyed when the Honor prefix runs (`ApplyInternal` destroys it before dispatching `OnHeroKilled`), which nulls its kingdom; that is why every participant carries the culture id as well, of the same entity, and `AlignmentService.ResolveSide` falls back to it. There is deliberately no "unknown, defer to vanilla" early return (#556). The relation postfix returns whatever the service decides: a zero trips the engine's own `!= 0` guard, so that clan takes no hit and is not counted in the summary notice. The same static method feeds the pre-execution "this will hurt your relations with N clans" tooltip, so the warning and the outcome read the same rule. They can differ when the kill destroys the victim's clan: the tooltip reads the victim before the kill, still in its kingdom, and the outcome reads its own pair afterwards (a kingdom clan resolves its kingdom's side on its own pair, which `ShippedClanSideCoverageTests` checks). The feud itself, the victim clan's relation floor, is left to vanilla: a clan whose kinsman you beheaded is entitled to hunt you whatever side either of you is on.

There is no snapshot any more. The v1.4.x design captured executor and victim in a thread-local `ExecutionContext` at the top of `KillCharacterAction.ApplyInternal` because the honour seam had no parameters and the relation pass ran after the victim's clan was torn down; v1.5.0 gave `OnBloodFeudStarted` the executed hero and moved the per-clan penalty into a static method that receives the dying hero and the evaluating clan, and vanilla reaches that loop only while the feud clan still has a kingdom, so every read is live (`FactionSides`), with the culture fallback covering a kingdom-less side. The old ordering is kept for the record in `alignment-aware-execution.md` (#556).

The relation postfix builds three `SideIds`: the victim from `FactionSides.OfHero(dyingHero)` (the clan's map faction with that faction's culture, or a clanless hero's own culture), the evaluator from `FactionSides.Of(otherClan.MapFaction)`, and the executor from `FactionSides.OfPlayer()` (the player clan's map faction with that faction's culture; a mercenary player reads the employer's realm, because a contract sets `Clan.Kingdom`). It routes the engine's integer through `IOnExecutionAction.GetRelationModifier(executor, victim, evaluator, baseRelationDelta)`, which calls `IExecutionRelationService.GetRelationModifier` with `baseShowNotification: false`. The service returns:
- `0` for cross-alignment evaluators who share the executor's alignment
- `baseRelationChange` for evaluators who share the victim's alignment
- `baseRelationChange × 1.5` (kinslaying) when executor and victim share the same alignment

## Configuration

### Config File: `Main/_Module/ModuleData/execution/alignment.json`

A flat JSON object mapping a `StringId` to an alignment string. Keys are read as kingdom ids, culture ids
and minor clan ids: `AlignmentService` uses one table for `GetKingdomSide` and `GetCultureSide`, and
`ResolveSide(id, culture)` tries the id first (a kingdom, or a minor clan) and falls back to the culture of the
same entity. An id that appears in none of those roles resolves Neutral, which is nobody's ally and everybody's enemy.

| Field | Type | Description |
|-------|------|-------------|
| `<kingdom_culture_or_clan_id>` | `"free"` \| `"evil"` \| `"neutral"` | Alignment of that faction. An unlisted id resolves Neutral, which is indistinguishable from an explicit `"neutral"` at runtime, and a `neutral` clan row is a no-op (`ResolveSide` falls through to the culture): a clan row is `free` or `evil`. That is why the coverage gaps are closed by build-time gates rather than at runtime: `ShippedMainCultureAlignmentCoverageTests` (playable cultures), `ShippedCultureAlignmentCoverageTests` (cultures used by `lords.xml`) and `ShippedClanSideCoverageTests` (minor clans and kingdom clans). |

### Current Values

Kingdom ids are what `Hero.Clan.Kingdom.StringId` returns at runtime. Eight are vanilla ids that
`Main/_Module/ModuleData/spkingdoms.xslt` renames rather than replaces (`empire`, `empire_w`, `empire_s`,
`sturgia`, `aserai`, `vlandia`, `battania`, `khuzait`); the other 22 rows are kingdoms that
`taom_spkingdoms.xml` defines outright. The in-game names below are taken from those files, not from the id.

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
| `arthedain` | Arthedain | free | |
| `eredluin` | Ered Luin | free | |
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
| `cardolan` | Cardolan | neutral | an Arnor remnant that stands apart from the War of the Ring (#978) |
| `rhudaur` | Rhudaur | neutral | an Arnor remnant that stands apart from the War of the Ring (#978) |
| `anduinvale` | Anduin Vale | neutral | |
| `angaladh` | Angáladh | neutral | |
| `dorwinion` | Dorwinion | neutral | |
| `southrhovanion` | South Rhovanion | neutral | |

Two further keys are **culture** ids, not kingdom ids. Every other playable culture shares its id
with its kingdom, but Gondor and Mordor do not, so they need their own entries for the culture
fallback to place a kingdom-less hero:

| Culture id | Kingdom | Alignment |
|---|---|---|
| `gondor` | `empire_w` | free |
| `mordor` | `empire_s` | evil |

Four further keys are **minor clan** ids, for the clans whose own culture is unlisted or on the wrong side:
`ghilman` (Serpent Guard, evil), `skolderbrotva` (Axemen of Erebor, free), `forest_people` (Druedain, free) and
`eleftheroi` (Beornings, free). The file holds 36 keys in all (30 kingdoms, `gondor`, `mordor`, the 4 clans). The
other ten minor clans resolve through their culture; the per-clan table is in
[alignment-aware-execution.md](alignment-aware-execution.md) ("Minor Clan ID Reference").

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Execution/AlignmentService.cs` | Maps kingdom IDs to alignments; answers `AreEnemyAlignments`, `AreSameAlignment`, `GetKingdomSide` |
| `Main/Features/Execution/IAlignmentService.cs` | Alignment query interface |
| `Main/Features/Execution/SideIds.cs` | The id and culture pair of one entity |
| `Main/Features/Execution/AlignmentServiceExtensions.cs` | `ResolveSide(this IAlignmentService, SideIds)` |
| `Main/Adapters/FactionSides.cs` | The one place a `SideIds` is built from engine objects |
| `Main/Features/Execution/AlignmentConfigProvider.cs` | Loads + parses `alignment.json`; `Reuse.Singleton` (cached for process lifetime) |
| `Main/Features/Execution/IAlignmentConfigProvider.cs` | Config provider interface |
| `Main/Features/Execution/FactionSide.cs` | Enum: `Free`, `Evil`, `Neutral` |
| `Main/Features/Execution/Hooks/IOnExecutionAction.cs` | Boundary hook: `ShouldApplyHonorPenalty` (honor half) and `GetRelationModifier` (relation half) |
| `Main/Features/Execution/IExecutionRelationService.cs` | `ExecutionRelationResult`, relation contract (takes `SideIds`) |
| `Main/Features/Execution/ExecutionRelationService.cs` | Relation decision: side resolution, kinslaying, notification suppression |
| `Main/Features/Execution/Hooks/ExecutionActionHook.cs` | `IOnExecutionAction` implementation; honor half consults `IAlignmentService`, relation half delegates to `IExecutionRelationService` with notifications off |
| `Main/Features/Execution/Hooks/TraitLevelingHelper_OnBloodFeudStarted_Patch.cs` | Harmony Prefix on `TraitLevelingHelper.OnBloodFeudStarted(Hero)`; returns `ShouldApplyHonorPenalty`, `false` skips the vanilla Honor hit |
| `Main/Features/Execution/Hooks/ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.cs` | Harmony Postfix on the static `ExecutionCampaignBehavior.GetBloodFeudStartRelationPenaltyToOtherClan(Hero, Clan)`; rewrites the per-clan penalty through the hook |
| `Main/Features/Execution/ExecutionIoC.cs` | DryIoc registrations (singletons for all 3 services); `InitializeHooks` hands the Honor patch the hook and the relation patch the hook and `IPlayerContextAdapter` (for the player clan id) |
| `Main/_Module/ModuleData/execution/alignment.json` | Kingdom → alignment data |
| `Main/_Module/GUI/PreFabs/Party/PartyTroopTuple.xml` | TAOM's copy of the right party tuple; its Execute button matches vanilla v1.5.5, blood-feud mark included (#1031) |

## Dependencies

- `IAlignmentService` (Execution feature) — public alignment-query API; consumed by `ExecutionActionHook` and (indirectly) by anyone needing alignment context
- `IOnExecutionAction` (Execution feature): both decisions, consumed by `TraitLevelingHelper_OnBloodFeudStarted_Patch` and `ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch`
- `IExecutionRelationService` (Execution feature): relation decision, consumed by `ExecutionActionHook`
- `IPlayerContextAdapter` (Adapters): the player clan id, read by the relation postfix for the bereaved test
- `IPathService` (Core) — resolves the alignment.json path during config load
- `IModLogger` (Core) — used by `AlignmentConfigProvider` for load diagnostics

No feature-specific adapters: participants are `SideIds` pairs built by `TAOM.Adapters.FactionSides` at the two patch entry points from the `Hero` and `Clan` the engine passes. The sealed types are touched only there, which is what ADR-002 / ADR-007 allow for an entry point.

## Tests

- `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs`: **36 tests**, kingdom- and culture-to-side mapping, `ResolveSide` precedence and fallback, both truth tables in their string and `FactionSide` forms, unknown-id behavior.
- `TAOM.Tests/Features/Execution/ExecutionActionHookTests.cs`: **12 tests**, `ShouldApplyHonorPenalty` per alignment pairing plus the kingdom-less executor and destroyed-victim-clan paths, and `GetRelationModifier` delegation: the service delta is returned unchanged (zero included), every participant reaches the service intact, notifications are requested off; `IsPlayerTheBereaved` for the player's clan, another clan and an unknown clan.
- `TAOM.Tests/Features/Execution/ExecutionRelationServiceTests.cs`: **17 tests**, cross-alignment branching, kinslaying multiplier, notification suppression, and a kingdom-less participant in each of the three positions.
- `TAOM.Tests/Features/Execution/ShippedMainCultureAlignmentCoverageTests.cs`: **3 tests**, every playable TAOM culture has an alignment entry and resolves to its declared side through the kingdom-less path.
- `TAOM.Tests/Features/Execution/ShippedClanSideCoverageTests.cs`: **14 tests**, the data gate for the clan rows (every minor clan and kingdom clan, the troops and leader templates); described once in [alignment-aware-execution.md](alignment-aware-execution.md) ("Tests").
- `TAOM.Tests/Features/Execution/SidePairScanTests.cs`: **3 tests**, the IL gate on `ResolveSide(id, culture)` callers and on where a `SideIds` is built; described once in the same place.
- `TAOM.Tests/Features/Execution/PartyTupleVanillaParityTests.cs`: **3 tests**, both of TAOM's party tuple copies against the installed vanilla prefabs, element for element apart from TAOM's two edits (LiveInstall, one per tuple), and the blood-feud mark beside the Execute skull, which runs without the game (#1031).

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
3. Where the new decision applies, inject the interface into a single entry point (patch, GameModel, or behavior), the same pattern the two `Patch14_Execution` patches use. Resolve sides through a `SideIds` pair from `FactionSides` and `IAlignmentService.ResolveSide(SideIds)`, never `GetKingdomSide` alone.
4. Add `ExecutionActionHookTests.cs` coverage for the new method's branches.

## How to Test In Game

1. Gondor character: capture a Mordor lord and execute that lord from the party screen. Expected: no Honor penalty, and the relation preview shows no hit from the Free Peoples' clans. The Execute button shows the small blood-feud mark at its lower right, as in vanilla.
2. Capture a Rohan lord and execute that lord. Expected: kinslaying, 1.5 times vanilla for the victim's side, and the Honor penalty applies.
3. Capture a lord of a Neutral realm (Khand), ideally one born into a Free culture, and execute that lord. Expected: no Honor penalty and no relation hit from anyone, as for any Neutral lord (#957: before, such a lord read as Free and the kill counted as kinslaying).

The full walk-through is in [alignment-aware-execution.md](alignment-aware-execution.md) ("How to Test In-Game").

## Performance

`AlignmentService.GetKingdomSide` is an `O(1)` dictionary lookup; `AlignmentConfigProvider` caches the parsed `alignment.json` for the process lifetime. The decision hook is consulted once per execution kill chain and once per evaluating clan during relation preview (≤ 50 evaluations per execution UI render); no hot-path concerns.

## Changelog

- 2026-10-20 (#957, part 2): every participant is a `SideIds` pair (the id and the culture of ONE entity) built by `FactionSides`; `ExecutionParticipant` and `IPlayerContextAdapter.GetPlayerCultureId()` are gone, the patches call `FactionSides.OfPlayer()`, and a lord of a Neutral realm is no longer sided by the lord's own culture. Four minor clan rows in `alignment.json`; see [alignment-aware-execution.md](alignment-aware-execution.md).
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

- [docs/features/alignment-aware-execution.md](./alignment-aware-execution.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/migration/v1.5.2-impact.md](../migration/v1.5.2-impact.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/modding/kingdoms.md](../modding/kingdoms.md)

<!-- backlinks-end -->
