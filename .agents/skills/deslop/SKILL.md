---
name: deslop
description: Regression-safe cleanup of AI-generated bloat in C# code. Deletion-first, requires green tests before starting.
argument-hint: [path or feature to clean up, e.g. "Main/Features/Execution" or "WargAttackService"]
---

# Deslop

Regression-safe cleanup of AI-generated bloat. Targets dead code, duplicate logic, over-abstraction, and unnecessary complexity — **without changing behavior**.

**Scope:** `$ARGUMENTS` (if empty, use `git diff --name-only` to find recently modified files)

## Precondition: Tests Must Be Green

```bash
dotnet test TAOM.Tests --no-build 2>&1 | tail -5
```

If tests are failing: **STOP**. Do not clean up broken code — you'll mask real bugs. Report the failures and exit.

## Phase 1: Read-Only Slop Inventory

Scan target files for these patterns. **Do not change anything yet.**

| Pattern | TAOM Example |
|---------|-------------|
| **Dead code** | Private methods never called, unused fields, commented-out patch blocks |
| **Redundant null guards** | Null checks on DryIoc-injected services (the container guarantees non-null) |
| **Restatement comments** | `// This method calculates the wage` above `CalculateWage()` |
| **Empty base calls** | `public Foo() : base() {}` with no reason to exist |
| **Adapter over-wrapping** | Adapter that just delegates every property 1:1 with no translation logic |
| **Single-use private methods** | `private bool IsValid() { return x != null; }` called exactly once |
| **Duplicate guard clauses** | Same null/empty check copy-pasted across 3+ methods in the same class |
| **Over-defensive catch blocks** | `try { } catch (Exception) { }` around code that can't throw |
| **Unused using directives** | `using TaleWorlds.Library;` when nothing from that namespace is used |
| **Harmony patch boilerplate noise** | `__state` parameters declared but never used, `__result` assigned then immediately returned unchanged |

For each item found, record: `file:line`, pattern type, deletion safety (`SAFE` / `RISKY`).

Mark `RISKY` if:
- The method is public or internal (may be called from XML, reflection, or Harmony)
- The comment is the only documentation for a non-obvious algorithm
- The deletion would change behavior under any code path

## Phase 2: Delete First, Extract Second

Apply changes in this order — safest first:

1. **Remove unused `using` directives** (compiler-verified safe)
2. **Delete commented-out code blocks** (git history preserves them)
3. **Remove restatement comments** (keep comments that explain *why*, delete comments that restate *what*)
4. **Delete dead private methods** (verify with Grep: `grep -r "MethodName" Main/` first)
5. **Remove redundant null guards** on injected services (DryIoc registration is the guarantee)
6. **Inline single-use private methods** if doing so doesn't reduce clarity
7. **Extract 3+ duplicate guards** into a shared helper — only after deletions are done

**Hard limits — never do these:**
- Do not rename anything (rename = refactor, not deslop)
- Do not change method signatures or interface contracts
- Do not touch test files (they're your safety net)
- Do not remove `#pragma warning` suppressions without understanding why they exist
- Do not collapse Harmony patch classes even if they're thin (patch structure is intentional)

## Phase 3: Re-Run Tests

```bash
dotnet test TAOM.Tests --no-build 2>&1 | tail -10
```

If any test fails: **revert the last change** (`git diff` to identify it), report which deletion broke which test, and mark that item `RISKY` in the report. Do not delete it.

## Output

```
DESLOP REPORT
=============
Scope: [files or feature name]
Files touched: N
Lines deleted: N
Lines added: N (extractions only)

CLEANED:
- [file:line] [pattern] — [brief description]
- ...

SKIPPED (RISKY):
- [file:line] [pattern] — [reason skipped]

Tests: PASS (N tests) / FAIL (list failures)

NET: [clean / needs manual review of N risky items]
```
