# RCA: Enlisted vassal sits out battles his kingdom is not at war in (#960, partial deep-review, 2026-10-21)

## Top-line

`/deep-review` on #960 was **partial**. Lenses 1 (standards), 5 (data flow) and 6 (design) reported.
Lens 2 (engine compatibility) stopped on the account's weekly Opus limit before it reported, and lenses 3
(efficiency), 4 (completeness) and the convergence pass did not run. The maintainer accepted the partial
review on 2026-10-21. The design lens did verify the load-bearing engine fact on v1.5.5: Attack and Send
troops declare war on `battle.GetLeaderParty(MainParty.OpponentSide).MapFaction` (`MenuHelper.cs:267-269`,
`:571-572`, `BeHostileAction.cs:192-195`), the same party the sit-out reads.

No code defect was found. All findings were LOW and are fixed or recorded.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | LOW (Standards, Data flow, Design) | Every sit-out first logged "war top-up refused ...; joining anyway", then "sitting this battle out", on each hourly retry. | A test stub the production code cannot produce | The fixture stubs `MirrorWarAtJoin` to true, but for a vassal the real service returns false in exactly the sit-out case. | The sit-out runs before the top-up; the sit-out tests stub the top-up false and assert it is never called. Lesson appended to `lessons/testing-qa.md`. |
| F2 | LOW (Standards, Design) | Two conditions in `ShouldSitOutAsVassal` could never change the result (the commander id, and `MayMirrorMidTerm` once the player has a kingdom). | Dead guard | Copied from `MirrorWarAtJoin`, which needs them. | Deleted. |
| F3 | LOW (Data flow) | A stuck battle the soldier sits out is neither swept by the map-event guard nor joined; a soldier with no kingdom whose oath identity is not his clan gets neither rule. | Documentation | Edge cases outside the issue. | Both named in the docs. |

Recorded for later, not part of #960: the status board reads "engaged" while the soldier sits a battle
out, and a siege his commander takes still counts as his for career and momentum credit (pre-existing).

## Feedback memories to codify

None.
