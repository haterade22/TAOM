# RCA: career-enum-prefab-cleanup review findings (2026-07-06)

**Changeset:** PassiveEffectType member deletion (15) + rename (10) + regroup; 14-file rename
sweep incl. 240 `type=` attributes in `taom_career_choices.xml`; unknown-type load warning in
`CareerConfigProvider.ParseChoice` (+ test); `CareerScreen.xml` VisualDefinitions rename/retune +
pane widths; `CareerChoiceGroupObjectVM` LINQ rewrite; comment/hint-text cleanup.

**Review:** 5-agent deep review + Codex pre-review. **0 HIGH / 0 P1 / 0 P2.** 2 Codex P3 +
2 LOW (docs) confirmed and fixed same session; 2 pre-existing items recorded as follow-ups;
1 cosmetic accepted. Suite green after fixes: 4121 passed / 0 failed / 2 skipped.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | P3 | Old vocabulary survived in 10 test METHOD NAMES (`…HeroWithHorseChargeDamagePassive…`, `…CustomResourceGain_ScalesEarning`, etc.) across 2 test files | rename hygiene | The sweep used `\b`-bounded regex deliberately (protects engine-owned identifiers — e.g. the `isShruggedOff` engine parameter); compound identifiers have no boundary at the seam. No follow-up substring pass ran. The internal completeness agent re-checked with the same whole-word assumption and passed it; Codex's substring grep caught it — reviewer-methodology diversity was the signal. | Rename sweeps end with a second SUBSTRING grep over `TAOM.Tests/` + `docs/`, triaging each hit. LESSONS-LEARNED entry added. **Fixed:** substring sweep of both test files. |
| 2 | P3 | Retired enum name `WindsOfMagic` used as the negative-parse test exemplar | test hygiene | Exemplar was picked for realism when repointing the test; the deletion pass didn't treat test string literals as in-scope. | Negative "unknown value" tests use synthetic tokens, never retired real names. **Fixed:** exemplar → `NoSuchEffectType`. |
| 3 | LOW | Stale enum names in `docs/features/battle-balance.md` (3×) + `special-resources.md` (3×) | docs drift | Sweep file-set was code + ModuleData + tools; `docs/` excluded. | Same substring-pass rule as #1 covers docs. **Fixed:** both docs updated. |
| 4 | LOW (pre-existing) | `TaomCombatMechanicsModel.cs` is 197 lines vs the ADR-002 150-line entry-point ceiling (override bodies are thin; length is primitive-extractor helpers) | standards | Pre-dates this changeset (today's diff removed 2 comment lines); shipped with the feature's own review. | Follow-up decision: extract the context builders to a boundary helper, or record a documented exception. Not fixed here (out of changeset scope). |
| 5 | LOW (pre-existing) | 5 consumed passive types have zero XML pip users (`ShrugOff`, `CompanionLimit`, `InventoryCapacity`, `SmithingCostReduction`, `SpecialResourceUpkeepModifier`) | content gap | Pre-existing authoring gap, verified not a regression (the 240-attr rename diff reconciles exactly). | Follow-up authoring pass when career pips are next balanced. |
| 6 | Cosmetic (accepted) | An unrecognized `type=` value now logs TWO warnings (new unknown-type + pre-existing phantom gate, since the `Special` fallback is unconsumed) | logging | By construction of the new gate. | Accepted — both warnings are truthful; deduplicating would add code for no behavioral win (simplicity criterion). |

## Root-cause pattern

Findings 1–3 share one cause: **a deliberately conservative whole-word rename sweep was declared
complete without a second, looser pass over the non-production surfaces** (test identifiers, docs).
The `\b` boundary was the right call for production safety; the miss was treating one grep
methodology as sufficient evidence of completeness.

## Why each agent missed the P3s

- **Standards / Efficiency / API-compat:** scoped to production semantics — test method names and
  docs are outside their rule sets (correctly).
- **Completeness:** ran the old-name check but with the same whole-word matching assumption the
  sweep used, so compound names passed. Its prompt now needs the substring instruction (folded
  into the LESSONS-LEARNED rule rather than a prompt edit — the rule fires at sweep time, before
  review).
- **Data Flow:** traced runtime flows (XML→enum→consumer), where everything was genuinely clean.
- **Codex:** independent methodology (substring grep) — caught both. The disagreement between the
  internal completeness pass and Codex was the valuable signal, per the deep-review skill's
  disagreement rule.

## Feedback memories to codify

One systemic rule → `docs/reviews/LESSONS-LEARNED.md` "Build, Tooling & Workflow":
*After a whole-word identifier rename sweep, run a substring sweep over tests and docs.*

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
