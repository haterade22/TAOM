# RCA: Fords of War check 12, the warg return distance (2026-11-06)

## Top-line

Check 12 ("Raging warg returns to its formation") failed in round robin run `20261106-054613` (Gondor v Dol Guldur): `not-returned episode=2 s=60.1 distance=208.2 allowed=29.5`. The check measured the rider's distance to `Formation.CurrentPosition` only, and a Fords lane formation strings out along its lane: the lane lines of that minute show the lane average 22 to 314 m from its lead group. The likely cause is a rider that rode with its lead group and read as lost; the log cannot prove it, because it named no position.

The first fix took the minimum of the front-middle distance and the distance to the nearest formation unit. `/deep-review` (tier 1: Data flow and Engine compatibility lenses at max effort, Codex at xhigh) found the findings below on that uncommitted draft. All were fixed before the commit; the shipped rule is: the old front-middle allowance, OR within 15 m of a mate in the formation's arrangement that has no open rage episode, with both distances on the log lines.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | Two riders reset together far from the formation were each other's nearest unit, so both closed as `returned`; a detached unit (task force, machine) could serve as the anchor too (Codex; Engine lens F1) | A proxy that the thing judged can satisfy by itself | The draft asked "is a member near?" without asking which members the judged riders themselves are | `NearestMate` skips every rider with an open episode; mates are arrangement units only. Test `NearestMate_SkipsTheRiderItself_RidersTheSkipNames_AndNaNPositions` |
| 2 | MED | No test ran the new lines: the readers in tests return fixed views, so deleting the self-exclusion would make every rider 0 m away with every test green (Data flow trace 8) | Logic inside the engine reader | The minimum was written inline in the only method that reads agents, which tests replace by design | The search moved into the pure `NearestMate`, which the tests call; the engine reader only copies positions |
| 3 | MED | The log could not show which measure passed a rider, so the next run could not confirm the cause (Data flow trace 10) | A diagnostic that cannot falsify its own premise | The draft changed the verdict without changing what the line records | `returned` and `not-returned` print `distance=` and `nearest=` |
| 4 | LOW | The width and depth allowance, built for a distance to the front-middle, was applied to a nearest-unit distance (Data flow trace 6) | One threshold for two measures | The two distances were folded into one number | Two tests in `HasReturned`: the old allowance for the front-middle, a fixed 15 m for the nearest mate |
| 5 | LOW | A NaN mate position hid a real nearby mate through `Math.Min` (Data flow trace 5) | NaN at a decision gate | `Math.Min` returns NaN, a known trap (csharp-architecture.md, NaN rule) | `if (d < best)` skips NaN; a test feeds a NaN unit |
| 6 | LOW | `docs/features/warg-combat.md` still described `distance=` as the only measure (Data flow trace 9) | Doc drift | The draft updated the Fords doc, not the warg doc that owns the log lines | Updated; the lesson below covers it |
| 7 | LOW | Comment called the point "centre" while every other text says "front-middle" (Data flow trace 11; Engine lens) | Naming | Written from the lane log's word "center", a different point | Comment rewritten |

## Root-cause pattern

Findings 1 to 3 share one cause: the draft fixed the verdict without a test that could make the new rule fail and without a log field that could show the premise wrong. The rule in `docs/reviews/lessons/testing-qa.md` "Each trial verdict has a test that makes it fail" already says this for Marshal's Word; it was not applied to a Fords check edit because the edit sat in the engine reader.

## Why each agent missed nothing here

The findings came from the review of the draft, before any commit, so no earlier agent ran on this code. The UNVERIFIED native timing points from the Engine lens (OnPreTick ordering, removal callbacks off the main thread) apply equally to the pre-change `CurrentPosition` read, which enumerates the same list.

## Not proven

The cause itself stays `[Likely]`: only a Fords run with wargs, now logging `nearest=`, can show a rider passing on the mate rule after it would have failed on the front-middle. No issue: Mike waived issues for Fords of War, 2026-10-31.
