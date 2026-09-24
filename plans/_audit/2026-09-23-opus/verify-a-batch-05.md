# verify-a-batch-05: adversarial re-check of CORRECTNESS-04, CORRECTNESS-05, PERF-L4-02

Checker: fresh adversarial pass. All code reads are `git show b2e387db:<path>` (the working tree HEAD
is `4b5662b2`, one commit later). Engine reads come from `pwsh tools/taom-src.ps1 path <Type>` (v1.5.3
decompile cache). No build or test was run.

## CORRECTNESS-04: CONFIRMED (impact LOW)

**Claim**: four of the nine Patch37 finalizers sit on empty base virtuals and can never catch an
override's exception.

**Re-read, every link**:
- `git show b2e387db:Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs`: targets at `:45`
  (`ScriptComponentBehavior.OnTick`), `:63` (`MissionView.OnMissionScreenTick`), `:104`
  (`MissionBehavior.OnMissionTick`), `:113` (`MBSubModuleBase.OnSubModuleLoad`); header `:11` says
  "9 Harmony Finalizers". Line numbers in the finding are right.
- v1.5.3 decompile cache (`C:\Users\mikew\.taom-src\v1.5.3\`): `TaleWorlds.MountAndBlade.MissionBehavior.cs:150-152`
  empty virtual; `TaleWorlds.MountAndBlade.MBSubModuleBase.cs:8-10` empty `protected internal virtual`;
  `TaleWorlds.MountAndBlade.View.MissionViews.MissionView.cs:21-23` empty virtual;
  `TaleWorlds.Engine.ScriptComponentBehavior.cs:215-218` body is `Debug.FailedAssert(...)`.
- Tried to refute "the base cannot throw": `Debug.FailedAssert` (`TaleWorlds.Library.Debug.cs:117-123`)
  forwards to `DebugManager.Assert`, and the retail `MBDebugManager` implements `IDebugManager.Assert`
  as an empty body (`TaleWorlds.MountAndBlade.MBDebugManager.cs:33-35`). So even a `base.OnTick()` call
  cannot throw in the shipped game.
- Tried to refute "no other patch shares the method" (a throwing prefix/postfix on the same target
  would be inside the finalizer's try): `git grep -n -E "typeof\((MissionBehavior|MBSubModuleBase|MissionView|ScriptComponentBehavior)\)" b2e387db -- Main`
  returns only the four Patch37 lines. No TAOM prefix/postfix shares these targets (another mod's
  could, UNVERIFIED and irrelevant to TAOM's own attribution).
- Override exceptions still reach an outer finalizer: `Mission.OnTick` loops
  `MissionBehaviors[num2].OnMissionTick(dt)` at `TaleWorlds.MountAndBlade.Mission.cs:3757-3760`, called
  from `MissionState.cs:217`, under `Module.OnApplicationTick` (`Module.cs:478`, `:527`, `:534`), under
  `Managed.ApplicationTick` (`TaleWorlds.DotNet.Managed.cs:290-301`). Impact paragraph holds.
- `CrashReportService.cs:94` comment "9 per-tick Harmony Finalizers" and the `LogError` at `:110-113`
  confirmed; `CrashReportPatchHelper.cs:29-49` swallows (returns null) and `CrashBundleThrottle.cs:65-66`
  counts occurrences, as stated.
- No test references any of the four finalizer classes (`git grep` over `TAOM.Tests`).

**Not by-design, and the doc is worse than the finding says**: `docs/features/crash-report.md:63`
labels `MissionBehavior.OnMissionTick` "abstract, patches all overrides at JIT time", and `:285`
("Known limitations") repeats "is abstract. Harmony patches its overrides at JIT time". Both are false
at v1.5.3 (the method is a concrete empty virtual, and Harmony never patches overrides). The design
rationale rests on a wrong premise, so this is a defect in the design doc, not a decided tradeoff.
`:284` also claims Patch37 registered first "maximise[s] coverage of our own init", but TAOM's own
`OnSubModuleLoad` is an override, so the base finalizer covers none of it.

**Corrected evidence**: add `docs/features/crash-report.md:63,285` (the false "abstract" rationale) and
`:284`. Delta pre-existing (`7df18ca0`, 2026-05-25) confirmed by `git log --diff-filter=A`.

## CORRECTNESS-05: CONFIRMED (impact LOW), one delta commit mis-cited

**Claim**: BHA0001 (Patch88 scope) and BHA0006 (TeamTacticProbe) are analyzer false positives; the
`FormationAI._behaviors` reflection bind has no binding test.

**Re-read, every link**:
- Warnings exist as quoted: orchestrator raw build log (session scratchpad `raw\build-main-cold.log`
  lines 5-6) shows `Patch88_InitializeLordPartyPropertiesScope.cs(22,2): warning BHA0001 ... Type
  'TaleWorlds.CampaignSystem.Party.PartyComponents.InitializationArgs'` (nesting dropped) and
  `TeamTacticProbe.cs(40,22): warning BHA0006 ... Expected 'System.Collections.Generic.List`1', actual
  'System.Collections.Generic.List<TaleWorlds.MountAndBlade.BehaviorComponent>'`.
- `git show b2e387db:.../Patch88_InitializeLordPartyPropertiesScope.cs`: `:22` is the
  `[HarmonyPatch(typeof(LordPartyComponent.InitializationArgs), nameof(...InitializeLordPartyProperties))]`
  attribute; `:27` prefix takes `Hero owner`. The `nameof` only compiles if the member exists.
- Engine (`TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent.cs`): `:14` nested
  `public class InitializationArgs`, `:29` `public void InitializeLordPartyProperties(MobileParty mobileParty, Hero owner)`,
  `:129` the call from `OnMobilePartySetOnCreation`. All three line numbers right.
- Test `TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs:81-98` resolves
  `LordPartyComponent+InitializationArgs` with `AccessTools.Method` (what Harmony does) and pins
  `mobileParty`, `owner`; baseline `raw\tests.trx` records
  `InitializeLordPartyProperties_StillResolves_OnTheNestedArgs_MobilePartyAndOwner` outcome `Passed`.
- Runtime support I added: `taom_debug_2026-09-23_13-43-40.log` has 0 `HarmonyException|Patching exception`
  lines and the campaign batch runs past `_harmony.PatchCategory("Patch88_LordPartyTemplate")`
  (`SubModule.cs:1605` at `b2e387db`; the next providers log at 13:55:27 to 13:55:30). An unresolved
  `[HarmonyPatch]` target would throw from `PatchCategory`.
- `git show b2e387db:.../TeamTacticProbe.cs:38-42`: fail-soft `FieldRefAccess<FormationAI, List<BehaviorComponent>>("_behaviors")`;
  `:52-53` returns early when null, so a lost bind prints no rows at all (not even `{none}`), which is
  the "silently blank" the finding says. Engine `TaleWorlds.MountAndBlade.FormationAI.cs:41`
  `private readonly List<BehaviorComponent> _behaviors`, exact type match.
- Runtime support I added for BHA0006: `taom_debug_2026-09-19_17-30-21.log` 17:36:31 prints
  `[Doctrine] ... {Charge=1,PullBack=1,Reserve=1,Stop=1,Advance=1,Vanguard=1}`, which only
  `AppendArmedRows` produces after a successful `_behaviors` read. The bind works in the real game.
- Gap holds: `git grep -n "_behaviors" b2e387db -- TAOM.Tests` finds nothing; `CultureDoctrineBindingTests.cs:122-123`
  pins `_currentTactic`, `:210-224` pins the public `FormationAI` surface; `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`
  and `docs/reference/taleworlds-api-snapshot/reflection-sites.md` carry no `FormationAI` row either.

**Not by-design**: the repo previously drove BHA warnings to zero (`docs/reviews/REVIEW-LOG.md:990`,
`CHANGELOG-2026-H1.md:5458`), so a two-warning build is drift, not a decision. Note for the fix: past
BHA0001 false positives were cleared by restructuring, not pragmas (`Patch77_BodyGeneratorView_OnFinalize.cs:18-20`
attribute order, `Patch35_OrderOfBattleVM_Ctor.cs:12-15` explicit ctor form, `InventoryVMAdapter.cs:132-136`
public path). A scoped `#pragma` is not banned by any ADR (ADR-003 and ADR-005 cover `#region` and
`#if DEBUG` only), so the sketch is acceptable, but the plan should say why restructuring does not apply.

