# RCA: TaomCombatMechanicsModel split under ADR-002 (#737), deep review 2026-10-05

## Top-line

`/deep-review` ran six lenses in two waves on the uncommitted #737 refactor. Wave 1 was Standards, Engine
compatibility, Data flow and Efficiency; wave 2 was Completeness and Design. XML and Tooling were not in scope (no
XML, no scripts).

The change moved the campaign damage model's glue into three facades (`CombatMechanicsHooks`,
`SignatureStrikeVerdicts`, `RefugeDamageHooks`) and took the model from 286 lines to 141. The review found:
- 0 incompatibilities with the installed engine (v1.5.4; 60 members verified) and 0 behaviour changes against HEAD
  (13 overrides traced; the moved code byte-identical).
- 1 MEDIUM standards defect (F1) and 5 LOW defects and gaps (F2 to F6).
- 0 efficiency issues (checked in the compiled IL: no new allocation, no new struct copy).
- 2 KEEP proposals from the design lens: one applied (it is F1's fix), one behaviour-changing and left as a follow-up.

Every finding was fixed before the commit. One convergence review then checked the fixes: behaviour parity, NaN
polarity and rule 4 came back clean, and its one LOW finding (F7) was fixed too. With F1's fix the model is 129 lines.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | `DecideAgentKnockedDownByBlow` and `DecideAgentKnockedBackByBlow` kept an `if (IsHorseCharge)` routing guard, which gamemodels.md rule 4 forbids in an override body. The file's header excused it as "the parent's accepted idiom", an exception recorded nowhere. | Rule exception asserted in code | The plan kept both guards on the strength of the header comment. Nobody looked for the exception where exceptions are recorded (`.ai/review-reference.md` "Intentional Patterns", an ADR). The sentence predates #737 and passed earlier reviews of this file for the same reason. | Each guard moved into the facade that owns the verdict: `ChargeKnockdown` declines a hit that is no horse charge before it builds a context, `SignatureStrikeVerdicts.Decide` declines a charge before the roster probe. Verdicts are unchanged because both services already declined the other case. RED-first tests for charge, non-charge and a NaN charge velocity. Lesson added to `gamemodels-services.md`. |
| F2 | LOW | The new agent-per-seam source pin skipped two call sites where a wrong argument still compiles: the refuge step's agent origin (one of four on `AttackInformation`) and the shield seam's base result. | Pin chosen narrower than the mistake | The pin's rule was "the seams that take two agents". The mistake it guards is wider: any argument with more than one in-scope candidate of its type. | Two rows added. Lesson added to `testing-qa.md`. |
| F3 | LOW | The facade tests fed only default values (zero damage, empty blow flags, a non-charge collision), so a swapped flag mask, a dropped damage field or a flipped verdict arm would still pass. | Degenerate test oracle | The tests were written to pin the null contracts and stopped at defaults. The verdict path was assumed to need a live agent; a bare `Agent` with weapon slot -1 reaches it. | Non-default values per mapped field (`CollisionDataFixture`), and verdict-routing tests with a bare agent. Same lesson as F2. |
| F4 | LOW | New text in `RefugeDamageHooks` said the origin switch "knows the vanilla party origins only". Vanilla `SimpleAgentOrigin` also carries a `Party` (v1.5.4 `SimpleAgentOrigin.cs:115,127`). | Unverified engine claim, new text | I wrote the sentence from the switch's two arms without listing `IAgentOriginBase`'s implementers in the decompile. | Sentence corrected. Covered by AGENTS.md "Evidence, never invention"; no new rule. |
| F5 | LOW | Comments moved with the code carried wrong or stale engine claims: "per missile spawn" (the engine asks per missile hit, `Mission.MissileHitCallback`), a v1.4.6 line citation, "feeds our ChargeKnockdownContext" (the context has no penetration field), "every decision stays in the services" (two gates live in the facade), and `StrikeContextFactory` naming one verdict caller of two. | Moved text not re-verified | The parity check proved the moved code byte-identical. I read that as proof the move was right, but byte parity says nothing about whether the comments that came with the code were true. | All five corrected against the v1.5.4 decompile. Lesson added to `adapters-taleworlds-api.md`. |
| F6 | LOW | `signature-strikes.md`'s architecture diagram and Tests list missed `SignatureStrikeVerdicts`. The plan listed the diagram line; execution skipped it as "still conceptually right". | Plan item dropped in execution | A planned edit was judged unnecessary mid-way and the deviation went unrecorded. | Both updated. One-off. |
| F7 | LOW | Found by the convergence review of the fixes: two new test descriptions claimed more than their inputs exercised. `IsColliderAgent` stayed false in every hook test (the fixture could not write the private `_isColliderAgent`), the knock-back flag was never set, and the pin comment claimed every call site when it covers the model's calls into the three facades. | Test claim wider than the test | The descriptions were written to the intent; nobody listed the fields each seam maps against what each test feeds. | The fixture writes `_isColliderAgent`, a knock-back blow test was added, both descriptions narrowed to what they check. Same lesson as F2 and F3. |

## Root-cause pattern: text that arrives with the code is not verified by the move

F1, F4 and F5 share one cause. Text that came with the code, or was written straight from it, was treated as checked:
- **F1:** an exception the header claimed;
- **F5:** comments carried verbatim into new files;
- **F4:** a summary written from the switch's arms.

The refactor's own proof (the moved blocks are byte-identical) covered code only, and the review found every one of
these by doing what the move skipped: looking for the recorded exception, and reading the engine callers. F2, F3 and
F7 share a second, smaller cause: each test oracle was chosen narrower than the class of mistake it exists to catch.

## Why each lens missed or caught them

- **Standards** caught F1 by searching the exception catalogues for "accepted idiom" and finding nothing, plus the
  "every decision stays in the services" part of F5.
- **Engine compatibility** caught F4 and three F5 claims by reading every v1.5.4 caller of the members the comments
  describe.
- **Data flow** caught F2's refuge row and the penetration comment in F5 while tracing each value to its service.
- **Completeness** caught F2's shield row, F3 and F6, and showed the queued F1 fix would break an existing test and
  leave five comments stale.
- **Efficiency** found nothing to fix; it confirmed in the IL that the hop adds no allocation or copy.
- **Design** endorsed F1's direction with the constraint that both guards read the same `IsHorseCharge` property, so
  NaN routing stays identical, and raised the follow-up below.
- **The convergence review** caught F7 by checking each test description against the fields its seam maps.

## Follow-ups (not part of #737)

Resolved 2026-10-06 in #741: the first two below (the refuge reduction widened to mounts as well, on the
maintainer's word), plus four source pins: the damage argument of the race-ability and
creature-bandit calls in `ApplyDamageReductions`, the base result `AmplifyDamage` receives, and the agent order of
`RaceAbilityHooks.CrushVerdict`.

- **Refuge victim party through `BattleCombatant`** (design lens, behaviour-changing): would extend the refuge
  reduction to the howdah and mumakil crews and to a `SimpleAgentOrigin` hero; needs its own issue, commit and an
  in-game check. `mumakil.md` does not record the gap yet.
- **"Vanilla never grants KnockBack to a melee swing"** is wrong on v1.5.4 (`SandboxAgentApplyDamageModel.cs:929-942`:
  a crush-through head-to-shoulders hit from a weapon without `CanKnockDown` can earn it, and
  `MissionCombatMechanicsHelper` grants every kick or bash before asking). The claim sits in four places:
  the model's knock-back comment, `signature-strikes.md:37-39`, `combat-mechanics.md:42` and
  `CombatMechanicsModelInvariantsTests.cs:40-41`. Behaviour is unaffected.
- **Perf audit E2 items** (`GetWieldedUsageItem`'s two detoured getters; the crush context built in full for hits the
  service rejects early): pre-existing, moved unchanged.

## Feedback to codify

Three lessons carry the durable rules: `gamemodels-services.md` (F1), `testing-qa.md` (F2, F3, F7) and
`adapters-taleworlds-api.md` (F5). No rule file changes: rule 4 is already binary, and the gap was trusting a claim
that only the excused code made.
