# Codex Adversarial Review Guide

How to write effective prompts, what to verify, and what we've learned.

## Process Overview

```
1. Choose feature to review (highest risk-per-line-of-code)
2. Gather file list + identify vanilla targets to decompile
3. Write prompt using template below
4. Dispatch: /codex:adversarial-review --background
5. Retrieve: /codex:result
6. Claude critically reviews Codex output against actual source
7. Implement confirmed fixes
8. Log results in REVIEW-LOG.md
```

**Critical rule:** Claude ALWAYS reviews the Codex output. Codex findings are hypotheses, not facts. Every finding must be verified against source code before implementing.

---

## Prompt Formatting Note

**Avoid indented continuation lines** in prompts sent via `/codex:adversarial-review`. Leading whitespace gets backslash-escaped, triggering a confirmation prompt. Use flat formatting:
- No leading spaces on lines inside sections
- Use `--` or blank lines as visual separators instead of indentation
- Lists use `a)` `b)` `c)` at the start of the line, not indented under a header

## Prompt Template (v5)

```
/codex:adversarial-review --background

Adversarial review of {FeatureName}.

{1-2 sentences: what the feature does, its risk profile, what's already good}

TAOM KINGDOM MAPPING (prevents false positives from ID confusion):
  empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan,
  battania=Dunland(alt), aserai=Harad, khuzait=Easterlings,
  sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell,
  lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard,
  gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar,
  shaghana=Harad(sub), abanissa=Harad(sub)

READ FIRST (required context):
- docs/features/{feature-name}.md
- {any config files: JSON, XML}

FILES (service — business logic):
{list service files}

FILES (entry points — thin, delegate to service):
{list GameModel and Harmony patch files}

FILES (config):
{list JSON/XML config files}

FILES (tests):
{list test files with count and coverage note}

=== REQUIRED SECTIONS (missing section = incomplete review) ===

SECTION 1: VANILLA CODE
Read these files from E:\Decompiled_Bannerlord\ and paste the relevant
methods into your output as ``` code blocks:
  - Find {VanillaClass}.cs in {Modules|Campaign|Core}/ — paste {MethodName}()
  - Find {VanillaClass2}.cs — paste {MethodName2}()

This section MUST contain ``` code blocks with decompiled C#. Prose
descriptions of vanilla behavior are NOT sufficient — prior reviews
described vanilla behavior without reading the code and produced false
positives. If you cannot find the file, say so explicitly.

SECTION 2: VANILLA ANALYSIS
Using the code from Section 1, answer:
  a) {specific question about vanilla behavior}
  b) {specific question about how TAOM interacts with vanilla}
Reference specific line numbers from the code you pasted.

SECTION 3: {FEATURE-SPECIFIC DEEP ANALYSIS}
{Concrete scenarios with expected outputs, math walkthroughs, or
 IL verification — whatever is the highest-risk area for this feature}
  a) {specific scenario with numbers — show the formula step by step}
  b) {specific scenario with numbers}
  c) {edge case}

SECTION 4: {SECOND ANALYSIS AREA — e.g., CONFIG VALIDATION}
{Config validation, convention consistency, etc.}
  a) {specific check with cross-reference file path}
  b) {specific check}
Cross-reference config values against {specific file path, e.g.,
Main/_Module/ModuleData/settlements.xml} — do not claim validity
without checking.

SECTION 5: FINDINGS OR OBSERVATIONS
If bugs found — each finding MUST include:
  - TAOM code (file:line)
  - Vanilla code (quote from Section 1)
  - Evidence of divergence
  - Severity: CRITICAL / HIGH / MEDIUM / LOW

If approve verdict — you MUST still provide an OBSERVATIONS subsection
listing things worth noting even if not bugs (e.g., high multiplier
magnitudes, silent filtering, exception swallowing, design tradeoffs).
An approve with zero observations suggests shallow analysis.

=== QUALITY GATES ===

Your review is INCOMPLETE if:
  - Section 1 contains no ``` code blocks with decompiled C#
  - Section 4 claims validity without showing cross-reference evidence
  - All findings are the same severity (vary your calibration)
  - Section 5 has no observations on an approve verdict
  - A finding claims "this is wrong" without checking feature docs for
    design intent (Wave 1 produced a false positive from misreading
    kingdom mapping — always check docs/features/ before flagging)

Lessons from prior reviews — do NOT repeat these mistakes:
{paste 2-3 concrete examples from Codex Failure Patterns section}

DO NOT flag architecture/pattern compliance {if feature is well-architected}.