**Corrected evidence**: the `_behaviors` bind (the BHA0006 site, `TeamTacticProbe.cs:36-42`) came in
`3fb3b391` (2026-09-17, "status line lists armed rows"), per `git blame -L 36,42`; the file itself came
in `5dfa8f4c` (2026-09-16). `75f1880a` touched the file but not these lines. Binding-test range for the
public surface is `:210-224`. Delta stays `introduced`.

## PERF-L4-02: CONFIRMED (impact MED; the measurement understates it), two counts and one citation corrected

**Claim**: the Patch89 `DisableGlobalLoadingWindow` postfix runs a 12-frame stack walk and a durable,
flushed `LogInfo` on every rendered frame of the main menu and party screen.

**Re-read, every link**:
- `git show b2e387db:Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs:30-35`
  postfix calls `MapLoadTracer.TraceWithCallers("LOADING-WINDOW lowered")`; its own comment `:17-18`
  assumes "a handful of times per session, not per frame". `docs/features/map-load-diagnostics.md:48-49`
  states the same assumption, so the per-frame rate contradicts the design, it is not a decided tradeoff.
- `MapLoadTracer.cs:62-82`: `new StackTrace`, `Math.Min(frames + 2, ...)` = 12 frames, `GetMethod()` per
  frame, `StringBuilder`; `:52` `logger.LogInfo`. `Main/IoC.cs:233` (at `b2e387db`) registers
  `IModLogger` as `FileLogger` (no decorator), and `FileLogger.cs:85` `LogInfo` is `durable: true`, so
  `Enqueue` (`:90-95`) calls `Drain` (`:99-136`) on the calling thread: `lock (_writeLock)`, `WriteLine`,
  `Flush()` at `:124`.
