# RCA: Seat of Seeing, stages S4 and S5 (deep review, 2026-11-04)

**Top line:** the tier 1 `/deep-review` of the hero who fights on (S4) and of the take after a fall (S5) found one defect that would have stranded the hero on the AI and one that reaches trunk: the Seat and Marshal's Word read `GameNetwork.IsSessionActive` to spot an online battle, and that fact stays false in a co-op battle, because BannerlordCoop never sets the engine's multiplayer state. Field runs also showed that three trial steps judged too early or depended on where the camera looked. The last two runs, `20261104-193822` and `20261104-193933`, passed all six Seat steps (`enter` ran in both) with `checks_fail=0` and `swallowed=0`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (Codex 1) | `EndFight` cleared `_handed` before the hand-back, and the adapter wrote `Controller = Player` last, so a throw left the hero on the AI with no Seat, no morale gate and no retry | A cleanup that gives up its own debt | The flag was cleared before the hand-back, and nobody asked what a throw left behind. The test asserted the flags, not the controller | `HandBack` writes the controller in a `finally`, and the session retries at most three times. The test asserts the controller |
| 2 | HIGH (Tooling F1) | `0.toml`'s `enter` and `fight-on` fail lists missed the panel gate's `[ERROR]` line, so `ingame_verdict` read PASS on a log the trial judge failed | A judge blind to its feature's own failure line | The same class as S2 and S3's row 7: a list kept by hand. The S4 line came after the list | The line is in both lists, and a sweep test reads every fixed-prefix `LogError` in the feature's code |
| 3 | MED (lens 5 item 2) | `SeatMissionClassifier` read only `GameNetwork.IsSessionActive`. The BannerlordCoop repo has no use of `CurrentGameType`, so a co-op battle read as single player. The take and the hero who fights on would have been open there, and Marshal's Word was already open there on trunk | A fact read from the wrong owner | The contract answered the co-op question with the native fact, and the unit test fed `network: true` by hand, so nothing pinned where the fact came from | The classifier takes `ICoopSessionProvider` too, and `SeatNetworkSessionFactTests` pins both reads. Lesson below |
| 4 | MED (Codex 2) | `Patch140_BoundaryCountdown` tested only `IsSeated`, and a client-hosted network mission reaches it through `TickForAgentAsServer`, so a host could dodge the boundary's punishment by entering the Seat | A presentation patch that changes authority | The prefix returns void and looks harmless. The registry's reason looked at the campaign | The hold needs the policy's `FightOn` permission, with a source pin of the prefix |
| 5 | MED (Codex 3) | `morale_held` could pass with its write removed | A step that does not judge its own stimulus | The step checked the outcome and printed the morale without reading it | The step reads the morale back at the floor before it waits, and the stand-in can ignore the write |
| 6 | MED (lens 5 item 1) | The take refused howdah crew but not Mumak crew, who carry a separate origin | A rule that names one of two siblings | The rule's comment said "howdah or war tower", and the code named one type | Both origins are named, and the pin covers both |
| 7 | MED (lens 2 F1) | A retreat that was live at the hand-back survived it, and a later hand-off wrote `retreat-stopped` while the hero kept running. `IMBAgent.SetController` does not write the retreat field (`+0xb00`), by native decompile | A log line that claims what it did not check | `StopHeroRetreat` returned true on the old read | `HandBack` and `StopHeroRetreat` make the conditional stop, and the return reads the result |
| 8 | MED (Tooling F2, Codex 4) | A human row promised a refusal of another party's soldier, which neither the code nor the contract has | Text against code | The row was written from the hero's rule | The row now names who is refused and who is taken |
| 9 | MED (lens 7 item 1, lens 5 item 6) | A second press after a take said "Your hero still stands" to a player whose hero was dead | A message written for its first caller | `HeroAlive` means the main agent lives, and the taken soldier is the main agent | New text in the literal, the English row and 12 language rows, pinned by `SeatPlayerTextTests` |
| 10 | MED (wave 2 Standards 1) | The R3 fix put `IoC.Resolve` inside a static classifier | A fix that breaks a rule the gate cannot see | `ServiceLocatorGateTests` scans only some folders and names | Each view resolves the provider once and passes it |
| 11 | MED (wave 2 Completeness 1, 2) | The boundary fix's wiring had no test, and the panel gate's logger could be cut without a test failing | A test that does not reach the wiring | The test asked the rule, not the prefix | A source pin of the prefix, and a test of the logger's handover |
| 12 | LOW (field runs) | `hero_falls` read the gate in the kill's own tick, `back_to_stand` read a hero whose company still held a Charge, and `take_soldier` depended on the camera's aim (`reason=nobody`) | A step that reads before the reaction, or trusts the ground | The test world reacted in the same tick, and the camera happened to look at soldiers | The step waits within a limit, stops the company and reads `before=`, and keeps a company before the fall. Each failure mode has a test. On `back_to_stand`, no root cause was shown for `speed=2.0`: the passing run read `before=0.0 speed=0.0` on a quiet ground |
| 13 | LOW (lens 2 F2) | The hand-off cut a use short with `isSuccessful` true, so a pick-up was granted | An engine default not read | The default was not visible at the call | `isSuccessful: false` |
| 14 | LOW (lens 5 item 9) | The take could pick a soldier with no formation, which the Fords take refuses | Two siblings with different rules | Row 75 recorded only the race decision | Reason `no-formation`. A creature's rider stays takeable, as a named limit |
| 15 | LOW (lens 2 W1 to W8, lens 5 items 3 to 7, Codex 5, Tooling F3 to F5, F7, lens 7 items 2 to 5, Standards 2, 4, 5, Design) | Eight wrong engine claims in comments and docs, among them "MoveToClear and IsDefending then throw", which do not throw, plus stale counts and status text, gendered text, an untraced swallow and small simplifications | Text against code | The claims came from the contract and were not re-read when the code settled | Each was corrected, and contract lines got Deviations rows 88 to 100 |

**Not applied:** wave 2 Standards 6, moving `SeatOrderPanelTranspiler` to its own file. The win is small, and the move touches two pinned names.

FOLLOW-UP: Tooling F6, a test that ties every `NoteCall` site to `SEAT_ERROR`. no issue: every site matches today, and the issue is waived for this work by the maintainer's word.

FOLLOW-UP: the fade timer can beat the once-a-second retreat stop (lens 5 item 8), and a double fault on the begin path leaves no debt (Completeness 5). no issue: both are named in Known limits; neither has a known producer.

## Root-cause patterns

**Facts taken from the nearest API.** Findings 3, 4 and 7 each read an engine fact that sounded right: "a network session", "the hero is seated", "the agent stopped". Each was true for a different question. The first needed a read of the co-op mod's own code, which this review was the first to make. `coop-interop.md` repeated the same fact until this fix.

**A failure path nobody ran.** Finding 1 is a throw between two writes, in code whose normal path passed in the game.

**Steps that trust the world.** Finding 12's three failures are one mistake: the step assumed a reaction or a position that the test world gave it for free.

## Why each agent missed these

- **Lens 5 (data flow)** found 3 and 6. It did not walk a throw through the hand-back, because it traced data, not failure. Codex found 1.
- **Lens 2 (engine)** found 7 and the wrong claims. It did not report who reaches the boundary patch in a network mission. Codex did.
- **Tooling** found 2 by reading the verdict on a log that held the feature's own failure line.
- **Lens 7 (XML)** read the strings against the code and found 9.
- **Earlier reviews (S1 to S3)** did not report where the session fact came from.

## Feedback memories to codify

None. The lessons below hold the rules.
