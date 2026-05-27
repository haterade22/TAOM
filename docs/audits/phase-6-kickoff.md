# Phase 6 Kickoff — Cross-Feature Handshake Review

For the next session. Read this + [feature-manifest.md](feature-manifest.md) + [wiring-matrix.md](wiring-matrix.md) + the four completed cluster docs ([cluster-gamemodels.md](cluster-gamemodels.md), [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md), [cluster-harmony-patches.md](cluster-harmony-patches.md), [cluster-ui.md](cluster-ui.md)) before doing anything else.

## Audit state at start of Phase 6

| Phase | Status | Output |
|---|---|---|
| 0 (Manifest) | Complete | [feature-manifest.md](feature-manifest.md) |
| 1 (Wiring) | Complete | [wiring-matrix.md](wiring-matrix.md) + issue #122 |
| 2 (GameModels) | Complete | [cluster-gamemodels.md](cluster-gamemodels.md) + issues #134, #135, #137, #138, #140, #142, #144, #145, #147, #148 |
| 3 (CampaignBehaviors) | Complete | [cluster-campaign-behaviors.md](cluster-campaign-behaviors.md) + issues #123–#131 |
| 4 (Harmony patches) | Complete | [cluster-harmony-patches.md](cluster-harmony-patches.md) |
| 5 (UI / Mixin / Prefab) | Complete (2026-05-13) | [cluster-ui.md](cluster-ui.md) + issues #165–#169 |
| **6 (Cross-feature handshake)** | **Not started** | This phase |
| 7 (Tests) | Not started | `test-coverage.md` |
| 8 (Docs) | Not started | `docs-gaps.md` |
| 9 (Triage + Fix) | Not started | issues + commits |

## Goal

Phases 2–5 reviewed features in isolation. Phase 6 reviews the **gaps between features** — places where two TAOM features both touch the same TaleWorlds API, GameModel parameter, mission state, or static helper, and the result depends on which fires last (or first).

Output: `docs/audits/cluster-cross-feature.md`. P1 (silent overwrite) and P2 (race-condition risk) → GitHub issues with `audit-impl` label.

This is **structural correctness across feature boundaries.** It is NOT a re-review of any single feature; it is the question \"do feature A and feature B coexist in a way that produces the intended union of their behaviors, or does one silently win and the other become inert?\"

## The known + suspected collision pairs

Pulled from CLAUDE.md memory + cluster docs:

| Pair / triple | Shared touchpoint | Risk profile | Source |
|---|---|---|---|
| **SmartCavalryAI × MixedFormations × CompanionTactics** | `Formation.SetMovementOrder` patches; shared deferred category `Patch_MissionTime_SetMovementOrder` | Horse-archer charge line silently overwritten by MixedFormations layout (confirmed via Codex review 2026-05-06; memory `feedback_cross_feature_handshake_via_shared_adapter.md`); CompanionTactics `CancelStanceOnMove` Postfix lives in same shared category | Memory + cluster-harmony-patches |
| **CulturalFeats × RevoltTuning** | `TaomSettlementLoyaltyModel` consumed by CulturalFeats, fed by RevoltTuning's `IRevoltTuningConfigProvider` | Cross-feature data-flow path — verify provider returns valid values when CulturalFeats consumes it; memory `feedback_simpler_fix_first` reminds us to check config defaults | Manifest + wiring-matrix |
| **CharacterCreation × HeroRace × RaceAge** | All three touch Hero racial state. `Patch3_SetRace`, `Patch5_FaceGen`, `Patch9_RaceFilter`, `Patch29_CCBodyProperties` interact | Race ID flow from CC → Patch29 body re-apply → HeroRace persistence → RaceAge lifespan logic | Manifest |
| **BannerColorPersistence × BannerInjection × Patch24_BannerDriftGuard** | Three features mutate banner colors | Last-writer-wins risk; #122 already flagged the MobilePartyVisual miss in wiring | Manifest + #122 |
| **CareerSystem × TroopProgression** | Careers modify TroopWages / PartySize / PartyMovementSpeed which TroopProgression's models also touch via `CareerPassiveHelper.ApplyFactor` static helper (8 of 16 CulturalFeats models also use it) | `CareerPassiveHelper` is the cross-cutting integration point — verify multiplication order vs additive feats | cluster-gamemodels + memory |
| **SpecialResources × CareerSystem** | Both extend the inventory upgrade screen via different `[PrefabExtension]` decorators; SpecialResources resolves `IOnPartyUpgradeResourceCheck` | Verify both extensions can coexist on the same screen without z-order / hit-test conflicts | manifest |
| **TimeAcceleration × MapBar** | TimeAcceleration injects into vanilla map bar's CenterPanel; other features also extend MapBar | Verify TAOM extra fast-forward button doesn't conflict with future MapBar mods or SiegeDefense map notifications | cluster-ui |
| **FactionMap × CharacterCreation** | FactionMap widgets rendered on `CharacterCreationCultureStage.xml`; CharacterCreation also patches that stage's VM via `Patch9_RaceFilter` | Verify widget lifecycle vs VM rebuild on culture change | cluster-ui + Patch9 |

Plus a **global \"any two features patch the same method?\" sweep** to catch unknown collisions.

## Inputs

