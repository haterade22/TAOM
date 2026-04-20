---
name: deep-review
description: Launch parallel deep-dive agents to review completed work for quality, standards, compatibility, completeness, and cross-system data flow
argument-hint: "[feature-name]"
---

# Deep Review

Launch as many review agents as needed to audit the current session's work. The baseline is 5 core agents (below), but there is NO LIMIT — if the scope demands 10, 20, or 100 agents, launch them. Scale the review to match the risk.

Run this AFTER completing a feature or fix, BEFORE closing out.

The feature or area to review: `$ARGUMENTS` (if empty, review all uncommitted changes).

## Step 0 (Optional): Codex Independent Pre-Review

**Trigger when:** `$ARGUMENTS` contains `--codex` (strip `--codex` from the feature name before proceeding).

If triggered:
1. Identify changed files (same logic as Step 1 below)
2. Dispatch Codex via the plugin:
   ```
   /codex:review --background
   ```
3. Continue to Step 1 immediately — do NOT wait for Codex here
4. After all 5 Claude agents complete (Step 2), retrieve Codex results:
   ```
   /codex:result
   ```
5. Include Codex findings in the Step 3 compiled report as a sixth section:
   ```
   CODEX REVIEW:  [PASS/ISSUES — N findings]
   [Codex findings grouped by severity]
   ```

If Codex and any Claude agent disagree on a finding, flag the disagreement explicitly — it is valuable signal.

If `--codex` is not present, skip this step entirely.

## Step 1: Identify Scope

Determine what to review:
- If `$ARGUMENTS` is provided, focus on that feature/area
- Otherwise, use `git diff --name-only` and `git ls-files --others --exclude-standard` to find all changed/new files

Collect the list of changed files for the agents.

## Step 2: Launch Core Review Agents in Parallel

Launch ALL core agents in a SINGLE message (parallel execution). Pass each agent the list of changed files.

