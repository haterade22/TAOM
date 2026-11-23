# RCA: the gate rule that would have gone blind at Helm's Deep (2026-11-22)

Found in review, before any commit. No player and no run met it.

## What was changed

`gate-attacked` (issue #1079) failed whenever an attacker was sampled within 3 m behind a standing repaired gate. At Minas Tirith attackers climb the walls in every wave and stand behind the gate without passing it, so the line failed in every later wave (runs `20261122-095258`, `20261122-111133`). The change lets attackers above the walls excuse that count.

## Findings

| # | Severity | Finding | Fix | Proof |
|---|---|---|---|---|
| 1 | HIGH (data flow lens; P2 from the independent pass) | The first form passed `read.AttackersHigh > 0` as "an attacker is on the walls". The adapter's test is a height: `agent.Position.z >= _highZ`, with `_highZ` the highest wall segment less 3 m (`LongDefeatBattlefieldAdapter.Diagnostics.cs`). At Helm's Deep that line is 6.9 m and the gate stands at 20.7 m, so every attacker in the doorway is also high in the same sample. Every doorway fail there would have become a skip, and the judge would have taken it as paid | `ILongDefeatFortAdapter.HighLineClearsGates`: the high count excuses a doorway count only where the high line lies above every gate's doorway band. False at Helm's Deep (the rule is as it was), true at Minas Tirith. A boot line logs the two heights | `AttackersBehindARepairedGate_FailTheCheck_UnlessAttackersStoodOnTheWalls` (the third case: high attackers, line not clear, fail) |
| 2 | P2 (independent pass) | `WallsReached` was sticky for the whole watch, so a climber late in the wave excused an attacker sampled behind the gate early | `GateWatch.ThroughBeforeWalls` counts the doorway samples taken before the walls were first reached, and any of those fails the line | `GateAttacked_AnAttackerBehindTheGateBeforeTheWallsWereReached_StillFails_AndTheLineSaysHowMany` |
| 3 | MEDIUM (tooling lens) | A walls-reached skip paid the judge's debt even when nobody struck the repaired gate | The check tests the blow first: an unstruck gate reads `reason=no-blow` as before | `GateAttacked_WithTheWallsReachedAndNoBlow_IsTheNoBlowSkip_NotTheWallsSkip` |
| 4 | P2 (independent pass) | One walls-reached skip paid for every repair of the run, and a reason with text after it was accepted | `walls_reached_waves` asks for a skip of each repair's own wave that ends the line, and no other gate-attacked skip | `test_the_walls_reached_skip_must_be_the_repairs_own_wave_and_must_end_the_line` |
| 5 | MEDIUM (tooling lens; P3) | The PASS verdict listed gate-attacked as passed when it was waived | The verdict leaves it out of the passed list and names the waves not judged | `test_a_walls_reached_skip_stands_in_for_the_pass_and_a_mixed_or_other_skip_does_not` |

## Why finding 1 was missed

I took a name for a meaning. The state line prints `high N`, the split code calls its groups "high archers", and I read "high" as "on the walls" without opening the line that computes it. The rule was designed from two Minas Tirith logs, where the walls are far above the gate, and it was not checked against the one other fortress, where the check had been proven. The unit tests could not see it: they set the fake's `AttackersHigh` and the fake has no geometry.

## Prevent

- A value that a new rule leans on is read at the line that produces it before the rule is written (AGENTS.md "Research first": quote the line, never infer from a name). This applies to TAOM's own adapter values as much as to the engine's.
- A rule made for one fortress is checked against every shipped fortress row before review, with the numbers of each scene.
- Lesson entry: docs/reviews/lessons/testing-qa.md, "A count named for what it suggests".

## Not applied

- The high count includes attackers who run away and leaves out the main agent, while the doorway count does the reverse (LOW). A fleeing attacker above the walls still marks the walls as crossed.
- A leak that happens between two 5-second samples is not seen, as before this change.

FOLLOW-UP: no issue: the two items above are limits of a diagnostic check and change no play.
