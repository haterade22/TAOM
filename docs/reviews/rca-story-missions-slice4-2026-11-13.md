# RCA: the story offer, Story Missions slice 4 (#1105, 2026-11-13)

## Top-line

A player reported that the War Chronicle's "Battle of the Hornburg" always ends with "No assault comes to Helm's Deep". That
ending is by design: no AI army besieged `town_V2` inside the 20 day window, and the chronicle spawns no battle. Slice 4 lets
the window offer the slice 1 Helm's Deep defence: a win resolves Held, a loss gives Helm's Deep to Isengard (Fell), and a
window with no assault and no mission resolves Held by fate.

`/deep-review` ran at tier 1 on the uncommitted worktree `E:/repos/taom-story-missions` (base `cb482a5d4`): Codex at `ultra`,
then Lens 2 (engine), Lens 5 (data flow) and Lens 7 (XML and ModuleData). No finding was HIGH or CRITICAL, so no second wave
ran. Eight MEDIUM and eleven LOW findings were confirmed against the code or the v1.5.7 decompile and fixed with tests, except
the two rows marked NOT APPLIED and one `FOLLOW-UP`. Two findings repeat earlier lessons (rows 4 and 9).

Codex read some files while the pre-review fixes S1 and S2 landed; its report says its final reads include them.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| S1 | MED | `GetStoryOffers` read the store directly, so between War Ended and the next guarded step a story window still listed, and a Lost verdict transferred the fortress after the war | state lifecycle | the war-end guard lives in `Guarded`, and a read path that skips the wrapper skips the guard | found while briefing the review; tests `GetStoryOffers_TheWarHasEndedBeforeTheDailyStep_ListsNothing`, `ConfirmStoryWindow_TheWarHasEnded_IsFalse` |
| S2 | LOW | the offer could open its menu while the main party is in a map event (`MapPopupGate` tests the encounter, not the event) | campaign menus | the #1099 lesson was not applied to a new menu opener | `NextOffer_InAMapEvent_ReturnsNull` |
| 1 | MED | a save on the result menu dropped the verdict: the behavior saves nothing, and the verdict waited for the "Continue" click (Codex; Lens 2 F3 and Lens 5 trace 5 rated it LOW) | save lifecycle | the menu was treated as a moment; the engine saves and restores the open menu id (`MapStateData`, `SandBoxGameManager`) | the init the engine runs on the mission's return applies the verdict once; pin `TheStoryOffer_SnoozesOnEscape_DeclinesOnlyByChoice_AndAppliesTheVerdictBeforeASave`; lesson |
| 2 | MED | a Lost transfer could overwrite an owner change that raised no event (a clan that leaves its kingdom keeps its fiefs) (Codex; Lens 5 trace 8) | campaign ownership | the Won path ran `CheckOwner`, the Lost path checked only the offer list | `ConfirmStoryWindow` runs `Guarded` and `CheckOwner` before the transfer; tests `ConfirmStoryWindow_AnOwnerChangeWithNoEvent_ResolvesTheWindowAndIsFalse`, `ResolveStoryWon_AnAttackerTookTheTargetWithNoEvent_ResolvesFellInstead`; lesson |
| 3 | MED | the offer listed a window that an attacker was besieging or had besieged, or whose target the defender no longer held, so a win mid-siege paid Held while the AI siege went on (Lens 5 trace 2) | design gap | decision 2 ("the map decides first") was implemented only as "a resolved window withdraws" | `GetStoryOffers` requires not assaulted, no attacker siege, and the defender as live owner; three `GetStoryOffers_*` tests; lesson |
| 4 | MED | Escape on the popup ran its negative action, the saved "Let fate decide", and the menu's leave hotkey ran the same decline (Lens 5 trace 1; Lens 2 F2) | UI input | REPEAT of `lessons/localization-ui.md` "Option B of a two-option inquiry is the Escape key" (2026-10-23): the lesson was not in any trap sheet, and no path rule fires in a sibling worktree | popup B is "Not now" (a snooze); the menu decline is not a leave option; a trap sheet line in `campaign.md`; pin as row 1 |
| 5 | MED | a player whose kingdom is one of the attackers was offered the defence, and a loss or retreat gave the fortress to the player's own ruler (Lens 5 trace 3) | design gap | the gates listed the player's states, not the player's side | `NextOffer` skips an offer whose attackers include the player's map faction; two tests. A recommendation under the standing word; undo: drop the term |
| 6 | MED | the popup's "Ride" button opened the menu without a new check: the rest of the campaign tick that raised the popup (`Campaign.cs:980` the event, `:988` periodic events, `:994` `EncounterManager.Tick`) can open an encounter or a conversation under it (Lens 2 F1) | campaign menus | the free-map check ran at the tick, and the click comes later; near-repeat of "A modal opened from a game menu ... must revalidate" | `MapPopupGate.IsMapFreeUnderPopup` on the click (main party, no encounter, no map event, map state not at a menu or in a conversation); pin |
| 7 | MED | `ChronicleShippedContentTests` checked every chronicle text against its row but not `fateText`, so the English could drift from 12 translations with every gate green (Lens 7) | test gap | the test lists JSON fields by name, and the new field was not added | `$..fateText` added, count 48 |
| 8 | LOW | the offer popup could show before the window's opening announcement that explains it (`MbEvent` listeners run newest first) (Codex; Lens 5 trace 4) | ordering | the two features shared a free-map gate but no order | `GetStoryOffers` waits while the announcement queue holds a line; `GetStoryOffers_WhileAnAnnouncementWaits_ListsNothing` |
| 9 | LOW | `OutcomeLog` latches one line per key for the whole process, and the keys were the event id only, so a reload that played the other result wrote no line, against the in-game plan (Codex; Lens 5 trace 12) | evidence | repeat in kind of `lessons/build-tooling-workflow.md` "A once-only reason line has one latch per reason" | keys name `UniqueGameId`, the window and the verdict; `Apply_AWinAndALossInOneProcess_WriteTwoResultKeys`; lesson |
| 10 | LOW | the doc's owner-change audit omitted vanilla effects (captures, siege camps, fief votes, prisoner release, governor, patrol, clan homes) and the adapter comment claimed it covered every listener (Codex 3; Lens 2 F6) | doc | the vanilla listener list was read for crashes, not for effects | the section lists them, with the v1.5.7 sources |
| 11 | LOW | a failed or unchanged result showed "The walls ... are lost." (Lens 5 trace 5) | UI text | the text followed the verdict, not the applied result | `StoryOfferService.MenuText`; `MenuText_FollowsTheAppliedResult` |
| 12 | LOW | `fate` was read by exact case, `requiresOutcome` by `EnumNames.TryParse` (Lens 5 trace 9) | config | two readers of one enum in one file | same parser; `Parse_AStoryFateInAnotherCaseWithPadding_IsRead` |
| 13 | LOW | the leave-settlement guard in `StoryCampaignAdapter` was unreachable, and worse than vanilla if reached (Lens 2 F4) | simplicity | written "in case"; the offer needs a free map and time stays stopped | deleted (vanilla's `PartyDiplomaticHandlerCampaignBehavior` handles a party inside) |
| 14 | LOW | the militia stayed at full strength for the new owner; vanilla's rebellion transfer sets it to 0 (Lens 2 F5) | parity | only the garrison was read as "the defenders" | `settlement.Militia = 0f` |
| 15 | LOW | `StoryMissionLauncher`'s comment named the dev menu as its only caller (Lens 2 F7) | doc | a new caller, an old comment | comment names both callers |
| 16 | LOW | the docs did not say that fate Held pays the Held effects and momentum and lets `ents` follow, or that a retreat after the run starts is Lost (Lens 7; Lens 5 traces 6, 13) | doc | the decision text was copied, its consequences were not traced | `war-chronicle.md` and `story-missions.md` say both; the commit body names the +100 Free momentum |
| 17 | LOW | test gaps: an attacking mission, a later eligible offer, Lost with no target, a malformed `sd` (Codex 15) | test gap | the tests followed the happy paths | `NextOffer_AnAttackingMission_IsNotOffered`, `NextOffer_ASnoozedFirstWindow_ReturnsTheNextOne`, `Apply_LostWithNoTarget_FailsAndGivesNothingAway`, `Parse_AStoryFlagThatIsNotTrue_IsNotDeclined` |

NOT APPLIED:
- Lens 5 trace 8 and Codex 6: the taker is not filtered by war with the old owner, and a prisoner or child ruler passes. `ApplyByDefault` neither needs nor declares war, no crash path was found, and no approved requirement asks for it.
- Codex 15: no unit test runs `StoryCampaignAdapter` (it is engine-bound, ADR-007); the in-game rows in `tools/ingame_checks/1105.toml` cover it.

FOLLOW-UP (no issue: the effect is UNVERIFIED and is pre-existing; check it during the slice 4 in-game proof): `EnsureMenuOpen` on a free map calls `SwitchToMenu` with no menu context, which reaches `Debug.FailedAssert` (v1.5.7 `GameMenu.cs:368-383`, Lens 2 F8). The dev menu reaches it too. What the assert does in a shipping build is unknown.

## Root-cause pattern

Rows S1, 2, 3 and 6 share one cause: the change added a second path to a map outcome (the story result) beside the
chronicle's own paths, and checked only the conditions its author thought of. The chronicle already held every check the new
path needed: `Guarded` for War Ended, `CheckOwner` for a silent owner change, `Assaulted` and `AttackerBesieges` for the map's
own fight. The fix routes the story path through those checks instead of copying a subset.

Rows 1, 4 and 6 share a second cause: the offer UI was designed as a sequence of clicks, but the engine can save between two of
them (row 1), answer a popup for the player (row 4) and change the map under it (row 6).

## Why each agent missed these

- **Codex** found rows 1, 2, 8, 9 and 10, and missed rows 3 to 7: it disputed the menu return and the save codec with evidence,
  but did not read the input bindings or the tick order, and its prompt did not ask about the siege state or the player's side.
- **Lens 2** missed rows 2, 3 and 5: they are flows across TAOM code, outside an engine-compatibility lens.
- **Lens 5** missed row 6 and rated row 1 LOW: it traced data, not the frame order of the tick.
- **Lens 7** ran only on the data files; rows 1 to 6 are not in its scope.
- **The author** wrote S1 and S2 into the first draft and caught them only while writing the review brief.

## Feedback memories to codify

None. The two rules that recur go to the lessons files and one trap sheet line (`.claude/rules/traps/campaign.md`).
