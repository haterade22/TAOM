# RCA: a sally-out check opens a meeting with an empty party (#1099, 2026-11-08)

## Top-line

A player's two crash bundles (9d1db520, 66372c43) showed a `NullReferenceException` in
`CreateConversationTableau`, then `MapConversationTableau.OnTick` throwing on every frame. The first fix was
a skip prefix on `SallyOutsCampaignBehavior.CheckSallyOut`. `/deep-review` at tier 1 (Codex at `ultra`, Lens 5,
Lens 2) showed that fix was incomplete in two ways, and that the real cause was in TAOM:
`EnlistmentMenuBehavior.OnConversationEnded` replaced the `encounter` menu during the player's live battle, so
nothing closed the battle until the sally check's `Finish` did.

Two HIGH findings triggered a second wave (Lenses 1, 3, 4, 6) and a re-review by Lenses 5 and 2. The menu hook
now returns while the player is in a map event. Patch142 stays as a narrow backstop. Nothing here ran in game.

## Findings

Every row was re-read against the v1.5.6 decompile, the crash logs or the working tree before it entered this table.

| # | Sev | Finding | Found by | Why missed | Fix and proof |
|---|---|---|---|---|---|
| 1 | HIGH | The skip removed the only `Finish` closing the player's decided battle, so the crash became a stalled battle. `OnConversationEnded` had replaced `encounter`; `MapEventManager.Tick` skips the player's non-raid event. | Lens 5 | The investigation stopped at the first TAOM-free frame and asked where the bad encounter starts, not what state let it start. The stack held no TAOM frame, and the log line that showed the wait menu was read as context. | The hook returns on `IsInMapEvent`. `EnlistmentConversationEndTests`, RED then GREEN. |
| 2 | HIGH | A leader marked `DiedInBattle` passed the guard. He still leads the party when the prefix reads it, and `Finish` then kills him. | Lens 5, Lens 2 | The comment "the two cannot disagree" treated one function as one moment. The same lookup ran before `Finish`. | `AnyoneRemains` does not count a marked hero. `Evaluate_LeaderOnlyHasHeroMarkedDiedInBattle_Skips`, proven by mutation. |
| 3 | MED | An empty garrison stopped valid AI-only sallies, on a path that opens no meeting. | Codex, Lens 5 | The rule tested emptiness before it tested whether a meeting could open. The plan recorded this as a trade-off and did not ask what it bought. | The skip needs the leader inside the player's battle. `Evaluate_EmptyLeaderInAnotherBattle_RunsVanilla`, `Evaluate_EmptyLeaderInNoBattle_RunsVanilla`. |
| 4 | MED | The warning and `[Check]` line ran before vanilla's strength test, so they could describe a sally vanilla would not start, and the warning repeated. | Codex, Lens 5 | The report was written to the verdict's meaning, not to what the code knew at that line. | New wording, one state warns once, and the manifest says what the line proves. `Apply_RepeatedSkipOfOneParty_WarnsOnce`. |
| 5 | MED | No test ran the prefix: `return true` after the report, or swapped parties, passed every test. | Codex, Lens 5, Lens 4 | Only the pure `Decide` was tested, and the crash "needs a live campaign" was accepted without looking for a staging precedent. | `Evaluate_*` run on identity-only engine objects, shape from `VillageRaidReaderTests`. Mutations of `DeathMark` and the return both fail. |
| 6 | MED | No test proved the patch emits its `[Check]` line. A manifest with `absence = "not-reached"` hides a lost emitter. | Lens 4 | The first render test called `log.Pass` itself. | `Apply_Skip_WritesTheCheckLineTheManifestReads` goes through `Outcomes`. |
| 7 | MED | The first fix pass added an attached-to-leader branch that read `AttachedTo` before `Finish`, and a test pinned the wrong answer. | Lens 5 and Lens 2 re-review | The fix pass repeated finding 2's slip on a new field: it read pre-`Finish` state to predict post-`Finish` state. | Branch and garrison read deleted. No producer of a garrison partner was found. |
| 8 | MED | The rule sits in the patch class, not a service (ADR-002). | Lens 1, Codex re-review | Followed the Patch84 shape the plan named. | **Applied on Mike's decision.** Lens 6 rejected the move as a service and an adapter pair for about 10 pure, tested lines with one caller. Codex raised it again, and Mike chose full compliance. `SallyOutGuardService` holds the rule, `SallySiegeAdapter` does the engine reads, and the patch acts on the answer. The method and test names in findings 2, 3, 5 and 12 predate this move. |
| 9 | LOW | Comments and docs claimed the wrong mechanism: the player "involved" opens the meeting, the hourly tick only, "other paths are AI-only", a rename "would pass null". | Lens 2, Lens 4 | Claims were written from the first reading of the path. | Corrected in the patch, tests, registry and feature doc. |
| 10 | LOW | `map-event-guard.md` was stale beyond the new row, and `enlistment.md` still described the hook without this exit. | Lens 4 | The doc update touched only the row added. | Both updated. |
| 11 | LOW | Two `Decide` tests could not detect the clause they named. | Lens 4 | Inputs were chosen to read well, not by the two-edit test. | Clause and tests removed with finding 7. |
| 12 | LOW | Roster walk on checks that could never skip; a one-caller report helper with a `try` guarding nothing. | Lens 6 | First-draft shape. | Early return in `Evaluate`; helper and `try` deleted. |
| 13 | MED | No test ran the prefix's own veto: a `Prefix` that always returned true passed every test. | Codex re-review | Finding 5's staging stopped one level below the entry point. | `Prefix_LeaderOnlyHasHeroMarkedDiedInBattle_StopsVanilla` stages a campaign, proven by mutation. |

