# Cold review: plan 057, round 1

Plan: `plans/057-signature-find-first-byte-prefilter.md`. Code read from the run worktree at
`c6a82aa04` (`git rev-parse --short HEAD` printed `c6a82aa04`). No earlier `plan-review-057*.md`
existed in this folder, so there are no earlier blocking items to re-check.

## Verdict

No blocking items. A weak executor can carry the plan out with only the plan and the repo. The
excerpts match the code, the new tests' oracles hold against the base loop, and the five mutants each
fail their required tests in a simulation.

## What I checked, with evidence

### Excerpts against the code at c6a82aa04

- `Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs:21-29` (`Parse`) and `:31-47` (`Find`):
  identical to plan lines 60 to 88. `using System;` at line 1 and `using System.Collections.Generic;`
  at line 2, as the plan says at line 91.
- `TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs`: 14 `[TestMethod]`
  (`git grep -c` printed 14); usings `System`, `System.Linq`, MSTest, `TAOM.Features.MissionPerf.AnimMemory`
  (lines 1 to 4); `Find_HaystackShorterThanPattern_ReturnsEmpty` at lines 78 to 84 and
  `RipTarget_RealLoadEncoding_IsDABE40` at line 87. Matches plan lines 50, 270 to 274.
- `SignatureScanSession.cs:89` is `return ClipBudgetSignature.Find(text, pattern, maxHits);`;
  `FindPlain` is called at line 75; a single hit is cached at lines 76 to 80. Matches plan lines 111 to
  113 (the plan's "`SignatureScanSession.Find` (line 75)" names the call site, not the declaration at
  line 56; harmless).
- `SkeletonBufferSignature.cs` scans at lines 81, 82, 85, 86, 106, 109, each with `maxHits` 2; the
  foreign scans are skipped when the guard came from the cache. Matches plan lines 55 and 117 to 119.
- Pattern first bytes: Clip `8B`, Watch `48`, Pool2 `48`, Guard `41`, Foreign `E9`, Pool2Guard `4C`,
  Pool2Foreign `E9` (`SkeletonBufferSignature.cs:24,33,40,43,50,53`, `ClipBudgetSignature.cs:16`).
  Matches plan lines 124 and 638 ("`48` starts two of today's six skeleton patterns").
- The only production callers of `Find` are the session's `FindPlain` and, through `scan.Find`, the
  seven ids above (`git grep` over `Main`). `SkeletonBufferInstalledBinaryTests.cs:106-107` call `Find`
  directly with `maxHits` 3. Matches plan lines 100 and 120.
- `AnimMemoryProbe.cs:92` is the `ClipBudgetSignature.Resolve(` call (plan line 116).
- `SpatialGridQueryTests.cs:51-69` is `CollectInRadius_RandomPointsAndCentres_MatchesABruteForceSphereScan`
  with `new Random(15)` (plan lines 165 and 579).
- `Directory.Build.props:3` is `<TargetFramework>net472</TargetFramework>`; no
  `CheckForOverflowUnderflow` in any `.props`, `.csproj` or `.targets` (plan lines 131 and 147).
- Commit `116e541d1` is `2026-11-13 perf(skeleton-buffer): v2.0.38 - Reuse the engine address on
  relaunch` (plan line 37). `Main/_Module/SubModule.xml:6` is `<Version value="v2.0.40" />`.
- Issue #784: `perf(tools): a first-byte prefilter in ClipBudgetSignature.Find | OPEN | Performance`;
  its body states the 10.6 MB `.text`, 152 to 173 ms and 48 to 58 ms (Debug) figures the plan and the
  commit body draft use.
- `docs/features/mission-perf-heartbeat.md:946` and `docs/features/skeleton-buffer-guard.md:171,187`
  say what plan lines 626 to 628 quote.
- `.claude/rules/tests.md:16,28` carry the two-edit test and the naming rule; the lesson heading is at
  `docs/reviews/lessons/build-tooling-workflow.md:2896`; ADR-002, 007 and 008 exist under those names.
- Both in-scope files are CRLF (`file` printed `with CRLF line terminators` for each).

### Test and filter mechanics

