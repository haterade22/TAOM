# RCA: Fords of War, the work of 2026-11-04 and 2026-11-05 (/deep-review)

## Top-line

A post-hoc `/deep-review` of the day's Fords of War commits (`c834915e9..062b30a89`: soldier control, lost lanes, towers with no
bowmen and a rock, wall wear 0.25, the 40-minute time limit, the speed key on L, `AiCommander.IsDue`, the Mordor swarm and
`tools/fords_round_robin.py`). The review had been waived while Mike tested in game, and was run on his "full authority" word
of 2026-11-05.

- **Tier 1** (a GameModel seam, mission ticks, engine calls). First wave: Lens 5 Data flow, Lens 2 Engine compatibility,
  Lens 7 XML and the Tooling lens. Second wave, after the Tooling lens's HIGH: Lens 3 Efficiency, Lens 4 Completeness and
  Lens 6 Design on the round robin tool, plus one convergence pass on the applied fixes.
- **Codex ran later the same day on Mike's word** (`ultra`, range `c834915e9..6d2c51de9`; prompt and raw output in the main
  checkout's untracked `docs/reviews/raw/codex-prereview-fords-day-2026-11-05.*`). Two MEDIUM, both fixed with tests: deaths in the
  result window entered the troop tally the round robin reads as the gold trade (now left out for `KillCredit.AfterEnd`), and the
  round robin counted a match whose speed changed during play (now it needs exactly one `[Speed] speed=4` line). It refuted a thread
  race and any result change after the end, and read the native crash as a target index of -1, as the crash triage did.
- **Result:** 0 CRITICAL, 2 HIGH (one in the first wave, one in the convergence pass), 9 MEDIUM, about 20 LOW, 0 API
  incompatibilities. Every confirmed finding is fixed except those under "Not applied".
- **Proof:** the full `/verify` on the final tree (see the commit bodies), plus mutation checks: each counting rule of the round
  robin was disabled in turn, and a test failed each time. Nothing new ran in game; the round robin on the frozen build
  `92973f27a` keeps running and counts with the fixed tool.
- **Also found during the review:** the engine fault at `TaleWorlds.Native.dll` RVA `0x6B716E` (changeset 125270) crashed
  Rohan v Mordor at about 10:46. It is the same site as `0x6B4C4E` on changeset 125053 (`native_build_match.py`: MATCHED,
  hash), now seen twice in about 55 matches. No TAOM call on that path sets an AI target; see the feature doc.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `fords_round_robin.py` counted matches that never finished (no winner as a draw, a non-final report with a winner as a win, a run that never went live), and each dropped its pair from `todo`. | Gate without tests | The tool was written in the evening as a counter, and only its pure functions had tests; `results_for`, which decides what counts, had none. The same day's lesson "Break the asset before you trust a gate you wrote for it" applied and was not followed: a repeat offender. | Temp-tree tests for `results_for`; a mutation check that disables each counting rule and expects a failing test. Lesson in `build-tooling-workflow.md`. |
