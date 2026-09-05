# RCA — Shader Pre-compile Phase 0 crash-mitigation review (2026-06-25)

**Scope:** Phase 0 of the native shader-compile guard (#287) — MCM toggles + `DefaultScenes` fallback-drift sync + post-crash capture guidance. 6 files. Reviewed by 5 deep-review agents (standards, compat, efficiency, completeness, data-flow) + 1 Codex adversarial pass (`docs/reviews/codex-adversarial-shader-precompile-phase0-2026-06-25.md`).

**Outcome:** Codex verdict **SHIP** (0 CRITICAL / 0 HIGH / 0 MED / 1 LOW). Deep-review: 0 HIGH, 1 disputed standards finding, 2 LOW data-flow. Total confirmed findings fixed: **3 LOW**. No HIGH/MED in any pass. The feature's logic (scene-pass gate, fallback sync, crash-guard lifecycle) traced clean by both the deep-review data-flow agent and Codex with zero gaps.

## Findings table

| # | Sev | Bug | Category | Found by | Why missed | Preventive action |
|---|-----|-----|----------|----------|-----------|-------------------|
| 1 | LOW | Master-toggle HintText promised "no pre-compilation runs," but `EnableShaderPrecompilation` only hides the menu option / blocks new `Begin()` — a walk already running is not aborted (the per-frame `OnApplicationTick` keeps ticking it). | User-facing-promise mismatch | Deep-review Agent 5 (rule 2b) | Wrote the HintText from intended behavior without tracing that the toggle gates only `isHidden` (menu visibility), not the in-flight walk. | Tightened HintText to "a walk already in progress finishes — it is not aborted mid-flight." The existing Agent-5 rule 2b (MCM user-facing-promise) fired correctly; no new rule. |
| 2 | LOW | Crash-capture toast said "is skipping N scene(s) that crashed your GPU before" — misleading when scene passes are toggled OFF (those scenes wouldn't load anyway). | UX wording across gate states | Deep-review Agent 5 (toggle-state coherence) | Wrote the toast for the scene-passes-ON case without auditing the wording in the newly-added OFF state. | Reworded to "N scene(s) crashed your GPU on a previous shader pre-compile" — accurate in both states. |
| 3 | LOW | `docs/features/shader-precompilation.md:135` Tests bullet still named the old test `default-scenes-includes-crash-scene` after it was replaced by `DefaultScenes_ExcludesDisabledCrashScenes` + `DefaultScenes_IncludesActiveSiegeScene`. | Doc/symbol drift | Codex | Updated the doc's prose caveats + Changelog but not the Tests-section enumeration; didn't grep `docs/` for the renamed test. | Fixed the bullet. New LESSONS-LEARNED note (Build/Tooling): grep `docs/` for a test/symbol name when renaming it. |

## Disputed / non-findings (recorded so they aren't re-litigated)

- **Settings-provider (Standards Agent 1, flagged as ADR-008 "violation"):** DISPUTED by Claude with evidence, then INDEPENDENTLY DISPUTED by Codex (Suspect #1). `ShaderPrecompileRunner` is a documented ADR-008 engine boundary (`ShaderPrecompileRunner.cs:14-18`, game-only, not unit-tested). Boundary classes read `TaomSettings.Instance` directly by established precedent: `Main/Features/TroopWeight/Hooks/*` (8 Harmony patches), `TaomSettlementFoodModel.cs:34`, `PartyIconScaleConfig.cs:47`. A `*SettingsProvider` would add interface + class + IoC registration for two bool reads with **zero testability win** (the consumer isn't tested) → `simplicity-criterion.md` reject ("tiny win + added complexity → Reject"). **Not a defect.** Codified below so the next review doesn't re-flag it.
- **Localization (Completeness Agent 4, flagged "incomplete"):** Resolved as a deliberate decision to MATCH the feature's established plain-English-toast precedent — `Finish()` completion toast, `StatusLine`, loading-screen status, and the inquiry-dialog body are all plain English; every MCM hint in `TaomSettings.cs` is plain English; the crash-guard line is a log (never localized). Codex independently confirmed (Suspect #6). Localizing only the new toast would half-localize the feature; localizing the whole feature is a separate optional task. Not a blocking gap.

## Root-cause pattern

All 3 confirmed findings are LOW polish (two UX-wording, one doc-drift), not logic/safety bugs. The two wording findings share one theme: **a new gate (toggle) was added, and the user-facing text was written for the dominant gate state without auditing the other state.** This is exactly what Agent 5's MCM-toggle-coverage (2b) + cross-state coherence traces exist to catch — and they caught both before Codex. The review mechanism worked as designed; the changeset shipped no logic regression.

## Why each agent's coverage held / gapped

- **Agent 5 (Data Flow)** caught both wording findings via its MCM-toggle-coverage rule 2b + cross-state coherence trace. Highest-value agent again — consistent with the project history that every prior HIGH was a data-flow gap.
- **Agent 4 (Completeness)** verified the doc was updated for the NEW content (DefaultScenes sync, MCM toggles, capture guidance) but did NOT diff the Tests-section enumeration against the renamed tests — Codex caught that. Scope gap: "is the doc updated for the new work" ≠ "does every symbol the doc names still exist."
- **Agents 1/2/3:** standards finding disputed (and Codex-confirmed disputed); compatibility genuinely clean (every API verified against installed v1.4.6, including `isHidden` re-evaluation per render); efficiency genuinely clean (all new work is in `Begin()`, once per walk; nothing on the per-frame `Tick`).
- **Codex** added the one finding the 5 agents missed (stale test name) by diffing the test file against the doc, and independently confirmed all 6 suspect verdicts + both disputes. No disagreement between Codex and the Claude agents on any item.

## Lessons codified (appended to `docs/reviews/LESSONS-LEARNED.md`)

1. **GameModels & Services:** A boundary class (GameModel, Harmony patch, runner/orchestrator, static IL-call target) reading `TaomSettings.Instance` directly is NOT an ADR-008 defect — the `*SettingsProvider` injection pattern is for services and unit-tested classes; don't flag a boundary's direct MCM read.
2. **Build/Tooling/Workflow:** When renaming a test or any symbol that docs reference by name, grep `docs/` for the old name — prose caveats and Changelog get updated but enumerated symbol lists (Tests sections, Key Files tables) drift silently.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