Output to: docs/reviews/codex-adversarial-{feature}-{date}.md
```

---

## Prompt Design Principles

### What makes Codex find real bugs vs. surface noise

| Principle | Why | Example |
|-----------|-----|---------|
| **Point to E:\Decompiled_Bannerlord\** | Codex has pre-decompiled source but won't use it unless told | "Find DefaultPartySpeedCalculatingModel.cs in Modules/" |
| **Require vanilla code in output** | Forces Codex to actually read vanilla, not guess | "Include the decompiled C# in your output" |
| **Give concrete scenarios** | Forces math walkthrough, catches formula bugs | "Mordor army besieging town_EW3, position 0 of 4, show the multiplier" |
| **Name the required sections** | Codex skips hard sections silently; named sections make gaps visible | "REQUIRED SECTIONS (missing = incomplete)" |
| **Reference feature docs** | Codex needs design intent to distinguish bugs from features | "READ FIRST: docs/features/army-targeting.md" |
| **Say what's already good** | Prevents Codex from filling the review with easy pattern violations | "100% test coverage, proper service/adapter separation" |
| **Include prior failure examples** | Concrete failures are stronger than abstract "DO NOT" rules | "Codex called X a bug but vanilla uses the same pattern" |
| **Vary severity explicitly** | Codex defaults to everything-is-HIGH | "If everything is HIGH, your calibration is off" |
| **Require verification artifacts** | Codex describes instead of showing; prose is unfalsifiable | "Your output MUST contain ``` code blocks with decompiled C#" |
| **Separate "show" from "analyze"** | Codex skips showing code if it can jump to conclusions | "Step A: paste the code. Step B: answer questions about it." |
| **Require observations on approve** | Clean verdicts need evidence of depth, not just absence of findings | "OBSERVATIONS section required even for approve verdicts" |

### What wastes Codex's time

| Anti-pattern | Why it fails |
|-------------|-------------|
| "Check for ADR violations" on a well-architected feature | Codex finds pattern violations and inflates them to CRITICAL |
| "Decompile X" without pointing to E:\Decompiled_Bannerlord\ | Codex often skips decompilation entirely |
| Generic focus areas ("null handling", "thread safety") | Gets generic answers; Codex checks superficially |
| No feature documentation reference | Codex can't distinguish design intent from bugs |
| Same severity guidance as AGENTS.md defaults | AGENTS.md rates ADR violations as CRITICAL; prompt needs to override for mature features |
| "Include decompiled code" without structural enforcement | Codex ignored this instruction in 3/3 reviews — words alone don't work |
| No config cross-reference file path | "Validate config" without "against settlements.xml" lets Codex claim validity without checking |

---

## Codex Failure Patterns (observed)

Track these to prevent repeats. Each entry: what went wrong, which review, how to prevent.

### FP-1: False positive from not reading vanilla code
**Review:** CulturalFeats (2026-04-05)
**What happened:** Codex called `characterObject.IsMounted` in TaomPartyTroopUpgradeModel a bug, claiming it should be `upgradeTarget.IsMounted`. Vanilla Bannerlord's `KhuzaitRecruitUpgradeFeat` uses the exact same `characterObject.IsMounted` check.
**Prevention:** Require "VANILLA REFERENCE" section with specific decompiled file paths. Require decompiled code in output.

### FP-2: Skipped hardest analysis silently
**Review:** BannerColorPersistence (2026-04-05)
**What happened:** Prompt's #1 focus area was transpiler IL verification. Codex skipped it entirely — no mention, no acknowledgment, no "I couldn't verify this." Found 3 surface-level scope concerns instead.
**Prevention:** Use "REQUIRED SECTIONS (missing = incomplete)" format. Name each section explicitly.

### FP-3: ADR violations inflated to CRITICAL/no-ship
**Review:** CulturalFeats (2026-04-05)
**What happened:** CulturalFeats GameModels have feat-check logic directly in models (no service extraction). Codex rated this CRITICAL/no-ship. The "logic" is `if (hasFeat) addBonus` — one-liners in <55-line files. This is tech debt, not a ship-blocker.
**Prevention:** Tell Codex what's already good. "This feature is well-architected" prevents pattern-compliance padding. Override AGENTS.md severity for mature features.

### FP-4: Design intent questions presented as confirmed bugs
**Review:** BannerColorPersistence (2026-04-05)
**What happened:** Codex flagged global clan color scope as a "regression." In TAOM's LOTR setting, global scope is likely intentional — each faction keeps its iconic colors regardless of kingdom. Codex didn't consider this.
**Prevention:** Reference feature docs so Codex understands design intent. Add "READ FIRST" section.