| 2 | HIGH | The convergence pass's fix list removed `agent.CommonAIComponent?.Initialize()` from `FordsAgentControl.Return` as dead code; it is live, so a soldier handed back would have kept morale at a flat 50 with no recovery. | Engine assumption | Lens 2 and the orchestrator read only the launcher's behaviour list. `MissionState.OpenNew` adds `AgentCommonAILogic` by default (MissionState.cs:302, :329). | Reverted before landing, pinned by a wiring test whose message cites the default. Lesson in `adapters-taleworlds-api.md`. |
| 3 | MED | After a match ended, towers still took damage through the 25 second result window (wear frozen at its highest, heroes past the assault limit), so a keep could fall under "time ran out" and the run tools recorded a time win as a keep win. | Lifecycle | Three sibling callbacks (`OnUnitKilled`, `OnCampUnitKilled`, `OnTowerDestroyed`) gate on `Phase == Ended`; `AdjustTowerDamage` never did, and the time limit made the window reachable. | `Judge` returns 0 with `TowerDamageReason.MatchEnded`, so damage and the reason for a 0 blow agree. Lesson in `state-lifecycle-save.md`. |
| 4 | MED | The round robin accepted a `.dirty` or `.nogit` build by substring. | Tool input | The build test was a substring of the stamp. | Anchored `+<sha>$` match, tested. |
| 5 | MED | Any run of a pair counted: the swapped orientation, a mirror, a run with no names, a 10x smoke run, a run without `--west-ai` or with `--control-check`. | Tool input | The tool assumed every folder of the build came from the queue. | Only the scheduled order, 4x, both sides AI and no control check count; each left-out folder of the build is named on stderr. |
| 6 | MED | One unreadable report stopped `todo`, and the queue read the empty output as "round robin complete". | Error handling | No per-folder error handling, and the caller had no exit check. | Per-folder try and skip; the queue stops on a failed `todo`. |
| 7 | MED | A troop id both factions field was credited to West. | Data attribution | The game tallies by troop id alone; East was filled first and West overwrote it. | A shared id counts for neither side. |
| 8 | MED | The schedule and troop table came from the checkout's `factions.json`, not the build's. | Tool input | Assumed the tool runs on the build. | `git show <build>:.../factions.json`; an unknown build is an error. |
| 9 | MED | Nothing showed the rock working, and its launches skewed check 11's hit rate (one launch in nine is a rock). | Diagnostics | The rock reused the volley path, and its hits were filed with lane archers as `otherItem`. | `Volley.RocksLaunched`, `Filtered.rock`, a reach warning, and a shipped-item test. |
| 10 | MED | A 30-damage Boulder can knock down a soldier it does not kill; nothing said so. | Undocumented behaviour | Vanilla's 2500 boulder kills whatever it hits. | Documented in the item comment and the feature doc; the round robin's blow lines will show how often. |
| 11 | LOW | The run evidence never recorded an end on time, so check 14's keep part read NOT_SEEN on every timed end. | Diagnostics | The time limit added an end path without its evidence. | `End.EndedOnTime`, accepted by check 14, read by both tools, and a test that the written JSON carries the key. |
| 12 | LOW | Zeroed own-side tower shots on units had no counter. | Diagnostics | Check 10 counts own-side blows on towers only. | `ownTowerShots` in the `[Upgrade]` totals line. |
| 13 | LOW | The rock's comment and doc misdescribed the ring's armour and radius. | Wrong claim | Written from the item's flags, not from `Mission.MissileAreaDamageCallback`. | Corrected from the engine lines. |
| 14 | LOW | Rivendell's and Mirkwood's slower free waves were never told to the player. | Completeness | A data tune with no trait line. | Two weakness traits with seeded text. |
| 15 | LOW | Minutes and "won on time" used the last 30 s summary instead of `End.WinnerMt`; `--control-check` accepted 0; test names claimed more than they checked. | Tool details | Written in one pass. | Fixed and tested. |
| 16 | LOW | Two director wirings (`EndedOnTime`, the rock flag) had no test that fails without them. | Test coverage | Covered only at the report level. | Scenario tests through the director. |

### Not applied

