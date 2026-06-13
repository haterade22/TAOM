# Plan 002: Reject NaN/Infinity in the TroopWeight and Career mutation config loaders

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**:
> `git diff --stat 141b749..HEAD -- Main/Features/TroopWeight/TroopWeightXmlLoader.cs Main/Features/CareerSystem/Mutations/MutationParams.cs`
> If either in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `141b749`, 2026-06-13
- **Issue**: create before implementation lands — orchestrator (TAOM issue-first mandate)

## Why this matters

This is the **4th shipping of the house NaN/Infinity config-guard bug class** (Career cooldown review #31, EditorCacheRebuild review #38, scene-scripts CS_Road 2026-05-13 are the prior three — all documented in `FiniteFloatValidator.cs` and `.claude/rules/csharp-architecture.md`). Two config loaders parse `float` values with `float.TryParse(..., NumberStyles.Float, ...)` and then guard with a bare comparison. `float.TryParse` **parses the literal strings `"NaN"`, `"Infinity"`, and `"-Infinity"` as `true`** (with `NumberStyles.Float`), and every IEEE-754 comparison against `NaN` returns `false` — so `NaN <= 0` is `false` and `Infinity <= 0` is `false`, and both sneak past the "must be positive" guard. A `NaN` troop weight corrupts the troop-weight party-budget math; a `NaN`/`Infinity` mutation param flows unguarded into Career ability templates — the exact "NaN cooldown → ability permanently ready / never ready" failure that review #31 was supposed to have killed. The fix routes both loaders through the established house guard `TAOM.Core.Validation.FiniteFloatValidator` so a fourth instance can't ship.

## Current state

Two files contain the bug. The house guard already exists and is what other features use.

### File 1 — `Main/Features/TroopWeight/TroopWeightXmlLoader.cs` (the bug, lines 75–85)

Role: loads `troop_weights.xml` into a `Dictionary<string,float>`; owns the parse + validity guard. Read this session — the exact current lines:

```csharp
// TroopWeightXmlLoader.cs:75-85
                if (!float.TryParse(weightStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
                {
                    _logger.LogWarning($"Invalid weight value '{weightStr}' for troop '{id}' — skipping");
                    continue;
                }

                if (weight <= 0)
                {
                    _logger.LogWarning($"Weight must be positive for troop '{id}' (got {weight}) — skipping");
                    continue;
                }
```

The bug: `weightStr == "NaN"` → `TryParse` returns `true`, `weight = NaN`, and `NaN <= 0` is `false` → the entry is kept. Same for `"Infinity"` (`Infinity <= 0` is `false`). The class already imports `using TAOM.Core.Infrastructure;` and `using TAOM.Core.Logging;` (lines 6–7) but **not** `using TAOM.Core.Validation;` — you will add that.

> **Finding line-number note (corrected this session):** the audit cited "lines ~75-90." The actual `weight <= 0` guard is lines 81–85; the `TryParse` is line 75. Use the line numbers above.

### File 2 — `Main/Features/CareerSystem/Mutations/MutationParams.cs` (the bug, lines 15–20)

Role: typed accessor over a `IReadOnlyDictionary<string,string>` of mutation parameters; `GetFloat` is consumed by all 5 built-in calculators (`flat`, `skill_scaling`, `level_scaling`, `replace`, `multiply` — see `Main/Features/CareerSystem/Mutations/BuiltInCalculators.cs`), so a bad value flows straight into Career ability template values. Read this session — the exact current `GetFloat`:

```csharp
// MutationParams.cs:15-20
    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (!_params.TryGetValue(key, out var val)) return defaultValue;
        return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result : defaultValue;
    }
```

The bug: `val == "NaN"` or `"Infinity"` → `TryParse` returns `true` → the non-finite `result` is returned. There is no range guard here at all, so the *only* fix needed is a finiteness check that falls back to `defaultValue`. The file currently imports only `using System.Collections.Generic;` and `using System.Globalization;` (lines 1–2) — you will add `using TAOM.Core.Validation;`.

> **Finding line-number note (corrected this session):** the audit cited "lines ~15-20" — confirmed exact. No correction needed for this file.

### The house guard (use it — do NOT write a new IsNaN/IsInfinity check)

`Main/Core/Validation/FiniteFloatValidator.cs` — a static class, no instance/DI. Read this session, the relevant signatures:

```csharp
namespace TAOM.Core.Validation;
public static class FiniteFloatValidator
{
    public static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
    // also: IsFiniteInRange(float,float,float), IsFiniteAtMost(float,float), IsFiniteAtLeast(float,float)
    //       (+ double overloads of all four)
}
```

For **File 1** the existing guard is "weight must be positive" → use `FiniteFloatValidator.IsFiniteAtLeast(weight, ...)` semantics, but note the current behavior is strict `> 0` (zero is rejected: see `GetTroopWeights_ZeroWeight_SkipsEntryWithWarning`). `IsFiniteAtLeast` is `>= min` (inclusive), so it is NOT a drop-in for `weight <= 0` (which rejects zero). Keep the existing positivity check exactly as-is and ADD a finiteness gate in front of it — see Step 2 for the precise shape. Do not change the zero-rejection behavior.

For **File 2** there is no range constraint — `GetFloat` must return *any* finite parsed value, only rejecting NaN/Infinity. Use `FiniteFloatValidator.IsFinite(result)`.

### Conventions that bind this change

- **TDD (Critical Rule, RED→GREEN→REFACTOR):** the failing test is Step 1, before any production edit.
- **House NaN guard:** `.claude/rules/csharp-architecture.md` → "Config Providers MUST Validate" → rule 4: *"For every `float`/`double` field: reject `NaN` and `±Infinity` BEFORE the range check… Use `TAOM.Core.Validation.FiniteFloatValidator` — never write bare `< min || > max` checks on floats."* This is exactly that rule, applied to two loaders that predate/skip it.
- **Memory `feedback_editor_fields_are_config.md`:** this bug class has shipped 3× prior; user-editable XML and mutation-param dicts are config and must be guarded.
- **No adapters / GameModel / IoC involved.** `TroopWeightXmlLoader` is a plain loader (constructor-injected `IPathService`/`IModLogger`); `MutationParams` is a plain data accessor with no DI. `FiniteFloatValidator` is a static helper. No ADR-007 adapter work, no `IoC.cs`/`SubModule.cs` edits.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` | exit 0, 0 errors |
| Tests (all) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true` | all pass |
| Tests (narrowed) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~TroopWeightXmlLoader"` | named tests pass |
| Tests (narrowed) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~MutationParams"` | named tests pass |

`-p:DisableModuleCopy=true` is required on BOTH build AND test — the tests project builds Main, whose post-build target otherwise deploys to the game install. **NEVER run `./build.ps1`** from an executor — same deploy, and it must not run concurrently.

## Scope

**In scope** (the only files you may modify):
- `Main/Features/TroopWeight/TroopWeightXmlLoader.cs`
- `Main/Features/CareerSystem/Mutations/MutationParams.cs`
- `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs` (add tests; existing file)
- `TAOM.Tests/Features/CareerSystem/MutationParamsTests.cs` (NEW file — there is no existing `MutationParams` test file; `MutationServiceTests.cs` and `MutationCalculatorRegistryTests.cs` exist but cover different classes)

**Out of scope** (do NOT touch, even though they look related):
- `Main/Core/Validation/FiniteFloatValidator.cs` — the guard already has everything you need; do not extend it.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj` — single-owner / build files; no registration change is required for this fix (no new types, no new DI). If you believe one is needed, STOP and report.
- `BuiltInCalculators.cs`, `MutationService.cs`, `MutationCalculatorRegistry.cs` — consumers of `GetFloat`; fixing `GetFloat` is sufficient, do not change them.
- The `troop_weights.xml` data file or any career XML — this is a code-side guard, not a data fix.

## Git workflow

- Branch: work in the dispatched worktree's branch; do NOT push or open a PR.
- Commit (50/72, imperative, no AI attribution), e.g.:
  `fix(config): reject NaN/Infinity in troop-weight and mutation float loaders`
  Suggested trailers:
  `Constraint: float.TryParse(NumberStyles.Float) parses "NaN"/"Infinity" as true`
  `Save-compat: none — load-time validation only, no serialized fields`

## Steps

### Step 1: Write the failing tests (RED)

**1a — TroopWeight.** Open `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs` (already imports MSTest + NSubstitute + the feature namespace; uses a `WriteXml(...)` helper and a temp-dir fixture — model the new tests on the existing `GetTroopWeights_NegativeWeight_SkipsEntryWithWarning`). Add three tests, one per non-finite literal:

```csharp
[TestMethod]
public void GetTroopWeights_NaNWeight_SkipsEntryWithWarning()
{
    WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""bad_troop"" weight=""NaN"" />
    <TroopWeight id=""valid_troop"" weight=""2.0"" />
</TroopWeights>");

    var result = _sut.GetTroopWeights();

    Assert.AreEqual(1, result.Count);
    Assert.IsFalse(result.ContainsKey("bad_troop"));
    _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("bad_troop")));
}

