# Plan review 041, round 1 (cold)

Plan: `plans/041-profiler-extensions-and-hitch-probe.md` (1,453 lines). Code read at `0d1e91f0` with
`git show 0d1e91f0:<path>` (the worktree HEAD is `0b2944ad`, docs-only commits past the base). Engine
facts re-read from the installed v1.5.3 client: `pwsh tools/taom-src.ps1 path <Type>` for
`ManagedScriptHolder`, `TWParallel`, `Mission`, `MBAnimation`, `ScriptComponentBehavior`,
`MissionBehavior`, plus a byte scan of the installed IL (`MethodBody.GetILAsByteArray()` from Windows
PowerShell, tokens resolved). No earlier `plan-review-041*.md` existed, so there is nothing to re-check.

Verdict: one blocking gap (Step 10's test resets), otherwise executable. Most excerpts match exactly.

## Blocking

1. **Step 10 (lines 1058-1069, 1089-1094): the installer tests need reset members that plan 028 never
   creates, and the plan does not say to add them.** The RED text says "`[TestCleanup]` calls both
   installers' and both hooks' test resets", and the cases (`Install_BothTogglesOff_...` expecting plan
   028's `OffLine`, `Install_ProfilerOn_AppliesPatch97ThenTheProbe` expecting the order
   `["Patch97_MissionTickProfiler", "Patch98_HitchProbe"]`, `Install_SecondCall_DoesNothing`) can only
   be driven through `MissionTickProfilerInstaller.InstallIfEnabled`. Plan 028 gives that installer a
   `private static bool _attempted` set before anything else (028 lines 955 and 964) and no reset of any
   kind; it gives `MissionTickProfilerHooks` none either (028's hooks tests clear the statics from the
   test side). Step 1's re-anchor greps (lines 652-661) do not look for one. Only `HitchProbeInstaller`
   and `HitchProbeHooks` get a `ResetForTests()` in this plan (lines 1030, 1088). So after the first
   test sets `_attempted`, every later test in the class is a silent no-op, and
   `Install_SecondCall_DoesNothing` cannot say which guard it exercises (`HitchProbeInstaller.Install`
   has no once-guard in its spec). A weak executor either STOPs ("a member that does not exist") or
   improvises a reflection hack on a private field.
   **Fix:** in Step 10 GREEN add, to plan 028's files (both already in Scope):
   `internal static void ResetForTests()` on `MissionTickProfilerInstaller` (clears `_attempted`) and on
   `MissionTickProfilerHooks` (nulls `Profiler`, `Logger`, `WaitTickCompletionCall`; zeroes `Installed`,
   `OnTickSites`, `OnPreTickSites`), or say the test clears the hooks' internal statics directly as 028's
   tests do. State that all five tests call `MissionTickProfilerInstaller.InstallIfEnabled(...)`, and
   that `Install_SecondCall_DoesNothing` exercises `_attempted` (or give `HitchProbeInstaller` its own
   guard and say so).

## Non-blocking

1. **Scope vs. permitted file splits (lines 557-563, 1053-1054, 1138-1140, 1354).** Step 9 says to split
   `HitchProbeHooks.cs` "by bracket family" if it exceeds 150 lines, and Step 11 allows
   `Hooks/MissionAttributionInstaller.cs` as its own file; neither file name is in Scope, and the Done
   criterion "`git status --porcelain` lists only in-scope files" then conflicts. Both splits are
   likely (ten bracket methods each in try/catch plus the thread line and wait self-check; four timed
   helpers plus the installer). Name the allowed extra files in Scope (for example
   `Hooks/HitchProbeHooks.Spawn.cs` or a fixed name, and `Hooks/MissionAttributionInstaller.cs`). Also
   say what to do if plan 028's `MissionTickProfilerHooks.cs` or `MissionTickProfilerBehavior.cs` passes
   150 lines after Steps 11 and 12 (only the behaviour gets a "move logic into `ProbeWindowWriter`" hint).
2. **Step 9 needs probe mode before Step 12 adds it (lines 988-991 vs 1177).** `HitchProbeHooksTests`
   "begins a probe-mode mission", but `MissionTickProfilerHooks.BeginMission` gains `behaviourTiming`
   only in Step 12. Say the Step 9 tests call `MissionTickProfilerHooks.Profiler.BeginMission(..., behaviourTiming: false)`
   directly (Step 3 added that parameter), or move the hooks' parameter into Step 9.
3. **Vacuous stale-claim greps (lines 727-728, 1345).** Plan 028 writes the restart-test summary with
   "the one" at the end of one `///` line and "setting a Harmony category is gated on" on the next (028
   lines 662-663), so `git grep -n "the one setting a Harmony category"` returns nothing before the edit
   too and proves nothing. The same may hold for "is the one such setting today" in `mcm.md`, depending
   on wrap. Grep for a fragment that sits on one line, for example `setting a Harmony category is gated on at apply time (read once per process for Patch97)`
   and `is the one such setting`.