- **The warning text says "Five minutes"** whatever `timeLimitMinutes` is (Lens 5 #6): the shipped value is 40, so no player sees it, and changing the English would stale twelve seeded rows.
- **A riderless creature of the tower's own side is not shielded from its own rock** (Lens 5 #5): UNVERIFIED, native `IsFriend`. Act only if a run shows it.
- **Dol Guldur's new weakness text is shared with other factions** (Lens 7 #4): changed once the round robin shows what sets Dol Guldur apart.
- **Vanilla's own take-over refuses heroes and agents under 25% health** (Lens 2 #6): Mike asked that a hero near the pointer win, so heroes stay takeable.
- **Five efficiency savings in the round robin** (Lens 3): each under a second for a full round, rejected under the simplicity rule.

## Root-cause patterns

- **A new path was added without listing what already depends on the old ones** (3, 9, 11, 16). The time limit is a second way
  to end a match, and the rock is a second kind of tower missile. Each reused the old plumbing, and each consumer that assumed
  only the old path (the hit hook, check 14, check 11's hit rate) went on assuming it.
- **A counting tool was trusted because it printed numbers** (1, 4 to 8). It is the gate the balance decisions rest on, and it
  met only clean folders until the review built bad ones.
- **Engine facts read from TAOM's own list** (2). The launcher's behaviour list looked complete, and two reviewers took it as the
  mission's whole list.

## Why each lens missed what it missed

- **Lens 5 Data flow** traced the neutral team and the time limit end to end and found 3, 11 and 12, but not 2: it reported
  `AgentCommonAILogic` absent from the launcher list without opening `MissionState.OpenNew`.
- **Lens 2 Engine compatibility** found 9, 10 and the wrong comments, and asserted 2 the wrong way, for the same reason.
- **Lens 7 XML** found 10, 13 and 14; it does not read tools.
- **The Tooling lens** found 1 and 4 to 8 on real run folders; the second wave added 5's arm rule and 8's build source.
- **The convergence pass** read `MissionState.OpenNew` and caught 2 before it landed, and found 16.

## Feedback to codify

- `adapters-taleworlds-api.md`: a mission's behaviours are its launcher's list plus `OpenNew`'s defaults.
- `state-lifecycle-save.md`: a new end path lists every hook still installed through the end window.
- `build-tooling-workflow.md`: the counting function of a tool that decides what counts gets temp-tree and mutation tests on its
  first commit (repeat of the dire wolf lesson of 2026-11-04).

## Addendum: the review of the Codex fixes (2026-11-05, afternoon)

Tier 1 on the fix diff: Lens 5 Data flow, Lens 2 Engine compatibility, the Tooling lens, and a Codex `xhigh` pass. Codex
confirmed its two findings closed. Applied:

- **The engine dump copy (`974e60bb2`) missed the full dump** (Codex R1, Tooling F1, proved by replay of the real watchdog logs):
  the Watchdog writes the dump twice and lets the game die right after the second pass, and a size rule needing two equal sightings
  saw that size once. Now the game's `watchdog_log_<pid>.txt` names the dump and its `Starting Crash Uploader` line says when to copy,
  which also ties the dump to this game's pid (Tooling F3). A failed copy is said once with the path to copy by hand (Tooling F2).
- **A diagnostic counter of soldiers targeting a faded mount was dropped** (Lens 2 F1, Codex R2): `Agent.FadeOut` routs the agent
  first, and the rout resets each soldier's target and flag, so the fade cannot leave the stale target and the counter could show
  nothing. The crash paragraph in the feature doc now says so and names the open candidate: the delete of a dead agent.
- **Test strength** (Lens 5 6, Tooling F5): the after-end scenario test proves its path ran; a single 10x `[Speed]` line is a case.
- **The speed pattern is anchored** to `[FordsOfWar][Speed]` (Lens 5 8), and the counting rule is written where decisions are read
  (Lens 5 9), with the real cause of the one changed speed: a soldier taken with P runs the match at 1x.
- **The gold-trade window** is stated in the balance log (Lens 5 3).

Not applied:

- **Hero seconds run to the mission close** while kills stop at the winner (Lens 5 4): it moves only the review tool's hero rows,
  not anything the round robin counts.
- **A same-side death credits the killer's troop** in the tally (Lens 5 5): no report of nine has one.
- **"Soldiers never target a riderless mount"** in the patch registry rests on addresses from before changeset 125270 (Lens 2 F2):
  pre-existing text in another feature's entry.

## Addendum: the third buy row and Dol Guldur's uruk roster (2026-11-05, evening)

Tier 2 (UI that reads game state, Fords troop XML): Lens 5 Data flow and Lens 7 XML at `high`, and Codex at `xhigh` on Mike's word.
No CRITICAL or HIGH. Applied:

| # | Sev | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| 1 | MED | Free-wave archers spawned with the role Infantry in every faction (27 entries, 25 troops): `WaveScheduler` falls back to Infantry when neither the wave entry nor an offer names the troop, so the field probe and check 09 counted them as melee. Found on Dol Guldur's new uruk skirmishers (Codex, Lens 5); the test then found the other 26. | The fallback was silent and no test compared a wave troop's role with its own class. | `EveryFreeWaveTroop_SpawnsInTheRoleItsTroopDeclares`; each entry names `"role": "Ranged"`. |
| 2 | LOW | A doc line still said Dol Guldur calls spider riders. | Three of four lines were updated by hand. | Fixed. |
| 3 | LOW | The buy key count lived in two places, and a test (the two dearest offers within the hotkeys) could no longer fail while the count test passed. | Both rules predate the third row. | `OfferHotkeys` reads the key table; the count test reads `OfferHotkeys`; the redundant test is gone. |

Not applied: the new troop copies keep their attributes on one line (Lens 7, style only; the engine reads attributes by name), and a
spawn failure still moves later offers up one key (Lens 5, pre-existing, no shipped troop fails).