- Applied unconditionally for every player: `SubModule.cs:390-419` (at `b2e387db`) patches
  `Patch89_MapLoadDiagnostics_Lifecycle` with no MCM or debug gate. `502f7cde` is an ancestor of the
  release commit `96f17fec`; `git tag --contains 502f7cde` lists `v2.0.29` and `v2.0.30`. Shipped.
- Engine: `TaleWorlds.Engine.LoadingWindow.cs:31-44` (v1.5.3) runs the body whether or not the window
  is up (`:35` tests `IsLoadingWindowActive`, `:41` clears it unconditionally). `SandBox.GauntletUI.GauntletPartyScreen.cs:113-119`
  (decompiled this run with `pwsh tools/taom-src.ps1 path SandBox.GauntletUI.GauntletPartyScreen`) calls
  `LoadingWindow.DisableGlobalLoadingWindow()` unconditionally in `OnFrameTick`. `MBInitialScreenBase`
  did not decompile under the name I tried; its per-frame call is proven by the log caller chains below.
- Fix sketch is sound: `:41` clears the flag before any postfix runs, so the guard must be a prefix
  that captures `IsLoadingWindowActive` (public getter, `LoadingWindow.cs:5`) into `__state`, as the
  finding says.

**Measurement re-run** (`E:\Steam\...\bin\Win64_Shipping_Client\Logs`, read only):
- `taom_debug_2026-09-23_13-43-40.log`: 83,803,347 bytes, 265,061 lines (match). `grep -c "LOADING-WINDOW lowered"`
  gives **262,763** (the finding's 262,831 does not reproduce; `grep -c "\[MapLoad\]"` gives 262,833, so
  the finding likely counted all `[MapLoad]` lines). Share is 99.1%, not 99.2%. `GauntletPartyScreen.OnFrameTick`
  140,047 (match), `BodyGeneratorView.OnTick` 248 (match), `MBInitialScreenBase.OnFrameTick` as first
  caller 121,358 (finding 121,359). Per-minute 13:45 to 13:49: 21,554 / 21,528 / 21,534 / 21,533 / 21,560
  (match, about 359 a second); party-screen minutes 13:57 to 14:18 run 4,000 to 7,800 a minute.
- Stronger evidence the finding missed: `taom_debug_2026-09-20_10-06-26.log` is **1,158,497,918 bytes
  (1.16 GB), 4,098,264 lines, of which 4,095,062 are `LOADING-WINDOW lowered`**, 4,095,057 of them from
  `MBInitialScreenBase.OnFrameTick`: the main menu left open from 10:06 to 13:26 (about 20,500 a minute).
  `taom_debug_2026-09-21_17-58-02.log` is 130 MB with 461,338. The 30 retained logs total 1.5 GB (`du -ch`).
  `FileLogger` retains 30 files (`FileLogger.cs:34`), so an idle main menu can put tens of GB on a
  player's disk.

**Crash-bundle citation corrected**: the full log is copied into the bundle by
`Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:46` (`TryCopyFile(zip, "taom_debug.log", ...)`,
`CompressionLevel.Optimal`, `:87-98`), not by the files cited (`CrashReportService.cs:97` is a comment,
`CrashReportIoC.cs:48` wires the tail collector). The report's own log tail is 500 lines
(`Collectors/LogTailCollector.cs:13`), read by scanning the whole file line by line (`:44-59`), so a
crash on the main menu or party screen gets a tail that is almost all trace lines (500 lines is about
1.4 s of main-menu frames) after reading up to a gigabyte.

**Still UNMEASURED**: per-frame time cost of the stack walk plus flush (no Stopwatch or fps A/B).

## What I did not cover

- No build or test run (not granted); the Patch88 binding result is the orchestrator's baseline trx.
- CORRECTNESS-04: the remaining five Patch37 targets (`Managed.ApplicationTick`, `Module.OnApplicationTick`,
  `ScreenManager.Tick`, `ScreenManager.Update`, `Mission.Tick`) were not audited. `Mission.Tick`
  (`Mission.cs:1839-1842`) is a thin native wrapper; whether managed exceptions from native callbacks
  unwind through it is native behaviour and UNVERIFIED. Another mod's prefix or postfix on the four
  targets was not checked.
- CORRECTNESS-05: Harmony's runtime application of `Patch88_InitializeLordPartyPropertiesScope` is inferred
  from zero Harmony exceptions and the batch continuing; I did not find a per-class "applied" log line.
- PERF-L4-02: frame-time cost not measured; `MBInitialScreenBase` source not decompiled (namespace
  guess failed); the size of a real crash bundle built from a 1 GB log was not tested.
