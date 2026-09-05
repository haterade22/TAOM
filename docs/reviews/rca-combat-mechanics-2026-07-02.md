# RCA — CombatMechanics deep review (2026-07-02)

Six-agent deep review (standards, 1.4.6 compat, efficiency, completeness, data-flow, spec-conformance) of the CombatMechanics feature (issue #320). Standards/compat/spec-math all PASS; 8 findings total, all verified against source and fixed in-session (107 CombatMechanics tests green after fixes, +5 regression pins). The build pattern for this feature was novel — five parallel builder agents against frozen contracts — and three of the findings trace directly to seams between builders, which is the systemic lesson here.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `CreatureCombatService.NormalizeMonsterId` allocated a `Substring` per settlement-variant hit on the per-hit path | Hot-path allocation | The orchestrator's builder brief PRESCRIBED the runtime-strip helper; the sibling CrushThrough builder independently invented the allocation-free construction-time expansion. Both builders satisfied their briefs — the brief itself embedded the allocation | Hot-path briefs must specify the allocation-free pattern (precompute/expand at construction), and parallel-builder outputs need a cross-consistency pass (the efficiency + data-flow agents caught it) |
| 2 | HIGH (process) | No GitHub issue existed at review time | Process | Plan sequencing put issue creation in close-out; CLAUDE.md requires it BEFORE implementation | Issue #320 created. Open the issue when the plan is approved, not at close-out |
| 3 | LOW→fixed | `ShouldForceSliceThrough` guard `momentumRemaining <= 0f` lets NaN pass (all NaN comparisons are false) → NaN momentum could force SlicedThrough chains | NaN gate (4th shipping-adjacent instance: career #31, EditorCacheRebuild #38, CS_Road 2026-05-13) | The NaN rule lives in "Config Providers MUST Validate" — scoped to CONFIG floats, which were all FiniteFloatValidator-covered. This NaN arrives from an ENGINE input at a runtime decision gate — a scope gap the config rule never covered | Rule generalized (LESSONS-LEARNED): decision gates on engine-sourced floats are written as POSITIVE requirements (`> 0f`), never inverted early-exits (`<= 0f`); NaN must fail the gate. Regression tests added |
| 4 | LOW→fixed | `ChargeKnockdownService` with NaN velocity/resistance emitted an OWNED `false` verdict instead of deferring to vanilla | NaN gate (same class as #3) | Same scope gap | Same rule; explicit `float.IsNaN → null` fall-through + tests |
| 5 | LOW→fixed | `GetHorseChargePenetration()` returned the tuned config value even with the master/mechanic toggle OFF — the only read bypassing the "master off = pre-feature behavior" invariant | Master-toggle coverage | The "single source for the constant" design goal made the unconditional read feel correct; the master-off invariant was never enumerated per-override | For any feature promising "toggle off = vanilla", enumerate EVERY override in the model and verify each one folds the toggle (the data-flow agent's MCM-coverage rule does this — keep it rigid) |
| 6 | LOW→fixed | MCM slider `[2,30]` bypassed the JSON ordering invariant `auto ≥ neutral` — slider 2-5 would auto-floor ordinary horse charges | Dual-surface validation divergence | JSON invariant authored by the config-provider builder; MCM clamp authored separately by the orchestrator; nobody owned the cross-surface consistency | When a value is settable from both JSON and MCM, the invariant must be enforced at BOTH surfaces or centralized — added to LESSONS-LEARNED |
| 7 | LOW→fixed | `WeaponClass.ToString()` allocation per missile/shield hit | Hot-path allocation (minor) | Known at authoring time, judged acceptable; review disagreed cheaply | Static enum-name cache in the model |
| 8 | LOW→fixed | `GetKnockDownResistance(victimAgent)` extraction lacked the null-guard its sibling extractors use (unreachable per engine contract) | Consistency nit | Deliberate (engine contract), but inconsistent with the surrounding idiom | Guarded with neutral 1f + contract comment |

## Root-cause pattern

**Parallel-builder seams.** Findings 1, 3, 4, 6 all live at boundaries between independently-authored components: two builders solving the same sub-problem differently (1), a rule applied rigorously in one layer (config NaN validation) but absent in the sibling layer nobody briefed (3, 4), and two entry points for one value validated by different authors (6). The per-component work was uniformly good — every builder passed its own brief. The residual risk concentrated exactly where no single author could see both sides. The 6-agent review's cross-cutting passes (data-flow, spec-conformance-with-NaN-audit, efficiency) were the correct countermeasure and caught all of them; the NaN-polarity audit line in the spec-conformance prompt paid for the whole review.

## Why each agent missed / caught these

- **Standards (PASS):** correctly scoped — none of the findings are ADR violations.
- **Compat:** caught #8; its per-API verification scope doesn't cover allocation or toggle semantics.
- **Efficiency:** caught #1 and #7 — its per-hit allocation rules fired exactly as designed.
- **Completeness:** caught #2 (issue missing); its checklist doesn't inspect formula semantics.
- **Data-flow:** caught #5 and #6 — the rigid "enumerate ALL MCM properties + verify the gated behavior matches the hint promise" rule (added after the CrashReport 2026-05-25 miss) worked.
- **Spec-conformance (added beyond the core 5):** caught #3 and #4 via the explicit "NaN polarity on every gate" audit criterion. The core 5 prompts do NOT ask this question — see preventive action below.

## Codex adversarial pass (Phase 2, same day)

Codex (gpt-5.5, xhigh) reviewed the post-fix changeset with six seeded Known Suspects: **VERDICT CLEAN — 0 P1, 0 P2, 2 P3 observations, no confirmed bugs.** All six suspects were disproved as bugs with decompile evidence (monster-vs-shield fall-through is spec-conformant; shield blocks DO carry damage into `InflictedDamage` via `ComputeBlowDamageOnShield` so cleave chains through damaging blocks; `BasicCharacterObject.Race` and `IRaceManager` share the FaceGen id space; `ChargeDamageCallback` sets `BlowFlags.KnockBack` on the same `Blow` instance before the knockdown call; the dwarf stagger multiplier applies exactly once; MCM-over-JSON semantics match the AlignmentDesertion precedent). Its independent scenario arithmetic reproduced the calibration: horse-vs-man Branch B == vanilla verdict, dwarf threshold 118 vs damage 50 → no knockdown, mûmakil ratio ~125 → Branch A. The two P3s were closed as: (1) monster-id lists not resolvability-validated — documented known limitation (typo = inert, adapter cost fails the simplicity criterion); (2) cleave MCM hint overpromised the zero-shield-damage edge — hint reworded. Review: `codex-adversarial-combat-mechanics-2026-07-02.md`.

## Preventive actions — ALL MECHANIZED (2026-07-02, same session)

Every preventive action below is now enforced by a loaded rule or review prompt, not just recorded here:

1. **DONE — LESSONS-LEARNED (Testing & QA):** "Write engine-float decision gates as positive requirements — NaN must FAIL the gate" appended (4th instance of the NaN-gate class; the config-side `FiniteFloatValidator` rule does not protect runtime engine inputs).
2. **DONE — LESSONS-LEARNED (Build, Tooling & Workflow):** "Parallel-builder briefs: shared sub-problems get ONE prescribed solution in the contract" appended.
3. **DONE — `.claude/rules/csharp-architecture.md`:** new always-loaded-for-C# section "Engine-Float Decision Gates: NaN Must FAIL the Gate" (runtime sibling of "Config Providers MUST Validate", with the ❌/✅ polarity patterns and the scope-widening meta-rule: each of the 4 recurrences happened because the rule's scope was one category narrower than the bug); plus new point 7 in the config rule — dual-surface (JSON + MCM) values enforce the same invariants at both surfaces or centralize the clamp.
4. **DONE — `/deep-review` skill (Agent 5):** new rule **4b "Engine-Float Gate NaN Polarity"** (mandatory for every decision gate on an engine-sourced float — inverted early-exits flagged; `bool?` services must return null on non-finite input), and the MCM toggle-coverage rule 2b gained the **master-toggle fold check** (enumerate EVERY override incl. constant-returning getters and confirm each folds the master when the hint promises "off = vanilla").
5. **DONE — `.claude/rules/harness-facts.md`:** new section "Parallel builder briefs: shared sub-problems get ONE prescribed solution" with the pre-dispatch checklist (list ≥2-brief sub-problems → pin one solution → cross-consistency review over the seams); **CLAUDE.md "Briefing subagents"** gained item 6 pointing at it.
6. **DONE — process (finding 2):** issue #320 created; the existing LESSONS-LEARNED rule ("Open the GitHub issue when STARTING the work") already covered this and the deep-review completeness gate caught the violation before commit — the gate worked as designed; plan authoring must place issue creation in step 1, not close-out.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
