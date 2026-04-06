# Codex Adversarial Review Log

Running scorecard of all reviews. Updated after each review cycle.

## Summary

| # | Date | Feature | Codex Verdict | Claude Verdict | Real Bugs | False Positives | Missed Bugs | Prompt Version |
|---|------|---------|--------------|----------------|-----------|-----------------|-------------|----------------|
| 1 | 2026-04-05 | CulturalFeats | no-ship | partial-agree | 1 confirmed | 1 | 2 | v1 (basic) |
| 2 | 2026-04-05 | BannerColorPersistence | no-ship | partial-agree | 1 (understated) | 2 | 4 | v2 (improved) |
| 3 | 2026-04-05 | ArmyTargeting | approve | agree (shallow) | 0 | 0 | 0 | v3 (required sections) |
| 4 | 2026-04-05 | TroopProgression | no-ship | agree | 2 confirmed + 1 valid divergence | 0 | 0 | v4 (verification artifacts) |
| 5 | 2026-04-05 | Diplomacy+Execution | no-ship | agree | 4 confirmed + 1 valid | 0 | 0 | v4 |
| 6 | 2026-04-05 | FactionMap | no-ship | agree | 2 confirmed | 0 | 0 | v4 |
| 7 | 2026-04-05 | CustomBattles | no-ship | agree | 1 confirmed + 1 valid concern | 0 | 0 | v4 |
| 8 | 2026-04-05 | CharacterCreation | no-ship | agree | 1 confirmed + 1 valid | 0 | 0 | v5 |
| 9 | 2026-04-05 | RaceAge | no-ship | design questions | 3 valid (need design input) | 0 | 0 | v5 |
| 10 | 2026-04-05 | BattleBalance | no-ship | agree | 3 confirmed | 0 | 0 | v5 |
| 11 | 2026-04-05 | HeroRace | no-ship | agree | 3 confirmed | 0 | 0 | v6 |
| 12 | 2026-04-05 | Siege+BannerInjection | no-ship | agree (1 deferred) | 1 confirmed + 1 valid | 0 | 0 | v6 |
| 13 | 2026-04-05 | AdvancedCombat+Warg | no-ship | agree | 4 confirmed | 0 | 0 | v6 |

## Metrics

**Codex accuracy rate:** 26 real findings / 34 total findings = 76% (↑ from 69%)
**Codex miss rate:** 6 missed bugs / 32 total real bugs = 19% (↓ from 25%)
**False positive rate:** 4 false positives / 34 findings total = 12% (↓ from 15%)
**Clean feature detection:** 1/1 (ArmyTargeting correctly approved)

**v4 prompt batch (reviews 4-7):** 10 findings, 9 confirmed, 0 false positives = **90% accuracy**
**v5 prompt batch (reviews 8-10):** 8 findings, 7 confirmed + 1 FP-adjacent, 0 false positives = **88% accuracy**

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

## Review #3: ArmyTargeting

