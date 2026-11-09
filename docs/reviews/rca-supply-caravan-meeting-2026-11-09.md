# RCA: Supply Lines caravan meeting and failed-join rollback (deep-review, 2026-11-09)

## Top-line

`/deep-review` tier 1 ran Lens 5 (data flow) and Lens 2 (engine compatibility) on the uncommitted #1104 change and the #1099 rollback gate, base `d1572fcc1`. Neither lens found a CRITICAL or HIGH defect, so no second wave ran. Seven LOW findings were confirmed against the source and fixed in the same change. Two rollback guards were added on the maintainer's word. A re-review found two HIGH defects in the first draft of those guards, and the draft was corrected before any commit. Items that stay open have their reasons below. A Codex `ultra` pass on the pushed commit `218f4ee31` ran afterward on the maintainer's word. It confirmed five LOW items, recorded below.

The finding that matters most changes a conclusion, not code. The Patch73 defect is real, but a single player with Supply Lines on, outside an army or leading fewer than five attached parties, reaches it only on a large frame step. It is therefore less likely to be the delivery crash a player reported, and that crash has no proven cause.

## Findings and root cause

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The issue text and the session report ranked the Patch73 defect first for the delivery crash, on the claim that a player is likely to click the caravan as it arrives. Delivery fires at 1.2 map units and runs before the encounter check each frame (`SupplyOrderEngine.cs:21`, `Campaign.Tick` lines 980 and 994). The meeting radius is `Max(0.5, dt * 10)` (`PartyBase.GetEncounterTargetPoint`), and `dt` is `0.25 * real seconds * multiplier` (`Campaign.TickMapTime`). | Reachability of a crash hypothesis | The ranking came from what a player might do. Nobody compared the two distances, and the earlier review that rated the defect HIGH read only the encounter chain. | Lesson in `lessons/campaign-mechanics.md`: a hypothesis names the check that fires first on its route. The manifest `needs` now gives a route that reaches the prefix. |
| 2 | LOW | `docs/features/coop-interop.md` and the `ReviewedSafe` reason in `CoopVetoClassificationTests` still described the removed teardown. | Stale text after a behaviour change | The doc pass covered the registry and the feature doc. The rename rule in `csharp-patterns.md` greps identifiers, and this behaviour change kept the identifier. | Grep the patch class name across docs and tests when its behaviour changes. |
| 3 | LOW | Nothing pinned that the in-event gate belongs to the failed-join call only. Moving it into `ReassertServiceMenu` passed every test. | Test gap | The new test covered the gate's presence, not its place. | New test `BattleEnded_PlayerStillInTheEndingEvent_StillReopensTheServiceWaitMenu`. |
| 4 | LOW | The rollback test contradicted itself: the adapter said the player was in an event while the ownership snapshot said otherwise, so `Finish(true)` ran in a state where R1 defers. | Inconsistent fake | The test added one stub to the shared default shape. | The test now stubs `playerInMapEvent: true` and asserts that `Finish` does not run. |
| 5 | LOW | The patch's pass and fail outcome lines had no test, although `ingame-verdicts.md` asks for one test per branch. | Test gap | The lines sat in the prefix, behind engine statics. | The branches moved into `RunsVanilla`, which takes an `IOutcomeLog`. Two tests pin the lines. |
| 6 | LOW | The patch comment and the registry said each `DoMeeting` guard acts only on its own component type. Patch75 keys on refuge ids and Patch143 on an empty party. The conclusion holds for a different reason: Harmony skips later bool prefixes after a false return. Pre-existing sentence. | Wrong reason in a doc | The sentence was carried over unread when the section changed. | Both texts now give the Harmony reason. |
| 7 | LOW | The `SupplyOrderTownMenu` summary did not mention the meeting menu it now registers. | Stale comment | The registration moved there late, to stay under the line-count baseline. | Summary updated. |
| 8 | HIGH, caught before commit | The first draft of the rollback guard skipped the army undo whenever the party read as in-event. A landed merge always reads that way, because `MobileParty.SetAttachedToInternal` copies the commander's event side onto the party, so the undo never ran for the case it exists for. | Who set the state | The guard asked whether the party was in an event, not who put it there. Tests stubbed the flag with the merge left at its default. | The guard now excludes a merge made by this method. Two tests model the merge. Lesson in `lessons/campaign-mechanics.md`. |
| 9 | HIGH, caught before commit | The same draft set `EnlistedAttached` while the party stayed in the battle. That lifts the presence hold keyed on `EnlistedBattle` (`ServiceAttachmentService`), so a later edge could restore presence and tear the party out. | State against sibling paths | The draft kept the existing transition. The join-success branch and `OnPlayerBattleStarted` both keep `EnlistedBattle`. | The stay branch returns before the transition. A test asserts `EnlistedBattle`. |
| 10 | LOW, Codex F1 | The reachability text gave the radius for a party outside an army as the whole rule. `PartyBase.cs:663-666` has an army branch, so a player who leads five or more attached parties has a radius above 1.2 and reaches the prefix at normal speed. | Incomplete read of an engine method | The lenses and this RCA stopped at the first formula that explained the threshold. | Every text now covers both branches. New lesson in `lessons/campaign-mechanics.md`. |
| 11 | LOW, Codex F2 | The source pin `Patch_DoesNotEndTheEncounterFromInsideInit` passed on `PlayerEncounter.Finish ()` and failed on a comment that named the call. | Weak gate | The test was a plain text search. | The test blanks comments and allows whitespace inside the call. It still cannot see a call through a helper. |
| 12 | LOW, Codex O3 | `TryJoin_RollbackAfterTheMergeAndStillInTheEvent_LeavesTheBattleMenu` stubbed in-event on the adapter and left the ownership snapshot out of the event. This is the same fake error as finding 4. | Inconsistent fake | The test was added after finding 4 was fixed and copied the shared default. | Both reads now say in-event, and the test asserts that `Finish` does not run. |
| 13 | LOW, Codex O4 | The manifest did not read the prefix's catch, so an attempt that only threw judged NOT REACHED, and an earlier Pass could stand. | Manifest gap | The manifest listed the two outcome lines and not the third exit. | `1104.toml` now fails on the `[Swallowed]` line of that site. |
| 14 | LOW, Codex O5 | The `ReviewedSafe` reason said a guest gets the same line. A guest gets the guest wording. | Stale text | The reason predated the guest line in #1091. | Reason corrected. |

