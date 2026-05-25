# Codex Adversarial Review -- CrashReport feature (2026-05-25)

You are reviewing a TAOM commit that adds a comprehensive crash diagnostic capture feature for Bannerlord v1.4.5. The feature is **inspired by but not a port of** BetterExceptionWindow (BEW, GNU AGPL v3) -- BEW was used only as a design reference for what to patch and what to display; the implementation is TAOM-native.

The feature has already been through Phase 1 (`/deep-review` with 5 parallel agents) which surfaced 1 HIGH + 2 MED + 3 LOW findings. All 6 fixed in-session. RCA at `docs/reviews/rca-crash-report-2026-05-25.md`. **Your job is Phase 2: find what Claude + deep-review missed.**

Prior Codex pre-reviews on this project have caught HIGH bugs that shipped past 4-5 deep-review agents (CompanionTactics inventory mutations, MixedFormations engine threading, SmartCavalryAI NaN propagation, EquipPresets `InventoryLogic.TransferCommand` bypass, etc.). Be adversarial.

## TAOM ID CHEATSHEET (use ONLY these)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

(This feature has NO ID-dependent work, but the cheatsheet is here for completeness.)

## READ FIRST

- `docs/features/crash-report.md` -- the feature documentation: architecture, catch points, BUTR coexistence, MCM toggles, data captured, performance posture.
- `docs/reviews/rca-crash-report-2026-05-25.md` -- the Phase 1 RCA. It documents the 6 findings already fixed. Don't re-flag the same bugs.
- `CHANGELOG.md` (the 2026-05-25 "feat(crash-report):" entry) -- full per-file inventory + design rationale.
- `~/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_no_aspirational_enum_values.md` -- the rule the Phase 1 RCA cites as the repeat-offender pattern (3 occurrences in 19 days).
- `~/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_user_facing_promise_must_match_code.md` -- related rule.
- `~/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_movementorder_cctor_mission_current.md` -- TAOM-specific Harmony-attach timing rule.
- `~/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md` -- relevant for the dev-trigger MissionBehavior.

## Known Suspects -- CONFIRM or DISPUTE each

1. **Harmony Finalizer on `MBSubModuleBase.OnSubModuleLoad` chicken-and-egg.** TAOM registers `Patch37_CrashReport` inside its own `OnSubModuleLoad` -- the very method it's trying to Finalize. The Finalizer is in place BEFORE the patch list is registered, so TAOM's own `OnSubModuleLoad` should be covered. But what about the gap between the Patch37 registration and the Finalizer actually being JIT-attached to the method? Walk the Harmony `PatchCategory(...)` call and confirm the attach happens synchronously before `PatchCategory` returns. If async or deferred, TAOM's own `OnSubModuleLoad` (lines 96-212 after the CrashReport block) is uncatchable.

2. **`ScreenManagerUpdateFinalizer` patches a `private static` method.** The decompile confirms v1.4.5 has `private static void Update()` no-arg overload, AND `public static void Update(IReadOnlyList<int>)`. The patch uses `[HarmonyPatch(typeof(ScreenManager), "Update", new Type[0])]`. Verify: does Harmony 2.4.2's `[HarmonyPatch(Type, string, Type[])]` resolve the **private** overload by signature, or does it need `BindingFlags.NonPublic | BindingFlags.Static` in `AccessTools.Method`? If the attribute can't see private methods, our Finalizer silently never attaches -- a critical hot-path catch point would be missing without any error.

3. **`CrashReportPatchHelper.ResolveService` lazy-cache race + early-init NRE.** `_service` is `private static ICrashReportService?`. First exception triggers `IoC.Resolve<ICrashReportService>()`. But the Finalizer is registered in `OnSubModuleLoad` BEFORE `IoC.Configure()` completes -- actually wait, `IoC.Configure()` is at line 88, Patch37 registration is at line 108. So IoC IS configured first. But what about the period BETWEEN `IoC.Configure()` and `PatchCategory("Patch37_CrashReport")`? An exception in that window (lines 89-107) would fire NO Finalizer at all. Confirm whether any code in lines 89-107 (UIExtender.Create, _uiExtender.Register/.Enable, ITimeAccelerationService resolve, `new Harmony("com.taom.mod")`) can throw. If yes, those throws are silently uncaught.

