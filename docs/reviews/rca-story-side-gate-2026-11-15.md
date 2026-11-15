# RCA: the story offer follows the player's side (#1326)

Findings from the tier 1 review of #1326, Phase 1 of #1325: the Helm's Deep story offer now shows a side only when the player may take it. Three lenses ran on the uncommitted worktree (5 Data flow, 2 Engine compatibility, 7 XML). Engine compatibility found nothing (29 API uses verified against the installed v1.5.7 DLLs). The other two found one MEDIUM and seven LOW items, and the first full test run found one more before the lenses started. The Codex pre-review did not run: the account is at its usage limit until 2026-11-20, so that pass is owed before the merge to trunk.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | `StoryOfferService` sided a window's kingdoms with the two-argument `ResolveSide(kingdom id, mission row culture)`. `SidePairScanTests.TwoArgumentResolveSide_IsCalledOnlyByTheListedTypes` (#957) failed in the first full run | A plan that prescribes a gated call | The approved plan named that call. Its design pass checked the entry-point and service-signature gates, not the gates on `IAlignmentService`, and the filtered run while building named the feature's tests only | The service calls `GetKingdomSide`, with no culture fallback, and `ShippedStoryOffersTests.EveryStoryWindowKingdom_IsListedInTheAlignmentTable` requires every kingdom of a story window in the table. Lesson 1 |
| 2 | MED | Lens 7 F1, lens 5 T9: the seven new strings and the attack branches of the menu, the text and `Apply` cannot be reached until an attack mission ships | Staged feature | The approved plan puts the "Storm the walls" option and the attack verdicts in this phase, before the battle exists | Kept, as the plan decided. The commit body and the #1129 backlog note say the seven keys need no translation until the attack ships. Finding 3 pins the staged state |
| 3 | LOW | Lens 7 F4, lens 5 T4: a shipped `attackMission` that names an Attack row passed every test, while `StoryMissionLauncher.Launch` refuses every attacking mission. Its players would get a side that never opens, every day | Offered is not launchable | The shipped test was written for the finished feature, not for the stage the code is in | `ShippedStoryOffersTests.EveryStoryBlock_ItsMissionIsADefence_AndNoneNamesAnAttackYet` asserts no story block names an `attackMission`. The attack phase replaces the assertion with the row's check |
| 4 | LOW | Lens 7 F2: the `offer.shown` pass pattern in `1105.toml` accepted every choice and side, so the check could not catch an evil player who was asked to defend | A check that cannot fail | The pattern was written from every line the code can write, not from the lines correct code writes with shipped data | The pattern is `choices=Defend side=(Free\|Neutral)`, and a shown line with `side=Evil` is a fail pattern. Lesson 2 |
| 5 | LOW | Lens 7 F3, lens 5 T7: the human row and "How to verify in game" sent a Khand player to expect the popup. A pledged Khand reads Evil (`GetKingdomSide` applies the War Pledge) and correctly gets none | A precondition the text left out | The rows were written from the alignment table alone | Both texts say "a neutral player whose realm has not pledged" and name `taom.print_war_pledges` |
| 6 | LOW | Lens 5 T12: no service test needed the kingdom tie. The three tie tests used Isengard, Dunland and Rohan, whose alignment alone gives the same answer, so `TieOf` could return `None` with the suite green | A rule with no case that needs it | The tests were written from the Helm's Deep story, where the two rules agree | `NextOffer_ANeutralRealmAmongTheAttackers_ItsPlayerMayOnlyAttack` and `NextOffer_ANeutralRealmDefends_ItsPlayerMayOnlyDefend`. With the tie forced to `None`, exactly these two fail. The three older tests are renamed for what they test. Lesson 3 |
| 7 | LOW | Lens 5 T3, T5: the attack mission id passed into the offer by `GetStoryOffers`, and the mission id `Launch` picks for the chosen side, had no test and no pin. A swapped pair would open the defence for "Storm" and apply its held walls as a won attack | Untested pass-through | Both lines are one expression each, and the attack cannot run yet | `GetStoryOffers_AStoryWithAnAttackMission_CarriesItToTheOffer`, and a pin of the `Launch` line in `StoryMissionWiringPinTests` |
| 8 | LOW | Lens 5 T11: `StorySideGate`'s summary and `story-missions.md` said no battle of the player takes a fortress from his own realm. A lost defence by a player of the defending realm does. Also: `StoryOffer.cs` had no key-files row, Dependencies lacked `IAlignmentService`, the feature map was not updated, and three comments still said "lost mission" for a path a won attack also takes | Text that claims more than the code | The sentence was written for the rule's intent, not read back against the `Apply` matrix | The texts say he never fights against his own realm; the rows and comments are corrected |

Not applied, both unreachable until the attack ships:

- `NoteShown` writes `mission=` as the defence id even for an offer with only the attack.
- The `offer.result` and `ended` patterns in `1105.toml` do not match an attacking line (`side=Attacking ...`), so one would read NOT REACHED.

FOLLOW-UP: the attack phase of #1325 widens both with its data and its launcher change.

## Root-cause pattern

Findings 2, 3, 4 and 7 share one cause: this phase ships the offer side of a feature whose battle comes later. Tests, the manifest and the shipped-data check were written for the finished feature, so they accepted states the current code cannot serve. The fix in each case was to pin the stage the code is in and let the later phase replace the pin.

Findings 5, 6 and 8 share another: text and tests written from the Helm's Deep case, where alignment and kingdom tie agree and no realm has pledged. The cases that separate the rules needed a neutral realm.

## Why each reviewer missed these

- **Finding 1** was found by the full suite, before any lens ran. No lens saw the first version.
- **Lens 2 (engine)** had no rule that applies to findings 2 to 8: none concerns an engine call.
- **Lens 5 (data flow)** found 3, 5, 6, 7 and 8, and noted 2. It did not flag the loose pass pattern (4): it tested that today's line matches the pattern, not that a wrong line fails it.
- **Lens 7 (XML)** found 2, 3, 4 and 5. Findings 6 to 8 are C# tests and prose outside its files.

## Lessons recorded

Three entries in `docs/reviews/lessons/testing-qa.md`, dated 2026-11-15 with this file as their source.
