# RCA — SettlementFood review (2026-06-18)

**Top line:** Deep review (5 agents) returned PASS / 0 findings. Codex adversarial review CONFIRMED all
6 Known Suspects clean (including an independent two-way hand-computation proving no double-count) and
found **1 LOW** — a doc/contract inaccuracy, not a logic bug. Fixed in-session. No HIGH/MED/CRITICAL.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | LOW | `SettlementFoodConfig` summary comment said "Production knobs ADD food", but `TownBaseFood`/`CastleBaseFood`/`VillageFoodMultiplier` are absolute REPLACEMENT values (default = vanilla). `townBaseFood=0` passes `[0,10000]` validation, then the service applies `0 − 15 = −15` (a reduction). Comment vs behavior mismatch. | Convention inconsistency (doc claim vs behavior) | The comment was written with the feature's relief framing and over-generalized to all production knobs; the data-flow agent traced the math correctly but checked "is the delta computed right", not "does the prose claim match the below-vanilla input case". | Fixed the comment + feature-doc to state replacement-vs-additive semantics explicitly (knobs tune both directions; only `flatFoodBonus` is purely additive). Added regression test `ComputeFoodDelta_BelowVanillaTownBaseFood_ProducesNegativeDelta` locking the intentional behavior. |

## Why each deep-review agent missed it

- **Standards / Compat / Efficiency:** out of scope — the finding is neither a standards breach, an API
  mismatch, nor a perf issue. The code is correct; the comment is imprecise.
- **Completeness:** checks that docs *exist*, not that a code comment's directional claim is exhaustive.
- **Data Flow:** traced every knob → consumer and verified the math is a correct delta (it is). It did
  not adversarially feed a *below-vanilla* value and compare the result against the prose contract —
  that's the gap Codex's "config contract" lens caught.

## Root-cause pattern

Not systemic. A single over-generalized comment ("ADD food") on a config type whose fields have *mixed*
semantics — three are absolute replacements (default = vanilla, tunable both ways), one is a pure
additive bonus. The validation (`≥ 0`, finite, divisors `≥ 1`) was already correct; only the prose
implied a "relief-only / ≥ vanilla" floor that the code never enforced (and shouldn't — the divisors
already tune both directions, so a production floor would be inconsistent).

**Lesson (lightweight, not a new rule):** when a config POCO mixes *replacement* knobs (absolute,
default = engine constant) with *additive bonus* knobs, say so per-field; don't summarize them all as
"adds X". The `feedback`-worthy generalization is minor and already covered by the existing
"Config Providers MUST Validate" rule's intent — no new always-load rule warranted.

## Resolution

- `Main/Features/SettlementFood/SettlementFoodConfig.cs` — corrected summary comment.
- `docs/features/settlement-food.md` — added replacement-semantics clarification.
- `TAOM.Tests/Features/SettlementFood/SettlementFoodServiceTests.cs` — +1 regression test (28 total, green).
- No production-logic change (the behavior was correct; the prose was not).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
