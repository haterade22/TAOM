# RCA: plan 026, seam decision logic (2026-09-24)

## Top-line

Branch `improve/026-seam-decision-logic`, diff `e452b0c7..af6329ea`, reviewed by six `/deep-review`
lenses (standards, engine compatibility, efficiency, completeness, data flow, design) and one Codex
adversarial pass (gpt-6-astra, ultra). The refactor moved nine seams' decisions into their services
and kept behaviour: no lens and no Codex finding showed a reachable behaviour change. Seven findings
were confirmed, none HIGH: one latent fail-open in a split guard, one CI coverage gap, one imprecise
engine claim, two doc gaps, one unpinned contract, and one plan-text defect. The process gap (no
GitHub issue yet) is the orchestrator's at merge. Report:
`docs/reviews/deep-review-026-seam-decision-logic-2026-09-24.md`.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| S1 | LOW | `RefugeService.IsPrisonerAtWarWithRefuge` re-read the refuge's faction per row and answered `false` (release, irreversible) when it was missing; the source captured it once and released nobody without it | Missing null guard (fail direction) | The plan split one captured-once precondition into a start-of-walk count guard and a per-row seam, then judged parity on reachable states only. Its maintenance note even records "if the faction vanished mid-walk the check returns false (release)" and accepts it as unreachable; nobody asked which way the default should fail | Fixed (keep on a missing refuge faction). Lesson in `lessons/gamemodels-services.md` |
| S3 | LOW | The 8 pure `FortificationSearch.NearestDistance` tests lived in `CampServiceTests`, tagged `RequiresGame`, so hosted CI never ran the rule behind three keep-outs | Convention inconsistency (test placement) | The plan put the tests beside the file that declares the rule (`CampService.cs`) and never checked the class's category tag; the green local run hides the CI filter | Fixed (new untagged `FortificationSearchTests`). Lesson in `lessons/testing-qa.md` |
| C1 | LOW | New doc comments said a dropped prison row's captor "is another party"; the engine nulls `PartyBelongedToAsPrisoner` when a hero leaves any prison roster, so it can be none | Other: imprecise engine claim | The comment was written from the old code's branch structure (`== refuge.Party` else), not from where the engine sets the field | Fixed. One-off: the engine-claims check in Agent 2's lens caught it as designed |
| P1 | MEDIUM | No GitHub issue for plan 026 | Other: process | By design: the orchestrator files each sprint plan's issue at merge (Mike's standing request, 2026-09-24) | None: owed at merge |
| P2 | LOW | `supply-lines.md` tests list and `field-camp.md` file row stale | Other: doc drift | The plan scoped feature docs out (plan line 952) because it changed no behaviour; the new shared type and new tests still change what the docs describe | Fixed. One-off |
| P3 | LOW | The winner-only name render (`MobileParty.Name` allocates) was documented in `FindNearestHostile` and `PartyDisplayName` but pinned by no test | Other: unpinned contract | The plan's test list covered each filter and the pick, not the cost contract its own comments state | Fixed with one test. Covered by the existing `testing-qa.md` lesson "each test the plan listed was written; nothing checked what the list left out" (plan 022) |
| X1 | P3 | Three plan RED checks required a `CS0122` the recorded compiler runs do not emit | Other: over-specified verification | The plan predicted the compiler's full diagnostic set instead of naming the diagnostics that prove RED | Plan is a record; not edited. Lesson in `lessons/misc.md` |

## Root-cause pattern

S1 and X1 share one theme: **a plan that prescribes parity at the level of outcomes it can
enumerate misses what it did not enumerate.** For S1 that was the fail direction of a default in
a state the plan called unreachable; for X1 it was the exact compiler output. In both, the plan's
own text documented the gap (the maintenance note for S1, the RED command for X1) without a check
that would flag it.

## Why each agent missed these

The plan author, its two cold reviews and the executor are the implementation side. Between them,
the six lenses and Codex found every item above.

- **Agent 1 (Standards):** found S1 (as a note on ADR-007 condition 2) and S3; C1, P2 and P3 are
  outside its checks (engine claims, docs, contract tests); X1 is plan text, outside its scope.
- **Agent 2 (Engine compatibility):** found C1 by verifying each new engine claim in doc comments;
  S1 needs a comparison with the old code's early return, which its checklist does not ask for.
- **Agent 3 (Efficiency):** reported the costs behind P3 but not the missing test; S1, S3, C1 and
  P2 are outside its lens.
- **Agent 4 (Completeness):** found S1, S3, P1, P2 and P3; missed C1 (an engine fact) and X1 (it
  counted RED logs as evidence without checking them against the plan's exact-output rule).
- **Agent 5 (Data flow):** found S1 (flow 6) and S3; C1 needs the engine's captor reset, which its
  flow 5 trace did not reach.
- **Agent 6 (Design):** reviewed shape, not defects; none of these are design proposals.
- **Codex:** found X1; missed S1 because its "Missing refuge/faction" path stopped at the initial
  count, and S3, C1, P2, P3 because it checked tests for content, not for CI category or docs.

## Feedback memories to codify

None beyond the three lessons: S1 and S3 are systemic for the follow-up plan that will move the
deferred seams, X1 for every improvement plan with an exact-output RED check.
