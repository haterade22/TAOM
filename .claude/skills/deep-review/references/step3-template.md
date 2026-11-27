# Step 3 template

Step 3 of `.claude/skills/deep-review/SKILL.md` compiles the review in this template. Step 4 fills in the three IMPROVEMENTS lists and the VERDICT.

## The compiled review

```
DEEP REVIEW REPORT
===================
Feature: [name or "uncommitted changes"]
Date: [today]

Scope:   [C#/C++, XML/XSLT, scripts, harness, docs; live files swept since <time>]
Blast radius: [N types checked with graphify affected, M callers outside the diff / UNCHECKED (why) / NOT IN SCOPE (XML only)]
Waves:   [which agents ran in which wave]

STANDARDS:     [PASS/FAIL — N violations / NOT IN SCOPE / SKIPPED BY TIER]
COMPATIBILITY: [PASS/FAIL — N incompatible, N unverified / NOT IN SCOPE / SKIPPED BY TIER]
EFFICIENCY:    [PASS/FAIL — N issues (H high, M medium, L low) / NOT IN SCOPE / SKIPPED BY TIER]
COMPLETENESS:  [COMPLETE/INCOMPLETE — list missing items / SKIPPED BY TIER]
DATA FLOW:     [PASS/FAIL — N gaps, N inconsistencies / SKIPPED BY TIER]
DESIGN:        [N KEEP proposals (A apply, F follow-up) / ALREADY OPTIMAL / SKIPPED BY TIER]
XML:           [PASS/FAIL — G gates (F failed, X not run), N findings / NOT IN SCOPE / SKIPPED BY TIER]
TOOLING:       [PASS/FAIL — N findings / NOT IN SCOPE / SKIPPED BY TIER]

─────────────────────────
DETAILS
─────────────────────────

[Agent 1 results — Standards]

[Agent 2 results — Compatibility]

[Agent 3 results — Efficiency]

[Agent 4 results — Completeness]

[Agent 5 results — Data Flow]

[Agent 6 results — Design & Elegance]

[Agent 7 results — XML & ModuleData Integrity, incl. OWED in-game checks]

[Tooling correctness results]

─────────────────────────
ACTION ITEMS
─────────────────────────
1. [Most critical issue first]
2. ...

─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:     [file:line, what changed, the test that proves it]
NOT APPLIED: [file:line, one-line reason each]
FOLLOW-UP:   [pre-existing code outside the change; its issue number, or why none is filed]

VERDICT: READY FOR COMMIT / NEEDS FIXES
```

- If the verdict is NEEDS FIXES, list the fixes needed in priority order.

## The Codex section (Step 0)

When Step 0 ran, the compiled review holds the Codex findings as their own section:

   ```
   CODEX REVIEW:  [PASS/ISSUES — N findings]
   [Codex findings grouped by severity]
   ```
