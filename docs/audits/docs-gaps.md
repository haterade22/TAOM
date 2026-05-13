# Documentation Audit — Phase 8

Last updated: 2026-05-13
Scope: 44 features × `docs/features/<name>.md` presence + TEMPLATE.md shape conformance + accuracy spot-checks + v1.2 staleness scan.

## Executive summary

| Severity | Count | Features |
|---|---|---|
| **P1** | 1 | Execution (doc entirely missing — Phase 0 #19 carryover) |
| **P2** | 3 | CompanionTactics (build-disabled status hidden in Overview), AdvancedCombat (stale "no tests" claim), Warg (stale "no tests" claim) |
| Inline note (no issue) | ~9 | CharacterCreation sibling cross-ref, HeroRace sibling cross-ref, Siege multi-doc cross-ref, FiefManagement historical v1.2.x context, Messengers historical migration context, QuickActions historical migration context, CareerSystem one-line schema history note, BannerColorPersistence test count mismatch, CustomBattles test count mismatch |
| **OK** | ~31 | The remaining features — TEMPLATE-conformant, spot-checks pass, no stale claims |

**Phase 8 verdict:** TAOM documentation is in **substantially better shape than test coverage was.** Where Phase 7 surfaced 20 P1/P2 issues, Phase 8 surfaces only 4. All 43 features (per manifest) — 44 directories on disk — have at least one feature doc except Execution; the typical doc covers all 8 TEMPLATE sections; spot-checks pass at well over 90%.

**Dominant gap class:** **stale test-coverage claims in docs** (AdvancedCombat, Warg) — docs were written before the tests were added, and the "no tests exist" assertion was never updated. Two of the 3 P2s share this exact pattern.

**Phase 0 carryovers resolved:**
- ✅ **Execution missing doc** — Phase 0 #19 confirmed, P1 issue opened.
- ✅ **Manifest 43 vs disk 44 off-by-one** — Phase 7 noted; the actual feature count on disk is 44 (manifest text undercounts by 1; manifest table rows are correct). Will be normalized in Phase 9 by updating manifest text.

**Per user rule (this phase):** *small typos / single-sentence improvements should NOT become issues — note them inline in the audit doc only.* That rule consolidated ~9 candidate findings into inline notes; only 4 issues opened.

## Master findings table

| # | Severity | Feature | Gap | Doc path | Issue |
|---|---|---|---|---|---|
| 1 | **P1** | Execution | `docs/features/execution.md` MISSING; Phase 0 carryover; `detect-docs-gaps.sh` flags this on every session start. | `docs/features/execution.md` (missing) | #196 |
| 2 | **P2** | CompanionTactics | Feature is build-disabled (TEMP-SMARTCAVALRY-EXCLUDE state per `companion-tactics.md:185`) but the disclosure is buried in "Known limitations" instead of the Overview. Readers may expect the feature to work out-of-box. | `docs/features/companion-tactics.md:185` | #197 |
| 3 | **P2** | AdvancedCombat | Tests section at `advanced-combat.md:71` claims "No unit tests exist for AdvancedCombat services" — but `TAOM.Tests/Features/AdvancedCombat/BoneCollisionServiceTests.cs` exists at 252 lines (11 tests). Doc is stale relative to current test surface. | `docs/features/advanced-combat.md:71` | #198 |
| 4 | **P2** | Warg | Tests section at `warg-combat.md:117` claims "No dedicated test files (ported from LOTRAOM without TDD)" — but `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs` exists with 7 tests. Doc is stale. | `docs/features/warg-combat.md:117` | #199 |

## Per-feature reports (folded into the batch summaries below)

Phase 8 dispatched 5 parallel `Explore` agents against the same batches used in Phase 7. Per-feature audit findings are summarized in the inline-notes table that follows; the four flagged P1/P2 features are described in the Master findings table above.

### Batch A — Pure GameModels (8) — ALL OK

| Feature | Primary doc | Sibling doc(s) | TEMPLATE | Verdict |
|---|---|---|:--:|:--:|
| Arena | `arena.md` | `tournament-armor-assignment.md` | 8/8 | OK |
| ArmyTargeting | `army-targeting.md` | — | 8/8 | OK |
| BattleBalance | `battle-balance.md` | — | 8/8 | OK |
| CulturalFeats | `cultural-feats.md` | — | 8/8 | OK |
| Diplomacy | `diplomacy.md` | `war-of-the-ring.md` | 8/8 each | OK (well-cross-referenced) |
| Encyclopedia | `encyclopedia.md` | — | 8/8 | OK |
| RaceAge | `race-age-system.md` | `offspring-race-inheritance.md` | 8/8 + 7/8 (sibling Configuration N/A) | OK |
| TroopProgression | `troop-progression.md` | — | 8/8 | OK |

**Batch A inline notes:**
- CulturalFeats feat count discrepancy: doc cluster table says "59 feats total" but per-culture rows sum to 63. Cosmetic.

### Batch B — CampaignBehaviors (9) — 1 P2, 7 inline-or-OK

| Feature | Primary doc | Sibling doc(s) | TEMPLATE | Verdict |
|---|---|---|:--:|:--:|
| BannerInjection | `banner-injection.md` | — | 8/8 | OK |
| CharacterCreation | `character-creation.md` | `character-creation-body-properties.md` | 8/8 each | inline-note (sibling not cross-linked) |
| CompanionTactics | `companion-tactics.md` | — | 8/8 | **P2** (build-disabled hidden) |
| EquipPresets | `equip-presets.md` | — | 8/8 | OK |
| FiefManagement | `fief-management.md` | — | 8/8 | inline-note (v1.2.x is historical context) |
| HeroRace | `hero-race.md` | `offspring-race-inheritance.md` (shared with RaceAge) | 8/8 each | inline-note (sibling not cross-linked) |
| InitialChildGeneration | `initial-child-generation.md` | — | 8/8 | OK |
| NamedCompanions | `named-companions.md` | — | 8/8 | OK |
| StartupResources | `startup-resources.md` | — | 8/8 | OK |

### Batch C — Services / service-heavy (9) — 2 P2, 6 OK + 1 inline

| Feature | Primary doc | Sibling doc(s) | TEMPLATE | Verdict |
|---|---|---|:--:|:--:|
| AdvancedCombat | `advanced-combat.md` | — | 8/8 | **P2** (stale "no tests" claim) |
| EditorCacheRebuild | `editor-cache-rebuild.md` | — | 8/8 | OK |
| MainMenuCustomizer | `main-menu-customizer.md` | — | 8/8 | OK |
| RevoltTuning | `revolt-tuning.md` | — | 8/8 | OK |
| ShaderPrecompilation | `shader-precompilation.md` | — | 8/8 | OK |
| Siege | `siege.md` | `siege-defense.md`, `siege-trebuchets.md` | 8/8 each | inline-note (no cross-refs between the 3) |
| Spider | `spider.md` | — | 8/8 | inline-note (fang bone indices are flagged placeholder pending smoke test) |
| TimeAcceleration | `time-acceleration.md` | — | 8/8 | OK |
| Warg | `warg-combat.md` | — | 8/8 | **P2** (stale "no tests" claim) |

### Batch D — Patches / UI-heavy (10) — ALL OK + 2 inline

| Feature | Primary doc | Sibling doc(s) | TEMPLATE | Verdict |
|---|---|---|:--:|:--:|
| AtmospherePersistence | `atmosphere-persistence.md` | — | 8/8 | OK |
| BannerColorPersistence | `banner-color-persistence.md` | — | 8/8 | inline-note (test count summary 22 vs breakdown 25) |
| BattleScenes | `battle-scenes.md` | — | 8/8 | OK (DISABLED status correctly documented) |
| CharacterSelection | `character-selection.md` | — | 8/8 | OK (transpiler-only nature correctly documented; Phase 7 Phase-0 carryover) |
| CustomBattles | `custom-battles.md` | — | 8/8 | inline-note (test count summary 22 vs breakdown ~39) |
| FactionMap | `faction-map.md` | — | 8/8 | OK |
| LocalizationOverride | `localization-override.md` | `localization.md` (general system doc) | 8/8 each | OK (Distinction section explicitly clarifies sibling) |
| MixedFormations | `mixed-formations.md` | — | 8/8 | OK |
| SmartCavalryAI | `smart-cavalry-ai.md` | — | 8/8 | OK |
| WeatherBoundsGuard | `weather-bounds-guard.md` | — | 8/8 | OK |

### Batch E — Heavy mixed (8) — 1 P1, 1 P2-downgraded-to-inline, 6 OK/inline

| Feature | Primary doc | Sibling doc(s) | TEMPLATE | Verdict |
|---|---|---|:--:|:--:|
| CareerSystem | `career-system.md` | `career-cc-selection.md` | 8/8 each | inline-note (one-line v1.2 schema history at line 67, explicitly dated; per user rule, not an issue) |
| **Execution** | **MISSING** | — | n/a | **P1** |
| Messengers | `messengers.md` | — | 8/8 | inline-note (v1.2.12 refs are explicit migration context, not stale) |
| QuickActions | `quick-actions.md` | — | 8/8 | inline-note (v1.2.x refs are explicit migration context) |
| SettlementGuards | `settlement-guards.md` | — | 8/8 | OK |
| SiegeDismount | `siege-dismount.md` | — | 8/8 | OK (unusually thorough with Codex review citations) |
| SpecialResources | `special-resources.md` | — | 8/8 | OK (11 resources enumerated; audit-motivating requirement met) |
| TroopWeight | `troop-weight-system.md` | — | 8/8 | OK |

## Cross-cuts

### Sibling-doc cross-reference gaps (4 cases — inline notes, not issues)

Several features have one or more sibling docs that aren't cross-linked from the primary doc:

| Primary | Sibling(s) | Status |
|---|---|---|
| `character-creation.md` | `character-creation-body-properties.md` | No cross-ref — reader discovery gap |
| `hero-race.md` | `offspring-race-inheritance.md` | No cross-ref |
| `siege.md` | `siege-defense.md`, `siege-trebuchets.md` | No cross-ref between the 3 |
| `arena.md` | `tournament-armor-assignment.md` | No cross-ref (clean separation: code vs data) |

**Phase 9 fix sketch (consolidated):** add a "See also" section to each primary doc above. This is a single coordinated PR, not 4 separate issues.

### v1.2 reference scan — distinguish stale vs migration context

Phase 8 explicitly flagged v1.2 references. Distribution:
- **Stale (would mislead the reader): 0 found.** ✅
- **Historical / migration context (acceptable):** ~6 features cite "Ported from LOTRAOM 1.2.x" or "v1.3.15 introduced API breaks vs 1.2.12" as explicit migration framing. These are *correct* documentation and should stay.
- **Schema history note:** 1 case — `career-system.md:67` notes a 2026-05-04 schema cleanup. Per user rule (inline-only).

**Outcome:** v1.2-staleness was NOT a real failure mode in TAOM docs. The risk-of-doc-rot manifests in **stale test-count claims** instead (2 cases — AdvancedCombat, Warg P2s).

### Orphan / cross-cutting docs (no feature-dir mapping — informational only)

`docs/features/` contains ~11 markdown files that do NOT map 1:1 to `Main/Features/<X>/`. These are cross-cutting concerns / system-level guides; they are NOT gaps:

- `alignment-aware-execution.md` — Execution-feature alignment subsystem
- `bannerlord-together-compat.md` — Multiplayer-compatibility shim
- `gondor-armor-revamp.md` — Asset/data revamp guide
- `gui-sprite-system.md` — Sprite atlas system reference
- `kingdom-creation.md` — Kingdom data authoring guide
- `localization.md` — General localization system (sibling to `localization-override.md`)
- `minor-factions.md` — Minor faction data guide
- `no-mount-cultures.md` — Mount-disabled culture pattern guide
- `scene-scripts.md` — Maps to `Main/SceneScripts/` (NOT under `Main/Features/`)
- `tournament-armor-assignment.md` — Arena sibling
- `weapon-xml-pipeline.md` — Asset pipeline guide

Phase 8's TEMPLATE conformance rubric does not apply to these — they're guides, not feature pages.

### Stale-claim risk-class (the surprise finding)

Phase 8 expected v1.2 API drift to be the dominant staleness. Actual: **the dominant staleness is test-coverage claims that were never updated when tests were added.** AdvancedCombat said "no tests exist" and Warg said "no dedicated test files" — both demonstrably wrong as of Phase 7 (the very same audit that demanded these docs).

**Recommendation for Phase 9:** when fixing the AdvancedCombat / Warg doc issues, also add a maintenance note to TEMPLATE.md or the `docs/features/` README warning that "Tests" sections drift fast — link to `TAOM.Tests/Features/<X>/` and let CI catch the discrepancy.

## GitHub issues opened (4 — P1×1 + P2×3)

| # | Severity | Title |
|---|---|---|
| #196 | P1 | audit-docs: Execution — `docs/features/execution.md` MISSING (Phase 0 #19 carryover) |
| #197 | P2 | audit-docs: CompanionTactics — feature is build-disabled, status hidden in "Known limitations" instead of Overview |
| #198 | P2 | audit-docs: AdvancedCombat — Tests section claims "no tests exist" but BoneCollisionServiceTests.cs has 252 lines (11 tests) |
| #199 | P2 | audit-docs: Warg — Tests section claims "no dedicated test files" but WargAttackServiceTests.cs exists |

(Phase 9 fix queue is now ~79 open `audit-*` issues: Phase 1-6 = #121-#175, Phase 7 = #176-#195, Phase 8 = #196-#199.)

## Phase 8 complete