4. **Done-criteria grep vs. comments (lines 1043-1045, 1339-1341).** The Patch97 comment "saying the
   frame boundary now lives in `Patch98_HitchProbe`" and the Patch98 header comment must not contain the
   word `OnFrameBoundary`, or the comment-blind `git grep` criteria (Patch97 "returns nothing", Patch98
   "returns one line") fail. Say so in Step 9.
5. **Wait double count in one edge (lines 1083-1085).** `WaitSwapActive` requires
   `MissionTickProfilerHooks.Installed`, which is false when Patch97 applied but `OnTickSites != 2`
   (an engine bump). Patch97's `OnPreTick` transpiler can still have swapped the wait (`OnPreTickSites == 2`),
   so `TimedWaitTickCompletion` (not routed through `Timed`, so Step 11's `BehaviourTiming` gate does not
   cover it) and the probe's wait bracket both call `AddWait`. Base `WaitSwapActive` on
   `applied && WaitTickCompletionCall != null && OnPreTickSites == 2`, or gate `TimedWaitTickCompletion`
   on `BehaviourTiming` too.
6. **`[TickSummary]` in probe mode (lines 464-466, 1195-1196, 1415).** Plan 028's amendment says "When
   the profiler is off, no `[TickSummary]` is written". If 028 implemented that as `TickProfilerEnabled`
   or `Installed` rather than "the mission measured anything", a default (probe) run writes neither
   `[TickSummary]` nor `[TickSummaryExtra]` ("under the same condition"), contrary to the maintainer's
   check list. Tell the executor to make the condition "the mission measured" and to say so in the report.
7. **Step 1 Verify (lines 676-679) demands exactly one failure.** If the paid translation run lands before
   dispatch, `EveryLanguage_DeclaresARowForEveryEnglishKey` passes and the "exactly one failure" check
   reads as a STOP. Say "the failures are a subset of {that test}" here and in Step 14 item 3 and the
   Done criteria.
8. **Drift check names one 028 commit (lines 14-18, 650).** Plan 028's reviewed branch tip may carry
   review-fix commits whose subjects do not contain `per-behaviour tick profiler and hitch log`. Say
   "plan 028's commits (its feature commit and any review fixes on its branch)".
9. **Log levels.** The quoted D6 text (line 304) says reason lines are INFO, but the plan logs
   `WaitUnseenLine`, the off-main spawn line, `ProbeNotInstalledLine` and `ScriptDelegateUnboundLine`
   with `LogWarning`; and it never says which level the two script-thread lines use, though
   `ScriptBracket_*_LogsTheThreadLineOnce` must assert one. Pin the level per status line.
10. **`AnimLoadingSamplerTests` clock oracle (lines 922-926).** The fake clock "advances by a scripted
    amount per call", but the plan does not say whether the sampler reads the clock twice per adapter
    call or once per boundary; the scripted costs only give a median of 16.5 for one of those shapes.
    Simplest: the substitute adapter advances the fake clock inside each call.
11. **Step 10 "leave a call site you complete there" (lines 1091-1092)** is vague; say "a
    `// Step 11:` comment line at that point", since a call to a missing type does not compile.
12. **Step 12 RED (lines 1172-1174).** All four wiring pins already pass after Steps 9 and 10, so this
    step has no RED; the text half-admits it. Fine as pins, but call them pins, not TDD.
13. **Step 13 fallback (lines 1238-1243)** flips the default but Step 14's feature-map row text (lines
    1270-1272, "on by default") and the SubModule comment (line 1201, "default on") are not told to flip.
14. **Category `diagnostics` (line 32)** is not one of the template's categories (bug | security | perf
    | tests | tech-debt | migration | dx | docs | data | direction); `perf` fits.
15. **Plan 036 adapter "identical" claim (lines 173-177, 935-940).** Plan 036 Step 7 gives no XML
    summary for `IAnimationLoadingAdapter`; this plan adds one. Two branches adding the same path with
    different comments conflict on merge rather than "adding nothing". Use plan 036's exact text (no
    summary) or say the second merge keeps the first file.
16. **RefAsm build needs NuGet** (line 544): it downloads BUTR's reference assemblies. Add "if the
    restore cannot download them, report not run (environment), not a failure".

## Excerpt mismatches (against `0d1e91f0`)

1. Line 321: "`PatchShieldPolicyTests` (21 test methods)". `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`
   has 26 `[TestMethod]` attributes at `0d1e91f0` (no `DataRow`). Harmless to the executor.

Everything else checked matches:

- `Mission_SpawnAgent_Patch.cs:9-10` attributes, `[HarmonyPrefix]` at 54, `[HarmonyPostfix]` at 68.
- `PatchShieldPolicy.cs:134-148` `ExcludedTargetMethods` (seven entries as listed); `"ManagedCallbacks"`
  on the namespace list (line 105), `TaleWorlds.Engine` not on it.
- `Native2ManagedTargets.cs:33-34`; RCA line 33 (ButterLib `BlankTranspiler` on the shim).
- `FileLogger.cs:8-12` comment.
- 14 files override `OnAgentBuild`, `ShaderPrecompilePlayerAgentGuard.cs` among them.
- `TaomHowdahMachine.cs:28`, `TaomHowdahStandingPoint.cs:30`, `TaomMumakilPlatform.cs:29`,
  `TaomMumakilStandingPoint.cs:36`.
- Decompile: `ManagedScriptHolder.TickComponents` lines 240-267 as quoted; `TWParallel` delegate line 13
  and `For` lines 72-82; `Mission` lines 3546, 3601, 3617, 3652, 4174, 6742 (empty `TickDebugAgents`),
  the two `OnAgentBuild` loops; `MissionBehavior.cs:61`; `ScriptComponentBehavior.cs:215`;
  `MBAnimation.cs:143-146`. One overload each for every Patch98 target.
- Installed IL: `TickComponents` 351 bytes, `For(Int32,Int32,Single,ParallelForWithDtAuxPredicate,Int32)`
  at IL_0024, IL_004d, IL_0076, IL_0134 and `callvirt OnTick` at IL_00a1; `SpawnAgent` 2209 bytes,
  `OnAgentBuild` at IL_078d and IL_080e; `OnPreTick` 55 bytes, `WaitTickCompletion` at IL_0001 and
  `OnPreMissionTick` at IL_0023; `WaitTickCompletion` 17 bytes; `OnTick` is `IsFamilyOrAssembly`, virtual.
- Engine doc section 6 quotes (`0x6EAAE0`, state 1, "blocks that worker", "a frame spike").
- `harmony-patches.md` thread table row for `Mission.SpawnAgent -> OnAgentBuild` and "is a claim until a
  log line proves it" (line 162).
- Plan 028's amendment quote (028 line 1277) and the members listed under "Plan 028's code" (028 lines
  759-790, 925-975, 1020-1034, 648-663, 1096-1102); `SettingsFingerprintTests` pin 9 at base (12 after
  028); co-op doc counts 337, 120, 217 at base, 340 and 123 after 028.
- Exemplars exist: `CreatureBanditsWiringTests.cs:47-54` (`TargetOf`) and `:469-487`;
  `FeatureModuleHooksTests` `ProbeMissionBehavior : MissionLogic`; `PatchCategoryApplierTests` `new Harmony`;
  `AiPartySizeServiceTests.cs:539` `new TaomSettings()`; `PartyIconScaleTranspilerTests`,
  `TranspilerSiteBindingTests`, `AnimaliaWiringTests`, `RepoPaths.ReadSource(path, stripComments)`,
  `GameAssemblies`, `MissionBehaviorLifecycleTests`, `binding-gate.runsettings`.
- RefAsm commands match `.github/workflows/csharp.yml` lines 59 and 67. No `Benchmark` category or
  `TAOM_RUN_BENCHMARKS` exists yet, so "1 passed" holds. `Patch98`/`Patch99` are free in Main,
  Dependencies, TAOM.Tests and the registry. `<Version value="v2.0.32" />`; the subject is 69 characters.

## Checklist

| Item | Result |
|---|---|
| Executable from plan plus repo | Yes, except Blocking 1 |
| Every step ends in a command with an exact result | Yes (Step 14's "re-read every sentence" is the usual exception) |
| TDD order for C# | Yes; Step 12's wiring pins have no real RED (non-blocking 12); `MissionTickProfilerBehavior` named Not-tested |
| Issue line | "filed by the orchestrator before execution" |
| Binding ADRs named | ADR-002, 003, 004, 005, 007, 008 with one line each; rules files named |
| Protected files | None edited; Step 0 says none |
| Single-owner files | `SubModule.cs`: two comment replacements with exact text and a diff check; `IoC.cs`, csproj out of scope |
| STOP conditions specific | Yes (call-site counts, open-delegate dispatch, thread model, Patch98 taken, setting counts) |
| Done criteria machine-checkable | Yes; two greps are vacuous or comment-sensitive (non-blocking 3, 4) |
| Planned-at and drift paths vs Scope | Consistent; drift list covers every in-scope path plus read-only references |
| Non-deploying commands with `-p:ModuleId=` | Yes, on build and test |
| No worktree path or branch name | None |
| No CHANGELOG step | Explicitly excluded |
| Em or en dashes | None in the plan (scanned for U+2013 and U+2014) |
| Secrets | None |
