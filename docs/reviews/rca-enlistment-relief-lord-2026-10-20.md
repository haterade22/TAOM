# RCA: Enlistment joins a lord who relieves the commander's raid (#954, deep-review, 2026-10-20)

## Top-line

`/deep-review` (six lenses in two waves, then one convergence pass; no XML in scope) on #954 found
**no API incompatibility and no code defect**. The change is one loop in `VillageRaidReader.IsRaiding`
that copies vanilla's lord-defender test, and every #831 site inherits it. The review found **one
MEDIUM test gap** and a set of LOW findings, all fixed before the first commit, and named two open risks
that need an in-game probe.

The finding that mattered: the only test of the new loop was a source-text pin that read comments as
well as code and did not name `IsMobile`. Every raid's defender side holds the village's own settlement
party, which has no `MobileParty`. Without the `IsMobile` guard the read throws, the reader's fail-open
catch returns false, and every raid is joined again: #831 silently undone with the whole suite green.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Completeness, Standards, Data flow, Design) | The defender loop had no behavioural test; the source pin matched the summary comment and omitted `IsMobile`; removing the guard or swapping the returns stayed green. | A fail-open read hides its own defects | The plan accepted "engine types cannot be built in a unit test" without checking: the repo already stages bare `MapEvent` objects (`FiefGrantingBehaviorCaptureGateTests`, `RefugeCampaignBehaviorTests.BattleOf`). | A `RequiresGame` reader test over six staged shapes, proven RED with the guard removed, plus a comment-stripped source pin (`RepoPaths.ReadSource(stripComments: true)`) that forbids any lambda. Lesson appended to `lessons/testing-qa.md`. |
| F2 | LOW (Data flow, Design) | One-frame timing: a join made from `TickEvent` or the hourly tick runs before `MapEventManager.Tick` in the same frame (`Campaign.cs:980-982`), and raids update even as the player's own event (`MapEventManager.cs:59`). | Engine tick order | The join path assumed the player's event is frozen, which holds for every event type except a raid. | Recorded as open risk 1 with a top-speed in-game probe; a guard, if needed, goes where both join sources arrive (`EnlistmentBattleBehavior.OnBattleJoinRequested`). |
| F3 | LOW (Engine compat, convergence) | In a war that began after the oath, Attack applies the hostile action against the village: relation, crime, trait, and the clan's own war, which discharge does not unwind. | Cross-feature interaction | The village is the defender leader of a relieved raid; the war mirror runs only at the oath. | Recorded as open risk 2; wars during service are A6 (#832). |
| F4 | LOW (several) | Duplicate test rows and a compile-bound binding test; a test name off the house pattern; flag contract comments describing only #831; three "reachable only" sentences; R1/R2 labels clashing with `EncounterOwnershipPolicy`; in-game checks without expected results; a comment that named the wrong cause in `EncounterAdapter`; stale doc lines. | Test and doc accuracy | Written before the engine trace was complete. | All fixed. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources. It confirmed that the
staged objects match every member the reader reads, that no shape passes for the wrong reason (the two
"false" shapes fail if the loop or the side check is removed), and that every engine line reference in
the doc holds. It found nine LOW items, all fixed, including the understated hostile-action risk (F3)
and a LINQ pin that would not have caught the engine's own allocating LinQuick form.

## Root-cause pattern

A fail-open catch turns every exception into the "safe" answer. That is right in production and wrong
in tests: a staging bug or a missing guard returns the same value as a correct read. The implementer hit
this too: a test staging bug threw inside the reader and showed as "false" until a shape that expects
true failed.

## Why each lens caught or missed these

- **Standards:** F1 (the comment-reading pin, the house helper), the binding test, the test name.
- **Engine compatibility:** every member verified; F3 first raised as relation and crime only.
- **Efficiency:** no findings; the loop allocates nothing.
- **Completeness:** F1, the duplicate rows, the contract comments and the doc gaps.
- **Data flow:** F1, F2, and every lifecycle path connected.
- **Design:** confirmed the shape, supported the staged test, placed any future F2 guard.

## Feedback memories to codify

None; the lesson covers the pattern.
