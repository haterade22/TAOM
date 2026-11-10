# RCA: crew spawners and the 2,040-agent limit (#1107), deep review of 2026-11-09

## Top-line

`/deep-review` at risk tier 1 covered the uncommitted #1107 fix on top of `e6edc5729`. The reviewers were Codex (`ultra`,
read-only sandbox, under decision D2 of `harness-retro-2026-11-03.md`), the data flow lens and the engine compatibility
lens. They reported no CRITICAL or HIGH, so no second wave ran. No reviewer found a crash, hang or corruption in the
check or in the three choices the maintainer approved.

Nine findings were confirmed: 2 MEDIUM and 7 LOW. All nine are text: doc sentences, code comments, one log message and
the issue body. The check itself did not change. Each finding below was re-read against the code or the decompile
before it was fixed.

The fix is not proven in game. No automated check reproduces the crash, and the in-game check on #1107 is still owed.

## Findings and root cause

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Codex F1, data flow F4, engine 1) | The engine doc and the `CrewAgentBudget` summary said vanilla compares `AllAgents.Count` with the limit before it spawns. Only reinforcements do (`MissionBattleSideSpawnContext` line 105, `DefaultBattleMissionAgentSpawnLogic` line 315). The initial spawn at line 614 reads no live count and relies on `_battleSize`, capped at line 121. | Claim generalized from one path | The sentence came from reading `TryReinforcementSpawn` only. The initial spawn path was not read for this claim. | Fixed in both places. Lesson: name the vanilla method that holds a check. |
| F2 | MEDIUM (data flow F1, engine 2) | The engine doc and the lesson said a spawn outside vanilla's budget calls `HasRoom`, with the gate named as proof. The gate matches only a direct `Mission.SpawnAgent`. Fords of War (3 sites), Long Defeat (1) and the dev console (1) spawn through `SpawnTroop*` with no check, and two of those can spawn a mount, so a one-slot answer would be wrong for them. | Rule wider than its gate | The rule was written from the two crew spawners. No step compared it with the other spawn sites, although the sibling check had listed them. | Text narrowed to direct callers, one slot, and what the gate proves. Lesson added. |
| F3 | LOW (Codex F2, data flow F3, engine 4) | The warning said "every later howdah seat stays empty". The `break` ends one machine's loop, and a beast drained later gets a crew if a slot is free. | Log text promises more than the code does | The text described the reported battle, in which the pool never freed. | Text now names this machine's seats, in both spawners and `elephant.md`. |
| F4 | LOW (engine 3) | The engine doc named two `std::map` headers and a mutex at `mission + 0x2B58` and so on. `FUN_1805bb0d0` constructs an object at `mission + 0x20`, and `FUN_1805a2980` places three tree anchors and a mutex in that object. The `std::map` type had no evidence. The conclusion holds, because the object lies inside slot -1. | Native layout read from one caller | The offsets came from the callers of the tree insert. The constructor was not read. | Sentence corrected. |
| F5 | LOW (data flow F4, engine 5) | The lesson stated "mounted on both sides" and the engine doc said the log "shows the spawn past that count". No log line prints the agent count; the issue labels this a hypothesis. | Inference stated as an observation | The label in the issue was not carried into the docs. | Both now say inference. |
| F6 | LOW (engine, Q8) | "non-canonical node pointer": the bundle gives fault address `0xFFFFFFFFFFFFFFFF` and does not log `r8`. "caught each `AccessViolationException`": the log shows only the first per spawner. | Inference stated as an observation | Same as F5. | Both sentences reworded to what the log shows. |
| F7 | LOW (Codex S11, O5) | `mumakil.md` called the agent budget "closed" with the in-game check still owed. | Status ahead of its evidence | The risk bullet was closed when the code was written. | Label now says fixed in code, in-game check owed. |
| F8 | LOW (data flow F2) | The #1107 body planned a trap index row. `lint_docs.py --fail-on-drift` rejected the row and it was removed, and the body was not updated. | Plan text left behind a removed change | The body was written before the lint run. | Trap index removed from the body when it is next updated. |
| F9 | LOW (data flow, nit) | A gate comment said zero callers means the match broke, while the assertion requires two. | Comment drift | Written before the floor was raised. | Comment corrected. |

## Root-cause pattern

F1, F2, F5 and F6 share one cause: a sentence written from one site or one log line and then stated as a general fact.
The first text of the fix was checked against the code that had just been written, and not against the sibling spawn
sites or the other vanilla path. Three reviewers found F1 or F2 independently, so the error was visible to anyone who
read the neighbouring code.

## Why each reviewer missed what it missed

- **Codex:** `native_decompile.py` did not run in its environment, so every native claim stayed UNVERIFIED and it could
  not find F4 or F6. It did read every spawn site and the vanilla paths.
- **Data flow lens:** it left the native claims to the engine lens, as its brief said, so F4 and F6 were out of its scope.
- **Engine compatibility lens:** it did not compare the docs with the issue body, so it did not report F8. It treated F7's
  status word as outside the engine claims.

## Observations that changed nothing in the code

- **The gate proves presence only** (Codex O1, data flow). `EveryDirectSpawnAgentCaller_ChecksTheAgentBudget` passes
  when `HasRoom` is discarded or runs after the spawn. A two-edit run on 2026-11-09 proved that removing the call fails
  the gate and names `HowdahCrewSpawner.Spawn`. A control-flow check or a spawner seam was not added: both spawners run
  on sealed engine types, and the cost is large against a check that is one line in two methods. The limit is stated in
  the test comment and the engine doc.
- **Caller names lose overload and closure identity** (Codex O2). No collision exists today. Not applied.
- **A throw after native creation could leave a slot uncounted** (Codex O3, engine Q1). The window is proven, but no call
  site produces the throw, and the maintainer approved keeping the catch. UNVERIFIED and left as it is.
- **Other spawn routes have no global check** (Codex O4). Read on 2026-11-09: only Harad offers crew-bearing beasts in
  Fords of War, at `cooldownSeconds` 120 in a 40-minute match, so at most 20 beasts and 160 crew per side. That brings the
  earlier bound of 1,208 agents to 1,528. Long Defeat waves name no beast.
- **The state this fix makes stable** (engine Q7): crews at the limit with soldiers waiting in reserve. Section 3 of the
  engine doc records an unverified report of a hang near the limit with reserves. The in-game check must use rosters
  larger than the battle size.

FOLLOW-UP: `docs/features/elephant.md` line 1025 names a `crew spawn:` log prefix that no code emits. Pre-existing. no issue: one token, fix it at the next edit of that table.

FOLLOW-UP: `docs/reference/engine/mission-frame-threads-and-native-costs.md` line 180 gives `0x6E5EE0` for `GetMaximumNumberOfAgents` on v1.5.6; the engine lens resolved it at `0x6E7030` on the installed binary. Pre-existing. no issue: the next engine bump re-resolves that row.

## Feedback memories to codify

None. The lesson is in `docs/reviews/lessons/adapters-taleworlds-api.md`.
