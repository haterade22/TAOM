# Phase 2 Kickoff — GameModel Cluster Review

For the next session. Read this + [feature-manifest.md](feature-manifest.md) + [wiring-matrix.md](wiring-matrix.md) and start there — do NOT re-derive scope from CLAUDE.md or session memory.

## Goal

Per-feature semantic review of every `Taom*Model.cs` GameModel override. Phase 1 verified wiring (the model is correctly constructed with all dependencies). Phase 2 verifies **correctness of the override itself**: does it behave properly when called, does it gracefully fall back to vanilla, does it follow `.claude/rules/gamemodels.md`, and does its test coverage match ADR-008.

Output: `docs/audits/cluster-gamemodels.md` with a per-model pass/fail row covering override correctness, null safety, fallback chain, inline-logic violations, and test coverage. Plus GitHub issues for actionable misses (label `audit-impl`).

Phase 2 is semantic. **This is NOT a wiring matrix** — Phase 1 already did that. Phase 2 reviews the model's body and tests.

## Inputs

- [feature-manifest.md](feature-manifest.md) — manifest's `Model` column lists models per feature
- [wiring-matrix.md](wiring-matrix.md) — Phase 1's findings; Probe 2 already verified ctor wiring for 22 of these models
- [.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md) — TAOM GameModel rules (override pattern, base.X() fallback, no inline logic, registration)
- [.claude/rules/csharp-architecture.md](../../.claude/rules/csharp-architecture.md) — non-negotiable rules, `?.` on computed properties, Config Provider validation, Entity State Matrix
- `Main/Features/<X>/Models/Taom*Model.cs` — the review targets
- `TAOM.Tests/Features/<X>/` — corresponding test files for coverage assessment

## Scope — 12 features, ~39 model classes