- The filter `FullyQualifiedName~ClipBudgetSignatureTests` matches only `ClipBudgetSignatureTests`
  (the other class is `ClipBudgetSignatureInstalledBinaryTests`, which does not contain that
  substring). `SkeletonBufferSignatureTests`, `EngineSignatureCacheTests`,
  `ClipBudgetSignatureInstalledBinaryTests` and `SkeletonBufferInstalledBinaryTests` all exist under
  `TAOM.Tests`, and the two named durations' tests exist
  (`ClipBudgetSignatureInstalledBinaryTests.cs:33`, `SkeletonBufferInstalledBinaryTests.cs:46`). Both
  LiveInstall classes call `Assert.Inconclusive` when no install resolves, as plan lines 263 to 265
  expect.
- The new `using TAOM.Features.SkeletonBuffer;` introduces no name clash: that namespace declares
  `PoolPlan`, `SkeletonBuffer*` types and two interfaces only; `SyntheticNativeImage` lives in the
  test's own namespace. The `SkeletonBufferSignature` pattern constants are `internal`, which the test
  project already reaches (existing tests call `internal` members of `ClipBudgetSignature`).
- Test SDK is `Microsoft.NET.Test.Sdk` 17.8.0 with MSTest 3.1.1 (`TAOM.Tests.csproj:9-11`); no default
  `.runsettings` filters `LiveInstall`.
- Simulation (`scratch/review-057-r1/sim.py`, Python, with `Array.IndexOf` bounds checks and
  out-of-range reads modelled as exceptions): every new and existing `Find` oracle matches the base
  loop; the new `Find` matches the base on 20,000 random small cases; mutants fail as follows:
  M1 `FirstByteOnlyInTheLastBytes` (exception) plus 1355 random mismatches; M2 `LeadingWildcard` plus
  1825; M3 `Overlapping` plus 369; M4 `Overlapping`, `CandidateFails`, `SameLength`, `MatchAtVeryEnd`,
  `Wildcard` plus 435; M5 `EmptyPattern` (exception) plus 3342. Every row's required names are among
  the failures. Python's RNG is not .NET's, so the C# run in Step 4 remains the evidence, as the plan
  says. I did not run dotnet (read-only role).

### Checklist items

- TDD: a behaviour-preserving perf change; Step 2 adds characterization tests that pass on the base,
  and Step 4 proves each can fail with named mutants. Acceptable and explained (plan lines 276 to 278).
- Issue line present (#784, verified). Binding ADRs named (lines 152 to 156). No protected file, no
  Step 0 needed. Single-owner files listed out of scope (line 211).
- Drift-check paths (line 9) are the two Scope files plus the two read-only callers; consistent with
  Scope.
- Every dotnet command carries `-p:DisableModuleCopy=true -p:ModuleId=`; `./build.ps1` is forbidden.
- No worktree path, no branch name, no CHANGELOG step (line 244 forbids editing it).
- No em or en dash in the plan (scan for U+2013 and U+2014 found none); no local absolute path; no
  secret.

## Blocking

None.

## Non-blocking

1. Plan lines 176, 256, 263, 455, 510, 539, 592: the expected text `Passed: 25` (and `Passed: 14`,
   `Failed: 0`) is not the literal vstest output, which pads the numbers
   (`Passed!  - Failed:     0, Passed:    25, Skipped:     0, Total:    25`). A literal-minded executor
   grepping for `Passed: 25` finds nothing. Say "the totals line shows Failed 0 and Passed 25" or quote
   the padded form.
2. Plan lines 550 to 552 (Step 5 item 5): the expected result "shows changed lines only inside the
   `Find` summary and body" is a judgment. Make it exact: every `-` line is either the old one-line
   summary (base line 31) or the old `for` line (base line 35), and every `+` line appears in the Step 3
   code block.
3. Plan lines 236 to 238, commit body draft: the wrap breaks oddly ("out of / date. Issue #784 /
   measured that scan"). Re-flow the paragraph to 72 columns.
4. Plan line 112: "`SignatureScanSession.Find` (line 75)" reads as the declaration line; the method is
   declared at line 56 and line 75 is its `FindPlain` call. Say "calls `FindPlain` at line 75".
5. Plan line 537 (Step 4 Verify): M4 also fails `Find_OverlappingMatches_ReturnsEachStart` and
   `Find_WildcardBytesDiffer_StillMatches` in the simulation. The plan already says "at least" and asks
   for every failing name, so this is information only; the executor should not read extra failures as
   a STOP.

## Excerpt mismatches

None.