**Minimum: 5 core agents (always launch all 5).** If the changeset spans multiple features, multiple XML config files, multiple Harmony patches, or touches more than 20 files — launch ADDITIONAL focused agents for each distinct subsystem. There is no upper limit on agent count. The cost of missing a bug in production is always higher than the cost of an extra review agent.

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
5. **Thin Entry Points (ADR-002):** Behaviors/Models/Patches under 150 lines AND they delegate to services. Line count is a ceiling, NOT the test. For each GameModel override in the changeset, inspect the override method body: if it contains `if`, `foreach`, `switch`, `yield return` with branching, or any multi-line decision logic INLINE (not inside a service call), that is a violation — even if the file is under 20 lines. The only acceptable override bodies are (a) a single constant/expression (`=> 10`), or (b) boundary conversion (adapter wrap, perk check) plus a direct delegate to an injected service. Do NOT invent "simple enough to skip the service" carve-outs; they are not in the rules.
6. **Interface Segregation:** Every service has an interface. Every adapter has an interface.
7. **IoC Registration:** New services/adapters registered in the feature's IoC.cs or Main/IoC.cs.
8. **Naming:** Classes match file names. Interfaces prefixed with I.
9. **No Service Locator (Constructor Injection Only):** Flag ANY `IoC.Resolve<T>()` or `IoC.ResolveAll<T>()` call outside a BOUNDARY class. The only acceptable boundary locations are: (a) Harmony patch static methods, (b) `ScreenBase` subclasses and other TaleWorlds-constructed entry points, (c) `CampaignBehaviorBase` constructors, (d) `GameModel` constructors, (e) `SubModule.cs`, (f) static `OpenXxx()` helpers that exist because the caller has no DI access. Services, ViewModels, engines, helpers, mixins, and hooks MUST receive dependencies via constructor injection. **Red flag patterns to grep for:** `IoC.Resolve<` inside any `.cs` file under `Main/Features/**/Services/`, `Main/Features/**/*Service.cs`, `Main/Features/**/*VM.cs` (excluding the VM's boundary parent), `Main/Features/**/Engines/`, or any method not in the boundary list above. A `try { IoC.Resolve<T>() } catch { }` guard is STILL a violation — it merely hides the test-time NRE while preserving the anti-pattern. **Why this rule exists:** Review #26 (2026-04) — 8 CareerScreenVMTests failed with NullReferenceException because `CareerScreenVM` resolved `ICareerConfigProvider` and `IModLogger` via service locator. DryIoc isn't configured in unit tests, so every test that exercised the code path threw. The rule was already in `.claude/rules/csharp-architecture.md` but wasn't mechanized here, so it wasn't caught at review time.

OUTPUT FORMAT:
For each violation found:
- File path and line number
- Rule violated
- What needs to change

If all checks pass, say "ALL STANDARDS CHECKS PASSED" with a brief summary of what was reviewed.
```

### Agent 2: Bannerlord API Compatibility

```
subagent_type: taleworlds-researcher
model: sonnet
```

**Prompt:**
```
Review these files for Bannerlord API compatibility. Focus on TaleWorlds API usage.

CRITICAL: The decompiled source at E:\Decompiled_Bannerlord\ is from a DIFFERENT version than the installed game. ALWAYS verify against the INSTALLED DLLs using ilspycmd:
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "Full.Type.Name"
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "Full.Type.Name"
NEVER trust the decompiled folder for signature verification.

FILES: [list changed files]

FOR EACH FILE that references TaleWorlds APIs:
1. Identify every TaleWorlds class, method, property, or enum used
2. Decompile the relevant TaleWorlds type from the INSTALLED DLL to verify:
   - The method/property EXISTS
   - The SIGNATURE matches (parameter types, return type)
   - The method is not marked internal/private
   - For GameModel overrides: the base class method signature is correct
   - For Harmony patches: the target method exists with the expected signature

OUTPUT FORMAT:
For each API usage:
- ✅ Verified: [Type.Method] — exists with matching signature
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
2. **IoC.Resolve in Hot Paths:** Flag ANY IoC.Resolve<T>() call inside per-frame, per-hit, or per-tick methods. These MUST use lazy-cached properties instead.
3. **LINQ in Loops:** Flag .ToList(), .ToArray(), .Where().Select() chains inside loops or frequent callbacks
4. **String Concatenation:** Use string interpolation or StringBuilder, not repeated + concatenation
5. **Dictionary Lookups:** Use TryGetValue instead of ContainsKey + indexer (double lookup)
6. **Unnecessary Boxing:** Watch for value types passed as object parameters
7. **Caching Opportunities:** Repeated expensive lookups that could be cached (e.g., race lookups, config reads)
8. **IEnumerable Multiple Enumeration:** Flag any IEnumerable parameter that's enumerated more than once
9. **Closure Allocations in Loops:** Flag lambda/delegate creation inside per-frame loops (RemoveAll with closure, etc.)
10. **Resource Disposal:** IDisposable types properly disposed or in using blocks
11. **Harmony Patch Overhead:** For every `[HarmonyPatch]` class in changed files:
    - Check if the patch target is a hot method (called per-frame, per-tick, per-hit, per-AI-decision)
    - Flag any `IoC.Resolve` not using lazy-cached `??=` pattern
    - Flag any `new List<>`, `new Dictionary<>`, or LINQ chain inside the patch method body
    - Flag any delegate/closure creation that captures local variables
12. **CampaignBehavior Lifecycle Cleanup:** For every `CampaignBehaviorBase` subclass in changed files:
    - Check that `RegisterEvents` has a corresponding cleanup path
    - Flag behaviors that subscribe to events in `OnSessionLaunched` but don't override `OnFinalize` or `OnGameEnd` to unsubscribe
    - Flag any static fields or `static Dictionary` that are populated at runtime but never cleared on session end
    - Flag any collection (List, Dictionary, HashSet) used for persistence/sync/tracking that grows with game events but has no pruning, eviction, or size cap — especially SyncData stores, buff trackers, and per-hero caches
13. **GameModel Override Weight:** For every `GameModel` override in changed files:
    - Identify the override methods and assess call frequency (per-frame vs per-day vs one-time)
    - Flag any service resolution that isn't constructor-injected or lazy-cached
    - Flag any LINQ chain or collection allocation inside override methods called more than once per game tick
14. **GC Pressure Patterns:** Across all changed C# files:
    - Flag `string.Format` or `$""` interpolation inside loops (prefer StringBuilder for >3 concatenations)
    - Flag `params object[]` calls in tight loops (implicit array allocation)
    - Flag `foreach` over `Dictionary.Keys` or `.Values` when only one is needed and the dictionary is large
    - Flag `Enum.ToString()` or `Enum.Parse()` in hot paths (both allocate; prefer lookup dictionaries)

OUTPUT FORMAT:
For each issue found:
- File path and line number
- Issue type (allocation, LINQ, caching, patch overhead, lifecycle leak, GC pressure, etc.)
- Severity: HIGH (hot path / per-frame) / MEDIUM (per-tick / occasional) / LOW (startup only)
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

### Agent 5: Cross-System Data Flow Tracing

**This agent catches bugs that per-file reviews miss — where data declared in one file is consumed (or NOT consumed) in another.**

```
subagent_type: Explore
model: sonnet
```

**Prompt:**
```
CROSS-SYSTEM DATA FLOW REVIEW — trace data declarations through the codebase to find gaps where declared data is never consumed, or where parallel code paths use inconsistent logic.

FILES: [list changed files]

This review exists because per-file reviews consistently miss bugs that span multiple files. Every check below has caught real bugs in this project.

TRACE THESE DATA FLOWS:

1. **XML Config → C# Consumption:** For every configurable value declared in XML (ModuleData/**/*.xml), trace it to the C# code that reads and acts on it. Flag any XML attribute that is parsed but never used at runtime.
   - Read ALL changed XML files. For each attribute/element, grep the C# codebase for where it's consumed.
   - Example bug pattern: XML declares `charge_type="DamageDone"` but the only code that emits charges uses `ChargeType.Kills`.

2. **Enum Coverage:** For every enum type referenced in changed files, check that ALL enum values have at least one handler. Flag any enum value with zero callsites.
   - Example bug pattern: `PassiveEffectType` has 50 values but only 15 are wired into GameModels.
   - Example bug pattern: `ChargeType` has 5 values but only 1 is emitted by mission behavior.

3. **Mutation/Transform Chain Completeness:** When data is transformed through a pipeline (raw → mutated → applied), verify every stage connects to the next.
   - Example bug pattern: Mutation service mutates `MaxCharge` on template, but `CareerAbility` reads `MaxCharge` from career definition (unmutated source).
   - Check: For every `property="X"` in mutation XML, trace X through the mutation service to where the mutated value is consumed.

4. **Parallel Method Consistency:** When multiple methods serve the same purpose (e.g., checking cost, applying cost, displaying cost), verify they ALL use the same calculation.
   - Example bug pattern: `CanAffordUpgrade` uses `baseCost * count` while `SpendForUpgrade` uses `GetEffectiveUpgradeCost()`.
   - Check: Find method families (CanAfford/Spend/Clamp/Display) and verify they share the same cost derivation.

5. **Lifecycle Completeness (State Matrix):** For every "set" operation, verify there is a corresponding "clear" for ALL entity lifecycle states.
   - Entity states to check: alive, killed, unconscious, removed, mission-end, screen-close, session-end.
   - Example bug pattern: `CareerAbilityBuffTracker.SetBuff()` on activation, but `ClearBuff()` only on timeout — not on hero death.
   - Check: For every static dictionary, cached field, or session-scoped state, trace all paths that clear it.

6. **Event Hook Coverage:** For behaviors that register campaign/mission events, verify all relevant events are hooked.
   - Example bug pattern: `OnAgentRemoved` emits kill charges but no hook exists for damage-dealt charges, even though most careers use `DamageDone` charge type.
   - Check: Read the behavior's RegisterEvents/constructor, cross-reference with the data it needs to provide.

7. **Sprite/Asset Reference Verification:** For every `Sprite="X"` in XML prefabs or `GetSprite("X")` in C#, trace X through `TAOMSpriteData.xml` to verify the sprite ID is registered and matches the PNG filename in `SpriteParts/`.
   - Example bug pattern: Code writes `Sprite="TAOM\\CareerSystem\\career_button_placeholder"` but `TAOMSpriteData.xml` registers it as `CareerSystem\career_button_placeholder` (no module prefix). Silent failure — sprite just doesn't render.
   - Check: Read TAOMSpriteData.xml, extract all `<Name>` entries, cross-reference every `Sprite=` attribute in changed prefab XML and every `GetSprite(` call in changed C#.

8. **Vanilla Interaction Safety:** For every UIExtenderEx `PrefabExtension` that injects into a vanilla prefab, check whether vanilla code makes assumptions about the target container's children (hardcoded indices, typed casts, count-based iteration).
   - Example bug pattern: Adding items to `SecondaryInfoItems` collection — vanilla `HandlePanelSwitchingInput` indexes by hardcoded position, causing `IndexOutOfRangeException`.
   - Example bug pattern: Appending a non-template child to a data-bound `ListPanel` — vanilla teardown may cast all children to the template type.
   - Check: For each `PrefabExtension`, identify the target widget, then search decompiled vanilla code for how that widget's children are accessed. Flag any hardcoded indexing, typed iteration, or count assumptions.

OUTPUT FORMAT:
For each trace:
- DATA FLOW: [source] → [transform] → [consumer]
- STATUS: ✅ CONNECTED / ❌ GAP FOUND / ⚠️ INCONSISTENT
- If GAP/INCONSISTENT: describe exactly what's missing and which files are involved

Summary: N flows traced, X gaps found, Y inconsistencies found
```

## Step 2b: Adversarial Escalation (conditional)

**Only launch this step if Agent 1 (Standards) reports ANY violation rated CRITICAL.**

A CRITICAL violation is any of:
- Direct TaleWorlds sealed type usage in a service class (ADR-007 breach)
- Harmony patch that directly accesses game state without an adapter
- Entry point over 150 lines that does business logic itself

If triggered, launch a 6th agent targeting ONLY the offending files:

```
subagent_type: Explore
model: sonnet
```

**Prompt:**
```
ADVERSARIAL REVIEW — assume this code has a critical architecture violation. Prove it.

FILES WITH REPORTED VIOLATIONS: [list only the files Agent 1 flagged as CRITICAL]

For each file:
1. Read the ENTIRE file — not just the flagged lines
2. Map every dependency: what does this class hold references to? What does it return?
3. Find the blast radius: if this adapter pattern violation is kept, which other classes are contaminated?
4. Identify the minimum surgical fix: what is the smallest change that restores compliance without a rewrite?
5. Check if there is a corresponding test that would CATCH this violation (an integration test that passes a real TaleWorlds type). If not, that's a second finding.

OUTPUT FORMAT:
CONFIRMED / DISPUTED for each violation:
- CONFIRMED: [file:line] [exact violation] — blast radius: [N classes affected] — minimum fix: [description]
- DISPUTED: [file:line] [why Agent 1 was wrong]

Minimum fix plan (in order of least disruption):
1. ...
```

## Step 2c: Adaptive Expansion (always evaluate)

After the core 5 agents complete, assess whether the findings warrant additional focused agents. There is NO upper limit.

**Launch additional agents when:**
- Agent 5 (Data Flow) finds gaps → launch per-gap investigation agents to trace the full chain and propose fixes
- Multiple XML config files changed → launch one agent per config file to cross-reference all consumers
- Multiple Harmony patches changed → launch one agent per patch to verify target method signatures and side effects
- Multiple GameModel overrides changed → launch one agent to verify all overrides are registered and don't conflict
- Any agent reports >3 issues → launch a focused agent on just those files to determine root cause
- Feature spans >3 features/ subdirectories → launch per-feature agents with full context of that feature

**Launch additional Codex passes when:**
- Any Claude agent and Codex disagree → dispatch a second Codex pass focused on the disputed finding
- Data Flow agent finds a gap Codex missed → dispatch Codex with the specific gap description to get independent verification

**The review is done when:** All agents have reported, all disagreements are resolved, and no agent's findings suggest an unexplored area.

## Step 3: Compile Report

After all 5 agents complete, compile their results into a single report:

```
DEEP REVIEW REPORT
===================
Feature: [name or "uncommitted changes"]
Date: [today]

STANDARDS:     [PASS/FAIL — N violations]
COMPATIBILITY: [PASS/FAIL — N incompatible, N unverified]
EFFICIENCY:    [PASS/FAIL — N issues (H high, M medium, L low)]
COMPLETENESS:  [COMPLETE/INCOMPLETE — list missing items]
DATA FLOW:     [PASS/FAIL — N gaps, N inconsistencies]

─────────────────────────
DETAILS
─────────────────────────

[Agent 1 results — Standards]

[Agent 2 results — Compatibility]

[Agent 3 results — Efficiency]

[Agent 4 results — Completeness]

[Agent 5 results — Data Flow]

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
- The Bannerlord compatibility agent (Agent 2) MUST use installed DLLs via ilspycmd, NOT the decompiled folder at E:\Decompiled_Bannerlord\ (it's a different version).
- Agent 5 (Data Flow) is the highest-value agent — it catches the class of bugs that all other agents consistently miss. Every HIGH bug found by Codex in this project was a data flow gap.
- If the verdict is NEEDS FIXES, list the fixes needed in priority order.

## HIGH findings — no silent deferrals (MANDATORY)

If any agent reports a HIGH-severity finding (or Codex reports P1):
1. The default action is FIX. Implement the fix in the same session.
2. If the user explicitly chooses to defer, the deferral MUST be recorded in one of:
   - A GitHub issue (`gh issue create`) with the finding text
   - A commit trailer `Deferred: <reason>` on the commit that would have fixed it
   - A CHANGELOG "Known limitation:" bullet

What is NOT allowed: quietly proceeding past a HIGH finding on informal reasoning ("only matters in case X") without writing the decision down. Past experience: Career System P2 (ally buff overwrite) was flagged HIGH by Agent 5 and dismissed — Codex independently caught the same bug later. Memory: `feedback_dont_defer_high_review_findings.md`.
