---
name: review-codex
description: Write a Codex adversarial review prompt, OR verify an existing Codex review and implement fixes
argument-hint: "[feature-name] or [path-to-codex-review.md]"
---

# Codex Adversarial Review Pipeline

This skill handles BOTH sides of the Codex review process:
- If `$ARGUMENTS` is a feature name (no `.md` extension): **write a Codex prompt** for that feature
- If `$ARGUMENTS` is a path to an existing review `.md` file: **verify and implement fixes**
- If `$ARGUMENTS` is empty: look in `docs/reviews/` for the most recently modified `codex-adversarial-*.md` and verify it

## Mode A: Write Codex Prompt (argument is a feature name)

### A1: Gather feature files

Find all files for the feature:
- `Main/Features/{feature}/` -- all .cs files (services, hooks, models, UI, IoC)
- `TAOM.Tests/Features/{feature}/` -- all test files
- `Main/_Module/ModuleData/` -- any config files (JSON, XML) used by the feature
- `Main/Adapters/` -- any adapters used by this feature
- `docs/features/{feature}.md` -- feature documentation if it exists
- Check `Main/SubModule.cs` and `Main/IoC.cs` for registration lines

Count files, identify GameModel overrides, Harmony patches, config files, and test coverage.

### A2: Identify vanilla targets

For each Harmony patch, identify the vanilla class and method being patched.
For each GameModel, identify the vanilla base class being overridden.
Map these to paths in `E:\Decompiled_Bannerlord\` for the prompt.

### A3: Identify Known Suspects

Run a quick analysis of the feature for likely issues:
- Check all config files for kingdom/culture IDs -- do they match the cheatsheet?
- Check all Harmony patches for fail-safe defaults (`?? true` vs `?? false`)
- Check for dead code (methods defined but never called)
- Check for convention consistency with other TAOM features
- Check any reflection usage for correct field/type targets

List 3-6 Known Suspects with specific hypotheses for Codex to CONFIRM or DISPUTE.

### A4: Write the prompt

Use the v6 template from `docs/reviews/REVIEW-GUIDE.md`. The prompt must include:

1. **Feature description** (1-2 lines: what it does, risk profile, what's already good)
2. **TAOM ID CHEATSHEET** (always include -- prevents false positives):
   Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
   Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
   Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
   NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".
3. **READ FIRST** section (feature docs, config files)
4. **Known Suspects** section (from A3 analysis)
5. **File lists** (grouped by category: services, entry points, config, tests)
6. **REQUIRED SECTIONS** with feature-specific questions:
   - SECTION 1: VANILLA CODE (decompile targets, paste as code blocks)
   - SECTION 2: Feature-specific deep analysis (concrete scenarios with numbers)
   - SECTION 3: CONFIG CROSS-REFERENCE (cross-ref IDs against actual files)
   - SECTION 4: FINDINGS OR OBSERVATIONS
7. **QUALITY GATES** (Section 1 must have code blocks, config must be cross-referenced, etc.)
8. **Prior review lessons** (successes + failures from our 18-review history)
9. **Output to:** `docs/reviews/codex-adversarial-{feature}-{date}.md`

CRITICAL formatting rules:
- Flat formatting only -- NO indented continuation lines (triggers backslash-escape prompt)
- Use `--` instead of `—` for dashes
- Lists use `a)` `b)` `c)` at start of line, not indented

### A5: Display the prompt

Output the complete prompt for the user to copy and dispatch to Codex CLI.

Then tell the user:
```
Dispatch this prompt to Codex:
  /codex:adversarial-review --background

When Codex finishes writing to docs/reviews/codex-adversarial-{feature}-{date}.md, run:
  /review-codex docs/reviews/codex-adversarial-{feature}-{date}.md
