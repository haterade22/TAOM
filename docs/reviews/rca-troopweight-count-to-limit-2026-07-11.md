# RCA — TroopWeight count→limit rework deep-review (2026-07-11)

**Top line:** The rework (move the "elite tax" off the `NumberOfAllMembers` count getter onto a
`TaomPartySizeModel` size-limit deflation) passed Standards, API-compat, Efficiency, and Completeness
cleanly. The **cross-system Data-Flow agent (Agent 5)** found 3 real gaps + 2 stale-doc drifts — the
review worked exactly as designed (every prior HIGH in this project was a data-flow gap). All 5 findings
were verified against source and fixed in-session; full suite green (4199), no findings deferred.

## Findings

| # | Sev | Bug | Category | Why the implementation had it | Preventive action |
|---|-----|-----|----------|-------------------------------|-------------------|
| 1 | MED-HIGH | Shed-on-upgrade recovered `trueBase = deflated + surplus`; when the penalty was **clamped** (`surplus > base−1`, e.g. a full weight-1 party upgraded to weight-2 — the shed's exact target case), `deflated=1` and the recovery overshoots (`121` vs true `100`), so the shed **under-trims** the heaviest parties. | Lossy-clamp recovery | I reasoned the algebra `deflated + surplus = base` from the UNCLAMPED case and never re-checked it under the clamp branch I had just written 20 lines earlier. | **Cache the pre-clamp original; never reconstruct it from the clamped result.** `ApplyPartySizeWeightPenalty` now stores the pre-penalty base in a `ConditionalWeakTable`; the shed reads `GetTrueBaseSizeLimit`. |
| 2 | MED | SpecialResources battle-reward scaling was rewired from `p.Party.NumberOfAllMembers` to `CalculateWeightedMemberCount` — but the OLD getter's weighting was itself gated on `EnableTroopWeight`, so with the feature OFF rewards now stayed weighted instead of reverting to raw. | Toggle-gate drop | When "preserving" the consumer I replicated the getter's VALUE (weighted) but forgot the getter carried a TOGGLE (the Harmony patch's `EnableTroopWeight` gate) — so I preserved the on-state and silently broke the off-state. | **When replacing a gated Harmony-patched getter with an explicit call, replicate the gate too.** Now `weightOn ? weighted : raw`. |
| 3 | LOW | `TroopWeightXmlLoader` accepted `weight="NaN"`/`"Infinity"` — the `weight <= 0` guard can't catch NaN (all NaN comparisons are false) → poisons the weighted sum → `(int)Ceiling(NaN)`=`int.MinValue` → collapses the party limit to 1 via int-overflow-then-clamp. | Config NaN validation | Pre-existing (loader predates the rework), but the rework's new `weighted→ceil→int→limit.Add` path made a garbage weight collapse the size limit. The mandatory FiniteFloatValidator rule wasn't applied to this category-1 config float. | Loader now uses `FiniteFloatValidator.IsFinite` before the range check + a regression test. |
| 4 | LOW | Stale doc comments in `TownFoodSnapshot`, `TaomSettlementFoodModel`, `TroopShedPlanning`, `TroopWeightService.PlanShed` still described "the (Patch17-)patched `NumberOfAllMembers`". | Doc drift on patch removal | Removed the getter patches without grepping the tree for comments that referenced them. | **When removing a Harmony patch, grep the whole tree for prose that references it.** Comments updated. |

## Root-cause pattern

Findings 1 and 2 share a theme: **when a rework relocates behavior, the derived/secondary consumers that
depended on the OLD mechanism's *shape* (not just its value) silently drift.** #1 depended on the limit
being un-clamped (the shed reconstructed the base); #2 depended on the getter carrying a toggle. In both,
the primary path was correct and the seam consumer was wrong — exactly the class the data-flow agent
exists to catch, and did.

## Why the (non-data-flow) agents structurally couldn't catch #1/#2

- **Standards / Efficiency / Completeness** review each file in isolation. #1 spans `ApplyPartySizeWeightPenalty`
  (service) → the clamp → the shed hook (different file) reconstructing the base; #2 spans the removed Harmony
  patch's gate → the new service call. Neither is visible without tracing the value across files — the
  data-flow agent's whole remit. Agent 5 caught both, plus #3 by tracing the float→int path end to end.
- **API-compat** verified every signature (all correct) — the bugs were in TAOM logic, not engine bindings.

## Lessons codified to LESSONS-LEARNED.md

- "Reconstructing a clamped value from its clamped result is impossible — cache the pre-clamp original."
  (GameModels & Services / State category.)
- "Replacing a gated Harmony-patched getter with an explicit call must replicate the gate, not just the value."
  (Adapters & TaleWorlds API category.)

No new feedback-memory files — both fold into existing LESSONS-LEARNED categories. #3 is a re-hit of the
already-documented FiniteFloatValidator rule (scope gap: the loader predated it), #4 is one-off doc hygiene.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
