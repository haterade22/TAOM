# RCA — Mûmakil Phase 1 deep-review (2026-06-29)

## Top-line

The Mûmakil (Phase 1) is a near-exact clone of the proven War Elephant attack feature (minus the howdah).
A 5-agent `/deep-review` (Standards, API compatibility, Efficiency, Completeness, Data Flow) returned
**zero confirmed bugs in the Mûmakil feature**: 0 ADR violations, 0 v1.4.6 API incompatibilities, 0 per-frame
performance delta vs the elephant, 9/9 cross-system data-flow traces CONNECTED (all id-strings + clip names +
action-set/usage reuse match exactly), all modified XML parses clean. Verdict: **READY**.

Two non-code items are expected-pending (feature doc, GitHub issue, CHANGELOG — authored after the in-game test).
One systemic test lesson and one out-of-scope pre-existing bug were surfaced; both are recorded below.

## Findings

| # | Sev | Finding | Category | In Mûmakil scope? | Why missed / status |
|---|-----|---------|----------|-------------------|---------------------|
| 1 | LOW | A weighted-recruitment-pool change silently broke a test that stubbed `_random.Next(11)` (pool total became 12 → stub miss → NSubstitute default 0 → first troop). | Testing & QA | Yes (caught + fixed during impl, not by the review agents) | Fixed in-session: updated `GetVolunteerTroopId_ClanAserai1_*` to `Next(12)` + added a Mûmakil-bucket test. Proven via `git stash` that it was *this* change, not pre-existing. |
| 2 | MED | Committed debug weight `("taom_spider_creature", 40)` (`VolunteerRecruitmentService.cs:617` + `:656`, comment `// TEMP-SPIDER-TEST … REVERT before commit`) — bumps Dol Guldur recruitment of the giant spider to ~69% and leaves 9 Dol Guldur `*_MaxRoll_*` tests red on the branch. | Testing & QA / Campaign | **No** — pre-existing, committed before this session; surfaced by the Data Flow agent | Out of scope for Mûmakil; reverting `40 → 1` fixes both the gameplay bug and the 9 red tests. Surfaced to project owner. |
| 3 | LOW | `MumakilConfig.TrampleTriggerRange=3f` / `TrampleRadius=4f` are the elephant's 1× values; the Mûmakil ships at 3× body (`body_length=300`), so the trample reach is small relative to the beast. | Tuning | Yes (deliberate) | Documented design choice (Phase-1 elephant parity; `MumakilConfig` comments say tune for the larger footprint later). Recommend scaling the radius/trigger ~3× for feel — Phase-2 tuning, not a bug. |
| 4 | INFO | Shared `act_elephant_attack_*` clips via `as_elephant` — a future LOTRLOME action rename would break the Mûmakil (and the elephant). | Animation | Yes | Already guarded: `MumakilAttackActions.AnyUnresolved()` logs at mission start. No action needed. |

## Root-cause pattern — weighted-pool ↔ test coupling

Findings #1 and #2 are the same mechanism: `PickWeighted` rolls `_random.Next(totalWeight)`; tests stub a fixed
`Next(N)` for the pool's current total. Any pool edit changes N, the stub misses, and the pick returns the first
troop. #1 was my own edit (caught + fixed). #2 is a committed `TEMP-` weight that shipped against its own
"REVERT before commit" note, leaving a red baseline that masks regressions. The durable rule is codified in
`docs/reviews/LESSONS-LEARNED.md` → Testing & QA → "Changing a weighted recruitment pool breaks tests that stub
`_random.Next(<hardcoded total>)`".

## Why each deep-review agent's result was correct

- **Standards / API / Efficiency:** correctly PASS — the Mûmakil introduces no new pattern, API, or hot-path cost
  vs the elephant (a faithful clone). Nothing to miss.
- **Completeness:** correctly flagged feature-doc / issue / CHANGELOG as pending — these are post-test deliverables,
  not defects.
- **Data Flow:** the highest-value agent — traced all 9 id/clip/action-set flows (0 gaps) AND caught the
  out-of-scope committed spider-weight (#2) that the per-file agents would not have connected to the red test suite.
- Finding #1 was caught during implementation (the full-suite run went 9→10 failures), not by an agent — the agents
  reviewed the *fixed* state, so they correctly saw green Harad tests.

## Preventive actions

1. **Codified** the weighted-pool test-coupling lesson in `LESSONS-LEARNED.md` (Testing & QA).
2. **Grep `TEMP-` before any recruitment commit** — added to the lesson's Prevent line.
3. **Red-baseline discipline:** when tests fail, prove the change didn't alter the failure set (stash + diff)
   before labelling them "pre-existing" — done here for the 9 Dol Guldur failures.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
