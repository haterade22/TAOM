# RCA: Encounter states with no automatic exit (#1021, deep-review, 2026-10-26)

## Top-line

#1021 listed three rare enlistment encounter states with no exit, found while #796 was reviewed. An architect
confirmed all three against v1.5.5 and found a fourth, worse one (a frozen commander battle when the battle
menu failed to open after a landed join). The first build fixed all four. `/deep-review` (an engine lens and a
data-flow lens) found **two design gaps** in that build, both fixed before the push: the encounter kept inside
the town carried the repulsed assault's result into the next assault, ending it the moment the soldier joined
(MEDIUM, and already reachable through a save and load before #1021); and the discharge fix reached only a
soldier fighting outside his commander's army, while a merged defender was still pulled out of the battle by a
discharge that runs mid-battle. The feature switched off in the options is one of two such discharges; the other
(an heir mismatch at load) is tracked in #1030 (MEDIUM).

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Data flow, Engine compat) | Reusing a joined encounter after a repulse kept its saved battle result; the next assault the soldier joined continued from "enemy retreated" and ended at once, leaving him on `menu_siege_strategies` with no battle. A defender's repulsed assault keeps its encounter with the result still set (`EnemyRetreated`); the siege component's continue check returns false on that result only when the other side is not the attacker, and for a defender it is, so the continue handler finished the new assault. Before the fix, every later assault on that town ended the moment he joined. | Saved engine state carried across a reuse | The plan verified that the encounter could be reused, not what it still held. | Before a join, a reused encounter with no battle forgets the ended battle's result (every component's continue check returns false on a null result). |
| F2 | MEDIUM (Engine compat, Data flow) | Discharge step 5 (`RestorePresence`, whose first call clears `AttachedTo`) takes a merged soldier out of a live battle before step 6 and step 7 look at him, so the step-10 guard never saw him in his battle. | A guard placed after the step that removes its condition | The discharge fix was tested with an unmerged soldier. | The feature-off release waits for the battle to end, as the crown-war release already does; the step-10 guard stays as a floor. The heir-mismatch discharge at load still runs mid-battle (#1030); a guard on step 6 alone cannot hold him. |
| F3 | LOW (Data flow, Standards) | A discharge test could not fail (no presence was configured, so step 11 never ran); the recovery time was stated as "within an hour" and later as one to two hours, but it fires on the second or third hourly tick (within three in-game hours); a log line and two comments named only the old case. | Test strength and accuracy | Routine. | Test configures presence and pins the warning and the menu pop; wording and log line corrected. |

## Convergence pass

Text corrections after a verification against the v1.5.5 engine source, no logic changed:

- F1: the mechanism is the siege component's continue check reading the old `EnemyRetreated` result for a
  defender (the other side is the attacker), after which the continue handler finishes the new assault and
  opens `menu_siege_strategies`. A null result makes every component's check return false.
- F2: the step that pulls a merged soldier out of a live battle is discharge step 5 (`RestorePresence`), not
  step 6. The residual note now sits with step 5.
- F2 residual: the feature-off release is one of two mid-battle discharges; the heir-mismatch discharge at load
  still runs mid-battle and is tracked in #1030.
- F3: stale-latch recovery fires within three in-game hours (the first latched tick only sets the anchor, and
  the elapsed check against 1/24 day falls short by float rounding on about half of the hours).
- F6: only `FinishEncounterInternal` teleports; the walk-out is `PlayerEncounter.Finish`'s own
  `LeaveSettlement()`. Both need `AttachedTo == null`, so the conclusion is unchanged.

## Root-cause pattern

Both gaps came from checking a state by its shape at one moment, not by what the engine had stored in it (a
saved battle result) or changed just before (step 5's presence restore). For a fix in engine encounter state, list the
saved fields the reused object carries and the steps that run before the guard.

## Feedback memories to codify

None.
