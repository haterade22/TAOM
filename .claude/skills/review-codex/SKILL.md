---
name: review-codex
description: Auto-detect what was built, write a Codex adversarial prompt, dispatch Codex directly via `codex exec`, then verify results when complete
argument-hint: "[optional: feature-name or path-to-review.md]"
---

# Codex Adversarial Review Pipeline

Handles the full Codex review lifecycle automatically — Claude dispatches Codex directly via the local `codex` CLI (no terminal hand-off to the user). Detects what needs reviewing from context.

**Argument handling:**
- No argument: auto-detect from git what was changed, write prompt, dispatch
- Feature name (no `.md`): write prompt for that feature, dispatch
- Path to `.md` file: verify that existing Codex review, implement fixes
- If a `codex-adversarial-*.md` file was recently created in `docs/reviews/raw/`, ask user if they want to verify it

## Codex CLI invocation contract (added 2026-05-25)

Claude has direct access to Codex via the `codex` binary (`C:\Users\mikew\AppData\Roaming\npm\codex.cmd` on Windows, `codex` on POSIX). Dispatch is a **Bash call from inside this skill** — the user does NOT open a separate terminal.

**Required pre-flight (Phase 2 only — once per skill invocation):**
```bash
codex login status
```
Expect `Logged in using ChatGPT` (or equivalent). If not logged in, stop the skill and surface the message — the user must run `codex login` themselves (interactive browser flow).

**Dispatch command (Phase 2e):**
```bash
cd "<repo-root>" && codex exec -c project_doc_max_bytes=65536 - < "<prompt-file-path>" > "<output-file-path>" 2>&1
```
- `codex exec -` reads the prompt from stdin (avoids argv length limits for large prompts).
- Output (stdout + stderr) goes to `docs/reviews/raw/codex-adversarial-{feature}-{date}.md`.
- Wrap with `run_in_background: true` on the Bash tool call — Codex with `model_reasoning_effort = "max"` typically runs 10-45 minutes. The harness notifies when the background job completes.
- Model + reasoning effort come from the repo's `.codex/config.toml` (currently `model = "gpt-5.6-sol"`, `model_reasoning_effort = "max"`, set 2026-09-05); a trusted project config outranks `~/.codex/config.toml`, so the repo pin is the one that runs. Do NOT override unless the user asks.
- Codex reads project rules from `AGENTS.md`, **but truncates it at `project_doc_max_bytes` (default 32768). AGENTS.md exceeds that, so the `-c project_doc_max_bytes=65536` override is REQUIRED** — without it Codex reviews without TAOM's Critical Rules, ADR rules, and commit conventions (they live past the cut). The project `.codex/config.toml` sets the key but is never loaded (`CODEX_HOME` unset → Codex reads `~/.codex/config.toml`), which is why the flag is passed explicitly. Confirm the fix is live with `codex debug prompt-input "hi"` (no API call): the rendered input must contain "Non-Negotiable ADR Rules". This is also why the project-declared `filesystem`/`git`/`ilspy` MCP servers are inert — they're only in the unloaded project config.

**When the background job notifies completion:**
1. Read the output file. Confirm it's a real Codex review (starts with review structure, not an error message).
2. If the output starts with `Error:` / `panic:` / login failure / empty file, fall back to manual dispatch by telling the user the failure mode.
3. Otherwise, proceed to Phase 3 (verify findings).

## Phase 1: Detect What to Review

### If no argument provided:

1. Run `git diff --name-only HEAD` and `git diff --name-only HEAD~3..HEAD` to find recently changed `.cs` files
2. Group changed files by feature directory (e.g., `Main/Features/SettlementGuards/` → SettlementGuards)
3. Check `docs/reviews/raw/` for any `codex-adversarial-*.md` file modified in the last hour
   - If found: go to **Phase 3** (verify that review)
   - If not found: go to **Phase 2** (write prompt for the most-changed feature)

### If argument is a feature name:
Go to **Phase 2** with that feature.

### If argument is a `.md` path:
Go to **Phase 3** with that file.

## Phase 2: Write Codex Prompt

### 2a: Gather feature files

Find all files for the feature:
- `Main/Features/{feature}/` — all .cs files (services, hooks, models, UI, IoC)
- `TAOM.Tests/Features/{feature}/` — all test files
- `Main/_Module/ModuleData/` — any config files (JSON, XML) used by the feature
- `Main/Adapters/` — any adapters used by this feature (grep for feature-related types)
- `docs/features/{feature}.md` — feature documentation if it exists
- Check `Main/SubModule.cs` and `Main/IoC.cs` for registration lines

Count files, identify GameModel overrides, Harmony patches, config files, and test coverage.

### 2b: Identify vanilla targets