4. **`CrashReportService.HandleException` calls `CrashNotifier.Notify` which calls `InformationManager.ShowInquiry` from inside a Harmony Finalizer.** The Finalizer fires on a tick method -- typically the render/UI thread. Is `InformationManager.ShowInquiry` safe to call from inside a tick Finalizer? v1.4.5 may assume inquiries are only invoked from the campaign/menu thread state, not mid-`Mission.Tick`. Decompile `InformationManager.ShowInquiry` and walk what it does on call -- does it push to a queue (safe) or directly construct a screen (unsafe in a tick context)?

5. **`CrashBundleWriter.TryCopyFile` cannot read the live TAOM debug log.** `FileLogger` opens `_logPath` via `new StreamWriter(_logPath, true)` -- default `FileShare` for `StreamWriter` write mode is `FileShare.Read`. So another reader can open it for reading, but our writer holds the file open. Our `TryCopyFile` opens with `FileShare.ReadWrite` which is the READER side's declaration of what it tolerates from OTHER openers -- it does not relax the writer's restriction. Verify by reading FileLogger.cs constructor and tracing the StreamWriter default. If the writer's lock blocks copies, the bundle ZIP never contains `taom_debug.log` and the diagnostic value is halved.

6. **`Native2ManagedPatcher.AttachAll` patches hundreds of methods reflectively.** It iterates `*CallbacksGenerated` types in 3 AutoGenerated DLLs and calls `harmony.Patch(m, finalizer: new HarmonyMethod(bridge))` on every static method. Verify:
   - Does Harmony 2.4.2's `Patch` API throw on generic methods, methods with `ref`/`out` parameters, methods with specific calling conventions, or methods returning struct-by-ref?
   - We wrap each `harmony.Patch` in try/catch (per the source). But do we leak any per-patch state on failure -- e.g., is there a partial-attach that leaves Harmony in an inconsistent state?
   - The bridge method is `internal static Exception? Finalizer(Exception __exception)`. Verify this signature matches what Harmony expects for Finalizer methods (return type Exception or void, parameter `Exception __exception`).

7. **`AppDomainExceptionHook.OnUnhandled` may fire on TaleWorlds worker threads.** Bannerlord uses multi-threaded mission ticks (`_MT` suffix convention -- see `feedback_detect_engine_threading_via_mt_suffix.md`). If a worker thread throws an unhandled exception, `OnUnhandled` fires on that thread. `_service.HandleException` then composes the context, which involves WMI (single-threaded apartment concerns?), reflection over assemblies (safe), `Campaign.Current` / `Mission.Current` reads (NOT thread-safe -- vanilla code uses `TWSharedMutexReadLock`). Walk the collectors and identify any read that would be unsafe from a worker thread.

8. **Crash signature collision: top-5 frames is too shallow.** `CrashSignatureCalculator` uses `ExceptionType + originatingPatchTarget + first 5 stack frame names`. A common NRE pattern -- `Mission.CheckMissionEnded` walking a `MissionLogics` list with a null entry -- always produces the same top-5 frames regardless of WHICH `MissionLogic` is null. So all such crashes dedupe to the same signature, masking that different mods are responsible. Confirm whether this is intentional (high-level dedup for triage) or a defect (loses signal). Suggest a fix if it's a defect.

9. **`CrashReportSettings.Instance` reads in `Module.OnApplicationTick` postfix.** `CrashReportApplicationTickTrigger.Postfix` reads `CrashReportSettings.Instance` every application tick (60-200 Hz). MCM's `AttributeGlobalSettings<T>.Instance` accessor -- is it a cheap singleton field read, or does it traverse internal MCM state per call? Decompile `Bannerlord.MCM`'s `AttributeGlobalSettings<>.Instance` and confirm. If it's an expensive call, this is a real per-frame cost that the deep-review missed.

