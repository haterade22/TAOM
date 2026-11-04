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
| 19 | LOW | A fix for the standards rule put `DoNotAcceptEvents` on the modal shop panel's root, so a click on the dim background would reach lower layers. The checker read `EventManager` and `ScreenManager` and caught it before any run | Rule applied past its reason | The rule was written for HUD layers, and I passed it on without its scope. Native's `EscapeMenu.xml` has no such flag | Reverted on the seven containers; the host's comment names the reason |

## Not applied, with the reason

- **The shared horn formula (design 5):** the `early-start` check recomputes the amount on purpose. A check that shares the formula cannot catch a change in it.
- **A guard for a speculative race (first wave, data flow 10):** `simplicity-criterion.md` makes that Mike's call. I name it in the report.
- **A missing scene (data flow 12):** unverified, and Fords has the same path. A follow-up, or `/investigate` if it ever fires.
- **A disabled shop button's hint (XML 5):** a design choice for Mike.
- **A mid-wave power reading (engine 4):** it needs a rerun, and the thresholds were tuned with the current behaviour.
- **Tooling 8, 10, 11:** follow-ups in the shared Fords runner, which other sessions drive.
- **Tooling 12, the limits:** five waves took at most 2,427 live seconds, so they fit 3,600 s. No change.
- **The sentence in `TaomCustomBattleMoraleModel.cs:26` (standards 5):** kept. It matches its neighbours in a shared model, and each of them names its rule.
- **A second call to `ReturnToMainMenu` after a refused launch:** the call is guarded and caught, and I did not read what the engine does with it. It needs a data fault to run. Noted, not changed.

## Root cause

Rows 1, 3, 4 and 6 share one cause: a copy keeps the shape and loses the guard. Fords' patterns were copied on purpose, to leave its code untouched, and that made it easy to copy what a class does and miss what it refuses. The review's new-mission step caught row 1, and the plan should have listed every veto a Fords view overrides.

Rows 5, 7, 8 and 15 share a second cause: the fake stood in for the engine at the seams that mattered. The keep counts were zero, a felled soldier raised no removal, and the judge's evidence was written by the same hand as the judge. Real lines from real runs now sit in fixtures.

Row 16 and row 9 share a third: I judged a run by its verdict, not by what its numbers said. The first run passed at 2 waves, and nothing in that verdict said that the keep was out of reach or that the table climbed too fast.

## Proof

- **Tests, on the tree this RCA is committed with:** `dotnet test` 32,341 passed, 9 skipped; `python -m unittest discover -s tools/tests -t .` 7,157 OK, 11 skipped; the canary tripped; the API snapshot reproduces; `lint_docs.py --fail-on-drift` exit 0.
- **The new tests can fail:** a builder broke eight production lines at once (the keep counts, the 0.5 s gate, the fleeing argument, the bot's clock, the respawn guard, the felled skip, the driver's fault clause, the done file's `expect`) and each test went red. Handing the runner the Fords judge turns the judge test red.
- **Run `20261104-033424`, build `985b0d54e.dirty`:** PASS at 2 waves, every required check, HUD and order bar.
- **Run `20261104-035343`, same base:** lost at wave 4 of 5. The respawn (`player-death via=respawn`), a lost run's report and the best-run file worked, with no fault.
- **Run `20261104-044306`, same base, with the first wave's fixes:** lost at wave 5 of 5 after 4 cleared waves. `[LongDefeat][Hud] end panel open` was written, the morale counter reached 19, and frames averaged 11.16 ms with a worst of 66.67 ms.

## What this review did not prove

- **In game:** the third wave table, five cleared waves, and every change from the second wave. The clean-tree runs after this commit answer those.
- **The keep:** no run has brought a sampled attacker within 39 m, so a keep loss has run only in tests.
- **The felled path:** Phase 1 runs hold no `stragglers fall` line, so it has run only in tests.
- **What only a person can show:** the shop's clicks and its modal background, Leave, the Escape exit, Photo Mode's veto, the prefab colours, taking over a soldier with `E`. They are `[[human]]` rows or notes for Mike's first play.
- **A full field:** the live cap of 150 attackers with a garrison of 220 has not been measured.
- **Why wave 3 climbs:** from wave 3 far more attackers reach high ground than in wave 2, in every run and on every table. Nobody has traced why.
