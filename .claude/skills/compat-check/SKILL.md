---
name: compat-check
description: Check TAOM mod compatibility against a new Bannerlord version by diffing decompiled sources and reviewing all patch targets, GameModel overrides, and reflection usage
argument-hint: "[old-version-dir] (default: auto-detect)"
---

# Bannerlord Version Compatibility Check

Review all TAOM code against API changes between two Bannerlord versions. Uses the pre-decompiled source trees and the API diff script.

## Prerequisites

- Two decompiled Bannerlord source trees (run `tools/Decompile-Bannerlord.ps1` first)
- Current version at `E:\Decompiled_Bannerlord\`
- Previous version backup (e.g., `E:\Decompiled_Bannerlord_v1.3.15\`)

## Step 1: Generate API Diff Report

Run the diff script to produce the change report:

```powershell
powershell -ExecutionPolicy Bypass -File "tools/Diff-BannerlordAPI.ps1"
```

If `$ARGUMENTS` provides an old version directory, pass it: `-OldDir "$ARGUMENTS"`

Read the generated report at `tools/api-diff-report.md`. Extract:
- Number of changed types
- Number of removed types (BREAKING)
- List of signature changes

If there are 0 changed types and 0 removed types, report "No compatibility issues found" and stop.

## Step 2: Launch 3 Review Agents in Parallel

Read `tools/api-diff-report.md` fully before launching agents. Pass each agent the COMPLETE list of changed types with their signature diffs from the report.

### Agent 1: Harmony Patch Compatibility (use model: sonnet)

```
You are reviewing TAOM mod Harmony patches for compatibility with Bannerlord version changes.

CONTEXT: The following TaleWorlds types have changed between versions. For each changed type, the signature diff shows what was added/removed/modified.

[PASTE THE FULL "Changed Types" SECTION FROM api-diff-report.md HERE]

YOUR TASK: Check every Harmony patch in Main/Features/**/Hooks/ against these changes.

For each patch file:
1. Read the patch to find the exact target method/property
2. Check if that target appears in the changed types list
3. If it does: read the FULL diff for that type and determine if the patch will still work
4. For transpiler patches: check if the IL instruction sequence assumptions still hold

Focus on these specific risks:
- Method signature changes (added/removed/reordered parameters)
- Method renamed (old name gone, new name exists)
- Method access modifier changed (public -> private, virtual -> sealed)
- Return type changed
- Method removed entirely
- For reflection (AccessTools): private field/property names changed

Also check the 6 manual harmony.Patch() calls in SubModule.cs (lines ~351-392).

Report format for each finding:
- File: [path]
- Target: [ClassName.MethodName]
- Risk: [BREAKING / HIGH / MEDIUM / LOW]
- What changed: [description]
- Impact: [what will happen at runtime]
- Fix: [what needs to change in TAOM]
```

### Agent 2: GameModel Override Compatibility (use model: sonnet)

```
You are reviewing TAOM mod GameModel overrides for compatibility with Bannerlord version changes.

CONTEXT: The following TaleWorlds types have changed between versions.

[PASTE THE FULL "Changed Types" SECTION FROM api-diff-report.md HERE]

YOUR TASK: Check every GameModel override in Main/Features/**/Models/ against these changes.

TAOM has 28+ GameModel classes that inherit from Default*Model base classes. For each:
1. Read the TAOM override class
2. Check if its base class appears in the changed types list
3. If it does: read both the TAOM override AND the full diff, then check:
   - Do overridden methods still have the same signature in the base?
   - Were new abstract methods added that TAOM must now implement?
   - Did base class constructor parameters change?
   - Did the base class method behavior change in ways that affect TAOM's super calls?
   - Were virtual methods made sealed/non-virtual?
   - Were properties TAOM reads from the base renamed or removed?

Pay special attention to:
- DefaultAllianceModel (massive rewrite: +454 lines, GetScoreOfStartingAlliance signature changed)
- DefaultDiplomacyModel (properties removed/renamed: WarDeclarationScorePenaltyAgainstAllies gone)
- DefaultTargetScoreCalculatingModel (methods renamed: GetPatrollingFactor -> GetDefensivePatrollingFactor)
- DefaultKingdomDecisionPermissionModel (+48 lines, new alliance behavior)
- DefaultClanFinanceModel (+68 lines)

