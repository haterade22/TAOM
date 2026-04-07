---
name: review-codex
description: Critically verify a Codex adversarial review against actual source code, then implement confirmed fixes
argument-hint: "[path-to-codex-review.md]"
---

# Review Codex Output

Critically verify a Codex adversarial review and implement confirmed fixes. Codex finds real bugs AND produces false positives -- every finding must be verified against source code before implementing.

The review file to verify: `$ARGUMENTS`

If no argument provided, look in `docs/reviews/` for the most recently modified `codex-adversarial-*.md` file.

**NOTE:** This skill does NOT dispatch to Codex. The workflow is:
1. User writes a prompt using `docs/reviews/REVIEW-GUIDE.md` v6 template
2. User runs the prompt in Codex CLI (terminal) or via `/codex:adversarial-review --background`
3. Codex writes output to `docs/reviews/codex-adversarial-{feature}-{date}.md`
4. User invokes this skill: `/review-codex docs/reviews/codex-adversarial-{feature}-{date}.md`
5. This skill verifies findings, implements confirmed fixes, and updates the review log

## Context

Read these first for process knowledge:
- `docs/reviews/REVIEW-GUIDE.md` -- prompt template, failure patterns, success patterns
- `docs/reviews/REVIEW-LOG.md` -- scoring history (18 reviews, 81% accuracy, 9% FP rate)

Key lessons from 18 prior reviews:
- Codex accuracy improved from 33% (v1) to 81% (v6) through structured prompts
- After v4 prompts, Codex produced 0 false positives across 12 reviews
- Most common Codex mistakes: assumed empire=Rohan (it's Dunland), flagged vanilla-matching code as bugs, skipped hard sections silently, claimed "config looks valid" without checking
- Most common real bugs found: config ID mismatches, missing vanilla gates, stale state/lifecycle, dead/no-op code, convention inconsistencies

## Step 1: Read the Review

Read the Codex review file. Identify:
- Total number of findings and their severities
- Whether the review has a "Known Suspects" section (if so, these are highest priority)
- Whether the review includes code blocks from BOTH codebases (quality indicator)
- Whether config was cross-referenced against actual files (quality indicator)

## Step 2: Verify Each Finding

For EACH finding in the review:

### 2a: Read the TAOM source
Read the exact file and line Codex references. Does the code actually do what Codex claims?

### 2b: Verify "missing" claims
If Codex claims TAOM is missing something, grep the codebase. Don't trust "I didn't find it" -- actually search.

### 2c: Decompile vanilla targets
For Harmony patch or GameModel findings, check the vanilla target in `E:\Decompiled_Bannerlord\` (organized by: Campaign/, MountAndBlade/, Modules/, Core/, UI/). Verify method signatures and behavior match what Codex claims.

### 2d: Check TOR comparison fairness
If Codex compares against TOR_Core, note that TOR targets older Bannerlord. Flag any API differences.

### 2e: Check what Codex missed
For each feature area, also check:
- Cross-file convention consistency (do all config IDs match real TAOM kingdom/culture IDs?)
- Fail-safe defaults (null-coalescing `?? true` vs `?? false` -- which matches intent?)
- No-op code paths (code that exists but does nothing)
- Dead config values (defined but never loaded)
- Stale state across lifecycle boundaries (save/load, mission end, kingdom change)

## Step 3: Verify Known Suspects (if present)

If the review has a "Known Suspects" section where Codex was asked to CONFIRM or DISPUTE pre-identified issues:
- Verify each verdict independently by reading the source
- These are typically the highest-value findings
- Codex has been wrong about these -- don't trust the verdict without checking

## Step 4: Kingdom/Culture ID Cross-Reference

For any finding involving config IDs, use this reference:

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale

CRITICAL: "rohan" is NOT a valid ID anywhere. "dol_guldur" is NOT valid -- use "dolguldur". Kingdom IDs and culture IDs differ.

## Step 5: Produce Assessment

Output a table of ALL findings:

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|

Then categorize:

### Confirmed bugs (implement these)
For each, specify: file, line, what to change, why.

### False positives (do not implement)
For each, explain why Codex was wrong.

### Design questions (need user input)
For each, explain the trade-off and what decision is needed.

### Things Codex missed
Any additional bugs you found that Codex didn't catch.

## Step 6: Implement Confirmed Fixes

For each confirmed bug:
1. Make the code change
2. Run `dotnet build TAOM.Tests` to verify compilation
3. Run `dotnet test TAOM.Tests` to verify tests pass
4. If a test needs updating due to changed behavior, update it

## Step 7: Update Review Log

After all fixes are implemented:
1. Add an entry to `docs/reviews/REVIEW-LOG.md` with the review number, date, feature, findings table, and scores
2. Update the metrics (accuracy rate, false positive rate, miss rate)
3. If you discovered a new Codex failure pattern, add it to `docs/reviews/REVIEW-GUIDE.md`

## Rules

- NEVER implement a fix without reading the source file first
- NEVER agree with a Codex finding just because it sounds plausible -- verify
- Decompile vanilla targets for ALL Harmony patch and GameModel findings
- Config cross-reference is mandatory, not optional
- When in doubt about design intent, flag for user input rather than guessing
- Build and test after EVERY batch of fixes