[TestMethod]
public void GetTroopWeights_InfinityWeight_SkipsEntryWithWarning()
{
    WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""bad_troop"" weight=""Infinity"" />
    <TroopWeight id=""valid_troop"" weight=""2.0"" />
</TroopWeights>");

    var result = _sut.GetTroopWeights();

    Assert.AreEqual(1, result.Count);
    Assert.IsFalse(result.ContainsKey("bad_troop"));
    _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("bad_troop")));
}

[TestMethod]
public void GetTroopWeights_NegativeInfinityWeight_SkipsEntryWithWarning()
{
    WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""bad_troop"" weight=""-Infinity"" />
</TroopWeights>");

    var result = _sut.GetTroopWeights();

    Assert.AreEqual(0, result.Count);
    _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("bad_troop")));
}
```

> The literals `"NaN"`, `"Infinity"`, `"-Infinity"` are the exact strings `float.TryParse` with `NumberStyles.Float` + `CultureInfo.InvariantCulture` accepts. Do not use `"inf"` or `"nan"` (those do NOT parse and would just hit the existing "Invalid" path, not the bug). The warning-message assertion is loose (`Contains("bad_troop")`) so it passes whether the implementation logs via the new finiteness branch or routes through the existing "positive" message — don't over-specify the message text.

**1b — MutationParams.** Create NEW file `TAOM.Tests/Features/CareerSystem/MutationParamsTests.cs`. There is no existing test for this class; model the harness on the simplest existing CareerSystem test (e.g. construct the SUT directly with a `Dictionary<string,string>`, no mocks needed — `MutationParams` has no dependencies). Required tests:

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Mutations;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class MutationParamsTests
{
    private static MutationParams Make(string key, string value) =>
        new MutationParams(new Dictionary<string, string> { [key] = value });

    [TestMethod]
    public void GetFloat_ValidValue_ReturnsParsed()
    {
        Assert.AreEqual(2.5f, Make("value", "2.5").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_MissingKey_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("other", "2.5").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_NaN_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("value", "NaN").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_PositiveInfinity_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("value", "Infinity").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_NegativeInfinity_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("value", "-Infinity").GetFloat("value", -99f));
    }
}
```