**Rejected:** a fuller roster fixture (Codex re-review). The fixture fills only `_troopRosterElements`, but the tests go through the real `GetTroopRoster`, in the shape of `VillageRaidReaderTests`. Deleting the warning throttle (Lens 6). The lens argued that `Outcomes.Pass` already writes at that
rate, and `OutcomeLog` writes one line per settlement key, so the warning is the line that repeats. Moving the
`IsInMapEvent` test into `IServiceMaintenanceService` (Lens 1): the handler's other gates are inline, and Lens 6
rejected it.

## Root-cause pattern

Findings 1, 2 and 7 are one slip: a guard placed at an engine call must account for what that call does after the
guard returns. The call on this path runs `Finish`, which closes the battle and changes the parties. The guard read
the state before that, and skipped the closing with it.

**Repeat offender.** `lessons/harmony-il.md` already holds "A skip prefix on a tick also skips what the tick clears"
and "Find one real producer before you build a guard for a path". The first describes finding 1. The second describes
finding 7, and the garrison test before it. The plan named the lesson file and read the headings; it did not apply them.

## Why each reviewer missed what it missed

- **Codex** found findings 3, 4 and 5 and confirmed the teardown reasoning. It did not read the enlistment hook: the prompt scoped the review to the patch files, so TAOM's own part in the state was outside its brief. Its "suspect 3" (other null sources) pointed the right way.
- **Lens 5** found findings 1 and 2 in the first wave because it followed the battle's state past the skip. In its re-review it found finding 7.
- **Lens 2** found finding 2 and proposed skipping every decided player battle. Finding 1 refutes that proposal, and the lens withdrew it in its re-review.
- **Lens 1, Lens 3, Lens 4 and Lens 6** ran on the fixed code. Lens 3 found no issue.

## Open items

- Resolved: why the state read `EnlistedAttached`. The load sweep at 22:41:46 ran with an open encounter and no map event, so the #577 repair had nothing to act on. The battle began after that, the commander was not in it, and no edge set the battle state. `ServiceBattleService.OnPlayerBattleStarted` now does. The exact caller that started the battle is inferred: `game_menu_encounter_meeting_on_init` calls `StartBattle` when no battle exists.
- Resolved: `ServiceMaintenanceService.EnsureServiceMenu` now returns during a map event, with `Pump_AttachedInAMapEvent_LeavesTheBattleMenu`.
- Resolved: a Patch142 stop reopens `encounter` when the service wait menu covers it.
- Resolved: `Patch143_EmptyMeetingGuard` replaces any meeting with no speaker with a one-option Leave menu. It does not call `Finish`, because `RestartPlayerEncounter` reads `PlayerEncounter.Current` after the meeting returns.

## Second review: the four follow-ups

Lens 5, Lens 2 and Lens 7 reviewed the uncommitted follow-ups at tier 1. No finding in the change was above LOW. Lens 2 reported one HIGH in existing Supply Lines code, recorded below. No second wave ran, because that file is outside this change. Codex did not run: both approved runs were spent on the first fix.

