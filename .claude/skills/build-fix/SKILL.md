---
name: build-fix
description: Incrementally fix dotnet build errors with minimal diffs, one error at a time
argument-hint: [optional: specific error or file to focus on]
---

# Build Fix

Incrementally fix build errors with minimal, safe changes. Fix the error, not the architecture.

## Step 1: Run the Build

```bash
dotnet build Main --no-restore 2>&1
```

Capture the full output. If build succeeds, report success and stop.

## Step 2: Parse and Group Errors

1. Extract all `error CS####` lines from build output
2. Group errors by file path
3. Sort by dependency order (fix type/interface errors before usage errors)
4. Count total errors for progress tracking

## Step 3: Fix Loop (One Error at a Time)

For each error:

1. **Read the file** — Use Read tool to see 10 lines around the error line
2. **Diagnose** — Identify root cause (missing using, wrong type, missing member, API change)
3. **Fix minimally** — Use Edit tool for the smallest change that resolves the error
4. **Re-run build** — Run `dotnet build Main --no-restore 2>&1` to verify
5. **Report** — "Fixed [error] in [file]. Remaining: N errors."
6. **Move to next** — Continue with remaining errors

## Step 4: Guardrails

**STOP and ask the user if:**
- A fix introduces **more errors than it resolves**
- The **same error persists after 3 attempts** (likely a deeper issue)
- The fix requires **architectural changes** (not just a build fix)
- Errors stem from **missing NuGet packages** (need `dotnet restore`)
- Errors stem from **TaleWorlds API changes** (need `/research` first)

## Step 5: Summary

After all errors are fixed (or guardrails triggered):

```
BUILD FIX REPORT
================
Errors fixed: X
  - CS0246 in Main/Features/Foo/FooService.cs (missing using)
  - CS0103 in Main/Features/Foo/Hooks/FooHook.cs (renamed method)
Errors remaining: Y (if any)
New errors introduced: 0 (should always be zero)
Build status: PASS/FAIL
```

## Common C# / Bannerlord Error Patterns

| Error | Typical Fix |
|-------|-------------|
| `CS0246: type or namespace not found` | Add missing `using` statement or NuGet reference |
| `CS0103: name does not exist` | Fix typo, add using, or update to renamed API |
| `CS0535: does not implement interface member` | Add missing method to class |
| `CS0115: no suitable method found to override` | Check v1.3 API changes — method signature may have changed |
| `CS0029: cannot implicitly convert type` | Add cast or fix type mismatch |
| `CS0234: type or namespace does not exist in namespace` | v1.2->v1.3 namespace change — use `/research` to find new location |
| `CS0012: type defined in unreferenced assembly` | Add assembly reference to `.csproj` |
| `CS8602: dereference of possibly null reference` | Add null check or `!` operator |

## DO and DON'T

**DO:**
- Add missing `using` statements
- Add null checks where needed
- Fix method signatures to match interfaces
- Update API calls for v1.3 changes
- Add missing method implementations

**DON'T:**
- Refactor unrelated code
- Change architecture or patterns
- Rename variables (unless causing the error)
- Add new features
- Suppress warnings with `#pragma`
- Change `Directory.Build.props`

## When NOT to Use

- Code needs refactoring → do that directly
- Tests are failing → run `dotnet test TAOM.Tests` instead
- Architecture changes needed → use `/new-feature` or plan mode
- TaleWorlds API is unknown → use `/research` first

## Arguments

If `$ARGUMENTS` is provided, focus on that specific file or error first.