10. **`Patch37_CrashReport` Postfix collision with native2managed patches on Module.OnApplicationTick.** Both `Patch37_CrashReport.ModuleOnApplicationTickFinalizer` (Finalizer, priority 800) AND `CrashReportApplicationTickTrigger` (Postfix, priority 900) target `Module.OnApplicationTick`. The Postfix is in the same Patch37 category. When the Postfix throws (dev trigger), the Finalizer catches. But priority 900 > 800 -- does that affect Finalizer order? Walk Harmony's documented Finalizer ordering vs Postfix ordering and confirm there's no scenario where the Postfix throw bypasses the Finalizer.

11. **`ButterLibExceptionHandlerAdapter.TrySuspend` and `_butterLibSuspended` interaction.** `CrashReportService` sets `_butterLibSuspended = true` only if `TrySuspend()` returns true. If TrySuspend returns false (ButterLib absent, reflection failed), we retry on next crash -- correct. But if TrySuspend RETURNS TRUE but ButterLib re-enables itself somehow (some user toggles ButterLib's "Enabled" MCM setting at runtime), we never re-suspend. Confirm whether ButterLib has a runtime re-enable path; if so, our one-shot suspension may not be sticky.

12. **`AppDomainExceptionHook.Unsubscribe` race.** The fix in `OnSubModuleUnloaded` calls `IoC.Resolve<AppDomainExceptionHook>()?.Unsubscribe()` before `IoC.Dispose()`. But if `OnUnhandled` is firing on another thread AT THE SAME MOMENT, we could mid-call. The unsubscribe doesn't synchronize against in-flight handler execution. Probably acceptable (the worst case is one more report written after Unsubscribe). Confirm.

## File lists

**Feature module (60 new files):**

```
Main/Features/CrashReport/Domain/                 (18 sealed records)
Main/Features/CrashReport/Collectors/             (14 collectors + 5 utilities + ring buffers)
Main/Features/CrashReport/Rendering/              (ICrashReportRenderer, PlainText, Json, CrashBundleWriter)
Main/Features/CrashReport/Adapters/               (IButterLibExceptionHandlerAdapter + impl)
Main/Features/CrashReport/Hooks/                  (Patch37_CrashReport + helper + Native2Managed + AppDomain)
Main/Features/CrashReport/DevTriggers/            (mission-tick trigger + app-tick trigger + tagged exception)
Main/Features/CrashReport/UI/                     (CrashNotifier -- InformationManager.ShowInquiry)
Main/Features/CrashReport/CrashReportService.cs
Main/Features/CrashReport/ICrashReportService.cs
Main/Features/CrashReport/CrashReportIoC.cs
Main/Features/CrashReport/CrashReportSettings.cs  (MCM page)
```

**Modified core files:**

```
Main/Core/Logging/IModLogger.cs                   (added string? LogFilePath { get; })
Main/Core/Logging/FileLogger.cs                   (added _logPath field + property impl)
Main/IoC.cs                                       (CrashReportIoC.RegisterCrashReportFeature)
Main/SubModule.cs                                 (lines 96-119: Patch37 FIRST; line 594: mission behavior; line 633-646: Unsubscribe on unload)
Main/TAOM.csproj                                  (System.Management + System.IO.Compression refs)
CHANGELOG.md                                      (full feat entry under 2026-05-25)
docs/features/crash-report.md                     (new feature doc)
```

**Tests (5 files, 21 tests):**

```
TAOM.Tests/Features/CrashReport/ExceptionFrameBuilderTests.cs
TAOM.Tests/Features/CrashReport/StackFrameSnapshotBuilderTests.cs
TAOM.Tests/Features/CrashReport/CrashSignatureCalculatorTests.cs
TAOM.Tests/Features/CrashReport/RingBufferTests.cs
TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs
```

## REQUIRED SECTIONS in your output

### 1. VANILLA CODE

For each Harmony patch target, decompile the v1.4.5 method body from the INSTALLED DLLs (NOT `E:\Decompiled_Bannerlord\` which is a different version) and paste the relevant excerpt. Specifically:

- `TaleWorlds.DotNet.Managed.ApplicationTick` -- `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.DotNet.dll`
- `TaleWorlds.Engine.ScriptComponentBehavior.OnTick` -- `TaleWorlds.Engine.dll`
- `TaleWorlds.MountAndBlade.Module.OnApplicationTick` -- `TaleWorlds.MountAndBlade.dll`
- `TaleWorlds.MountAndBlade.View.MissionViews.MissionView.OnMissionScreenTick` -- `Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll`
- `TaleWorlds.ScreenSystem.ScreenManager.Tick` and `ScreenManager.Update()` (private no-arg) -- `TaleWorlds.ScreenSystem.dll`
- `TaleWorlds.MountAndBlade.Mission.Tick` -- `TaleWorlds.MountAndBlade.dll`
- `TaleWorlds.MountAndBlade.MissionBehavior.OnMissionTick` -- `TaleWorlds.MountAndBlade.dll`
- `TaleWorlds.MountAndBlade.MBSubModuleBase.OnSubModuleLoad` -- `TaleWorlds.MountAndBlade.dll`

Decompile `TaleWorlds.Library.InformationManager.ShowInquiry(InquiryData, bool, bool)` and walk what it does on call -- is it queued or direct?

Decompile `Bannerlord.ButterLib.ExceptionHandler.ExceptionHandlerSubSystem.Disable()` from `C:/Users/mikew/source/repos/TAOM/Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ButterLib.dll` -- confirm it can be called more than once safely.

### 2. Feature-specific deep analysis

Walk EACH of the 12 Known Suspects above. For each, paste evidence from the TAOM source AND the vanilla decompile. Mark each CONFIRMED, DISPUTED, or NEEDS-DESIGN-INPUT.

### 3. CONFIG CROSS-REFERENCE

The CrashReport feature has no XML/JSON config. Skip this section.

But cross-reference `CrashReportSettings.cs` MCM property names against every read site -- grep `Main/Features/CrashReport/**/*.cs` for each property name and verify reads exist. The Phase 1 RCA already found one decorative toggle (SuspendButterLibHandler) and fixed it; look for others.

### 4. FINDINGS OR OBSERVATIONS

For each finding, output:
- Severity: CRITICAL (data loss / hard crash / silent corruption) / HIGH (incorrect behavior visible to user) / MEDIUM (degraded behavior, possible to miss) / LOW (cleanup / nit)
- File:line
- What the bug is (1-2 sentences)
- Why it's a bug (evidence from vanilla + TAOM source)
- Suggested fix (specific code change)
- Whether you're CONFIRMED or NEEDS-DESIGN-INPUT

## QUALITY GATES

Before submitting your review:
1. Have you actually decompiled the vanilla targets (not just trusted the prompt)?
2. Have you read TAOM's actual source code (not just trusted the file list)?
3. For every "missing" claim, have you grep'd the codebase to confirm?
4. For every Harmony Finalizer concern, have you decompiled the target method?
5. For the thread-safety concerns (Native2Managed, AppDomain hook), have you traced the call chain from a worker thread back to TaleWorlds engine state reads?

If you can't do one of these for time/scope reasons, mark the finding NEEDS-DESIGN-INPUT and explain what verification is missing.

## Prior review lessons

**SUCCESSES (do these):**
- Config ID cross-ref caught rohan/dol_guldur mismatches in prior reviews
- Vanilla decompilation caught missing safety gates (MixedFormations IsFormationUnitPositionAvailable bypass)
- Lifecycle tracing caught stale-state bugs (cache survives session end)
- API misread reproduction (CompanionTactics Hero.BattleEquipment fallback target)

**FAILURES (don't do these):**
- Codex once assumed empire=Rohan (it is Dunland)
- Codex once flagged vanilla-matching code as a TAOM bug
- Codex once skipped a hard section ("can't verify") and rated overall PASS

## Output to

`docs/reviews/codex-adversarial-crash-report-2026-05-25.md`

Use the standard review structure: top-line verdict, vanilla code section, suspect-by-suspect analysis, findings table sorted by severity, recommended fixes.