For each Harmony patch, identify the vanilla class and method being patched.
For each GameModel, identify the vanilla base class being overridden.
Map these to paths in `E:\Decompiled_Bannerlord\` for the prompt.

### 2c: Identify Known Suspects

Run a quick analysis of the feature for likely issues:
- Check all config files for kingdom/culture IDs — do they match the cheatsheet?
- Check all Harmony patches for fail-safe defaults (`?? true` vs `?? false`)
- Check for dead code (methods defined but never called)
- Check for convention consistency with other TAOM features
- Check any reflection usage for correct field/type targets
- Check for stale state across lifecycle boundaries
- **Entity state matrix for OnGameLoaded behaviors:** If the feature has an `OnGameLoadedEvent` handler that mutates Hero/Settlement state, enumerate all possible entity states (recruited, traveling, dead, prisoner, fugitive) and check whether the mutation is guarded for each. This is a HIGH-priority suspect — Review #23 found a ship-blocking bug from this pattern.
- **Idempotent vs destructive operations:** If the feature copies a behavior pattern from another feature (e.g., "run same logic on new game and load"), check whether the operation is idempotent. Destructive operations (moving heroes, changing state) need stricter guards on the load path than their new-game counterparts.

List 3-6 Known Suspects with specific hypotheses for Codex to CONFIRM or DISPUTE.

### 2d: Write the prompt

Use flat formatting — NO indented continuation lines (triggers backslash-escape prompt). Use `--` not `—`.

The prompt must include:

1. Feature description (1-2 lines)
2. TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".
3. READ FIRST section (feature docs, config files)
4. Known Suspects section (from 2c)
5. File lists grouped by category
6. REQUIRED SECTIONS with feature-specific questions:
   - VANILLA CODE (decompile targets, paste as code blocks)
   - Feature-specific deep analysis (concrete scenarios)
   - CONFIG CROSS-REFERENCE
   - FINDINGS OR OBSERVATIONS
7. QUALITY GATES
8. Prior review lessons:
   SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
   FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
9. Output to: docs/reviews/raw/codex-adversarial-{feature}-{date}.md

### 2e: Dispatch Codex directly

1. **Write the prompt to disk** at `docs/reviews/codex-adversarial-{feature}-{date}.prompt.md` so it's reviewable + reusable.
2. **Pre-flight Codex auth** — run `codex login status` once. If not `Logged in`, stop and tell the user to `codex login`.
3. **Dispatch via Bash** in background:
   ```
   Bash tool call:
     command: cd "<repo-root>" && mkdir -p docs/reviews/raw && codex exec -c project_doc_max_bytes=65536 - < "docs/reviews/codex-adversarial-{feature}-{date}.prompt.md" > "docs/reviews/raw/codex-adversarial-{feature}-{date}.md" 2>&1
     run_in_background: true
     timeout: 600000  (10 min — Codex usually finishes inside this; harness will notify when actually done)
   ```
4. **Tell the user once** what was dispatched: feature name, prompt path, output path, expected completion window (10-45 min on `xhigh` reasoning). Do NOT poll the background job — the harness sends a notification when the job actually completes.
5. **Continue with other work or stop**. When the background notification arrives, automatically proceed to Phase 3 by reading the output file. Do NOT re-prompt the user to "run /review-codex again" — Claude continues the lifecycle itself.

Fallback path (only if direct dispatch fails — `codex` binary missing, auth expired, sandbox refuses):

```
Tell the user:
  Direct Codex dispatch failed: <exact error from Bash output>.
  Manual fallback:
    1. Open a terminal
    2. cd <repo-root>
    3. mkdir -p docs/reviews/raw && codex exec -c project_doc_max_bytes=65536 - < "docs/reviews/codex-adversarial-{feature}-{date}.prompt.md" > "docs/reviews/raw/codex-adversarial-{feature}-{date}.md"
    4. When done, re-invoke /review-codex with the .md file path as argument