### FP-5: Uniform severity (everything HIGH)
**Review:** Both reviews (2026-04-05)
**What happened:** CulturalFeats: 1 CRITICAL + 2 HIGH + 1 MEDIUM. BannerColorPersistence: 3 HIGH. No LOW findings in either. Real bugs ranged from LOW (null guard) to HIGH (unconditional forest speed).
**Prevention:** Explicit instruction: "If everything is HIGH, your calibration is off. Vary severity."

### FP-6: Approve without evidence
**Review:** ArmyTargeting (2026-04-05)
**What happened:** Codex issued a correct "approve" verdict but produced no decompiled vanilla code (despite explicit instruction), claimed config IDs were valid without cross-referencing settlements.xml, and described vanilla behavior in prose instead of showing code. The verdict was right but indistinguishable from a rubber stamp.
**Prevention:** Require "VERIFICATION ARTIFACTS" — specific code blocks that must appear in the output. For approve verdicts, require an "OBSERVATIONS" section (things worth noting even if not bugs). An approve with zero decompiled code is incomplete regardless of verdict.

### FP-7: Wrong kingdom mapping assumption
**Review:** Diplomacy (2026-04-05)
**What happened:** Codex assumed `empire`=Rohan and `vlandia`=Arthedain, then flagged phase-1 war pairs as wrong. Actual mapping: `empire`=Dunland, `vlandia`=Rohan. The config was correct all along. False positive from not having the TAOM kingdom→LOTR mapping.
**Prevention:** Include "TAOM KINGDOM MAPPING" reference block in every prompt. Prevents ID-to-faction confusion.

---

## Real Bugs Found (by source)

Track which review source (Codex vs. Claude) found each real bug.

### Found by Codex, confirmed by Claude
| Bug | Feature | Severity | What Codex got right |
|-----|---------|----------|---------------------|
| Unique secondary-color is a no-op | BannerColorPersistence | MEDIUM | Correctly identified the sentinel fallthrough (but understated — it's a no-op in ALL cases) |

### Found by Claude, missed by Codex
| Bug | Feature | Severity | Why Codex missed it |
|-----|---------|----------|-------------------|
| Forest speed applied unconditionally | CulturalFeats | HIGH | Didn't decompile vanilla to see terrain gate |
| Caravan EffectBonus convention (0.75 vs -0.25) | CulturalFeats | HIGH | Didn't check cross-feat convention consistency |
| Fail-safe `?? true` defaults | BannerColorPersistence | HIGH | Didn't compare fail-safe patterns across patches |
| `GetUniqueIconColor` is complete no-op (both cases) | BannerColorPersistence | MEDIUM | Only caught the overlap case, missed non-overlap no-op |
| Layer limit transpiler `?? true` default | BannerColorPersistence | MEDIUM | Didn't analyze the transpiler at all |

### False positives by Codex, caught by Claude
| Claim | Feature | Why it was wrong |
|-------|---------|-----------------|
| `characterObject.IsMounted` should be `upgradeTarget` | CulturalFeats | Vanilla uses same check |
| Global drift guard is a "regression" | BannerColorPersistence | Likely intentional for LOTR |
| Global clan color scope is a "regression" | BannerColorPersistence | Same design intent question |

---

## Feature Risk Assessment

Use this to prioritize which feature to review next.

| Factor | Weight | How to assess |
|--------|--------|--------------|
| **Recency** | High | Newest code has least testing in the wild |
| **Harmony surface area** | High | More patched classes = more vanilla-divergence risk |
| **Transpilers** | Very High | IL modification is the most fragile patch type |
| **Math complexity** | Medium | Multi-factor calculations with stacking multipliers |
| **Config-driven** | Medium | Typos in config silently produce wrong behavior |
| **Existing test coverage** | Inverse | Low coverage = higher review value |
| **Architecture maturity** | Inverse | Well-architected features have fewer pattern bugs (focus on logic instead) |

---

## Post-Review Checklist

After receiving Codex output, Claude must:

- [ ] Read every file Codex references — verify claims against actual code
- [ ] For each HIGH/CRITICAL finding: decompile vanilla target and compare
- [ ] Check for convention inconsistencies Codex missed (cross-file patterns)
- [ ] Check for fail-safe/default value consistency across the feature's patches
- [ ] Check for no-op code paths (feature that does nothing in all cases)
- [ ] Verify any proposed remediation actually fixes the issue
- [ ] Log results in REVIEW-LOG.md
