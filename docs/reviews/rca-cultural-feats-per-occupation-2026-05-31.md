# RCA — Cultural Feats Per-Occupation Town Notable Counts (Codex Review)

**Date:** 2026-05-31
**Feature:** `Main/Features/CulturalFeats/*` per-occupation town notable-count refactor (follow-up to commit `582275f`)
**Review pipeline:** `/verify` → `/deep-review` (5 agents, all PASS) → `/review-codex` → fix → final `/verify`
**Codex output:** [`codex-adversarial-cultural-feats-per-occupation-2026-05-31.md`](raw/codex-adversarial-cultural-feats-per-occupation-2026-05-31.md)
**Codex prompt:** [`codex-adversarial-cultural-feats-per-occupation-2026-05-31.prompt.md`](codex-adversarial-cultural-feats-per-occupation-2026-05-31.prompt.md)

## Top-line summary

Codex returned **0 CRITICAL, 1 HIGH, 1 MEDIUM** against a refactor that all 5 `/deep-review` agents passed. The HIGH was a missing dispatch test for one of the nine new feats (Dol Guldur Artisan); the MEDIUM was an architectural observation that vanilla template selection samples with replacement, so a pool equal to the target leaves expected `~0.632 × N` distinct archetypes per settlement.

The HIGH was fixed in-session (test added). The MEDIUM is documented as a known characteristic — the user explicitly chose 14/15 GL templates for AI-recruitment density (parity with Rohan), not encyclopedia name diversity, and the simplest acceptable resolution is to document the trade-off and let the user request headroom after in-game testing if duplicate names are visually obvious.

## Findings table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | `ApplyNotableCountFeat_DolGuldurArtisan_AddsOne` test absent; service branch at `CulturalFeatsService.cs:295-296` had no direct dispatch test. The feat WAS declared, registered, XML-bound, and in the reflection-init table — only the dispatch test was missing. | Skip-guard exhaustion / ADR-008 service coverage gap | The deep-review Completeness agent counted 86 test methods and 13 `ApplyNotableCountFeat_*` tests and called the coverage complete. None of the 5 agents enumerated the per-(culture, occupation) HasFeat branches in `CulturalFeatsService.ApplyNotableCountFeat` and cross-referenced each branch against a dispatch test. The "Skip-Guard Exhaustion" rule (`.claude/rules/tests.md`) requires one test per guard clause — extending the same discipline to dispatch arms in a switch would have caught this. | Added the missing test. Codified the per-branch-test rule in the AGENTS.md "Bugs Codex typically misses" section so Codex spots it on the next per-occupation-style review. Also added a feedback memory entry `feedback_per_branch_dispatch_test_enumeration.md`. |
| 2 | MEDIUM | At target = pool size (Isengard 14/14 GL, Dol Guldur 15/15 GL), vanilla `DefaultHeroCreationModel.GetRandomTemplateByOccupation` samples with replacement → expected `~9` distinct archetypes per settlement, with ~5 duplicate names/portraits. | Vanilla characteristic / design trade-off, not a code bug | Implementation assumed pool size = target meant "one template per slot, each used once." Vanilla's selector does `.Where(...).ToList()` then weighted-random picks without removing the chosen template; the implementation didn't decompile this selector during design. | Documented in `docs/features/cultural-feats.md` "Known characteristic — duplicate archetype selection at target = pool size" with the expected-distinct math (`N × (1 − (1 − 1/N)^N) ≈ 0.632 N`). The user can add headroom or patch the selector after in-game observation. Per `simplicity-criterion.md`: tiny cosmetic gain + significant authoring/patching cost = reject as a default fix. |

## Root-cause pattern — Per-branch test coverage doesn't follow from "lots of tests"

A clean test count is not the same as a complete branch matrix. The Codex finding is structurally identical to the "Skip-Guard Exhaustion" rule, applied to switch/dispatch arms instead of guard clauses:

> For any method that iterates entities and conditionally skips: list every possible entity state ... write one test per skip condition.

Same pattern, dispatch flavor: for any service method that dispatches by enum + culture, list every (enum, culture) cell with a non-trivial branch — write one test per branch. The dispatch test must exercise the specific (culture, occupation) combination, not just "Dol Guldur" or "Artisan" in isolation.

The deep-review Completeness agent's prompt asked for "(a) each occupation dispatching to its correct feat" — which it answered with "yes, tests exist for each occupation type" — but missed the implicit (culture × occupation) cell-by-cell requirement. The agent's coverage check was *occupation-coverage* and *culture-coverage* in isolation, not the cross-product.

## Why each agent missed these