**Date:** 2026-04-05
**Prompt version:** v3 (required sections, `E:\Decompiled_Bannerlord\` paths, concrete math scenarios)
**Report:** [codex-adversarial-army-targeting-2026-04-05.md](codex-adversarial-army-targeting-2026-04-05.md)

### Codex Findings (0)

Verdict: **approve** — "No blocking no-ship case."

### Claude Verification

| Check | Codex Claim | Claude Verified? |
|-------|-------------|-----------------|
| Math scenario (a): committed + priority pos 0 | 12.0× | Yes — traced formula, correct |
| Math scenario (b): priority pos 2/4 | 1.667× | Yes — interpolation correct |
| Math scenario (c): non-priority fallthrough | 1.0× | Yes — falls through to vanilla |
| Math scenario (d): CommitmentMultiplier=0 | MCM range prevents | Yes — range is 1.0-10.0 |
| Config settlement IDs valid | "All follow valid naming" | Yes — all 67 IDs verified against settlements.xml |
| Decompiled vanilla code shown | Described in prose | **No — third consecutive failure.** Prose descriptions only, no C# code blocks. |

### Observations Codex missed (not bugs, but a thorough review would note)

| Observation | Location | Impact |
|-------------|----------|--------|
| `BuildFloatIndex` silently drops multipliers ≤1.0 | ArmyTargetingService.cs:87 | Config values ≤1.0 are silently ignored — no feedback |
| Combined multiplier can reach 18× | GameModel line 40 | 4.0 × 3.0 × 1.5 = 18× on committed top-priority targets |
| Harmony patch swallows all exceptions | Patch.cs:42-44 | `catch (Exception) {}` hides service bugs during dev |
| Strength inflation bypasses vanilla strength gate | TaomTargetScoreModel.cs:27 | `ourStrength * 2.0` lets evil factions besiege what vanilla would reject |

### Prompt Lessons
- v3's required sections and concrete math scenarios produced the first correct verdict
- Decompiled code STILL not shown despite explicit instruction — Codex consistently avoids this
- "Approve" verdicts need evidence of depth — an approve with no analysis is indistinguishable from a skip
- Config validation was claimed but not evidenced — Codex didn't cross-reference against settlements.xml

---

## Review #4: TroopProgression + TroopWeight

**Date:** 2026-04-05
**Prompt version:** v4 (verification artifacts, split show/analyze, quality gates)
**Report:** [codex-adversarial-troop-progression-2026-04-05.md](codex-adversarial-troop-progression-2026-04-05.md)

### Codex Findings (3)

| # | Severity | Finding | Claude Assessment |
|---|----------|---------|-------------------|
| 1 | HIGH | Garrison wage feats apply to any party in settlement, not just garrisons | **Confirmed bug** — vanilla gates behind `mobileParty.IsGarrison`. Decompilation verified. |
| 2 | HIGH | NumberOfRegularMembers uses wounded-ratio approximation | **Valid but overstated** — math error is real for asymmetric weights. Downgrade to MEDIUM (uncommon scenario). |
| 3 | MEDIUM | Rohan mounted wage uses headcount ratio, vanilla uses wage share | **Valid divergence** — confirmed by decompilation. Design choice with lower practical impact. |

### Bugs Codex Missed (0)

Claude found no additional bugs. First review where Codex found everything.

### Prompt Lessons (v4 improvements that worked)
- Decompiled code finally shown (partial — "truncated by format" but present)
- Evidence per finding produced zero false positives for the first time
- Quality gates and required sections prevented shallow analysis
- Observations section populated with useful notes

---

## Prompt Evolution

| Version | Used In | Key Changes | Impact |
|---------|---------|-------------|--------|
| v1 | CulturalFeats | ADR focus, generic focus areas, no decompilation guidance | 33% accuracy, 1 false positive |
| v2 | BannerColorPersistence | Feature-specific focus, DO NOT section, decompilation requested | Still 33% accuracy — Codex skipped hard analysis |
| v3 | ArmyTargeting | Required sections, `E:\Decompiled_Bannerlord\` paths, concrete scenarios, READ FIRST docs, prior failure examples | Correct verdict but shallow — no decompiled code shown, config not cross-referenced |
| v4 | TroopProgression, Wave 1 | Verification artifacts, split "show code" from "answer questions", config cross-reference with file path, approve-verdict evidence requirement | 90% accuracy on v4 batch, 0 false positives in reviews 4-6, 1 FP in review 7 |
| v5 | Wave 2 | Kingdom mapping reference, design-intent gate, flat formatting, FP-7 lesson | 88% accuracy, config ID mismatches caught (rohan/vlandia, dol_guldur/dolguldur) |
| v6 | Wave 3 (planned) | Config ID cross-ref as standard section, culture-to-kingdom ID cheatsheet, "dead config" detection section, success patterns from v5 | Target: maintain 88%+ |

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

### v3 → v4 changes
- Added "VERIFICATION ARTIFACTS" — Codex must produce code blocks, not prose descriptions
- Split vanilla analysis into "SHOW the code" + "ANSWER questions about it" (two separate steps)
- Config validation now requires cross-referencing against a specific file path
- Added approve-verdict evidence requirement: "An approve with no decompiled code is incomplete"
- Added "OBSERVATIONS" section requirement for approve verdicts (things worth noting even if not bugs)

### v5 → v6 changes
- Added TAOM ID CHEATSHEET — culture StringIds AND kingdom StringIds in one block (prevents rohan/vlandia-type mismatches)
- Added "DEAD CONFIG DETECTION" as standard check — config values that exist but are never read at runtime
- Config cross-reference is now a REQUIRED section, not optional (caught 5+ bugs across waves 1-2)
- Added success patterns to prior-review-lessons (what WORKED, not just what failed)
- Flat formatting standard (no indented continuation lines — prevents backslash-escape prompts)
