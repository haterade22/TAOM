# RCA: battle-death roster crash and the Patch169 review (#1475, 2026-11-18)

## Top-line

A player's crash bundle showed `ArgumentOutOfRangeException` in `LinQuick.WhereQ` under `MapEventSide.HandleMapEventEndForPartyInternal`, twice in one session. The second throw hit the player's own siege defence. The player pressed Continue on the TAOM crash dialog and saved, and that save then threw a `NullReferenceException` in `SiegeAssaultEventComponent.OnPlayerEncounterFinalizeBattle` on every load. The cause was LordRetinue's `OnPartyDisbandStarted` handler: it called `GetTroopRoster()`, which refilled the roster cache that the engine's kill loop was still indexing. It shipped in v2.0.38 (`9f6b2c98f`).

The fix has two layers: LordRetinue reads the roster by index, and `Patch169_BattleDeathRosterSnapshot` makes the engine loop walk a filled copy. `/deep-review` (Lens 5 and Lens 2, then a second wave) found one HIGH in the first draft of Patch169: it replaced the `WhereQ` call, and Coop's own transpiler on the same method throws when that call is gone. The fix keeps the `WhereQ` and inserts one call after it. The second wave found that two open PRs by another author (#1463, and #1477 built on it) already carried a different `Patch169` for the same crash, with the replacing design. Mike chose this branch's Patch169, with #1477's save repair to rebase onto it. Codex did not run (no usage since 2026-11-15); that pass is owed before merge.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| P0 | HIGH (player crash, shipped) | `LordRetinueWorld.ReleaseAtDisband` ran `foreach` over `GetTroopRoster()` inside a listener that the engine fires from inside its own loop over that list. Every AI lord killed in battle as his party's only hero could throw, and a save made after Continue would not load. | Listener re-entrancy into an engine loop | The feature's reader list covered who reads the retinue hero, not where the disband event fires from. The disband was pictured as a map-time event (a marriage). The behavior had no unit test and no in-game check before release. | Lessons entry in `campaign-mechanics.md`; trap row "Kill-path listeners" in `.claude/rules/traps/campaign.md`; `LordRetinueRosterReadTests` source gate; Patch169 as the engine floor. |
| F1 | HIGH (Lens 2) | The first Patch169 draft replaced the operand of the one `WhereQ<TroopRosterElement>` call. Coop's transpiler on the same method (BannerlordCoop `MapEventSidePatches.cs:152-203`) finds that call by name, inserts `ToArray` after it, and throws when it is gone. When TAOM's transpiler is registered first (a campaign loaded in the same process before a co-op join or host), Coop's `PatchAll` throws and the join is abandoned. | Patch overlap with another loaded mod | The plan and the first wave of tests were checked against vanilla IL only. Nobody searched the Coop source for the target method, although the lesson "A veto prefix on a method another mod also prefixes" says to, because that lesson is worded for prefixes and lives only in `harmony-il.md`, which a builder reads by heading. `harmony-patches.md`, the path rule that loads on every hook edit, has no line on overlap. | Repeat cause (#1343, #1346 for prefixes). Promote the rule to `harmony-patches.md` for every patch shape; lessons entry in `harmony-il.md`; `TheTranspiler_AndCoopsTranspiler_BothApply_InEitherOrder` runs a Coop-shaped matcher in both orders. |
| F2 | HIGH (Lens 1) | PR #1463 (open since 2026-11-18 20:42 UTC) already added `Patch169_BattleEndHeroDeathSnapshot` on the same method for the same crash, and PR #1477 builds a save repair on it. Two patches would share one id and both rewrite one call. #1463 also replaces the `WhereQ`, so F1 applies to it. | Duplicate work across sessions | The work claim (`crash-9469e856`) is keyed by the crash signature, and the other author's work used no claim with that key. `/investigate` Phase 1 reads `git log` of the affected files on the current branch only, so unmerged branches and open PRs were never searched. | `/investigate` Phase 1 step 3 also searches open PRs and all branches for the throw site; lessons entry in `build-tooling-workflow.md`. |

## Root-cause pattern

All three findings come from one blind spot: the change was checked against the engine method alone, not against the other code that runs in the same place. P0 is TAOM code that runs inside an engine loop through an event. F1 is another mod's transpiler that runs on the same method body. F2 is the same blind spot across people: another branch that changes the same method. In each case the facts were readable before the first edit: the `CampaignEventDispatcher` call sites for P0, a grep of the Coop source for F1, and `gh pr list` for F2.

## Why each agent missed these

- **Lens 5 (Data flow):** it traced TAOM's data from the event to the roster, and the patch category to `ModuleRunner`. Another mod's patch on the target is outside a TAOM data flow, so it reported no gap.
- **Lens 2 (Engine compatibility):** it found F1. It was the only lens that read another mod's patches on the target. It did not look at other TAOM branches, which is outside its lens.
- **Lens 1 (Standards, second wave):** it found F2 with `git log --all -S"Patch169"`. The first wave did not run it.
- **The builder (this session):** the Explore pass searched TAOM for other patches on `MapEventSide` and `KillCharacterAction`, and found none. It did not search Coop. Coop had fixed the same crash on 2026-09-01 (Coop commit `c4712ceba`, whose comment names the roster cache refresh), so a Coop grep would also have shown the problem was known upstream.

## Preventive actions

1. `/investigate` Phase 1 step 3 gains one line: search open PRs and all branches for the throwing method before any fix (`gh pr list --state open --search <Method>`, `git log --all --oneline -S <Method>`).
2. `harmony-patches.md` gains one line: before any patch on an engine method, grep the Coop source for the method name; a transpiler keeps every instruction another mod's matcher finds by name (insert next to it, never replace it).
3. `harmony-il.md` and `build-tooling-workflow.md` gain the lessons entries for F1 and F2.
4. FOLLOW-UP (no issue: Mike to decide whether a gate is worth the cost): a test that lists the Harmony targets in the deployed Coop `GameInterface.dll` and fails when a TAOM patch shares one with no binding test that names Coop. It needs the live install, like the other `RequiresGameIL` tests.

## Feedback memories to codify

None. The rule belongs in the repo (item 1 above), not in a private memory.
