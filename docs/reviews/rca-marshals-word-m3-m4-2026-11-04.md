# RCA: Marshal's Word, the M3 slice and M4 (deep review, 2026-11-04)

**Top line:** the tier 1 `/deep-review` of the volley without Massed fire (M3), the mark and outlines (M4) and the trial steps for M1 to M4 found no HIGH in the game code. Its two HIGH findings were in the trial's judge, which could not read a real SKIPPED line and passed a run with dead keys. The field runs did more: they showed that a mark steers archers (`onTarget=19 of=20`) and does not steer melee soldiers on a Move (`onTarget=1 of=20`), so the melee half of the stage stopped under its own rule, and they showed that the hold reaches most of a company. Run `20261104-184948` read at most 6 of 40 holding, and its cause was not shown. In run `20261104-194356`, at a gap of 80.3 m to the enemy, 35 of 40 held, and at the word 36 of 37 loosed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (Tooling F1) | `MARSHAL_ERROR` read `\S*Patch141_`, and the real patch classes are named `MarshalArrayPatch` and so on, so a real SKIPPED line passed the judge | A judge blind to its feature's own failure line | The guard's test made up a class name that no class has. The Seat's row 7 was the same class of bug, and the Marshal regex copied its shape | The regex takes the namespace, and a test reads the class names from source |
| 2 | HIGH (Tooling F2, Codex 5) | A failed hot key registration passed the trial with every real key dead: the module logged a warning, and each step presses by method | A judge blind to its feature's own failure line | The Seat had already fixed this (S2 and S3 row 8), and the fix did not reach the sibling feature | `LogError`, the tag in the regex, and a twin of the Seat's test |
| 3 | MED (Codex 6) | `array_keeps_offsets` could pass with the layout never applied: the drag followed the existing front, and the counter rises before the apply | A step that a broken feature also passes | The step's facts were unchanged positions, which a do-nothing build also gives | The drag moves the front 20 m, and the pass needs the order points moved by that shift (`planned=20.0 miss=0.0`) |
| 4 | MED (Codex 3) | `ApplyWord` used a census up to a second old, so the word could be judged on the holders of up to a second earlier | A stale read at a decision | The census was made for the summary, and the word reused it | A fresh census at the word |
| 5 | MED (Codex 4) | A second word could erase the first word's pending failure | A new event that overwrites an open judgment | The judge had one window and one writer | An open window stays until it is judged |
| 6 | MED (Codex 2, lens 5 item 7, lens 2 F7) | A fault left the callback flag on every touched soldier for the rest of the mission. The comment said the agents go with the mission, which is false after a fault | One teardown written for two exits | The drop was written for the mission's end, and the fault path reused it | One owed agent cleanup, paid at the next mission tick |
| 7 | MED (Codex 1) | One throw while clearing contours stopped the rest, and the records were then dropped | A loop with one try | No real throw path is known | One try per unit. No retry: no real throw path is known, so a retry loop fails the simplicity rule |
| 8 | MED (lens 5 item 1, lens 2 F2) | `queue_two_moves` walked with the panel open, at a quarter speed on the engine's default option | A step that depends on a saved option | The option defaults to true, and the stand-in modelled no time request. The saved option on this machine was not read | The step closes the panel first, and the stand-in walks slowly with the panel open |
| 9 | MED (Tooling F3) | `captured` had no fail row for the patch category | A sibling's row not copied | The Seat's row came later | The row, with fixtures |
| 10 | MED (wave 2 Efficiency 1) | `ReadMarks` sampled and logged every vanilla charge once a second, because a charge also sets a target | A read that matches more than it means | The contract asked for each marked company, and a charge looks like a mark | Companies on Charge, Advance and ChargeToTarget are skipped |
| 11 | MED (wave 2 Completeness 3) | The owed cleanup's throw path had no test, and it runs outside the session's try | Test gap | The fake could not throw | `ClearThrows` on the fake and three tests |
| 12 | LOW (field runs) | `queue_two_moves` and `mark_keeps_order` armed against a panel that their own direct order had shut. `array_keeps_offsets` read companies still walking. Eight steps in one battle ran out of battle | A step that assumes its ground | The stand-in kept the panel open after any order, and I ran the steps in one battle although the contract gives each subset its own | Each cause has a test on a stand-in that now behaves as the engine does, and each subset runs in its own battle |
| 13 | LOW (field run, stop rule) | A mark does not steer melee soldiers on a Move, 1 of 20 at 29.7 m | A belief that only the game could prove | Section 8.1 said nobody had read how the AI uses the value, and it carried a proof for this reason | The melee half stops: the context, the step and the row go, and the text says a mark steers ranged soldiers. Deviations row 19 |
| 14 | LOW (field run) | A release at the hold limit was counted as a shot after the word, so it could pass the release check with no word given | A table row that was wrong | Contract 7 counted any release in the last 2 s | The component records the cause, and `shotsAfterOther` holds the rest |
| 15 | LOW (lens 2 F1) | Text said a captured click leaves the panel open. A click on a focused enemy company shuts a toggled panel, as in the base game | Text against code | The sentence was reasoned from the ground click | Corrected in Deviations row 16, Known limits and a human row |
| 16 | LOW (lens 5 items 2 to 6, 10, 11, lens 2 F3 to F7, Tooling F4 to F9, lens 7 items 1 to 4, wave 2 Standards 3, 5, 7, Completeness 4, 7 to 9, Design 1 to 7) | Dead members, skip keys that lost a company, stale text and pointers, one screen tick cache write in the trial, a mounted target counted wrong, and small simplifications | Various | Each was small and in code that was new | Each was applied with its test or corrected |

**Not applied:** lens 5 item 9, forcing saved settings in the trial. It cannot give a false pass, and it adds a settings class. The doc names it.

FOLLOW-UP: the cause of the 6 of 40 holding in run `20261104-184948`. no issue: Massed fire reads the same numbers, and its step will show whether the low share returns.

## Root-cause patterns

**The sibling's fix did not travel.** Findings 1, 2 and 9 were all fixed in the Seat a day earlier. The Marshal builder worked from a contract written before those fixes, so a rule that lives only in one feature's tests does not reach the next feature.

**The stand-in is too kind.** Finding 12's failures all passed on a stand-in that kept the panel open, had no slow time and reacted in the same tick. The stand-in's panel and slow time now do what the engine does.

**A proof built into the contract works.** The contract named the two beliefs only a game could prove (a held draw, and the mark's effect) and gave each a stop rule. One held, and one stopped a half of a stage for one line of text instead of a feature that does nothing.

## Why each agent missed these

- **Lens 5 (data flow)** found 8 and the 5 of 40 reading. It could not see the judge's blind spot, because it followed the data that flows.
- **Lens 2 (engine)** found 8 and 15. It did not settle the low share. Only a game run could.
- **Tooling** found 1 and 2 by feeding real lines to the judge.
- **Lens 7 (XML)** found the 14 s text.
- **Codex** found 3 to 7.

## Feedback memories to codify

None. The lessons below hold the rules.