```

---

## Mode B: Verify Codex Review (argument is a .md file path)

### Context

Read these first for process knowledge:
- `docs/reviews/REVIEW-GUIDE.md` -- prompt template, failure patterns, success patterns
- `docs/reviews/REVIEW-LOG.md` -- scoring history (18 reviews, 81% accuracy, 9% FP rate)

Key lessons from 18 prior reviews:
- Codex accuracy improved from 33% (v1) to 81% (v6) through structured prompts
- After v4 prompts, Codex produced 0 false positives across 12 reviews
- Most common Codex mistakes: assumed empire=Rohan (it's Dunland), flagged vanilla-matching code as bugs, skipped hard sections silently, claimed "config looks valid" without checking
- Most common real bugs found: config ID mismatches, missing vanilla gates, stale state/lifecycle, dead/no-op code, convention inconsistencies

### B1: Read the Review

Read the Codex review file. Identify:
- Total number of findings and their severities
- Whether the review has a "Known Suspects" section (if so, these are highest priority)
- Whether the review includes code blocks from BOTH codebases (quality indicator)
- Whether config was cross-referenced against actual files (quality indicator)

### B2: Verify Each Finding

For EACH finding in the review:

**B2a: Read the TAOM source**
Read the exact file and line Codex references. Does the code actually do what Codex claims?

**B2b: Verify "missing" claims**
If Codex claims TAOM is missing something, grep the codebase. Don't trust "I didn't find it" -- actually search.

**B2c: Decompile vanilla targets**
For Harmony patch or GameModel findings, check the vanilla target in `E:\Decompiled_Bannerlord\` (organized by: Campaign/, MountAndBlade/, Modules/, Core/, UI/). Verify method signatures and behavior match what Codex claims.

**B2d: Check TOR comparison fairness**
If Codex compares against TOR_Core, note that TOR targets older Bannerlord. Flag any API differences.

**B2e: Check what Codex missed**
For each feature area, also check:
- Cross-file convention consistency (do all config IDs match real TAOM kingdom/culture IDs?)
- Fail-safe defaults (null-coalescing `?? true` vs `?? false` -- which matches intent?)
- No-op code paths (code that exists but does nothing)
- Dead config values (defined but never loaded)
- Stale state across lifecycle boundaries (save/load, mission end, kingdom change)

### B3: Verify Known Suspects (if present)

If the review has a "Known Suspects" section where Codex was asked to CONFIRM or DISPUTE pre-identified issues:
- Verify each verdict independently by reading the source
- These are typically the highest-value findings
- Codex has been wrong about these -- don't trust the verdict without checking

### B4: Kingdom/Culture ID Cross-Reference

For any finding involving config IDs, use this reference:

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale

CRITICAL: "rohan" is NOT a valid ID anywhere. "dol_guldur" is NOT valid -- use "dolguldur". Kingdom IDs and culture IDs differ.

### B5: Produce Assessment

Output a table of ALL findings:

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|

Then categorize:

**Confirmed bugs (implement these)**
For each, specify: file, line, what to change, why.

**False positives (do not implement)**
For each, explain why Codex was wrong.

**Design questions (need user input)**
For each, explain the trade-off and what decision is needed.

**Things Codex missed**
Any additional bugs you found that Codex didn't catch.

### B6: Implement Confirmed Fixes

For each confirmed bug:
1. Make the code change
2. Run `dotnet build TAOM.Tests` to verify compilation
3. Run `dotnet test TAOM.Tests` to verify tests pass
4. If a test needs updating due to changed behavior, update it

### B7: Update Review Log

After all fixes are implemented:
1. Add an entry to `docs/reviews/REVIEW-LOG.md` with the review number, date, feature, findings table, and scores
2. Update the metrics (accuracy rate, false positive rate, miss rate)
3. If you discovered a new Codex failure pattern, add it to `docs/reviews/REVIEW-GUIDE.md`

---

## Rules

- NEVER implement a fix without reading the source file first
- NEVER agree with a Codex finding just because it sounds plausible -- verify
- Decompile vanilla targets for ALL Harmony patch and GameModel findings
- Config cross-reference is mandatory, not optional
- When in doubt about design intent, flag for user input rather than guessing
- Build and test after EVERY batch of fixes
- Flat formatting in prompts -- no indented continuation lines
