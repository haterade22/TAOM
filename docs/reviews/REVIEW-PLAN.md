# TAOM Comprehensive Code Review Plan

Systematic adversarial review of all features using the Codex + Claude verification process.

## Current State

**Reviewed:** 4 features — CulturalFeats, BannerColorPersistence, ArmyTargeting, TroopProgression+TroopWeight
**Remaining:** 21 features + infrastructure
**Prompt version:** v4 (67% accuracy, 0 false positives)
**Process:** Codex generates review → Claude critically verifies every finding → implement confirmed fixes → log results

## Review Waves

Ordered by risk. Each wave should be completed before moving to the next.

### Wave 1: Critical — Largest attack surface

These have the most patches, GameModels, and complexity. Most likely to have bugs.

| # | Feature | Files | GameModels | Patches | Tests | Key Risk |
|---|---------|------:|----------:|--------:|------:|----------|
| 5 | **Diplomacy** | 28 | 8 | 7 | 5 | 8 GameModels touching alliances, decisions, diplomacy — game-wide AI impact |
| 6 | **FactionMap** | 46 | 5 | 10 | 7 | Largest feature. 10 patches on map/faction systems. High interaction surface. |
| 7 | **CustomBattles** | 17 | 0 | 12 | 4 | 12 patches — highest patch count. Custom factions, commanders, troops. |

**Estimated review time:** 3 sessions (1 per feature — these are too large to batch)

### Wave 2: High — GameModel-heavy, balance-critical

These override core game calculations. Wrong math = broken economy/combat/progression.

| # | Feature | Files | GameModels | Patches | Tests | Key Risk |
|---|---------|------:|----------:|--------:|------:|----------|
| 8 | **CharacterCreation** | 21 | 3 | 4 | 6 | 4 patches on CC flow. Narrative horse guard. 3 GameModels. |
| 9 | **RaceAge** | 10 | 4 | 0 | 3 | 4 GameModels (age, pregnancy, hero creation, alliance). Race lifespan math. |
| 10 | **BattleBalance** | 9 | 3 | 0 | 3 | Military power, combat simulation, party healing. Direct combat impact. |
| 11 | **Execution** | 12 | 1 | 5 | 2 | 5 patches on execution system. Relation penalty GameModel. |

**Estimated review time:** 2 sessions (batch RaceAge+BattleBalance, batch CharacterCreation+Execution)

### Wave 3: Medium — Patch-heavy or mission behaviors

These modify runtime behavior (missions, agents, combat). Harder to test, harder to debug.

| # | Feature | Files | GameModels | Patches | Tests | Key Risk |
|---|---------|------:|----------:|--------:|------:|----------|
| 12 | **HeroRace** | 18 | 0 | 6 | 4 | 6 patches on race/face gen. Race assignment correctness. |
| 13 | **Siege** | 11 | 2 | 1 | 1 | Timed defense events. 2 GameModels. Config-driven watched factions. |
| 14 | **AdvancedCombat** | 22 | 0 | 0 | 1 | SpatialGrid, BoneCollision, CustomAttacks. Mission behavior — runtime only. |
| 15 | **Warg** | 22 | 0 | 0 | 1 | Warg combat behavior. BT elements. Mission behavior. |
| 16 | **BannerInjection** | 11 | 0 | 3 | 3 | Banner injection patches. Related to BannerColorPersistence. |

**Estimated review time:** 2 sessions (batch HeroRace+Siege, batch AdvancedCombat+Warg+BannerInjection)

### Wave 4: Low — Small, service-only, or trivial

Lower risk individually. Batch into efficient review groups.

| # | Feature | Files | GameModels | Patches | Tests | Key Risk |
|---|---------|------:|----------:|--------:|------:|----------|
| 17 | **WeatherBoundsGuard** | 4 | 0 | 3 | 1 | 3 patches clamping weather values |
| 18 | **AtmospherePersistence** | 2 | 0 | 1 | 1 | Single transpiler on Mission.Initialize |
| 19 | **ShaderPrecompilation** | 5 | 0 | 1 | 1 | Single transpiler on LoadingWindowViewModel |
| 20 | **TimeAcceleration** | 11 | 0 | 0 | 1 | Service-only, no patches. Campaign speed. |
| 21 | **InitialChildGeneration** | 9 | 0 | 0 | 3 | Service-only. Child spawning logic. |
| 22 | **StartupResources** | 9 | 0 | 0 | 4 | Behavior-only. Startup config loading. |
| 23 | **MainMenuCustomizer** | 5 | 0 | 0 | 1 | UI-only. Hide/rename menu items. |
| 24 | **BattleScenes** | 3 | 0 | 3 | 0 | **DISABLED + NO TESTS.** Check if dead code. |
| 25 | **Encyclopedia** | 1 | 1 | 0 | 1 | Single GameModel override. Trivial. |