| # | Sev | Finding | Found by | Why missed | Fix and proof |
|---|---|---|---|---|---|
| 14 | LOW | Three texts said Patch143 covers the player's sally button. While the besieger leader is at the settlement, the button's encounter goes to `encounter` and does not reach `DoMeeting`. Whether `FinalizeEvent` can remove the leader first is unverified. | Lens 5, Lens 2 | The claim came from the guard's position in the meeting code. The route through `GetEncounterGameMenu` was not traced. | Patch comment, registry and open item corrected. |
| 15 | LOW | `EnsureMenuOpen` was the wrong call for the reopen. `SwitchToMenu` runs the init at once, and on a decided battle init can leave `encounter`. The adapter then activates it a second time. | Lens 2 | The call was copied from a sibling whose target menu has a harmless init. | `SwitchTo`. `ShouldStopSally_StoppedOnTheServiceWaitMenu_ReopensTheBattleMenu`, red then green. |
| 16 | LOW | The manifest's `fail` list missed the "could not open its menu" line, and the header named the wrong emitter. | Lens 7, Lens 5 | The manifest was written for the catch path only. | One combined pattern. `ShouldDivertMeeting_MenuWouldNotOpen_ReturnsFalseAndReportsNoPass` matches the line against the manifest, red then green. |
| 17 | LOW | `taom_empty_meeting_leave` duplicated vanilla's translated `{=3sRdGQou}Leave`. | Lens 7 | `/localize` starts from a new key, and no step asks whether vanilla has the word. | Vanilla key used, 13 rows deleted. |
| 18 | LOW | The registry said a null check covers a Supply Lines teardown. A prefix that returned false skips later prefixes, so the check never ran for that case. | Lens 2 | Harmony's skip was not read. | Registry corrected, the redundant check deleted. |
| 19 | LOW | The adapter summary said it reads what vanilla is about to get. `DoMeetingInternal` has two army branches that read no partner. | Lens 5, Lens 2 | Same slip as finding 14. | Summary corrected. |
| 20 | LOW | Two comments said only the battle menu closes the player's event. `MapEventManager.cs:59` still ticks a raid, and the vanilla sally check also closes that event. | Lens 2 | The rule was written from the non-raid crash. | Comments now state the tick rule. No behaviour changes. A first correction kept the word "only", and the Lens 2 re-check rejected it. |
| 21 | LOW | `map-event-guard.md` said "the other two" with four rows in the table. | Lens 2 | The row was added without a read of the sentence below it. | Sentence rewritten. |
| 22 | LOW | The `EnlistedBattle` edge had no in-game check. | Lens 5 | The manifest was extended only for the new patch. | `taom.enlistment.own-battle-state` added. |

**Why the three lenses split this way.** Lens 7 read the data and manifest, so it found findings 16 and 17. Lenses 5 and 2 read the engine routes, so they found the rest. Lens 3, Lens 4, Lens 6 and Lens 1 did not run (tier 1 first wave).

**Root-cause pattern.** Findings 14, 18, 19 and 20 repeat finding 9: a claim about a route, written from where the code sits. The lesson is in `lessons/campaign-mechanics.md`.

**Not changed.**

- A battle without the commander now pays kill XP, merit band and spoils, because the merit sampler keys on `EnlistedBattle`. A reload into the same battle already did this.
- If the player's own battle ends while the commander is fighting, the state holds at `EnlistedBattle` until the commander's battle ends. The window needs time to pass on a menu that stops it.
- `IsMainPartyIn` stays in place of `mapEvent.IsPlayerMapEvent`: the same file uses that helper for the sibling check.
- FOLLOW-UP (no issue: filing is public and needs Mike's word): HIGH, existing code. `SupplyCaravanEncounterPatch.cs:58` calls `PlayerEncounter.Finish()` from the `DoMeeting` prefix, inside `Init`. `EncounterManager.cs:340` then reads `PlayerEncounter.Current`, which `Finish` set to null. What catches that throw is unverified. Refuge's shape, a menu that leaves the encounter open, is the model.
- FOLLOW-UP (no issue: no producer found, and a guard for a speculative case needs Mike's word): the failed-join rollback at `ServiceBattleService.cs:226` has no in-event gate, so it could put the wait menu over a live event.

## Not verified

No in-game run: no save and no deterministic repro exist. Which party was empty is inferred from the capture at
22:42:40 and the battle closing at 22:42:42.916. Codex re-reviewed the fix at `high` on HEAD 78f918903 and reported no
CRITICAL, no HIGH and two MEDIUM (findings 8 and 13). The service extraction for finding 8 came after that run, and no
reviewer read it: its evidence is the tests, four mutations and `/verify`.
