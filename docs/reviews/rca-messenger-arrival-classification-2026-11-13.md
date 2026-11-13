# RCA: messenger arrival classification (plan 050, 2026-11-13)

**Summary:** one confirmed finding, MED, in the tests and the commit text, not in game behaviour. The
refactor commit 0848c47e2 said in its `Not-tested:` trailer that a source pin holds the text of the
boundary reads `IsPlayerAvailable()` and the target party's map event, but the pin asserted neither.
Two breaking edits (`playerAvailable: true`, and `MapEvent == null` in place of `!= null`) left every
test green. The pin now asserts both arguments, and each mutation fails it. The verdict rule itself
matches the old branch sequence for all 256 input combinations.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | The arrival source pin did not assert the `playerAvailable` or `targetPartyInMapEvent` arguments that the commit's `Not-tested:` trailer said it held (`MessengerCampaignBehaviorSourceTests.cs`, before line 153) | Other: claim not backed by a test | The plan's draft trailer (plan 050, lines 493-496) named four boundary reads as pinned, while its pin specification (lines 991-992) named only the party equality and the cast. The executor copied both drafts and did not mutate each named read against the pin | Two assertions, each proven by its own mutation run; lesson in `lessons/testing-qa.md` |

## Pre-existing items found by the review (not defects of this change)

- FOLLOW-UP #1133: a non-wanderer target that is Released or Traveling on arrival is also Lost, although
  the engine returns both states to Active (Lens 2). #1133 names only prisoners and inactive wanderers.
- FOLLOW-UP no issue: filing it is a public action the run does not take; the maintainer decides. While
  the player is a prisoner, the raw party equality is true for a reachable target with no party, so the
  messenger is dropped as Confused instead of waiting (Lens 5 flow 12). Identical at the base; an
  in-game repro is owed.

## Why each agent missed it

- **The plan's author and the executor:** the trailer and the pin were drafted separately in the
  plan and never compared. Standing rule 12 says plan text is a draft to verify; the trailer was not.
- **The orchestrator's spec check:** it checked scope, the build and the suite totals. A green suite
  cannot show that a pin is weaker than the commit says.
- **Agent 2 (Engine compatibility):** its scope is engine API use and engine claims in text; the
  trailer's claim is about TAOM's own tests.
- **Agent 5 (Data flow):** found it, by tracing each boundary argument to the assertion that should
  hold it.
- **Agents 1, 3, 4, 6:** did not run in this tier.

## Root-cause pattern

A recurrence of the #1110 lesson "A source pin on a branch pins its condition and its exit, not only
the order of two calls" (`lessons/testing-qa.md`), whose rule is to name one breaking edit per pin and
see the pin fail. Here the pin was right for what it named; the commit text claimed more. The new
lesson ties the trailer to the assertions.

## Feedback memories to codify

None beyond the lessons entry.
