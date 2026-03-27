---
name: deep-review
description: Launch parallel deep-dive agents to review completed work for quality, standards, compatibility, and completeness
argument-hint: [feature-name]
---

# Deep Review

Launch 4 parallel review agents to audit the current session's work. Run this AFTER completing a feature or fix, BEFORE closing out.

The feature or area to review: `$ARGUMENTS` (if empty, review all uncommitted changes).

## Step 1: Identify Scope

Determine what to review:
- If `$ARGUMENTS` is provided, focus on that feature/area
- Otherwise, use `git diff --name-only` and `git ls-files --others --exclude-standard` to find all changed/new files

Collect the list of changed files for the agents.

## Step 2: Launch 4 Review Agents in Parallel

Launch ALL FOUR agents in a SINGLE message (parallel execution). Pass each agent the list of changed files.

### Agent 1: Standards Compliance

```
subagent_type: Explore
model: haiku
```

**Prompt:**
```
Review these files for TAOM project standards compliance. Read each file and check:

FILES: [list changed files]

CHECK ALL OF THESE:
1. **Adapter Pattern (ADR-007):** Services NEVER reference TaleWorlds types directly (Hero, Clan, Kingdom, etc.). They use IXxxAdapter interfaces. Flag ANY direct TaleWorlds type usage in service classes.
2. **No #region (ADR-003):** Zero `#region` directives anywhere.
3. **No [Obsolete] (ADR-004):** Zero `[Obsolete]` attributes.
4. **No #if DEBUG (ADR-005):** Zero preprocessor directives except in IoC.cs.
5. **Thin Entry Points (ADR-002):** Behaviors/Models/Patches under 150 lines. They delegate to services.
6. **Interface Segregation:** Every service has an interface. Every adapter has an interface.
7. **IoC Registration:** New services/adapters registered in the feature's IoC.cs or Main/IoC.cs.
8. **Naming:** Classes match file names. Interfaces prefixed with I.

OUTPUT FORMAT:
For each violation found:
- File path and line number
- Rule violated
- What needs to change

If all checks pass, say "ALL STANDARDS CHECKS PASSED" with a brief summary of what was reviewed.
```

### Agent 2: Bannerlord 1.3 Compatibility

```
subagent_type: taleworlds-researcher
model: sonnet
```

**Prompt:**
```
Review these files for Bannerlord v1.3.12 API compatibility. Focus on TaleWorlds API usage.

FILES: [list changed files]

FOR EACH FILE that references TaleWorlds APIs:
1. Identify every TaleWorlds class, method, property, or enum used
2. Decompile the relevant TaleWorlds type using ilspy MCP to verify:
   - The method/property EXISTS in v1.3
   - The SIGNATURE matches (parameter types, return type)
   - The method is not marked internal/private
   - For GameModel overrides: the base class method signature is correct
   - For Harmony patches: the target method exists with the expected signature
3. Check for v1.2 APIs that were removed/renamed in v1.3 (see docs/migration/v1.3-api-changes.md)

OUTPUT FORMAT:
For each API usage:
- ✅ Verified: [Type.Method] — exists in v1.3 with matching signature
- ❌ INCOMPATIBLE: [Type.Method] — [reason: removed/renamed/signature changed]
- ⚠️ UNVERIFIED: [Type.Method] — could not decompile, needs manual check

Summary: X verified, Y incompatible, Z unverified
```

### Agent 3: Efficiency & Performance

```
subagent_type: Explore
model: haiku
```

**Prompt:**
```
Review these files for performance and efficiency issues. Read each file and check:

FILES: [list changed files]

CHECK ALL OF THESE:
1. **Hot Path Allocations:** Any code in DailyTick, HourlyTick, or OnTick handlers — avoid LINQ, avoid allocating lists/arrays per tick, use cached collections
2. **LINQ in Loops:** Flag .ToList(), .ToArray(), .Where().Select() chains inside loops or frequent callbacks
3. **String Concatenation:** Use string interpolation or StringBuilder, not repeated + concatenation
4. **Dictionary Lookups:** Use TryGetValue instead of ContainsKey + indexer (double lookup)
5. **Unnecessary Boxing:** Watch for value types passed as object parameters
6. **Caching Opportunities:** Repeated expensive lookups that could be cached (e.g., race lookups, config reads)
7. **IEnumerable Multiple Enumeration:** Flag any IEnumerable parameter that's enumerated more than once
8. **Resource Disposal:** IDisposable types properly disposed or in using blocks

OUTPUT FORMAT:
For each issue found:
- File path and line number
- Issue type (allocation, LINQ, caching, etc.)
- Severity: HIGH (hot path) / MEDIUM (occasional) / LOW (startup only)
- Suggested fix

If no issues found, say "NO PERFORMANCE ISSUES FOUND" with a brief summary.
```

### Agent 4: Completeness Check

```
subagent_type: Explore
model: haiku
```

**Prompt:**
```
Review the current work session for completeness. Check ALL of these:

FILES: [list changed files]

1. **Tests Exist:** For every new service/behavior/model class in Main/Features/, verify a corresponding test file exists in TAOM.Tests/Features/. Flag any untested classes.
2. **Test Coverage:** Read each test file. Are edge cases covered? Are there tests for error/null/empty cases? Is the AAA pattern used (Arrange/Act/Assert)?
3. **Feature Doc:** If this is a new feature, check that docs/features/<name>.md exists. If not, flag it as MISSING.
4. **GitHub Issue:** Run `gh issue list --state all --limit 20` and check if there's an issue for this work. If not, flag as MISSING.
5. **CHANGELOG Updated:** Check if CHANGELOG.md has been modified with an entry for this work. Use `git diff --name-only -- CHANGELOG.md`.
6. **IoC Registered:** Check that new services/adapters are registered in DryIoc. Read the relevant IoC.cs file.
7. **SubModule.xml:** If new behaviors or models were added, verify they don't need SubModule.xml registration (most don't, but check).

OUTPUT FORMAT:
- ✅ Tests: [X test files, Y test methods]
- ✅/❌ Feature Doc: [exists at path / MISSING]
- ✅/❌ GitHub Issue: [#N title / MISSING]
- ✅/❌ CHANGELOG: [updated / NOT UPDATED]
- ✅/❌ IoC: [registered / MISSING registrations]

Overall: COMPLETE / INCOMPLETE — [list what's missing]
```

## Step 3: Compile Report

After all 4 agents complete, compile their results into a single report:

```
DEEP REVIEW REPORT
===================
Feature: [name or "uncommitted changes"]
Date: [today]

STANDARDS:     [PASS/FAIL — N violations]
COMPATIBILITY: [PASS/FAIL — N incompatible, N unverified]
EFFICIENCY:    [PASS/FAIL — N issues (H high, M medium, L low)]
COMPLETENESS:  [COMPLETE/INCOMPLETE — list missing items]

─────────────────────────
DETAILS
─────────────────────────

[Agent 1 results]

[Agent 2 results]

[Agent 3 results]

[Agent 4 results]

─────────────────────────
ACTION ITEMS
─────────────────────────
1. [Most critical issue first]
2. ...

VERDICT: READY FOR COMMIT / NEEDS FIXES
```

## Important

- This is a READ-ONLY review. Do NOT make any code changes.
- If any agent fails to launch (MCP issues, etc.), note it in the report and run the checks manually.
- The Bannerlord compatibility agent (Agent 2) is the most critical — API mismatches cause runtime crashes.
- If the verdict is NEEDS FIXES, list the fixes needed in priority order.