**Estimated review time:** 2 sessions
- Batch A: transpilers (AtmospherePersistence + ShaderPrecompilation + WeatherBoundsGuard)
- Batch B: services (TimeAcceleration + InitialChildGeneration + StartupResources + MainMenuCustomizer + Encyclopedia + BattleScenes)

### Wave 5: Infrastructure

Cross-cutting code used by all features.

| # | Component | Files | Key Risk |
|---|-----------|------:|----------|
| 26 | **Adapters** (Main/Adapters/) | 42 | Boundary layer. Wrong adapter = wrong data in every feature. |
| 27 | **Core** (Main/Core/) | 11 | IoC, logging, path service. Foundation. |
| 28 | **SubModule.cs + IoC.cs** | 2 | Registration order, initialization timing. |

**Estimated review time:** 1 session

---

## Prompt Strategy Per Wave

Each wave targets different risk vectors. The v4 template is the base; customize per feature type.

| Wave | Primary Focus | Vanilla Methods to Decompile | Config Cross-Ref |
|------|--------------|------------------------------|-----------------|
| 1 (Critical) | Patch targets exist in v1.3.15, GameModel base call correctness | All 15 patched methods + 13 overridden GameModel methods | Faction IDs, settlement IDs |
| 2 (High) | GameModel math correctness, feat interaction | 7 overridden GameModel methods | Race configs, CC narrative data |
| 3 (Medium) | Mission behavior lifecycle, agent state, race assignment | Patch targets for 9 patches | Troop IDs, scene configs |
| 4 (Low) | Transpiler IL correctness, dead code detection | 5 transpiler/patch targets | Startup configs |
| 5 (Infra) | Adapter completeness, IoC registration, init order | N/A | N/A |

---

## Per-Review Checklist

Before each review:
- [ ] Gather complete file list for the feature
- [ ] Identify vanilla methods to decompile (GameModel bases, patch targets)
- [ ] Identify config files to cross-reference
- [ ] Check if feature docs exist (add READ FIRST if so)
- [ ] Check what's already good (test coverage, architecture) to avoid pattern-compliance padding
- [ ] Write prompt using v4+ template from REVIEW-GUIDE.md
- [ ] Include 2-3 concrete scenarios with expected numbers

After each review:
- [ ] Claude reads every file Codex references
- [ ] Decompile vanilla targets for each HIGH/CRITICAL finding
- [ ] Check cross-file convention consistency
- [ ] Check fail-safe default consistency across patches
- [ ] Implement confirmed fixes
- [ ] Update REVIEW-LOG.md with scores
- [ ] Update REVIEW-GUIDE.md if new failure pattern discovered

---

## Success Criteria

| Metric | Current | Target | How |
|--------|---------|--------|-----|
| Codex accuracy | 44% | >60% | Better prompts, required evidence |
| False positive rate | 33% | <20% | "Decompile before claiming bug" rule |
| Miss rate | 60% | <30% | Claude's own analysis catches what Codex misses |
| Features reviewed | 4/25 | 25/25 | Systematic wave execution |
| Decompiled code in output | 1/4 reviews | Every review | v4 quality gates |

---

## Tracking

| Wave | Features | Status | Bugs Found | Bugs Fixed |
|------|----------|--------|------------|------------|
| Pre | CulturalFeats | Done | 3 | 3 |
| Pre | BannerColorPersistence | Done | 4 | 4 |
| Pre | ArmyTargeting | Done (clean) | 0 | 0 |
| Pre | TroopProgression+TroopWeight | Done | 3 | 3 |
| 1 | Diplomacy | Pending | | |
| 1 | FactionMap | Pending | | |
| 1 | CustomBattles | Pending | | |
| 2 | CharacterCreation | Pending | | |
| 2 | RaceAge + BattleBalance | Pending | | |
| 2 | Execution | Pending | | |
| 3 | HeroRace + Siege | Pending | | |
| 3 | AdvancedCombat + Warg + BannerInjection | Pending | | |
| 4A | Transpilers (Atmosphere + Shader + Weather) | Pending | | |
| 4B | Services (Time + ChildGen + Startup + Menu + Encyclopedia + BattleScenes) | Pending | | |
| 5 | Infrastructure (Adapters + Core + SubModule) | Pending | | |