- `Main/Features/**/Hooks/Patch*.cs` — Harmony patch targets enumeration
- `Main/Features/**/Models/Taom*Model.cs` — GameModel parameter dependencies
- `Main/Features/**/CareerPassiveHelper*.cs` (CareerSystem) — the cross-feature static helper
- `Main/Adapters/**` — shared adapter interfaces used by multiple features
- Memory: `feedback_cross_feature_handshake_via_shared_adapter.md`, `feedback_clamp_nan_infinity_propagates.md`, `feedback_no_aspirational_enum_values.md`
- cluster-ui.md \"Cross-cuts\" section (FactionMap widget threading question)

## Procedure (per collision pair / triple)

Spawn one `feature-dev:code-explorer` agent per pair to trace the data flow. Plus one `error-detective` agent for the global \"do any two features patch the same method?\" sweep.

### Per-pair check

1. Identify the shared touchpoint (method, GameModel param, adapter, static helper).
2. Trace the call path from each feature into the touchpoint.
3. Enumerate the possible outcomes:
   - **Both apply additively** (e.g., both add multipliers to the same float) → safe, but verify ordering is deterministic.
   - **One overrides the other** (last-write-wins on a struct property) → P1 if behavior depends on order, P2 if outcomes happen to converge.
   - **One short-circuits the other** (Prefix returning false) → P1 if the short-circuit feature isn't aware of the other feature's logic.
   - **Race on shared state** (static field, singleton mutation) → P1 if multi-threaded, P2 otherwise.
4. Identify the precedence handshake — is it explicit (one feature \"owns\" the touchpoint and the other defers) or implicit (whichever IoC registers later)?

### Global sweep (error-detective agent)

1. Grep `[HarmonyPatch(typeof(X), \"Method\")]` across all `Main/Features/**/Hooks/*.cs`.
2. Group by (TaleWorlds type, method name).
3. Any group with > 1 TAOM patch class = a collision pair to inspect.
4. Plus: any pair of features that both `IoC.Resolve<IXxxAdapter>()` and both call setter methods on the resolved adapter = a write-conflict pair.

## Specific carryovers from prior phases

- **FactionMap widget threading** (cluster-ui finding #15, #16): does Gauntlet render single-threaded? If yes, the `_allInstances` race + `HoveredFactionName` cross-thread write drop to P3. If no, they are reproducible crashes. Decompile `TaleWorlds.GauntletUI.GauntletLayer.Update` + `GauntletLayer.Render` from the installed v1.3.15 DLL.
- **CareerPassiveHelper as system-wide integration point** (cluster-gamemodels #43): 8 of 16 CulturalFeats models + TroopProgression models + 2 CareerSystem models all funnel through `CareerPassiveHelper.ApplyFactor`. Trace the multiplication order and verify it composes cleanly with vanilla per-mille feat formulas.
- **SiegeDismount manifest reclassification** (wiring-matrix Phase 1 P3): SiegeDismount is a `MissionBehavior`, not a manual patch. Carry forward to Phase N+2 docs pass. Not a Phase 6 collision concern.
- **gui-ui.md `SecondaryInfoItems` rule rationale** (cluster-ui cross-cuts): the rationale paragraph cites a v1.3.15-unreachable crash. Phase 9 doc fix.

## Output format

`docs/audits/cluster-cross-feature.md` mirrors the other cluster docs:

```markdown
# Cross-Feature Handshake Audit — Phase 6

Last updated: <date>
Scope: 7+ known collision pairs + global same-method-patch sweep

## Executive summary
…

## Master findings table
| # | Severity | Pair | Touchpoint | File:Line (both features) | Finding | Issue |

## Per-pair reports
### SmartCavalryAI × MixedFormations × CompanionTactics
…
### CulturalFeats × RevoltTuning
…
(etc.)

## Global sweep — same-method-patch collisions

## Cross-cuts
- Implicit-precedence patterns (which feature wins by IoC order)
- Static-helper integration points (CareerPassiveHelper)
- Gauntlet threading verdict

## GitHub issues opened

## Phase 6 complete
```

## Constraint

**No code edits this phase.** Findings → issues only. Phase 9 batches the fixes.

## Done condition

Phase 6 is complete when:

1. `docs/audits/cluster-cross-feature.md` has master findings table + per-pair reports + global sweep + cross-cuts populated.
2. Every P1/P2 has a GitHub issue (`audit-impl`).
3. `docs/audits/phase-7-kickoff.md` written for the next session (Test Coverage audit per session-prompts.md Phase 7 template).
4. `docs/audits/README.md` phases table updated.
5. `/context-save` ran with descriptor `phase6-crossfeature-complete`.

## Pre-flight

1. `/context-restore` to load the latest snapshot.
2. Read this brief + the 4 cluster docs from Phases 2–5.
3. Spawn 1 `feature-dev:code-explorer` agent per known collision pair (8 pairs/triples — can batch 2–3 if context budget tight) + 1 `error-detective` agent for the global sweep, in parallel.
4. Aggregate to `cluster-cross-feature.md`.

## What this phase will NOT cover

- Test coverage gaps (Phase 7).
- Doc staleness (Phase 8).
- Any actual fix work (Phase 9).

## What still hasn't been audited at all (forward-looking)

Phases 7, 8, 9 are all unstarted. After Phase 6, the audit shifts from \"find bugs in code\" to \"find untested code\" (Phase 7), \"find stale docs\" (Phase 8), and finally \"fix everything\" (Phase 9). Phase 9 may span multiple sessions depending on the cumulative issue queue (currently 30+ open `audit-*` issues; Phase 6 will add more).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/cluster-cross-feature.md](./cluster-cross-feature.md)

<!-- backlinks-end -->
