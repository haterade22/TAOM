OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019edb40-e20d-7162-997d-4990613d16b3
--------
user
# Codex adversarial review -- re-patch crash fix (issue #288)

You are an adversarial reviewer. Read the actual files in this repo and decompile vanilla targets where
relevant. Verify each Known Suspect as CONFIRMED or DISPUTED with evidence (cite file:line and, for
TaleWorlds APIs, decompile the INSTALLED v1.4.6 DLLs at
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/` via ilspycmd -- do
NOT trust `E:/Decompiled_Bannerlord/`). Use `--` not the em-dash. Output to this file's `.md` sibling.

## The change (2 files only -- ignore all other uncommitted work in the tree)

Crash (issue #288): the ShaderPrecompilation walk crashed entering item 2/9 with a HarmonyException.
Root cause: `SubModule.OnGameInitializationFinished` applies ~26 `_harmony.PatchCategory(...)` calls +
manual `_harmony.Patch(...)` + `BattleLoadStallWatchdog.Start()` on EVERY game init with no guard.
Harmony patches are process-global, so the 2nd game init in one process re-applies everything; the
non-idempotent `DeliverOffSpring_RaceAssert_Patch` transpiler, chained twice, runs the 2nd pass on IL
the 1st already NOPped, cannot find its `Debug.SilentAssert` anchor, and throws.

File 1 -- `Main/SubModule.cs`: added `private static bool _gameInitPatchesApplied;` and, at the TOP of
`OnGameInitializationFinished` (right AFTER `base.OnGameInitializationFinished(game)`), a guard:
`if (_gameInitPatchesApplied) return; _gameInitPatchesApplied = true;` -- so the whole patch-wiring body
runs once per process. Mirrors the existing `_missionTimePatchesApplied` guard in
`OnMissionBehaviorInitialize`.

File 2 -- `Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs`: the two
`throw new ArgumentException(...)` sites (anchor-not-found) were replaced with
`LogTranspilerDegradation(...); return newInstructions.AsEnumerable();`, and a private static
`LogTranspilerDegradation` helper added that does `try { IoC.Resolve<IModLogger>()?.LogWarning(...) } catch {}`.
Mirrors the existing graceful-degradation pattern in
`Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs`.

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. **Guard correctness / blast radius.** Read the FULL body of `OnGameInitializationFinished`. Does the
   once-per-process guard break ANYTHING that genuinely needs per-game re-execution? Classify every
   statement (PatchCategory / manual Patch / static Initialize / IoC.Resolve / watchdog Start). Confirm
   the `game` parameter is never used inside the guarded body, and that the genuine per-game
   `campaignStarter.AddBehavior` / `AddModel` registrations live in a DIFFERENT method (`OnGameStart`),
   not here. Flag any statement that captures per-game state or registers a per-Game callback inside the
   guarded body (that would be a real bug under the guard).

2. **Watchdog lifetime.** Read `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs`. Is
   `Start()` correct to call once per process (process-lifetime singleton), or does it need a fresh
   Start per game/mission? Does it have its own idempotency guard? Does the once-guard change its
   behavior for the worse?

3. **Transpiler soft-fail returns UNMODIFIED IL.** In `DeliverOffSpring_RaceAssert_Patch.cs`, confirm
   both early-return points (`callIndex < 0` and `startIndex < 0`) return `newInstructions` while it is
   still unmodified (the NOP-mutation loop runs AFTER both checks). Confirm the soft-fail is harmless
   (the patch is pure noise-reduction). Confirm the `LogTranspilerDegradation` try/catch can't surface
   an exception to the Harmony caller.

4. **PatchShield interaction.** `TAOM.Dependencies.Foundation.PatchShield` installs a Finalizer on every
   Harmony patch and auto-unpatches an offending owner on MissingMethod/MissingField/TypeLoad. With the
   once-per-process guard, if PatchShield unpatches a TAOM category on a transient failure, the guard
   prevents re-application on a later game init -- is that a real regression risk, or acceptable
   (the pre-guard per-game re-application was itself the crash, so "re-apply every game for resilience"
   was never a working design)? Assess and give a verdict. If it IS a real risk, propose the minimal
   mitigation.

5. **base call ordering.** Confirm `base.OnGameInitializationFinished(game)` runs every game init
   (outside the guard) and that this is correct against the MBSubModuleBase contract in installed v1.4.6.

## Also look for what we might have missed

- Any other non-idempotent transpiler in a per-game-init category that would STILL crash if its category
  is ever re-applied via a path the guard doesn't cover (e.g. a direct `PatchCategory` call elsewhere).
  Grep `Main/**/Hooks/**` + `Main/**/Patches/**` for `[HarmonyTranspiler]` + `throw`.
- Thread-safety of the `_gameInitPatchesApplied` flag (is `OnGameInitializationFinished` always on the
  main thread?).
- Whether `_gameInitPatchesApplied` being `static` (vs instance) matters -- is `SubModule` a singleton
  the engine constructs once?

## Output

A findings section (0 CRITICAL / N HIGH / N MED / N LOW), a per-suspect CONFIRM/DISPUTE table with
evidence, and a short verdict (SHIP / NEEDS-FIX). The TAOM verdict is that this is a clean root-cause fix
mirroring two existing proven patterns; try hard to find a hole in that.

ERROR: The process "50976" not found.
codex
I’ll treat this as a review-only pass: read the two intended files, inspect related patch/transpiler surfaces, and decompile installed v1.4.6 APIs where the claim hinges on TaleWorlds behavior. I’ll write the review artifact rather than modifying production code.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg --files' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
 succeeded in 528ms:
 M CHANGELOG.md
 M Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs
 M Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs
 M Main/SubModule.cs
?? docs/reviews/codex-adversarial-repatch-crash-fix-2026-06-18.md
?? docs/reviews/codex-adversarial-repatch-crash-fix-2026-06-18.prompt.md
?? docs/reviews/rca-repatch-crash-2026-06-18.md
warning: could not open directory '.codex_tmp/tmp1fft9w7e/': Permission denied
warning: could not open directory '.codex_tmp/tmp1fyfj2_j/': Permission denied
warning: could not open directory '.codex_tmp/tmp29jamgyn/': Permission denied
warning: could not open directory '.codex_tmp/tmp2owwhlqy/': Permission denied
warning: could not open directory '.codex_tmp/tmp5516wbdv/': Permission denied
warning: could not open directory '.codex_tmp/tmp78qze76y/': Permission denied
warning: could not open directory '.codex_tmp/tmp8gds9xfp/': Permission denied
warning: could not open directory '.codex_tmp/tmpbwbm4o8d/': Permission denied
warning: could not open directory '.codex_tmp/tmpc85yzrbw/': Permission denied
warning: could not open directory '.codex_tmp/tmpi3maldm8/': Permission denied
warning: could not open directory '.codex_tmp/tmpkr2gpims/': Permission denied
warning: could not open directory '.codex_tmp/tmpktgkfgay/': Permission denied
warning: could not open directory '.codex_tmp/tmplcnrd81l/': Permission denied
warning: could not open directory '.codex_tmp/tmplorpih3w/': Permission denied
warning: could not open directory '.codex_tmp/tmpm39f2s15/': Permission denied
warning: could not open directory '.codex_tmp/tmpov4yy7nx/': Permission denied
warning: could not open directory '.codex_tmp/tmpsanxk9fp/': Permission denied
warning: could not open directory '.codex_tmp/tmpwy0s04pv/': Permission denied
warning: could not open directory '.codex_tmp/tmpyqy6lg5o/': Permission denied
warning: could not open directory '.codex_tmp2/tmpulsj5ei5/': Permission denied
warning: could not open directory '.pytest_cache/': Permission denied

2026-06-18T15:02:21.705433Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 1.6 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 1639ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
I found the prompt/artifact pair already in `docs/reviews/`: `codex-adversarial-repatch-crash-fix-2026-06-18.prompt.md` and its `.md` sibling. I’ll use that sibling as the requested output target and keep unrelated dirty files out of scope.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/SubModule.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 526ms:
    1: using HarmonyLib;
    2: using System.Collections.Generic;
    3: using System.Linq;
    4: using System.Reflection;
    5: using System.Reflection.Emit;
    6: using TaleWorlds.CampaignSystem;
    7: using TaleWorlds.Core;
    8: using TaleWorlds.Library;
    9: using TAOM.Core.Logging;
   10: 
   11: namespace TAOM.Features.RaceAge.Hooks;
   12: 
   13: // Noise-reduction (NOT a crash fix). Vanilla HeroCreator.DeliverOffSpring carries a
   14: // Debug.SilentAssert(mother.Race == father.Race). In TAOM, mixed-race couples are normal, so
   15: // this assert fires on every cross-race birth. It is harmless for players — ButterLib's
   16: // DebugManagerWrapper.SilentAssert only writes a Debug-level log line and delegates, and
   17: // MBDebug.SilentAssert only calls Debugger.Break() when a debugger is attached. This transpiler
   18: // NOPs the assert call so it (a) stops breaking a developer's attached debugger on every
   19: // mixed-race birth and (b) drops the recurring "Silent Assert Failed!" debug-log spam.
   20: // Behavior is otherwise unchanged — the birth proceeds identically with or without this patch.
   21: [HarmonyPatch(typeof(HeroCreator), "DeliverOffSpring")]
   22: [HarmonyPatchCategory("Patch13_RaceAge")]
   23: public static class DeliverOffSpring_RaceAssert_Patch
   24: {
   25:     [HarmonyTranspiler]
   26:     static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
   27:     {
   28:         var newInstructions = new List<CodeInstruction>(instructions);
   29:         var callIndex = -1;
   30: 
   31:         // Find the SilentAssert call by matching method name on the operand,
   32:         // since CallerXxx default parameter attributes can cause MethodInfo.Equals mismatch
   33:         for (int i = 0; i < newInstructions.Count; i++)
   34:         {
   35:             if (newInstructions[i].opcode == OpCodes.Call &&
   36:                 newInstructions[i].operand is MethodInfo mi &&
   37:                 mi.Name == "SilentAssert" &&
   38:                 mi.DeclaringType?.Name == "Debug")
   39:             {
   40:                 callIndex = i;
   41:                 break;
   42:             }
   43:         }
   44: 
   45:         if (callIndex < 0)
   46:         {
   47:             // Anchor gone (already NOPped by a prior application of this transpiler, or the engine IL
   48:             // changed). This patch is pure noise-reduction, so degrade to a no-op instead of throwing
   49:             // out of PatchCategory and crashing the mod (mirrors RefreshCharacterEntityAuxPatch).
   50:             LogTranspilerDegradation("Debug.SilentAssert call not found in DeliverOffSpring IL.");
   51:             return newInstructions.AsEnumerable();
   52:         }
   53: 
   54:         // Walk backwards from the call to find the start of the argument sequence.
   55:         // The IL pattern is:
   56:         //   ldarg.0 (mother)
   57:         //   callvirt get_CharacterObject
   58:         //   callvirt get_Race
   59:         //   ldarg.1 (father)
   60:         //   callvirt get_CharacterObject
   61:         //   callvirt get_Race
   62:         //   ceq
   63:         //   ldstr "" (message)
   64:         //   ldc.i4.0 (getDump)
   65:         //   ldstr "..." (callerFile)
   66:         //   ldstr "..." (callerMethod)
   67:         //   ldc.i4 275 (callerLine)
   68:         //   call Debug.SilentAssert
   69:         //
   70:         // Find ldarg.0 that starts the race comparison by scanning backwards
   71:         var startIndex = -1;
   72: 
   73:         for (int i = callIndex - 1; i >= 0; i--)
   74:         {
   75:             if (newInstructions[i].opcode == OpCodes.Ldarg_0)
   76:             {
   77:                 // Verify this ldarg.0 is followed (within a few instructions) by
   78:                 // a call to a property getter named "get_Race"
   79:                 for (int j = i + 1; j < callIndex && j <= i + 4; j++)
   80:                 {
   81:                     if (newInstructions[j].operand is MethodInfo propGetter &&
   82:                         propGetter.Name == "get_Race")
   83:                     {
   84:                         startIndex = i;
   85:                         break;
   86:                     }
   87:                 }
   88: 
   89:                 if (startIndex >= 0)
   90:                     break;
   91:             }
   92:         }
   93: 
   94:         if (startIndex < 0)
   95:         {
   96:             LogTranspilerDegradation("Race-comparison start (ldarg.0 ... get_Race) not found in DeliverOffSpring IL.");
   97:             return newInstructions.AsEnumerable();
   98:         }
   99: 
  100:         // NOP out the entire SilentAssert sequence (args + call)
  101:         for (int i = startIndex; i <= callIndex; i++)
  102:         {
  103:             newInstructions[i].opcode = OpCodes.Nop;
  104:             newInstructions[i].operand = null;
  105:         }
  106: 
  107:         return newInstructions.AsEnumerable();
  108:     }
  109: 
  110:     private static void LogTranspilerDegradation(string detail)
  111:     {
  112:         try
  113:         {
  114:             IoC.Resolve<IModLogger>()?.LogWarning(
  115:                 $"[RaceAge] DeliverOffSpring_RaceAssert_Patch transpiler degrading to no-op — {detail} " +
  116:                 $"The harmless mixed-race SilentAssert noise-reduction will not apply this session (no gameplay effect).");
  117:         }
  118:         catch { /* logger resolution failure must not surface to the transpiler caller */ }
  119:     }
  120: }

 succeeded in 539ms:
    1: using HarmonyLib;
    2: using System;
    3: using System.Collections.Generic;
    4: using System.Linq;
    5: using System.Reflection.Emit;
    6: using TaleWorlds.Core;
    7: using TaleWorlds.MountAndBlade;
    8: using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
    9: using TAOM.Core.Logging;
   10: 
   11: namespace TAOM.Features.CharacterSelection.Patches;
   12: 
   13: [HarmonyPatch(typeof(BodyGeneratorView), "RefreshCharacterEntityAux")]
   14: [HarmonyPatchCategory("Late_Transpiler")]
   15: public class RefreshCharacterEntityAuxPatch
   16: {
   17:     public static MBActionSet GetActionSet(BodyGeneratorView bodyGeneratorView)
   18:     {
   19:         var race = bodyGeneratorView.BodyGen.Race;
   20:         var monster = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(race);
   21:         if (monster == null)
   22:             monster = TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(0); // fallback to human
   23:         return MBGlobals.GetActionSetWithSuffix(monster, bodyGeneratorView.BodyGen.IsFemale, "_facegen");
   24:     }
   25: 
   26:     [HarmonyTranspiler]
   27:     static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
   28:     {
   29:         // Phase 9b #160 — degrade gracefully on IL pattern mismatch. Prior to this fix, any of the
   30:         // three lookups (ctor / ActionSet method / Newobj match) throwing ArgumentException at
   31:         // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
   32:         // bricking startup even though the rest of TAOM (and most of CharacterSelection) is
   33:         // unaffected. Soft-fail: log once with the specific gap, return original instructions
   34:         // unchanged, let the game boot. The CharacterSelection face-generator action-set inject
   35:         // doesn't apply, but no other feature breaks.
   36:         var ctor = AccessTools.Constructor(typeof(AgentVisualsData), Type.EmptyTypes);
   37:         if (ctor == null)
   38:         {
   39:             LogTranspilerDegradation("AgentVisualsData parameterless constructor not found via reflection.");
   40:             return instructions;
   41:         }
   42: 
   43:         var actionSetMethod = typeof(AgentVisualsData).GetMethod(nameof(AgentVisualsData.ActionSet));
   44:         if (actionSetMethod == null)
   45:         {
   46:             LogTranspilerDegradation("AgentVisualsData.ActionSet method not found via reflection.");
   47:             return instructions;
   48:         }
   49: 
   50:         var newInstructions = new List<CodeInstruction>(instructions);
   51:         var insertionIndex = -1;
   52: 
   53:         for (int i = 0; i < newInstructions.Count - 1; i++)
   54:         {
   55:             if (newInstructions[i].opcode == OpCodes.Newobj && ctor.Equals(newInstructions[i].operand))
   56:             {
   57:                 insertionIndex = i + 1;
   58:                 break;
   59:             }
   60:         }
   61: 
   62:         if (insertionIndex < 0)
   63:         {
   64:             LogTranspilerDegradation("AgentVisualsData Newobj IL pattern not found in BodyGeneratorView.RefreshCharacterEntityAux.");
   65:             return instructions;
   66:         }
   67: 
   68:         newInstructions.InsertRange(insertionIndex, new[]
   69:         {
   70:             new CodeInstruction(OpCodes.Ldarg_0),
   71:             new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RefreshCharacterEntityAuxPatch), nameof(GetActionSet))),
   72:             new CodeInstruction(OpCodes.Callvirt, actionSetMethod)
   73:         });
   74: 
   75:         return newInstructions.AsEnumerable();
   76:     }
   77: 
   78:     private static void LogTranspilerDegradation(string detail)
   79:     {
   80:         try
   81:         {
   82:             IoC.Resolve<IModLogger>()?.LogError(
   83:                 $"[CharacterSelection] RefreshCharacterEntityAuxPatch transpiler degrading to no-op — {detail} " +
   84:                 $"BodyGenerator face-generator action-set injection will not apply this session; " +
   85:                 $"verify v1.3.15 IL via ilspycmd on TaleWorlds.MountAndBlade.GauntletUI.dll if behavior is unexpected.");
   86:         }
   87:         catch { /* logger resolution failure must not surface to the transpiler caller */ }
   88:     }
   89: }

 succeeded in 540ms:
    1: using System;
    2: using System.Threading;
    3: using TAOM.Core.Logging;
    4: using TAOM.Features.CrashReport;
    5: 
    6: namespace TAOM.Features.BattleLoadDiagnostics;
    7: 
    8: // Background-thread stall detector. A battle-load hang freezes the MAIN thread, so a
    9: // main-thread timer can't time itself out — this uses a thread-pool Timer. When the
   10: // loading window (opened at Mission.Initialize, closed at first OnMissionTick) has been
   11: // open longer than the threshold, it:
   12: //   1. writes a GUARANTEED "STILL LOADING" marker naming the last phase reached
   13: //      (IModLogger's queue is thread-safe and flushed by its own background thread), then
   14: //   2. best-effort triggers the CrashReport bundle so the user can ship the log in one
   15: //      action.
   16: // Some CrashReport collectors read live mission state; from this thread while the main
   17: // thread is frozen they may return partial data — that's acceptable, the marker + the
   18: // already-flushed phase log are the primary signal and the bundle is a bonus.
   19: public sealed class BattleLoadStallWatchdog : IDisposable
   20: {
   21:     private const string Tag = "[BattleLoad]";
   22:     private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
   23: 
   24:     private readonly IModLogger _logger;
   25:     private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
   26:     private readonly IBattleLoadDiagnosticsService _service;
   27:     private readonly ICrashReportService? _crashReport;
   28: 
   29:     private Timer? _timer;
   30:     private long _lastWindowOpenedTicks = -1L;
   31:     private bool _firedForCurrentWindow;
   32: 
   33:     public BattleLoadStallWatchdog(
   34:         IModLogger logger,
   35:         IBattleLoadDiagnosticsSettingsProvider settings,
   36:         IBattleLoadDiagnosticsService service,
   37:         ICrashReportService? crashReport = null)
   38:     {
   39:         _logger = logger;
   40:         _settings = settings;
   41:         _service = service;
   42:         _crashReport = crashReport;
   43:     }
   44: 
   45:     public void Start()
   46:     {
   47:         if (_timer != null) return;
   48:         _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
   49:     }
   50: 
   51:     // Pure decision: fire once per window when it's been open at or past the threshold.
   52:     public static bool ShouldFire(bool windowOpen, double elapsedSeconds, double thresholdSeconds, bool alreadyFired)
   53:         => windowOpen && !alreadyFired && elapsedSeconds >= thresholdSeconds;
   54: 
   55:     private void Poll()
   56:     {
   57:         try
   58:         {
   59:             if (!_settings.IsEnabled || !_settings.StallWatchdogEnabled) return;
   60: 
   61:             var openedAt = BattleLoadLoadingWindow.OpenedAtUtc;
   62:             if (!openedAt.HasValue)
   63:             {
   64:                 _firedForCurrentWindow = false; // window closed — ready for the next load
   65:                 return;
   66:             }
   67: 
   68:             // New window since we last fired? reset the latch so each load gets one fire.
   69:             long openedTicks = openedAt.Value.Ticks;
   70:             if (openedTicks != _lastWindowOpenedTicks)
   71:             {
   72:                 _lastWindowOpenedTicks = openedTicks;
   73:                 _firedForCurrentWindow = false;
   74:             }
   75: 
   76:             double elapsed = (DateTime.UtcNow - openedAt.Value).TotalSeconds;
   77:             if (!ShouldFire(true, elapsed, _settings.StallWatchdogSeconds, _firedForCurrentWindow)) return;
   78: 
   79:             _firedForCurrentWindow = true;
   80:             _logger.LogError($"{Tag} WATCHDOG STILL LOADING after {elapsed:F0}s — last {_service.CurrentStatusLine}");
   81: 
   82:             if (_settings.StallWatchdogBundleEnabled && _crashReport != null)
   83:             {
   84:                 try
   85:                 {
   86:                     var zip = _crashReport.HandleException(
   87:                         new BattleLoadStallException($"Mission load stalled >{elapsed:F0}s; last {_service.CurrentStatusLine}"),
   88:                         "BattleLoadStallWatchdog");
   89:                     if (!string.IsNullOrEmpty(zip))
   90:                         _logger.LogError($"{Tag} WATCHDOG bundle written: {zip}");
   91:                 }
   92:                 catch (Exception ex)
   93:                 {
   94:                     _logger.LogWarning($"{Tag} WATCHDOG bundle failed: {ex.GetType().Name}: {ex.Message}");
   95:                 }
   96:             }
   97:         }
   98:         catch (Exception ex)
   99:         {
  100:             try { _logger.LogWarning($"{Tag} WATCHDOG poll failed: {ex.GetType().Name}: {ex.Message}"); }
  101:             catch { /* never propagate from a timer callback */ }
  102:         }
  103:     }
  104: 
  105:     public void Dispose()
  106:     {
  107:         _timer?.Dispose();
  108:         _timer = null;
  109:     }
  110: }

 succeeded in 596ms:
    1: using Bannerlord.UIExtenderEx;
    2: using HarmonyLib;
    3: using TaleWorlds.CampaignSystem;
    4: using TaleWorlds.Core;
    5: using TaleWorlds.Engine;
    6: using TaleWorlds.Library;
    7: using TaleWorlds.Localization;
    8: using TaleWorlds.MountAndBlade;
    9: using TaleWorlds.MountAndBlade.ComponentInterfaces;
   10: using TAOM.Features;
   11: using TAOM.Features.BannerInjection;
   12: using TAOM.Features.HeroRace;
   13: using TAOM.Core.Infrastructure;
   14: using TAOM.Core.Logging;
   15: using TAOM.Features.CharacterCreation;
   16: using TAOM.Features.FactionMap;
   17: using TAOM.Features.InitialChildGeneration;
   18: using TAOM.Adapters;
   19: using TAOM.Features.Diplomacy;
   20: using TAOM.Features.Diplomacy.Hooks;
   21: using TAOM.Features.Diplomacy.Models;
   22: using TAOM.Features.Execution;
   23: using TAOM.Features.Execution.Hooks;
   24: using TAOM.Features.Execution.Models;
   25: using TAOM.Features.RaceAge;
   26: using TAOM.Features.RaceAge.Models;
   27: using TAOM.Features.StartupResources;
   28: using TAOM.Features.NamedCompanions;
   29: using TAOM.Features.TroopProgression;
   30: using TAOM.Features.TroopWeight;
   31: using TAOM.Features.TroopWeight.Hooks;
   32: using TAOM.Features.AtmospherePersistence.Hooks;
   33: using TAOM.Features.TroopProgression.Models;
   34: using TAOM.Features.AdvancedCombat;
   35: using TAOM.Features.CulturalFeats.Models;
   36: using TAOM.Features.CustomBattles;
   37: using TAOM.Features.CustomBattles.Hooks;
   38: using TAOM.Features.Warg;
   39: using TAOM.Features.Spider;
   40: using TAOM.Features.BattleBalance;
   41: using TAOM.Features.BattleBalance.Models;
   42: using TAOM.Features.Arena.Models;
   43: using TAOM.Features.Encyclopedia;
   44: using TAOM.Features.Encyclopedia.Models;
   45: using TAOM.Features.MainMenuCustomizer;
   46: using TAOM.Features.NativeSkinFixes;
   47: using TAOM.Features.ShaderPrecompilation;
   48: using TAOM.Features.Siege;
   49: using TAOM.Features.Siege.Models;
   50: using TAOM.Features.ArmyTargeting;
   51: using TAOM.Features.ArmyTargeting.Models;
   52: using TAOM.Features.TimeAcceleration;
   53: using TAOM.Features.BannerColorPersistence;
   54: using TAOM.Features.BannerColorPersistence.Hooks;
   55: using TAOM.Features.LocalizationOverride;
   56: using TAOM.Features.LocalizationOverride.Hooks;
   57: using TAOM.Features.SpecialResources;
   58: using TAOM.Features.SpecialResources.Hooks;
   59: using TAOM.Features.CareerSystem;
   60: using TAOM.Features.CareerSystem.Models;
   61: using TAOM.Features.SettlementGuards;
   62: using TAOM.Features.SettlementGuards.Hooks;
   63: using TAOM.Features.RevoltTuning;
   64: using TAOM.Features.BanditManagement;
   65: using TAOM.Features.BanditManagement.Models;
   66: using TAOM.Features.CastleRecruitment;
   67: using TAOM.Features.CastleRecruitment.Hooks;
   68: using TAOM.Features.SiegeDismount.Hooks;
   69: using TAOM.Features.MixedFormations.Hooks;
   70: using TAOM.Features.SmartCavalryAI.Hooks;
   71: using TAOM.Features.FiefManagement;
   72: using TAOM.Features.FiefManagement.Hooks;
   73: using TAOM.Features.SettlementNameplateFade;
   74: using TAOM.Features.SettlementNameplateFade.Hooks;
   75: using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
   76: using BehaviorTreeWrapper;
   77: using TaleWorlds.CampaignSystem.CampaignBehaviors;
   78: 
   79: namespace TAOM;
   80: 
   81: public class SubModule : MBSubModuleBase
   82: {
   83:     private Harmony _harmony;
   84:     private UIExtender? _uiExtender;
   85:     private ITimeAccelerationService? _timeAccelerationService;
   86:     private static float _shaderTickAccumulator;
   87:     private static ShaderPrecompileRunner _shaderRunner;
   88:     private static bool _missionTimePatchesApplied;
   89:     private static bool _gameInitPatchesApplied;
   90: 
   91:     protected override void OnSubModuleLoad()
   92:     {
   93:         base.OnSubModuleLoad();
   94: 
   95:         IoC.Configure();
   96: 
   97:         // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
   98:         // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
   99:         // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
  100:         // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
  101:         // is the IoC.Configure() call itself — if THAT throws, the entire feature is
  102:         // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
  103:         // a manual DI container; accept and document the residual.
  104:         _harmony = new Harmony("com.taom.mod");
  105:         if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
  106:         {
  107:             try
  108:             {
  109:                 _harmony.PatchCategory("Patch37_CrashReport");
  110:                 IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
  111:                 if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
  112:                 {
  113:                     IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
  114:                 }
  115:             }
  116:             catch (System.Exception ex)
  117:             {
  118:                 IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
  119:             }
  120:         }
  121: 
  122:         _uiExtender = UIExtender.Create("TAOM");
  123:         _uiExtender.Register(typeof(SubModule).Assembly);
  124:         _uiExtender.Enable();
  125: 
  126:         // Patch41_McmLayoutFix — flip MCM's embedded options-screen prefabs from VerticalBottomToTop
  127:         // to VerticalTopToBottom (v1.4.0 layout regression). MCM's prefabs are embedded in
  128:         // Bannerlord.MBOptionScreen and load via WidgetFactoryManager.CreateAndRegister, which bypasses
  129:         // UIExtenderEx's [PrefabExtension] hook — so this is a Harmony Postfix, not a PrefabExtension.
  130:         // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
  131:         // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
  132:         // must already be attached when MCM calls CreateAndRegister.
  133:         _harmony.PatchCategory("Patch41_McmLayoutFix");
  134: 
  135:         _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();
  136: 
  137:         // Must be first — intercepts GetLocalizedText before any game texts are resolved.
  138:         // Loads English string overrides from taom_module_strings.xml (removes hardcoded "The" articles).
  139:         _harmony.PatchCategory("Patch25_LocalizationOverride");
  140:         var pathService0 = IoC.Resolve<IPathService>();
  141:         var logger0 = IoC.Resolve<IModLogger>();
  142:         var xmlPath = System.IO.Path.Combine(pathService0.ModuleDataPath, "taom_module_strings.xml");
  143:         try
  144:         {
  145:             var overrides = LocalizationOverrideLoader.ParseOverridesFromFile(xmlPath);
  146:             foreach (var kvp in overrides)
  147:                 MBTextManager_GetLocalizedText_Patch.RegisterOverride(kvp.Key, kvp.Value);
  148:             logger0.LogInfo($"[LocalizationOverride] Registered {overrides.Count} English string overrides");
  149:         }
  150:         catch (System.Exception ex)
  151:         {
  152:             logger0.LogError($"[LocalizationOverride] Failed to load overrides: {ex.Message}");
  153:         }
  154: 
  155:         _harmony.PatchCategory("Patch18_CulturalFeats");
  156:         _harmony.PatchCategory("Patch19_CustomBattles");
  157:         // Patch0_BattleScenes: loads TAOM's sp_battle_scenes.xml (full 0-255 map_indices coverage) so the
  158:         // TAOM_Map Main_map grid's extended indices (158-255) resolve to real battle terrains instead of
  159:         // FailedAsserting against vanilla's 1-157 table. Re-enabled 2026-06-01 (TAOM_Map ships Main_map +
  160:         // the extended XML exists; 3 patch targets verified against installed 1.4.5). In-game grid validation
  161:         // pending the worldmap_battle_scene_grid re-author. See docs/reference/worldmap-battle-scene-grid.md.
  162:         _harmony.PatchCategory("Patch0_BattleScenes");
  163:         // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first
  164: 
  165:         var pathService = IoC.Resolve<IPathService>();
  166:         var logger = IoC.Resolve<IModLogger>();
  167:         FactionMapPaths.Initialize(pathService.ModuleRootPath, logger);
  168: 
  169:         var allianceHook = IoC.Resolve<IOnAllianceAction>();
  170:         var peaceHook = IoC.Resolve<IOnPeaceAction>();
  171:         DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
  172:         AllianceCampaignBehavior_EndAlliance_Patch.Initialize(logger);
  173:         AllianceCampaignBehavior_StartAlliance_Patch.Initialize(logger);
  174:         AllianceCampaignBehavior_AddAllianceDecision_Patch.Initialize(logger);
  175:         DeclareWarAction_ApplyInternal_Patch.Initialize(logger);
  176:         MakePeaceAction_ApplyInternal_Patch.Initialize(logger);
  177: 
  178:         var executionHook = IoC.Resolve<IOnExecutionAction>();
  179:         ExecutionIoC.InitializeHooks(executionHook);
  180: 
  181:         TroopWeightIoC.InitializeHooks(
  182:             IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
  183:             IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),
  184:             IoC.Resolve<IOnRecruitmentVMRefreshPartyProperties>(),
  185:             IoC.Resolve<IOnPartyVMPopulatePartyListLabel>(),
  186:             IoC.Resolve<IOnPartyUpgraderUpgradeReadyTroops>(),
  187:             IoC.Resolve<TroopWeightDisplayHook>());
  188: 
  189:         CustomBattlesIoC.InitializeHooks(
  190:             IoC.Resolve<IOnGetCustomBattleCommanders>(),
  191:             IoC.Resolve<IOnGetCustomBattleFactions>(),
  192:             IoC.Resolve<IOnGetDefaultTroopOfFormation>(),
  193:             IoC.Resolve<ISideCommanderFilter>(),
  194:             logger);
  195: 
  196:         _harmony.PatchCategory("Patch21_ShaderPrecompilation");
  197:         _shaderRunner = IoC.Resolve<ShaderPrecompileRunner>();
  198:         ShaderPrecompilationIoC.InitializeHooks(logger, _shaderRunner);
  199: 
  200:         _harmony.PatchCategory("Patch22_ArmyTargeting");
  201:         // Patch49: Finalizer guarding vanilla Army.FindBestGatheringSettlementAndMoveTheLeader,
  202:         // which NREs (Army.cs:726 settlement.GatePosition / 659 Kingdom.Settlements, v1.4.6) when a
  203:         // besieger army can't resolve a gathering fortification — a map-tick CTD on siege start.
  204:         // No TAOM patch is on the stack; aggressive Patch22 targeting just makes it more reachable.
  205:         // Crash report 2026-06-17. See the patch's doc-comment.
  206:         _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
  207:         _harmony.PatchCategory("Patch30_MixedFormations");
  208:         // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
  209:         // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in
  210:         // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
  211:         // which is null during OnSubModuleLoad and would crash JIT prep with NRE.
  212: 
  213:         var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
  214:         var bannerColorService = IoC.Resolve<IBannerColorService>();
  215:         var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();
  216: 
  217:         Banner_TryGetBannerDataFromCode_Transpiler.Initialize(bannerColorConfig, logger);
  218:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(bannerColorService);
  219:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(logger);
  220:         Clan_UpdateBannerColor_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  221:         Banner_GetFirstIconColor_Patch.Initialize(bannerColorService);
  222:         BannerEditorView_OnTick_Patch.Initialize(bannerColorService, logger);
  223:         CampaignUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  224:         SandBoxUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  225:         SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  226:         PartyVM_RefreshCurrentCharacterInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  227:         HeroViewModel_FillFrom_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  228:         PartyCharacterVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  229:         ClanPartyItemVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  230:         CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Initialize(bannerColorService);
  231:         var agentColorStore = IoC.Resolve<IAgentColorStore>();
  232:         Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
  233:         Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
  234:         AgentVisuals_Create_Patch.Initialize(bannerColorService);
  235:         MapConversationTableau_SpawnOpponentLeader_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  236:         MapConversationTableau_SpawnOpponentBodyguard_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  237:         MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  238:         OrderOfBattleHeroItemVM_RefreshInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
  239: 
  240:         Mission_Initialize_Patch.Initialize(logger);
  241: 
  242:         // Patch42_CastleRecruitment — castle notable recruitment. Targets RecruitmentCampaignBehavior
  243:         // + AiVisitSettlementBehavior (both in TaleWorlds.CampaignSystem, no View/Mission.cctor
  244:         // dependency, safe in OnSubModuleLoad). The transpilers swap the AI IsCastle gate to a runtime
  245:         // toggle; the postfix invokes the private CheckRecruiting for castles. All fail-safe.
  246:         var castleRecruitmentSettings = IoC.Resolve<ICastleRecruitmentSettingsProvider>();
  247:         CastleAiToggle.Initialize(castleRecruitmentSettings);
  248:         Patch42_AiHourlyTick_Transpiler.Initialize(logger);
  249:         Patch42_FillSettlements_Transpiler.Initialize(logger);
  250:         Patch42_HourlyTickParty_Postfix.Initialize(castleRecruitmentSettings, logger);
  251:         _harmony.PatchCategory("Patch42_CastleRecruitment");
  252: 
  253:         InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
  254:     }
  255: 
  256:     protected override void OnBeforeInitialModuleScreenSetAsRoot()
  257:     {
  258:         base.OnBeforeInitialModuleScreenSetAsRoot();
  259:         IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();
  260: 
  261:         // BattleLoadDiagnostics collection: a battle/scene load that hung last session left
  262:         // an inflight marker (phase-4 wrote it; phase-6/end never ran to clear it). If it
  263:         // survived to this main menu, the previous load never finished — surface a notice so
  264:         // the player knows to send the diagnostic log. See docs/features/battle-load-diagnostics.md.
  265:         try
  266:         {
  267:             var stallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
  268:             var stale = stallMarker?.TryConsumeStaleMarker();
  269:             if (stale != null)
  270:                 Features.BattleLoadDiagnostics.StallReportNotifier.Notify(stale);
  271:         }
  272:         catch { /* never block the main menu over a diagnostic */ }
  273: 
  274:         // NativeSkinFixes — three native MinHook detours that fix engine bugs
  275:         // TaleWorlds won't: covers_head morph freeze, hair cloth orphan, beard
  276:         // cloth orphan. Loads TAOM.NativeSkinFixes.dll from Main/_Module/bin
  277:         // and pattern-scans TaleWorlds.Native.dll for the hook targets at
  278:         // install time. Failure is logged and the game continues vanilla — no
  279:         // crash, no NRE. See docs/features/native-skin-fixes.md.
  280:         NativeSkinFixesInstaller.Install(IoC.Resolve<IModLogger>());
  281: 
  282:         // Pre-compile Shaders — RE-ENABLED 2026-06-17 (issue #287). Walks the all-characters battle
  283:         // (character/equipment shaders) then each TAOM battle scene (terrain + forced-atmosphere
  284:         // shaders — the d3dcompiler battle-load CTD class). Drives ShaderPrecompileRunner; progress
  285:         // shows on the loading screen + a 1 Hz status toast. See docs/features/shader-precompilation.md.
  286:         if (Module.CurrentModule.GetInitialStateOptionWithId("TaomPrecompileShaders") == null)
  287:         {
  288:             Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
  289:                 id:                  "TaomPrecompileShaders",
  290:                 name:                new TextObject("{=taom_precompile_shaders}Pre-compile Shaders"),
  291:                 orderIndex:          100,
  292:                 action:              () => InformationManager.ShowInquiry(new InquiryData(
  293:                     "Shader Pre-compilation",
  294:                     "Loads a battle with all TAOM troops, then walks each TAOM battle scene, to " +
  295:                     "pre-compile every shader the game would otherwise compile mid-battle.\n\n" +
  296:                     "THIS TAKES A LONG TIME (1-2 hours+). Leave it running — progress shows on the " +
  297:                     "loading screen and as a status line. One-time process; it eliminates in-game " +
  298:                     "stutter and the intermittent battle-load crash/hang.\n\n" +
  299:                     "When you see 'Shader pre-compilation COMPLETE', you can play.",
  300:                     true, true, "Start", "Cancel",
  301:                     () =>
  302:                     {
  303:                         _shaderTickAccumulator = 0f;
  304:                         _shaderRunner?.Begin();
  305:                     },
  306:                     () => InformationManager.HideInquiry())),
  307:                 isDisabledAndReason: () => (false, new TextObject("")),
  308:                 enabledHint:         new TextObject("{=taom_precompile_hint}Pre-compiles shaders to eliminate in-game stutter + the battle-load crash. Run once after installing TAOM."),
  309:                 isHidden:            null));
  310:         }
  311:     }
  312: 
  313:     protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
  314:     {
  315:         base.OnGameStart(game, gameStarterObject);
  316: 
  317:         // Session-level diagnostic snapshot: OS / CLR / mod list / mod-stack
  318:         // assembly versions / campaign context. Runs once per session and is
  319:         // idempotent so OnGameStart on save-load doesn't spam.
  320:         try
  321:         {
  322:             IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>()?.LogSessionSnapshot();
  323:         }
  324:         catch { /* diagnostic is best-effort, never break OnGameStart */ }
  325: 
  326:         if (gameStarterObject is CampaignGameStarter campaignStarter)
  327:         {
  328:             var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
  329:             campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));
  330: 
  331:             var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
  332:             var bannerExclusionService = IoC.Resolve<IBannerExclusionService>();
  333:             campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService, bannerExclusionService));
  334: 
  335:             var ccContentService = IoC.Resolve<ICharacterCreationContentService>();
  336:             var ccLogger = IoC.Resolve<IModLogger>();
  337:             campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));
  338: 
  339:             campaignStarter.RemoveBehaviors<InitialChildGenerationCampaignBehavior>();
  340:             var childGenService = IoC.Resolve<IInitialChildGenerationService>();
  341:             campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));
  342: 
  343:             var costService = IoC.Resolve<ITroopCostService>();
  344:             // Phase 9b #173 — careerPassives resolved once for the whole CulturalFeats + CareerSystem
  345:             // + TroopProgression model registration block. Replaces all CareerPassiveHelper static
  346:             // calls with instance-injected ICareerPassiveService.
  347:             var careerPassives = IoC.Resolve<TAOM.Features.CareerSystem.ICareerPassiveService>();
  348:             // Phase 9b #180 / partial #148 — IWageModifierService extraction. Hoists garrison-wage
  349:             // feat loop + Mordor/Gundabad/Umbar party-wage feats + Rohan mounted-wage scaling +
  350:             // recruitment-cost feats out of the model body, satisfying gamemodels.md rule 4.
  351:             var wageModifiers = IoC.Resolve<IWageModifierService>();
  352:             var volunteerService = IoC.Resolve<IVolunteerTierService>();
  353:             var recruitmentService = IoC.Resolve<IVolunteerRecruitmentService>();
  354:             var volunteerContextAdapter = IoC.Resolve<IVolunteerContextAdapter>();
  355:             // Hoisted: TaomVolunteerModel now consumes ICulturalFeatsService for the village
  356:             // volunteer-respawn-rate feats (Dunland/Gundabad/Dol Guldur/Mordor). The later
  357:             // "Cultural feat models" block reuses this same `culturalFeats` reference.
  358:             var culturalFeats = IoC.Resolve<TAOM.Features.CulturalFeats.ICulturalFeatsService>();
  359:             var recruitmentAlignment = IoC.Resolve<TAOM.Features.AlignmentRecruitment.IRecruitmentAlignmentService>();
  360:             campaignStarter.AddModel(new TaomCharacterStatsModel());
  361:             campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
  362:             campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));
  363: 
  364:             var raceAgeService = IoC.Resolve<IRaceAgeService>();
  365:             var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
  366:             var raceAgeLogger = IoC.Resolve<IModLogger>();
  367:             campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger));
  368:             campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
  369:             campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
  370:             campaignStarter.AddModel(new TaomHeroCreationModel());
  371: 
  372:             var diplomacyService = IoC.Resolve<IDiplomacyService>();
  373:             var wotrService = IoC.Resolve<IWarOfTheRingService>();
  374:             var diplomacyLogger = IoC.Resolve<IModLogger>();
  375:             campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger));
  376:             campaignStarter.AddBehavior(new PlayerAllianceProposalBehavior(diplomacyService, diplomacyLogger));
  377:             campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
  378:             campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
  379:             campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
  380: 
  381:             var wotrLogger = IoC.Resolve<IModLogger>();
  382:             campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
  383: 
  384:             var siegeDefenseService = IoC.Resolve<ISiegeDefenseService>();
  385:             var siegeDefenseLogger = IoC.Resolve<IModLogger>();
  386:             campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger));
  387:             campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));
  388: 
  389:             var executionRelationService = IoC.Resolve<IExecutionRelationService>();
  390:             var playerContext = IoC.Resolve<IPlayerContextAdapter>();
  391:             campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
  392: 
  393:             // Cultural feat models — Phase 9b #144/#176: dispatch logic extracted to
  394:             // ICulturalFeatsService. Each model is now a thin boundary that converts
  395:             // CultureObject → ICultureFeatAdapter and delegates (gamemodels.md rule 4).
  396:             // `culturalFeats` is resolved above (hoisted for TaomVolunteerModel).
  397:             campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
  398:             campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
  399:             campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
  400:             campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
  401:             campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
  402:             campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
  403:             campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
  404:             campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
  405:             campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
  406:             campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
  407:             campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
  408:             campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
  409:             campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
  410:             campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
  411:             campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
  412:             campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
  413:             campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
  414:             campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));
  415:             campaignStarter.AddModel(new TaomNotableSpawnModel(culturalFeats));
  416: 
  417:             // Battle balance models
  418:             var battleBalanceSettings = IoC.Resolve<IBattleBalanceSettingsProvider>();
  419:             var battleBalanceConfig = IoC.Resolve<IBattleBalanceConfigProvider>();
  420:             campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
  421:             campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
  422:             campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig));
  423: 
  424:             campaignStarter.AddModel(new TaomInformationRestrictionModel(IoC.Resolve<IEncyclopediaSettingsProvider>()));
  425: 
  426:             var armyTargetingService = IoC.Resolve<IArmyTargetingService>();
  427:             campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));
  428: 
  429:             var specialResourceService = IoC.Resolve<ISpecialResourceService>();
  430:             var specialResourceStorage = IoC.Resolve<ISpecialResourceStorageService>();
  431:             var specialResourceConfig = IoC.Resolve<ISpecialResourceConfigProvider>();
  432:             var specialResourceLogger = IoC.Resolve<IModLogger>();
  433:             var specialResourceBehavior = new SpecialResourcesBehavior(
  434:                 specialResourceService, specialResourceStorage, specialResourceConfig, specialResourceLogger);
  435:             campaignStarter.AddBehavior(specialResourceBehavior);
  436:             PartyScreenLogic_AddCommand_Patch.SetBehavior(specialResourceBehavior);
  437: 
  438:             var careerDataService = IoC.Resolve<ICareerDataService>();
  439:             var careerRegistry = IoC.Resolve<ICareerRegistry>();
  440:             var careerPassiveService = IoC.Resolve<ICareerPassiveService>();
  441:             var careerLogger = IoC.Resolve<IModLogger>();
  442:             campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));
  443:             var careerCreationHandler = IoC.Resolve<ICareerCreationHandler>();
  444:             var careerAbilityServiceForBehavior = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
  445:             campaignStarter.AddBehavior(new CareerCampaignBehavior(
  446:                 careerDataService, careerRegistry, careerPassiveService, careerCreationHandler, careerAbilityServiceForBehavior, careerLogger));
  447: 
  448:             var careerAdapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
  449:             // CareerSwitchDialogueBehavior used to take ICareerSwitchService too; that dependency
  450:             // moved to GauntletCareerScreen.OnChooseSwitchTarget (Codex Review #32 cleanup).
  451:             campaignStarter.AddBehavior(new CareerSwitchDialogueBehavior(
  452:                 careerDataService, careerRegistry, careerAdapterFactory, careerLogger));
  453: 
  454:             // Career-tied quest system (Phase 6) — offers/starts tier quests; CareerQuest : QuestBase
  455:             // is registered for saving by the auto-discovered CareerQuestSaveableTypeDefiner.
  456:             var careerQuestService = IoC.Resolve<Features.CareerSystem.ICareerQuestService>();
  457:             campaignStarter.AddBehavior(new Features.CareerSystem.Quests.CareerQuestCampaignBehavior(
  458:                 careerDataService, careerQuestService, careerLogger));
  459: 
  460:             // Career system GameModels — reuse careerPassiveService resolved above (line 334).
  461:             // Phase 9b #142 — agent-stat extraction: TaomAgentStatCalculateModel /
  462:             // TaomAgentApplyDamageModel now delegate UpdateAgentStats + damage-amp/red +
  463:             // shrug-off logic to ICareerAgentStatService (gamemodels.md rule 4).
  464:             var careerAgentStat = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>();
  465:             campaignStarter.AddModel(new TaomMapVisibilityModel(careerPassives));
  466:             campaignStarter.AddModel(new TaomInventoryCapacityModel(careerPassives));
  467:             var elephantAttackService = IoC.Resolve<Features.Elephant.IElephantAttackService>();
  468:             var spiderAttackService = IoC.Resolve<ISpiderAttackService>();
  469:             campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerPassiveService, careerAgentStat, elephantAttackService, spiderAttackService));
  470:             campaignStarter.AddModel<AgentApplyDamageModel>(new TaomAgentApplyDamageModel(careerAgentStat));
  471:             campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));
  472: 
  473:             var goldService = IoC.Resolve<IStartupGoldService>();
  474:             var influenceService = IoC.Resolve<IStartupInfluenceService>();
  475:             var startupLogger = IoC.Resolve<IModLogger>();
  476:             campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));
  477: 
  478:             var namedCompanionService = IoC.Resolve<INamedCompanionService>();
  479:             campaignStarter.AddBehavior(new NamedCompanionBehavior(namedCompanionService));
  480: 
  481:             // QuickActions: per-save inventory-search-box persistence (SyncData round-trips
  482:             // even when EnableInventorySearch is OFF — disabled = inert, not absent).
  483:             campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.QuickActions.Hooks.InventorySearchCampaignBehavior>());
  484: 
  485:             // EquipPresets: per-save preset persistence + orphan pruning. Unconditional registration
  486:             // so the SyncData round-trip preserves presets even when EnableEquipmentPresets is OFF
  487:             // (the MCM hint promises "existing presets are inert (preserved in save)").
  488:             campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.EquipPresets.Hooks.EquipmentPresetCampaignBehavior>());
  489: 
  490:             // FiefManagement (Patch36) — register UNCONDITIONALLY so the menu is always present
  491:             // and the EnableFiefManagement MCM toggle takes effect immediately at runtime.
  492:             campaignStarter.AddBehavior(new FiefHubCampaignBehavior(
  493:                 IoC.Resolve<IFiefHubMenuPresenter>(),
  494:                 IoC.Resolve<IFiefManagementSettingsProvider>()));
  495: 
  496:             // CompanionTactics (Patch35) — FormationPresets persistence behavior. Registered
  497:             // unconditionally so SyncData round-trips even when EnableFormationPresets is OFF.
  498:             campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(
  499:                 IoC.Resolve<Features.CompanionTactics.FormationPresets.IFormationPresetService>(),
  500:                 IoC.Resolve<IModLogger>()));
  501: 
  502:             // Messengers — paid messenger dispatch + dialog hooks + per-save SyncData persistence.
  503:             // Registered unconditionally so saves round-trip pending messengers even when
  504:             // EnableMessengers is OFF (disabled = inert, not absent).
  505:             campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>());
  506: 
  507:             // CultureMarketplace (#207) — daily injection of LOTRLOME items into town markets
  508:             // keyed by owner culture. No SyncData (stock lives in vanilla Settlement.ItemRoster).
  509:             campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
  510:                 IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
  511:                 IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
  512:                 IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
  513:                 IoC.Resolve<ITownRosterAdapter>(),
  514:                 IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
  515:                 IoC.Resolve<IModLogger>()));
  516: 
  517:             // CastleRecruitment (Patch42) — castle notable population + maintenance + volunteer fill +
  518:             // player "Recruit troops" castle menu + issue/quest suppression for castle notables.
  519:             // Registered unconditionally so the MCM master toggle takes effect at runtime.
  520:             campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
  521:                 IoC.Resolve<ICastleRecruitmentService>(),
  522:                 IoC.Resolve<IModLogger>()));
  523: 
  524:             // CultureConversion — conquered cross-culture fiefs gradually adopt the new owner's culture
  525:             // (troops, militia, identity). Registered unconditionally so SyncData round-trips conversion
  526:             // records and completed overrides re-apply on load even when the MCM toggle is off.
  527:             campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
  528:                 IoC.Resolve<Features.CultureConversion.ICultureConversionService>(),
  529:                 IoC.Resolve<Features.CultureConversion.ICultureConversionStore>(),
  530:                 IoC.Resolve<IModLogger>()));
  531:         }
  532:     }
  533: 
  534:     public override void OnGameInitializationFinished(Game game)
  535:     {
  536:         base.OnGameInitializationFinished(game);
  537: 
  538:         // Harmony patches are process-global (applied to methods, persist across games). Apply this
  539:         // whole per-game-init patch block ONCE per process — re-applying on a 2nd game init duplicates
  540:         // every prefix/postfix, restarts the BattleLoad watchdog, and CRASHES the non-idempotent
  541:         // DeliverOffSpring transpiler (chained twice, it can't find its already-NOPped anchor). The
  542:         // shader-precompile walk starts N custom games in one process and tripped exactly this on item 2;
  543:         // a player loading a 2nd campaign/custom-battle in one session hits the same crash.
  544:         // Mirrors _missionTimePatchesApplied in OnMissionBehaviorInitialize.
  545:         if (_gameInitPatchesApplied) return;
  546:         _gameInitPatchesApplied = true;
  547: 
  548:         _harmony.PatchCategory("Patch1_FirstTimeInit");
  549:         _harmony.PatchCategory("Patch2_RefreshTableau");
  550:         _harmony.PatchCategory("Patch3_SetRace");
  551:         _harmony.PatchCategory("Patch4_CharacterSpawner");
  552:         _harmony.PatchCategory("Patch5_FaceGen");
  553:         _harmony.PatchCategory("Late_Transpiler");
  554:         _harmony.PatchCategory("Late_ActionSetOverride");
  555:         _harmony.PatchCategory("Patch6_BannerEditor");
  556:         _harmony.PatchCategory("Patch7_FactionMap");
  557:         _harmony.PatchCategory("Patch9_RaceFilter");
  558:         _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
  559:         _harmony.PatchCategory("Patch8_SiegeCampGuard");
  560:         _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
  561:         _harmony.PatchCategory("Patch11_Diplomacy");
  562:         _harmony.PatchCategory("Patch12_WarOfTheRing");
  563: 
  564:         _harmony.PatchCategory("Patch14_Execution");
  565:         _harmony.PatchCategory("Patch15_BannerLayerLimit");
  566:         _harmony.PatchCategory("Patch16_AtmospherePersistence");
  567:         _harmony.PatchCategory("Patch17_TroopWeight");
  568:         _harmony.PatchCategory("Patch23_BannerColorPersistence");
  569:         _harmony.PatchCategory("Patch24_BannerDriftGuard");
  570:         _harmony.PatchCategory("Patch39_BanditPartySize");
  571:         _harmony.PatchCategory("Patch40_HideoutDescription");
  572:         _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
  573:         // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment
  574:         // ("post-sever tick AV") was actually the CanAttack charge crash at set_attack_entity
  575:         // (0x6BAB4E), which fired with AND without Patch47 and is fixed in data (LOTRLOME
  576:         // monster Flags). Patch47's own job verified working: severed riders die clean on-foot
  577:         // deaths (act_death_by_arrow class) instead of AVing in the native mounted-death path —
  578:         // which 1.4.6 still does on melee deaths (Die-path AV reading float-bits-as-index from
  579:         // a corrupted action record, debugger-proven 06-12). See docs/features/spider.md.
  580:         Features.Spider.Hooks.Agent_Die_SpiderDismount_Patch.Initialize();
  581:         _harmony.PatchCategory("Patch47_SpiderDeathDismount");
  582: 
  583:         // Patch48: the non-lethal sibling of Patch47. A CanDismount melee hit on a mounted Spider Rider AVs in
  584:         // native HandleBlowAux (reading 0x3) — the same broken non-vanilla mounted-dismount path Patch47 routes
  585:         // around on death. Strips CanDismount for spider riders so the native dismount never fires (the rider
  586:         // stays on the locked mount; damage still applies). Debugger-proven 2026-06-15. See docs/features/spider.md.
  587:         _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
  588: 
  589:         // Patch50: Finalizer swallowing a vanilla NRE in Agent.CheckToDropFlaggedItem (Agent.cs:3595),
  590:         // reached via the shared synthetic-bite path (CustomAttacksUtils.TakeDamage → RegisterBlow →
  591:         // OnAgentHit → affectedAgent.CheckToDropFlaggedItem) when a warg bites another warg (mount
  592:         // victim with a null wielded Item). Already caught by WargAttackService, but swallowing lets
  593:         // OnAgentHit finish and stops the log spam. Crash report 2026-06-17. See the patch doc-comment.
  594:         _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
  595: 
  596:         // Patch13_RaceAge — noise reduction (NOT a crash fix). NOPs the harmless
  597:         // mother.Race == father.Race SilentAssert in DeliverOffSpring that fires on every
  598:         // mixed-race birth (normal in TAOM). Stops the debugger break + debug-log spam.
  599:         _harmony.PatchCategory("Patch13_RaceAge");
  600: 
  601:         var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();
  602:         var specResLogger = IoC.Resolve<IModLogger>();
  603:         PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
  604:         PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
  605:         PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
  606:         _harmony.PatchCategory("Patch26_SpecialResources");
  607:         _harmony.PatchCategory("Patch27_CareerSystem");
  608:         _harmony.PatchCategory("Patch29_CCBodyProperties");
  609:         _harmony.PatchCategory("Patch44_CCNameAutofill");
  610:         _harmony.PatchCategory("Patch33_EquipPresets");
  611:         _harmony.PatchCategory("Patch34_QuickActions");
  612:         _harmony.PatchCategory("Patch35_CompanionTactics");
  613:         _harmony.PatchCategory("Patch36_FiefManagement");
  614:         SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
  615:         _harmony.PatchCategory("Patch38_SettlementNameplateFade");
  616: 
  617:         // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an
  618:         // intermittent battle-load hang leaves a log whose last line names the stuck phase
  619:         // (and, for the equipment phase, the agent + the item whose bo_ collision mesh is
  620:         // missing). The background stall watchdog auto-triggers a crash bundle on a freeze.
  621:         var battleLoadSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
  622:         var equipSnapshotAdapter = IoC.Resolve<IEquipmentSnapshotAdapter>();
  623:         var battleLoadStallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
  624:         Features.BattleLoadDiagnostics.Hooks.PlayerEncounter_Start_Patch.Initialize(battleLoadSvc);
  625:         Features.BattleLoadDiagnostics.Hooks.MissionState_OpenNew_Patch.Initialize(battleLoadSvc);
  626:         Features.BattleLoadDiagnostics.Hooks.BattleSceneSelection_Patch.Initialize(battleLoadSvc);
  627:         Features.BattleLoadDiagnostics.Hooks.Mission_Initialize_BattleLoad_Patch.Initialize(battleLoadSvc, battleLoadStallMarker);
  628:         Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.Initialize(battleLoadSvc, equipSnapshotAdapter);
  629:         _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
  630:         IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();
  631: 
  632:         // CompanionTactics — manual patch for the PRIVATE method
  633:         // OrderOfBattleHeroItemVM.GetCaptainTooltip (private in v1.3.15, can't use
  634:         // [HarmonyPatch] attribute binding).
  635:         var captainTooltipTarget = AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip");
  636:         if (captainTooltipTarget != null)
  637:             _harmony.Patch(captainTooltipTarget, postfix: new HarmonyMethod(
  638:                 typeof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip),
  639:                 nameof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip.Postfix)));
  640:         else
  641:             IoC.Resolve<IModLogger>().LogWarning("[CompanionTactics] OrderOfBattleHeroItemVM.GetCaptainTooltip not found — captain tooltip role hint will not appear");
  642: 
  643:         var settlementGuardService = IoC.Resolve<ISettlementGuardService>();
  644:         GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(settlementGuardService);
  645:         GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(settlementGuardService);
  646: 
  647:         // Manual patches for private GuardsCampaignBehavior methods (SandBox.dll)
  648:         var takeGuardTarget = GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod();
  649:         if (takeGuardTarget != null)
  650:             _harmony.Patch(takeGuardTarget, prefix: new HarmonyMethod(
  651:                 typeof(GuardsCampaignBehavior_TakeGuardAgentData_Patch),
  652:                 nameof(GuardsCampaignBehavior_TakeGuardAgentData_Patch.Prefix)));
  653:         else
  654:             IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] TakeGuardAgentDataFromGarrisonTroopList not found — custom guards will not apply");
  655: 
  656:         var spearTarget = GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod();
  657:         if (spearTarget != null)
  658:             _harmony.Patch(spearTarget, prefix: new HarmonyMethod(
  659:                 typeof(GuardsCampaignBehavior_GetSuitableSpear_Patch),
  660:                 nameof(GuardsCampaignBehavior_GetSuitableSpear_Patch.Prefix)));
  661:         else
  662:             IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] GetSuitableSpear not found — culture-specific spears will not apply");
  663: 
  664:         // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
  665:         var mobilePartyTarget = MobilePartyVisual_AddCharacterToPartyIcon_Patch.TargetMethod();
  666:         if (mobilePartyTarget != null)
  667:             _harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod(
  668:                 typeof(MobilePartyVisual_AddCharacterToPartyIcon_Patch),
  669:                 nameof(MobilePartyVisual_AddCharacterToPartyIcon_Patch.Postfix)));
  670:         else
  671:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MobilePartyVisual.AddCharacterToPartyIcon not found — party icon colors will not persist");
  672: 
  673:         // Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)
  674:         var agentVisualsCreateTarget = AgentVisuals_Create_Patch.TargetMethod();
  675:         if (agentVisualsCreateTarget != null)
  676:             _harmony.Patch(agentVisualsCreateTarget, prefix: new HarmonyMethod(
  677:                 typeof(AgentVisuals_Create_Patch),
  678:                 nameof(AgentVisuals_Create_Patch.Prefix)));
  679:         else
  680:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] AgentVisuals.Create not found — clan color randomness suppression will not apply");
  681: 
  682:         // Manual patches for MapConversationTableau (private methods in SandBox.View.dll)
  683:         var leaderTarget = MapConversationTableau_SpawnOpponentLeader_Patch.TargetMethod();
  684:         if (leaderTarget != null)
  685:             _harmony.Patch(leaderTarget, postfix: new HarmonyMethod(
  686:                 typeof(MapConversationTableau_SpawnOpponentLeader_Patch),
  687:                 nameof(MapConversationTableau_SpawnOpponentLeader_Patch.Postfix)));
  688:         else
  689:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentLeader not found — conversation tableau leader colors will not apply");
  690: 
  691:         var bodyguardTarget = MapConversationTableau_SpawnOpponentBodyguard_Patch.TargetMethod();
  692:         if (bodyguardTarget != null)
  693:             _harmony.Patch(bodyguardTarget, postfix: new HarmonyMethod(
  694:                 typeof(MapConversationTableau_SpawnOpponentBodyguard_Patch),
  695:                 nameof(MapConversationTableau_SpawnOpponentBodyguard_Patch.Postfix)));
  696:         else
  697:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentBodyguardCharacter not found — conversation tableau bodyguard colors will not apply");
  698:     }
  699: 
  700:     public override void OnMissionBehaviorInitialize(Mission mission)
  701:     {
  702:         base.OnMissionBehaviorInitialize(mission);
  703: 
  704:         // Apply Formation.SetMovementOrder patches (Patch31_SmartCavalryAI + Patch35
  705:         // CancelStanceOnMove) only once Mission.Current is non-null — MovementOrder's
  706:         // type initializer constructs static fields whose ctor reads
  707:         // Mission.Current.CurrentTime. Applying earlier crashes JIT prep with NRE.
  708:         if (!_missionTimePatchesApplied)
  709:         {
  710:             _missionTimePatchesApplied = true;
  711:             _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
  712:         }
  713: 
  714:         mission.AddMissionBehavior(new AdvancedCombatBehavior());
  715:         mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
  716:         mission.AddMissionBehavior(new AutonomousMovementPlayerController());
  717:         mission.AddMissionBehavior(new WargMissionBehavior());
  718:         mission.AddMissionBehavior(new SpiderMissionBehavior());
  719:         mission.AddMissionBehavior(new Features.Elephant.ElephantMissionBehavior());
  720:         mission.AddMissionBehavior(new SiegeDismountMissionBehavior());
  721:         mission.AddMissionBehavior(new MixedFormationsMissionBehavior());
  722:         mission.AddMissionBehavior(new SmartCavalryAIMissionBehavior());
  723:         mission.AddMissionBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());
  724: 
  725:         var colorStore = IoC.Resolve<IAgentColorStore>();
  726:         if (colorStore != null)
  727:             mission.AddMissionBehavior(new AgentColorStoreCleanupBehavior(colorStore));
  728: 
  729:         // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
  730:         // every other mod in the load chain. Dumps MissionBehaviors + MissionLogics
  731:         // on first OnMissionTick to taom_debug_*.log so user-uploaded crash logs
  732:         // contain enough data to identify mod-conflict bugs (BehaviorType=Logic +
  733:         // !MissionLogic null-cast offenders) and action-set anomalies.
  734:         var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
  735:         var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
  736:         var diagLogger = IoC.Resolve<IModLogger>();
  737:         if (diagSvc != null && raceMgr != null && diagLogger != null)
  738:             mission.AddMissionBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));
  739: 
  740:         // BattleLoadDiagnostics phase-6: "battle playable" marker on first tick + closes
  741:         // the loading window so the stall watchdog stands down and phase-5 stops logging.
  742:         var battleLoadDiagSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
  743:         if (battleLoadDiagSvc != null && battleLoadDiagSvc.IsEnabled)
  744:             mission.AddMissionBehavior(new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(
  745:                 battleLoadDiagSvc, IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>()));
  746: 
  747:         // Dev-trigger behavior watches the CrashReport MCM toggle and throws a tagged
  748:         // TaomDevTriggerException on the next OnMissionTick when the player flips
  749:         // "Throw On Next Mission Tick". QA only — no-op in normal play.
  750:         mission.AddMissionBehavior(new Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior());
  751: 
  752:         var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
  753:         if (careerAbilityService != null && Campaign.Current != null)
  754:         {
  755:             mission.AddMissionBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
  756:                 IoC.Resolve<ICareerDataService>(),
  757:                 careerAbilityService,
  758:                 IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
  759:                 IoC.Resolve<Features.CareerSystem.UI.IAbilityHudController>(),
  760:                 IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
  761:                 IoC.Resolve<IModLogger>()));
  762:         }
  763:     }
  764: 
  765:     protected override void OnApplicationTick(float dt)
  766:     {
  767:         _timeAccelerationService?.OnTick();
  768: 
  769:         // Shader pre-compilation walk: tick the runner every frame (responsive state transitions),
  770:         // and surface its status as a 1 Hz toast when a loading screen isn't already showing it.
  771:         var runner = _shaderRunner;
  772:         if (runner != null && runner.IsActive)
  773:         {
  774:             runner.Tick();
  775:             _shaderTickAccumulator += dt;
  776:             if (_shaderTickAccumulator >= 1f)
  777:             {
  778:                 _shaderTickAccumulator = 0f;
  779:                 if (!LoadingWindow.IsLoadingWindowActive && !string.IsNullOrEmpty(runner.StatusLine))
  780:                     InformationManager.DisplayMessage(new InformationMessage(runner.StatusLine));
  781:             }
  782:         }
  783:     }
  784: 
  785:     protected override void OnSubModuleUnloaded()
  786:     {
  787:         base.OnSubModuleUnloaded();
  788:         // Detach the AppDomain.UnhandledException subscription BEFORE IoC disposal so
  789:         // the hook doesn't hold a stale reference to a disposed CrashReportService
  790:         // across game-restart-in-same-process. Deep-review INC 3 (2026-05-25).
  791:         try { IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>()?.Unsubscribe(); }
  792:         catch { /* IoC may already be torn down — best-effort */ }
  793: 
  794:         // Reverse NativeSkinFixes hooks so DLL unload during reload-in-same-process
  795:         // doesn't leave dangling MinHook trampolines. Best-effort — swallows.
  796:         try { NativeSkinFixesInstaller.Uninstall(); }
  797:         catch { /* shutdown — never block */ }
  798: 
  799:         _harmony?.UnpatchAll("com.taom.mod");
  800:         IoC.Dispose();
  801: 
  802:         // Codex review #46 (2026-05-25) HIGH-01: clear the static service cache in
  803:         // the patch helper so the next module load resolves a fresh service graph from
  804:         // the new IoC container. Without this, Finalizers fire against a disposed
  805:         // FileLogger after reload and silently drop every log line.
  806:         TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
  807:     }
  808: }

codex
The first pass supports the stated root-cause shape: `OnGameInitializationFinished` now returns before every process-global patch operation and the `game` parameter is not visibly consumed in that guarded block. I’m drilling into per-game registrations, PatchShield, and the vanilla module lifecycle before calling it clean.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem -Path . -Recurse -File -Include *.cs | Where-Object { $_.FullName -notmatch '"'\\\\(bin|obj|\\.git|\\.codex_tmp|\\.codex_tmp2)\\\\' } | Select-String -Pattern 'PatchShield' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/SubModule.cs' | ForEach-Object { "'$i++; if ($i -ge 313 -and $i -le 532) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'docs/reviews' -File -Filter 'codex-adversarial-repatch-crash-fix-2026-06-18*' | Select-Object Name,Length,LastWriteTime | Format-Table -AutoSize" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem -Path . -Recurse -File -Include *.cs | Where-Object { $_.FullName -notmatch '"'\\\\(bin|obj|\\.git|\\.codex_tmp|\\.codex_tmp2)\\\\' } | Select-String -Pattern 'PatchCategory\\(' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 522ms:
  313:     protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
  314:     {
  315:         base.OnGameStart(game, gameStarterObject);
  316: 
  317:         // Session-level diagnostic snapshot: OS / CLR / mod list / mod-stack
  318:         // assembly versions / campaign context. Runs once per session and is
  319:         // idempotent so OnGameStart on save-load doesn't spam.
  320:         try
  321:         {
  322:             IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>()?.LogSessionSnapshot();
  323:         }
  324:         catch { /* diagnostic is best-effort, never break OnGameStart */ }
  325: 
  326:         if (gameStarterObject is CampaignGameStarter campaignStarter)
  327:         {
  328:             var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
  329:             campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));
  330: 
  331:             var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
  332:             var bannerExclusionService = IoC.Resolve<IBannerExclusionService>();
  333:             campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService, bannerExclusionService));
  334: 
  335:             var ccContentService = IoC.Resolve<ICharacterCreationContentService>();
  336:             var ccLogger = IoC.Resolve<IModLogger>();
  337:             campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));
  338: 
  339:             campaignStarter.RemoveBehaviors<InitialChildGenerationCampaignBehavior>();
  340:             var childGenService = IoC.Resolve<IInitialChildGenerationService>();
  341:             campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));
  342: 
  343:             var costService = IoC.Resolve<ITroopCostService>();
  344:             // Phase 9b #173 — careerPassives resolved once for the whole CulturalFeats + CareerSystem
  345:             // + TroopProgression model registration block. Replaces all CareerPassiveHelper static
  346:             // calls with instance-injected ICareerPassiveService.
  347:             var careerPassives = IoC.Resolve<TAOM.Features.CareerSystem.ICareerPassiveService>();
  348:             // Phase 9b #180 / partial #148 — IWageModifierService extraction. Hoists garrison-wage
  349:             // feat loop + Mordor/Gundabad/Umbar party-wage feats + Rohan mounted-wage scaling +
  350:             // recruitment-cost feats out of the model body, satisfying gamemodels.md rule 4.
  351:             var wageModifiers = IoC.Resolve<IWageModifierService>();
  352:             var volunteerService = IoC.Resolve<IVolunteerTierService>();
  353:             var recruitmentService = IoC.Resolve<IVolunteerRecruitmentService>();
  354:             var volunteerContextAdapter = IoC.Resolve<IVolunteerContextAdapter>();
  355:             // Hoisted: TaomVolunteerModel now consumes ICulturalFeatsService for the village
  356:             // volunteer-respawn-rate feats (Dunland/Gundabad/Dol Guldur/Mordor). The later
  357:             // "Cultural feat models" block reuses this same `culturalFeats` reference.
  358:             var culturalFeats = IoC.Resolve<TAOM.Features.CulturalFeats.ICulturalFeatsService>();
  359:             var recruitmentAlignment = IoC.Resolve<TAOM.Features.AlignmentRecruitment.IRecruitmentAlignmentService>();
  360:             campaignStarter.AddModel(new TaomCharacterStatsModel());
  361:             campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
  362:             campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));
  363: 
  364:             var raceAgeService = IoC.Resolve<IRaceAgeService>();
  365:             var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
  366:             var raceAgeLogger = IoC.Resolve<IModLogger>();
  367:             campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger));
  368:             campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
  369:             campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
  370:             campaignStarter.AddModel(new TaomHeroCreationModel());
  371: 
  372:             var diplomacyService = IoC.Resolve<IDiplomacyService>();
  373:             var wotrService = IoC.Resolve<IWarOfTheRingService>();
  374:             var diplomacyLogger = IoC.Resolve<IModLogger>();
  375:             campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger));
  376:             campaignStarter.AddBehavior(new PlayerAllianceProposalBehavior(diplomacyService, diplomacyLogger));
  377:             campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
  378:             campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
  379:             campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
  380: 
  381:             var wotrLogger = IoC.Resolve<IModLogger>();
  382:             campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
  383: 
  384:             var siegeDefenseService = IoC.Resolve<ISiegeDefenseService>();
  385:             var siegeDefenseLogger = IoC.Resolve<IModLogger>();
  386:             campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger));
  387:             campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));
  388: 
  389:             var executionRelationService = IoC.Resolve<IExecutionRelationService>();
  390:             var playerContext = IoC.Resolve<IPlayerContextAdapter>();
  391:             campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
  392: 
  393:             // Cultural feat models — Phase 9b #144/#176: dispatch logic extracted to
  394:             // ICulturalFeatsService. Each model is now a thin boundary that converts
  395:             // CultureObject → ICultureFeatAdapter and delegates (gamemodels.md rule 4).
  396:             // `culturalFeats` is resolved above (hoisted for TaomVolunteerModel).
  397:             campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
  398:             campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
  399:             campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
  400:             campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
  401:             campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
  402:             campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
  403:             campaignStarter.AddModel(new TaomCaravanModel(culturalFeats));
  404:             campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
  405:             campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
  406:             campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
  407:             campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
  408:             campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
  409:             campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
  410:             campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
  411:             campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
  412:             campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
  413:             campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
  414:             campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));
  415:             campaignStarter.AddModel(new TaomNotableSpawnModel(culturalFeats));
  416: 
  417:             // Battle balance models
  418:             var battleBalanceSettings = IoC.Resolve<IBattleBalanceSettingsProvider>();
  419:             var battleBalanceConfig = IoC.Resolve<IBattleBalanceConfigProvider>();
  420:             campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
  421:             campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
  422:             campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig));
  423: 
  424:             campaignStarter.AddModel(new TaomInformationRestrictionModel(IoC.Resolve<IEncyclopediaSettingsProvider>()));
  425: 
  426:             var armyTargetingService = IoC.Resolve<IArmyTargetingService>();
  427:             campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));
  428: 
  429:             var specialResourceService = IoC.Resolve<ISpecialResourceService>();
  430:             var specialResourceStorage = IoC.Resolve<ISpecialResourceStorageService>();
  431:             var specialResourceConfig = IoC.Resolve<ISpecialResourceConfigProvider>();
  432:             var specialResourceLogger = IoC.Resolve<IModLogger>();
  433:             var specialResourceBehavior = new SpecialResourcesBehavior(
  434:                 specialResourceService, specialResourceStorage, specialResourceConfig, specialResourceLogger);
  435:             campaignStarter.AddBehavior(specialResourceBehavior);
  436:             PartyScreenLogic_AddCommand_Patch.SetBehavior(specialResourceBehavior);
  437: 
  438:             var careerDataService = IoC.Resolve<ICareerDataService>();
  439:             var careerRegistry = IoC.Resolve<ICareerRegistry>();
  440:             var careerPassiveService = IoC.Resolve<ICareerPassiveService>();
  441:             var careerLogger = IoC.Resolve<IModLogger>();
  442:             campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));
  443:             var careerCreationHandler = IoC.Resolve<ICareerCreationHandler>();
  444:             var careerAbilityServiceForBehavior = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
  445:             campaignStarter.AddBehavior(new CareerCampaignBehavior(
  446:                 careerDataService, careerRegistry, careerPassiveService, careerCreationHandler, careerAbilityServiceForBehavior, careerLogger));
  447: 
  448:             var careerAdapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
  449:             // CareerSwitchDialogueBehavior used to take ICareerSwitchService too; that dependency
  450:             // moved to GauntletCareerScreen.OnChooseSwitchTarget (Codex Review #32 cleanup).
  451:             campaignStarter.AddBehavior(new CareerSwitchDialogueBehavior(
  452:                 careerDataService, careerRegistry, careerAdapterFactory, careerLogger));
  453: 
  454:             // Career-tied quest system (Phase 6) — offers/starts tier quests; CareerQuest : QuestBase
  455:             // is registered for saving by the auto-discovered CareerQuestSaveableTypeDefiner.
  456:             var careerQuestService = IoC.Resolve<Features.CareerSystem.ICareerQuestService>();
  457:             campaignStarter.AddBehavior(new Features.CareerSystem.Quests.CareerQuestCampaignBehavior(
  458:                 careerDataService, careerQuestService, careerLogger));
  459: 
  460:             // Career system GameModels — reuse careerPassiveService resolved above (line 334).
  461:             // Phase 9b #142 — agent-stat extraction: TaomAgentStatCalculateModel /
  462:             // TaomAgentApplyDamageModel now delegate UpdateAgentStats + damage-amp/red +
  463:             // shrug-off logic to ICareerAgentStatService (gamemodels.md rule 4).
  464:             var careerAgentStat = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>();
  465:             campaignStarter.AddModel(new TaomMapVisibilityModel(careerPassives));
  466:             campaignStarter.AddModel(new TaomInventoryCapacityModel(careerPassives));
  467:             var elephantAttackService = IoC.Resolve<Features.Elephant.IElephantAttackService>();
  468:             var spiderAttackService = IoC.Resolve<ISpiderAttackService>();
  469:             campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerPassiveService, careerAgentStat, elephantAttackService, spiderAttackService));
  470:             campaignStarter.AddModel<AgentApplyDamageModel>(new TaomAgentApplyDamageModel(careerAgentStat));
  471:             campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));
  472: 
  473:             var goldService = IoC.Resolve<IStartupGoldService>();
  474:             var influenceService = IoC.Resolve<IStartupInfluenceService>();
  475:             var startupLogger = IoC.Resolve<IModLogger>();
  476:             campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));
  477: 
  478:             var namedCompanionService = IoC.Resolve<INamedCompanionService>();
  479:             campaignStarter.AddBehavior(new NamedCompanionBehavior(namedCompanionService));
  480: 
  481:             // QuickActions: per-save inventory-search-box persistence (SyncData round-trips
  482:             // even when EnableInventorySearch is OFF — disabled = inert, not absent).
  483:             campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.QuickActions.Hooks.InventorySearchCampaignBehavior>());
  484: 
  485:             // EquipPresets: per-save preset persistence + orphan pruning. Unconditional registration
  486:             // so the SyncData round-trip preserves presets even when EnableEquipmentPresets is OFF
  487:             // (the MCM hint promises "existing presets are inert (preserved in save)").
  488:             campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.EquipPresets.Hooks.EquipmentPresetCampaignBehavior>());
  489: 
  490:             // FiefManagement (Patch36) — register UNCONDITIONALLY so the menu is always present
  491:             // and the EnableFiefManagement MCM toggle takes effect immediately at runtime.
  492:             campaignStarter.AddBehavior(new FiefHubCampaignBehavior(
  493:                 IoC.Resolve<IFiefHubMenuPresenter>(),
  494:                 IoC.Resolve<IFiefManagementSettingsProvider>()));
  495: 
  496:             // CompanionTactics (Patch35) — FormationPresets persistence behavior. Registered
  497:             // unconditionally so SyncData round-trips even when EnableFormationPresets is OFF.
  498:             campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(
  499:                 IoC.Resolve<Features.CompanionTactics.FormationPresets.IFormationPresetService>(),
  500:                 IoC.Resolve<IModLogger>()));
  501: 
  502:             // Messengers — paid messenger dispatch + dialog hooks + per-save SyncData persistence.
  503:             // Registered unconditionally so saves round-trip pending messengers even when
  504:             // EnableMessengers is OFF (disabled = inert, not absent).
  505:             campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>());
  506: 
  507:             // CultureMarketplace (#207) — daily injection of LOTRLOME items into town markets
  508:             // keyed by owner culture. No SyncData (stock lives in vanilla Settlement.ItemRoster).
  509:             campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
  510:                 IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
  511:                 IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
  512:                 IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
  513:                 IoC.Resolve<ITownRosterAdapter>(),
  514:                 IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
  515:                 IoC.Resolve<IModLogger>()));
  516: 
  517:             // CastleRecruitment (Patch42) — castle notable population + maintenance + volunteer fill +
  518:             // player "Recruit troops" castle menu + issue/quest suppression for castle notables.
  519:             // Registered unconditionally so the MCM master toggle takes effect at runtime.
  520:             campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
  521:                 IoC.Resolve<ICastleRecruitmentService>(),
  522:                 IoC.Resolve<IModLogger>()));
  523: 
  524:             // CultureConversion — conquered cross-culture fiefs gradually adopt the new owner's culture
  525:             // (troops, militia, identity). Registered unconditionally so SyncData round-trips conversion
  526:             // records and completed overrides re-apply on load even when the MCM toggle is off.
  527:             campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
  528:                 IoC.Resolve<Features.CultureConversion.ICultureConversionService>(),
  529:                 IoC.Resolve<Features.CultureConversion.ICultureConversionStore>(),
  530:                 IoC.Resolve<IModLogger>()));
  531:         }
  532:     }

 succeeded in 486ms:

[32;1mName                                                    [0m[32;1m Length[0m[32;1m LastWriteTime[0m
[32;1m----                                                    [0m [32;1m------[0m [32;1m-------------[0m
codex-adversarial-repatch-crash-fix-2026-06-18.md             0 6/18/2026 10:02:01 AM
codex-adversarial-repatch-crash-fix-2026-06-18.prompt.md   5341 6/18/2026 10:01:55 AM


2026-06-18T15:03:13.738971Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 24 seconds
Output:
.claude\worktrees\impl-001\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-001\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-001\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-001\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-001\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-001\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-001\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
.claude\worktrees\impl-002\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-002\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-002\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-002\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-002\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-002\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-002\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
.claude\worktrees\impl-003\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-003\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-003\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-003\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-003\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-003\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-003\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
.claude\worktrees\impl-005\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-005\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-005\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-005\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-005\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-005\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-005\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fft9w7e' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fyfj2_j' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp29jamgyn' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp2owwhlqy' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp5516wbdv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp78qze76y' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp8gds9xfp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpbwbm4o8d' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpc85yzrbw' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpi3maldm8' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpkr2gpims' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpktgkfgay' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplcnrd81l' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplorpih3w' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpm39f2s15' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpov4yy7nx' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpsanxk9fp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpwy0s04pv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpyqy6lg5o' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp2\tmpulsj5ei5' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

 exited 1 in 24042ms:
.claude\worktrees\impl-001\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-001\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-001\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-001\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-001\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-001\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-001\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-001\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-001\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
.claude\worktrees\impl-002\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-002\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-002\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-002\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-002\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-002\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-002\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-002\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-002\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
.claude\worktrees\impl-003\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-003\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-003\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-003\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-003\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-003\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-003\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-003\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-003\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
.claude\worktrees\impl-005\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-005\Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
.claude\worktrees\impl-005\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
.claude\worktrees\impl-005\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
.claude\worktrees\impl-005\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
.claude\worktrees\impl-005\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
.claude\worktrees\impl-005\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
.claude\worktrees\impl-005\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
.claude\worktrees\impl-005\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
Dependencies\Foundation\PatchShield.cs:115: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
Dependencies\Foundation\PatchShield.cs:122: var voidFinalizer = typeof(PatchShield).GetMethod(
Dependencies\Foundation\PatchShield.cs:125: var resultFinalizer = typeof(PatchShield).GetMethod(
Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fft9w7e' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fyfj2_j' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp29jamgyn' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp2owwhlqy' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp5516wbdv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp78qze76y' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp8gds9xfp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpbwbm4o8d' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpc85yzrbw' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpi3maldm8' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpkr2gpims' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpktgkfgay' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplcnrd81l' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplorpih3w' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpm39f2s15' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpov4yy7nx' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpsanxk9fp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpwy0s04pv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpyqy6lg5o' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp2\tmpulsj5ei5' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

 exited 1 in 24000ms:
.claude\worktrees\impl-001\Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
.claude\worktrees\impl-001\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
.claude\worktrees\impl-001\Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
.claude\worktrees\impl-001\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
.claude\worktrees\impl-001\Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-001\Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-001\Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-001\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
.claude\worktrees\impl-001\Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-001\Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-001\Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
.claude\worktrees\impl-001\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
.claude\worktrees\impl-001\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
.claude\worktrees\impl-001\Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
.claude\worktrees\impl-001\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-001\Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-001\Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-001\Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-001\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
.claude\worktrees\impl-001\Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
.claude\worktrees\impl-001\Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
.claude\worktrees\impl-001\Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
.claude\worktrees\impl-001\Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:30: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
.claude\worktrees\impl-001\Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
.claude\worktrees\impl-001\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-001\Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-001\Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-001\Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-001\Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-001\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-001\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-001\Main\SubModule.cs:108: _harmony.PatchCategory("Patch37_CrashReport");
.claude\worktrees\impl-001\Main\SubModule.cs:132: _harmony.PatchCategory("Patch41_McmLayoutFix");
.claude\worktrees\impl-001\Main\SubModule.cs:138: _harmony.PatchCategory("Patch25_LocalizationOverride");
.claude\worktrees\impl-001\Main\SubModule.cs:154: _harmony.PatchCategory("Patch18_CulturalFeats");
.claude\worktrees\impl-001\Main\SubModule.cs:155: _harmony.PatchCategory("Patch19_CustomBattles");
.claude\worktrees\impl-001\Main\SubModule.cs:161: _harmony.PatchCategory("Patch0_BattleScenes");
.claude\worktrees\impl-001\Main\SubModule.cs:193: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
.claude\worktrees\impl-001\Main\SubModule.cs:196: _harmony.PatchCategory("Patch22_ArmyTargeting");
.claude\worktrees\impl-001\Main\SubModule.cs:197: _harmony.PatchCategory("Patch30_MixedFormations");
.claude\worktrees\impl-001\Main\SubModule.cs:241: _harmony.PatchCategory("Patch42_CastleRecruitment");
.claude\worktrees\impl-001\Main\SubModule.cs:516: _harmony.PatchCategory("Patch1_FirstTimeInit");
.claude\worktrees\impl-001\Main\SubModule.cs:517: _harmony.PatchCategory("Patch2_RefreshTableau");
.claude\worktrees\impl-001\Main\SubModule.cs:518: _harmony.PatchCategory("Patch3_SetRace");
.claude\worktrees\impl-001\Main\SubModule.cs:519: _harmony.PatchCategory("Patch4_CharacterSpawner");
.claude\worktrees\impl-001\Main\SubModule.cs:520: _harmony.PatchCategory("Patch5_FaceGen");
.claude\worktrees\impl-001\Main\SubModule.cs:521: _harmony.PatchCategory("Late_Transpiler");
.claude\worktrees\impl-001\Main\SubModule.cs:522: _harmony.PatchCategory("Late_ActionSetOverride");
.claude\worktrees\impl-001\Main\SubModule.cs:523: _harmony.PatchCategory("Patch6_BannerEditor");
.claude\worktrees\impl-001\Main\SubModule.cs:524: _harmony.PatchCategory("Patch7_FactionMap");
.claude\worktrees\impl-001\Main\SubModule.cs:525: _harmony.PatchCategory("Patch9_RaceFilter");
.claude\worktrees\impl-001\Main\SubModule.cs:526: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
.claude\worktrees\impl-001\Main\SubModule.cs:527: _harmony.PatchCategory("Patch8_SiegeCampGuard");
.claude\worktrees\impl-001\Main\SubModule.cs:528: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
.claude\worktrees\impl-001\Main\SubModule.cs:529: _harmony.PatchCategory("Patch11_Diplomacy");
.claude\worktrees\impl-001\Main\SubModule.cs:530: _harmony.PatchCategory("Patch12_WarOfTheRing");
.claude\worktrees\impl-001\Main\SubModule.cs:532: _harmony.PatchCategory("Patch14_Execution");
.claude\worktrees\impl-001\Main\SubModule.cs:533: _harmony.PatchCategory("Patch15_BannerLayerLimit");
.claude\worktrees\impl-001\Main\SubModule.cs:534: _harmony.PatchCategory("Patch16_AtmospherePersistence");
.claude\worktrees\impl-001\Main\SubModule.cs:535: _harmony.PatchCategory("Patch17_TroopWeight");
.claude\worktrees\impl-001\Main\SubModule.cs:536: _harmony.PatchCategory("Patch23_BannerColorPersistence");
.claude\worktrees\impl-001\Main\SubModule.cs:537: _harmony.PatchCategory("Patch24_BannerDriftGuard");
.claude\worktrees\impl-001\Main\SubModule.cs:538: _harmony.PatchCategory("Patch39_BanditPartySize");
.claude\worktrees\impl-001\Main\SubModule.cs:539: _harmony.PatchCategory("Patch40_HideoutDescription");
.claude\worktrees\impl-001\Main\SubModule.cs:540: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
.claude\worktrees\impl-001\Main\SubModule.cs:549: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
.claude\worktrees\impl-001\Main\SubModule.cs:556: _harmony.PatchCategory("Patch26_SpecialResources");
.claude\worktrees\impl-001\Main\SubModule.cs:557: _harmony.PatchCategory("Patch27_CareerSystem");
.claude\worktrees\impl-001\Main\SubModule.cs:558: _harmony.PatchCategory("Patch29_CCBodyProperties");
.claude\worktrees\impl-001\Main\SubModule.cs:559: _harmony.PatchCategory("Patch44_CCNameAutofill");
.claude\worktrees\impl-001\Main\SubModule.cs:560: _harmony.PatchCategory("Patch33_EquipPresets");
.claude\worktrees\impl-001\Main\SubModule.cs:561: _harmony.PatchCategory("Patch34_QuickActions");
.claude\worktrees\impl-001\Main\SubModule.cs:562: _harmony.PatchCategory("Patch35_CompanionTactics");
.claude\worktrees\impl-001\Main\SubModule.cs:563: _harmony.PatchCategory("Patch36_FiefManagement");
.claude\worktrees\impl-001\Main\SubModule.cs:565: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
.claude\worktrees\impl-001\Main\SubModule.cs:578: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
.claude\worktrees\impl-001\Main\SubModule.cs:660: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
.claude\worktrees\impl-001\out\0Harmony.decompiled.cs:5733: public HarmonyPatchCategory(string category)
.claude\worktrees\impl-001\out\0Harmony.decompiled.cs:6522: public void PatchCategory(string category)
.claude\worktrees\impl-001\out\0Harmony.decompiled.cs:6526: PatchCategory(assembly, category);
.claude\worktrees\impl-001\out\0Harmony.decompiled.cs:6529: public void PatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-001\out\0Harmony.decompiled.cs:6642: public void UnpatchCategory(string category)
.claude\worktrees\impl-001\out\0Harmony.decompiled.cs:6646: UnpatchCategory(assembly, category);
.claude\worktrees\impl-001\out\0Harmony.decompiled.cs:6649: public void UnpatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-001\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:31: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
.claude\worktrees\impl-001\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:44: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
.claude\worktrees\impl-002\Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
.claude\worktrees\impl-002\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
.claude\worktrees\impl-002\Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
.claude\worktrees\impl-002\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
.claude\worktrees\impl-002\Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-002\Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
.claude\worktrees\impl-002\Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-002\Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-002\Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-002\Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-002\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-002\Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-002\Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-002\Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-002\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
.claude\worktrees\impl-002\Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-002\Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-002\Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
.claude\worktrees\impl-002\Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
.claude\worktrees\impl-002\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
.claude\worktrees\impl-002\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
.claude\worktrees\impl-002\Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-002\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-002\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-002\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-002\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-002\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-002\Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-002\Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-002\Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-002\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-002\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-002\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-002\Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-002\Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
.claude\worktrees\impl-002\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-002\Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-002\Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-002\Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-002\Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-002\Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-002\Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-002\Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-002\Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-002\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-002\Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
.claude\worktrees\impl-002\Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
.claude\worktrees\impl-002\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
.claude\worktrees\impl-002\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
.claude\worktrees\impl-002\Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
.claude\worktrees\impl-002\Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
.claude\worktrees\impl-002\Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
.claude\worktrees\impl-002\Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
.claude\worktrees\impl-002\Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
.claude\worktrees\impl-002\Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-002\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-002\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-002\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-002\Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
.claude\worktrees\impl-002\Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:30: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
.claude\worktrees\impl-002\Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
.claude\worktrees\impl-002\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-002\Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-002\Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-002\Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-002\Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-002\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-002\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-002\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-002\Main\SubModule.cs:108: _harmony.PatchCategory("Patch37_CrashReport");
.claude\worktrees\impl-002\Main\SubModule.cs:132: _harmony.PatchCategory("Patch41_McmLayoutFix");
.claude\worktrees\impl-002\Main\SubModule.cs:138: _harmony.PatchCategory("Patch25_LocalizationOverride");
.claude\worktrees\impl-002\Main\SubModule.cs:154: _harmony.PatchCategory("Patch18_CulturalFeats");
.claude\worktrees\impl-002\Main\SubModule.cs:155: _harmony.PatchCategory("Patch19_CustomBattles");
.claude\worktrees\impl-002\Main\SubModule.cs:161: _harmony.PatchCategory("Patch0_BattleScenes");
.claude\worktrees\impl-002\Main\SubModule.cs:193: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
.claude\worktrees\impl-002\Main\SubModule.cs:196: _harmony.PatchCategory("Patch22_ArmyTargeting");
.claude\worktrees\impl-002\Main\SubModule.cs:197: _harmony.PatchCategory("Patch30_MixedFormations");
.claude\worktrees\impl-002\Main\SubModule.cs:241: _harmony.PatchCategory("Patch42_CastleRecruitment");
.claude\worktrees\impl-002\Main\SubModule.cs:516: _harmony.PatchCategory("Patch1_FirstTimeInit");
.claude\worktrees\impl-002\Main\SubModule.cs:517: _harmony.PatchCategory("Patch2_RefreshTableau");
.claude\worktrees\impl-002\Main\SubModule.cs:518: _harmony.PatchCategory("Patch3_SetRace");
.claude\worktrees\impl-002\Main\SubModule.cs:519: _harmony.PatchCategory("Patch4_CharacterSpawner");
.claude\worktrees\impl-002\Main\SubModule.cs:520: _harmony.PatchCategory("Patch5_FaceGen");
.claude\worktrees\impl-002\Main\SubModule.cs:521: _harmony.PatchCategory("Late_Transpiler");
.claude\worktrees\impl-002\Main\SubModule.cs:522: _harmony.PatchCategory("Late_ActionSetOverride");
.claude\worktrees\impl-002\Main\SubModule.cs:523: _harmony.PatchCategory("Patch6_BannerEditor");
.claude\worktrees\impl-002\Main\SubModule.cs:524: _harmony.PatchCategory("Patch7_FactionMap");
.claude\worktrees\impl-002\Main\SubModule.cs:525: _harmony.PatchCategory("Patch9_RaceFilter");
.claude\worktrees\impl-002\Main\SubModule.cs:526: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
.claude\worktrees\impl-002\Main\SubModule.cs:527: _harmony.PatchCategory("Patch8_SiegeCampGuard");
.claude\worktrees\impl-002\Main\SubModule.cs:528: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
.claude\worktrees\impl-002\Main\SubModule.cs:529: _harmony.PatchCategory("Patch11_Diplomacy");
.claude\worktrees\impl-002\Main\SubModule.cs:530: _harmony.PatchCategory("Patch12_WarOfTheRing");
.claude\worktrees\impl-002\Main\SubModule.cs:532: _harmony.PatchCategory("Patch14_Execution");
.claude\worktrees\impl-002\Main\SubModule.cs:533: _harmony.PatchCategory("Patch15_BannerLayerLimit");
.claude\worktrees\impl-002\Main\SubModule.cs:534: _harmony.PatchCategory("Patch16_AtmospherePersistence");
.claude\worktrees\impl-002\Main\SubModule.cs:535: _harmony.PatchCategory("Patch17_TroopWeight");
.claude\worktrees\impl-002\Main\SubModule.cs:536: _harmony.PatchCategory("Patch23_BannerColorPersistence");
.claude\worktrees\impl-002\Main\SubModule.cs:537: _harmony.PatchCategory("Patch24_BannerDriftGuard");
.claude\worktrees\impl-002\Main\SubModule.cs:538: _harmony.PatchCategory("Patch39_BanditPartySize");
.claude\worktrees\impl-002\Main\SubModule.cs:539: _harmony.PatchCategory("Patch40_HideoutDescription");
.claude\worktrees\impl-002\Main\SubModule.cs:540: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
.claude\worktrees\impl-002\Main\SubModule.cs:549: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
.claude\worktrees\impl-002\Main\SubModule.cs:556: _harmony.PatchCategory("Patch26_SpecialResources");
.claude\worktrees\impl-002\Main\SubModule.cs:557: _harmony.PatchCategory("Patch27_CareerSystem");
.claude\worktrees\impl-002\Main\SubModule.cs:558: _harmony.PatchCategory("Patch29_CCBodyProperties");
.claude\worktrees\impl-002\Main\SubModule.cs:559: _harmony.PatchCategory("Patch44_CCNameAutofill");
.claude\worktrees\impl-002\Main\SubModule.cs:560: _harmony.PatchCategory("Patch33_EquipPresets");
.claude\worktrees\impl-002\Main\SubModule.cs:561: _harmony.PatchCategory("Patch34_QuickActions");
.claude\worktrees\impl-002\Main\SubModule.cs:562: _harmony.PatchCategory("Patch35_CompanionTactics");
.claude\worktrees\impl-002\Main\SubModule.cs:563: _harmony.PatchCategory("Patch36_FiefManagement");
.claude\worktrees\impl-002\Main\SubModule.cs:565: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
.claude\worktrees\impl-002\Main\SubModule.cs:578: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
.claude\worktrees\impl-002\Main\SubModule.cs:660: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
.claude\worktrees\impl-002\out\0Harmony.decompiled.cs:5733: public HarmonyPatchCategory(string category)
.claude\worktrees\impl-002\out\0Harmony.decompiled.cs:6522: public void PatchCategory(string category)
.claude\worktrees\impl-002\out\0Harmony.decompiled.cs:6526: PatchCategory(assembly, category);
.claude\worktrees\impl-002\out\0Harmony.decompiled.cs:6529: public void PatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-002\out\0Harmony.decompiled.cs:6642: public void UnpatchCategory(string category)
.claude\worktrees\impl-002\out\0Harmony.decompiled.cs:6646: UnpatchCategory(assembly, category);
.claude\worktrees\impl-002\out\0Harmony.decompiled.cs:6649: public void UnpatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-002\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:31: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
.claude\worktrees\impl-002\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:44: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
.claude\worktrees\impl-003\Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
.claude\worktrees\impl-003\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
.claude\worktrees\impl-003\Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
.claude\worktrees\impl-003\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
.claude\worktrees\impl-003\Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-003\Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
.claude\worktrees\impl-003\Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-003\Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-003\Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-003\Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-003\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-003\Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-003\Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-003\Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-003\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
.claude\worktrees\impl-003\Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-003\Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-003\Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
.claude\worktrees\impl-003\Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
.claude\worktrees\impl-003\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
.claude\worktrees\impl-003\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
.claude\worktrees\impl-003\Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-003\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-003\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-003\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-003\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-003\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-003\Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-003\Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-003\Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-003\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-003\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-003\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-003\Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-003\Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
.claude\worktrees\impl-003\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-003\Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-003\Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-003\Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-003\Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-003\Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-003\Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-003\Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-003\Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-003\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-003\Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
.claude\worktrees\impl-003\Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
.claude\worktrees\impl-003\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
.claude\worktrees\impl-003\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
.claude\worktrees\impl-003\Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
.claude\worktrees\impl-003\Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
.claude\worktrees\impl-003\Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
.claude\worktrees\impl-003\Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
.claude\worktrees\impl-003\Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
.claude\worktrees\impl-003\Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-003\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-003\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-003\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-003\Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
.claude\worktrees\impl-003\Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:30: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
.claude\worktrees\impl-003\Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
.claude\worktrees\impl-003\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-003\Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-003\Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-003\Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-003\Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-003\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-003\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-003\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-003\Main\SubModule.cs:108: _harmony.PatchCategory("Patch37_CrashReport");
.claude\worktrees\impl-003\Main\SubModule.cs:132: _harmony.PatchCategory("Patch41_McmLayoutFix");
.claude\worktrees\impl-003\Main\SubModule.cs:138: _harmony.PatchCategory("Patch25_LocalizationOverride");
.claude\worktrees\impl-003\Main\SubModule.cs:154: _harmony.PatchCategory("Patch18_CulturalFeats");
.claude\worktrees\impl-003\Main\SubModule.cs:155: _harmony.PatchCategory("Patch19_CustomBattles");
.claude\worktrees\impl-003\Main\SubModule.cs:161: _harmony.PatchCategory("Patch0_BattleScenes");
.claude\worktrees\impl-003\Main\SubModule.cs:193: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
.claude\worktrees\impl-003\Main\SubModule.cs:196: _harmony.PatchCategory("Patch22_ArmyTargeting");
.claude\worktrees\impl-003\Main\SubModule.cs:197: _harmony.PatchCategory("Patch30_MixedFormations");
.claude\worktrees\impl-003\Main\SubModule.cs:241: _harmony.PatchCategory("Patch42_CastleRecruitment");
.claude\worktrees\impl-003\Main\SubModule.cs:516: _harmony.PatchCategory("Patch1_FirstTimeInit");
.claude\worktrees\impl-003\Main\SubModule.cs:517: _harmony.PatchCategory("Patch2_RefreshTableau");
.claude\worktrees\impl-003\Main\SubModule.cs:518: _harmony.PatchCategory("Patch3_SetRace");
.claude\worktrees\impl-003\Main\SubModule.cs:519: _harmony.PatchCategory("Patch4_CharacterSpawner");
.claude\worktrees\impl-003\Main\SubModule.cs:520: _harmony.PatchCategory("Patch5_FaceGen");
.claude\worktrees\impl-003\Main\SubModule.cs:521: _harmony.PatchCategory("Late_Transpiler");
.claude\worktrees\impl-003\Main\SubModule.cs:522: _harmony.PatchCategory("Late_ActionSetOverride");
.claude\worktrees\impl-003\Main\SubModule.cs:523: _harmony.PatchCategory("Patch6_BannerEditor");
.claude\worktrees\impl-003\Main\SubModule.cs:524: _harmony.PatchCategory("Patch7_FactionMap");
.claude\worktrees\impl-003\Main\SubModule.cs:525: _harmony.PatchCategory("Patch9_RaceFilter");
.claude\worktrees\impl-003\Main\SubModule.cs:526: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
.claude\worktrees\impl-003\Main\SubModule.cs:527: _harmony.PatchCategory("Patch8_SiegeCampGuard");
.claude\worktrees\impl-003\Main\SubModule.cs:528: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
.claude\worktrees\impl-003\Main\SubModule.cs:529: _harmony.PatchCategory("Patch11_Diplomacy");
.claude\worktrees\impl-003\Main\SubModule.cs:530: _harmony.PatchCategory("Patch12_WarOfTheRing");
.claude\worktrees\impl-003\Main\SubModule.cs:532: _harmony.PatchCategory("Patch14_Execution");
.claude\worktrees\impl-003\Main\SubModule.cs:533: _harmony.PatchCategory("Patch15_BannerLayerLimit");
.claude\worktrees\impl-003\Main\SubModule.cs:534: _harmony.PatchCategory("Patch16_AtmospherePersistence");
.claude\worktrees\impl-003\Main\SubModule.cs:535: _harmony.PatchCategory("Patch17_TroopWeight");
.claude\worktrees\impl-003\Main\SubModule.cs:536: _harmony.PatchCategory("Patch23_BannerColorPersistence");
.claude\worktrees\impl-003\Main\SubModule.cs:537: _harmony.PatchCategory("Patch24_BannerDriftGuard");
.claude\worktrees\impl-003\Main\SubModule.cs:538: _harmony.PatchCategory("Patch39_BanditPartySize");
.claude\worktrees\impl-003\Main\SubModule.cs:539: _harmony.PatchCategory("Patch40_HideoutDescription");
.claude\worktrees\impl-003\Main\SubModule.cs:540: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
.claude\worktrees\impl-003\Main\SubModule.cs:549: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
.claude\worktrees\impl-003\Main\SubModule.cs:556: _harmony.PatchCategory("Patch26_SpecialResources");
.claude\worktrees\impl-003\Main\SubModule.cs:557: _harmony.PatchCategory("Patch27_CareerSystem");
.claude\worktrees\impl-003\Main\SubModule.cs:558: _harmony.PatchCategory("Patch29_CCBodyProperties");
.claude\worktrees\impl-003\Main\SubModule.cs:559: _harmony.PatchCategory("Patch44_CCNameAutofill");
.claude\worktrees\impl-003\Main\SubModule.cs:560: _harmony.PatchCategory("Patch33_EquipPresets");
.claude\worktrees\impl-003\Main\SubModule.cs:561: _harmony.PatchCategory("Patch34_QuickActions");
.claude\worktrees\impl-003\Main\SubModule.cs:562: _harmony.PatchCategory("Patch35_CompanionTactics");
.claude\worktrees\impl-003\Main\SubModule.cs:563: _harmony.PatchCategory("Patch36_FiefManagement");
.claude\worktrees\impl-003\Main\SubModule.cs:565: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
.claude\worktrees\impl-003\Main\SubModule.cs:578: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
.claude\worktrees\impl-003\Main\SubModule.cs:660: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
.claude\worktrees\impl-003\out\0Harmony.decompiled.cs:5733: public HarmonyPatchCategory(string category)
.claude\worktrees\impl-003\out\0Harmony.decompiled.cs:6522: public void PatchCategory(string category)
.claude\worktrees\impl-003\out\0Harmony.decompiled.cs:6526: PatchCategory(assembly, category);
.claude\worktrees\impl-003\out\0Harmony.decompiled.cs:6529: public void PatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-003\out\0Harmony.decompiled.cs:6642: public void UnpatchCategory(string category)
.claude\worktrees\impl-003\out\0Harmony.decompiled.cs:6646: UnpatchCategory(assembly, category);
.claude\worktrees\impl-003\out\0Harmony.decompiled.cs:6649: public void UnpatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-003\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:31: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
.claude\worktrees\impl-003\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:44: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
.claude\worktrees\impl-005\Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
.claude\worktrees\impl-005\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
.claude\worktrees\impl-005\Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
.claude\worktrees\impl-005\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
.claude\worktrees\impl-005\Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-005\Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
.claude\worktrees\impl-005\Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-005\Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-005\Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-005\Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-005\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-005\Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-005\Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-005\Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-005\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
.claude\worktrees\impl-005\Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-005\Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-005\Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
.claude\worktrees\impl-005\Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
.claude\worktrees\impl-005\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
.claude\worktrees\impl-005\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
.claude\worktrees\impl-005\Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-005\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-005\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-005\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-005\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-005\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-005\Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-005\Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-005\Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-005\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-005\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-005\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-005\Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-005\Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
.claude\worktrees\impl-005\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-005\Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-005\Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-005\Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-005\Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-005\Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-005\Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-005\Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-005\Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-005\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-005\Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
.claude\worktrees\impl-005\Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
.claude\worktrees\impl-005\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
.claude\worktrees\impl-005\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
.claude\worktrees\impl-005\Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
.claude\worktrees\impl-005\Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
.claude\worktrees\impl-005\Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
.claude\worktrees\impl-005\Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
.claude\worktrees\impl-005\Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
.claude\worktrees\impl-005\Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-005\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-005\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-005\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-005\Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
.claude\worktrees\impl-005\Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:30: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
.claude\worktrees\impl-005\Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
.claude\worktrees\impl-005\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-005\Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-005\Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-005\Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-005\Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-005\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-005\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-005\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-005\Main\SubModule.cs:108: _harmony.PatchCategory("Patch37_CrashReport");
.claude\worktrees\impl-005\Main\SubModule.cs:132: _harmony.PatchCategory("Patch41_McmLayoutFix");
.claude\worktrees\impl-005\Main\SubModule.cs:138: _harmony.PatchCategory("Patch25_LocalizationOverride");
.claude\worktrees\impl-005\Main\SubModule.cs:154: _harmony.PatchCategory("Patch18_CulturalFeats");
.claude\worktrees\impl-005\Main\SubModule.cs:155: _harmony.PatchCategory("Patch19_CustomBattles");
.claude\worktrees\impl-005\Main\SubModule.cs:161: _harmony.PatchCategory("Patch0_BattleScenes");
.claude\worktrees\impl-005\Main\SubModule.cs:193: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
.claude\worktrees\impl-005\Main\SubModule.cs:196: _harmony.PatchCategory("Patch22_ArmyTargeting");
.claude\worktrees\impl-005\Main\SubModule.cs:197: _harmony.PatchCategory("Patch30_MixedFormations");
.claude\worktrees\impl-005\Main\SubModule.cs:241: _harmony.PatchCategory("Patch42_CastleRecruitment");
.claude\worktrees\impl-005\Main\SubModule.cs:516: _harmony.PatchCategory("Patch1_FirstTimeInit");
.claude\worktrees\impl-005\Main\SubModule.cs:517: _harmony.PatchCategory("Patch2_RefreshTableau");
.claude\worktrees\impl-005\Main\SubModule.cs:518: _harmony.PatchCategory("Patch3_SetRace");
.claude\worktrees\impl-005\Main\SubModule.cs:519: _harmony.PatchCategory("Patch4_CharacterSpawner");
.claude\worktrees\impl-005\Main\SubModule.cs:520: _harmony.PatchCategory("Patch5_FaceGen");
.claude\worktrees\impl-005\Main\SubModule.cs:521: _harmony.PatchCategory("Late_Transpiler");
.claude\worktrees\impl-005\Main\SubModule.cs:522: _harmony.PatchCategory("Late_ActionSetOverride");
.claude\worktrees\impl-005\Main\SubModule.cs:523: _harmony.PatchCategory("Patch6_BannerEditor");
.claude\worktrees\impl-005\Main\SubModule.cs:524: _harmony.PatchCategory("Patch7_FactionMap");
.claude\worktrees\impl-005\Main\SubModule.cs:525: _harmony.PatchCategory("Patch9_RaceFilter");
.claude\worktrees\impl-005\Main\SubModule.cs:526: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
.claude\worktrees\impl-005\Main\SubModule.cs:527: _harmony.PatchCategory("Patch8_SiegeCampGuard");
.claude\worktrees\impl-005\Main\SubModule.cs:528: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
.claude\worktrees\impl-005\Main\SubModule.cs:529: _harmony.PatchCategory("Patch11_Diplomacy");
.claude\worktrees\impl-005\Main\SubModule.cs:530: _harmony.PatchCategory("Patch12_WarOfTheRing");
.claude\worktrees\impl-005\Main\SubModule.cs:532: _harmony.PatchCategory("Patch14_Execution");
.claude\worktrees\impl-005\Main\SubModule.cs:533: _harmony.PatchCategory("Patch15_BannerLayerLimit");
.claude\worktrees\impl-005\Main\SubModule.cs:534: _harmony.PatchCategory("Patch16_AtmospherePersistence");
.claude\worktrees\impl-005\Main\SubModule.cs:535: _harmony.PatchCategory("Patch17_TroopWeight");
.claude\worktrees\impl-005\Main\SubModule.cs:536: _harmony.PatchCategory("Patch23_BannerColorPersistence");
.claude\worktrees\impl-005\Main\SubModule.cs:537: _harmony.PatchCategory("Patch24_BannerDriftGuard");
.claude\worktrees\impl-005\Main\SubModule.cs:538: _harmony.PatchCategory("Patch39_BanditPartySize");
.claude\worktrees\impl-005\Main\SubModule.cs:539: _harmony.PatchCategory("Patch40_HideoutDescription");
.claude\worktrees\impl-005\Main\SubModule.cs:540: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
.claude\worktrees\impl-005\Main\SubModule.cs:549: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
.claude\worktrees\impl-005\Main\SubModule.cs:556: _harmony.PatchCategory("Patch26_SpecialResources");
.claude\worktrees\impl-005\Main\SubModule.cs:557: _harmony.PatchCategory("Patch27_CareerSystem");
.claude\worktrees\impl-005\Main\SubModule.cs:558: _harmony.PatchCategory("Patch29_CCBodyProperties");
.claude\worktrees\impl-005\Main\SubModule.cs:559: _harmony.PatchCategory("Patch44_CCNameAutofill");
.claude\worktrees\impl-005\Main\SubModule.cs:560: _harmony.PatchCategory("Patch33_EquipPresets");
.claude\worktrees\impl-005\Main\SubModule.cs:561: _harmony.PatchCategory("Patch34_QuickActions");
.claude\worktrees\impl-005\Main\SubModule.cs:562: _harmony.PatchCategory("Patch35_CompanionTactics");
.claude\worktrees\impl-005\Main\SubModule.cs:563: _harmony.PatchCategory("Patch36_FiefManagement");
.claude\worktrees\impl-005\Main\SubModule.cs:565: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
.claude\worktrees\impl-005\Main\SubModule.cs:578: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
.claude\worktrees\impl-005\Main\SubModule.cs:660: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
.claude\worktrees\impl-005\out\0Harmony.decompiled.cs:5733: public HarmonyPatchCategory(string category)
.claude\worktrees\impl-005\out\0Harmony.decompiled.cs:6522: public void PatchCategory(string category)
.claude\worktrees\impl-005\out\0Harmony.decompiled.cs:6526: PatchCategory(assembly, category);
.claude\worktrees\impl-005\out\0Harmony.decompiled.cs:6529: public void PatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-005\out\0Harmony.decompiled.cs:6642: public void UnpatchCategory(string category)
.claude\worktrees\impl-005\out\0Harmony.decompiled.cs:6646: UnpatchCategory(assembly, category);
.claude\worktrees\impl-005\out\0Harmony.decompiled.cs:6649: public void UnpatchCategory(Assembly assembly, string category)
.claude\worktrees\impl-005\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:31: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
.claude\worktrees\impl-005\TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:44: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Attributes.cs:185: public HarmonyPatchCategory(string category) => info.category = category;
Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:129: public void PatchCategory(string category)
Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:133: PatchCategory(assembly, category);
Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:142: public void PatchCategory(Assembly assembly, string category)
Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:250: public void UnpatchCategory(string category)
Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:254: UnpatchCategory(assembly, category);
Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:261: public void UnpatchCategory(Assembly assembly, string category)
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\CategoryPatches.cs:14: harmony.PatchCategory("CategoryA");
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\CategoryPatches.cs:22: harmony.PatchCategory("CategoryB");
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\CategoryPatches.cs:29: harmony.UnpatchCategory("CategoryA");
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\CategoryPatches.cs:36: harmony.UnpatchCategory("CategoryB");
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\CategoryPatches.cs:57: [HarmonyPatchCategory("CategoryA")]
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\CategoryPatches.cs:74: [HarmonyPatchCategory("CategoryB")]
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\FinalizerPatches.cs:20: harmony.PatchCategory("finalizer-test");
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\FinalizerPatches.cs:35: [HarmonyPatchCategory("finalizer-test")]
Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\FinalizerPatches.cs:47: [HarmonyPatchCategory("finalizer-test")]
Main\Features\AdvancedCombat\Hooks\Agent_CheckToDropFlaggedItem_Guard_Patch.cs:26: [HarmonyPatchCategory("Patch50_DropFlaggedItemGuard")]
Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
Main\Features\ArmyTargeting\Hooks\Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs:29: [HarmonyPatchCategory("Patch49_ArmyGatheringNreGuard")]
Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CulturalFeats\Hooks\PartyBaseHelper_HasFeat_Patch.cs:31: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_StartAlliance_Patch.cs:17: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:22: [HarmonyPatchCategory("Patch13_RaceAge")]
Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:17: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
Main\Features\Spider\Hooks\Agent_HandleBlowAux_SpiderDismountGuard_Patch.cs:31: [HarmonyPatchCategory("Patch48_SpiderHitDismountGuard")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\ClanPartyItemVM_UpdateProperties_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyUpgraderUpgradeReadyTroops_Patch.cs:14: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\SubModule.cs:109: _harmony.PatchCategory("Patch37_CrashReport");
Main\SubModule.cs:133: _harmony.PatchCategory("Patch41_McmLayoutFix");
Main\SubModule.cs:139: _harmony.PatchCategory("Patch25_LocalizationOverride");
Main\SubModule.cs:155: _harmony.PatchCategory("Patch18_CulturalFeats");
Main\SubModule.cs:156: _harmony.PatchCategory("Patch19_CustomBattles");
Main\SubModule.cs:162: _harmony.PatchCategory("Patch0_BattleScenes");
Main\SubModule.cs:196: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
Main\SubModule.cs:200: _harmony.PatchCategory("Patch22_ArmyTargeting");
Main\SubModule.cs:206: _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
Main\SubModule.cs:207: _harmony.PatchCategory("Patch30_MixedFormations");
Main\SubModule.cs:251: _harmony.PatchCategory("Patch42_CastleRecruitment");
Main\SubModule.cs:548: _harmony.PatchCategory("Patch1_FirstTimeInit");
Main\SubModule.cs:549: _harmony.PatchCategory("Patch2_RefreshTableau");
Main\SubModule.cs:550: _harmony.PatchCategory("Patch3_SetRace");
Main\SubModule.cs:551: _harmony.PatchCategory("Patch4_CharacterSpawner");
Main\SubModule.cs:552: _harmony.PatchCategory("Patch5_FaceGen");
Main\SubModule.cs:553: _harmony.PatchCategory("Late_Transpiler");
Main\SubModule.cs:554: _harmony.PatchCategory("Late_ActionSetOverride");
Main\SubModule.cs:555: _harmony.PatchCategory("Patch6_BannerEditor");
Main\SubModule.cs:556: _harmony.PatchCategory("Patch7_FactionMap");
Main\SubModule.cs:557: _harmony.PatchCategory("Patch9_RaceFilter");
Main\SubModule.cs:558: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
Main\SubModule.cs:559: _harmony.PatchCategory("Patch8_SiegeCampGuard");
Main\SubModule.cs:560: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
Main\SubModule.cs:561: _harmony.PatchCategory("Patch11_Diplomacy");
Main\SubModule.cs:562: _harmony.PatchCategory("Patch12_WarOfTheRing");
Main\SubModule.cs:564: _harmony.PatchCategory("Patch14_Execution");
Main\SubModule.cs:565: _harmony.PatchCategory("Patch15_BannerLayerLimit");
Main\SubModule.cs:566: _harmony.PatchCategory("Patch16_AtmospherePersistence");
Main\SubModule.cs:567: _harmony.PatchCategory("Patch17_TroopWeight");
Main\SubModule.cs:568: _harmony.PatchCategory("Patch23_BannerColorPersistence");
Main\SubModule.cs:569: _harmony.PatchCategory("Patch24_BannerDriftGuard");
Main\SubModule.cs:570: _harmony.PatchCategory("Patch39_BanditPartySize");
Main\SubModule.cs:571: _harmony.PatchCategory("Patch40_HideoutDescription");
Main\SubModule.cs:572: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
Main\SubModule.cs:581: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
Main\SubModule.cs:587: _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
Main\SubModule.cs:594: _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
Main\SubModule.cs:599: _harmony.PatchCategory("Patch13_RaceAge");
Main\SubModule.cs:606: _harmony.PatchCategory("Patch26_SpecialResources");
Main\SubModule.cs:607: _harmony.PatchCategory("Patch27_CareerSystem");
Main\SubModule.cs:608: _harmony.PatchCategory("Patch29_CCBodyProperties");
Main\SubModule.cs:609: _harmony.PatchCategory("Patch44_CCNameAutofill");
Main\SubModule.cs:610: _harmony.PatchCategory("Patch33_EquipPresets");
Main\SubModule.cs:611: _harmony.PatchCategory("Patch34_QuickActions");
Main\SubModule.cs:612: _harmony.PatchCategory("Patch35_CompanionTactics");
Main\SubModule.cs:613: _harmony.PatchCategory("Patch36_FiefManagement");
Main\SubModule.cs:615: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
Main\SubModule.cs:629: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
Main\SubModule.cs:711: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
out\0Harmony.decompiled.cs:5733: public HarmonyPatchCategory(string category)
out\0Harmony.decompiled.cs:6522: public void PatchCategory(string category)
out\0Harmony.decompiled.cs:6526: PatchCategory(assembly, category);
out\0Harmony.decompiled.cs:6529: public void PatchCategory(Assembly assembly, string category)
out\0Harmony.decompiled.cs:6642: public void UnpatchCategory(string category)
out\0Harmony.decompiled.cs:6646: UnpatchCategory(assembly, category);
out\0Harmony.decompiled.cs:6649: public void UnpatchCategory(Assembly assembly, string category)
TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:31: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:44: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fft9w7e' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fyfj2_j' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp29jamgyn' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp2owwhlqy' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp5516wbdv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp78qze76y' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp8gds9xfp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpbwbm4o8d' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpc85yzrbw' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpi3maldm8' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpkr2gpims' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpktgkfgay' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplcnrd81l' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplorpih3w' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpm39f2s15' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpov4yy7nx' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpsanxk9fp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpwy0s04pv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpyqy6lg5o' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp2\tmpulsj5ei5' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

2026-06-18T15:03:13.739395Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 24 seconds
Total output lines: 1033
Output:
.claude\worktrees\impl-001\Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
.claude\worktrees\impl-001\Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
.claude\worktrees\impl-001\Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
.claude\worktrees\impl-001\Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
.claude\worktrees\impl-001\Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
.claude\worktrees\impl-001\Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
.claude\worktrees\impl-001\Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-001\Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-001\Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
.claude\worktrees\impl-001\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
.claude\worktrees\impl-001\Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-001\Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-001\Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
.claude\worktrees\impl-001\Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
.claude\worktrees\impl-001\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
.claude\worktrees\impl-001\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
.claude\worktrees\impl-001\Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
.claude\worktrees\impl-001\Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
.claude\worktrees\impl-001\Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
.claude\worktrees\impl-001\Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
.claude\worktrees\impl-001\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-001\Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
.claude\worktrees\impl-001\Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-001\Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
.claude\worktrees\impl-001\Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-001\Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
.claude\worktrees\impl-001\Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
.claude\worktrees\impl-001\Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
.claude\worktrees\impl-001\Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
.claude\worktrees\impl-001\Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
.claude\worktrees\impl-001\Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
.claude\worktrees\impl-001\Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:30: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
.claude\worktrees\impl-001\Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
.claude\worktrees\impl-001\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
.claude\worktrees\impl-001\Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-001\Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-001\Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
.claude\worktrees\impl-001\Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
.claude\worktrees\impl-001\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-001\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\impl-001\Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
.claude\worktrees\imp…24312 tokens truncated…ventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:22: [HarmonyPatchCategory("Patch13_RaceAge")]
Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:17: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
Main\Features\Spider\Hooks\Agent_HandleBlowAux_SpiderDismountGuard_Patch.cs:31: [HarmonyPatchCategory("Patch48_SpiderHitDismountGuard")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\ClanPartyItemVM_UpdateProperties_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyUpgraderUpgradeReadyTroops_Patch.cs:14: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\SubModule.cs:109: _harmony.PatchCategory("Patch37_CrashReport");
Main\SubModule.cs:133: _harmony.PatchCategory("Patch41_McmLayoutFix");
Main\SubModule.cs:139: _harmony.PatchCategory("Patch25_LocalizationOverride");
Main\SubModule.cs:155: _harmony.PatchCategory("Patch18_CulturalFeats");
Main\SubModule.cs:156: _harmony.PatchCategory("Patch19_CustomBattles");
Main\SubModule.cs:162: _harmony.PatchCategory("Patch0_BattleScenes");
Main\SubModule.cs:196: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
Main\SubModule.cs:200: _harmony.PatchCategory("Patch22_ArmyTargeting");
Main\SubModule.cs:206: _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
Main\SubModule.cs:207: _harmony.PatchCategory("Patch30_MixedFormations");
Main\SubModule.cs:251: _harmony.PatchCategory("Patch42_CastleRecruitment");
Main\SubModule.cs:548: _harmony.PatchCategory("Patch1_FirstTimeInit");
Main\SubModule.cs:549: _harmony.PatchCategory("Patch2_RefreshTableau");
Main\SubModule.cs:550: _harmony.PatchCategory("Patch3_SetRace");
Main\SubModule.cs:551: _harmony.PatchCategory("Patch4_CharacterSpawner");
Main\SubModule.cs:552: _harmony.PatchCategory("Patch5_FaceGen");
Main\SubModule.cs:553: _harmony.PatchCategory("Late_Transpiler");
Main\SubModule.cs:554: _harmony.PatchCategory("Late_ActionSetOverride");
Main\SubModule.cs:555: _harmony.PatchCategory("Patch6_BannerEditor");
Main\SubModule.cs:556: _harmony.PatchCategory("Patch7_FactionMap");
Main\SubModule.cs:557: _harmony.PatchCategory("Patch9_RaceFilter");
Main\SubModule.cs:558: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
Main\SubModule.cs:559: _harmony.PatchCategory("Patch8_SiegeCampGuard");
Main\SubModule.cs:560: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
Main\SubModule.cs:561: _harmony.PatchCategory("Patch11_Diplomacy");
Main\SubModule.cs:562: _harmony.PatchCategory("Patch12_WarOfTheRing");
Main\SubModule.cs:564: _harmony.PatchCategory("Patch14_Execution");
Main\SubModule.cs:565: _harmony.PatchCategory("Patch15_BannerLayerLimit");
Main\SubModule.cs:566: _harmony.PatchCategory("Patch16_AtmospherePersistence");
Main\SubModule.cs:567: _harmony.PatchCategory("Patch17_TroopWeight");
Main\SubModule.cs:568: _harmony.PatchCategory("Patch23_BannerColorPersistence");
Main\SubModule.cs:569: _harmony.PatchCategory("Patch24_BannerDriftGuard");
Main\SubModule.cs:570: _harmony.PatchCategory("Patch39_BanditPartySize");
Main\SubModule.cs:571: _harmony.PatchCategory("Patch40_HideoutDescription");
Main\SubModule.cs:572: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
Main\SubModule.cs:581: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
Main\SubModule.cs:587: _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
Main\SubModule.cs:594: _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
Main\SubModule.cs:599: _harmony.PatchCategory("Patch13_RaceAge");
Main\SubModule.cs:606: _harmony.PatchCategory("Patch26_SpecialResources");
Main\SubModule.cs:607: _harmony.PatchCategory("Patch27_CareerSystem");
Main\SubModule.cs:608: _harmony.PatchCategory("Patch29_CCBodyProperties");
Main\SubModule.cs:609: _harmony.PatchCategory("Patch44_CCNameAutofill");
Main\SubModule.cs:610: _harmony.PatchCategory("Patch33_EquipPresets");
Main\SubModule.cs:611: _harmony.PatchCategory("Patch34_QuickActions");
Main\SubModule.cs:612: _harmony.PatchCategory("Patch35_CompanionTactics");
Main\SubModule.cs:613: _harmony.PatchCategory("Patch36_FiefManagement");
Main\SubModule.cs:615: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
Main\SubModule.cs:629: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
Main\SubModule.cs:711: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
out\0Harmony.decompiled.cs:5733: public HarmonyPatchCategory(string category)
out\0Harmony.decompiled.cs:6522: public void PatchCategory(string category)
out\0Harmony.decompiled.cs:6526: PatchCategory(assembly, category);
out\0Harmony.decompiled.cs:6529: public void PatchCategory(Assembly assembly, string category)
out\0Harmony.decompiled.cs:6642: public void UnpatchCategory(string category)
out\0Harmony.decompiled.cs:6646: UnpatchCategory(assembly, category);
out\0Harmony.decompiled.cs:6649: public void UnpatchCategory(Assembly assembly, string category)
TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:31: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
TAOM.Tests\Features\CompanionTactics\SharedMovementOrderPostfixTests.cs:44: StringAssert.Contains(src, "[HarmonyPatchCategory(\"Patch_MissionTime_SetMovementOrder\")]",
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fft9w7e' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fyfj2_j' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp29jamgyn' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp2owwhlqy' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp5516wbdv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp78qze76y' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp8gds9xfp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpbwbm4o8d' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpc85yzrbw' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpi3maldm8' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpkr2gpims' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpktgkfgay' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplcnrd81l' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplorpih3w' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpm39f2s15' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpov4yy7nx' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpsanxk9fp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpwy0s04pv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpyqy6lg5o' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp2\tmpulsj5ei5' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -File -Include *.cs[0m | Where-Object { $ …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

codex
A noisy recursive search hit stale worktrees and denied temp directories, so I’m narrowing subsequent searches to the active `Main/` and `Dependencies/` trees. The important result so far is that the real `PatchCategory(...)` call sites in active code are centralized in `Main/SubModule.cs`, with the mission-time category still separately guarded.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File -Include *.cs | Select-String -Pattern 'PatchCategory\\(' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features' -Recurse -File -Include *.cs | Select-String -Pattern '\\[HarmonyTranspiler\\]|throw new|throw;' -Context 2,2 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Dependencies/Foundation/PatchShield.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Dependencies/SubModule.cs' | ForEach-Object { "'$i++; if ($i -ge 160 -and $i -le 245) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 516ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using System.IO;
    4: using System.Linq;
    5: using System.Reflection;
    6: using System.Threading;
    7: using HarmonyLib;
    8: 
    9: namespace TAOM.Dependencies.Foundation;
   10: 
   11: /// <summary>
   12: /// Wraps every Harmony-patched method in the AppDomain with a Finalizer that catches
   13: /// the trinity of "mod compiled against an old Bannerlord version" exceptions:
   14: /// <c>MissingMethodException</c>, <c>MissingFieldException</c>, <c>TypeLoadException</c>.
   15: /// On catch, logs the failure, increments per-category counters, and removes the
   16: /// offending owner's prefixes/postfixes/transpilers from this method via
   17: /// <see cref="Harmony.Unpatch(MethodBase, HarmonyPatchType, string)"/>. The patched
   18: /// method continues running uncaught from the user's perspective — the game keeps going.
   19: ///
   20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
   21: /// This is the single highest-leverage component in BetaDeps's "every BUTR-dependent mod
   22: /// works even when broken" promise.
   23: ///
   24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
   25: /// TAOM.Dependencies module directory to skip install. Useful for diagnosing whether
   26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
   27: ///
   28: /// Install timing: should run AFTER all other mods have applied their Harmony patches
   29: /// — i.e., late in the load lifecycle, NOT in SubModule ctors. See SubModule.cs
   30: /// OnSubModuleLoad or OnBeforeInitialModuleScreenSetAsRoot.
   31: /// </summary>
   32: public static class PatchShield
   33: {
   34:     private const string Tag = "PatchShield";
   35:     private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
   36:     private const string DisableFlagName = "patchshield-disabled.flag";
   37: 
   38:     private static readonly HashSet<MethodBase> _shielded = new();
   39:     private static readonly HashSet<string> _unpatched = new();
   40:     private static readonly object _lock = new();
   41: 
   42:     // Codex review 2026-05-27 S1 (HIGH): expanded from "TAOM" prefix only to full
   43:     // infrastructure-owner allowlist. Vendored BUTR/MCM Harmony IDs ("Bannerlord.ButterLib.SaveSystem",
   44:     // "MCM.UI.Adapter.MCMv5", etc.) do NOT start with "TAOM" — the prior filter would have
   45:     // unpatched the entire BUTR stack on the first MissingMethodException, breaking
   46:     // every dependent mod. This list mirrors the vendored DLLs in
   47:     // Dependencies/_Module/bin/Win64_Shipping_Client/ + Lib.Harmony's own runtime types.
   48:     private static readonly string[] ProtectedOwnerPrefixes =
   49:     {
   50:         "TAOM",
   51:         "Bannerlord.ButterLib",
   52:         "butterlib.",
   53:         "Bannerlord.UIExtenderEx",
   54:         "Bannerlord.MBOptionScreen",
   55:         "Bannerlord.ModuleLoader",
   56:         "Bannerlord.MCM",
   57:         "bannerlord.mcm.",
   58:         "MCM",
   59:         "MCMv5",
   60:         "MCM.UI.Adapter",
   61:         "BUTR.",
   62:         "HarmonyLib.",
   63:         "0Harmony",
   64:     };
   65: 
   66:     private static bool IsProtectedOwner(string owner)
   67:     {
   68:         if (string.IsNullOrEmpty(owner)) return false;
   69:         foreach (var prefix in ProtectedOwnerPrefixes)
   70:         {
   71:             if (owner.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
   72:                 return true;
   73:         }
   74:         return false;
   75:     }
   76: 
   77:     private static readonly Dictionary<string, int> _ownerCounts =
   78:         new(StringComparer.OrdinalIgnoreCase);
   79:     private static readonly object _ownerLock = new();
   80: 
   81:     private static long _swallowedMissingMethod;
   82:     private static long _swallowedMissingField;
   83:     private static long _swallowedTypeLoad;
   84:     private static long _swallowedOther;
   85: 
   86:     public static int ShieldedCount { get { lock (_lock) return _shielded.Count; } }
   87:     public static int UnpatchedCount { get { lock (_lock) return _unpatched.Count; } }
   88:     public static long SwallowedMissingMethod => Interlocked.Read(ref _swallowedMissingMethod);
   89:     public static long SwallowedMissingField => Interlocked.Read(ref _swallowedMissingField);
   90:     public static long SwallowedTypeLoad => Interlocked.Read(ref _swallowedTypeLoad);
   91:     public static long SwallowedOther => Interlocked.Read(ref _swallowedOther);
   92:     public static long SwallowedTotal => SwallowedMissingMethod + SwallowedMissingField + SwallowedTypeLoad + SwallowedOther;
   93: 
   94:     public static bool IsDisabled()
   95:     {
   96:         try
   97:         {
   98:             var dir = RuntimeLog.ModuleDir;
   99:             if (string.IsNullOrEmpty(dir)) return false;
  100:             return File.Exists(Path.Combine(dir, DisableFlagName));
  101:         }
  102:         catch { return false; }
  103:     }
  104: 
  105:     /// <summary>
  106:     /// Installs the shield: iterates all currently-patched methods, attaches a
  107:     /// Finalizer to each. Idempotent — methods already shielded are skipped.
  108:     /// Safe to call multiple times to "shield-pass" new patches added by mods
  109:     /// that load after our first install (call from a late lifecycle hook).
  110:     /// </summary>
  111:     public static void Install()
  112:     {
  113:         if (IsDisabled())
  114:         {
  115:             DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
  116:             return;
  117:         }
  118: 
  119:         try
  120:         {
  121:             var harmony = new Harmony(HarmonyId);
  122:             var voidFinalizer = typeof(PatchShield).GetMethod(
  123:                 nameof(ShieldFinalizerVoid),
  124:                 BindingFlags.Static | BindingFlags.NonPublic);
  125:             var resultFinalizer = typeof(PatchShield).GetMethod(
  126:                 nameof(ShieldFinalizerWithResult),
  127:                 BindingFlags.Static | BindingFlags.NonPublic);
  128:             if (voidFinalizer == null || resultFinalizer == null)
  129:             {
  130:                 DiagLog.Log(Tag, "could not resolve shield finalizer methods; aborting install");
  131:                 return;
  132:             }
  133: 
  134:             List<MethodBase> patched;
  135:             try
  136:             {
  137:                 patched = Harmony.GetAllPatchedMethods().ToList();
  138:             }
  139:             catch (Exception ex)
  140:             {
  141:                 DiagLog.LogCaught(Tag, "GetAllPatchedMethods", ex);
  142:                 return;
  143:             }
  144: 
  145:             int added = 0, skipped = 0, alreadyShielded = 0;
  146:             lock (_lock)
  147:             {
  148:                 foreach (var method in patched)
  149:                 {
  150:                     if (method == null) { skipped++; continue; }
  151:                     if (_shielded.Contains(method)) { alreadyShielded++; continue; }
  152: 
  153:                     // Don't shield our own methods.
  154:                     try
  155:                     {
  156:                         var declAsm = method.DeclaringType?.Assembly.GetName().Name ?? string.Empty;
  157:                         if (declAsm.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
  158:                         {
  159:                             _shielded.Add(method);
  160:                             skipped++;
  161:                             continue;
  162:                         }
  163:                     }
  164:                     catch { }
  165: 
  166:                     try
  167:                     {
  168:                         bool isVoid = true;
  169:                         if (method is MethodInfo mi) isVoid = mi.ReturnType == typeof(void);
  170:                         var finalizer = isVoid ? voidFinalizer : resultFinalizer;
  171:                         harmony.Patch(method, prefix: null, postfix: null, transpiler: null,
  172:                             finalizer: new HarmonyMethod(finalizer));
  173:                         _shielded.Add(method);
  174:                         added++;
  175:                     }
  176:                     catch (Exception ex)
  177:                     {
  178:                         skipped++;
  179:                         DiagLog.LogCaught(Tag, $"shielding {method.DeclaringType?.FullName}.{method.Name}", ex);
  180:                     }
  181:                 }
  182:             }
  183: 
  184:             if (added > 0 || alreadyShielded == 0)
  185:             {
  186:                 DiagLog.Log(Tag, $"shield pass: +{added} new, {alreadyShielded} already-shielded, {skipped} skipped (total: {_shielded.Count})");
  187:             }
  188:         }
  189:         catch (Exception ex)
  190:         {
  191:             DiagLog.LogCaught(Tag, "Install", ex);
  192:         }
  193:     }
  194: 
  195:     /// <summary>
  196:     /// Finalizer for void-return methods. Catches the swallow-trinity and returns
  197:     /// silently to suppress the exception; non-matching exceptions are re-thrown by
  198:     /// returning the original exception (Harmony Finalizer convention).
  199:     /// </summary>
  200:     private static Exception? ShieldFinalizerVoid(MethodBase __originalMethod, Exception __exception)
  201:     {
  202:         return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
  203:     }
  204: 
  205:     /// <summary>
  206:     /// Finalizer for return-value methods. Same swallow behavior; the patched method
  207:     /// returns its zero/default value when we swallow because we don't have access
  208:     /// to <c>__result</c> in a Finalizer (Harmony quirk). Acceptable trade-off:
  209:     /// the caller gets a "stub" return value, which is far better than a crash.
  210:     /// </summary>
  211:     private static Exception? ShieldFinalizerWithResult(MethodBase __originalMethod, Exception __exception)
  212:     {
  213:         return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
  214:     }
  215: 
  216:     private static bool ShouldSwallow(MethodBase originalMethod, Exception exception, out Exception unwrapped)
  217:     {
  218:         unwrapped = exception;
  219:         if (exception == null) return false;
  220: 
  221:         // Unwrap TargetInvocationException to get at the real reason.
  222:         var ex = exception;
  223:         while (ex is TargetInvocationException && ex.InnerException != null)
  224:             ex = ex.InnerException;
  225:         unwrapped = ex;
  226: 
  227:         if (ex is MissingMethodException || ex is MissingFieldException || ex is TypeLoadException)
  228:         {
  229:             if (ex is MissingMethodException) Interlocked.Increment(ref _swallowedMissingMethod);
  230:             else if (ex is MissingFieldException) Interlocked.Increment(ref _swallowedMissingField);
  231:             else Interlocked.Increment(ref _swallowedTypeLoad);
  232: 
  233:             try
  234:             {
  235:                 var owner = originalMethod?.DeclaringType?.FullName ?? "?";
  236:                 var name = originalMethod?.Name ?? "?";
  237:                 DiagLog.Log(Tag, $"swallowed {ex.GetType().Name} from a patch on {owner}.{name}: {ex.Message}");
  238:             }
  239:             catch { }
  240: 
  241:             TryUnpatchOffendingPatches(originalMethod, ex);
  242:             return true;
  243:         }
  244: 
  245:         // Codex A2 LOW fix 2026-05-27: do NOT increment _swallowedOther here — this
  246:         // path RETHROWS the exception. The counter previously misled WriteSessionSummary
  247:         // into reporting rethrown exceptions as swallowed.
  248:         return false;
  249:     }
  250: 
  251:     private static void TryUnpatchOffendingPatches(MethodBase originalMethod, Exception ex)
  252:     {
  253:         if (originalMethod == null) return;
  254: 
  255:         // Codex A3 LOW fix 2026-05-27: overload-safe dedupe key. Was
  256:         // <DeclaringType>::<methodName> — overloaded methods shared a key, so the
  257:         // second overload's failure would skip cleanup. Now uses
  258:         // <Module.ModuleVersionId>:<MetadataToken> which is unique per method handle.
  259:         string targetKey;
  260:         try
  261:         {
  262:             targetKey = $"{originalMethod.Module.ModuleVersionId}:{originalMethod.MetadataToken}";
  263:         }
  264:         catch
  265:         {
  266:             // Fallback if Module/MetadataToken unavailable for this method handle.
  267:             try { targetKey = originalMethod.ToString(); }
  268:             catch { return; }
  269:         }
  270: 
  271:         lock (_lock)
  272:         {
  273:             if (_unpatched.Contains(targetKey)) return;  // already cleaned
  274:             _unpatched.Add(targetKey);
  275:         }
  276: 
  277:         try
  278:         {
  279:             var patches = Harmony.GetPatchInfo(originalMethod);
  280:             if (patches == null) return;
  281: 
  282:             var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  283:             foreach (var p in patches.Prefixes) if (p != null) owners.Add(p.owner ?? string.Empty);
  284:             foreach (var p in patches.Postfixes) if (p != null) owners.Add(p.owner ?? string.Empty);
  285:             foreach (var p in patches.Transpilers) if (p != null) owners.Add(p.owner ?? string.Empty);
  286:             foreach (var p in patches.Finalizers) if (p != null) owners.Add(p.owner ?? string.Empty);
  287: 
  288:             var harmony = new Harmony(HarmonyId);
  289:             foreach (var owner in owners)
  290:             {
  291:                 if (string.IsNullOrEmpty(owner) || owner == HarmonyId) continue;
  292: 
  293:                 // Refuse to unpatch protected infrastructure owners (Codex S1 HIGH fix
  294:                 // 2026-05-27). Filter now covers TAOM + vendored BUTR/MCM/Harmony.
  295:                 if (IsProtectedOwner(owner))
  296:                 {
  297:                     DiagLog.Log(Tag, $"refusing to unpatch protected owner '{owner}' on {targetKey}");
  298:                     continue;
  299:                 }
  300: 
  301:                 try
  302:                 {
  303:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Prefix, owner);
  304:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Postfix, owner);
  305:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Transpiler, owner);
  306:                     DiagLog.Log(Tag, $"unpatched owner '{owner}' on {targetKey}");
  307: 
  308:                     lock (_ownerLock)
  309:                     {
  310:                         _ownerCounts.TryGetValue(owner, out var count);
  311:                         _ownerCounts[owner] = count + 1;
  312:                     }
  313:                 }
  314:                 catch (Exception unpatchEx)
  315:                 {
  316:                     DiagLog.LogCaught(Tag, $"Unpatch owner='{owner}' on {targetKey}", unpatchEx);
  317:                 }
  318:             }
  319:         }
  320:         catch (Exception ex2)
  321:         {
  322:             DiagLog.LogCaught(Tag, $"TryUnpatchOffendingPatches({targetKey})", ex2);
  323:         }
  324:     }
  325: 
  326:     /// <summary>
  327:     /// Writes a one-line summary of swallow stats. Wire to AppDomain.ProcessExit.
  328:     /// </summary>
  329:     public static void WriteSessionSummary()
  330:     {
  331:         try
  332:         {
  333:             string topOwner = "(none)";
  334:             lock (_ownerLock)
  335:             {
  336:                 if (_ownerCounts.Count > 0)
  337:                 {
  338:                     var top = _ownerCounts.OrderByDescending(k => k.Value).First();
  339:                     topOwner = $"{top.Key} ({top.Value})";
  340:                 }
  341:             }
  342:             DiagLog.Log(Tag,
  343:                 $"SESSION SUMMARY: shielded {ShieldedCount} method(s), unpatched {UnpatchedCount} target(s), " +
  344:                 $"swallowed {SwallowedTotal} exception(s) " +
  345:                 $"(MissingMethod {SwallowedMissingMethod}, MissingField {SwallowedMissingField}, " +
  346:                 $"TypeLoad {SwallowedTypeLoad}, other {SwallowedOther}). " +
  347:                 $"Top unpatched owner: {topOwner}.");
  348:         }
  349:         catch (Exception ex)
  350:         {
  351:             DiagLog.LogCaught(Tag, "WriteSessionSummary", ex);
  352:         }
  353:     }
  354: }

 succeeded in 475ms:
  160:         }
  161: 
  162:         // DR3 Phase 4 C-series: install ALL the defensive shields here. Originally
  163:         // CollectAssemblyTypesShim + SubModuleConstructionGuard + RunEarlyPhase were
  164:         // called from AliasStubSubModule.ctor, but observed 2026-05-27 — the launcher
  165:         // never constructs AliasStubSubModule because the alias stub module folders
  166:         // have no `bin/<platform>/TAOM.Dependencies.dll` (the DLL lives in TAOM.Dependencies's
  167:         // own bin/, not duplicated to each stub). OnSubModuleLoad fires for TAOM.Dependencies's
  168:         // main SubModule deterministically, so all shields install from one known-working hook.
  169:         DiagLog.Log("Dependencies", "OnSubModuleLoad: installing defensive shields");
  170: 
  171:         // RunEarlyPhase writes session-launching.marker for crash-loop detection. Was
  172:         // supposed to fire from stub ctors (very early), now fires at OnSubModuleLoad
  173:         // (~642ms later — acceptable; pre-OnSubModuleLoad crashes can't be diagnosed by
  174:         // TAOM anyway since our code isn't loaded yet).
  175:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → IncompatibleModDetector.RunEarlyPhase"); IncompatibleModDetector.RunEarlyPhase(); }
  176:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "IncompatibleModDetector.RunEarlyPhase", ex); }
  177: 
  178:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → CollectAssemblyTypesShim.Install"); CollectAssemblyTypesShim.Install(); }
  179:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "CollectAssemblyTypesShim.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] CollectAssemblyTypesShim.Install failed: {ex.Message}"); }
  180: 
  181:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SubModuleConstructionGuard.Install"); SubModuleConstructionGuard.Install(); }
  182:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "SubModuleConstructionGuard.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] SubModuleConstructionGuard.Install failed: {ex.Message}"); }
  183: 
  184:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
  185:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
  186: 
  187:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SaveShield.Install"); SaveShield.Install(); }
  188:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "SaveShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] SaveShield.Install failed: {ex.Message}"); }
  189: 
  190:         // Trigger VersionProbe explicitly so the version is logged. Without this,
  191:         // VersionProbe's lazy-detect via Major/Minor getters never fires (no consumer
  192:         // touches it today). Observed 2026-05-27 — version probe silent in diag.log.
  193:         try
  194:         {
  195:             DiagLog.Log("Dependencies", "OnSubModuleLoad: → VersionProbe (triggering detection)");
  196:             var detected = VersionProbe.IsDetected;
  197:             DiagLog.Log("Dependencies", $"OnSubModuleLoad: VersionProbe.IsDetected={detected} (Major={VersionProbe.Major}, Minor={VersionProbe.Minor}, Revision={VersionProbe.Revision})");
  198:         }
  199:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "VersionProbe trigger", ex); }
  200: 
  201:         // Write a session summary to diag.log on process exit so users can see the
  202:         // shield's swallow-counts even if no crash dump is produced.
  203:         try
  204:         {
  205:             AppDomain.CurrentDomain.ProcessExit += (_, __) =>
  206:             {
  207:                 try { PatchShield.WriteSessionSummary(); } catch { }
  208:             };
  209:             DiagLog.Log("Dependencies", "OnSubModuleLoad: ProcessExit hook for session summary registered");
  210:         }
  211:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "ProcessExit hook", ex); EarlyLog.Error($"[TAOM.Dependencies] ProcessExit hook failed: {ex.Message}"); }
  212: 
  213:         DiagLog.Log("Dependencies", "OnSubModuleLoad: complete");
  214:         EarlyLog.Info("[TAOM.Dependencies] OnSubModuleLoad complete");
  215:     }
  216: 
  217:     /// <summary>
  218:     /// Called when the main menu has rendered — signals the crash-loop detector that
  219:     /// this session reached menu (deletes the launch marker, snapshots modlist as
  220:     /// last-good). Override of MBSubModuleBase.OnGameInitializationFinished, the
  221:     /// closest TaleWorlds lifecycle hook to "we made it past load."
  222:     /// </summary>
  223:     public override void OnGameInitializationFinished(Game game)
  224:     {
  225:         base.OnGameInitializationFinished(game);
  226:         DiagLog.Log("Dependencies", "OnGameInitializationFinished: entered (main menu reached)");
  227: 
  228:         try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → MarkSessionLaunchSuccessful"); IncompatibleModDetector.MarkSessionLaunchSuccessful(); }
  229:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "MarkSessionLaunchSuccessful", ex); EarlyLog.Error($"[TAOM.Dependencies] MarkSessionLaunchSuccessful failed: {ex.Message}"); }
  230: 
  231:         // Second PatchShield pass — captures patches registered by mods that hook this
  232:         // lifecycle event (after our OnSubModuleLoad).
  233:         try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
  234:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
  235: 
  236:         DiagLog.Log("Dependencies", "OnGameInitializationFinished: complete");
  237:     }
  238: 
  239:     private static void ApplyHarmonyGuards()
  240:     {
  241:         var harmony = new Harmony(GuardHarmonyId);
  242:         var unpatchAll = AccessTools.Method(typeof(Harmony), nameof(Harmony.UnpatchAll));
  243:         harmony.Patch(unpatchAll, prefix: new HarmonyMethod(typeof(SubModule), nameof(UnpatchAllGuard)));
  244:     }
  245: 

 succeeded in 684ms:
Main\Features\AdvancedCombat\Services\BoneCollisionService.cs:31: throw new ArgumentNullException(nameof(component));
Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:26: [HarmonyTranspiler]
Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:19: [HarmonyTranspiler]
Main\Features\CastleRecruitment\Hooks\CastleNotableMaintainer.cs:116: default: throw new ArgumentOutOfRangeException(nameof(occupation), occupation, "Unmapped castle occupation");
Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:26: [HarmonyTranspiler]
Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:28: [HarmonyTranspiler]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:26: [HarmonyTranspiler]
Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:31: throw new TaomDevTriggerException("TAOM CrashReport dev trigger: app-tick throw fired by MCM toggle.");
Main\Features\CrashReport\DevTriggers\CrashReportDevTrigger.cs:31: throw new TaomDevTriggerException("TAOM CrashReport dev trigger: mission-tick throw fired by MCM toggle.");
Main\Features\CrashReport\DevTriggers\CrashReportDevTrigger.cs:33: catch (TaomDevTriggerException) { throw; }
Main\Features\CulturalFeats\TaomCulturalFeats.cs:13: ?? throw new InvalidOperationException(
Main\Features\CultureMarketplace\CultureItemPoolService.cs:154: throw new InvalidOperationException("CultureItemPoolService: BuildPools must be called before GetPool");
Main\Features\CultureMarketplace\CultureMarketplaceBehavior.cs:87: foreach (var settlement in Campaign.Current?.Settlements ?? throw new InvalidOperationException("Campaign.Current is null in OnNewGameCreatedPartialFollowUp"))
Main\Features\CultureMarketplace\CultureMarketplaceInjectionService.cs:30: throw new ArgumentNullException(nameof(rng));
Main\Features\EditorCacheRebuild\Phase1\ParallelPhase1Builder.cs:85: throw;
Main\Features\EditorCacheRebuild\Phase1\ParallelPhase1Builder.cs:96: throw;
Main\Features\EditorCacheRebuild\Phase2\ParallelPhase2Builder.cs:91: throw;
Main\Features\EditorCacheRebuild\Phase2\ParallelPhase2Builder.cs:101: throw;
Main\Features\FiefManagement\UI\GauntletFiefManagementScreen.cs:65: throw;
Main\Features\HeroRace\CharacterSpawnerService.cs:29: _raceManager = raceManager ?? throw new ArgumentNullException(nameof(raceManager));
Main\Features\HeroRace\CharacterSpawnerService.cs:30: _faceGenAdapter = faceGenAdapter ?? throw new ArgumentNullException(nameof(faceGenAdapter));
Main\Features\HeroRace\CharacterSpawnerService.cs:31: _logger = logger ?? throw new ArgumentNullException(nameof(logger));
Main\Features\HeroRace\CharacterTableauService.cs:25: _raceManager = raceManager ?? throw new ArgumentNullException(nameof(raceManager));
Main\Features\MixedFormations\LayoutPositioner.cs:63: throw new System.ArgumentOutOfRangeException(
Main\Features\NativeSkinFixes\NativeSkinFixesInstaller.cs:81: if (logger == null) throw new ArgumentNullException(nameof(logger));
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:25: [HarmonyTranspiler]
Main\Features\ShaderPrecompilation\TaomShaderGameManager.cs:71: ?? throw new InvalidOperationException("[ShaderPrecompilation] No player character resolved");
Main\Features\ShaderPrecompilation\TaomShaderGameManager.cs:106: ?? throw new InvalidOperationException("[ShaderPrecompilation] No player character for scene pass");
Main\Features\ShaderPrecompilation\TaomShaderGameManager.cs:128: ?? throw new InvalidOperationException("No fallback culture available");
Main\Features\Siege\Models\TaomSiegeEventModel.cs:42: _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
Main\Features\TroopProgression\VolunteerRecruitmentService.cs:945: throw new ArgumentNullException(nameof(condition), $"Conditional pool for '{settlementId}' needs a predicate.");
Main\Features\TroopProgression\VolunteerRecruitmentService.cs:975: throw new ArgumentException($"Volunteer pool for '{ownerId}' is empty.", nameof(entries));
Main\Features\TroopProgression\VolunteerRecruitmentService.cs:981: throw new ArgumentException($"Volunteer pool for '{ownerId}' contains a blank troop id.", nameof(entries));
Main\Features\TroopProgression\VolunteerRecruitmentService.cs:983: throw new ArgumentException($"Volunteer pool for '{ownerId}': '{troopId}' has weight {weight}; must be positive.", nameof(entries));

 succeeded in 816ms:
Main\Features\AdvancedCombat\Hooks\Agent_CheckToDropFlaggedItem_Guard_Patch.cs:26: [HarmonyPatchCategory("Patch50_DropFlaggedItemGuard")]
Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
Main\Features\ArmyTargeting\Hooks\Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs:29: [HarmonyPatchCategory("Patch49_ArmyGatheringNreGuard")]
Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CulturalFeats\Hooks\PartyBaseHelper_HasFeat_Patch.cs:31: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_StartAlliance_Patch.cs:17: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:22: [HarmonyPatchCategory("Patch13_RaceAge")]
Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:17: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
Main\Features\Spider\Hooks\Agent_HandleBlowAux_SpiderDismountGuard_Patch.cs:31: [HarmonyPatchCategory("Patch48_SpiderHitDismountGuard")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\ClanPartyItemVM_UpdateProperties_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyUpgraderUpgradeReadyTroops_Patch.cs:14: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\SubModule.cs:109: _harmony.PatchCategory("Patch37_CrashReport");
Main\SubModule.cs:133: _harmony.PatchCategory("Patch41_McmLayoutFix");
Main\SubModule.cs:139: _harmony.PatchCategory("Patch25_LocalizationOverride");
Main\SubModule.cs:155: _harmony.PatchCategory("Patch18_CulturalFeats");
Main\SubModule.cs:156: _harmony.PatchCategory("Patch19_CustomBattles");
Main\SubModule.cs:162: _harmony.PatchCategory("Patch0_BattleScenes");
Main\SubModule.cs:196: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
Main\SubModule.cs:200: _harmony.PatchCategory("Patch22_ArmyTargeting");
Main\SubModule.cs:206: _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
Main\SubModule.cs:207: _harmony.PatchCategory("Patch30_MixedFormations");
Main\SubModule.cs:251: _harmony.PatchCategory("Patch42_CastleRecruitment");
Main\SubModule.cs:548: _harmony.PatchCategory("Patch1_FirstTimeInit");
Main\SubModule.cs:549: _harmony.PatchCategory("Patch2_RefreshTableau");
Main\SubModule.cs:550: _harmony.PatchCategory("Patch3_SetRace");
Main\SubModule.cs:551: _harmony.PatchCategory("Patch4_CharacterSpawner");
Main\SubModule.cs:552: _harmony.PatchCategory("Patch5_FaceGen");
Main\SubModule.cs:553: _harmony.PatchCategory("Late_Transpiler");
Main\SubModule.cs:554: _harmony.PatchCategory("Late_ActionSetOverride");
Main\SubModule.cs:555: _harmony.PatchCategory("Patch6_BannerEditor");
Main\SubModule.cs:556: _harmony.PatchCategory("Patch7_FactionMap");
Main\SubModule.cs:557: _harmony.PatchCategory("Patch9_RaceFilter");
Main\SubModule.cs:558: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
Main\SubModule.cs:559: _harmony.PatchCategory("Patch8_SiegeCampGuard");
Main\SubModule.cs:560: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
Main\SubModule.cs:561: _harmony.PatchCategory("Patch11_Diplomacy");
Main\SubModule.cs:562: _harmony.PatchCategory("Patch12_WarOfTheRing");
Main\SubModule.cs:564: _harmony.PatchCategory("Patch14_Execution");
Main\SubModule.cs:565: _harmony.PatchCategory("Patch15_BannerLayerLimit");
Main\SubModule.cs:566: _harmony.PatchCategory("Patch16_AtmospherePersistence");
Main\SubModule.cs:567: _harmony.PatchCategory("Patch17_TroopWeight");
Main\SubModule.cs:568: _harmony.PatchCategory("Patch23_BannerColorPersistence");
Main\SubModule.cs:569: _harmony.PatchCategory("Patch24_BannerDriftGuard");
Main\SubModule.cs:570: _harmony.PatchCategory("Patch39_BanditPartySize");
Main\SubModule.cs:571: _harmony.PatchCategory("Patch40_HideoutDescription");
Main\SubModule.cs:572: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
Main\SubModule.cs:581: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
Main\SubModule.cs:587: _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
Main\SubModule.cs:594: _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
Main\SubModule.cs:599: _harmony.PatchCategory("Patch13_RaceAge");
Main\SubModule.cs:606: _harmony.PatchCategory("Patch26_SpecialResources");
Main\SubModule.cs:607: _harmony.PatchCategory("Patch27_CareerSystem");
Main\SubModule.cs:608: _harmony.PatchCategory("Patch29_CCBodyProperties");
Main\SubModule.cs:609: _harmony.PatchCategory("Patch44_CCNameAutofill");
Main\SubModule.cs:610: _harmony.PatchCategory("Patch33_EquipPresets");
Main\SubModule.cs:611: _harmony.PatchCategory("Patch34_QuickActions");
Main\SubModule.cs:612: _harmony.PatchCategory("Patch35_CompanionTactics");
Main\SubModule.cs:613: _harmony.PatchCategory("Patch36_FiefManagement");
Main\SubModule.cs:615: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
Main\SubModule.cs:629: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
Main\SubModule.cs:711: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");

codex
`PatchShield` does not actually unpatch TAOM-owned patches: its protected owner prefixes include `"TAOM"` and it logs/refuses protected owners before unpatching. That makes the suspected “PatchShield unpatches a TAOM category and the guard prevents reapply” path look disputed rather than a real regression, but I’m still checking for non-TAOM direct reapplication and other throwing transpilers.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features' -Recurse -File -Include *.cs | Where-Object { "'$_.FullName -match '"'\\\\(Hooks|Patches)\\\\' } | Select-String -Pattern 'throw new ArgumentException|throw new InvalidOperationException|throw;' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features' -Recurse -File -Include *.cs | Where-Object { "'$_.FullName -match '"'\\\\(Hooks|Patches)\\\\' } | Select-String -Pattern '\\[HarmonyTranspiler\\]' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File -Include *.cs | Select-String -Pattern 'PatchCategory\\(' | Where-Object { "'$_.Path -notlike '"'*SubModule.cs' } | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Command ilspycmd | Format-List Source,Version' in C:\Users\mikew\source\repos\TAOM
 succeeded in 416ms:

[32;1mSource  : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mVersion : [0m10.0.1.8346


 succeeded in 663ms:
 succeeded in 678ms:
Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:26: [HarmonyTranspiler]
Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:19: [HarmonyTranspiler]
Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:26: [HarmonyTranspiler]
Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:28: [HarmonyTranspiler]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:26: [HarmonyTranspiler]
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:25: [HarmonyTranspiler]

 succeeded in 832ms:
Main\Features\AdvancedCombat\Hooks\Agent_CheckToDropFlaggedItem_Guard_Patch.cs:26: [HarmonyPatchCategory("Patch50_DropFlaggedItemGuard")]
Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
Main\Features\ArmyTargeting\Hooks\Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs:29: [HarmonyPatchCategory("Patch49_ArmyGatheringNreGuard")]
Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CulturalFeats\Hooks\PartyBaseHelper_HasFeat_Patch.cs:31: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_StartAlliance_Patch.cs:17: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:22: [HarmonyPatchCategory("Patch13_RaceAge")]
Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:17: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
Main\Features\Spider\Hooks\Agent_HandleBlowAux_SpiderDismountGuard_Patch.cs:31: [HarmonyPatchCategory("Patch48_SpiderHitDismountGuard")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\ClanPartyItemVM_UpdateProperties_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyUpgraderUpgradeReadyTroops_Patch.cs:14: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]

codex
The active hook/patch tree has six transpilers. A focused search found no throwing anchor-failure code in those hook/patch files after the RaceAge soft-fail change; the remaining `throw` matches are unrelated services/dev triggers or ordinary non-transpiler paths. Next I’m decompiling the installed v1.4.6 module lifecycle instead of relying on cached decompile output.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.MBSubModuleBase "'$dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File -Include *.cs | Select-String -Pattern '\\.PatchCategory\\(' | Where-Object { "'$_.Path -notlike '"'*SubModule.cs' } | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.Module "'$dll | Select-String -Pattern '"'OnGameInitializationFinished|OnGameStart|SubModules|MBSubModuleBase' -Context 5,8 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'; ilspycmd --type TaleWorlds.Core.Game "'$dll | Select-String -Pattern '"'OnGameInitializationFinished|Started|Initialize|GameState' -Context 4,6 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 801ms:
 succeeded in 802ms:
using System.Collections.Generic;
using TaleWorlds.Core;

namespace TaleWorlds.MountAndBlade;

public abstract class MBSubModuleBase
{
	protected internal virtual void OnSubModuleLoad()
	{
	}

	protected internal virtual void OnSubModuleUnloaded()
	{
	}

	protected internal virtual void OnBeforeInitialModuleScreenSetAsRoot()
	{
	}

	protected internal virtual void RegisterSubModuleTypes()
	{
	}

	protected internal virtual void OnNewModuleLoad()
	{
	}

	public virtual void OnConfigChanged()
	{
	}

	protected internal virtual void OnBeforeGameStart(MBGameManager mbGameManager, List<string> disabledModules)
	{
	}

	protected internal virtual void OnGameStart(Game game, IGameStarter gameStarterObject)
	{
	}

	protected internal virtual void OnApplicationTick(float dt)
	{
	}

	protected internal virtual void AfterAsyncTickTick(float dt)
	{
	}

	protected internal virtual void InitializeGameStarter(Game game, IGameStarter starterObject)
	{
	}

	public virtual void OnGameLoaded(Game game, object initializerObject)
	{
	}

	public virtual void OnAfterGameLoaded(Game game)
	{
	}

	public virtual void OnNewGameCreated(Game game, object initializerObject)
	{
	}

	public virtual void BeginGameStart(Game game)
	{
	}

	public virtual void OnCampaignStart(Game game, object starterObject)
	{
	}

	public virtual void RegisterSubModuleObjects(bool isSavedCampaign)
	{
	}

	public virtual void AfterRegisterSubModuleObjects(bool isSavedCampaign)
	{
	}

	public virtual void OnMultiplayerGameStart(Game game, object starterObject)
	{
	}

	public virtual void OnGameInitializationFinished(Game game)
	{
	}

	public virtual void OnAfterGameInitializationFinished(Game game, object starterObject)
	{
	}

	public virtual bool DoLoading(Game game)
	{
		return true;
	}

	public virtual void OnGameEnd(Game game)
	{
	}

	public virtual void OnMissionBehaviorInitialize(Mission mission)
	{
	}

	public virtual void OnBeforeMissionBehaviorInitialize(Mission mission)
	{
	}

	public virtual void OnInitialState()
	{
	}

	protected internal virtual void OnNetworkTick(float dt)
	{
	}

	public virtual void OnSubModuleActivated()
	{
	}

	public virtual void OnSubModuleDeactivated()
	{
	}

	public virtual void InitializeSubModuleGameObjects(Game game)
	{
	}
}

 succeeded in 1483ms:
  
  	private TestContext _testContext;
  
  	private SingleThreadedSynchronizationContext _synchronizationContext;
  
> 	private readonly Dictionary<SubModuleInfo, MBSubModuleBase> _subModuleBases;
  
  	private bool _splashScreenPlayed;
  
  	private List<InitialStateOption> _initialStateOptions;
  
  	private IEditorMissionTester _editorMissionTester;
  
  	private Dictionary<string, MultiplayerGameMode> _multiplayerGameModesWithNames;
  	private Module()
  	{
  		MBDebug.Print("Creating module...");
  		StartupInfo = new GameStartupInfo();
  		_testContext = new TestContext();
> 		_subModuleBases = new Dictionary<SubModuleInfo, MBSubModuleBase>();
  		GlobalGameStateManager = new GameStateManager(this, GameStateManager.GameStateManagerType.Global);
  		GameStateManager.Current = GlobalGameStateManager;
  		GlobalTextManager = new GameTextManager();
  		JobManager = new JobManager();
  	}
  
> 	public MBReadOnlyList<MBSubModuleBase> CollectSubModules()
  	{
> 		MBList<MBSubModuleBase> mBList = new MBList<MBSubModuleBase>();
  		foreach (ModuleInfo allModule in ModuleHelper.GetAllModules())
  		{
  			if (!allModule.IsActive)
  			{
  				continue;
  			}
> 			foreach (SubModuleInfo subModule in allModule.SubModules)
  			{
> 				MBSubModuleBase subModuleBase = GetSubModuleBase(subModule);
  				if (subModuleBase != null)
  				{
  					mBList.Add(subModuleBase);
  				}
  			}
  		}
  		return mBList;
  	}
  		Dictionary<string, Type> types;
  		AssemblyLoader.AssemblyLoadResult assemblyLoadResult = CollectModuleAssemblyTypes(subModuleInfo, subModuleAssembly, out types);
  		if (assemblyLoadResult == AssemblyLoader.AssemblyLoadResult.Success)
  		{
  			Managed.AddTypes(types);
> 			MBSubModuleBase value = (MBSubModuleBase)constructor.Invoke(new object[0]);
  			_subModuleBases.Add(subModuleInfo, value);
  		}
  		return assemblyLoadResult;
  	}
  
  	private AssemblyLoader.AssemblyLoadResult CollectModuleAssemblyTypes(SubModuleInfo subModule, Assembly moduleAssembly, out Dictionary<string, Type> types)
  	{
  		try
  	}
  
  	private void InitializeSubModuleBases()
  	{
  		Managed.AddConstructorDelegateOfClass<SpawnedItemEntity>();
> 		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnSubModuleLoad();
  		}
  	}
  
  	private void OnNewModuleLoaded()
  	{
> 		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnNewModuleLoad();
  		}
  	}
  
> 	private MBSubModuleBase GetSubModuleBase(SubModuleInfo subModuleInfo)
  	{
  		if (_subModuleBases.TryGetValue(subModuleInfo, out var value))
  		{
  			return value;
  		}
  		return null;
  	}
  
> 	private void FinalizeSubModulesBases()
  	{
> 		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnSubModuleUnloaded();
  		}
  	}
  
  	[MBCallback(null, false)]
  	internal void LoadSingleModule(string modulePath)
  	{
  		List<ModuleInfo> list = new List<ModuleInfo>();
  		list.Add(ModuleHelper.InitializeSingleModule(modulePath));
  		LocalizedTextManager.AddLocalizationXml(modulePath);
> 		LoadSubModules(list, loadNewModules: true);
  		BannerManager.ResetAndLoad();
  	}
  
  	[MBCallback(null, false)]
  	internal void Initialize()
  	{
  		MBDebug.Print("Module Initialize begin...");
  		TWParallel.InitializeAndSetImplementation(new NativeParallelDriver());
  		LoadLocalizationXmls();
  		GlobalTextManager.LoadDefaultTexts();
  		IsOnlyCoreContentEnabled = Utilities.IsOnlyCoreContentEnabled();
  		NativeConfig.OnConfigChanged();
  		List<ModuleInfo> modules = ModuleHelper.GetModules();
> 		LoadSubModules(modules, loadNewModules: false);
  		MBDebug.Print("Adding trace listener...");
  		MBDebug.Print("MBModuleBase Initialize begin...");
  		MBDebug.Print("MBModuleBase Initialize end...");
  		GameNetwork.FindGameNetworkMessages();
  		GameNetwork.FindSynchedMissionObjectTypes();
  		HasTableauCache.CollectTableauCacheTypes();
  		MBDebug.Print("Module Initialize end...");
  		MBDebug.TestModeEnabled = Utilities.CommandLineArgumentExists("/runTest");
  		_synchronizationContext.Tick();
  		if (GameManagerBase.Current != null)
  		{
  			GameManagerBase.Current.OnTick(dt);
  		}
> 		foreach (MBSubModuleBase item in CollectSubModules())
  		{
  			item.OnApplicationTick(dt);
  		}
  		JobManager.OnTick(dt);
  		AvatarServices.UpdateAvatarServices(dt);
  	}
  
  	private void OnConfirmReturnToMainMenu()
  		MBGameManager.EndGame();
  	}
  
  	private void OnNetworkTick(float dt)
  	{
> 		foreach (MBSubModuleBase item in CollectSubModules())
  		{
  			item.OnNetworkTick(dt);
  		}
  	}
  
  	[MBCallback(null, false)]
  	internal void RunTest(string commandLine)
  	{
  	{
  		if (GameStateManager.Current != GlobalGameStateManager)
  		{
  			GameStateManager.Current = GlobalGameStateManager;
  		}
> 		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnBeforeInitialModuleScreenSetAsRoot();
  		}
  		if (GameNetwork.IsDedicatedServer)
  		{
  			return;
  		}
  		string text = ModuleHelper.GetModuleFullPath("Native") + "Videos/TWLogo_and_Partners.ivf";
  		}
  		if (IsOnlyCoreContentEnabled || !MultiplayerRequested)
  		{
  			GlobalGameStateManager.CleanAndPushState(GlobalGameStateManager.CreateState<InitialState>());
  		}
> 		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnInitialState();
  		}
  	}
  
  	private void OnSignInStateUpdated(bool isLoggedIn, TextObject message)
  	{
  		if (!isLoggedIn && !(GlobalGameStateManager.ActiveState is ProfileSelectionState))
  		{
  			JobManager.AddJob(new OnPlatformRequestedMultiplayerJob());
  		}
  	}
  
> 	private void LoadSubModules(List<ModuleInfo> modules, bool loadNewModules)
  	{
> 		MBDebug.Print("Loading submodules...");
  		foreach (ModuleInfo module in modules)
  		{
  			XmlResource.GetMbprojxmls(module.Id);
  			XmlResource.GetXmlListAndApply(module.Id);
  		}
  		List<SubModuleInfo> list = new List<SubModuleInfo>();
  		new List<ModuleInfo>();
  		foreach (ModuleInfo module2 in modules)
  		{
> 			foreach (SubModuleInfo subModule in module2.SubModules)
  			{
  				if (!CheckIfSubmoduleCanBeLoadable(subModule) || _subModuleBases.ContainsKey(subModule))
  				{
  					continue;
  				}
  				string path = System.IO.Path.Combine(module2.FolderPath, "bin", Common.ConfigName);
  				string text = System.IO.Path.Combine(path, subModule.DLLName);
  				string text2 = ManagedDllFolder.Name + subModule.DLLName;
  		}
  		if (loadNewModules)
  		{
  			foreach (SubModuleInfo item in list)
  			{
> 				MBSubModuleBase value = null;
  				if (_subModuleBases.TryGetValue(item, out value))
  				{
  					value.OnSubModuleLoad();
  				}
  			}
  			OnNewModuleLoaded();
  		}
  		else
  		return stringBuilder.ToString();
  	}
  
  	public Type GetSubModuleType(string name)
  	{
> 		foreach (KeyValuePair<SubModuleInfo, MBSubModuleBase> subModuleBasis in _subModuleBases)
  		{
  			if (subModuleBasis.Key.SubModuleClassTypeName == name)
  			{
  				return subModuleBasis.Value.GetType();
  			}
  		}
  		return null;
  	}
  		_testContext.FinalizeContext();
  		MBInformationManager.Clear();
  		InformationManager.Clear();
  		ScreenManager.OnFinalize();
  		BannerlordConfig.Save();
> 		FinalizeSubModulesBases();
  		PlatformServices.Instance?.Terminate();
  		Common.MemoryCleanupGC();
  		GC.WaitForPendingFinalizers();
  	}
  
  	internal static void FinalizeCurrentModule()
  	{
  		CurrentModule.FinalizeModule();
  		GameStateManager.Current.PopState();
  	}
  
  	private void OnConfigChanged()
  	{
> 		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnConfigChanged();
  		}
  	}
  
  	private void OnConstrainedStateChange(bool isConstrained)
  	{
  		if (!isConstrained)
  		{
  			return;
  		}
  		TaleWorlds.Library.Debug.Print("Deactivating Module: " + moduleId, 0, TaleWorlds.Library.Debug.DebugColor.Green);
  		ModuleHelper.OnModuleDeactivated(moduleId);
> 		foreach (SubModuleInfo subModule in moduleInfo.SubModules)
  		{
  			if (_subModuleBases.TryGetValue(subModule, out var value))
  			{
  				value.OnSubModuleDeactivated();
  			}
  		}
  	}
  
  		{
  			return;
  		}
  		TaleWorlds.Library.Debug.Print("Activating Module: " + moduleId, 0, TaleWorlds.Library.Debug.DebugColor.Green);
  		ModuleHelper.OnModuleActivated(moduleId);
> 		foreach (SubModuleInfo subModule in moduleInfo.SubModules)
  		{
  			if (_subModuleBases.TryGetValue(subModule, out var value))
  			{
  				value.OnSubModuleActivated();
  			}
  		}
  	}
  
  	internal void OnBeforeGameStart(MBGameManager mbGameManager)
  	{
  		List<string> list = new List<string>();
> 		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnBeforeGameStart(mbGameManager, list);
  		}
  		foreach (string item in list)
  		{
  			ModuleInfo moduleInfo = ModuleHelper.GetModuleInfo(item);
  			if (moduleInfo != null && moduleInfo.IsActive)
  			{

 succeeded in 1156ms:
  
  namespace TaleWorlds.Core;
  
  [SaveableRootClass(5000)]
> public sealed class Game : IGameStateManagerOwner
  {
  	public enum State
  	{
  		Running,
  		Destroying,
  		Destroyed
  	public GameManagerBase GameManager { get; private set; }
  
  	public GameTextManager GameTextManager { get; private set; }
  
> 	public GameStateManager GameStateManager { get; private set; }
  
  	public bool CheatMode => GameManager.CheatMode;
  
  	public bool IsDevelopmentMode => GameManager.IsDevelopmentMode;
  
  	public bool IsEditModeOn => GameManager.IsEditModeOn;
  		GameManager.Game = this;
  		EventManager = new EventManager();
  		ObjectManager = objectManager;
  		RandomGenerator = new MBFastRandom();
> 		InitializeParameters();
  	}
  
  	public static Game CreateGame(GameType gameType, GameManagerBase gameManager)
  	{
  		MBObjectManager objectManager = MBObjectManager.Init();
  		RegisterTypes(gameType, objectManager, gameManager);
  		MBSaveLoad.OnStartGame(loadResult);
  		MBObjectManager objectManager = MBObjectManager.Init();
  		Game obj = (Game)loadResult.Root;
  		RegisterTypes(obj.GameType, objectManager, gameManager);
> 		loadResult.InitializeObjects();
> 		MBObjectManager.Instance.ReInitialize();
> 		loadResult.AfterInitializeObjects();
  		GC.Collect();
  		obj.ObjectManager = objectManager;
  		obj.BeginLoading(gameManager);
  		return obj;
  	}
  
  		GameType.CurrentGame = this;
  		GameManager = gameManager;
  		GameManager.Game = this;
  		EventManager = new EventManager();
> 		InitializeParameters();
  	}
  
  	private void SaveAux(MetaData metaData, string saveName, ISaveDriver driver, Action<SaveResult> onSaveCompleted)
  	{
  		foreach (GameHandler component in _gameEntitySystem.Components)
  		{
  			SaveAux(metaData, saveName, driver, onSaveCompleted);
  		}
  	}
  
> 	private void InitializeParameters()
  	{
> 		ManagedParameters.Instance.Initialize(ModuleHelper.GetXmlPath("Native", "managed_core_parameters"));
> 		GameType.InitializeParameters();
  	}
  
> 	void IGameStateManagerOwner.OnStateStackEmpty()
  	{
  		Destroy();
  	}
  
  	public void Destroy()
  	{
  		GameType.OnDestroy();
  		ObjectManager.Destroy();
  		EventManager.Clear();
  		EventManager = null;
> 		GameStateManager.Current = null;
> 		GameStateManager = null;
  		Current = null;
  		CurrentState = State.Destroyed;
  		_currentActiveSaveData = null;
  		Common.MemoryCleanupGC();
  	}
  
  	public void CreateGameManager()
  	{
> 		GameStateManager = new GameStateManager(this, GameStateManager.GameStateManagerType.Game);
  	}
  
> 	public void OnStateChanged(GameState oldState)
  	{
  		GameType.OnStateChanged(oldState);
  	}
  
  	public T AddGameHandler<T>() where T : GameHandler, new()
  	{
  	{
  		_gameEntitySystem.RemoveComponent<T>();
  	}
  
> 	public void Initialize()
  	{
  		if (_gameEntitySystem == null)
  		{
  			_gameEntitySystem = new EntitySystem<GameHandler>();
  		}
  		GameTextManager = new GameTextManager();
  		GameTextManager.LoadGameTexts();
  		_gameModelManagers = new Dictionary<Type, GameModelsManager>();
> 		GameTexts.Initialize(GameTextManager);
> 		GameType.OnInitialize();
  	}
  
  	public static void RegisterTypes(GameType gameType, MBObjectManager objectManager, GameManagerBase gameManager)
  	{
  		gameType?.BeforeRegisterTypes(objectManager);
  		objectManager.RegisterType<Monster>("Monster", "Monsters", 2u);
  	}
  
  	internal void OnTick(float dt)
  	{
> 		if (GameStateManager.Current == GameStateManager)
  		{
> 			GameStateManager.OnTick(dt);
  			if (_gameEntitySystem != null)
  			{
  				foreach (GameHandler component in _gameEntitySystem.Components)
  				{
  					try
  					{
  	{
  		return GameType.DoLoadingForGameType();
  	}
  
> 	public void OnMissionIsStarting(string missionName, MissionInitializerRecord rec)
  	{
  		GameType.OnMissionIsStarting(missionName, rec);
  	}
  
  	public void OnFinalize()
  	{
  		CurrentState = State.Destroying;
> 		GameStateManager.Current.CleanStates();
  	}
  
> 	public void InitializeDefaultGameObjects()
  	{
  		DefaultCharacterAttributes = new DefaultCharacterAttributes();
  		DefaultSkills = new DefaultSkills();
  		DefaultBannerEffects = new DefaultBannerEffects();
  		DefaultItemCategories = new DefaultItemCategories();
  		DefaultSiegeEngineTypes = new DefaultSiegeEngineTypes();
> 		GameManager.InitializeSubModuleGameObjects(Current);
  	}
  
  	public void LoadBasicFiles()
  	{
  		ObjectManager.LoadXML("Monsters");
  		ObjectManager.LoadXML("SkeletonScales");

codex
The base method itself is empty in the installed `TaleWorlds.MountAndBlade.dll`, so calling it before the guard is harmless and preserves the lifecycle contract. I still need the installed caller that dispatches the event to settle the threading/static-submodule questions.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter '*.dll' | Select-String -Pattern 'OnGameInitializationFinished' | ForEach-Object { "'$_.Path } | Sort-Object -Unique' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'; ilspycmd --type TaleWorlds.Core.GameManagerBase "'$dll | Select-String -Pattern '"'OnGameInitializationFinished|OnAfterGameInitializationFinished|OnGameStart|InitializeGameStarter|BeginGameStart|foreach|GameManager' -Context 4,8 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.MBGameManager "'$dll | Select-String -Pattern '"'OnGameInitializationFinished|OnAfterGameInitializationFinished|OnGameStart|InitializeGameStarter|BeginGameStart|foreach' -Context 4,8 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.MBGameManager "'$dll' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1062ms:
using System.Collections.Generic;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.ObjectSystem;
using TaleWorlds.PlatformService;

namespace TaleWorlds.MountAndBlade;

public abstract class MBGameManager : GameManagerBase
{
	private readonly object _lockObject = new object();

	public bool IsEnding { get; private set; }

	public new static MBGameManager Current => (MBGameManager)GameManagerBase.Current;

	public bool IsLoaded { get; protected set; }

	public override float ApplicationTime => MBCommon.GetApplicationTime();

	public override bool CheatMode => NativeConfig.CheatMode;

	public override bool IsDevelopmentMode => NativeConfig.IsDevelopmentMode;

	public override bool IsEditModeOn => MBEditor.IsEditModeOn;

	public override UnitSpawnPrioritizations UnitSpawnPrioritization => (UnitSpawnPrioritizations)BannerlordConfig.UnitSpawnPrioritization;

	protected MBGameManager()
	{
		IsEnding = false;
		NativeConfig.OnConfigChanged();
	}

	protected static void StartNewGame()
	{
		MBAPI.IMBGame.StartNew();
	}

	protected static void LoadModuleData(bool isLoadGame)
	{
		MBAPI.IMBGame.LoadModuleData(isLoadGame);
	}

	public static void StartNewGame(MBGameManager gameLoader)
	{
		Module.CurrentModule.OnBeforeGameStart(gameLoader);
		GameLoadingState gameLoadingState = GameStateManager.Current.CreateState<GameLoadingState>();
		gameLoadingState.SetLoadingParameters(gameLoader);
		GameStateManager.Current.CleanAndPushState(gameLoadingState);
	}

	public override void BeginGameStart(Game game)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.BeginGameStart(game);
		}
	}

	public override void OnNewCampaignStart(Game game, object starterObject)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnCampaignStart(game, starterObject);
		}
	}

	public override void InitializeSubModuleGameObjects(Game game)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.InitializeSubModuleGameObjects(game);
		}
	}

	public override void RegisterSubModuleObjects(bool isSavedCampaign)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.RegisterSubModuleObjects(isSavedCampaign);
		}
	}

	public override void RegisterSubModuleTypes()
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.RegisterSubModuleTypes();
		}
	}

	public override void AfterRegisterSubModuleObjects(bool isSavedCampaign)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.AfterRegisterSubModuleObjects(isSavedCampaign);
		}
	}

	public override void InitializeGameStarter(Game game, IGameStarter starterObject)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.InitializeGameStarter(game, starterObject);
		}
	}

	public override void OnGameInitializationFinished(Game game)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnGameInitializationFinished(game);
		}
		foreach (SkeletonScale objectType in Game.Current.ObjectManager.GetObjectTypeList<SkeletonScale>())
		{
			sbyte[] array = new sbyte[objectType.BoneNames.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Skeleton.GetBoneIndexFromName(objectType.SkeletonModel, objectType.BoneNames[i]);
			}
			objectType.SetBoneIndices(array);
		}
	}

	public override void OnAfterGameInitializationFinished(Game game, object initializerObject)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnAfterGameInitializationFinished(game, initializerObject);
		}
	}

	public override void OnGameLoaded(Game game, object initializerObject)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnGameLoaded(game, initializerObject);
		}
	}

	public override void OnAfterGameLoaded(Game game)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnAfterGameLoaded(game);
		}
	}

	public override void OnNewGameCreated(Game game, object initializerObject)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnNewGameCreated(game, initializerObject);
		}
	}

	public override void OnGameStart(Game game, IGameStarter gameStarter)
	{
		Game.Current.MonsterMissionDataCreator = new MonsterMissionDataCreator();
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnGameStart(game, gameStarter);
		}
		Game.Current.AddGameModelsManager<MissionGameModels>(gameStarter.Models);
		Monster.GetBoneIndexWithId = MBActionSet.GetBoneIndexWithId;
		Monster.GetBoneHasParentBone = MBActionSet.GetBoneHasParentBone;
	}

	public override void OnGameEnd(Game game)
	{
		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
		{
			item.OnGameEnd(game);
		}
		Module.CurrentModule.OnGameEnd();
		MissionGameModels.Clear();
		base.OnGameEnd(game);
	}

	public static async void EndGame()
	{
		while (true)
		{
			MBGameManager current = Current;
			if (current == null || current.IsLoaded)
			{
				break;
			}
			await Task.Delay(100);
		}
		MBGameManager current2 = Current;
		if ((current2 != null && !current2.CheckAndSetEnding()) || Game.Current.GameStateManager == null)
		{
			return;
		}
		while (Mission.Current != null && !(Game.Current.GameStateManager.ActiveState is MissionState))
		{
			Game.Current.GameStateManager.PopState();
		}
		if (Game.Current.GameStateManager.ActiveState is MissionState)
		{
			((MissionState)Game.Current.GameStateManager.ActiveState).CurrentMission.EndMission();
			while (Mission.Current != null)
			{
				await Task.Delay(1);
			}
		}
		else
		{
			Game.Current.GameStateManager.CleanStates();
		}
	}

	public override void OnLoadFinished()
	{
		IsLoaded = true;
	}

	public bool CheckAndSetEnding()
	{
		lock (_lockObject)
		{
			if (IsEnding)
			{
				return false;
			}
			IsEnding = true;
			return true;
		}
	}

	public virtual void OnSessionInvitationAccepted(SessionInvitationType targetGameType)
	{
		if (targetGameType != SessionInvitationType.None)
		{
			EndGame();
		}
	}

	public virtual void OnPlatformRequestedMultiplayer()
	{
		EndGame();
	}

	protected List<MbObjectXmlInformation> GetXmlInformationFromModule()
	{
		return XmlResource.XmlInformationList;
	}
}

 succeeded in 1099ms:
  using System.Collections.Generic;
  
  namespace TaleWorlds.Core;
  
> public abstract class GameManagerBase
  {
> 	private EntitySystem<GameManagerComponent> _entitySystem;
  
> 	private GameManagerLoadingSteps _stepNo;
  
  	private Game _game;
  
  	private bool _initialized;
  
> 	public static GameManagerBase Current { get; private set; }
  
  	public Game Game
  	{
  		get
  		{
  			return _game;
  		}
  		internal set
  			}
  		}
  	}
  
> 	public IEnumerable<GameManagerComponent> Components => _entitySystem.Components;
  
  	public abstract float ApplicationTime { get; }
  
  	public abstract bool CheatMode { get; }
  
  	public abstract bool IsDevelopmentMode { get; }
  
  	public abstract bool IsEditModeOn { get; }
  			_initialized = true;
  		}
  	}
  
> 	protected GameManagerBase()
  	{
  		Current = this;
> 		_entitySystem = new EntitySystem<GameManagerComponent>();
> 		_stepNo = GameManagerLoadingSteps.PreInitializeZerothStep;
  	}
  
> 	public GameManagerComponent AddComponent(Type componentType)
  	{
> 		GameManagerComponent gameManagerComponent = _entitySystem.AddComponent(componentType);
> 		gameManagerComponent.GameManager = this;
> 		return gameManagerComponent;
  	}
  
> 	public T AddComponent<T>() where T : GameManagerComponent, new()
  	{
  		return (T)AddComponent(typeof(T));
  	}
  
> 	public GameManagerComponent GetComponent(Type componentType)
  	{
  		return _entitySystem.GetComponent(componentType);
  	}
  
> 	public T GetComponent<T>() where T : GameManagerComponent
  	{
  		return _entitySystem.GetComponent<T>();
  	}
  
> 	public IEnumerable<T> GetComponents<T>() where T : GameManagerComponent
  	{
  		return _entitySystem.GetComponents<T>();
  	}
  
> 	public void RemoveComponent<T>() where T : GameManagerComponent
  	{
  		T component = _entitySystem.GetComponent<T>();
  		RemoveComponent(component);
  	}
  
> 	public void RemoveComponent(GameManagerComponent component)
  	{
  		_entitySystem.RemoveComponent(component);
  	}
  
  	public void OnTick(float dt)
  	{
> 		foreach (GameManagerComponent component in _entitySystem.Components)
  		{
  			component.OnTick();
  		}
  		if (Game != null)
  		{
  			Game.OnTick(dt);
  		}
  	}
  
  	public void OnGameNetworkBegin()
  	{
> 		foreach (GameManagerComponent component in _entitySystem.Components)
  		{
  			component.OnGameNetworkBegin();
  		}
  		if (Game != null)
  		{
  			Game.OnGameNetworkBegin();
  		}
  	}
  
  	public void OnGameNetworkEnd()
  	{
> 		foreach (GameManagerComponent component in _entitySystem.Components)
  		{
  			component.OnGameNetworkEnd();
  		}
  		if (Game != null)
  		{
  			Game.OnGameNetworkEnd();
  		}
  	}
  
  	public void OnPlayerConnect(VirtualPlayer peer)
  	{
> 		foreach (GameManagerComponent component in _entitySystem.Components)
  		{
  			component.OnEarlyPlayerConnect(peer);
  		}
  		if (Game != null)
  		{
  			Game.OnEarlyPlayerConnect(peer);
  		}
> 		foreach (GameManagerComponent component2 in _entitySystem.Components)
  		{
  			component2.OnPlayerConnect(peer);
  		}
  		if (Game != null)
  		{
  			Game.OnPlayerConnect(peer);
  		}
  	}
  
  	public void OnPlayerDisconnect(VirtualPlayer peer)
  	{
> 		foreach (GameManagerComponent component in _entitySystem.Components)
  		{
  			component.OnPlayerDisconnect(peer);
  		}
  		if (Game != null)
  		{
  			Game.OnPlayerDisconnect(peer);
  		}
  	}
  		Current = null;
  		Game = null;
  	}
  
> 	protected virtual void DoLoadingForGameManager(GameManagerLoadingSteps gameManagerLoadingStep, out GameManagerLoadingSteps nextStep)
  	{
> 		nextStep = GameManagerLoadingSteps.None;
  	}
  
> 	public bool DoLoadingForGameManager()
  	{
  		bool result = false;
> 		GameManagerLoadingSteps nextStep = GameManagerLoadingSteps.None;
  		switch (_stepNo)
  		{
> 		case GameManagerLoadingSteps.LoadingIsOver:
  			result = true;
  			break;
> 		case GameManagerLoadingSteps.PreInitializeZerothStep:
> 			DoLoadingForGameManager(GameManagerLoadingSteps.PreInitializeZerothStep, out nextStep);
> 			if (nextStep == GameManagerLoadingSteps.FirstInitializeFirstStep)
  			{
  				_stepNo++;
  			}
  			break;
> 		case GameManagerLoadingSteps.FirstInitializeFirstStep:
> 			DoLoadingForGameManager(GameManagerLoadingSteps.FirstInitializeFirstStep, out nextStep);
> 			if (nextStep == GameManagerLoadingSteps.WaitSecondStep)
  			{
  				_stepNo++;
  			}
  			break;
> 		case GameManagerLoadingSteps.WaitSecondStep:
> 			DoLoadingForGameManager(GameManagerLoadingSteps.WaitSecondStep, out nextStep);
> 			if (nextStep == GameManagerLoadingSteps.SecondInitializeThirdState)
  			{
  				_stepNo++;
  			}
  			break;
> 		case GameManagerLoadingSteps.SecondInitializeThirdState:
> 			DoLoadingForGameManager(GameManagerLoadingSteps.SecondInitializeThirdState, out nextStep);
> 			if (nextStep == GameManagerLoadingSteps.PostInitializeFourthState)
  			{
  				_stepNo++;
  			}
  			break;
> 		case GameManagerLoadingSteps.PostInitializeFourthState:
> 			DoLoadingForGameManager(GameManagerLoadingSteps.PostInitializeFourthState, out nextStep);
> 			if (nextStep == GameManagerLoadingSteps.FinishLoadingFifthStep)
  			{
  				_stepNo++;
  			}
  			break;
> 		case GameManagerLoadingSteps.FinishLoadingFifthStep:
> 			DoLoadingForGameManager(GameManagerLoadingSteps.FinishLoadingFifthStep, out nextStep);
> 			if (nextStep == GameManagerLoadingSteps.None)
  			{
  				_stepNo++;
  				result = true;
  			}
  			break;
  		}
  		return result;
  	}
  	public virtual void OnLoadFinished()
  	{
  	}
  
> 	public virtual void InitializeGameStarter(Game game, IGameStarter starterObject)
  	{
  	}
  
> 	public abstract void OnGameStart(Game game, IGameStarter gameStarter);
  
> 	public abstract void BeginGameStart(Game game);
  
  	public abstract void OnNewCampaignStart(Game game, object starterObject);
  
  	public abstract void OnAfterCampaignStart(Game game);
  
  	public abstract void RegisterSubModuleObjects(bool isSavedCampaign);
  
  	public abstract void AfterRegisterSubModuleObjects(bool isSavedCampaign);
  
> 	public abstract void OnGameInitializationFinished(Game game);
  
  	public abstract void OnNewGameCreated(Game game, object initializerObject);
  
  	public abstract void OnGameLoaded(Game game, object initializerObject);
  
  	public abstract void OnAfterGameLoaded(Game game);
  
> 	public abstract void OnAfterGameInitializationFinished(Game game, object initializerObject);
  
  	public abstract void RegisterSubModuleTypes();
  
  	public virtual void InitializeSubModuleGameObjects(Game game)
  	{
  	}
  }

 succeeded in 1243ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Core.dll
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.dll

 succeeded in 1220ms:
  		gameLoadingState.SetLoadingParameters(gameLoader);
  		GameStateManager.Current.CleanAndPushState(gameLoadingState);
  	}
  
> 	public override void BeginGameStart(Game game)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
> 			item.BeginGameStart(game);
  		}
  	}
  
  	public override void OnNewCampaignStart(Game game, object starterObject)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.OnCampaignStart(game, starterObject);
  		}
  	}
  
  	public override void InitializeSubModuleGameObjects(Game game)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.InitializeSubModuleGameObjects(game);
  		}
  	}
  
  	public override void RegisterSubModuleObjects(bool isSavedCampaign)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.RegisterSubModuleObjects(isSavedCampaign);
  		}
  	}
  
  	public override void RegisterSubModuleTypes()
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.RegisterSubModuleTypes();
  		}
  	}
  
  	public override void AfterRegisterSubModuleObjects(bool isSavedCampaign)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.AfterRegisterSubModuleObjects(isSavedCampaign);
  		}
  	}
  
> 	public override void InitializeGameStarter(Game game, IGameStarter starterObject)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
> 			item.InitializeGameStarter(game, starterObject);
  		}
  	}
  
> 	public override void OnGameInitializationFinished(Game game)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
> 			item.OnGameInitializationFinished(game);
  		}
> 		foreach (SkeletonScale objectType in Game.Current.ObjectManager.GetObjectTypeList<SkeletonScale>())
  		{
  			sbyte[] array = new sbyte[objectType.BoneNames.Count];
  			for (int i = 0; i < array.Length; i++)
  			{
  				array[i] = Skeleton.GetBoneIndexFromName(objectType.SkeletonModel, objectType.BoneNames[i]);
  			}
  			objectType.SetBoneIndices(array);
  		}
  	}
  
> 	public override void OnAfterGameInitializationFinished(Game game, object initializerObject)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
> 			item.OnAfterGameInitializationFinished(game, initializerObject);
  		}
  	}
  
  	public override void OnGameLoaded(Game game, object initializerObject)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.OnGameLoaded(game, initializerObject);
  		}
  	}
  
  	public override void OnAfterGameLoaded(Game game)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.OnAfterGameLoaded(game);
  		}
  	}
  
  	public override void OnNewGameCreated(Game game, object initializerObject)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.OnNewGameCreated(game, initializerObject);
  		}
  	}
  
> 	public override void OnGameStart(Game game, IGameStarter gameStarter)
  	{
  		Game.Current.MonsterMissionDataCreator = new MonsterMissionDataCreator();
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
> 			item.OnGameStart(game, gameStarter);
  		}
  		Game.Current.AddGameModelsManager<MissionGameModels>(gameStarter.Models);
  		Monster.GetBoneIndexWithId = MBActionSet.GetBoneIndexWithId;
  		Monster.GetBoneHasParentBone = MBActionSet.GetBoneHasParentBone;
  	}
  
  	public override void OnGameEnd(Game game)
  	{
> 		foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  		{
  			item.OnGameEnd(game);
  		}
  		Module.CurrentModule.OnGameEnd();
  		MissionGameModels.Clear();
  		base.OnGameEnd(game);
  	}
  

codex
Installed `MBGameManager.OnGameInitializationFinished` dispatches to every collected submodule, then does skeleton-scale setup; there’s no parallel dispatch or task handoff in that method. I’m following the loading-state caller now because that’s the best evidence for the static flag/threading assessment.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter '*.dll' | ForEach-Object { "'$dll=$_.FullName; try { $out = ilspycmd $dll 2>$null | Select-String -Pattern '"'OnGameInitializationFinished\\(' -Context 3,5; if ("'$out) { $out | ForEach-Object { '"'DLL: ' + [System.IO.Path]::GetFileName("'$dll); $_.ToString() } } } catch {} }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'; ilspycmd --type TaleWorlds.Core.GameLoadingState "'$dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll'; ilspycmd --type TaleWorlds.Core.GameManagerBase "'$dll' in C:\Users\mikew\source\repos\TAOM
2026-06-18T15:04:40.073948Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.Core.GameLoadingState in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 573ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.Core.GameLoadingState in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 succeeded in 927ms:
using System;
using System.Collections.Generic;

namespace TaleWorlds.Core;

public abstract class GameManagerBase
{
	private EntitySystem<GameManagerComponent> _entitySystem;

	private GameManagerLoadingSteps _stepNo;

	private Game _game;

	private bool _initialized;

	public static GameManagerBase Current { get; private set; }

	public Game Game
	{
		get
		{
			return _game;
		}
		internal set
		{
			if (value == null)
			{
				_game = null;
				_initialized = false;
			}
			else
			{
				_game = value;
				Initialize();
			}
		}
	}

	public IEnumerable<GameManagerComponent> Components => _entitySystem.Components;

	public abstract float ApplicationTime { get; }

	public abstract bool CheatMode { get; }

	public abstract bool IsDevelopmentMode { get; }

	public abstract bool IsEditModeOn { get; }

	public abstract UnitSpawnPrioritizations UnitSpawnPrioritization { get; }

	public void Initialize()
	{
		if (!_initialized)
		{
			_initialized = true;
		}
	}

	protected GameManagerBase()
	{
		Current = this;
		_entitySystem = new EntitySystem<GameManagerComponent>();
		_stepNo = GameManagerLoadingSteps.PreInitializeZerothStep;
	}

	public GameManagerComponent AddComponent(Type componentType)
	{
		GameManagerComponent gameManagerComponent = _entitySystem.AddComponent(componentType);
		gameManagerComponent.GameManager = this;
		return gameManagerComponent;
	}

	public T AddComponent<T>() where T : GameManagerComponent, new()
	{
		return (T)AddComponent(typeof(T));
	}

	public GameManagerComponent GetComponent(Type componentType)
	{
		return _entitySystem.GetComponent(componentType);
	}

	public T GetComponent<T>() where T : GameManagerComponent
	{
		return _entitySystem.GetComponent<T>();
	}

	public IEnumerable<T> GetComponents<T>() where T : GameManagerComponent
	{
		return _entitySystem.GetComponents<T>();
	}

	public void RemoveComponent<T>() where T : GameManagerComponent
	{
		T component = _entitySystem.GetComponent<T>();
		RemoveComponent(component);
	}

	public void RemoveComponent(GameManagerComponent component)
	{
		_entitySystem.RemoveComponent(component);
	}

	public void OnTick(float dt)
	{
		foreach (GameManagerComponent component in _entitySystem.Components)
		{
			component.OnTick();
		}
		if (Game != null)
		{
			Game.OnTick(dt);
		}
	}

	public void OnGameNetworkBegin()
	{
		foreach (GameManagerComponent component in _entitySystem.Components)
		{
			component.OnGameNetworkBegin();
		}
		if (Game != null)
		{
			Game.OnGameNetworkBegin();
		}
	}

	public void OnGameNetworkEnd()
	{
		foreach (GameManagerComponent component in _entitySystem.Components)
		{
			component.OnGameNetworkEnd();
		}
		if (Game != null)
		{
			Game.OnGameNetworkEnd();
		}
	}

	public void OnPlayerConnect(VirtualPlayer peer)
	{
		foreach (GameManagerComponent component in _entitySystem.Components)
		{
			component.OnEarlyPlayerConnect(peer);
		}
		if (Game != null)
		{
			Game.OnEarlyPlayerConnect(peer);
		}
		foreach (GameManagerComponent component2 in _entitySystem.Components)
		{
			component2.OnPlayerConnect(peer);
		}
		if (Game != null)
		{
			Game.OnPlayerConnect(peer);
		}
	}

	public void OnPlayerDisconnect(VirtualPlayer peer)
	{
		foreach (GameManagerComponent component in _entitySystem.Components)
		{
			component.OnPlayerDisconnect(peer);
		}
		if (Game != null)
		{
			Game.OnPlayerDisconnect(peer);
		}
	}

	public virtual void OnGameEnd(Game game)
	{
		Current = null;
		Game = null;
	}

	protected virtual void DoLoadingForGameManager(GameManagerLoadingSteps gameManagerLoadingStep, out GameManagerLoadingSteps nextStep)
	{
		nextStep = GameManagerLoadingSteps.None;
	}

	public bool DoLoadingForGameManager()
	{
		bool result = false;
		GameManagerLoadingSteps nextStep = GameManagerLoadingSteps.None;
		switch (_stepNo)
		{
		case GameManagerLoadingSteps.LoadingIsOver:
			result = true;
			break;
		case GameManagerLoadingSteps.PreInitializeZerothStep:
			DoLoadingForGameManager(GameManagerLoadingSteps.PreInitializeZerothStep, out nextStep);
			if (nextStep == GameManagerLoadingSteps.FirstInitializeFirstStep)
			{
				_stepNo++;
			}
			break;
		case GameManagerLoadingSteps.FirstInitializeFirstStep:
			DoLoadingForGameManager(GameManagerLoadingSteps.FirstInitializeFirstStep, out nextStep);
			if (nextStep == GameManagerLoadingSteps.WaitSecondStep)
			{
				_stepNo++;
			}
			break;
		case GameManagerLoadingSteps.WaitSecondStep:
			DoLoadingForGameManager(GameManagerLoadingSteps.WaitSecondStep, out nextStep);
			if (nextStep == GameManagerLoadingSteps.SecondInitializeThirdState)
			{
				_stepNo++;
			}
			break;
		case GameManagerLoadingSteps.SecondInitializeThirdState:
			DoLoadingForGameManager(GameManagerLoadingSteps.SecondInitializeThirdState, out nextStep);
			if (nextStep == GameManagerLoadingSteps.PostInitializeFourthState)
			{
				_stepNo++;
			}
			break;
		case GameManagerLoadingSteps.PostInitializeFourthState:
			DoLoadingForGameManager(GameManagerLoadingSteps.PostInitializeFourthState, out nextStep);
			if (nextStep == GameManagerLoadingSteps.FinishLoadingFifthStep)
			{
				_stepNo++;
			}
			break;
		case GameManagerLoadingSteps.FinishLoadingFifthStep:
			DoLoadingForGameManager(GameManagerLoadingSteps.FinishLoadingFifthStep, out nextStep);
			if (nextStep == GameManagerLoadingSteps.None)
			{
				_stepNo++;
				result = true;
			}
			break;
		}
		return result;
	}

	public virtual void OnLoadFinished()
	{
	}

	public virtual void InitializeGameStarter(Game game, IGameStarter starterObject)
	{
	}

	public abstract void OnGameStart(Game game, IGameStarter gameStarter);

	public abstract void BeginGameStart(Game game);

	public abstract void OnNewCampaignStart(Game game, object starterObject);

	public abstract void OnAfterCampaignStart(Game game);

	public abstract void RegisterSubModuleObjects(bool isSavedCampaign);

	public abstract void AfterRegisterSubModuleObjects(bool isSavedCampaign);

	public abstract void OnGameInitializationFinished(Game game);

	public abstract void OnNewGameCreated(Game game, object initializerObject);

	public abstract void OnGameLoaded(Game game, object initializerObject);

	public abstract void OnAfterGameLoaded(Game game);

	public abstract void OnAfterGameInitializationFinished(Game game, object initializerObject);

	public abstract void RegisterSubModuleTypes();

	public virtual void InitializeSubModuleGameObjects(Game game)
	{
	}
}

 succeeded in 149967ms:
DLL: TaleWorlds.CampaignSystem.dll
  			InitializeDefaultEquipments();
  			NameGenerator.Initialize();
  			base.CurrentGame.OnGameStart();
> 			base.GameManager.OnGameInitializationFinished(base.CurrentGame);
  		}
  
  		private void CalculateCachedStatsOnLoad()
  		{
  			ItemRoster.CalculateCachedStatsOnLoad();
DLL: TaleWorlds.Core.dll
  
  		public abstract void AfterRegisterSubModuleObjects(bool isSavedCampaign);
  
> 		public abstract void OnGameInitializationFinished(Game game);
  
  		public abstract void OnNewGameCreated(Game game, object initializerObject);
  
  		public abstract void OnGameLoaded(Game game, object initializerObject);
  
DLL: TaleWorlds.MountAndBlade.dll
  			}
  		}
  
> 		public override void OnGameInitializationFinished(Game game)
  		{
  			foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
  			{
DLL: TaleWorlds.MountAndBlade.dll
> 				item.OnGameInitializationFinished(game);
  			}
  			foreach (SkeletonScale objectType in Game.Current.ObjectManager.GetObjectTypeList<SkeletonScale>())
  			{
  				sbyte[] array = new sbyte[objectType.BoneNames.Count];
  				for (int i = 0; i < array.Length; i++)
DLL: TaleWorlds.MountAndBlade.dll
  		{
  		}
  
> 		public virtual void OnGameInitializationFinished(Game game)
  		{
  		}
  
  		public virtual void OnAfterGameInitializationFinished(Game game, object starterObject)
  		{
DLL: TaleWorlds.MountAndBlade.dll
  			objectManager.UnregisterNonReadyObjects();
  			base.GameManager.OnNewCampaignStart(base.CurrentGame, null);
  			base.GameManager.OnAfterCampaignStart(base.CurrentGame);
> 			base.GameManager.OnGameInitializationFinished(base.CurrentGame);
  		}
  
  		private void InitializeGameModels(IGameStarter basicGameStarter)
  		{
  			basicGameStarter.AddModel(new CustomBattleAgentStatCalculateModel());

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd -l c "'$dll | Select-String -Pattern '"'GameStarter|GameManager|Campaign' | Select-Object -First 120 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd -l c "'$dll | Select-String -Pattern '"'GameManager|CustomBattle|Loading' | Select-Object -First 160 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features' -Recurse -File -Include *.cs | Select-String -Pattern 'UnpatchCategory|UnpatchAll|PatchAll|PatchCategory' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 718ms:
Class TaleWorlds.CampaignSystem.Army
Class TaleWorlds.CampaignSystem.AtmosphereGrid
Class TaleWorlds.CampaignSystem.BattleSimulation
Class TaleWorlds.CampaignSystem.Campaign
Class TaleWorlds.CampaignSystem.CampaignBehaviorBase
Class TaleWorlds.CampaignSystem.CampaignBehaviorDataStore
Class TaleWorlds.CampaignSystem.CampaignCheats
Class TaleWorlds.CampaignSystem.CampaignData
Class TaleWorlds.CampaignSystem.CampaignEntityComponent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MBCampaignEvent
Class TaleWorlds.CampaignSystem.CampaignEventDispatcher
Class TaleWorlds.CampaignSystem.CampaignEventReceiver
Class TaleWorlds.CampaignSystem.CampaignEvents
Class TaleWorlds.CampaignSystem.CampaignPeriodicEventManager
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.CampaignGameStarter
Class TaleWorlds.CampaignSystem.CampaignInformationManager
Class TaleWorlds.CampaignSystem.CampaignMission
Class TaleWorlds.CampaignSystem.CampaignObjectBase
Class TaleWorlds.CampaignSystem.CampaignObjectManager
Class TaleWorlds.CampaignSystem.CampaignOptions
Class TaleWorlds.CampaignSystem.AutoGeneratedSaveManager
Class TaleWorlds.CampaignSystem.MapTimeTracker
Class TaleWorlds.CampaignSystem.CharacterObject
Class TaleWorlds.CampaignSystem.CharacterRelationManager
Class TaleWorlds.CampaignSystem.Clan
Class TaleWorlds.CampaignSystem.Concept
Class TaleWorlds.CampaignSystem.CultureTrait
Class TaleWorlds.CampaignSystem.CultureObject
Class TaleWorlds.CampaignSystem.FactionManager
Class TaleWorlds.CampaignSystem.Hero
Class TaleWorlds.CampaignSystem.Kingdom
Class TaleWorlds.CampaignSystem.KingdomManager
Class TaleWorlds.CampaignSystem.PlayerCaptivity
Class TaleWorlds.CampaignSystem.PolicyObject
Class TaleWorlds.CampaignSystem.QuestBase
Class TaleWorlds.CampaignSystem.JournalLog
Class TaleWorlds.CampaignSystem.QuestManager
Class TaleWorlds.CampaignSystem.QuestTaskBase
Class TaleWorlds.CampaignSystem.Romance
Class TaleWorlds.CampaignSystem.Track
Class TaleWorlds.CampaignSystem.TrackedObject
Class TaleWorlds.CampaignSystem.VisualTrackerManager
Class TaleWorlds.CampaignSystem.TradeRumor
Class TaleWorlds.CampaignSystem.StanceLink
Class TaleWorlds.CampaignSystem.FactionManagerStancesData
Class TaleWorlds.CampaignSystem.CampaignTickCacheDataStore
Class TaleWorlds.CampaignSystem.CampaignTutorial
Class TaleWorlds.CampaignSystem.CharacterData
Class TaleWorlds.CampaignSystem.DefaultItems
Class TaleWorlds.CampaignSystem.DefaultPolicies
Class TaleWorlds.CampaignSystem.DefaultSkillEffects
Class TaleWorlds.CampaignSystem.DialogFlowLine
Class TaleWorlds.CampaignSystem.DialogFlowContext
Class TaleWorlds.CampaignSystem.DialogFlow
Class TaleWorlds.CampaignSystem.EncounterManager
Class TaleWorlds.CampaignSystem.GameModels
Class TaleWorlds.CampaignSystem.GameSceneDataManager
Class TaleWorlds.CampaignSystem.HeroCreator
Class TaleWorlds.CampaignSystem.PartyThinkParams
Class TaleWorlds.CampaignSystem.RandomOwnerExtensions
Class TaleWorlds.CampaignSystem.ManagedParameters
Class TaleWorlds.CampaignSystem.NameGenerator
Class TaleWorlds.CampaignSystem.SandBoxManager
Class TaleWorlds.CampaignSystem.SandBoxMission
Class TaleWorlds.CampaignSystem.SaveableCampaignTypeDefiner
Class TaleWorlds.CampaignSystem.SaveHandler
Class TaleWorlds.CampaignSystem.SkillEffect
Class TaleWorlds.CampaignSystem.TroopUpgradeTracker
Class TaleWorlds.CampaignSystem.VisualCreator
Class TaleWorlds.CampaignSystem.TroopSuppliers.PartyGroupTroopSupplier
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.BecomeKingSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.CampaignSceneNotificationHelper
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.ClanMemberPeaceDeathSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.ClanMemberWarDeathSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.DeathOldAgeSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.DeclareDragonBannerSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.EmpireConspiracyBeginsSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.EmpireConspiracySupportsSceneNotificationItemBase
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.AntiEmpireConspiracyBeginsSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.ProEmpireConspiracyBeginsSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.FindingFirstBannerPieceSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.FindingSecondBannerPieceSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.FindingThirdBannerPieceSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.HeirComingOfAgeFemaleSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.HeirComingOfAgeSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.HeroExecutionSceneNotificationData
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.JoinKingdomSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.KingdomCreatedSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.KingdomDestroyedSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.MainHeroBattleDeathNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.MainHeroBattleVictoryDeathNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.MarriageSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.NavalDeathSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.NewBornFemaleHeroSceneAlternateNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.NewBornFemaleHeroSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.NewBornSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.PledgeAllegianceSceneNotificationItem
Class TaleWorlds.CampaignSystem.SceneInformationPopupTypes.SupportedFactionDefeatedSceneNotificationItem
Class TaleWorlds.CampaignSystem.SaveCompability.ArmyDispersionReasonEnumResolver
Class TaleWorlds.CampaignSystem.SaveCompability.BattleTypeEnumResolver
Class TaleWorlds.CampaignSystem.SaveCompability.CharacterAttributesResolver
Class TaleWorlds.CampaignSystem.SaveCompability.CharacterPerksResolver
Class TaleWorlds.CampaignSystem.SaveCompability.CharacterTraitsResolver
Class TaleWorlds.CampaignSystem.SaveCompability.EndCaptivityDetailEnumResolver
Class TaleWorlds.CampaignSystem.SaveCompability.HeroDeveloperResolver
Class TaleWorlds.CampaignSystem.SaveCompability.HeroTraitDeveloperResolver
Class TaleWorlds.CampaignSystem.Inventory.InventoryListener
Class TaleWorlds.CampaignSystem.Inventory.FakeInventoryListener
Class TaleWorlds.CampaignSystem.Inventory.InventoryLogic

 succeeded in 737ms:
Class NetworkMessages.FromClient.FinishedLoading
Class TaleWorlds.MountAndBlade.CustomBattleAgentOrigin
Class TaleWorlds.MountAndBlade.MBGameManager
Class TaleWorlds.MountAndBlade.CustomBattleAgentStatCalculateModel
Class TaleWorlds.MountAndBlade.CustomBattleApplyWeatherEffectsModel
Class TaleWorlds.MountAndBlade.CustomBattleAutoBlockModel
Class TaleWorlds.MountAndBlade.CustomBattleInitializationModel
Class TaleWorlds.MountAndBlade.CustomBattleMoraleModel
Class TaleWorlds.MountAndBlade.CustomBattleSpawnModel
Class TaleWorlds.MountAndBlade.CustomBattleBannerBearersModel
Class TaleWorlds.MountAndBlade.CustomBattleCombatant
Class TaleWorlds.MountAndBlade.CustomBattleTroopSupplier
Class TaleWorlds.MountAndBlade.EditorGameManager
Class TaleWorlds.MountAndBlade.GameLoadingState
Class TaleWorlds.MountAndBlade.CustomBattleAgentLogic
Class TaleWorlds.MountAndBlade.MissionSpawnHandlers.CustomBattleMissionSpawnHandler
Class TaleWorlds.MountAndBlade.Missions.BattleScore.CustomBattleScoreContext
Class TaleWorlds.MountAndBlade.CustomBattleBannerBearersModel.<>c__DisplayClass11_0
Class TaleWorlds.MountAndBlade.CustomBattleBannerBearersModel.<>c
Class TaleWorlds.MountAndBlade.CustomBattleCombatant.<>c
Class TaleWorlds.MountAndBlade.CustomBattleTroopSupplier.<>c__DisplayClass13_0
Class TaleWorlds.MountAndBlade.GameLoadingState.<>c

 succeeded in 698ms:
Main\Features\AdvancedCombat\Hooks\Agent_CheckToDropFlaggedItem_Guard_Patch.cs:26: [HarmonyPatchCategory("Patch50_DropFlaggedItemGuard")]
Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:32: [HarmonyPatchCategory("Patch46_TournamentDwarfDismount")]
Main\Features\Arena\Hooks\Patch46_TournamentDwarfDismount.cs:43: // to apply and crashed the game on load (HarmonyException at PatchCategory). See RCA 2026-06-09.
Main\Features\ArmyTargeting\Hooks\AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:11: [HarmonyPatchCategory("Patch22_ArmyTargeting")]
Main\Features\ArmyTargeting\Hooks\Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs:29: [HarmonyPatchCategory("Patch49_ArmyGatheringNreGuard")]
Main\Features\AtmospherePersistence\Hooks\Mission_Initialize_Patch.cs:12: [HarmonyPatchCategory("Patch16_AtmospherePersistence")]
Main\Features\BanditManagement\Hooks\Patch39_BanditPartySize.cs:27: [HarmonyPatchCategory("Patch39_BanditPartySize")]
Main\Features\BanditManagement\Hooks\Patch40_HideoutDescription.cs:27: [HarmonyPatchCategory("Patch40_HideoutDescription")]
Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs:7: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Banner_TryGetBannerDataFromCode_Transpiler.cs:10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColor_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:8: [HarmonyPatchCategory("Patch24_BannerDriftGuard")]
Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs:16: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs:12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs:10: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs:9: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
Main\Features\BannerInjection\Hooks\GauntletBannerEditorScreen_OnDone_Patch.cs:8: [HarmonyPatchCategory("Patch6_BannerEditor")]
Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:13: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
Main\Features\BattleScenes\Hooks\Campaign_InitializeScenes_Patch.cs:10: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MapScene_Load_DiagnosticPatch.cs:9: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\BattleScenes\Hooks\MBMapScene_GetBattleSceneIndexMap_Patch.cs:13: [HarmonyPatchCategory("Patch0_BattleScenes")]
Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_CareerScreen_Patch.cs:9: [HarmonyPatchCategory("Patch27_CareerSystem")]
Main\Features\CastleRecruitment\Hooks\Patch42_AiHourlyTick_Transpiler.cs:17: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_FillSettlements_Transpiler.cs:18: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CastleRecruitment\Hooks\Patch42_HourlyTickParty_Postfix.cs:23: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:18: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:57: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:96: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:138: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:183: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:277: [HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
Main\Features\CharacterCreation\Hooks\CharacterCreationContent_SetSelectedCulture_Patch.cs:17: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs:25: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:19: [HarmonyPatchCategory("Patch29_CCBodyProperties")]
Main\Features\CharacterCreation\Hooks\CharacterCreationReviewStageVM_AutoFillName_Patch.cs:26: [HarmonyPatchCategory("Patch44_CCNameAutofill")]
Main\Features\CharacterCreation\Hooks\FaceGenVM_Refresh_RaceFilter_Patch.cs:9: [HarmonyPatchCategory("Patch9_RaceFilter")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:14: [HarmonyPatchCategory("Late_Transpiler")]
Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:20: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:16: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OOBUIHandler_Tick.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Ctor.cs:18: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_OrderOfBattleVM_Finalize.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_RefreshValues.cs:12: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CompanionTactics\Roles\Hooks\Patch35_PartyCharacterVM_RefreshValues.cs:11: [HarmonyPatchCategory("Patch35_CompanionTactics")]
Main\Features\CrashReport\DevTriggers\CrashReportApplicationTickTrigger.cs:18: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:15: // `_harmony.UnpatchCategory("Patch37_CrashReport")` would detach the lot in one call.
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:30: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:37: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:46: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:55: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:64: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:73: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:87: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:96: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:105: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:114: [HarmonyPatchCategory("Patch37_CrashReport")]
Main\Features\CulturalFeats\Hooks\Campaign_InitializeDefaultCampaignObjects_Patch.cs:8: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CulturalFeats\Hooks\PartyBaseHelper_HasFeat_Patch.cs:31: [HarmonyPatchCategory("Patch18_CulturalFeats")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_CustomBattle_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\BannerlordMissions_Siege_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Characters_Patch.cs:12: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleData_Factions_Patch.cs:11: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleHelper_Troop_Patch.cs:10: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_Constructor_Patch.cs:14: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCharacterSelection_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:15: // null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_OnCultureSelection_Patch.cs:18: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_RefreshValues_Patch.cs:9: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\CustomBattles\Hooks\CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:7: [HarmonyPatchCategory("Patch19_CustomBattles")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_AddAllianceDecision_Patch.cs:23: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_EndAlliance_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\AllianceCampaignBehavior_StartAlliance_Patch.cs:17: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\DeclareWarAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch11_Diplomacy")]
Main\Features\Diplomacy\Hooks\MakePeaceAction_ApplyInternal_Patch.cs:9: [HarmonyPatchCategory("Patch12_WarOfTheRing")]
Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs:28: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\EquipPresets\Hooks\Patch33_SPInventoryVMRefresh.cs:26: [HarmonyPatchCategory("Patch33_EquipPresets")]
Main\Features\Execution\Hooks\KillCharacterAction_ApplyInternal_Patch.cs:8: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\Execution\Hooks\TraitLevelingHelper_OnLordExecuted_Patch.cs:7: [HarmonyPatchCategory("Patch14_Execution")]
Main\Features\FactionMap\Hooks\CultureStageView_Constructor_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Finalize_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\CultureStageView_Tick_Patch.cs:8: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FactionMap\Hooks\TrySwitchToNextMenu_Patch.cs:12: [HarmonyPatchCategory("Patch7_FactionMap")]
Main\Features\FiefManagement\Hooks\Patch36_GameStateScreenManager.cs:12: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\FiefManagement\Hooks\Patch36_MapScreenF6.cs:15: [HarmonyPatchCategory("Patch36_FiefManagement")]
Main\Features\HeroRace\Hooks\ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:9: [HarmonyPatchCategory("Late_ActionSetOverride")]
Main\Features\HeroRace\Hooks\CharacterSpawner_InitWithCharacter_Patch.cs:9: [HarmonyPatchCategory("Patch4_CharacterSpawner")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:14: [HarmonyPatchCategory("Patch1_FirstTimeInit")]
Main\Features\HeroRace\Hooks\CharacterTableau_RefreshCharacterTableau_Patch.cs:34: [HarmonyPatchCategory("Patch2_RefreshTableau")]
Main\Features\HeroRace\Hooks\CharacterTableau_SetRace_Patch.cs:10: [HarmonyPatchCategory("Patch3_SetRace")]
Main\Features\HeroRace\Hooks\FaceGen_GetBaseMonsterFromRace_Patch.cs:7: [HarmonyPatchCategory("Patch5_FaceGen")]
Main\Features\LocalizationOverride\Hooks\MBTextManager_GetLocalizedText_Patch.cs:20: [HarmonyPatchCategory("Patch25_LocalizationOverride")]
Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:28: [HarmonyPatchCategory("Patch41_McmLayoutFix")]
Main\Features\MixedFormations\Hooks\Patch30_FormationGetOrderPositionOfUnit.cs:10: [HarmonyPatchCategory("Patch30_MixedFormations")]
Main\Features\QuickActions\Hooks\Patch34_SellAllItemsMenu.cs:21: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMCapture.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMFinalize.cs:19: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\QuickActions\Hooks\Patch34_SPInventoryVMSearchApply.cs:15: [HarmonyPatchCategory("Patch34_QuickActions")]
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:22: [HarmonyPatchCategory("Patch13_RaceAge")]
Main\Features\RaceAge\Hooks\DeliverOffSpring_RaceAssert_Patch.cs:49: // out of PatchCategory and crashing the mod (mirrors RefreshCharacterEntityAuxPatch).
Main\Features\SettlementNameplateFade\Hooks\SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs:24: [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
Main\Features\ShaderPrecompilation\Hooks\LoadingScreen_ShaderProgress_Patch.cs:17: [HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
Main\Features\Siege\Hooks\BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:11: [HarmonyPatchCategory("Patch8_SiegeCampGuard")]
Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:27: [HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
Main\Features\SpecialResources\Hooks\PartyCharacterVM_InitializeUpgrades_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_AddCommand_Patch.cs:11: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\SpecialResources\Hooks\PartyScreenLogic_UpgradeTroop_Patch.cs:9: [HarmonyPatchCategory("Patch26_SpecialResources")]
Main\Features\Spider\Hooks\Agent_Die_SpiderDismount_Patch.cs:24: [HarmonyPatchCategory("Patch47_SpiderDeathDismount")]
Main\Features\Spider\Hooks\Agent_HandleBlowAux_SpiderDismountGuard_Patch.cs:31: [HarmonyPatchCategory("Patch48_SpiderHitDismountGuard")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:9: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\CampaignUIHelper_GetPartyHealthTooltip_Patch.cs:10: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\ClanPartyItemVM_UpdateProperties_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\GameMenuPartyItemVM_RefreshCounts_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfAllMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBase_NumberOfRegularMembers_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyBaseHelper_GetPartySizeText_Patch.cs:11: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyUpgraderUpgradeReadyTroops_Patch.cs:14: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\PartyVM_PopulatePartyListLabel_Patch.cs:8: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\TroopWeight\Hooks\RecruitmentVM_RefreshPartyProperties_Patch.cs:7: [HarmonyPatchCategory("Patch17_TroopWeight")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]
Main\Features\WeatherBoundsGuard\Hooks\DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs:9: [HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]

codex
`TaleWorlds.CampaignSystem` and custom-battle startup both call `base.GameManager.OnGameInitializationFinished(base.CurrentGame)` after game start initialization. The custom battle path matters for this fix because the shader precompile runner starts repeated custom games in one process.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File -Include *.cs | Select-String -SimpleMatch '.PatchCategory(' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd --type TaleWorlds.CampaignSystem.CampaignGameStarter "'$dll | Select-String -Pattern '"'OnGameInitializationFinished|OnGameStart|InitializeDefaultEquipments|NameGenerator|CalculateCachedStatsOnLoad' -Context 8,8 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.CustomGameManager "'$dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.GameLoadingState "'$dll | Select-String -Pattern '"'DoLoading|OnGameInitializationFinished|OnGameStart|GameManager|Task|Thread' -Context 6,10 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
2026-06-18T15:07:36.369335Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.MountAndBlade.CustomGameManager in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 600ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.MountAndBlade.CustomGameManager in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 succeeded in 872ms:
Main\SubModule.cs:109: _harmony.PatchCategory("Patch37_CrashReport");
Main\SubModule.cs:133: _harmony.PatchCategory("Patch41_McmLayoutFix");
Main\SubModule.cs:139: _harmony.PatchCategory("Patch25_LocalizationOverride");
Main\SubModule.cs:155: _harmony.PatchCategory("Patch18_CulturalFeats");
Main\SubModule.cs:156: _harmony.PatchCategory("Patch19_CustomBattles");
Main\SubModule.cs:162: _harmony.PatchCategory("Patch0_BattleScenes");
Main\SubModule.cs:196: _harmony.PatchCategory("Patch21_ShaderPrecompilation");
Main\SubModule.cs:200: _harmony.PatchCategory("Patch22_ArmyTargeting");
Main\SubModule.cs:206: _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
Main\SubModule.cs:207: _harmony.PatchCategory("Patch30_MixedFormations");
Main\SubModule.cs:251: _harmony.PatchCategory("Patch42_CastleRecruitment");
Main\SubModule.cs:548: _harmony.PatchCategory("Patch1_FirstTimeInit");
Main\SubModule.cs:549: _harmony.PatchCategory("Patch2_RefreshTableau");
Main\SubModule.cs:550: _harmony.PatchCategory("Patch3_SetRace");
Main\SubModule.cs:551: _harmony.PatchCategory("Patch4_CharacterSpawner");
Main\SubModule.cs:552: _harmony.PatchCategory("Patch5_FaceGen");
Main\SubModule.cs:553: _harmony.PatchCategory("Late_Transpiler");
Main\SubModule.cs:554: _harmony.PatchCategory("Late_ActionSetOverride");
Main\SubModule.cs:555: _harmony.PatchCategory("Patch6_BannerEditor");
Main\SubModule.cs:556: _harmony.PatchCategory("Patch7_FactionMap");
Main\SubModule.cs:557: _harmony.PatchCategory("Patch9_RaceFilter");
Main\SubModule.cs:558: _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
Main\SubModule.cs:559: _harmony.PatchCategory("Patch8_SiegeCampGuard");
Main\SubModule.cs:560: _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
Main\SubModule.cs:561: _harmony.PatchCategory("Patch11_Diplomacy");
Main\SubModule.cs:562: _harmony.PatchCategory("Patch12_WarOfTheRing");
Main\SubModule.cs:564: _harmony.PatchCategory("Patch14_Execution");
Main\SubModule.cs:565: _harmony.PatchCategory("Patch15_BannerLayerLimit");
Main\SubModule.cs:566: _harmony.PatchCategory("Patch16_AtmospherePersistence");
Main\SubModule.cs:567: _harmony.PatchCategory("Patch17_TroopWeight");
Main\SubModule.cs:568: _harmony.PatchCategory("Patch23_BannerColorPersistence");
Main\SubModule.cs:569: _harmony.PatchCategory("Patch24_BannerDriftGuard");
Main\SubModule.cs:570: _harmony.PatchCategory("Patch39_BanditPartySize");
Main\SubModule.cs:571: _harmony.PatchCategory("Patch40_HideoutDescription");
Main\SubModule.cs:572: _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
Main\SubModule.cs:581: _harmony.PatchCategory("Patch47_SpiderDeathDismount");
Main\SubModule.cs:587: _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
Main\SubModule.cs:594: _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
Main\SubModule.cs:599: _harmony.PatchCategory("Patch13_RaceAge");
Main\SubModule.cs:606: _harmony.PatchCategory("Patch26_SpecialResources");
Main\SubModule.cs:607: _harmony.PatchCategory("Patch27_CareerSystem");
Main\SubModule.cs:608: _harmony.PatchCategory("Patch29_CCBodyProperties");
Main\SubModule.cs:609: _harmony.PatchCategory("Patch44_CCNameAutofill");
Main\SubModule.cs:610: _harmony.PatchCategory("Patch33_EquipPresets");
Main\SubModule.cs:611: _harmony.PatchCategory("Patch34_QuickActions");
Main\SubModule.cs:612: _harmony.PatchCategory("Patch35_CompanionTactics");
Main\SubModule.cs:613: _harmony.PatchCategory("Patch36_FiefManagement");
Main\SubModule.cs:615: _harmony.PatchCategory("Patch38_SettlementNameplateFade");
Main\SubModule.cs:629: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
Main\SubModule.cs:711: _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");

 succeeded in 1029ms:
  namespace TaleWorlds.MountAndBlade;
  
  public class GameLoadingState : GameState
  {
  	private bool _loadingFinished;
  
> 	private MBGameManager _gameLoader;
  
  	public override bool IsMusicMenuState => true;
  
> 	public void SetLoadingParameters(MBGameManager gameLoader)
  	{
  		Game.OnGameCreated += OnGameCreated;
  		_gameLoader = gameLoader;
  	}
  
  	protected override void OnTick(float dt)
  	{
  		base.OnTick(dt);
  		if (!_loadingFinished)
  		{
> 			_loadingFinished = _gameLoader.DoLoadingForGameManager();
  			return;
  		}
  		GameStateManager.Current = Game.Current.GameStateManager;
  		_gameLoader.OnLoadFinished();
  	}
  
  	private void OnGameCreated()
  	{
  		Game.OnGameCreated -= OnGameCreated;
  		Game.Current.OnItemDeserializedEvent += delegate(ItemObject itemObject)

 succeeded in 1083ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd "'$dll | Select-String -Pattern '"'base\\.CurrentGame\\.OnGameStart\\(\\);|base\\.GameManager\\.OnGameInitializationFinished\\(base\\.CurrentGame\\);' -Context 30,10 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd "'$dll | Select-String -Pattern '"'base\\.GameManager\\.OnGameInitializationFinished\\(base\\.CurrentGame\\);' -Context 30,10 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.GameLoadingState "'$dll' in C:\Users\mikew\source\repos\TAOM
 succeeded in 827ms:
using TaleWorlds.Core;
using TaleWorlds.Engine;

namespace TaleWorlds.MountAndBlade;

public class GameLoadingState : GameState
{
	private bool _loadingFinished;

	private MBGameManager _gameLoader;

	public override bool IsMusicMenuState => true;

	public void SetLoadingParameters(MBGameManager gameLoader)
	{
		Game.OnGameCreated += OnGameCreated;
		_gameLoader = gameLoader;
	}

	protected override void OnTick(float dt)
	{
		base.OnTick(dt);
		if (!_loadingFinished)
		{
			_loadingFinished = _gameLoader.DoLoadingForGameManager();
			return;
		}
		GameStateManager.Current = Game.Current.GameStateManager;
		_gameLoader.OnLoadFinished();
	}

	private void OnGameCreated()
	{
		Game.OnGameCreated -= OnGameCreated;
		Game.Current.OnItemDeserializedEvent += delegate(ItemObject itemObject)
		{
			if (itemObject.Type == ItemObject.ItemTypeEnum.HandArmor)
			{
				Utilities.RegisterMeshForGPUMorph(itemObject.MultiMeshName);
			}
		};
	}
}

 succeeded in 22942ms:
  		}
  
  		internal (float startOffset, float endOffset) GetFreeSegment(int segmentIndex)
  		{
  			return _freeSegments[segmentIndex];
  		}
  	}
  	public class EditorGame : GameType
  	{
  		public static EditorGame Current => Game.Current.GameType as EditorGame;
  
  		protected override void OnInitialize()
  		{
  			Game currentGame = base.CurrentGame;
  			IGameStarter gameStarter = new BasicGameStarter();
  			InitializeGameModels(gameStarter);
  			base.GameManager.InitializeGameStarter(currentGame, gameStarter);
  			base.GameManager.OnGameStart(base.CurrentGame, gameStarter);
  			MBObjectManager objectManager = currentGame.ObjectManager;
  			currentGame.SetBasicModels(gameStarter.Models);
  			currentGame.CreateGameManager();
  			base.GameManager.BeginGameStart(base.CurrentGame);
  			currentGame.InitializeDefaultGameObjects();
  			currentGame.LoadBasicFiles();
  			LoadCustomGameXmls();
  			objectManager.UnregisterNonReadyObjects();
  			currentGame.SetDefaultEquipments(new Dictionary<string, Equipment>());
  			objectManager.UnregisterNonReadyObjects();
  			base.GameManager.OnNewCampaignStart(base.CurrentGame, null);
  			base.GameManager.OnAfterCampaignStart(base.CurrentGame);
> 			base.GameManager.OnGameInitializationFinished(base.CurrentGame);
  		}
  
  		private void InitializeGameModels(IGameStarter basicGameStarter)
  		{
  			basicGameStarter.AddModel(new CustomBattleAgentStatCalculateModel());
  			basicGameStarter.AddModel(new CustomAgentApplyDamageModel());
  			basicGameStarter.AddModel(new CustomBattleApplyWeatherEffectsModel());
  			basicGameStarter.AddModel(new CustomBattleMoraleModel());
  			basicGameStarter.AddModel(new CustomBattleInitializationModel());
  			basicGameStarter.AddModel(new CustomBattleSpawnModel());

 succeeded in 24170ms:
  			{
  				AddCampaignBehaviorManager(new CampaignBehaviorManager(campaignGameStarter.CampaignBehaviors));
  				base.GameManager.OnAfterCampaignStart(base.CurrentGame);
  			}
  			else
  			{
  				base.GameManager.OnGameLoaded(base.CurrentGame, campaignGameStarter);
  				_campaignBehaviorManager.InitializeCampaignBehaviors(campaignGameStarter.CampaignBehaviors);
  				_campaignBehaviorManager.LoadBehaviorData();
  				_campaignBehaviorManager.RegisterEvents();
  			}
  			foreach (INonReadyObjectHandler campaignBehavior in GetCampaignBehaviors<INonReadyObjectHandler>())
  			{
  				campaignBehavior.OnBeforeNonReadyObjectsDeleted();
  			}
  			if (_gameLoadingType != GameLoadingType.Tutorial)
  			{
  				campaignGameStarter.UnregisterNonReadyObjects();
  			}
  			if (_gameLoadingType == GameLoadingType.SavedCampaign)
  			{
  				InitializeCampaignObjectsOnAfterLoad();
  			}
  			else if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.Tutorial)
  			{
  				CampaignObjectManager.InitializeOnNewGame();
  			}
  			InitializeCachedLists();
  			InitializeDefaultEquipments();
  			NameGenerator.Initialize();
> 			base.CurrentGame.OnGameStart();
> 			base.GameManager.OnGameInitializationFinished(base.CurrentGame);
  		}
  
  		private void CalculateCachedStatsOnLoad()
  		{
  			ItemRoster.CalculateCachedStatsOnLoad();
  		}
  
  		private void InitializeBasicObjectXmls()
  		{
  			base.ObjectManager.LoadXML("SPCultures");

codex
The loading state calls `DoLoadingForGameManager()` from `GameLoadingState.OnTick`, i.e. the normal game-state tick path, and `MBGameManager` synchronously iterates submodules. I don’t see evidence of concurrent `OnGameInitializationFinished` entry; the static bool is nevertheless the right lifetime because the engine keeps one submodule instance per module in `Module._subModuleBases`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd "'$dll | Select-String -Pattern '"'base\\.CurrentGame\\.OnGameStart\\(\\);' -Context 120,5 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.Module "'$dll | Select-String -Pattern '"'_subModuleBases =|_subModuleBases.Add|CollectSubModules|constructor.Invoke|OnSubModuleLoad|FinalizeSubModulesBases' -Context 3,6 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/BannerColorPersistence/Hooks/Banner_TryGetBannerDataFromCode_Transpiler.cs','Main/Features/BannerColorPersistence/Hooks/CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs','Main/Features/CastleRecruitment/Hooks/Patch42_AiHourlyTick_Transpiler.cs','Main/Features/CastleRecruitment/Hooks/Patch42_FillSettlements_Transpiler.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 482ms:
    1: using System.Collections.Generic;
    2: using System.Reflection;
    3: using System.Reflection.Emit;
    4: using HarmonyLib;
    5: using TAOM.Core.Logging;
    6: using TaleWorlds.Core;
    7: 
    8: namespace TAOM.Features.BannerColorPersistence.Hooks;
    9: 
   10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
   11: public static class Banner_TryGetBannerDataFromCode_Transpiler
   12: {
   13:     private static IBannerColorConfigProvider? _configProvider;
   14:     private static IModLogger? _logger;
   15: 
   16:     public static void Initialize(IBannerColorConfigProvider configProvider, IModLogger logger)
   17:     {
   18:         _configProvider = configProvider;
   19:         _logger = logger;
   20:     }
   21: 
   22:     public static MethodBase TargetMethod() =>
   23:         AccessTools.Method(typeof(Banner), "TryGetBannerDataFromCode",
   24:             new[] { typeof(string), typeof(List<BannerData>).MakeByRefType() });
   25: 
   26:     [HarmonyTranspiler]
   27:     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
   28:     {
   29:         var list = new List<CodeInstruction>(instructions);
   30: 
   31:         // Find the RemoveRange callvirt
   32:         int removeRangeIdx = -1;
   33:         for (int i = 0; i < list.Count; i++)
   34:         {
   35:             if (list[i].opcode == OpCodes.Callvirt &&
   36:                 list[i].operand is MethodInfo mi &&
   37:                 mi.Name == "RemoveRange" &&
   38:                 mi.DeclaringType == typeof(List<BannerData>))
   39:             {
   40:                 removeRangeIdx = i;
   41:                 break;
   42:             }
   43:         }
   44: 
   45:         if (removeRangeIdx < 0)
   46:         {
   47:             _logger?.LogWarning("[BannerColor] Transpiler: RemoveRange not found — layer limit patch not applied");
   48:             return list;
   49:         }
   50: 
   51:         // Find the Ble_S (or Ble) guard immediately before RemoveRange
   52:         int bleIdx = -1;
   53:         for (int i = removeRangeIdx - 1; i >= System.Math.Max(0, removeRangeIdx - 15); i--)
   54:         {
   55:             if (list[i].opcode == OpCodes.Ble_S || list[i].opcode == OpCodes.Ble)
   56:             {
   57:                 bleIdx = i;
   58:                 break;
   59:             }
   60:         }
   61: 
   62:         if (bleIdx < 0)
   63:         {
   64:             _logger?.LogWarning("[BannerColor] Transpiler: Ble_S guard not found — layer limit patch not applied");
   65:             return list;
   66:         }
   67: 
   68:         // Insert AFTER the Ble_S — at that point the stack is empty (ble consumed both operands).
   69:         // Inserting before would corrupt the stack: [Count][32] are already pushed for the ble comparison.
   70:         var skipTarget = list[bleIdx].operand; // the Label the Ble_S jumps to when count <= 32
   71: 
   72:         var callHelper = new CodeInstruction(OpCodes.Call,
   73:             AccessTools.Method(typeof(Banner_TryGetBannerDataFromCode_Transpiler), nameof(ShouldSkipLayerLimit)));
   74:         var brTrue = new CodeInstruction(OpCodes.Brtrue_S, skipTarget);
   75: 
   76:         list.Insert(bleIdx + 1, callHelper);
   77:         list.Insert(bleIdx + 2, brTrue);
   78: 
   79:         _logger?.LogDebug("[BannerColor] Transpiler: Banner layer limit patch applied");
   80:         return list;
   81:     }
   82: 
   83:     public static bool ShouldSkipLayerLimit() =>
   84:         _configProvider?.GetConfig().EnableLayerLimitTranspiler ?? false;
   85: }
   86: using System.Collections.Generic;
   87: using System.Reflection;
   88: using System.Reflection.Emit;
   89: using HarmonyLib;
   90: using TaleWorlds.CampaignSystem;
   91: using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
   92: 
   93: namespace TAOM.Features.BannerColorPersistence.Hooks;
   94: 
   95: [HarmonyPatch(typeof(CampaignSceneNotificationHelper),
   96:     nameof(CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero))]
   97: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
   98: public static class CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler
   99: {
  100:     private static IBannerColorService? _service;
  101: 
  102:     public static void Initialize(IBannerColorService service) => _service = service;
  103: 
  104:     [HarmonyTranspiler]
  105:     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  106:     {
  107:         var list = new List<CodeInstruction>(instructions);
  108:         var getMapFaction = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MapFaction));
  109:         var helperMethod = AccessTools.Method(
  110:             typeof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler),
  111:             nameof(GetFactionOrClanFaction));
  112: 
  113:         int replacements = 0;
  114:         for (int i = 0; i < list.Count; i++)
  115:         {
  116:             if (list[i].opcode == OpCodes.Callvirt &&
  117:                 list[i].operand is MethodInfo mi &&
  118:                 mi == getMapFaction)
  119:             {
  120:                 list[i] = new CodeInstruction(OpCodes.Call, helperMethod);
  121:                 replacements++;
  122:             }
  123:         }
  124: 
  125:         return list;
  126:     }
  127: 
  128:     public static IFaction? GetFactionOrClanFaction(Hero hero)
  129:     {
  130:         if (_service?.IsEnabled() ?? false)
  131:         {
  132:             var clan = hero?.Clan;
  133:             if (clan != null)
  134:                 return clan;
  135:         }
  136: 
  137:         return hero?.MapFaction;
  138:     }
  139: }
  140: using System.Collections.Generic;
  141: using System.Reflection;
  142: using HarmonyLib;
  143: using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
  144: using TAOM.Core.Logging;
  145: 
  146: namespace TAOM.Features.CastleRecruitment.Hooks;
  147: 
  148: /// <summary>
  149: /// Patch42 (T3) — Transpiler on private <c>AiVisitSettlementBehavior.AiHourlyTick</c>. Neutralises the
  150: /// <c>!settlement.IsCastle</c> term in the recruitment-desirability gate
  151: /// (<c>if (!settlement.IsCastle &amp;&amp; item &lt; 1f &amp;&amp; mobileParty.GetAvailableWageBudget() &gt; 0)</c>)
  152: /// so AI lord parties SCORE castles for recruitment and therefore travel to them. Gated at runtime by
  153: /// <see cref="CastleAiToggle"/> (the swapped call returns the real IsCastle when AI castle recruitment
  154: /// is off). Disambiguated by the <c>GetAvailableWageBudget</c> call that follows the IsCastle check.
  155: /// </summary>
  156: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
  157: public static class Patch42_AiHourlyTick_Transpiler
  158: {
  159:     private static IModLogger? _logger;
  160: 
  161:     public static void Initialize(IModLogger logger) => _logger = logger;
  162: 
  163:     public static MethodBase TargetMethod() => AccessTools.Method(typeof(AiVisitSettlementBehavior), "AiHourlyTick");
  164: 
  165:     [HarmonyTranspiler]
  166:     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
  167:         CastleAiTranspiler.SwapIsCastleGate(instructions, "GetAvailableWageBudget", "AiHourlyTick", _logger);
  168: }
  169: using System.Collections.Generic;
  170: using System.Reflection;
  171: using HarmonyLib;
  172: using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
  173: using TAOM.Core.Logging;
  174: 
  175: namespace TAOM.Features.CastleRecruitment.Hooks;
  176: 
  177: /// <summary>
  178: /// Patch42 (T4) — Transpiler on private static
  179: /// <c>AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays</c>. Neutralises the
  180: /// <c>!settlement.IsCastle</c> term in the non-owned-settlement targeting gate (line ~696) so AI also
  181: /// considers allied/neutral castles as recruitment-visit candidates (own-faction castles are already
  182: /// iterated unconditionally). War-state filtering in <c>IsSettlementSuitableForVisitingCondition</c>
  183: /// still applies. Applied together with the AiHourlyTick transpiler for full town-parity travel.
  184: /// Disambiguated by the <c>IsSettlementSuitableForVisitingCondition</c> call after the IsCastle check.
  185: /// </summary>
  186: [HarmonyPatchCategory("Patch42_CastleRecruitment")]
  187: public static class Patch42_FillSettlements_Transpiler
  188: {
  189:     private static IModLogger? _logger;
  190: 
  191:     public static void Initialize(IModLogger logger) => _logger = logger;
  192: 
  193:     public static MethodBase TargetMethod() =>
  194:         AccessTools.Method(typeof(AiVisitSettlementBehavior), "FillSettlementsToVisitWithDistancesAsDays");
  195: 
  196:     [HarmonyTranspiler]
  197:     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
  198:         CastleAiTranspiler.SwapIsCastleGate(instructions, "IsSettlementSuitableForVisitingCondition", "FillSettlements", _logger);
  199: }

 succeeded in 1453ms:
  		MBDebug.Print("Creating module...");
  		StartupInfo = new GameStartupInfo();
  		_testContext = new TestContext();
> 		_subModuleBases = new Dictionary<SubModuleInfo, MBSubModuleBase>();
  		GlobalGameStateManager = new GameStateManager(this, GameStateManager.GameStateManagerType.Global);
  		GameStateManager.Current = GlobalGameStateManager;
  		GlobalTextManager = new GameTextManager();
  		JobManager = new JobManager();
  	}
  
> 	public MBReadOnlyList<MBSubModuleBase> CollectSubModules()
  	{
  		MBList<MBSubModuleBase> mBList = new MBList<MBSubModuleBase>();
  		foreach (ModuleInfo allModule in ModuleHelper.GetAllModules())
  		{
  			if (!allModule.IsActive)
  			{
  		if (assemblyLoadResult == AssemblyLoader.AssemblyLoadResult.Success)
  		{
  			Managed.AddTypes(types);
> 			MBSubModuleBase value = (MBSubModuleBase)constructor.Invoke(new object[0]);
> 			_subModuleBases.Add(subModuleInfo, value);
  		}
  		return assemblyLoadResult;
  	}
  
  	private AssemblyLoader.AssemblyLoadResult CollectModuleAssemblyTypes(SubModuleInfo subModule, Assembly moduleAssembly, out Dictionary<string, Type> types)
  	{
  		Managed.AddConstructorDelegateOfClass<SpawnedItemEntity>();
  		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
> 			value.OnSubModuleLoad();
  		}
  	}
  
  	private void OnNewModuleLoaded()
  	{
  		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		return null;
  	}
  
> 	private void FinalizeSubModulesBases()
  	{
  		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnSubModuleUnloaded();
  		}
  	}
  		{
  			GameManagerBase.Current.OnTick(dt);
  		}
> 		foreach (MBSubModuleBase item in CollectSubModules())
  		{
  			item.OnApplicationTick(dt);
  		}
  		JobManager.OnTick(dt);
  		AvatarServices.UpdateAvatarServices(dt);
  	}
  
  	private void OnNetworkTick(float dt)
  	{
> 		foreach (MBSubModuleBase item in CollectSubModules())
  		{
  			item.OnNetworkTick(dt);
  		}
  	}
  
  	[MBCallback(null, false)]
  				MBSubModuleBase value = null;
  				if (_subModuleBases.TryGetValue(item, out value))
  				{
> 					value.OnSubModuleLoad();
  				}
  			}
  			OnNewModuleLoaded();
  		}
  		else
  		{
  		InformationManager.Clear();
  		ScreenManager.OnFinalize();
  		BannerlordConfig.Save();
> 		FinalizeSubModulesBases();
  		PlatformServices.Instance?.Terminate();
  		Common.MemoryCleanupGC();
  		GC.WaitForPendingFinalizers();
  	}
  
  	internal static void FinalizeCurrentModule()

 succeeded in 21997ms:
  			{
  				string text = ModuleHelper.GetModuleFullPath(activeModule.Id) + "ModuleData/";
  				string path = text + "sp_battle_scenes.xml";
  				string path2 = text + "conversation_scenes.xml";
  				string path3 = text + "meeting_scenes.xml";
  				if (File.Exists(path))
  				{
  					GameSceneDataManager.Instance.LoadSPBattleScenes(path);
  				}
  				if (File.Exists(path2))
  				{
  					GameSceneDataManager.Instance.LoadConversationScenes(path2);
  				}
  				if (File.Exists(path3))
  				{
  					GameSceneDataManager.Instance.LoadMeetingScenes(path3);
  				}
  			}
  		}
  
  		public void SetLoadingParameters(GameLoadingType gameLoadingType)
  		{
  			Current = this;
  			_gameLoadingType = gameLoadingType;
  			if (gameLoadingType == GameLoadingType.SavedCampaign)
  			{
  				Current.GameStarted = true;
  			}
  		}
  
  		public void AddCampaignEventReceiver(CampaignEventReceiver receiver)
  		{
  			CampaignEventDispatcher.AddCampaignEventReceiver(receiver);
  		}
  
  		protected override void OnInitialize()
  		{
  			CampaignEvents = new CampaignEvents();
  			CustomPeriodicCampaignEvents = new List<MBCampaignEvent>();
  			CampaignEventDispatcher = new CampaignEventDispatcher(new CampaignEventReceiver[3] { CampaignEvents, IssueManager, QuestManager });
  			SandBoxManager = Game.Current.AddGameHandler<SandBoxManager>();
  			SaveHandler = new SaveHandler();
  			VisualCreator = new VisualCreator();
  			GameMenuManager = new GameMenuManager();
  			_towns = new MBList<Town>();
  			_castles = new MBList<Town>();
  			_villages = new MBList<Village>();
  			_hideouts = new MBList<Hideout>();
  			if (_gameLoadingType != GameLoadingType.Editor)
  			{
  				CreateManagers();
  			}
  			CampaignGameStarter campaignGameStarter = new CampaignGameStarter(GameMenuManager, ConversationManager);
  			SandBoxManager.Initialize(campaignGameStarter);
  			base.GameManager.InitializeGameStarter(base.CurrentGame, campaignGameStarter);
  			GameSceneDataManager.Initialize();
  			if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.SavedCampaign)
  			{
  				InitializeScenes();
  			}
  			base.GameManager.OnGameStart(base.CurrentGame, campaignGameStarter);
  			base.CurrentGame.SetBasicModels(campaignGameStarter.Models);
  			_gameModels = base.CurrentGame.AddGameModelsManager<GameModels>(campaignGameStarter.Models);
  			CampaignTime.Initialize();
  			base.CurrentGame.CreateGameManager();
  			if (_gameLoadingType == GameLoadingType.SavedCampaign)
  			{
  				InitializeDefaultCampaignObjects();
  			}
  			else
  			{
  				MapTimeTracker = new MapTimeTracker(Models.CampaignTimeModel.CampaignStartTime);
  			}
  			base.GameManager.BeginGameStart(base.CurrentGame);
  			if (_gameLoadingType != GameLoadingType.SavedCampaign)
  			{
  				OnNewCampaignStart();
  			}
  			CreateLists();
  			InitializeBasicObjectXmls();
  			if (_gameLoadingType != GameLoadingType.SavedCampaign)
  			{
  				base.GameManager.OnNewCampaignStart(base.CurrentGame, campaignGameStarter);
  			}
  			SandBoxManager.OnCampaignStart(campaignGameStarter, base.GameManager, _gameLoadingType == GameLoadingType.SavedCampaign);
  			if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.SavedCampaign)
  			{
  				DetermineSavedStats(_gameLoadingType);
  			}
  			if (_gameLoadingType != GameLoadingType.SavedCampaign)
  			{
  				AddCampaignBehaviorManager(new CampaignBehaviorManager(campaignGameStarter.CampaignBehaviors));
  				base.GameManager.OnAfterCampaignStart(base.CurrentGame);
  			}
  			else
  			{
  				base.GameManager.OnGameLoaded(base.CurrentGame, campaignGameStarter);
  				_campaignBehaviorManager.InitializeCampaignBehaviors(campaignGameStarter.CampaignBehaviors);
  				_campaignBehaviorManager.LoadBehaviorData();
  				_campaignBehaviorManager.RegisterEvents();
  			}
  			foreach (INonReadyObjectHandler campaignBehavior in GetCampaignBehaviors<INonReadyObjectHandler>())
  			{
  				campaignBehavior.OnBeforeNonReadyObjectsDeleted();
  			}
  			if (_gameLoadingType != GameLoadingType.Tutorial)
  			{
  				campaignGameStarter.UnregisterNonReadyObjects();
  			}
  			if (_gameLoadingType == GameLoadingType.SavedCampaign)
  			{
  				InitializeCampaignObjectsOnAfterLoad();
  			}
  			else if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.Tutorial)
  			{
  				CampaignObjectManager.InitializeOnNewGame();
  			}
  			InitializeCachedLists();
  			InitializeDefaultEquipments();
  			NameGenerator.Initialize();
> 			base.CurrentGame.OnGameStart();
  			base.GameManager.OnGameInitializationFinished(base.CurrentGame);
  		}
  
  		private void CalculateCachedStatsOnLoad()
  		{

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/BannerColorPersistence/Hooks/Banner_TryGetBannerDataFromCode_Transpiler.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/BannerColorPersistence/Hooks/CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 465ms:
    1: using System.Collections.Generic;
    2: using System.Reflection;
    3: using System.Reflection.Emit;
    4: using HarmonyLib;
    5: using TaleWorlds.CampaignSystem;
    6: using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
    7: 
    8: namespace TAOM.Features.BannerColorPersistence.Hooks;
    9: 
   10: [HarmonyPatch(typeof(CampaignSceneNotificationHelper),
   11:     nameof(CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero))]
   12: [HarmonyPatchCategory("Patch23_BannerColorPersistence")]
   13: public static class CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler
   14: {
   15:     private static IBannerColorService? _service;
   16: 
   17:     public static void Initialize(IBannerColorService service) => _service = service;
   18: 
   19:     [HarmonyTranspiler]
   20:     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
   21:     {
   22:         var list = new List<CodeInstruction>(instructions);
   23:         var getMapFaction = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MapFaction));
   24:         var helperMethod = AccessTools.Method(
   25:             typeof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler),
   26:             nameof(GetFactionOrClanFaction));
   27: 
   28:         int replacements = 0;
   29:         for (int i = 0; i < list.Count; i++)
   30:         {
   31:             if (list[i].opcode == OpCodes.Callvirt &&
   32:                 list[i].operand is MethodInfo mi &&
   33:                 mi == getMapFaction)
   34:             {
   35:                 list[i] = new CodeInstruction(OpCodes.Call, helperMethod);
   36:                 replacements++;
   37:             }
   38:         }
   39: 
   40:         return list;
   41:     }
   42: 
   43:     public static IFaction? GetFactionOrClanFaction(Hero hero)
   44:     {
   45:         if (_service?.IsEnabled() ?? false)
   46:         {
   47:             var clan = hero?.Clan;
   48:             if (clan != null)
   49:                 return clan;
   50:         }
   51: 
   52:         return hero?.MapFaction;
   53:     }
   54: }

 succeeded in 494ms:
    1: using System.Collections.Generic;
    2: using System.Reflection;
    3: using System.Reflection.Emit;
    4: using HarmonyLib;
    5: using TAOM.Core.Logging;
    6: using TaleWorlds.Core;
    7: 
    8: namespace TAOM.Features.BannerColorPersistence.Hooks;
    9: 
   10: [HarmonyPatchCategory("Patch15_BannerLayerLimit")]
   11: public static class Banner_TryGetBannerDataFromCode_Transpiler
   12: {
   13:     private static IBannerColorConfigProvider? _configProvider;
   14:     private static IModLogger? _logger;
   15: 
   16:     public static void Initialize(IBannerColorConfigProvider configProvider, IModLogger logger)
   17:     {
   18:         _configProvider = configProvider;
   19:         _logger = logger;
   20:     }
   21: 
   22:     public static MethodBase TargetMethod() =>
   23:         AccessTools.Method(typeof(Banner), "TryGetBannerDataFromCode",
   24:             new[] { typeof(string), typeof(List<BannerData>).MakeByRefType() });
   25: 
   26:     [HarmonyTranspiler]
   27:     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
   28:     {
   29:         var list = new List<CodeInstruction>(instructions);
   30: 
   31:         // Find the RemoveRange callvirt
   32:         int removeRangeIdx = -1;
   33:         for (int i = 0; i < list.Count; i++)
   34:         {
   35:             if (list[i].opcode == OpCodes.Callvirt &&
   36:                 list[i].operand is MethodInfo mi &&
   37:                 mi.Name == "RemoveRange" &&
   38:                 mi.DeclaringType == typeof(List<BannerData>))
   39:             {
   40:                 removeRangeIdx = i;
   41:                 break;
   42:             }
   43:         }
   44: 
   45:         if (removeRangeIdx < 0)
   46:         {
   47:             _logger?.LogWarning("[BannerColor] Transpiler: RemoveRange not found — layer limit patch not applied");
   48:             return list;
   49:         }
   50: 
   51:         // Find the Ble_S (or Ble) guard immediately before RemoveRange
   52:         int bleIdx = -1;
   53:         for (int i = removeRangeIdx - 1; i >= System.Math.Max(0, removeRangeIdx - 15); i--)
   54:         {
   55:             if (list[i].opcode == OpCodes.Ble_S || list[i].opcode == OpCodes.Ble)
   56:             {
   57:                 bleIdx = i;
   58:                 break;
   59:             }
   60:         }
   61: 
   62:         if (bleIdx < 0)
   63:         {
   64:             _logger?.LogWarning("[BannerColor] Transpiler: Ble_S guard not found — layer limit patch not applied");
   65:             return list;
   66:         }
   67: 
   68:         // Insert AFTER the Ble_S — at that point the stack is empty (ble consumed both operands).
   69:         // Inserting before would corrupt the stack: [Count][32] are already pushed for the ble comparison.
   70:         var skipTarget = list[bleIdx].operand; // the Label the Ble_S jumps to when count <= 32
   71: 
   72:         var callHelper = new CodeInstruction(OpCodes.Call,
   73:             AccessTools.Method(typeof(Banner_TryGetBannerDataFromCode_Transpiler), nameof(ShouldSkipLayerLimit)));
   74:         var brTrue = new CodeInstruction(OpCodes.Brtrue_S, skipTarget);
   75: 
   76:         list.Insert(bleIdx + 1, callHelper);
   77:         list.Insert(bleIdx + 2, brTrue);
   78: 
   79:         _logger?.LogDebug("[BannerColor] Transpiler: Banner layer limit patch applied");
   80:         return list;
   81:     }
   82: 
   83:     public static bool ShouldSkipLayerLimit() =>
   84:         _configProvider?.GetConfig().EnableLayerLimitTranspiler ?? false;
   85: }

 succeeded in 527ms:
    1: using System;
    2: using System.Collections.Generic;
    3: using System.Reflection;
    4: using System.Reflection.Emit;
    5: using HarmonyLib;
    6: using TaleWorlds.CampaignSystem.Settlements;
    7: using TAOM.Core.Logging;
    8: 
    9: namespace TAOM.Features.CastleRecruitment.Hooks;
   10: 
   11: /// <summary>
   12: /// Shared IL surgery for the two AI-scoring transpilers. Swaps the recruitment-gate <c>get_IsCastle</c>
   13: /// for a static call to <see cref="CastleAiToggle"/>.
   14: ///
   15: /// Targets the FIRST <c>get_IsCastle</c> in the method — in both target methods the recruitment gate is
   16: /// the first (AiHourlyTick: line ~269 recruit gate vs. the later line ~317 reform-score gate;
   17: /// FillSettlements: a single get_IsCastle) — and additionally requires a uniquely-named anchor method
   18: /// to appear shortly after it. This double check means a future engine refactor that reorders or
   19: /// renames the gate makes the patch FAIL-SAFE (log + return the original stream, AI keeps vanilla
   20: /// behaviour) rather than swap the wrong IsCastle. We never search past the first get_IsCastle, so the
   21: /// later reform-score gate can never be hit by accident.
   22: /// </summary>
   23: internal static class CastleAiTranspiler
   24: {
   25:     private const int AnchorWindow = 24;
   26: 
   27:     internal static List<CodeInstruction> SwapIsCastleGate(
   28:         IEnumerable<CodeInstruction> instructions, string anchorMethodName, string label, IModLogger? logger)
   29:     {
   30:         var list = new List<CodeInstruction>(instructions);
   31:         var getIsCastle = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.IsCastle));
   32:         var replacement = AccessTools.Method(typeof(CastleAiToggle), nameof(CastleAiToggle.IsCastleAndAiDisabled));
   33: 
   34:         if (getIsCastle == null || replacement == null)
   35:         {
   36:             logger?.LogWarning($"[CastleRecruitment] {label}: reflection lookup failed — AI castle gate not patched (vanilla behaviour preserved)");
   37:             return list;
   38:         }
   39: 
   40:         int firstIsCastle = -1;
   41:         for (int i = 0; i < list.Count; i++)
   42:         {
   43:             if (IsGetIsCastle(list[i], getIsCastle))
   44:             {
   45:                 firstIsCastle = i;
   46:                 break;
   47:             }
   48:         }
   49: 
   50:         if (firstIsCastle < 0)
   51:         {
   52:             logger?.LogWarning($"[CastleRecruitment] {label}: no get_IsCastle found — AI castle gate not patched (vanilla behaviour preserved)");
   53:             return list;
   54:         }
   55: 
   56:         // Confirm the FIRST get_IsCastle is the recruitment gate by requiring the expected anchor
   57:         // method shortly after it. If absent, bail — never fall through to a different get_IsCastle.
   58:         bool anchored = false;
   59:         int end = Math.Min(list.Count, firstIsCastle + 1 + AnchorWindow);
   60:         for (int j = firstIsCastle + 1; j < end; j++)
   61:         {
   62:             if (IsCallNamed(list[j], anchorMethodName))
   63:             {
   64:                 anchored = true;
   65:                 break;
   66:             }
   67:         }
   68: 
   69:         if (!anchored)
   70:         {
   71:             logger?.LogWarning($"[CastleRecruitment] {label}: first get_IsCastle not followed by anchor '{anchorMethodName}' — AI castle gate not patched (vanilla behaviour preserved)");
   72:             return list;
   73:         }
   74: 
   75:         // Same stack shape: callvirt instance get_IsCastle(Settlement)->bool  becomes
   76:         // call static IsCastleAndAiDisabled(Settlement)->bool. Labels remain on the same object.
   77:         list[firstIsCastle].opcode = OpCodes.Call;
   78:         list[firstIsCastle].operand = replacement;
   79:         logger?.LogDebug($"[CastleRecruitment] {label}: IsCastle gate swapped at instruction {firstIsCastle}");
   80:         return list;
   81:     }
   82: 
   83:     private static bool IsGetIsCastle(CodeInstruction ci, MethodInfo getIsCastle) =>
   84:         (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
   85:         && ci.operand is MethodInfo mi
   86:         && mi.Name == getIsCastle.Name
   87:         && mi.DeclaringType == getIsCastle.DeclaringType;
   88: 
   89:     private static bool IsCallNamed(CodeInstruction ci, string name) =>
   90:         (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
   91:         && ci.operand is MethodInfo mi
   92:         && mi.Name == name;
   93: }

codex
The other transpilers all return the current instruction list on pattern mismatch and do not throw on anchor miss. The RaceAge patch’s soft-fail is still worth verifying against the installed `HeroCreator.DeliverOffSpring` target because that is where the original crash surfaced.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd --type TaleWorlds.CampaignSystem.HeroCreator "'$dll | Select-String -Pattern '"'DeliverOffSpring|SilentAssert|Race|mother|father' -Context 6,10 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.MountAndBlade.dll'; ilspycmd --type TaleWorlds.MountAndBlade.Module "'$dll | Select-String -Pattern '"'private Module\\(|_subModuleBases =|constructor.Invoke|_subModuleBases.Add|CollectSubModules\\(|FinalizeSubModulesBases\\(' -Context 6,8 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; ilspycmd --type TaleWorlds.CampaignSystem.Campaign "'$dll | Select-String -Pattern '"'protected override void OnInitialize|OnGameInitializationFinished|OnGameStart' -Context 8,10 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1075ms:
  		public Hero Hero { get; }
  
  		public TextObject Name { get; private set; }
  
  		public TextObject FirstName { get; private set; }
  
> 		public Hero Mother { get; private set; }
  
> 		public Hero Father { get; private set; }
  
  		public bool IsFemale { get; private set; }
  
  		public Settlement BornSettlement { get; private set; }
  
  		public int Level { get; private set; }
  
  		public float Weight { get; private set; }
  
  		public float Build { get; private set; }
  		{
  			DynamicBodyProperties dynamicBodyPropertiesBetweenMinMaxRange = CharacterHelper.GetDynamicBodyPropertiesBetweenMinMaxRange(hero.CharacterObject);
  			Hero = hero;
  			IsOffspring = isOffspring;
  			Name = hero.Name;
  			FirstName = hero.FirstName;
> 			Mother = hero.Mother;
> 			Father = hero.Father;
  			IsFemale = hero.IsFemale;
  			BornSettlement = null;
  			Level = hero.Level;
  			Weight = dynamicBodyPropertiesBetweenMinMaxRange.Weight;
  			Build = dynamicBodyPropertiesBetweenMinMaxRange.Build;
  			StaticBodyProperties = null;
  			PreferredUpgradeFormation = null;
  			Clan = null;
  			SupporterOf = hero.SupporterOf;
  			Occupation = hero.Occupation;
  		public HeroInitializationArgs SetFirstName(TextObject firstName)
  		{
  			FirstName = firstName;
  			return this;
  		}
  
> 		public HeroInitializationArgs SetMother(Hero mother)
  		{
> 			Mother = mother;
  			return this;
  		}
  
> 		public HeroInitializationArgs SetFather(Hero father)
  		{
> 			Father = father;
  			return this;
  		}
  
  		public HeroInitializationArgs SetIsFemale(bool isFemale)
  		{
  			IsFemale = isFemale;
  			return this;
  		}
  
  		public HeroInitializationArgs SetBornSettlement(Settlement bornSettlement)
  		CampaignTime item2 = birthAndDeathDay.deathDay;
  		Hero hero = CreateHero(randomTemplateByOccupation, useCharacterAsTemplate: true, item, item2);
  		BodyProperties bodyPropertiesMin = relative.CharacterObject.GetBodyPropertiesMin();
  		BodyProperties bodyPropertiesMin2 = randomTemplateByOccupation.GetBodyPropertiesMin();
  		int defaultFaceSeed = relative.CharacterObject.GetDefaultFaceSeed(1);
  		MBBodyProperty bodyPropertyRange = hero.CharacterObject.BodyPropertyRange;
> 		BodyProperties randomBodyProperties = BodyProperties.GetRandomBodyProperties(randomTemplateByOccupation.Race, randomTemplateByOccupation.IsFemale, bodyPropertiesMin, bodyPropertiesMin2, 1, defaultFaceSeed, bodyPropertyRange.HairTags, bodyPropertyRange.BeardTags, bodyPropertyRange.TattooTags);
  		HeroInitializationArgs heroInitializationArgs = new HeroInitializationArgs(hero, isOffspring: false).SetBornSettlement(relative.HomeSettlement).SetCulture(relative.Culture).SetAppearance(randomBodyProperties.StaticProperties)
  			.SetGenerateFirstAndFullName(value: true);
  		InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
  		return hero;
  	}
  
  	public static bool CreateBasicHero(string stringId, CharacterObject character, out Hero hero, bool isAlive = true)
  	{
  		hero = Campaign.Current.CampaignObjectManager.Find<Hero>(stringId);
  		if (hero == null)
  			InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
  			return true;
  		}
  		return false;
  	}
  
> 	public static Hero DeliverOffSpring(Hero mother, Hero father, bool isOffspringFemale)
  	{
> 		Debug.SilentAssert(mother.CharacterObject.Race == father.CharacterObject.Race, "", getDump: false, "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\HeroCreator.cs", "DeliverOffSpring", 272);
> 		CharacterObject characterTemplateForOffspring = Campaign.Current.Models.HeroCreationModel.GetCharacterTemplateForOffspring(mother, father, isOffspringFemale);
  		(CampaignTime birthDay, CampaignTime deathDay) birthAndDeathDay = Campaign.Current.Models.HeroCreationModel.GetBirthAndDeathDay(characterTemplateForOffspring, createAlive: true, 0);
  		CampaignTime item = birthAndDeathDay.birthDay;
  		CampaignTime item2 = birthAndDeathDay.deathDay;
  		Hero hero = CreateHero(characterTemplateForOffspring, useCharacterAsTemplate: true, item, item2);
> 		HeroInitializationArgs heroInitializationArgs = new HeroInitializationArgs(hero, isOffspring: true).SetMother(mother).SetFather(father).SetIsFemale(isOffspringFemale)
> 			.SetOccupation(isOffspringFemale ? mother.Occupation : father.Occupation)
  			.SetLevel(1)
  			.SetGenerateFirstAndFullName(value: true);
> 		if (mother == Hero.MainHero || father == Hero.MainHero)
  		{
  			heroInitializationArgs.SetClan(Hero.MainHero.Clan).SetCulture(Hero.MainHero.Culture);
  		}
  		else
  		{
> 			CultureObject culture = ((MBRandom.RandomFloat < 0.5f) ? father.Culture : mother.Culture);
> 			heroInitializationArgs.SetClan(father.Clan).SetCulture(culture);
  		}
  		InitializeHeroFromSettings(heroInitializationArgs.Hero, heroInitializationArgs);
  		return hero;
  	}
  
  	private static Hero CreateHero(CharacterObject character, bool useCharacterAsTemplate, CampaignTime birthDay, CampaignTime deathDay)
  	{
  		if (useCharacterAsTemplate)
  		{
  			Debug.Print("creating hero from template with id: " + character.StringId);
  		}
  		return new Hero(character.StringId, character, birthDay, deathDay);
  	}
  
  	private static void InitializeHeroFromSettings(Hero hero, HeroInitializationArgs initializationArgs)
  	{
> 		hero.Mother = initializationArgs.Mother;
> 		hero.Father = initializationArgs.Father;
  		hero.IsFemale = initializationArgs.IsFemale;
  		hero.BornSettlement = (initializationArgs.HasBornSettlementBeenSet ? initializationArgs.BornSettlement : Campaign.Current.Models.HeroCreationModel.GetBornSettlement(hero));
  		hero.PreferredUpgradeFormation = initializationArgs.PreferredUpgradeFormation ?? Campaign.Current.Models.HeroCreationModel.GetPreferredUpgradeFormation(hero);
  		hero.Clan = (initializationArgs.HasClanBeenSet ? initializationArgs.Clan : Campaign.Current.Models.HeroCreationModel.GetClan(hero));
  		hero.Culture = initializationArgs.Culture ?? Campaign.Current.Models.HeroCreationModel.GetCulture(hero, hero.BornSettlement, hero.Clan);
  		hero.StaticBodyProperties = initializationArgs.StaticBodyProperties ?? Campaign.Current.Models.HeroCreationModel.GetStaticBodyProperties(hero, initializationArgs.IsOffspring);
  		hero.SupporterOf = initializationArgs.SupporterOf;
  		hero.Level = initializationArgs.Level;
  		hero.Weight = initializationArgs.Weight;
  		hero.Build = initializationArgs.Build;

 succeeded in 1501ms:
  	public GameStartupInfo StartupInfo { get; private set; }
  
  	public event Action SkinsXMLHasChanged;
  
  	public event Action ImguiProfilerTick;
  
> 	private Module()
  	{
  		MBDebug.Print("Creating module...");
  		StartupInfo = new GameStartupInfo();
  		_testContext = new TestContext();
> 		_subModuleBases = new Dictionary<SubModuleInfo, MBSubModuleBase>();
  		GlobalGameStateManager = new GameStateManager(this, GameStateManager.GameStateManagerType.Global);
  		GameStateManager.Current = GlobalGameStateManager;
  		GlobalTextManager = new GameTextManager();
  		JobManager = new JobManager();
  	}
  
> 	public MBReadOnlyList<MBSubModuleBase> CollectSubModules()
  	{
  		MBList<MBSubModuleBase> mBList = new MBList<MBSubModuleBase>();
  		foreach (ModuleInfo allModule in ModuleHelper.GetAllModules())
  		{
  			if (!allModule.IsActive)
  			{
  				continue;
  			}
  		ConstructorInfo constructor = subModuleAssembly.GetType(subModuleInfo.SubModuleClassTypeName).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, new Type[0], null);
  		Dictionary<string, Type> types;
  		AssemblyLoader.AssemblyLoadResult assemblyLoadResult = CollectModuleAssemblyTypes(subModuleInfo, subModuleAssembly, out types);
  		if (assemblyLoadResult == AssemblyLoader.AssemblyLoadResult.Success)
  		{
  			Managed.AddTypes(types);
> 			MBSubModuleBase value = (MBSubModuleBase)constructor.Invoke(new object[0]);
> 			_subModuleBases.Add(subModuleInfo, value);
  		}
  		return assemblyLoadResult;
  	}
  
  	private AssemblyLoader.AssemblyLoadResult CollectModuleAssemblyTypes(SubModuleInfo subModule, Assembly moduleAssembly, out Dictionary<string, Type> types)
  	{
  		try
  		{
  		{
  			return value;
  		}
  		return null;
  	}
  
> 	private void FinalizeSubModulesBases()
  	{
  		foreach (MBSubModuleBase value in _subModuleBases.Values)
  		{
  			value.OnSubModuleUnloaded();
  		}
  	}
  
  	[MBCallback(null, false)]
  		PlatformServices.Instance?.Tick(dt);
  		_synchronizationContext.Tick();
  		if (GameManagerBase.Current != null)
  		{
  			GameManagerBase.Current.OnTick(dt);
  		}
> 		foreach (MBSubModuleBase item in CollectSubModules())
  		{
  			item.OnApplicationTick(dt);
  		}
  		JobManager.OnTick(dt);
  		AvatarServices.UpdateAvatarServices(dt);
  	}
  
  	private void OnConfirmReturnToMainMenu()
  	{
  		MBGameManager.EndGame();
  	}
  
  	private void OnNetworkTick(float dt)
  	{
> 		foreach (MBSubModuleBase item in CollectSubModules())
  		{
  			item.OnNetworkTick(dt);
  		}
  	}
  
  	[MBCallback(null, false)]
  	internal void RunTest(string commandLine)
  	{
  		}
  		_testContext.FinalizeContext();
  		MBInformationManager.Clear();
  		InformationManager.Clear();
  		ScreenManager.OnFinalize();
  		BannerlordConfig.Save();
> 		FinalizeSubModulesBases();
  		PlatformServices.Instance?.Terminate();
  		Common.MemoryCleanupGC();
  		GC.WaitForPendingFinalizers();
  	}
  
  	internal static void FinalizeCurrentModule()
  	{
  		CurrentModule.FinalizeModule();

 succeeded in 1583ms:
  		}
  	}
  
  	public void AddCampaignEventReceiver(CampaignEventReceiver receiver)
  	{
  		CampaignEventDispatcher.AddCampaignEventReceiver(receiver);
  	}
  
> 	protected override void OnInitialize()
  	{
  		CampaignEvents = new CampaignEvents();
  		CustomPeriodicCampaignEvents = new List<MBCampaignEvent>();
  		CampaignEventDispatcher = new CampaignEventDispatcher(new CampaignEventReceiver[3] { CampaignEvents, IssueManager, QuestManager });
  		SandBoxManager = Game.Current.AddGameHandler<SandBoxManager>();
  		SaveHandler = new SaveHandler();
  		VisualCreator = new VisualCreator();
  		GameMenuManager = new GameMenuManager();
  		_towns = new MBList<Town>();
  		_castles = new MBList<Town>();
  		CampaignGameStarter campaignGameStarter = new CampaignGameStarter(GameMenuManager, ConversationManager);
  		SandBoxManager.Initialize(campaignGameStarter);
  		base.GameManager.InitializeGameStarter(base.CurrentGame, campaignGameStarter);
  		GameSceneDataManager.Initialize();
  		if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.SavedCampaign)
  		{
  			InitializeScenes();
  		}
> 		base.GameManager.OnGameStart(base.CurrentGame, campaignGameStarter);
  		base.CurrentGame.SetBasicModels(campaignGameStarter.Models);
  		_gameModels = base.CurrentGame.AddGameModelsManager<GameModels>(campaignGameStarter.Models);
  		CampaignTime.Initialize();
  		base.CurrentGame.CreateGameManager();
  		if (_gameLoadingType == GameLoadingType.SavedCampaign)
  		{
  			InitializeDefaultCampaignObjects();
  		}
  		else
  		{
  		}
  		else if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.Tutorial)
  		{
  			CampaignObjectManager.InitializeOnNewGame();
  		}
  		InitializeCachedLists();
  		InitializeDefaultEquipments();
  		NameGenerator.Initialize();
> 		base.CurrentGame.OnGameStart();
> 		base.GameManager.OnGameInitializationFinished(base.CurrentGame);
  	}
  
  	private void CalculateCachedStatsOnLoad()
  	{
  		ItemRoster.CalculateCachedStatsOnLoad();
  	}
  
  	private void InitializeBasicObjectXmls()
  	{
  		base.ObjectManager.LoadXML("SPCultures");

codex
The installed `HeroCreator.DeliverOffSpring` decompile confirms the exact target is a single `Debug.SilentAssert(mother.CharacterObject.Race == father.CharacterObject.Race, ...)` before the real offspring creation logic. Returning original IL on anchor miss only preserves vanilla assert noise; it does not alter birth behavior.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/SubModule.cs Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/SubModule.cs' | ForEach-Object { "'$i++; if (($i -ge 534 -and $i -le 698) -or ($i -ge 86 -and $i -le 90)) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs' | ForEach-Object { "'$i++; if ($i -ge 21 -and $i -le 119) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 376ms:
diff --git a/Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs b/Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs
index c67a4c90..b3847b05 100644
--- a/Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs
+++ b/Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs
@@ -1,5 +1,4 @@
 using HarmonyLib;
-using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Reflection;
@@ -7,6 +6,7 @@ using System.Reflection.Emit;
 using TaleWorlds.CampaignSystem;
 using TaleWorlds.Core;
 using TaleWorlds.Library;
+using TAOM.Core.Logging;
 
 namespace TAOM.Features.RaceAge.Hooks;
 
@@ -43,8 +43,13 @@ public static class DeliverOffSpring_RaceAssert_Patch
         }
 
         if (callIndex < 0)
-            throw new ArgumentException(
-                "Cannot find Debug.SilentAssert call in DeliverOffSpring IL. Patch: DeliverOffSpring_RaceAssert_Patch");
+        {
+            // Anchor gone (already NOPped by a prior application of this transpiler, or the engine IL
+            // changed). This patch is pure noise-reduction, so degrade to a no-op instead of throwing
+            // out of PatchCategory and crashing the mod (mirrors RefreshCharacterEntityAuxPatch).
+            LogTranspilerDegradation("Debug.SilentAssert call not found in DeliverOffSpring IL.");
+            return newInstructions.AsEnumerable();
+        }
 
         // Walk backwards from the call to find the start of the argument sequence.
         // The IL pattern is:
@@ -87,8 +92,10 @@ public static class DeliverOffSpring_RaceAssert_Patch
         }
 
         if (startIndex < 0)
-            throw new ArgumentException(
-                "Cannot find race comparison start in DeliverOffSpring IL. Patch: DeliverOffSpring_RaceAssert_Patch");
+        {
+            LogTranspilerDegradation("Race-comparison start (ldarg.0 ... get_Race) not found in DeliverOffSpring IL.");
+            return newInstructions.AsEnumerable();
+        }
 
         // NOP out the entire SilentAssert sequence (args + call)
         for (int i = startIndex; i <= callIndex; i++)
@@ -99,4 +106,15 @@ public static class DeliverOffSpring_RaceAssert_Patch
 
         return newInstructions.AsEnumerable();
     }
+
+    private static void LogTranspilerDegradation(string detail)
+    {
+        try
+        {
+            IoC.Resolve<IModLogger>()?.LogWarning(
+                $"[RaceAge] DeliverOffSpring_RaceAssert_Patch transpiler degrading to no-op — {detail} " +
+                $"The harmless mixed-race SilentAssert noise-reduction will not apply this session (no gameplay effect).");
+        }
+        catch { /* logger resolution failure must not surface to the transpiler caller */ }
+    }
 }
diff --git a/Main/SubModule.cs b/Main/SubModule.cs
index a161b015..636ebdf0 100644
--- a/Main/SubModule.cs
+++ b/Main/SubModule.cs
@@ -86,6 +86,7 @@ public class SubModule : MBSubModuleBase
     private static float _shaderTickAccumulator;
     private static ShaderPrecompileRunner _shaderRunner;
     private static bool _missionTimePatchesApplied;
+    private static bool _gameInitPatchesApplied;
 
     protected override void OnSubModuleLoad()
     {
@@ -534,6 +535,16 @@ public class SubModule : MBSubModuleBase
     {
         base.OnGameInitializationFinished(game);
 
+        // Harmony patches are process-global (applied to methods, persist across games). Apply this
+        // whole per-game-init patch block ONCE per process — re-applying on a 2nd game init duplicates
+        // every prefix/postfix, restarts the BattleLoad watchdog, and CRASHES the non-idempotent
+        // DeliverOffSpring transpiler (chained twice, it can't find its already-NOPped anchor). The
+        // shader-precompile walk starts N custom games in one process and tripped exactly this on item 2;
+        // a player loading a 2nd campaign/custom-battle in one session hits the same crash.
+        // Mirrors _missionTimePatchesApplied in OnMissionBehaviorInitialize.
+        if (_gameInitPatchesApplied) return;
+        _gameInitPatchesApplied = true;
+
         _harmony.PatchCategory("Patch1_FirstTimeInit");
         _harmony.PatchCategory("Patch2_RefreshTableau");
         _harmony.PatchCategory("Patch3_SetRace");
warning: in the working copy of 'Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs', LF will be replaced by CRLF the next time Git touches it

 succeeded in 483ms:
   21: [HarmonyPatch(typeof(HeroCreator), "DeliverOffSpring")]
   22: [HarmonyPatchCategory("Patch13_RaceAge")]
   23: public static class DeliverOffSpring_RaceAssert_Patch
   24: {
   25:     [HarmonyTranspiler]
   26:     static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
   27:     {
   28:         var newInstructions = new List<CodeInstruction>(instructions);
   29:         var callIndex = -1;
   30: 
   31:         // Find the SilentAssert call by matching method name on the operand,
   32:         // since CallerXxx default parameter attributes can cause MethodInfo.Equals mismatch
   33:         for (int i = 0; i < newInstructions.Count; i++)
   34:         {
   35:             if (newInstructions[i].opcode == OpCodes.Call &&
   36:                 newInstructions[i].operand is MethodInfo mi &&
   37:                 mi.Name == "SilentAssert" &&
   38:                 mi.DeclaringType?.Name == "Debug")
   39:             {
   40:                 callIndex = i;
   41:                 break;
   42:             }
   43:         }
   44: 
   45:         if (callIndex < 0)
   46:         {
   47:             // Anchor gone (already NOPped by a prior application of this transpiler, or the engine IL
   48:             // changed). This patch is pure noise-reduction, so degrade to a no-op instead of throwing
   49:             // out of PatchCategory and crashing the mod (mirrors RefreshCharacterEntityAuxPatch).
   50:             LogTranspilerDegradation("Debug.SilentAssert call not found in DeliverOffSpring IL.");
   51:             return newInstructions.AsEnumerable();
   52:         }
   53: 
   54:         // Walk backwards from the call to find the start of the argument sequence.
   55:         // The IL pattern is:
   56:         //   ldarg.0 (mother)
   57:         //   callvirt get_CharacterObject
   58:         //   callvirt get_Race
   59:         //   ldarg.1 (father)
   60:         //   callvirt get_CharacterObject
   61:         //   callvirt get_Race
   62:         //   ceq
   63:         //   ldstr "" (message)
   64:         //   ldc.i4.0 (getDump)
   65:         //   ldstr "..." (callerFile)
   66:         //   ldstr "..." (callerMethod)
   67:         //   ldc.i4 275 (callerLine)
   68:         //   call Debug.SilentAssert
   69:         //
   70:         // Find ldarg.0 that starts the race comparison by scanning backwards
   71:         var startIndex = -1;
   72: 
   73:         for (int i = callIndex - 1; i >= 0; i--)
   74:         {
   75:             if (newInstructions[i].opcode == OpCodes.Ldarg_0)
   76:             {
   77:                 // Verify this ldarg.0 is followed (within a few instructions) by
   78:                 // a call to a property getter named "get_Race"
   79:                 for (int j = i + 1; j < callIndex && j <= i + 4; j++)
   80:                 {
   81:                     if (newInstructions[j].operand is MethodInfo propGetter &&
   82:                         propGetter.Name == "get_Race")
   83:                     {
   84:                         startIndex = i;
   85:                         break;
   86:                     }
   87:                 }
   88: 
   89:                 if (startIndex >= 0)
   90:                     break;
   91:             }
   92:         }
   93: 
   94:         if (startIndex < 0)
   95:         {
   96:             LogTranspilerDegradation("Race-comparison start (ldarg.0 ... get_Race) not found in DeliverOffSpring IL.");
   97:             return newInstructions.AsEnumerable();
   98:         }
   99: 
  100:         // NOP out the entire SilentAssert sequence (args + call)
  101:         for (int i = startIndex; i <= callIndex; i++)
  102:         {
  103:             newInstructions[i].opcode = OpCodes.Nop;
  104:             newInstructions[i].operand = null;
  105:         }
  106: 
  107:         return newInstructions.AsEnumerable();
  108:     }
  109: 
  110:     private static void LogTranspilerDegradation(string detail)
  111:     {
  112:         try
  113:         {
  114:             IoC.Resolve<IModLogger>()?.LogWarning(
  115:                 $"[RaceAge] DeliverOffSpring_RaceAssert_Patch transpiler degrading to no-op — {detail} " +
  116:                 $"The harmless mixed-race SilentAssert noise-reduction will not apply this session (no gameplay effect).");
  117:         }
  118:         catch { /* logger resolution failure must not surface to the transpiler caller */ }
  119:     }

 succeeded in 541ms:
   86:     private static float _shaderTickAccumulator;
   87:     private static ShaderPrecompileRunner _shaderRunner;
   88:     private static bool _missionTimePatchesApplied;
   89:     private static bool _gameInitPatchesApplied;
   90: 
  534:     public override void OnGameInitializationFinished(Game game)
  535:     {
  536:         base.OnGameInitializationFinished(game);
  537: 
  538:         // Harmony patches are process-global (applied to methods, persist across games). Apply this
  539:         // whole per-game-init patch block ONCE per process — re-applying on a 2nd game init duplicates
  540:         // every prefix/postfix, restarts the BattleLoad watchdog, and CRASHES the non-idempotent
  541:         // DeliverOffSpring transpiler (chained twice, it can't find its already-NOPped anchor). The
  542:         // shader-precompile walk starts N custom games in one process and tripped exactly this on item 2;
  543:         // a player loading a 2nd campaign/custom-battle in one session hits the same crash.
  544:         // Mirrors _missionTimePatchesApplied in OnMissionBehaviorInitialize.
  545:         if (_gameInitPatchesApplied) return;
  546:         _gameInitPatchesApplied = true;
  547: 
  548:         _harmony.PatchCategory("Patch1_FirstTimeInit");
  549:         _harmony.PatchCategory("Patch2_RefreshTableau");
  550:         _harmony.PatchCategory("Patch3_SetRace");
  551:         _harmony.PatchCategory("Patch4_CharacterSpawner");
  552:         _harmony.PatchCategory("Patch5_FaceGen");
  553:         _harmony.PatchCategory("Late_Transpiler");
  554:         _harmony.PatchCategory("Late_ActionSetOverride");
  555:         _harmony.PatchCategory("Patch6_BannerEditor");
  556:         _harmony.PatchCategory("Patch7_FactionMap");
  557:         _harmony.PatchCategory("Patch9_RaceFilter");
  558:         _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
  559:         _harmony.PatchCategory("Patch8_SiegeCampGuard");
  560:         _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
  561:         _harmony.PatchCategory("Patch11_Diplomacy");
  562:         _harmony.PatchCategory("Patch12_WarOfTheRing");
  563: 
  564:         _harmony.PatchCategory("Patch14_Execution");
  565:         _harmony.PatchCategory("Patch15_BannerLayerLimit");
  566:         _harmony.PatchCategory("Patch16_AtmospherePersistence");
  567:         _harmony.PatchCategory("Patch17_TroopWeight");
  568:         _harmony.PatchCategory("Patch23_BannerColorPersistence");
  569:         _harmony.PatchCategory("Patch24_BannerDriftGuard");
  570:         _harmony.PatchCategory("Patch39_BanditPartySize");
  571:         _harmony.PatchCategory("Patch40_HideoutDescription");
  572:         _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
  573:         // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment
  574:         // ("post-sever tick AV") was actually the CanAttack charge crash at set_attack_entity
  575:         // (0x6BAB4E), which fired with AND without Patch47 and is fixed in data (LOTRLOME
  576:         // monster Flags). Patch47's own job verified working: severed riders die clean on-foot
  577:         // deaths (act_death_by_arrow class) instead of AVing in the native mounted-death path —
  578:         // which 1.4.6 still does on melee deaths (Die-path AV reading float-bits-as-index from
  579:         // a corrupted action record, debugger-proven 06-12). See docs/features/spider.md.
  580:         Features.Spider.Hooks.Agent_Die_SpiderDismount_Patch.Initialize();
  581:         _harmony.PatchCategory("Patch47_SpiderDeathDismount");
  582: 
  583:         // Patch48: the non-lethal sibling of Patch47. A CanDismount melee hit on a mounted Spider Rider AVs in
  584:         // native HandleBlowAux (reading 0x3) — the same broken non-vanilla mounted-dismount path Patch47 routes
  585:         // around on death. Strips CanDismount for spider riders so the native dismount never fires (the rider
  586:         // stays on the locked mount; damage still applies). Debugger-proven 2026-06-15. See docs/features/spider.md.
  587:         _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
  588: 
  589:         // Patch50: Finalizer swallowing a vanilla NRE in Agent.CheckToDropFlaggedItem (Agent.cs:3595),
  590:         // reached via the shared synthetic-bite path (CustomAttacksUtils.TakeDamage → RegisterBlow →
  591:         // OnAgentHit → affectedAgent.CheckToDropFlaggedItem) when a warg bites another warg (mount
  592:         // victim with a null wielded Item). Already caught by WargAttackService, but swallowing lets
  593:         // OnAgentHit finish and stops the log spam. Crash report 2026-06-17. See the patch doc-comment.
  594:         _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
  595: 
  596:         // Patch13_RaceAge — noise reduction (NOT a crash fix). NOPs the harmless
  597:         // mother.Race == father.Race SilentAssert in DeliverOffSpring that fires on every
  598:         // mixed-race birth (normal in TAOM). Stops the debugger break + debug-log spam.
  599:         _harmony.PatchCategory("Patch13_RaceAge");
  600: 
  601:         var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();
  602:         var specResLogger = IoC.Resolve<IModLogger>();
  603:         PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
  604:         PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
  605:         PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
  606:         _harmony.PatchCategory("Patch26_SpecialResources");
  607:         _harmony.PatchCategory("Patch27_CareerSystem");
  608:         _harmony.PatchCategory("Patch29_CCBodyProperties");
  609:         _harmony.PatchCategory("Patch44_CCNameAutofill");
  610:         _harmony.PatchCategory("Patch33_EquipPresets");
  611:         _harmony.PatchCategory("Patch34_QuickActions");
  612:         _harmony.PatchCategory("Patch35_CompanionTactics");
  613:         _harmony.PatchCategory("Patch36_FiefManagement");
  614:         SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
  615:         _harmony.PatchCategory("Patch38_SettlementNameplateFade");
  616: 
  617:         // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an
  618:         // intermittent battle-load hang leaves a log whose last line names the stuck phase
  619:         // (and, for the equipment phase, the agent + the item whose bo_ collision mesh is
  620:         // missing). The background stall watchdog auto-triggers a crash bundle on a freeze.
  621:         var battleLoadSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
  622:         var equipSnapshotAdapter = IoC.Resolve<IEquipmentSnapshotAdapter>();
  623:         var battleLoadStallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
  624:         Features.BattleLoadDiagnostics.Hooks.PlayerEncounter_Start_Patch.Initialize(battleLoadSvc);
  625:         Features.BattleLoadDiagnostics.Hooks.MissionState_OpenNew_Patch.Initialize(battleLoadSvc);
  626:         Features.BattleLoadDiagnostics.Hooks.BattleSceneSelection_Patch.Initialize(battleLoadSvc);
  627:         Features.BattleLoadDiagnostics.Hooks.Mission_Initialize_BattleLoad_Patch.Initialize(battleLoadSvc, battleLoadStallMarker);
  628:         Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.Initialize(battleLoadSvc, equipSnapshotAdapter);
  629:         _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
  630:         IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();
  631: 
  632:         // CompanionTactics — manual patch for the PRIVATE method
  633:         // OrderOfBattleHeroItemVM.GetCaptainTooltip (private in v1.3.15, can't use
  634:         // [HarmonyPatch] attribute binding).
  635:         var captainTooltipTarget = AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip");
  636:         if (captainTooltipTarget != null)
  637:             _harmony.Patch(captainTooltipTarget, postfix: new HarmonyMethod(
  638:                 typeof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip),
  639:                 nameof(Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip.Postfix)));
  640:         else
  641:             IoC.Resolve<IModLogger>().LogWarning("[CompanionTactics] OrderOfBattleHeroItemVM.GetCaptainTooltip not found — captain tooltip role hint will not appear");
  642: 
  643:         var settlementGuardService = IoC.Resolve<ISettlementGuardService>();
  644:         GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(settlementGuardService);
  645:         GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(settlementGuardService);
  646: 
  647:         // Manual patches for private GuardsCampaignBehavior methods (SandBox.dll)
  648:         var takeGuardTarget = GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod();
  649:         if (takeGuardTarget != null)
  650:             _harmony.Patch(takeGuardTarget, prefix: new HarmonyMethod(
  651:                 typeof(GuardsCampaignBehavior_TakeGuardAgentData_Patch),
  652:                 nameof(GuardsCampaignBehavior_TakeGuardAgentData_Patch.Prefix)));
  653:         else
  654:             IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] TakeGuardAgentDataFromGarrisonTroopList not found — custom guards will not apply");
  655: 
  656:         var spearTarget = GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod();
  657:         if (spearTarget != null)
  658:             _harmony.Patch(spearTarget, prefix: new HarmonyMethod(
  659:                 typeof(GuardsCampaignBehavior_GetSuitableSpear_Patch),
  660:                 nameof(GuardsCampaignBehavior_GetSuitableSpear_Patch.Prefix)));
  661:         else
  662:             IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] GetSuitableSpear not found — culture-specific spears will not apply");
  663: 
  664:         // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
  665:         var mobilePartyTarget = MobilePartyVisual_AddCharacterToPartyIcon_Patch.TargetMethod();
  666:         if (mobilePartyTarget != null)
  667:             _harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod(
  668:                 typeof(MobilePartyVisual_AddCharacterToPartyIcon_Patch),
  669:                 nameof(MobilePartyVisual_AddCharacterToPartyIcon_Patch.Postfix)));
  670:         else
  671:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MobilePartyVisual.AddCharacterToPartyIcon not found — party icon colors will not persist");
  672: 
  673:         // Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)
  674:         var agentVisualsCreateTarget = AgentVisuals_Create_Patch.TargetMethod();
  675:         if (agentVisualsCreateTarget != null)
  676:             _harmony.Patch(agentVisualsCreateTarget, prefix: new HarmonyMethod(
  677:                 typeof(AgentVisuals_Create_Patch),
  678:                 nameof(AgentVisuals_Create_Patch.Prefix)));
  679:         else
  680:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] AgentVisuals.Create not found — clan color randomness suppression will not apply");
  681: 
  682:         // Manual patches for MapConversationTableau (private methods in SandBox.View.dll)
  683:         var leaderTarget = MapConversationTableau_SpawnOpponentLeader_Patch.TargetMethod();
  684:         if (leaderTarget != null)
  685:             _harmony.Patch(leaderTarget, postfix: new HarmonyMethod(
  686:                 typeof(MapConversationTableau_SpawnOpponentLeader_Patch),
  687:                 nameof(MapConversationTableau_SpawnOpponentLeader_Patch.Postfix)));
  688:         else
  689:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentLeader not found — conversation tableau leader colors will not apply");
  690: 
  691:         var bodyguardTarget = MapConversationTableau_SpawnOpponentBodyguard_Patch.TargetMethod();
  692:         if (bodyguardTarget != null)
  693:             _harmony.Patch(bodyguardTarget, postfix: new HarmonyMethod(
  694:                 typeof(MapConversationTableau_SpawnOpponentBodyguard_Patch),
  695:                 nameof(MapConversationTableau_SpawnOpponentBodyguard_Patch.Postfix)));
  696:         else
  697:             IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MapConversationTableau.SpawnOpponentBodyguardCharacter not found — conversation tableau bodyguard colors will not apply");
  698:     }

codex
I’m checking the DI lifetime for the watchdog now. A once-only `Start()` is only correct if the resolved watchdog is process-lifetime or otherwise retained; if it were transient, the timer could be lost.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File -Include *.cs | Select-String -Pattern 'class IoC|Register<|RegisterMany|Reuse\\.|Made\\.' | Select-Object -First 200 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File -Include *.cs | Select-String -Pattern 'BattleLoadStallWatchdog|IBattleLoadDiagnosticsService|IBattleLoadDiagnosticsSettingsProvider|ICrashReportService' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File -Include *.cs | Where-Object { "'$_.Name -match '"'IoC|Container|Bootstrap|Registry' -or "'$_.FullName -match '"'\\\\Core\\\\Infrastructure' } | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 788ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\Main\Core\Infrastructure\Path\IModulePathAdapter.cs
C:\Users\mikew\source\repos\TAOM\Main\Core\Infrastructure\Path\IPathService.cs
C:\Users\mikew\source\repos\TAOM\Main\Core\Infrastructure\Path\ModulePathAdapter.cs
C:\Users\mikew\source\repos\TAOM\Main\Core\Infrastructure\Path\PathService.cs
C:\Users\mikew\source\repos\TAOM\Main\Core\Infrastructure\Reflection\IReflectionService.cs
C:\Users\mikew\source\repos\TAOM\Main\Core\Infrastructure\Reflection\ReflectionHelper.cs
C:\Users\mikew\source\repos\TAOM\Main\Core\Infrastructure\Reflection\ReflectionService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\AdvancedCombatIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\AlignmentRecruitment\RecruitmentAlignmentIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\ArenaIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\ArmyTargeting\ArmyTargetingIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditManagementIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\BannerColorPersistenceIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerInjection\BannerInjectionIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleBalance\BattleBalanceIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityEffectRegistry.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Mutations\IMutationCalculatorRegistry.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Mutations\MutationCalculatorRegistry.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSystemIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\ICareerRegistry.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CastleRecruitment\CastleRecruitmentIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterCreation\CharacterCreationIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\CompanionTacticsIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\CrashReportIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CulturalFeats\CulturalFeatsIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CultureConversion\CultureConversionIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\CustomBattlesIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Diplomacy\DiplomacyIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Elephant\ElephantIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Encyclopedia\EncyclopediaIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\EquipPresetsIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Execution\ExecutionIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Widgets\MapContainerWidget.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\FactionMapIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\FactionRegistryService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\IFactionRegistryService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\FiefManagementIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\HeroRaceIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\InitialChildGeneration\InitialChildGenerationIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\MainMenuCustomizer\MainMenuCustomizerIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\MessengerIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\MissionDiagnostic\MissionDiagnosticIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\MixedFormations\MixedFormationsIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\NamedCompanions\NamedCompanionIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\QuickActions\QuickActionsIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\RaceAge\RaceAgeIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\RevoltTuning\RevoltTuningIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementGuards\SettlementGuardsIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SettlementNameplateFade\NameplateFadeIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\ShaderPrecompilation\ShaderPrecompilationIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Siege\SiegeDefenseIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SmartCavalryAI\SmartCavalryAIIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\SpecialResourcesIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\SpiderIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\StartupResources\StartupResourcesIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\TimeAcceleration\TimeAccelerationIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopProgression\TroopProgressionIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\TroopWeight\TroopWeightIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\WargIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\IoC.cs


 succeeded in 858ms:
Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:17: private static IBattleLoadDiagnosticsService? _service;
Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:20: public static void Initialize(IBattleLoadDiagnosticsService service, IEquipmentSnapshotAdapter adapter)
Main\Features\BattleLoadDiagnostics\Hooks\BattleLoadPhaseBehavior.cs:12: private readonly IBattleLoadDiagnosticsService _service;
Main\Features\BattleLoadDiagnostics\Hooks\BattleLoadPhaseBehavior.cs:16: public BattleLoadPhaseBehavior(IBattleLoadDiagnosticsService service, IBattleLoadStallMarker stallMarker)
Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:15: private static IBattleLoadDiagnosticsService? _service;
Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:17: public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:13: private static IBattleLoadDiagnosticsService? _service;
Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:16: public static void Initialize(IBattleLoadDiagnosticsService service, IBattleLoadStallMarker stallMarker)
Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:16: private static IBattleLoadDiagnosticsService? _service;
Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:18: public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:14: private static IBattleLoadDiagnosticsService? _service;
Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:16: public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:10: container.Register<IBattleLoadDiagnosticsSettingsProvider, BattleLoadDiagnosticsSettingsProvider>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:12: container.Register<IBattleLoadDiagnosticsService, BattleLoadDiagnosticsService>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:15: container.Register<BattleLoadStallWatchdog>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:9: public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:14: private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:23: IBattleLoadDiagnosticsSettingsProvider settings,
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:9: public sealed class BattleLoadDiagnosticsSettingsProvider : IBattleLoadDiagnosticsSettingsProvider
Main\Features\BattleLoadDiagnostics\BattleLoadStallException.cs:5: // Synthetic exception handed to ICrashReportService to produce a diagnostic bundle for a
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:19: public sealed class BattleLoadStallWatchdog : IDisposable
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:25: private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:26: private readonly IBattleLoadDiagnosticsService _service;
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:27: private readonly ICrashReportService? _crashReport;
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:33: public BattleLoadStallWatchdog(
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:35: IBattleLoadDiagnosticsSettingsProvider settings,
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:36: IBattleLoadDiagnosticsService service,
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:37: ICrashReportService? crashReport = null)
Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:88: "BattleLoadStallWatchdog");
Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsService.cs:9: public interface IBattleLoadDiagnosticsService
Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsSettingsProvider.cs:6: public interface IBattleLoadDiagnosticsSettingsProvider
Main\Features\CrashReport\Hooks\AppDomainExceptionHook.cs:24: private readonly ICrashReportService _service;
Main\Features\CrashReport\Hooks\AppDomainExceptionHook.cs:29: public AppDomainExceptionHook(ICrashReportService service, IModLogger logger)
Main\Features\CrashReport\Hooks\CrashReportPatchHelper.cs:19: private static ICrashReportService? _service;
Main\Features\CrashReport\Hooks\CrashReportPatchHelper.cs:62: private static ICrashReportService? ResolveService()
Main\Features\CrashReport\Hooks\CrashReportPatchHelper.cs:65: try { _service = TAOM.IoC.Resolve<ICrashReportService>(); }
Main\Features\CrashReport\CrashReportIoC.cs:61: container.Register<ICrashReportService, CrashReportService>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportService.cs:13: public sealed class CrashReportService : ICrashReportService
Main\Features\CrashReport\ICrashReportService.cs:13: public interface ICrashReportService
Main\SubModule.cs:621: var battleLoadSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
Main\SubModule.cs:630: IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();
Main\SubModule.cs:742: var battleLoadDiagSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();

 succeeded in 858ms:
Main\Adapters\BattlefieldQueryAdapter.cs:11: // Per-instance, NOT static. The adapter is registered as Reuse.Singleton so there's
Main\Features\AdvancedCombat\AdvancedCombatIoC.cs:10: container.Register<IBoneCollisionService, BoneCollisionService>(Reuse.Singleton);
Main\Features\AdvancedCombat\AdvancedCombatIoC.cs:11: container.Register<ISpatialGridDebugService, SpatialGridDebugService>(Reuse.Singleton);
Main\Features\AlignmentRecruitment\RecruitmentAlignmentConfigProvider.cs:13: /// Validate" architecture rule). Cached for the process lifetime (Reuse.Singleton).
Main\Features\AlignmentRecruitment\RecruitmentAlignmentIoC.cs:9: container.Register<IRecruitmentAlignmentConfigProvider, RecruitmentAlignmentConfigProvider>(Reuse.Singleton);
Main\Features\AlignmentRecruitment\RecruitmentAlignmentIoC.cs:10: container.Register<IRecruitmentAlignmentSettingsProvider, RecruitmentAlignmentSettingsProvider>(Reuse.Singleton);
Main\Features\AlignmentRecruitment\RecruitmentAlignmentIoC.cs:11: container.Register<IRecruitmentAlignmentService, RecruitmentAlignmentService>(Reuse.Singleton);
Main\Features\Arena\ArenaIoC.cs:11: container.Register<ITournamentService, TournamentService>(Reuse.Singleton);
Main\Features\ArmyTargeting\ArmyTargetingIoC.cs:9: container.Register<IArmyTargetingConfigProvider, ArmyTargetingConfigProvider>(Reuse.Singleton);
Main\Features\ArmyTargeting\ArmyTargetingIoC.cs:10: container.Register<IArmyTargetingSettingsProvider, ArmyTargetingSettingsProvider>(Reuse.Singleton);
Main\Features\ArmyTargeting\ArmyTargetingIoC.cs:11: container.Register<IArmyTargetingService, ArmyTargetingService>(Reuse.Singleton);
Main\Features\BanditManagement\BanditManagementIoC.cs:9: container.Register<IBanditScalingConfigProvider, BanditScalingConfigProvider>(Reuse.Singleton);
Main\Features\BanditManagement\BanditManagementIoC.cs:10: container.Register<IBanditScalingSettingsProvider, BanditScalingSettingsProvider>(Reuse.Singleton);
Main\Features\BanditManagement\BanditManagementIoC.cs:11: container.Register<IBanditScalingService, BanditScalingService>(Reuse.Singleton);
Main\Features\BanditManagement\BanditManagementIoC.cs:12: container.Register<IHideoutDescriptionService, HideoutDescriptionService>(Reuse.Singleton);
Main\Features\BannerColorPersistence\BannerColorPersistenceIoC.cs:10: container.Register<IBannerColorConfigProvider, BannerColorConfigProvider>(Reuse.Singleton);
Main\Features\BannerColorPersistence\BannerColorPersistenceIoC.cs:11: container.Register<IBannerColorService, BannerColorService>(Reuse.Singleton);
Main\Features\BannerColorPersistence\BannerColorPersistenceIoC.cs:12: container.Register<IBannerHeroAdapter, BannerHeroAdapter>(Reuse.Singleton);
Main\Features\BannerColorPersistence\BannerColorPersistenceIoC.cs:13: container.Register<IAgentColorStore, AgentColorStore>(Reuse.Singleton);
Main\Features\BannerInjection\BannerInjectionIoC.cs:11: container.Register<IKingdomBannerAdapter, KingdomBannerAdapter>(Reuse.Singleton);
Main\Features\BannerInjection\BannerInjectionIoC.cs:12: container.Register<IClanBannerAdapter, ClanBannerAdapter>(Reuse.Singleton);
Main\Features\BannerInjection\BannerInjectionIoC.cs:13: container.Register<IBannerExclusionService, BannerExclusionService>(Reuse.Singleton);
Main\Features\BannerInjection\BannerInjectionIoC.cs:14: container.Register<IBannerConfigProvider, BannerConfigProvider>(Reuse.Singleton);
Main\Features\BannerInjection\BannerInjectionIoC.cs:15: container.Register<IBannerInjectionService, BannerInjectionService>(Reuse.Singleton);
Main\Features\BannerInjection\BannerInjectionIoC.cs:17: container.Register<IOnBannerEditorDone, BannerEditorDoneHook>(Reuse.Singleton);
Main\Features\BattleBalance\BattleBalanceIoC.cs:9: container.Register<IBattleBalanceConfigProvider, BattleBalanceConfigProvider>(Reuse.Singleton);
Main\Features\BattleBalance\BattleBalanceIoC.cs:10: container.Register<IBattleBalanceSettingsProvider, BattleBalanceSettingsProvider>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:10: container.Register<IBattleLoadDiagnosticsSettingsProvider, BattleLoadDiagnosticsSettingsProvider>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:11: container.Register<IEquipmentDumpFormatter, EquipmentDumpFormatter>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:12: container.Register<IBattleLoadDiagnosticsService, BattleLoadDiagnosticsService>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:13: container.Register<IEquipmentSnapshotAdapter, EquipmentSnapshotAdapter>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:14: container.Register<IBattleLoadStallMarker, BattleLoadStallMarker>(Reuse.Singleton);
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:15: container.Register<BattleLoadStallWatchdog>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:16: container.Register<ICareerDataService, CareerDataService>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:19: container.Register<ICareerConfigProvider, CareerConfigProvider>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:20: container.Register<ICareerRegistry, CareerRegistry>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:21: container.Register<ICareerPassiveService, CareerPassiveService>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:22: container.Register<IMutationCalculatorRegistry, MutationCalculatorRegistry>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:25: container.Register<ICareerCreationHandler, CareerCreationHandler>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:26: container.Register<ICareerSwitchService, CareerSwitchService>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:29: container.Register<Abilities.ICareerAbilityService, Abilities.CareerAbilityService>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:30: container.Register<IMutationService, MutationService>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:35: container.Register<Abilities.IAbilityInputAdapter, Abilities.AbilityInputAdapter>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:36: container.Register<Abilities.IMissionTimeProvider, Abilities.MissionTimeProvider>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:37: container.Register<Abilities.IAbilityActivationController, Abilities.AbilityActivationController>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:38: container.Register<UI.IAbilityHudController, UI.AbilityHudController>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:39: container.Register<Abilities.IAbilityEffectExecutor, Abilities.AbilityEffectExecutor>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:44: container.Register<Abilities.ICareerAgentStatService, Abilities.CareerAgentStatService>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:49: container.RegisterDelegate(r => BuildAbilityEffectRegistry(r.Resolve<ICareerConfigProvider>()), Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:50: container.RegisterDelegate<ICareerArchetypeService>(_ => new CareerArchetypeService(GetCareerArchetypeMap()), Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:53: container.Register<ICareerHeroAdapterFactory, CareerHeroAdapterFactory>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:56: container.Register<ICareerQuestConfigProvider, CareerQuestConfigProvider>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:57: container.Register<ICareerQuestService, CareerQuestService>(Reuse.Singleton);
Main\Features\CareerSystem\CareerSystemIoC.cs:58: container.Register<IQuestHeroAdapterFactory, QuestHeroAdapterFactory>(Reuse.Singleton);
Main\Features\CastleRecruitment\CastleRecruitmentIoC.cs:9: container.Register<ICastleRecruitmentConfigProvider, CastleRecruitmentConfigProvider>(Reuse.Singleton);
Main\Features\CastleRecruitment\CastleRecruitmentIoC.cs:10: container.Register<ICastleRecruitmentSettingsProvider, CastleRecruitmentSettingsProvider>(Reuse.Singleton);
Main\Features\CastleRecruitment\CastleRecruitmentIoC.cs:11: container.Register<ICastleRecruitmentService, CastleRecruitmentService>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:10: container.Register<ICultureCreationDataProvider, CultureCreationDataProvider>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:11: container.Register<ICultureRaceFilterService, CultureRaceFilterService>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:12: container.Register<INarrativeDataProvider, NarrativeDataProvider>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:13: container.Register<IEquipmentRosterProvider, EquipmentRosterProvider>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:14: container.Register<ICareerMenuDataProvider, CareerMenuDataProvider>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:15: container.Register<ICareerMenuService, CareerMenuService>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:16: container.Register<ICharacterCreationContentService, CharacterCreationContentService>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:17: container.Register<INarrativeHorseGuardService, NarrativeHorseGuardService>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:18: container.Register<ICCBodyPropertiesProvider, CCBodyPropertiesProvider>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:19: container.Register<IPlayerBodyPropertiesAdapter, PlayerBodyPropertiesAdapter>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:20: container.Register<ICCBodyPropertiesService, CCBodyPropertiesService>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:21: container.Register<IPlayerEquipmentAdapter, PlayerEquipmentAdapter>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:22: container.Register<IPlayerEquipmentService, PlayerEquipmentService>(Reuse.Singleton);
Main\Features\CharacterCreation\CharacterCreationIoC.cs:23: container.Register<ICareerStartingEquipmentService, CareerStartingEquipmentService>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:9: /// DryIoc registrations for CompanionTactics. All services are <see cref="Reuse.Singleton"/>
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:19: container.Register<ICompanionTacticsSettingsProvider, CompanionTacticsSettingsProvider>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:22: container.Register<ICompanionRoleService, CompanionRoleService>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:23: container.Register<IRoleTooltipDecorator, RoleTooltipDecorator>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:26: container.Register<IFormationPresetService, FormationPresetService>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:27: container.Register<IHeroAutoAssigner, HeroAutoAssigner>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:28: container.Register<IOrderOfBattleVMTracker, OrderOfBattleVMTracker>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:29: container.Register<IOOBOverlayService, OOBOverlayService>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:32: container.Register<IBattleActionBarService, BattleActionBarService>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:33: container.Register<ITroopStanceManager, TroopStanceManager>(Reuse.Singleton);
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:34: container.Register<IFormationCompositionAnalyzer, FormationCompositionAnalyzer>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:13: container.Register<IdentityCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:14: container.Register<ModuleListCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:15: container.Register<AssemblyListCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:16: container.Register<HarmonyCorrelationCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:17: container.Register<MissionStateCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:18: container.Register<McmSettingsCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:19: container.Register<ProcessEnvironmentCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:20: container.Register<GpuDisplayCollector>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:23: container.Register<CampaignEventsBuffer>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:24: container.Register<FrameTimingBuffer>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:30: Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:35: Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:40: Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:43: container.Register<PlainTextCrashReportRenderer>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:44: container.Register<JsonCrashReportRenderer>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:45: container.Register<CrashBundleWriter>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:55: Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:58: container.Register<IButterLibExceptionHandlerAdapter, ButterLibExceptionHandlerAdapter>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:61: container.Register<ICrashReportService, CrashReportService>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:65: container.Register<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>(Reuse.Singleton);
Main\Features\CrashReport\CrashReportIoC.cs:66: container.Register<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>(Reuse.Singleton);
Main\Features\CulturalFeats\CulturalFeatsIoC.cs:13: container.Register<ICulturalFeatsService, CulturalFeatsService>(Reuse.Singleton);
Main\Features\CulturalFeats\ICultureFeatAdapter.cs:18: /// feat registry hands out nulls before <c>CreateAndRegister</c> runs).
Main\Features\CultureConversion\CultureConversionConfigProvider.cs:13: /// per-field revert-to-default + warning, NaN/Infinity guards on the float field. Reuse.Singleton —
Main\Features\CultureConversion\CultureConversionIoC.cs:10: container.Register<ICultureConversionConfigProvider, CultureConversionConfigProvider>(Reuse.Singleton);
Main\Features\CultureConversion\CultureConversionIoC.cs:11: container.Register<ICultureConversionSettingsProvider, CultureConversionSettingsProvider>(Reuse.Singleton);
Main\Features\CultureConversion\CultureConversionIoC.cs:12: container.Register<ICultureConversionStore, CultureConversionStore>(Reuse.Singleton);
Main\Features\CultureConversion\CultureConversionIoC.cs:13: container.Register<ICultureConversionAdapter, CultureConversionAdapter>(Reuse.Singleton);
Main\Features\CultureConversion\CultureConversionIoC.cs:14: container.Register<ICultureConversionService, CultureConversionService>(Reuse.Singleton);
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:12: container.Register<IItemPoolAdapter, ItemPoolAdapter>(Reuse.Singleton);
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:13: container.Register<ITownRosterAdapter, TownRosterAdapter>(Reuse.Singleton);
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:14: container.Register<ICultureMarketplaceConfigProvider, CultureMarketplaceConfigProvider>(Reuse.Singleton);
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:15: container.Register<ICultureItemPoolService, CultureItemPoolService>(Reuse.Singleton);
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:16: container.Register<ICultureMarketplaceInjectionService, CultureMarketplaceInjectionService>(Reuse.Singleton);
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:17: container.Register<ICultureMarketplaceMaintenanceService, CultureMarketplaceMaintenanceService>(Reuse.Singleton);
Main\Features\CustomBattles\CustomBattlesIoC.cs:10: container.Register<ICustomBattleService, CustomBattleService>(Reuse.Singleton);
Main\Features\CustomBattles\CustomBattlesIoC.cs:11: container.Register<ISideCommanderFilter, SideCommanderFilter>(Reuse.Singleton);
Main\Features\CustomBattles\CustomBattlesIoC.cs:12: container.Register<IOnGetCustomBattleCommanders, CustomBattleCommandersHook>(Reuse.Transient);
Main\Features\CustomBattles\CustomBattlesIoC.cs:13: container.Register<IOnGetCustomBattleFactions, CustomBattleFactionsHook>(Reuse.Transient);
Main\Features\CustomBattles\CustomBattlesIoC.cs:14: container.Register<IOnGetDefaultTroopOfFormation, CustomBattleTroopHook>(Reuse.Transient);
Main\Features\Diplomacy\DiplomacyIoC.cs:11: container.Register<IAllianceAdapter, AllianceAdapter>(Reuse.Singleton);
Main\Features\Diplomacy\DiplomacyIoC.cs:12: container.Register<IDiplomacyConfigProvider, DiplomacyConfigProvider>(Reuse.Singleton);
Main\Features\Diplomacy\DiplomacyIoC.cs:13: container.Register<IDiplomacyService, DiplomacyService>(Reuse.Singleton);
Main\Features\Diplomacy\DiplomacyIoC.cs:14: container.Register<IOnAllianceAction, AllianceActionHook>(Reuse.Singleton);
Main\Features\Diplomacy\DiplomacyIoC.cs:15: container.Register<IWarOfTheRingConfigProvider, WarOfTheRingConfigProvider>(Reuse.Singleton);
Main\Features\Diplomacy\DiplomacyIoC.cs:16: container.Register<ITaomSettingsProvider, TaomSettingsProvider>(Reuse.Singleton);
Main\Features\Diplomacy\DiplomacyIoC.cs:17: container.Register<IWarOfTheRingService, WarOfTheRingService>(Reuse.Singleton);
Main\Features\Diplomacy\DiplomacyIoC.cs:18: container.Register<IOnPeaceAction, PeaceActionHook>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:15: container.Register<ICacheRebuildConfigProvider, CacheRebuildConfigProvider>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:16: container.Register<IPathReuseCache, PathReuseCache>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:17: container.Register<IPersistentPathCache, PersistentPathCache>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:18: container.Register<SerialPhase1Builder>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:19: container.Register<ParallelPhase1Builder>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:20: container.Register<SerialPhase2Builder>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:21: container.Register<ParallelPhase2Builder>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:22: container.Register<ISmokeTestGate, SmokeTestGate>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:23: container.Register<IValidationReportWriter, ValidationReportWriter>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:24: container.Register<ICheckpointSerializer, CheckpointSerializer>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:25: container.Register<ISettlementSnapshotStore, SettlementSnapshotStore>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:26: container.Register<ISettlementDiffer, SettlementDiffer>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:27: container.Register<IDistanceCacheBuilderService, CacheBuilderService>(Reuse.Singleton);
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:28: container.Register<IRuntimeCacheRebuildService, RuntimeCacheRebuildService>(Reuse.Singleton);
Main\Features\Elephant\ElephantIoC.cs:10: container.Register<IElephantAttackService, ElephantAttackService>(Reuse.Singleton);
Main\Features\Encyclopedia\EncyclopediaIoC.cs:9: container.Register<IEncyclopediaSettingsProvider, EncyclopediaSettingsProvider>(Reuse.Singleton);
Main\Features\EquipPresets\Hooks\EquipmentPresetCampaignBehavior.cs:13: /// Registered <c>Reuse.Singleton</c>: the same instance survives across campaigns within one
Main\Features\EquipPresets\EquipPresetsIoC.cs:11: container.Register<IEquipPresetsSettingsProvider, EquipPresetsSettingsProvider>(Reuse.Singleton);
Main\Features\EquipPresets\EquipPresetsIoC.cs:12: container.Register<IEquipmentSlotAdapter, EquipmentSlotAdapter>(Reuse.Singleton);
Main\Features\EquipPresets\EquipPresetsIoC.cs:13: container.Register<IItemModifierLookupAdapter, ItemModifierLookupAdapter>(Reuse.Singleton);
Main\Features\EquipPresets\EquipPresetsIoC.cs:14: container.Register<IInventoryScreenAdapter, InventoryScreenAdapter>(Reuse.Singleton);
Main\Features\EquipPresets\EquipPresetsIoC.cs:15: container.Register<IEquipmentPresetService, EquipmentPresetService>(Reuse.Singleton);
Main\Features\EquipPresets\EquipPresetsIoC.cs:16: container.Register<EquipmentPresetCampaignBehavior>(Reuse.Singleton);
Main\Features\Execution\ExecutionIoC.cs:10: container.Register<IAlignmentConfigProvider, AlignmentConfigProvider>(Reuse.Singleton);
Main\Features\Execution\ExecutionIoC.cs:11: container.Register<IAlignmentService, AlignmentService>(Reuse.Singleton);
Main\Features\Execution\ExecutionIoC.cs:12: container.Register<IOnExecutionAction, ExecutionActionHook>(Reuse.Singleton);
Main\Features\Execution\ExecutionIoC.cs:15: container.Register<IExecutionRelationService, ExecutionRelationService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:11: container.Register<ICultureObjectAdapter, CultureObjectAdapter>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:13: container.Register<IFactionConfigProvider, FactionConfigProvider>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:14: container.Register<IFactionRegistryService, FactionRegistryService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:15: container.Register<ICultureResolverService, CultureResolverService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:16: container.Register<ILandmarkService, LandmarkService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:17: container.Register<IFactionSelectionService, FactionSelectionService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:18: container.Register<IFactionHoverService, FactionHoverService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:19: container.Register<ICultureSettingService, CultureSettingService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:20: container.Register<ICultureStageProgressionService, CultureStageProgressionService>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:22: container.Register<IOnCultureStageViewCreated, CultureStageViewCreatedHook>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:23: container.Register<IOnCultureStageViewTick, CultureStageViewTickHook>(Reuse.Singleton);
Main\Features\FactionMap\FactionMapIoC.cs:24: container.Register<IOnCultureStageViewFinalize, CultureStageViewFinalizeHook>(Reuse.Singleton);
Main\Features\FiefManagement\FiefManagementIoC.cs:10: container.Register<IFiefManagementSettingsProvider, FiefManagementSettingsProvider>(Reuse.Singleton);
Main\Features\FiefManagement\FiefManagementIoC.cs:11: container.Register<ISettlementOwnershipAdapter, SettlementOwnershipAdapter>(Reuse.Singleton);
Main\Features\FiefManagement\FiefManagementIoC.cs:12: container.Register<IMapScreenInputAdapter, MapScreenInputAdapter>(Reuse.Singleton);
Main\Features\FiefManagement\FiefManagementIoC.cs:13: container.Register<IRemoteFiefSettlementSwapper, RemoteFiefSettlementSwapper>(Reuse.Singleton);
Main\Features\FiefManagement\FiefManagementIoC.cs:14: container.Register<IFiefHubService, FiefHubService>(Reuse.Singleton);
Main\Features\FiefManagement\FiefManagementIoC.cs:15: container.Register<IFiefHubMenuPresenter, FiefHubMenuPresenter>(Reuse.Singleton);
Main\Features\HeroRace\HeroRaceIoC.cs:10: container.Register<IRacePositionConfigurationService, RacePositionConfigurationService>(Reuse.Singleton);
Main\Features\HeroRace\HeroRaceIoC.cs:12: container.Register<ICharacterTableauService, CharacterTableauService>(Reuse.Singleton);
Main\Features\HeroRace\HeroRaceIoC.cs:14: container.Register<ICharacterSpawnerService, CharacterSpawnerService>(Reuse.Singleton);
Main\Features\HeroRace\HeroRaceIoC.cs:16: container.Register<IOnFaceGenGetBaseMonsterFromRace, EyeHeightAdjustmentHook>(Reuse.Singleton);
Main\Features\HeroRace\HeroRaceIoC.cs:21: container.Register<IRacePersistenceService, RacePersistenceService>(Reuse.Singleton);
Main\Features\InitialChildGeneration\InitialChildGenerationIoC.cs:10: container.Register<IClanPopulationAdapter, ClanPopulationAdapter>(Reuse.Singleton);
Main\Features\InitialChildGeneration\InitialChildGenerationIoC.cs:11: container.Register<IChildCreatorAdapter, ChildCreatorAdapter>(Reuse.Singleton);
Main\Features\InitialChildGeneration\InitialChildGenerationIoC.cs:12: container.Register<IInitialChildGenerationConfigProvider, InitialChildGenerationConfigProvider>(Reuse.Singleton);
Main\Features\InitialChildGeneration\InitialChildGenerationIoC.cs:13: container.Register<IRandomSource, SystemRandomSource>(Reuse.Singleton);
Main\Features\InitialChildGeneration\InitialChildGenerationIoC.cs:14: container.Register<IInitialChildGenerationService, InitialChildGenerationService>(Reuse.Singleton);
Main\Features\MainMenuCustomizer\MainMenuCustomizerIoC.cs:9: container.Register<IModuleMenuAdapter, ModuleMenuAdapter>(Reuse.Singleton);
Main\Features\MainMenuCustomizer\MainMenuCustomizerIoC.cs:10: container.Register<IMainMenuCustomizerService, MainMenuCustomizerService>(Reuse.Singleton);
Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:24: /// Postfix is already attached when MCM calls <c>CreateAndRegister</c>.
Main\Features\Messengers\MessengerCampaignBehavior.cs:42: // Behavior is registered Reuse.Singleton, so a single instance survives across campaigns within the same
Main\Features\Messengers\MessengerIoC.cs:9: container.Register<IMessengerSettingsProvider, MessengerSettingsProvider>(Reuse.Singleton);
Main\Features\Messengers\MessengerIoC.cs:10: container.Register<IMessengerConfigProvider, MessengerConfigProvider>(Reuse.Singleton);
Main\Features\Messengers\MessengerIoC.cs:11: container.Register<IMessengerStateStore, MessengerStateStore>(Reuse.Singleton);
Main\Features\Messengers\MessengerIoC.cs:12: container.Register<IMessengerRandomSource, MessengerRandomSource>(Reuse.Singleton);
Main\Features\Messengers\MessengerIoC.cs:13: container.Register<IMessengerService, MessengerService>(Reuse.Singleton);
Main\Features\Messengers\MessengerIoC.cs:14: container.Register<MessengerCampaignBehavior>(Reuse.Singleton);
Main\Features\MissionDiagnostic\MissionDiagnosticIoC.cs:9: container.Register<IMissionDiagnosticService, MissionDiagnosticService>(Reuse.Singleton);
Main\Features\MixedFormations\MixedFormationsIoC.cs:9: container.Register<IMixedFormationsSettingsProvider, MixedFormationsSettingsProvider>(Reuse.Singleton);
Main\Features\MixedFormations\MixedFormationsIoC.cs:10: container.Register<ILayoutPositioner, LayoutPositioner>(Reuse.Singleton);
Main\Features\MixedFormations\MixedFormationsIoC.cs:11: container.Register<IFormationLayoutService, FormationLayoutService>(Reuse.Singleton);
Main\Features\NamedCompanions\NamedCompanionIoC.cs:10: container.Register<INamedCompanionConfigProvider, NamedCompanionConfigProvider>(Reuse.Singleton);

codex
The watchdog is registered `Reuse.Singleton`, and `Start()` has an internal `_timer != null` guard, so the once-per-process call aligns with the service lifetime. I’m drafting the review now.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs' | ForEach-Object { "'$i++; '"'{0,5}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/IoC.cs' | ForEach-Object { "'$i++; if ($i -le 120 -or ($i -ge 120 -and $i -le 220)) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path '"'Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs' | ForEach-Object { "'$i++; if ($i -ge 45 -and $i -le 80) { '"'{0,5}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 491ms:
    1: using DryIoc;
    2: using TAOM.Adapters;
    3: 
    4: namespace TAOM.Features.BattleLoadDiagnostics;
    5: 
    6: public static class BattleLoadDiagnosticsIoC
    7: {
    8:     public static void RegisterBattleLoadDiagnosticsFeature(IContainer container)
    9:     {
   10:         container.Register<IBattleLoadDiagnosticsSettingsProvider, BattleLoadDiagnosticsSettingsProvider>(Reuse.Singleton);
   11:         container.Register<IEquipmentDumpFormatter, EquipmentDumpFormatter>(Reuse.Singleton);
   12:         container.Register<IBattleLoadDiagnosticsService, BattleLoadDiagnosticsService>(Reuse.Singleton);
   13:         container.Register<IEquipmentSnapshotAdapter, EquipmentSnapshotAdapter>(Reuse.Singleton);
   14:         container.Register<IBattleLoadStallMarker, BattleLoadStallMarker>(Reuse.Singleton);
   15:         container.Register<BattleLoadStallWatchdog>(Reuse.Singleton);
   16:     }
   17: }

 succeeded in 481ms:
   45:     public void Start()
   46:     {
   47:         if (_timer != null) return;
   48:         _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
   49:     }
   50: 
   51:     // Pure decision: fire once per window when it's been open at or past the threshold.
   52:     public static bool ShouldFire(bool windowOpen, double elapsedSeconds, double thresholdSeconds, bool alreadyFired)
   53:         => windowOpen && !alreadyFired && elapsedSeconds >= thresholdSeconds;
   54: 
   55:     private void Poll()
   56:     {
   57:         try
   58:         {
   59:             if (!_settings.IsEnabled || !_settings.StallWatchdogEnabled) return;
   60: 
   61:             var openedAt = BattleLoadLoadingWindow.OpenedAtUtc;
   62:             if (!openedAt.HasValue)
   63:             {
   64:                 _firedForCurrentWindow = false; // window closed — ready for the next load
   65:                 return;
   66:             }
   67: 
   68:             // New window since we last fired? reset the latch so each load gets one fire.
   69:             long openedTicks = openedAt.Value.Ticks;
   70:             if (openedTicks != _lastWindowOpenedTicks)
   71:             {
   72:                 _lastWindowOpenedTicks = openedTicks;
   73:                 _firedForCurrentWindow = false;
   74:             }
   75: 
   76:             double elapsed = (DateTime.UtcNow - openedAt.Value).TotalSeconds;
   77:             if (!ShouldFire(true, elapsed, _settings.StallWatchdogSeconds, _firedForCurrentWindow)) return;
   78: 
   79:             _firedForCurrentWindow = true;
   80:             _logger.LogError($"{Tag} WATCHDOG STILL LOADING after {elapsed:F0}s — last {_service.CurrentStatusLine}");

 succeeded in 546ms:
    1: using DryIoc;
    2: using System.Collections.Generic;
    3: using TAOM.Adapters;
    4: using TAOM.Core.Domain;
    5: using TAOM.Core.Infrastructure;
    6: using TAOM.Core.Logging;
    7: using TAOM.Features.BannerColorPersistence;
    8: using TAOM.Features.BannerInjection;
    9: using TAOM.Features.HeroRace;
   10: using TAOM.Features.CharacterCreation;
   11: using TAOM.Features.FactionMap;
   12: using TAOM.Features.InitialChildGeneration;
   13: using TAOM.Features.Diplomacy;
   14: using TAOM.Features.Encyclopedia;
   15: using TAOM.Features.RaceAge;
   16: using TAOM.Features.Execution;
   17: using TAOM.Features.AlignmentRecruitment;
   18: using TAOM.Features.StartupResources;
   19: using TAOM.Features.TroopProgression;
   20: using TAOM.Features.AdvancedCombat;
   21: using TAOM.Features.CustomBattles;
   22: using TAOM.Features.TroopWeight;
   23: using TAOM.Features.Warg;
   24: using TAOM.Features.Spider;
   25: using TAOM.Features.BattleBalance;
   26: using TAOM.Features.MainMenuCustomizer;
   27: using TAOM.Features.Messengers;
   28: using TAOM.Features.ShaderPrecompilation;
   29: using TAOM.Features.Siege;
   30: using TAOM.Features.ArmyTargeting;
   31: using TAOM.Features.TimeAcceleration;
   32: using TAOM.Features.SpecialResources;
   33: using TAOM.Features.CareerSystem;
   34: using TAOM.Features.SettlementGuards;
   35: using TAOM.Features.NamedCompanions;
   36: using TAOM.Features.RevoltTuning;
   37: using TAOM.Features.BanditManagement;
   38: using TAOM.Features.CastleRecruitment;
   39: using TAOM.Features.SiegeDismount;
   40: using TAOM.Features.MixedFormations;
   41: using TAOM.Features.SmartCavalryAI;
   42: using TAOM.Features.QuickActions;
   43: using TAOM.Features.CompanionTactics;
   44: using TAOM.Features.FiefManagement;
   45: using TAOM.Features.EquipPresets;
   46: using TAOM.Features.EditorCacheRebuild;
   47: using TAOM.Features.CulturalFeats;
   48: using TAOM.Features.CultureMarketplace;
   49: using TAOM.Features.CultureConversion;
   50: using TAOM.Features.MissionDiagnostic;
   51: using TAOM.Features.CrashReport;
   52: using TAOM.Features.BattleLoadDiagnostics;
   53: using TAOM.Features.SettlementNameplateFade;
   54: 
   55: namespace TAOM;
   56: 
   57: public static class IoC
   58: {
   59:     private static IContainer _container;
   60: 
   61:     public static void Configure()
   62:     {
   63:         var container = new Container();
   64: 
   65:         container.RegisterInstance<IContainer>(container);
   66: 
   67:         RegisterCoreServices(container);
   68:         RegisterLoggingServices(container);
   69: 
   70:         HeroRaceIoC.RegisterHeroRaceFeature(container);
   71:         BannerInjectionIoC.RegisterBannerInjectionFeature(container);
   72:         BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
   73:         TroopProgressionIoC.RegisterTroopProgressionFeature(container);
   74:         Features.Arena.ArenaIoC.RegisterArenaFeature(container);
   75:         FactionMapIoC.RegisterFactionMapFeature(container);
   76:         CharacterCreationIoC.RegisterCharacterCreationFeature(container);
   77:         InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
   78:         DiplomacyIoC.RegisterDiplomacyFeature(container);
   79:         RaceAgeIoC.RegisterRaceAgeFeature(container);
   80:         ExecutionIoC.RegisterExecutionFeature(container);
   81:         RecruitmentAlignmentIoC.RegisterAlignmentRecruitmentFeature(container);
   82:         EncyclopediaIoC.RegisterEncyclopediaFeature(container);
   83:         StartupResourcesIoC.RegisterStartupResourcesFeature(container);
   84:         TroopWeightIoC.RegisterTroopWeightFeature(container);
   85:         AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
   86:         WargIoC.RegisterWargFeature(container);
   87:         SpiderIoC.RegisterSpiderFeature(container);
   88:         Features.Elephant.ElephantIoC.RegisterElephantFeature(container);
   89:         CustomBattlesIoC.RegisterCustomBattlesFeature(container);
   90:         BattleBalanceIoC.RegisterBattleBalanceFeature(container);
   91:         MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
   92:         ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
   93:         SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
   94:         ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
   95:         TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
   96:         SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
   97:         CareerSystemIoC.RegisterCareerSystemFeature(container);
   98:         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
   99:         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
  100:         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
  101:         BanditManagementIoC.RegisterBanditManagementFeature(container);
  102:         CastleRecruitmentIoC.RegisterCastleRecruitmentFeature(container);
  103:         SiegeDismountIoC.RegisterSiegeDismountFeature(container);
  104:         MixedFormationsIoC.RegisterMixedFormationsFeature(container);
  105:         SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
  106:         QuickActionsIoC.RegisterQuickActionsFeature(container);
  107:         MessengerIoC.RegisterMessengerFeature(container);
  108:         EquipPresetsIoC.RegisterEquipPresetsFeature(container);
  109:         CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);
  110:         FiefManagementIoC.RegisterFiefManagementFeature(container);
  111:         EditorCacheRebuildIoC.RegisterEditorCacheRebuildFeature(container);
  112:         CulturalFeatsIoC.RegisterCulturalFeatsFeature(container);
  113:         CultureMarketplaceIoC.RegisterCultureMarketplaceFeature(container);
  114:         CultureConversionIoC.RegisterCultureConversionFeature(container);
  115:         MissionDiagnosticIoC.RegisterMissionDiagnosticFeature(container);
  116:         CrashReportIoC.RegisterCrashReportFeature(container);
  117:         BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);
  118:         NameplateFadeIoC.RegisterSettlementNameplateFadeFeature(container);
  119: 
  120:         _container = container;
  121: 
  122:         // Post-registration initialization
  123:         CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());
  124:     }
  125: 
  126:     private static void RegisterCoreServices(IContainer container)
  127:     {
  128:         container.Register<IModulePathAdapter, ModulePathAdapter>(Reuse.Singleton);
  129:         container.Register<IFaceGenAdapter, FaceGenAdapter>(Reuse.Singleton);
  130:         container.Register<IPathService, PathService>(Reuse.Singleton);
  131:         container.Register<IReflectionService, ReflectionService>(Reuse.Singleton);
  132:         container.Register<IRaceManager, RaceManager>(Reuse.Singleton);
  133:         container.Register<IHeroRosterAdapter, HeroRosterAdapter>(Reuse.Singleton);
  134:         // Phase 9b #148 P2 — IVolunteerContextAdapter moved to TroopProgressionIoC for cohesion
  135:         // (only consumer is TaomVolunteerModel inside that feature).
  136:         container.Register<IMissionAdapterFactory, MissionAdapterFactory>(Reuse.Singleton);
  137:         container.Register<IObjectManagerAdapter, ObjectManagerAdapter>(Reuse.Singleton);
  138:         container.Register<ICampaignSessionAdapter, CampaignSessionAdapter>(Reuse.Singleton);
  139:     }
  140: 
  141:     private static void RegisterLoggingServices(IContainer container)
  142:     {
  143:         container.Register<IModLogger, FileLogger>(Reuse.Singleton);
  144:     }
  145: 
  146:     public static T Resolve<T>()
  147:     {
  148:         return _container.Resolve<T>();
  149:     }
  150: 
  151:     public static IEnumerable<T> ResolveAll<T>()
  152:     {
  153:         return _container.ResolveMany<T>();
  154:     }
  155: 
  156:     public static void Dispose()
  157:     {
  158:         _container?.Dispose();
  159:         _container = null;
  160:     }
  161: }

