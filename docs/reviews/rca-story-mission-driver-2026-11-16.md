# RCA: the unattended story-mission run (#1368)

Findings from the tier 1 review of #1368 on the uncommitted worktree `feat/hero-companions` over e3f612bad. A second architect refuted the pinned plan before any builder started. Three lenses then read the built tree (5 Data flow, 2 Engine compatibility, Tooling); no XML changed, so the XML lens did not run. They found no CRITICAL and no HIGH, so no second wave ran; two MEDIUM and a set of LOW items were fixed and verified with tests. The engine lens found no incompatible call. The Codex pass did not run: the maintainer said to skip it while the account has no usage.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | The driver's own done row in the new manifest was scoped to the mission's launch line. Every failure before the launch (a refused menu, a campaign that never reached the map) writes a done line with `result=fail` and no launch line, so the manifest read NOT REACHED where the runner said FAIL, and with more than one session an older pass could stand in for the failed run. The row's comment said the opposite | A scope copied to a row it does not fit | The three rows were written together and the scope of the two mission rows was given to the third. The plan and its refutation both argued about what could pass wrongly and not about what could fail unseen | The done row has no scope and its fail pattern names the mission; the manifest is read from one session. Tests feed it a failed run with no launch |
| 2 | MED | The message "this build did not arm the run" was written in the judge. The runner calls the judge only when a done file exists, and a build that does not know the kind writes none: the game sits at the main menu, the runner waits ten minutes, takes a dump of an idle process and reports a stall. The unit test called the judge directly and passed | A diagnosis in a branch the caller never reaches | The refutation asked for the message and named the symptom (a slow failure); the builder put it where the other log checks are, and tested the function, not the path | The result printers, which the runner calls for every stop reason, print the line; the test goes through the printer |
| 3 | LOW | `--speed` and `--stall-seconds` were accepted and ignored for the story kind, while `--days` was refused; the game matches a mission id in any case and the judge matched it exactly; an abandoned unattended mission read NOT REACHED in the manifest where the runner fails it; a docstring said the game's own wall limit trips first, and the story phases read no wall limit; the manifest of #1366 called the new command a proof before any run; the doc's engine facts lacked the five the story kind relies on and left to the first run an ordering the engine source settles | First-draft gaps | Each is a line or a pattern | Fixed in place, each with a test where code or a pattern changed |

Process notes, not findings of the lenses: the refutation of the plan found that an event window can stop the mission tick (the layer asks the engine to idle the active state) and that a mission the lord's body walked out of ends as Lost with a passing line; both were closed in the build. It also moved the run's end from "some days after the return" to the issue's own "when the map is back".

Not applied:

- No log poll in the shared runner to end the ten-minute wait on an unarmed build early. The message now names the cause; the wait stays.
- No wall-limit check inside the story phases. The step waits and the mission limit bound them, and the runner's own limit is the outer bound.

## Proof

Run 20261116-075342 on the committed build 4baa2f53a, the first run of the kind. The driver's lines, in order: `phase WaitMap -> MissionMenu`, `story menu: The story menu is open for 'helms_deep_defend'. Choose "Open the mission".`, `phase MissionOpen -> InMission`, `mission ended verdict=Won seconds=390.3`, `chose Back to the map`, `back on the free map after Back to the map`, `done reason=ok`. The done file reads `reason=ok`, `verdict=Won`, `mission_seconds=390.3`, `event_windows_closed=0`. The runner printed PASS with a clean unload and no crash block, and `python tools/ingame_verdict.py --issue 1368` reads PASS for its three rows.

What the run did not exercise: a refusal, a mission that hits its limit, an event window inside the mission, a return with no menu, an unarmed build. Those rest on the unit tests.

## Root-cause pattern

Both MEDIUM findings are a result placed where its reader does not look: a row behind a scope that a failed run never opens, and a message behind a branch that a failed run never enters. In both, the test exercised the piece and not the path a real failed run takes.

## Why each reviewer missed these

- **The architect's plan and the refutation** asked what could pass without proof. Neither asked what the manifest reads when the run fails early.
- **The builders** tested the judge and the manifest patterns in isolation.
- **Lens 5 and the tooling lens** each ran the real manifest and the real judge on made-up failed runs, and read the runner's call site for the judge. **Lens 2** read the same call site from the engine side and settled the callback order from the engine source.

## Lessons recorded

One entry in `docs/reviews/lessons/testing-qa.md`, dated 2026-11-16 with this file as its source.