| Agent | What it checked | Why it missed Finding #1 |
|---|---|---|
| Standards (Haiku) | ADR violations, line counts, GameModel thinness | Not in scope — tests aren't ADR-governed. |
| API Compatibility (Sonnet) | v1.4.5 API surface, signatures | Not in scope — pure C# concern. |
| Efficiency (Haiku) | Allocations, hot paths, LINQ | Not in scope — test additions don't change runtime. |
| Completeness (Haiku) | Tests exist, feature doc, IoC, CHANGELOG | **Closest agent.** Counted 86 test methods and confirmed every occupation had a test — but didn't cross-reference each (culture × occupation) HasFeat branch in `CulturalFeatsService.ApplyNotableCountFeat` against a matching dispatch test. The Completeness prompt asked "(a) each occupation dispatching to its correct feat" — which is true at the occupation-axis level but doesn't enumerate culture × occupation cells. |
| Data Flow (Sonnet) | XML→C# trace, two-layer registration, enum coverage, no dead code | **Second-closest.** Walked all 9 feat chains end-to-end and confirmed each is "CONNECTED," but the trace ended at "consumed by the matching switch arm in CulturalFeatsService" — it did not verify that the matching switch arm has a dedicated dispatch test. The trace asked "is the field populated?" not "is the field tested?" |

Finding #2 (vanilla sample-with-replacement) was missed by every agent because **no agent decompiled `GetRandomTemplateByOccupation`.** The API Compatibility agent verified `DefaultNotableSpawnModel.GetTargetNotableCountForSettlement` (the entry point) but didn't trace the downstream template selection. The Data Flow agent verified pool ≥ target but didn't ask "how does vanilla pick from the pool?"

## Preventive actions

### 1. Per-branch dispatch test enumeration (memory codification)

Add `feedback_per_branch_dispatch_test_enumeration.md`:

> When a service method dispatches by switching on a value (enum, culture id, kingdom id), list every concrete branch in the dispatcher BEFORE writing tests. For each branch that calls a HasFeat / TryGet / lookup, write one dispatch test that exercises that specific (input, value) pair. The Completeness agent's "tests exist for each enum value" question is necessary but not sufficient when dispatch is a cross-product (culture × occupation).
>
> **Why:** Codex review 2026-05-31, cultural-feats per-occupation refactor — 13 dispatch tests passed Completeness review with 9 service-branch HasFeat checks. One cell (Dol Guldur × Artisan) was missing a test. Service branch was reachable, feat was registered, but a typo or future refactor in the dispatcher would have flipped silently.

### 2. Extend Completeness agent prompt to cross-product check

Update `.claude/skills/deep-review/SKILL.md` Agent 4 (Completeness) prompt to add:

> When a service method dispatches by switch on a value, list every concrete (input, branch-value) pair in the dispatcher and check that each pair has a dedicated test. For per-(culture, occupation) dispatchers specifically, enumerate the (culture, occupation) cells with a non-trivial branch and verify each cell has its own test. "Tests exist for each occupation" and "tests exist for each culture" in isolation is NOT the same as "tests exist for each (culture, occupation) cell."

(Deferred to a follow-up commit per `simplicity-criterion.md` "edit scope discipline" — the deep-review prompt edit belongs in a separate harness-tuning commit, not bundled with the feature fix.)

### 3. Document vanilla template-selection math

Already done in `docs/features/cultural-feats.md` (the "Known characteristic" paragraph added in this fix). Future feature authors who set pool size = target now get a heads-up about expected distinct archetypes without needing to decompile `GetRandomTemplateByOccupation` themselves.

## Verification

```
dotnet build TAOM.Tests --p:DisableModuleCopy=true ... -> 0 Errors
dotnet test  TAOM.Tests --filter CulturalFeats     -> 192 / 0 / 0  (was 191 before; +1 confirms new test ran)
dotnet test  TAOM.Tests (full)                     -> 2772 / 0 / 2  (was 2771 before)
python tools/validate_moduledata.py                -> CLEAN
```

## Files changed in this RCA fix

- `TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs` — added `ApplyNotableCountFeat_DolGuldurArtisan_AddsOne` between `_DolGuldurGangLeader_AddsThirteen` and `_MordorGangLeader_AddsTwo`.
- `docs/features/cultural-feats.md` — added "Known characteristic — duplicate archetype selection at target = pool size" paragraph.
- `docs/reviews/rca-cultural-feats-per-occupation-2026-05-31.md` — this file.
- `docs/reviews/codex-adversarial-cultural-feats-per-occupation-2026-05-31.{prompt.md, md}` — Codex prompt + raw output.
- `AGENTS.md` — Codex feedback loop update (Lessons From Prior Reviews).
- `docs/reviews/REVIEW-LOG.md` — review entry.
- `CHANGELOG.md` — fix entry (refactor entry already present at top from prior step).

## Linked prior RCAs / memory

- [`docs/reviews/rca-cultural-feats-3pack-2026-05-31.md`](rca-cultural-feats-3pack-2026-05-31.md) — yesterday's RCA on commit `582275f` that codified the two-layer NPC registration rule (preceding context for this refactor).
- `feedback_notable_template_two_layer_registration` — applied successfully in this refactor (17 new GLs all two-layer-registered, verified by Codex).
- `feedback_audit_findings_not_always_correct` — applied to MEDIUM (Codex's math is correct but the "fix" is a design trade-off, not a code bug; documenting is the correct response, not patching vanilla).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
