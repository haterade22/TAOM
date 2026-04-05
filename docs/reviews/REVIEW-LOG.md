# Codex Adversarial Review Log

Running scorecard of all reviews. Updated after each review cycle.

## Summary

| # | Date | Feature | Codex Verdict | Claude Verdict | Real Bugs | False Positives | Missed Bugs | Prompt Version |
|---|------|---------|--------------|----------------|-----------|-----------------|-------------|----------------|
| 1 | 2026-04-05 | CulturalFeats | no-ship | partial-agree | 1 confirmed | 1 | 2 | v1 (basic) |
| 2 | 2026-04-05 | BannerColorPersistence | no-ship | partial-agree | 1 (understated) | 2 | 4 | v2 (improved) |

## Metrics

**Codex accuracy rate:** 2 real bugs found / 6 findings total = 33%
**Codex miss rate:** 6 missed bugs / 8 total real bugs = 75%
**False positive rate:** 3 false positives / 6 findings total = 50%

Target: accuracy >60%, miss rate <30%, false positives <20%

---

## Review #1: CulturalFeats

**Date:** 2026-04-05
**Prompt version:** v1 (basic — ADR-focused, no decompilation guidance)
**Report:** [codex-adversarial-cultural-feats-2026-04-05.md](codex-adversarial-cultural-feats-2026-04-05.md)

### Codex Findings (4)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | CRITICAL | GameModel entry points contain business logic (ADR-002/ADR-007) | **Overstated** — one-liner feat checks in <55-line files. Tech debt, not ship-blocker. Downgrade to LOW-MEDIUM. |
| 2 | HIGH | Mounted upgrade checks source troop, not target | **False positive** — vanilla `KhuzaitRecruitUpgradeFeat` uses same `characterObject.IsMounted` check |
| 3 | HIGH | Tests don't cover shipped behavior | **Partially valid** — true, but GameModel rule says thin entry points are exempt. MEDIUM. |
| 4 | MEDIUM | Null-reference if registration hook fails | **Valid but LOW** — scenario is unrealistic; `GetAllFeats()` already handles null |

### Bugs Codex Missed (2)

| Bug | Severity | Why missed |
|-----|----------|-----------|
| Forest speed bonus applied unconditionally (should be `TerrainType.Forest` only) | HIGH | Didn't decompile `DefaultPartySpeedCalculatingModel` to see vanilla terrain gate |
| Caravan `EffectBonus` convention (`0.75f` displays as "+75%" in UI) | HIGH | Didn't check cross-feat consistency of `EffectBonus` + `AdditionType` convention |

### Prompt Lessons
- ADR compliance as top focus led to pattern-violation padding
- No decompilation requirement led to the `IsMounted` false positive
- No feature-specific risk areas — generic focus produced generic results

---

## Review #2: BannerColorPersistence

**Date:** 2026-04-05
**Prompt version:** v2 (improved — feature-specific focus, DO NOT section, decompilation requested)
**Report:** [codex-adversarial-banner-color-persistence-2026-04-05.md](codex-adversarial-banner-color-persistence-2026-04-05.md)

### Codex Findings (3)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | Unique secondary-color patch falls through on overlap | **Partially valid, understated** — feature is actually a no-op in ALL cases, not just overlap. MEDIUM (dead code, not regression). |
| 2 | HIGH | Drift guard is global, not player-only | **Design question** — may be intentional for LOTR War of the Ring. MEDIUM. |
| 3 | HIGH | Persistence logic repaints AI clans | **Same design question** — global scope may be intentional. MEDIUM. |

### Bugs Codex Missed (4)

| Bug | Severity | Why missed |
|-----|----------|-----------|
| Fail-safe `?? true` in drift guard prefix blocks vanilla when uninitialized | HIGH | Didn't compare fail-safe patterns across the feature's 15 patches |
| `GetUniqueIconColor` is a complete no-op in both overlap AND non-overlap cases | MEDIUM | Only caught one branch; didn't trace both code paths |
| Layer limit transpiler `?? true` disables layer limit when uninitialized | MEDIUM | Didn't analyze the transpiler at all (skipped #1 focus area) |
| `MobilePartyVisual` patch has no category attribute (manual registration) | LOW | Didn't check patch registration consistency (actually intentional — private method) |

### Prompt Lessons (despite improvements)
- Codex still skipped the hardest analysis (transpiler IL verification) despite it being focus area #1
- "DO NOT" instructions were too weak — need concrete failure examples instead
- Codex needs `E:\Decompiled_Bannerlord\` file paths, not "decompile X" instructions
- Feature docs reference was missing — Codex couldn't distinguish design intent from bugs

---

## Prompt Evolution

| Version | Used In | Key Changes | Impact |
|---------|---------|-------------|--------|
| v1 | CulturalFeats | ADR focus, generic focus areas, no decompilation guidance | 33% accuracy, 1 false positive |
| v2 | BannerColorPersistence | Feature-specific focus, DO NOT section, decompilation requested | Still 33% accuracy — Codex skipped hard analysis |
| v3 | ArmyTargeting (planned) | Required sections, `E:\Decompiled_Bannerlord\` paths, concrete scenarios, READ FIRST docs, prior failure examples | Target: >60% accuracy |

### v1 → v2 changes
- Added feature-specific risk areas (transpilers, drift guard, scoping)
- Added "DO NOT" section for pattern compliance
- Added prior review lesson about `IsMounted` false positive
- Ordered focus areas by value

### v2 → v3 changes
- Added "REQUIRED SECTIONS" with named sections (prevents silent skipping)
- Pointed to `E:\Decompiled_Bannerlord\` instead of "decompile" (easier for Codex)
- Added "READ FIRST" for feature docs (design intent context)
- Replaced "DO NOT" with concrete failure examples (stronger)
- Added concrete math scenarios with expected outputs (forces deep analysis)
- Added config validation section (new attack vector)
- Added "If everything is HIGH, your calibration is off"