| Feature | Model count | Models |
|---|---|---|
| **CulturalFeats** | **17** | TaomArmyManagementModel, TaomPartySpeedModel, TaomSettlementProsperityModel, TaomSettlementMilitiaModel, TaomBuildingConstructionModel, TaomVillageProductionModel, TaomCaravanModel, TaomBattleRewardModel, TaomTournamentModel (note: see Arena), TaomPartyTroopUpgradeModel, TaomPartySizeModel, TaomFoodConsumptionModel, TaomSettlementLoyaltyModel, TaomPartyMoraleModel, TaomSmithingModel, TaomClanFinanceModel, TaomRaidModel |
| CareerSystem | 5 | TaomMapVisibilityModel, TaomInventoryCapacityModel, TaomAgentStatCalculateModel, TaomAgentApplyDamageModel, TaomClanTierModel |
| BattleBalance | 3 | TaomMilitaryPowerModel, TaomCombatSimulationModel, TaomPartyHealingModel |
| Diplomacy | 3 | TaomAllianceModel, TaomKingdomDecisionPermissionModel, TaomDiplomacyModel |
| RaceAge | 3 | TaomAgeModel, TaomPregnancyModel, TaomHeroCreationModel |
| TroopProgression | 2 | TaomPartyWageModel, TaomVolunteerModel |
| Arena | 1 | TaomTournamentModel (verify whether this is the same class as CulturalFeats' or a different one — manifest is ambiguous) |
| ArmyTargeting | 1 | TaomTargetScoreModel |
| CharacterCreation | 1 | TaomCharacterStatsModel |
| Encyclopedia | 1 | TaomInformationRestrictionModel |
| Execution | 1 | TaomExecutionRelationModel |
| Siege | 1 | TaomSiegeEventModel |

**Highest-value targets:** CulturalFeats (17 models, batched registration), CareerSystem (5 models, includes per-hot-path `TaomAgentStatCalculateModel`), Diplomacy (cross-feature interactions).

## Carryover from Phase 1

Phase 1 already cleared the **ctor wiring** for 22 of these 39 models (CulturalFeats 17 + Arena 1 + Encyclopedia 1 + Diplomacy 3 = 22). Phase 2 should **NOT re-verify ctor wiring** for those. Focus on:

- Override correctness — does the method actually compute the right value?
- Null safety on computed TaleWorlds properties (memory: `?.` rule from `csharp-architecture.md`)
- `base.X()` fallback chain — when the model declines to override, does it gracefully delegate?
- No inline logic in the override body (memory: `feedback_gamemodel_inline_logic.md` — no if/foreach/switch/yield, extract to a service)
- No service-locator anti-pattern inside the override (memory: `feedback_no_service_locator_in_services.md` — IoC.Resolve belongs only in boundary classes)

For the **17 models not yet ctor-verified** (CareerSystem 5, BattleBalance 3, RaceAge 3, TroopProgression 2, ArmyTargeting 1, CharacterCreation 1, Execution 1, Siege 1), Phase 2 should include a one-line ctor check as part of the per-model review.

## The Phase 2 probe (per-feature subagent)

Spawn one subagent per feature in the scope table (12 agents total). Use `feature-dev:code-reviewer` for features with ≥3 models; `Explore` for single-model features.

**Each subagent's prompt template:**

```
You are reviewing the GameModel cluster for feature <X> as part of Phase 2 of the TAOM
feature audit. The audit is enumeration only — DO NOT propose or apply any fixes.

Required reading first:
- docs/audits/feature-manifest.md (feature <X> row)
- docs/audits/wiring-matrix.md (Phase 1 already cleared ctor wiring for these models, if applicable)
- .claude/rules/gamemodels.md
- .claude/rules/csharp-architecture.md (sections: "Non-Negotiable Rules", "?. for computed properties",
  "Config Providers MUST Validate" — applies if the model reads config, "Lookup Functions With Fallbacks")

For each model in Main/Features/<X>/Models/Taom*Model.cs:

1. **Override correctness.** For each `override` method, does the body actually compute the
   intended result? Cross-reference against the vanilla `Default<X>Model` (via ilspycmd on the
   installed v1.3.15 DLLs at %BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\, NOT against
   E:\Decompiled_Bannerlord\ which is v1.4).

2. **Null safety on computed TaleWorlds properties.** Any `hero.HomeSettlement` /
   `clan.MapFaction` / `settlement.Culture` etc. that's computed (not a plain field) MUST use
   `?.` — the getter can crash before your null check sees it.

3. **base.X() fallback chain.** When the model decides not to override, does it call
   `return base.X(...)` to preserve vanilla behavior? Are early-return guards correct
   (e.g., `if (hero == null) return base.X(hero)` — not just `return 0f`)?

4. **No inline logic.** No if-chains, foreach loops, switch statements, or yield-branching
   in the override body. Extract to a `*Service`. Even a 3-line `if (x) y else z` is a
   violation per memory feedback_gamemodel_inline_logic.md. (Exception: a single early-return
   null guard before delegating to a service is fine.)

5. **No service locator inside override.** `IoC.Resolve<>` inside the override body is wrong;
   the service should be injected via the ctor and stored as a private readonly field.

6. **Test coverage.** Open TAOM.Tests/Features/<X>/ and find tests for this model.
   - Service-layer tests covering the business logic: count + assertion strength.
   - Model-layer tests covering the override path itself (vanilla call → TAOM call): present or missing?
   - Flag if zero coverage for any override method.

7. **Save-compat concerns.** If the model mutates state (rare for GameModels — most are pure
   calculators), check for SyncData / OnGameLoaded handlers and flag any state-mutation that
   doesn't go through a CampaignBehaviorBase.

Output format — one row per model:

| Model | Override correctness | Null safety | base.X() fallback | Inline-logic free | No service locator | Test coverage | Status |
|---|---|---|---|---|---|---|---|

Plus a "Per-feature notes" section for any cross-model observations (shared service deps,
suspicious patterns).

Severity:
- P1: override produces wrong result on a hot path (e.g., culture comparison wrong direction,
  base.X() never reached when it should be) → open issue with label audit-impl.
- P2: missing null-safety on a computed property, missing test coverage for an override,
  inline logic violation that's >3 lines → open issue with label audit-impl.
- P3: cosmetic (variable naming, missing comments, `_ = unused` cleanups) → note inline only.

Constraint: NO fixes. Phase 9 batches the fixes.
```

## Output format

Create `docs/audits/cluster-gamemodels.md`:

```markdown
# GameModel Cluster Review — Phase 2

Last updated: <date>
Inputs: feature-manifest.md (12 features, ~39 models) + wiring-matrix.md (Phase 1 ctor verification)

## Executive summary

(1-2 paragraphs: total models reviewed, P1/P2 finding counts, headline observations)

## Master matrix

| Feature | Models | All overrides ✅ | Null-safety ✅ | Fallback ✅ | Inline-logic free | No service locator | Test coverage | Issues opened |
|---|---|---|---|---|---|---|---|---|
| ArmyTargeting | 1 | … | … | … | … | … | … | — |
| ...

## Findings — Feature X

(per-feature notes + per-model rows from each subagent)

## GitHub issues opened

| # | Title | Feature | Severity |
|---|---|---|---|

## Phase 2 complete

- N features reviewed
- M issues opened
- K Phase 3+ targets surfaced
```

## What counts as "open a GitHub issue"

Same severity rubric as Phase 1 ([phase-1-kickoff.md](phase-1-kickoff.md) § "What counts as 'open a GitHub issue'"), but with label `audit-impl` instead of `audit-wiring`:

| Severity | Open issue? | Examples |
|---|---|---|
| **P1 — override produces wrong result** | YES, immediate | Wrong culture comparison direction → wrong wage; missing `base.X()` fallback → vanilla calls silently no-op; null-deref in production hot path |
| **P2 — missing safety / degraded** | YES | Computed property without `?.` (potential NRE under rare game state); inline if-chain in override body; missing test coverage for an override method |
| **P3 — cosmetic** | NO, note inline | Variable rename suggestions, missing XML doc comments, ordering preferences |

Each P1/P2 issue body follows CLAUDE.md bug-issue template (Problem / Analysis / Solution sketch / Files / Testing).

## Constraint: no fixes during Phase 2

Even if a finding is a 2-line `?.` fix or an obvious null guard, **DO NOT** fix it in Phase 2. Phase 9 (triage + fix execution) batches the fixes so each gets its own commit with proper CHANGELOG + issue lifecycle. Exception: a phase-blocking miss (e.g., a model that's so broken Phase 2 can't review it) — flag to user, ask whether to break the constraint.

## Done condition

Phase 2 is complete when:
- All 12 features × ~39 models reviewed via subagent passes.
- `cluster-gamemodels.md` is written with populated master matrix + per-feature findings.
- Every P1/P2 finding has a GitHub issue (`audit-impl` label).
- The `docs/audits/README.md` "Phases" table is updated with Phase 2 status.
- `docs/audits/phase-3-kickoff.md` is written for the next session (CampaignBehavior cluster review — source template in [session-prompts.md](session-prompts.md) § "Phase 3").

Then `/context-save` again before closing out the session.

## Pre-flight checklist for the new session

1. `/context-restore` to load `phase1-wiring-complete` snapshot.
2. Confirm `wiring-matrix.md` + `phase-2-kickoff.md` exist.
3. Confirm issue #122 (BannerColorPersistence MobilePartyVisual) is still open (will be fixed in Phase 9, not Phase 2).
4. Read this brief + the manifest + Phase 1's matrix.
5. Spawn the 12 per-feature subagents in batches (e.g., 4 parallel waves of 3 features each, or 2 waves of 6, depending on context-budget tolerance).
6. Don't re-verify ctor wiring for the 22 models Phase 1 already cleared.