**Verify (RED)**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~MutationParams|FullyQualifiedName~TroopWeightXmlLoader"`
→ build succeeds; the 5 new non-finite tests (`*_NaN_*`, `*_Infinity_*`, `*_NegativeInfinity_*` across both classes) **FAIL** (the two happy-path / missing-key tests for MutationParams pass). Confirm the failures are assertion failures on the non-finite cases, not compile errors. If they unexpectedly PASS, STOP — the behavior may already be guarded (drift) and you should report.

### Step 2: Fix `TroopWeightXmlLoader.cs` (GREEN for 1a)

Add `using TAOM.Core.Validation;` to the using block (alongside the existing `using TAOM.Core.Infrastructure;` / `using TAOM.Core.Logging;`). Then insert a finiteness gate between the `TryParse` block (ends line 79) and the `weight <= 0` block (starts line 81). Target shape:

```csharp
                if (!float.TryParse(weightStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
                {
                    _logger.LogWarning($"Invalid weight value '{weightStr}' for troop '{id}' — skipping");
                    continue;
                }

                if (!FiniteFloatValidator.IsFinite(weight))
                {
                    _logger.LogWarning($"Weight is not a finite number for troop '{id}' (got '{weightStr}') — skipping");
                    continue;
                }

                if (weight <= 0)
                {
                    _logger.LogWarning($"Weight must be positive for troop '{id}' (got {weight}) — skipping");
                    continue;
                }
```

Do **not** collapse the finiteness check into the `weight <= 0` line and do **not** switch the positivity check to `IsFiniteAtLeast` — that is `>= min` inclusive and would start accepting zero, breaking `GetTroopWeights_ZeroWeight_SkipsEntryWithWarning`. Keep the strict `weight <= 0` exactly as-is; the finiteness gate goes in front of it.

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~TroopWeightXmlLoader"`
→ all TroopWeightXmlLoader tests pass, including the 3 new ones AND the pre-existing `*_ZeroWeight_*` / `*_NegativeWeight_*` / `*_InvalidWeightValue_*` tests (no regression).

### Step 3: Fix `MutationParams.cs` (GREEN for 1b)