Report format for each finding:
- File: [path]
- Override: [TaomClassName : BaseClassName]
- Risk: [BREAKING / HIGH / MEDIUM / LOW]
- What changed: [description]
- Impact: [compile error / runtime error / behavioral change]
- Fix: [what needs to change in TAOM]
```

### Agent 3: Reflection & Private API Compatibility (use model: sonnet)

```
You are reviewing TAOM mod reflection usage for compatibility with Bannerlord version changes.

CONTEXT: The following TaleWorlds types have changed between versions.

[PASTE THE FULL "Changed Types" SECTION FROM api-diff-report.md HERE]

Also note these types are NEW in the new version (previously in different assemblies):
[PASTE "New Types" SECTION]

YOUR TASK: Check all reflection-based access in TAOM against the version changes.

TAOM uses reflection extensively via AccessTools and typeof().GetMethod/GetField/GetProperty to access private/internal members. These are the MOST fragile API touchpoints because:
- Private field names can change between versions with no notice
- Internal method signatures can change
- Fields can be refactored into properties or vice versa

Check these specific files and their reflection targets:
1. Main/Features/AtmospherePersistence/Hooks/ — Mission.InitializerRecord (AccessTools.Property)
2. Main/Features/BannerColorPersistence/Hooks/ — 15+ reflection targets on MapConversationTableau, AgentVisuals, SPInventoryVM, PartyVM, etc.
3. Main/Features/FactionMap/ — CharacterCreationManager private fields (_characterCreationContent, _cultures)
4. Main/Features/CharacterSelection/Patches/ — AgentVisualsData constructor, ActionSet method
5. Main/Features/ShaderPrecompilation/ — LoadingWindowViewModel.Update (now in different assembly?)
6. Main/Core/Infrastructure/Reflection/ — ReflectionService/ReflectionHelper (generic infrastructure)
7. Main/Features/HeroRace/ — CharacterTableauService (20+ private fields on CharacterTableau)
8. Main/Features/HeroRace/ — CharacterSpawnerService (5+ private fields on CharacterSpawner)

For each reflection target:
1. Find the exact field/property/method name being accessed
2. Check if the target type changed in the diff
3. If the type is NEW (moved to a different assembly), check if the reflection will still resolve
4. For private fields: grep the NEW decompiled source at E:\Decompiled_Bannerlord\ to verify the field still exists with the same name and type

Report format for each finding:
- File: [path]
- Reflection target: [Type.Member via AccessTools/GetField/etc]
- Risk: [BREAKING / HIGH / MEDIUM / LOW]
- What changed: [field renamed/removed/type changed/assembly moved]
- Impact: [NullReferenceException / silent failure / wrong value]
- Fix: [updated field name / new approach needed]
```

## Step 3: Compile Results

After all 3 agents complete, compile a single compatibility report:

### Format:
```
# TAOM v1.4.0 Compatibility Report

## Summary
- Harmony patches: X breaking, Y warnings
- GameModel overrides: X breaking, Y warnings  
- Reflection targets: X breaking, Y warnings

## BREAKING CHANGES (must fix before running on new version)
[All BREAKING findings from all agents]

## HIGH RISK (likely to cause issues)
[All HIGH findings]

## MEDIUM RISK (may cause subtle issues)
[All MEDIUM findings]

## LOW RISK (cosmetic or unlikely)
[All LOW findings]

## Unchanged (confirmed safe)
[Types that were verified unchanged]
```

Write this report to `docs/migration/compat-check-{version}.md`.

## Step 4: Create Remediation Tasks

If there are BREAKING or HIGH findings:
1. Create a GitHub issue titled "Bannerlord {version} compatibility: {N} breaking changes"
2. List each breaking change as a checkbox item in the issue body
3. Include the fix suggestion from each agent finding

If all clear, report success and note any MEDIUM items to watch.
