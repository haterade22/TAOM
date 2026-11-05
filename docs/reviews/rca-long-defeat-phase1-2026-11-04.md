# RCA: The Long Defeat Phase 1, two review waves (2026-11-04)

**Top line:** the tier 1 review of Phase 1 (#1079) found one HIGH: one click on Photo Mode in the Escape menu locked the player out of the run, because the mode copied Fords' patterns but not its veto. It also found that the unattended judge could pass a stale deploy, had lost the spike's read-back guard and rested on hand-built log lines. The second wave found no HIGH: its value was a test gap on the keep's wiring and the measurement that no attacker came within 45 m of the keep. A rehearsal between the waves lost at wave 4 of 5, which showed a wave table that climbs too fast for a bot that buys the cheapest packs. Codex did not run, because this work has no paid-dispatch word. No fix here is proved by a clean-tree run yet: the runs at this commit's tree follow it.

## Findings

Every row was read in the source or a run log before it was fixed. Severities are the lenses' own.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Photo Mode soft-locked the run: the Escape menu offers it unless a view says no, and its only exit is in a view this mission leaves out | Sibling guard not copied | The plan copied Fords' HUD and panel, not the one-line `IsPhotoModeAllowed` veto in `FordsCommanderCameraView`. Repeat of spike row 12: a guard that lives in a Fords view the plan did not list | Veto and source pin. Lesson: when a mode copies a sibling, list every `public override bool` in the sibling's views |
| 2 | MED | Prefab colours in ARGB order, while the engine reads RGBA, so the gold and red text read with the wrong channels | Engine format | No test read the order, and a screenshot of a small strip does not show a tint. I did not check the engine's parser (`Color.cs:172-183`) | A prefab test fails on an alpha below 0x80 |
| 3 | MED | The judge dropped the spike's read-back lines, so attackers had no floor check | Gate proves less than it says | The rewrite of the judge kept the check list and lost the guard beside it | `READ_BACK` required for each side, with a test per side |
| 4 | MED | A stale deploy could PASS: the judge never compared `[BuildStamp]` with the tree | Gate proves less than it says | The Fords runner only prints the stamp (`run_fords_autorun.py:751-752`), and I copied its shape. Fords keeps that gap, because its runner is shared and out of this change | The judge fails on a mismatch and names the build in its PASS text |
| 5 | MED | All judge evidence was hand-built, so a manifest row could match no real line | Test tied to itself | Tests written before a real log existed and never revisited | Fixtures of real lines from a passed and a lost run |
| 6 | MED | The rewrite dropped the wrapper's dialog tests | Test lost in a rewrite | I replaced the test file and diffed behaviour, not coverage | Restored from git, updated to the X4008 rule |
| 7 | MED | The monkeypatched judge was proved only by attribute identity | Test tied to itself | The test checked that the name was set, not that the run used it | A run through `run_once` with fakes. Handing it the Fords judge makes it fail |
| 8 | MED | The keep's wiring had no test: swapping the two counts or removing the 0.5 s gate stayed green | Test gap | The domain's keep rule was tested, and the fake hard-coded both counts to 0, so no test crossed the seam | Settable keep counts in the fake and three director tests |
| 9 | MED | Plan risk 5 had an answer in the logs that nobody wrote down: no attacker came near the keep | Evidence not read | The plan said the keep line would answer it, and I read the wave rows and not that line | Measured in the feature doc; the place is Mike's call |
| 10 | MED | A sorted sample of attackers was built at 2 Hz for a line written every 5 s | Efficiency | The sample started as a run rule and stayed in the read after it became log text | Read at the moment the line is written |
| 11 | MED | Per-frame garbage on the idle path: a delegate, a closure, two lists and a class record per tick | Efficiency | Fords' host shape was copied without a frame-cost read | One delegate, no lambda on the tick, early return, a record struct |
| 12 | MED | A helper called the service locator | Standards | `ServiceLocatorGateTests` reads names ending in Service, VM, ViewModel and Engine, and this one is a Host | Constructor argument; the view stays the boundary |
| 13 | LOW | Smaller defects, each fixed: a short wave could pass the wave check; a stale keep count could end a cleared wave; the end panel never opened in an unattended run; the warning text said the opposite of the rule; a refused launch waited 600 s; dead members; no summary warning after a reverted value, and the summary ran before the keep rule's warning; a scene name reached native code unbounded; "1 waves" in two strings; a string test that scanned one folder; a binding test pinned the wrong member; a relative log path in the done file; a raw byte order mark in a char literal; the judge took one wave line for five cleared waves; Cancel pressed on a dialog title alone; contradictory comments; a false comment on load order; a wrong file name printed in a shared tool; the wrong shebang; stale lines in the feature doc | Mixed | First read by the review. Slices were each tested alone, and these sit between them | Fixed in this change |
| 14 | LOW | Copied plumbing and repetition: three atomic writes, three failure paths in the prompt, constants tied by comments, an effect field that repeated the result, unused menu callbacks, a hand-written record struct, comments off the ADR-009 keep-list | Simplicity | Copied from Fords with the shape intact, then not re-read once the feature settled | Applied as the design lens proposed |
| 15 | LOW | Test gaps: the felled path in the engine's order, the fleeing peak, the driver's fault clause, the done file's `expect`, the bot's clock reset, the respawn guard, and a manifest loop that skipped rows | Test gap | Each lived on a seam the fake did not model | One test each |
| 16 | MED | The first wave table reached level 31 at wave 5 against a bot that buys level 11, and run `20261104-035343` lost at wave 4 of 5. A slower second table lost at wave 5 of 5 (`20261104-044306`), because the purse could not replace the losses | Balance guess | I wrote the table from the troop list and had no run past wave 2 | Third table and bounties 1.5 times higher; the runs below judge it. The feature doc holds the numbers. Final balance is Mike's |
| 17 | LOW | I started the second rehearsal with a 10-minute timeout, and the earlier long run had 90 minutes | Own process slip | I retyped the command and not its options | Stopped within a minute and restarted; nothing lost |
| 18 | LOW | Two of my proof scripts read nothing and printed empty rows: a state regex anchored on a time that follows a date, and a test that read a lazy catalog before it loaded. The test failed for the wrong reason | Evidence not read | I took a red result as proof without reading its message | Read the failing line; proved the fixed test against the old order |
| 20 | MED | Losses grew wave by wave on every table, and tuning the table alone did not help (`20261104-054852`). I suspected wounds, because nothing heals a surviving defender between waves. The measurement does not support that as the main cause: mean health was 84% to 91% before each rest (`20261104-071020`) | Hypothesis, then measured | The plan sold healing in Phase 3 and never asked what a wave does to survivors before that. I tuned the wave table twice before I asked what else carries over, and my first arrow idea was also wrong | `rules.restShare` and a line that measures the wound. The cause of the swings stays open, and the feature doc says so |
| 21 | MED | A wave's end could wait 150 s on its last attackers, and Mike closed a run because 2 attackers stood far outside the walls with nothing moving | Felt only in play | The stalemate rule was judged by whether it cleared the wave, and every unattended run waits as long as it must. The per-wave waits were in the logs, and I did not read them as a player would | `stragglerCount` and `stragglerSeconds`, Mike's numbers: 5 or fewer attackers fall after 60 s, with no charge, and a death does not restart the clock. A first form with 20 s of quiet could be held open by a lone archer |
| 19 | LOW | A fix for the standards rule put `DoNotAcceptEvents` on the modal shop panel's root, so a click on the dim background would reach lower layers. The checker read `EventManager` and `ScreenManager` and caught it before any run | Rule applied past its reason | The rule was written for HUD layers, and I passed it on without its scope. Native's `EscapeMenu.xml` has no such flag | Reverted on the seven containers; the host's comment names the reason |

**The leaderboard's report fields (#1080), the same day.** Tier 1 lenses 5 and 2 read the diff and the plan, and ran the plan's rules on six real reports. They found no HIGH, and rules 4 to 12 held on all six.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 22 | MED | The plan ranked on defenders lost, and no rule read it, so zeroing that number passed every rule | Gate proves less than it says | I wrote the rank and the rules apart and never read one against the other | The plan says so, and the end panel owes a line for the clip to prove |
| 23 | MED | The plan called `balance` the balance, but it covers three JSON files, not troops, gear, the scene or AI code | Overclaim | I named the field for what I wanted it to mean | The plan names what it covers, and a release that changes the rest opens a board by hand |
| 24 | MED | The bot held only `waves.json`, though the roster and the starting gold come from the other two, and the plan gave no source for the released pair and no way to rederive the hash | Spec gap | One roster exists today, so the gap did not show | The bot holds all three and recomputes the hash; the pair comes from a report made on the packaged install |
| 25 | LOW | Smaller items, each fixed: a director test that did not prove the wiring; dead null checks; a wrong reason for rule 6; rule 12 stale and too strict for autoplay data; rule 13 trusting the player's clock; rule 14 beaten by one added space; a routed attacker refusing an honest run; `mods` without the versions that record the engine changeset | Mixed | First read by the review | Fixed in the code or the plan |

**The muster place, the same day.** Mike saw new defenders stuck on the terrace. Tier 1 lenses 5 and 2 and a partial run found no incompatibility, and soldiers on the wall stayed at their posts.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 26 | MED | New defenders spawned on a terrace and stuck there, and every check passed, because no check can see where a healthy spawn should be | Felt only in play | The plan cut the recorded place, and the `ok` lines judge height and navmesh only | Recorded points, and a failed check when a point is lost |
| 27 | MED | The first draft's fallback only warned, so the terrace could return with a green judge, and the doc said no point shipped | Gate proves less than it says | The builder wrote the doc before the points landed, and the fallback copied the keep's quiet shape | `MusterPlaceLost`, `source=` in the check, a shipped-data test, corrected doc |

**Speed controls, the same day.** Tier 1 lenses 5 and 2 found no signature break.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 28 | MED | At 10x with a person driving, engine fast forward read each key press once per internal step, so use could start and stop in one frame | Sibling guard not copied | Vanilla allows it only for a dead player, and Fords forces 1x while controlling. The first draft took Fords' speed rule but not that guard. Repeat of row 1 | A fixed step for a person at 10x, with one tick per frame; a played check owed |
| 29 | MED | Speed stayed up after the run ended, so a fight with no rules ran at 10x behind the end panel | State not cleared | The speed was tied to the mission's end, not the run's | `Session.Speed` reads 1 once the run has a result |
| 30 | LOW | A HUD fault could leave 10x on, and the judge could pass `--speed 10` on a build that played at 1x | Gate proves less than it says | First read by the review | `Stop` runs first; the judge compares `maxSpeed` with the flag |
| 31 | MED | The new last-defenders loss turned the `stale` row from FAIL to a skip on the very run that prompted it, and `[End]` said `DefendersDead` with a defender alive | Gate proves less than it says | The first draft changed the end reason and did not follow it to its consumers, though the rule that a reason means one fact was implicit in the comment | Lens 5 traced each consumer; `standing=` in the check, `defenders N` in `[End]`, a director test, corrected comments |

## Not applied, with the reason

- **Restoring a formation's place after a bought pack:** it matters only for a player's Advance order between waves, and no run has shown harm. The feature doc names it.
- **Refusing a point recorded outside the walls:** only a person can record one, and a cavalry sally may want it later.

- **A latch for cheat mode switched on mid-run:** nobody has tested whether a player can do that. A guard for an unseen case is Mike's call under `simplicity-criterion.md`, and the plan names the gap.
- **The runner's report copy after a game closed by hand:** `run_fords_autorun.py` falls back to `fords_report_*`, so run `20261104-071020` left its report in `Logs`. Pre-existing and harmless to a verdict, because such a run already fails. no issue: nothing depends on the copy.

- **The shared horn formula (design 5):** the `early-start` check recomputes the amount on purpose. A check that shares the formula cannot catch a change in it.
- **A guard for a speculative race (first wave, data flow 10):** `simplicity-criterion.md` makes that Mike's call. I name it in the report.
- **A missing scene (data flow 12):** unverified, and Fords has the same path. A follow-up, or `/investigate` if it ever fires.
- **A disabled shop button's hint (XML 5):** a design choice for Mike.
- **A mid-wave power reading (engine 4):** it needs a rerun, and the thresholds were tuned with the current behaviour.
- **Tooling 8, 10, 11:** follow-ups in the shared Fords runner, which other sessions drive.
- **Tooling 12, the limits:** five waves took at most 2,427 live seconds, so they fit 3,600 s. No change.
- **A `rest` check in the judge (rest review, data flow 12):** a run can pass with the rule dead, and the first run's `rest:` lines are read by hand. One verdict, a manifest row and the fixtures' upkeep for a rule with two tests and a log line: `simplicity-criterion.md` says no for now.
- **Leaving a fleeing soldier out of the rest (rest review, data flow 9):** he counts in the line and gets the health. No harm, and a clause adds nothing.
- **The sentence in `TaomCustomBattleMoraleModel.cs:26` (standards 5):** kept. It matches its neighbours in a shared model, and each of them names its rule.
- **A second call to `ReturnToMainMenu` after a refused launch:** the call is guarded and caught, and I did not read what the engine does with it. It needs a data fault to run. Noted, not changed.

## Root cause

Rows 1, 3, 4 and 6 share one cause: a copy keeps the shape and loses the guard. Fords' patterns were copied on purpose, to leave its code untouched, and that made it easy to copy what a class does and miss what it refuses. The review's new-mission step caught row 1, and the plan should have listed every veto a Fords view overrides.

Rows 5, 7, 8 and 15 share a second cause: the fake stood in for the engine at the seams that mattered. The keep counts were zero, a felled soldier raised no removal, and the judge's evidence was written by the same hand as the judge. Real lines from real runs now sit in fixtures.

Row 16 and row 9 share a third: I judged a run by its verdict, not by what its numbers said. The first run passed at 2 waves, and nothing in that verdict said that the keep was out of reach or that the table climbed too fast.

## Why each lens missed these

Most rows have no earlier miss: this review was the first read of the code by anyone but its builders. Four rows show the limits of the tiers.

- **Data flow, first wave (rows 8 and 15):** it traced what the run does, and the domain's keep rule was tested and correct. Whether a test crosses the seam is the Completeness lens's question, and the tier rule runs that lens only in a second wave. That wave ran only because of row 1.
- **Engine compatibility, first wave (row 19):** row 19 did not exist yet. A fix pass made it, and the checker after that pass caught it by reading the engine's hit test. Without that checker, the tier rule would have left a MEDIUM's fix unreviewed.
- **Completeness (row 9):** it found the risk 5 answer because it reads the plan against the logs. No first-wave lens has that in its remit.
- **No lens can find row 16:** a balance problem shows only in a long run. The first full rehearsal found it, after the first wave had reported.

**Feedback to codify:** none new. "A verdict must be earned" and "evidence over claims" already cover these, and I name their repeat in the root cause.

**The convergence pass:** one `deep-reviewer` read every applied improvement for behaviour parity, and read the installed engine for the HIGH fix. It found no behaviour defect and two LOW leftovers, a stale comment and two dead test locals, both fixed. It traced the veto from `MissionScreen.IsPhotoModeAllowed` through `GetIsPhotoModeDisabled` to the disabled button, by reading only. Tier 1 also asks for Codex, and that stays owed on Mike's word.

## Proof

- **Tests, on the tree this RCA is committed with:** `dotnet test` 32,341 passed, 9 skipped; `python -m unittest discover -s tools/tests -t .` 7,157 OK, 11 skipped; the canary tripped; the API snapshot reproduces; `lint_docs.py --fail-on-drift` exit 0.
- **The new tests can fail:** a builder broke eight production lines at once (the keep counts, the 0.5 s gate, the fleeing argument, the bot's clock, the respawn guard, the felled skip, the driver's fault clause, the done file's `expect`) and each test went red. Handing the runner the Fords judge turns the judge test red.
- **Run `20261104-033424`, build `985b0d54e.dirty`:** PASS at 2 waves, every required check, HUD and order bar.
- **Run `20261104-035343`, same base:** lost at wave 4 of 5. The respawn (`player-death via=respawn`), a lost run's report and the best-run file worked, with no fault.
- **Run `20261104-054852`, clean build `1536be627`, with both waves' fixes and the third table:** lost at wave 5 of 5 after 4 cleared waves and 574 kills. Every check passed, and the judge failed only on the reason. `stragglers fall: count=1` is the felled path's first run in game.
- **Run `20261104-044306`, same base as the two above, with the first wave's fixes:** lost at wave 5 of 5 after 4 cleared waves. `[LongDefeat][Hud] end panel open` was written, the morale counter reached 19, and frames averaged 11.16 ms with a worst of 66.67 ms.

## What this review did not prove

- **In game:** the third wave table, five cleared waves, and every change from the second wave. The clean-tree runs after this commit answer those.
- **The keep:** no run has brought a sampled attacker within 39 m, so a keep loss has run only in tests.
- **What only a person can show:** the shop's clicks and its modal background, Leave, the Escape exit, Photo Mode's veto, the prefab colours, taking over a soldier with `E`. They are `[[human]]` rows or notes for Mike's first play.
- **A full field:** the live cap of 150 attackers with a garrison of 220 has not been measured.
- **Why losses swing:** the same table lost 151 defenders in wave 3 of one run and 91 in the next. In some waves attackers climb in numbers and in others they do not, and nobody has traced why.
- **A full 5-wave clear:** no run has ended by reaching its target. Mike closed the nearest one with 2 attackers left, and the straggler rule that answers that has not run in game.
- **The new engine:** changeset 125270 arrived at 06:34, and one run has played on it with no fault.