Add `using TAOM.Core.Validation;` to the using block. Change `GetFloat` so a successfully-parsed-but-non-finite value falls back to `defaultValue`. Target shape:

```csharp
    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (!_params.TryGetValue(key, out var val)) return defaultValue;
        return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && FiniteFloatValidator.IsFinite(result)
            ? result : defaultValue;
    }
```

`MutationParams` has no `IModLogger`, so there is no warning to emit here (unlike File 1) — silent fallback to `defaultValue` is the correct and only behavior for this accessor. Leave `GetInt` / `GetBool` / `GetString` untouched (int/bool aren't IEEE-754; the house rule explicitly scopes the guard to float/double).

**Verify**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~MutationParams"`
→ all 5 MutationParamsTests pass.

### Step 4: Full build + test (no regression)

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0, 0 errors
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true` → all pass (the whole suite, +8 new tests: 3 TroopWeight, 5 MutationParams)

## Test plan

- **New tests — `TroopWeightXmlLoaderTests.cs`** (3): NaN, Infinity, -Infinity weight → entry skipped + warning. Structural pattern: copy `GetTroopWeights_NegativeWeight_SkipsEntryWithWarning` (same file).
- **New tests — `MutationParamsTests.cs`** (5, new file): valid-value (happy path), missing-key (default), NaN → default, +Infinity → default, -Infinity → default. SUT constructed directly with a `Dictionary<string,string>`; no NSubstitute needed (`MutationParams` has no dependencies).
- **Regression coverage relied on (must stay green):** `GetTroopWeights_ZeroWeight_SkipsEntryWithWarning`, `GetTroopWeights_NegativeWeight_SkipsEntryWithWarning`, `GetTroopWeights_InvalidWeightValue_SkipsEntryWithWarning`, `GetTroopWeights_ValidXml_ReturnsAllEntries` — these prove the new finiteness gate didn't change the zero/negative/garbage/valid behaviors.
- **Structurally untestable:** nothing here is untestable — both fixes are in pure, dependency-light methods. No `Not-tested:` trailer needed.
- **Verification**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true` → all pass, including the 8 new tests.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true` exits 0; 8 new tests (3 TroopWeight + 5 MutationParams) exist and pass
- [ ] `grep -n "FiniteFloatValidator" Main/Features/TroopWeight/TroopWeightXmlLoader.cs` returns a match
- [ ] `grep -n "FiniteFloatValidator" Main/Features/CareerSystem/Mutations/MutationParams.cs` returns a match
- [ ] No files outside the in-scope list are modified (`git status` shows only the 4 listed files + `plans/README.md`)
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check shows either `TroopWeightXmlLoader.cs` or `MutationParams.cs` changed since `141b749` and the "Current state" excerpts no longer match the live code.
- The Step 1 RED tests unexpectedly PASS before any production edit (the guard may already exist — report rather than deleting tests).
- A verification fails twice after a reasonable fix attempt.
- The fix appears to require touching an out-of-scope file (especially `IoC.cs` / `SubModule.cs` / either `.csproj`) — e.g., a build error claims `FiniteFloatValidator` is not referenced by the Main project (it is: same `TAOM.Core.Validation` namespace, same assembly — if this happens, something is wrong; report).
- `FiniteFloatValidator.IsFinite` is not found / has a different signature than the one quoted in "Current state."

## Maintenance notes

For the human/agent who owns this code after the change lands:

- **What interacts with this:** any future config loader that parses a `float`/`double` from XML/JSON/MCM. The house rule (`.claude/rules/csharp-architecture.md` "Config Providers MUST Validate", rule 4) mandates `FiniteFloatValidator` for all of them — this plan brings two stragglers into compliance. If you add a new numeric config field anywhere, guard it the same way.
- **What a reviewer (`/deep-review`, run by the orchestrator for C# changes ≥2 files) should probe:** (1) that the TroopWeight finiteness gate sits BEFORE `weight <= 0` and did NOT change the strict zero-rejection (regression risk); (2) that `MutationParams.GetFloat` only rejects non-finite values and still returns legitimately negative finite values (a negative factor for the `multiply`/`flat` calculators is valid — do not over-constrain to non-negative); (3) that `GetInt`/`GetBool` were left alone.
- **Deferred out of this plan (intentionally):** the broader `MutationService` / `BuiltInCalculators` consumers are not re-validated here — fixing `GetFloat` at the accessor is the single chokepoint, so per-calculator guards would be redundant. If a future calculator parses a float by a path other than `MutationParams.GetFloat`, it needs its own guard.