```

## Phase 3: Verify Codex Review

Read process docs first:
- `docs/reviews/REVIEW-GUIDE.md` — failure patterns, success patterns
- `docs/reviews/REVIEW-LOG.md` — scoring history

### 3a: Read the Review

Read the Codex review file. Identify:
- Total findings and severities
- Known Suspects section (highest priority if present)
- Whether code blocks from both codebases are present (quality indicator)
- Whether config was cross-referenced (quality indicator)

### 3b: Verify Each Finding

**A Codex finding is a hypothesis, not a verdict** (per `.claude/rules/evidence-over-claims.md`; review accuracy is ~95%, not 100% — `feedback_audit_findings_not_always_correct.md`). Verify EVERY finding against the TAOM source and decompiled vanilla BEFORE implementing it — re-read the actual code, don't act on the reviewer's confidence. This skill auto-implements confirmed findings, so an unverified finding becomes an unverified change.

For EACH finding:

**Read the TAOM source.** Does the code do what Codex claims?

**Verify "missing" claims.** Grep the codebase — don't trust "I didn't find it."

**Decompile vanilla targets.** For Harmony/GameModel findings, check `E:\Decompiled_Bannerlord\` (Campaign/, MountAndBlade/, Modules/, Core/, UI/).

**Check donor-comparison fairness.** Ported references may target an older Bannerlord — flag API differences.

**Check what Codex missed:**
- Config ID consistency (kingdom/culture IDs match cheatsheet?)
- Fail-safe defaults (`?? true` vs `?? false`)
- No-op code paths
- Dead config values
- Stale state across lifecycle boundaries
- **OnGameLoaded entity state matrix**: If the feature mutates Hero/Settlement state on load, verify all entity states are guarded (recruited, traveling, dead, prisoner, fugitive). This is a HIGH-priority check — Review #23 found a load-path teleport bug from this exact gap.

### 3c: Verify Known Suspects

If the review has Known Suspects with CONFIRMED/DISPUTED verdicts:
- Verify each independently by reading source
- Codex has been wrong about these before

### 3d: Produce Assessment

Output table:

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|

Then categorize:
- **Confirmed bugs** — file, line, what to change, why
- **False positives** — why Codex was wrong
- **Design questions** — need user input
- **Things Codex missed** — additional bugs found

### 3e: Root Cause Analysis — Why Did We Miss This?

For EACH confirmed bug that Codex found, answer:

**What category?** (pick one)
- Config ID mismatch — wrong kingdom/culture/troop/settlement ID in config
- Missing vanilla gate — didn't check what vanilla does before overriding
- Stale state / lifecycle — cache, flag, or reference survives past its intended scope
- Dead / no-op code — code exists but does nothing in all cases
- Convention inconsistency — pattern used differently than rest of codebase
- Reflection target wrong — field/property on wrong type or wrong name
- Missing null guard — didn't handle the null/empty/missing case
- Logic error — wrong formula, wrong condition, wrong comparison
- Other — describe

**Why did Claude miss it during implementation?**
- Didn't decompile vanilla target before writing the patch/model
- Didn't cross-reference config IDs against source-of-truth files
- Didn't trace the full lifecycle (init → runtime → save/load → cleanup)
- Didn't enumerate all entity states for a load-path mutation (see csharp-architecture.md "Entity State Matrix")
- Assumed an API worked a certain way without verifying
- Copied a pattern from another feature without checking if it fits (idempotent vs destructive)
- Other — describe

**Preventive action:** For each root cause, add ONE of:
- A new unit test that would catch this category of bug
- A config validation test that cross-references IDs at test time
- A note in `docs/reviews/REVIEW-GUIDE.md` as a new check item
- A rule in `.claude/rules/` if it's a recurring pattern
- A `### <rule>` entry in the matching category file under `docs/reviews/lessons/` (index: `docs/reviews/LESSONS-LEARNED.md`) — **mandatory for any systemic finding.** Shape: `### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`. This is the always-consulted master record (indexed from the harness `MEMORY.md`); read the matching category file before the next review of that subsystem.

Do NOT skip this step. The point is not just to fix bugs — it's to make the same category of bug impossible in future features. Output the analysis as:

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|

### 3f: Implement Confirmed Fixes

For each confirmed bug:
1. Make the code change
2. `dotnet build TAOM.Tests` — must compile
3. `dotnet test TAOM.Tests` — must pass
4. Update tests if behavior changed
5. Add any preventive tests identified in 3e

### 3h: Update Codex Instructions (AGENTS.md)

Codex learns from us through `AGENTS.md`. After each review, update the "Lessons From Prior Reviews" section:

1. If Codex produced a **new false positive pattern** not already listed, add it to "False positives Codex has produced"
2. If Codex **missed a bug category** that Claude caught, add it to "Bugs Codex typically misses"
3. If Codex did something **particularly well** in this review, add it to "What Codex does well"
4. Update the "Last updated" date

This creates a feedback loop: Claude's findings improve Codex's next review. Over time, Codex's accuracy improves and the gap between what Codex finds and what Claude catches shrinks.

### 3i: Update Review Log

1. Add entry to `docs/reviews/REVIEW-LOG.md`
2. Update metrics
3. Add new failure patterns to `docs/reviews/REVIEW-GUIDE.md` if discovered
4. Include the root cause table from 3e in the review log entry

## Rules

- NEVER implement a fix without reading the source file first
- NEVER agree with a Codex finding just because it sounds plausible — verify
- Decompile vanilla targets for ALL Harmony patch and GameModel findings
- Config cross-reference is mandatory, not optional
- When in doubt about design intent, flag for user input rather than guessing
- Build and test after EVERY batch of fixes
- Flat formatting in prompts — no indented continuation lines
