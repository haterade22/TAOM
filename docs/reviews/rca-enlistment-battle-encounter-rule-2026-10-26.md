# RCA: The battle-encounter rule that never ran (#796, deep-review, 2026-10-26)

## Top-line

#796 asked to wire a dormant flag (`IsBattleEncounter`) into the encounter ownership snapshot so its rule (R1b)
would protect the battle loot window. A first build did that. `/deep-review` (engine and data-flow lenses, then
an architect's redesign) found **one HIGH, two MEDIUM and one LOW defect** in that build, all from one wrong premise:
the rule assumed the main party leaves its map event before the loot screens, so a battle would look over
while its screens were open. In v1.5.5 the main party stays in its map event through the loot screens, and on that path
`PlayerEncounter.Finish` removes it (`MapEvent.cs:1743`, `MapEventSide.cs:434`), so R1 already protects that window. The flag is true
without a map event when the party has left the battle while the encounter stays open (a repulsed assault, or a
party pulled out of a live event), and there wiring it made a discharge wait forever. The build was abandoned before it was pushed; the
change that ships deletes R1b, the field and every check of it. The adapter never passed the flag, so nothing
changes for players.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Engine compat, Data flow) | Wired, R1b made a discharge in a leftover battle encounter defer forever: the record reads as not enlisted, so the stale-latch recovery never runs, and every retry asks the same rule. The discharge itself could create that shape. | A guard built on an engine-ordering claim nobody checked against the engine | The claim (`MapEventSide.Clear()` nulls the main party's map event) was copied between comments; every test built the snapshot by hand with the claimed shape. | R1b and the flag deleted; tests built from the shapes the engine produces (encountered party is the opposing leader after a join; the player still in his map event through loot). Lesson in `lessons/campaign-mechanics.md`. |
| F2 | MEDIUM (Data flow) | The first build's cleanup retry could not recognise a joined battle's encounter, could close a new encounter the player started, and outlived a re-enlistment. | Machinery added for a shape that needs no deferral | Followed the plan's premise. | Deleted with the build. |
| F3 | MEDIUM (Engine compat, Data flow) | The first build's rollback stayed in battle state when the player was still in the map event, where no recovery can act. | Same premise | Same. | Deleted with the build. |
| F4 | LOW (all) | About fifteen comments, docs and lessons repeated the wrong premise. | A false fact copied between files | Each copy cited the previous one. | Every copy corrected with the engine lines; historical RCAs left as written. |

Older gaps found on the way, not changed (they need a guard for a rare state): an orphaned field-battle
encounter is never swept while the soldier is attached; an orphan inside a settlement has no exit; discharge
step 10 can act on a battle encounter in the commander's own settlement. Filed as #1021.

## Root-cause pattern

A comment that states engine ordering is a claim, and a rule built on it inherits its truth. Here the claim
spread to fifteen places and a guard, and only reading the decompiled lines settled it.

## Feedback memories to codify

None; the lesson covers it.
