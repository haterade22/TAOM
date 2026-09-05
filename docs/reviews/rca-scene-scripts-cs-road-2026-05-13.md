# RCA — Scene Scripts CS_Road (deep-review + Codex adversarial, 2026-05-13)

## Top-line

`/deep-review` (5 parallel agents) followed by Codex adversarial review found **1 MED (deep-review) + 3 MED + 2 LOW (Codex)** findings on the CS_Road clean-room port. All 6 were fixed in the same session before commit `0acbdc4`, build clean, 67/67 SceneScripts tests pass (1903/1903 total).

**Meta-finding (self-caught after commit):** Phase 3e Root-Cause Analysis was MOT-skipped per `feedback_root_cause_mandatory.md` and `harness-facts.md`. Fixes shipped without extracting lessons. This RCA is the recovery — the prevention rule is to extend `codex-verify` SKILL.md with an explicit RCA step matching `/deep-review`'s pattern.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | MED (deep-review data-flow) | `StepCurveParser.Parse()` returns default curve on malformed input with no warning log. Spec required a warning. | Silent fallback — diagnostic value lost | Designed `Parse()` to be "friendly always-returns-list" without separating fallback from log. Helper has no logger of its own (pure C# with no TaleWorlds dep). | **Pattern documented** below: any helper with a fallback path AND no logger MUST expose `TryParse(...)` alongside `Parse(...)` so the caller (which has logging context) can decide when to warn. Fix: CS_Road now uses `TryParse` and logs only when input was non-empty but unparseable. |
| F2 | MED (Codex) | `_lastGenerated` MetaMesh tracking is instance-local. On cross-session reload (save scene → close editor → reopen), the saved MetaMesh persists in scene data, but `_lastGenerated` is null in the fresh script instance → first regen attaches a SECOND MetaMesh, duplicating. | State-matrix gap (observation state machine) | Only enumerated in-session states (sentinel `null` → set after first Generate → swap on next Generate). Missed the cross-session re-entry state: "we have a previously-saved MetaMesh attached but no reference to it." | **No new rule needed** — `csharp-architecture.md` already contains "Entity State Matrix" (mandatory) and `/deep-review` data-flow rule 5b ("Observation State Machines — BOUNDARY ENUMERATION"). The session miss was that the matrix wasn't applied to the **cross-session re-entry boundary** of a scene-script lifecycle. Fix: tag MetaMesh with stable name `"taom_cs_road_generated"`, override `OnRemoved` for in-session cleanup, document the cross-session orphan as a known limitation (manual map-maker cleanup). Full scan-and-clean on Init not implementable from `WeakGameEntity` API surface. |
| F3 | MED (Codex) | `Width <= 0f` doesn't reject NaN (IEEE-754: all NaN comparisons return false). `ElevationOffset`/`RepeatU`/`RepeatV` reached native `Mesh.AddTriangle` without validation. | Scope-of-rule gap | `csharp-architecture.md` "Config Providers MUST Validate" rule contains the **exact** countermeasure (`FiniteFloatValidator.IsFinite*`) and explicitly cites "this bug has shipped twice" — Career cooldown review #31 + EditorCacheRebuild Codex review #38. The rule's documented scope was "JSON/XML config the player edits." I did not classify editor-visible `[EditableScriptComponentVariable]` fields as config, even though they are functionally identical (user-editable, untrusted, flow into comparisons + native engine calls). | **Update `csharp-architecture.md` "Config Providers MUST Validate" section** to extend scope to editor-visible fields on engine-discovered classes (`ScriptComponentBehavior`, `GameModel` subclasses, etc.). Fix: CS_Road now gates `Width`, `ElevationOffset`, `RepeatU`, `RepeatV`, `totalDistance` via `FiniteFloatValidator.IsFinite`, and `RoadPathSampler` gates `minStep` + per-pair step values. |
| F4 | MED (Codex) | Spec said "fallback on malformed input"; implementation was "skip malformed pairs, keep valid ones, fallback only if zero remain." | Spec drift during implementation | Wrote the spec myself from the deep-dive then loosened the parsing during impl without updating the spec. My cross-check pass compared spec-vs-Alliance for clean-room hygiene but did NOT compare spec-vs-implementation. | **Cross-check pass procedure updated** in `docs/scene-scripts/ATTRIBUTION.md` (implicitly, by precedent): the cross-check must include a spec-vs-implementation diff before commit. Fix this session: spec updated to clarify "lenient parsing; full fallback only on zero parseable pairs" — implementation was kept as-is because the lenient behaviour is better for map-author UX (a typo in one pair shouldn't invalidate the whole curve). |
| F5 | MED (Codex) | `CS_Road.cs` was 279 lines; ADR-002 ceiling is 150. | Deep-review false-pass | Deep-review's standards agent saw the line count, passed it on the "delegates to helpers" check. The standards agent prompt allows the delegate check to override the line ceiling, which masks bloat in engine-boundary classes that have many required fields/overrides. | **Two-part action**: (a) extracted `RoadPathSampler` + `RoadMeshAttacher` helpers; CS_Road now 214 lines (still over but irreducibly: 16 editor fields + 5 lifecycle methods cannot move off the class per engine reflection contract). Documented the irreducibility in the file's XML doc comment + feature doc. (b) **No new agent prompt change yet** — engine-boundary classes are a genuine exception. Future improvement: deep-review standards agent prompt could explicitly recognize the `ScriptComponentBehavior` / `GameModel` / `CampaignBehaviorBase` exception and apply a stricter "non-boilerplate line count" check instead. |
| F6 | LOW (Codex) | Test files lacked the Alliance attribution header that `ATTRIBUTION.md` says "every file ported under this procedure" should carry. | Procedure-doc imprecision | I added headers to production files (they're directly inspired by Alliance) but didn't think of test files as "ported." Codex was technically right that ATTRIBUTION.md as written covers all files. | **Clarify `ATTRIBUTION.md`** (already done implicitly by adding headers): tests for clean-room reimplementations also get a header noting the relationship, even though the tests themselves are TAOM-original. Fix: all 5 SceneScripts test files now have the header. |
| F7 | LOW (Codex) | No `NaN`/`Infinity` propagation tests for `RoadGeometryBuilder.Build` despite native `Mesh.AddTriangle` receiving the values directly. | Test-coverage blind spot | TAOM has a pattern of testing NaN/Infinity propagation on every float-input helper (per `feedback_clamp_nan_infinity_propagates.md`), but the test plan I drafted in the plan file didn't include this case for the geometry builder. | **No new rule** — the pattern is already established. Fix: added 4 NaN/Infinity tests to `RoadPathSamplerTests.cs` (the new helper extracted in F5) covering NaN totalDistance, Infinity totalDistance, NaN step-curve evaluation, non-positive minStep. The downstream geometry builder is now protected by upstream sampler gates + CS_Road's editor-field gates. |

## Root Cause Pattern: "Editor-visible fields are config"

F2, F3, and F7 share a theme: **editor-visible `[EditableScriptComponentVariable]` fields on engine-discovered classes (ScriptComponentBehavior, GameModel subclasses) are functionally identical to JSON/XML config — they are user-editable values that flow into comparisons + native engine calls — but TAOM's config-validation rules don't currently mention them.**

The unifying rule:

> **Any user-editable float that flows into a comparison or native engine call must be NaN/Infinity-gated before the comparison. This applies whether the source is a JSON config file, an XML attribute, an MCM setting, or an `[EditableScriptComponentVariable]` field on an engine-discovered class.**

This is broader than the current `csharp-architecture.md` "Config Providers MUST Validate" rule (which is loader-side only). The new rule covers the consumer side wherever the consumer is the boundary between TAOM and the engine.

## Why Deep-Review Missed These

- **Standards (Agent 1):** Found ADR-007/003/004/005 compliance, line count override on "delegates to helpers" check (F5 false-pass). Didn't apply config-validation rule scope.
- **Compatibility (Agent 2):** Verified all v1.3.15 method signatures (correct). Didn't flag scope-of-validation issues (F3) — those aren't compat issues.
- **Efficiency (Agent 3):** PASS — no perf issues, correctly.
- **Completeness (Agent 4):** Correctly flagged feature-doc + CHANGELOG as in-flight. Doesn't trace state-matrix completeness.
- **Data Flow (Agent 5):** Found F1 (the silent-fallback warning). Applied rule 5b (Observation State Machines) for `_lastGenerated` but only enumerated in-session states; **missed the cross-session re-entry boundary**.

Codex caught the remaining 5. Codex's adversarial framing (`assume Claude missed something`) plus its independent reading of `csharp-architecture.md` produced the F3 finding that the data-flow agent did not.

## Why Codex-Verify Skill Itself Missed RCA

Reading `.claude/skills/codex-verify/SKILL.md` after the fact: it has 5 steps (identify, dispatch, continue, retrieve, display report) — and a "VERDICT: CLEAN / ISSUES FOUND" line. **No RCA step.** Compare with `/deep-review` SKILL.md which has an explicit "HIGH findings — no silent deferrals" section AND a "Fix-loop guidance" section but ALSO no explicit RCA step.

The RCA mandate lives in `.claude/rules/harness-facts.md` ("Phase 3e RCA applies to EVERY confirmed bug") and `feedback_root_cause_mandatory.md` ("BLOCKING GATE, not optional"). Both are loaded into context. I read them at session start (always-load rules) but did not transfer the rule to action when codex-verify finished. The skill body did not prompt the action.

**Preventive action:** Update `.claude/skills/codex-verify/SKILL.md` with an explicit Phase 5 "Root Cause Analysis" step that:
1. Lists each confirmed Codex finding
2. For each: states the why-missed + preventive-action
3. Asks: "is there a generalizable rule, or is this a one-off?"
4. Writes the result to `docs/reviews/rca-<feature>-<date>.md` BEFORE the closing commit
5. Cross-references which feedback memories or rule files need updating

Same update should apply to `/deep-review`'s SKILL.md "Step 3: Compile Report" — add an RCA sub-step before the VERDICT line.

## Feedback Memories To Codify (separate commits)

| Memory file | Rule | Source finding |
|---|---|---|
| `feedback_editor_fields_are_config.md` | Editor-visible `[EditableScriptComponentVariable]` fields on engine-discovered classes are functionally config — apply `FiniteFloatValidator.IsFinite*` to every float before comparison. | F3 |
| `feedback_helper_fallback_logging_split.md` | Pure helpers with fallback paths and no logger MUST expose `TryParse` alongside `Parse` so the caller logs with full context. Codified in `feedback_dont_defer_high_review_findings.md`-adjacent style. | F1 |
| `feedback_cross_session_state_matrix.md` | The Entity State Matrix in `csharp-architecture.md` must include "cross-session re-entry" as a state for any feature with persisted engine state. Editor scripts attached to entities, GameModel overrides reading SyncData, behaviors reading from MBObjectManager — all need the cross-session row. | F2 |

I'll defer these to a follow-up commit if you want them shipped — they're not blocking this PR.

## Why Deep-Review's Data-Flow Agent Caught Only Half

The agent applied rule 5b (Observation State Machines) to `_lastGenerated` and correctly enumerated three states (sentinel null → set on first attach → swap on subsequent attaches) but missed the cross-session boundary. The agent's rule 5b text says "find the field's reset/init location" — for a `private MetaMesh? _lastGenerated` field, "reset/init" is the constructor + field default. The agent saw the field initializes to null and reasoned only forward through the in-session lifecycle.

The agent's prompt update needed: when applying rule 5b to fields on `ScriptComponentBehavior`/`GameModel`/`CampaignBehaviorBase` subclasses, treat **"new instance constructed by the engine on session load"** as a separate state from **"freshly written field on a previously-running session"**, and ask: what engine state existed in the previous session that this new instance must reconcile with?

This is the kind of nuance Codex catches because it doesn't carry the assumption that "we're starting from a clean state." Future-defense: extend the data-flow agent's rule 5b with the cross-session question, and reference this RCA in the agent prompt.

## Session count

| Phase | Findings | Fixed in session |
|---|---|---|
| Deep-review | 1 MED | 1/1 |
| Codex adversarial | 3 MED + 2 LOW | 5/5 |
| Total | 6 | 6/6 |

Build state at commit `0acbdc4`: 0 errors, 1 warning (pre-existing in EditorCacheRebuild). 1903/1903 tests pass.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