**Open:**

- Resolved on Mike's word (2026-11-09): a failed join whose army merge did not land, by a party already in an event, no longer leaves the army or parks. It stays in that battle in `EnlistedBattle`. Lens 5 F5.
- Resolved on Mike's word (2026-11-09): in that same case, when the service wait menu is current, the failed-join path calls `ExitToLast`, as the join-success branch does. Lens 5 F6 and Lens 2 F2.
- Open, unverified: with another menu up and time running, that case keeps the menu, and the engine opens no `encounter` until it closes. The join-success branch has the same gate. Re-review F3.
- Open, unverified: `mergedIntoArmy` is false after a throw part way through `JoinCommanderArmy`, so a partial merge takes the stay branch. Nobody found a producer of that throw. Codex O1. No guard added: a speculative case needs the maintainer's word (`simplicity-criterion.md`).
- Open, unverified: if the undo leaves the party in the event with the service menu or another menu up, that menu stays and blocks the engine's `AtMenu: false` recovery. Nobody found a producer of that state. Codex O2.
- FOLLOW-UP (no issue: pre-existing and bounded by the delivery hold): Ctrl+Space turbo held as this menu opens restores a running time mode behind it (`TimeAccelerationService.RestoreTurboIfActive`). Any menu that is not a wait menu has this shape. Lens 2 F4.
- The menu's background is unverified, because no `GameMenuInitializationHandler` is keyed to it. The in-game check in `tools/ingame_checks/1104.toml` covers it. Lens 2 F3.

**Information:** `OnPartyEncounter` now fires for a caravan, because the old throw at `EncounterManager.cs:340` left before it. `FarWatchReconcile` ends an active scout ride on that event, as it does for any encounter (Lens 2, not read further).

## Why each lens missed what it missed

- **Lens 5** found findings 1 to 5 and 7. It read the text sentence about guards and did not test its reason, because that sentence is an engine claim and belongs to Lens 2.
- **Lens 2** found finding 6. Findings 2 to 5 and 7 are outside its engine scope. It reported the radius formula only as a cost of the menu, and Lens 5 stated it as a threshold of 0.5 or one frame step.
- **Orchestrator:** after Lens 5 reported, I first wrote that a meeting needs 0.5. The source shows `Max(0.5, dt * 10)`, so I corrected every text that carried the shorter claim before this RCA.

## Re-review of the two added guards

Mike asked for the guards "if it won't hurt". Lens 5 re-reviewed the draft and showed findings 8 and 9, so the draft was narrowed before any commit. The re-review's merged-in-settlement route has no in-game evidence.

## Codex pass on `218f4ee31`

Codex `gpt-6-astra` at `ultra` effort, read-only sandbox, on the maintainer's word. It reported 0 CRITICAL, 0 HIGH, 0 MEDIUM, 2 LOW defects and 5 LOW observations, and it ran no build and no game. Claude checked each against the source: findings 10 to 14 are confirmed and fixed, and O1 and O2 stay open above. Codex disputed all of the prompt's defect suspects on the ordinary paths, including a later engine step that closes the menu, a Leave trap, a save and load race, and a permanent `EnlistedBattle` latch. It left in-game behaviour, guest network timing and exceptional rollback states UNVERIFIED. It also noted that a raid is an exception to the claim that the engine does not tick the player's own map event (`MapEventManager.cs:59-61`). The reported delivery crash still has no proven cause.

## Feedback memories to codify

None. The one durable rule is in the lessons file.
