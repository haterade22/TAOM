using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Targeted Finalizer shield on the 10 TaleWorlds save/load/mission methods that mods
/// most commonly break. Catches DuplicateKey + other save failures, attributes them to
/// the culprit assembly via stack-walk, persists to FailedModsCatalog, allows the game
/// to continue rather than crash to desktop.
///
/// Methods shielded (mirrors BetaDeps.Foundation.SaveShield):
///   TaleWorlds.Core.MBSaveLoad.LoadSaveGameData
///   SandBox.SandBoxSaveHelper.LoadGameAction
///   SandBox.SandBoxSaveHelper.LoadSaveGame
///   TaleWorlds.SaveSystem.SaveManager.Load
///   TaleWorlds.SaveSystem.LoadResult.Load
///   TaleWorlds.SaveSystem.Load.LoadResult.Load
///   TaleWorlds.MountAndBlade.MissionState.FinishMissionLoading
///   TaleWorlds.MountAndBlade.Mission.SetMissionMode
///   TaleWorlds.MountAndBlade.Mission.OnInitialize
///   TaleWorlds.MountAndBlade.Mission.SpawnTroop
///
/// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
/// indiscriminately and only catches the missing-API trinity. SaveShield targets 10
/// specific methods and catches a broader exception set because save corruption from
/// stale mods is a different failure class.
///
/// Opt-out via <c>saveshield-swallow-disabled.flag</c> in the TAOM.Dependencies module
/// directory.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports BetaDeps.Foundation.SaveShield.
/// </summary>
public static class SaveShield
{
    private const string Tag = "SaveShield";
    private const string HarmonyId = "TAOM.Dependencies.Foundation.SaveShield";
    private const string SwallowDisableFlagName = "saveshield-swallow-disabled.flag";
    private const int RecentCapacity = 20;

    private static readonly HashSet<MethodBase> _shielded = new();
    private static readonly object _lock = new();

    private static long _duplicateKeyHits;
    private static long _otherFailureHits;
    private static long _swallowedCount;

    private static readonly LinkedList<FailureRecord> _recent = new();
    private static readonly object _recentLock = new();

    // (TypeFullName, AssemblyName, MethodName, Category)
    // Targets verified against Bannerlord v1.4.5 via `ilspycmd` (DR3 Phase 4 follow-up,
    // 2026-05-27). The original BetaDeps target list was developed against a different
    // Bannerlord version and had 4 stale entries; this list is the v1.4.5-correct set:
    //   - SandBoxSaveHelper.TryLoadSave    — public entry point user clicks "Load Game"
    //   - SandBoxSaveHelper.LoadGameAction — private, the actual load delegate
    //   - SaveManager.Load                 — 2 overloads (string, ISaveDriver) and (..., bool)
    //   - LoadResult.InitializeObjects     — runs during save deserialization (post-stream)
    //   - LoadResult.AfterInitializeObjects — runs after objects loaded, before scene init
    //   - MissionState.OnInitialize        — protected override; calls FinishMissionLoading
    //   - MissionState.FinishMissionLoading — private; the post-stream init point
    //   - Mission.Initialize               — public; called when mission goes live
    //   - Mission.SetMissionMode           — runs at mode transitions inside a mission
    //   - Mission.SpawnTroop               — spawns agents into the mission
    // Note: LoadResult is in `TaleWorlds.SaveSystem.Load` namespace (NOT top-level
    // `TaleWorlds.SaveSystem`). v1.4.5 has no top-level `LoadResult` type.
    private static readonly (string TypeFullName, string AssemblyName, string MethodName, string Category)[] _targets =
    {
        // Save-load chain
        ("TaleWorlds.Core.MBSaveLoad", "TaleWorlds.Core", "LoadSaveGameData", "SAVE-LOAD"),
        ("SandBox.SandBoxSaveHelper", "SandBox", "TryLoadSave", "SAVE-LOAD"),
        ("SandBox.SandBoxSaveHelper", "SandBox", "LoadGameAction", "SAVE-LOAD"),
        ("TaleWorlds.SaveSystem.SaveManager", "TaleWorlds.SaveSystem", "Load", "SAVE-LOAD"),
        ("TaleWorlds.SaveSystem.Load.LoadResult", "TaleWorlds.SaveSystem", "InitializeObjects", "SAVE-LOAD"),
        ("TaleWorlds.SaveSystem.Load.LoadResult", "TaleWorlds.SaveSystem", "AfterInitializeObjects", "SAVE-LOAD"),
        // Mission init chain
        ("TaleWorlds.MountAndBlade.MissionState", "TaleWorlds.MountAndBlade", "OnInitialize", "MISSION-INIT"),
        ("TaleWorlds.MountAndBlade.MissionState", "TaleWorlds.MountAndBlade", "FinishMissionLoading", "MISSION-INIT"),
        ("TaleWorlds.MountAndBlade.Mission", "TaleWorlds.MountAndBlade", "Initialize", "MISSION-INIT"),
        ("TaleWorlds.MountAndBlade.Mission", "TaleWorlds.MountAndBlade", "SetMissionMode", "MISSION-INIT"),
        ("TaleWorlds.MountAndBlade.Mission", "TaleWorlds.MountAndBlade", "SpawnTroop", "MISSION-INIT"),
    };

    // Stack-frame attribution: anything starting with these prefixes is "engine" or
    // "infrastructure" and NOT a culprit. The first frame whose assembly doesn't match
    // <see cref="IsEngineAssembly"/> is the likely culprit mod.
    //
    // Codex S2 MED fix 2026-05-27: previously this list contained "TAOM" as a broad
    // prefix, which incorrectly matched consumer assemblies named "TAOM_Online" /
    // "TAOM_Map" (those ARE third-party mods from TAOM's perspective during stack-walk
    // attribution). It also missed bundled BUTR infrastructure assemblies. The list
    // below is the bundled-runtime allowlist; TAOM-owned exact matches are handled
    // separately in IsEngineAssembly.
    private static readonly string[] _enginePrefixes =
    {
        "TaleWorlds.", "SandBox", "StoryMode", "CustomBattle",
        "Bannerlord.Harmony", "Bannerlord.UIExtenderEx", "Bannerlord.ButterLib",
        "Bannerlord.MBOptionScreen", "Bannerlord.ModuleLoader",
        "MCMv5", "MCM.UI.Adapter", "BUTR.CrashReport",
        "0Harmony", "HarmonyLib", "Mono.Cecil", "MonoMod",
        "System.", "Microsoft.", "mscorlib", "Newtonsoft.Json", "Serilog",
    };

    private static bool IsEngineAssembly(string asmName)
    {
        if (string.IsNullOrEmpty(asmName)) return false;

        // Exact-match TAOM-owned assemblies. Use exact equality + ".TAOM." prefix —
        // NOT a broad "TAOM" StartsWith which would incorrectly catch TAOM_Online
        // and TAOM_Map (which are independent consumer mods, valid culprits).
        if (string.Equals(asmName, "TAOM", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(asmName, "TAOM.Dependencies", StringComparison.OrdinalIgnoreCase) ||
            asmName.StartsWith("TAOM.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var prefix in _enginePrefixes)
        {
            if (asmName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static int ShieldedCount { get { lock (_lock) return _shielded.Count; } }

    /// <summary>
    /// True when SaveShield has already attached its own finalizer to <paramref name="method"/>.
    ///
    /// PatchShield calls this to avoid stacking a second finalizer on the save/mission chain.
    /// Two finalizers on one method share a single exception slot in Harmony's generated wrapper,
    /// and whichever runs last wins — so PatchShield's unconditional swallow of the missing-API
    /// trinity would override SaveShield's co-op SAVE-LOAD rethrow and let a partially
    /// deserialised campaign load silently, which is the exact outcome that rethrow exists to
    /// prevent. SaveShield's finalizer already covers a strictly broader exception set on these
    /// methods, so PatchShield adds nothing here even in a solo session.
    /// </summary>
    public static bool IsShielding(MethodBase? method)
    {
        if (method == null) return false;
        lock (_lock) return _shielded.Contains(method);
    }
    public static long DuplicateKeyHits => Interlocked.Read(ref _duplicateKeyHits);
    public static long OtherFailureHits => Interlocked.Read(ref _otherFailureHits);
    public static long SwallowedCount => Interlocked.Read(ref _swallowedCount);

    public static IReadOnlyList<FailureRecord> RecentFailures
    {
        get { lock (_recentLock) return _recent.ToArray(); }
    }

    public static bool IsSwallowEnabled()
    {
        try
        {
            var dir = RuntimeLog.ModuleDir;
            if (string.IsNullOrEmpty(dir)) return true;
            return !File.Exists(Path.Combine(dir, SwallowDisableFlagName));
        }
        catch { return true; }
    }

    /// <summary>
    /// Installs the shield. Idempotent — methods already shielded are skipped.
    /// Call from a late-lifecycle hook (e.g., Dependencies/SubModule OnSubModuleLoad
    /// or after the save subsystem has initialised).
    /// </summary>
    public static void Install()
    {
        try
        {
            var harmony = new Harmony(HarmonyId);
            var finalizer = typeof(SaveShield).GetMethod(
                nameof(SaveShieldFinalizer),
                BindingFlags.Static | BindingFlags.NonPublic);
            if (finalizer == null)
            {
                DiagLog.Log(Tag, "could not resolve finalizer method; aborting install");
                return;
            }

            int installed = 0, skipped = 0, alreadyShielded = 0;
            foreach (var (typeFullName, _, methodName, category) in _targets)
            {
                try
                {
                    var t = ReflectionUtils.FindTypeAcrossLoadedAssemblies(typeFullName);
                    if (t == null)
                    {
                        DiagLog.Log(Tag, $"target type not loaded yet, skipping: {typeFullName} ({category})");
                        skipped++;
                        continue;
                    }

                    // Find any method named `methodName` on this type (overload-agnostic).
                    var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                              | BindingFlags.Instance | BindingFlags.Static)
                                   .Where(m => m.Name == methodName)
                                   .ToList();
                    if (methods.Count == 0)
                    {
                        DiagLog.Log(Tag, $"no method '{methodName}' on {typeFullName}; skipping");
                        skipped++;
                        continue;
                    }

                    foreach (var m in methods)
                    {
                        lock (_lock)
                        {
                            if (_shielded.Contains(m)) { alreadyShielded++; continue; }
                            try
                            {
                                harmony.Patch(m, prefix: null, postfix: null, transpiler: null,
                                    finalizer: new HarmonyMethod(finalizer));
                                _shielded.Add(m);
                                installed++;
                            }
                            catch (Exception ex)
                            {
                                skipped++;
                                DiagLog.LogCaught(Tag, $"shielding {typeFullName}.{methodName}", ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    DiagLog.LogCaught(Tag, $"Install/{typeFullName}.{methodName}", ex);
                }
            }

            DiagLog.Log(Tag, $"install complete: shielded +{installed} new, {alreadyShielded} already-shielded, {skipped} skipped");
        }
        catch (Exception ex)
        {
            DiagLog.LogCaught(Tag, "Install", ex);
        }
    }

    private static Exception? SaveShieldFinalizer(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null) return null;

        try
        {
            var ex = __exception;
            while (ex is TargetInvocationException && ex.InnerException != null)
                ex = ex.InnerException;

            var category = ResolveCategoryFor(__originalMethod);

            // Attribute and record ONCE per exception, not once per shielded frame it passes
            // through. The save chain nests — LoadResult.InitializeObjects -> SaveManager.Load ->
            // MBSaveLoad.LoadSaveGameData -> SandBoxSaveHelper.LoadGameAction -> TryLoadSave are
            // all targets — and a rethrow makes the same exception visit every outer finalizer.
            // Harmony's generated `throw finalizerResult;` resets the stack trace to the patched
            // frame (the same effect PatchShield documents), so each outer pass would walk a
            // truncated stack, resolve "(unknown)" as the culprit, and append another catalog row
            // blaming nobody — burying the one correct attribution the innermost frame produced.
            // Only the swallow path used to exist here, which terminated at the first frame; the
            // co-op rethrow is what made this reachable.
            if (AlreadyAttributed(ex))
            {
                var repeat = SaveShieldPolicy.ShouldSwallow(category, CoopPresence.IsActive, !IsSwallowEnabled());
                return repeat ? null : __exception;
            }
            MarkAttributed(ex);

            var (culpritAsm, culpritFrame) = AttributeCulprit(ex);

            // Co-op interop (2026-07-31): the swallow decision now depends on the category. A
            // SAVE-LOAD failure during a co-op session must be visible — swallowing it leaves a
            // partially deserialised campaign that the host then replicates as authoritative.
            // MISSION-INIT keeps swallowing (local fault, broken battle not corrupted campaign).
            // The disable flag still dominates both. The failure is recorded either way: the
            // rethrow path is exactly when we most want the catalog entry.
            var swallow = SaveShieldPolicy.ShouldSwallow(category, CoopPresence.IsActive, !IsSwallowEnabled());

            var isDuplicateKey = ex.GetType().Name == "DuplicateKeyException"
                                || ex.Message?.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0
                                && ex.Message.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isDuplicateKey) Interlocked.Increment(ref _duplicateKeyHits);
            else Interlocked.Increment(ref _otherFailureHits);

            // Only count an actual swallow. Codex A2 LOW (2026-05-27) fixed the sibling bug in
            // PatchShield: counting a rethrow as a swallow misleads the session summary.
            if (swallow) Interlocked.Increment(ref _swallowedCount);

            var rec = new FailureRecord
            {
                When = DateTime.UtcNow,
                Category = category,
                ExceptionType = ex.GetType().Name,
                Message = ex.Message ?? string.Empty,
                OwnerType = __originalMethod?.DeclaringType?.FullName ?? "?",
                OwnerMethod = __originalMethod?.Name ?? "?",
                CulpritAssembly = culpritAsm,
                CulpritFrame = culpritFrame,
            };

            // Push to recent + persist to catalog (both best-effort).
            lock (_recentLock)
            {
                _recent.AddFirst(rec);
                while (_recent.Count > RecentCapacity) _recent.RemoveLast();
            }
            FailedModsCatalog.Append(rec);

            DiagLog.Log(Tag,
                $"{(swallow ? "swallowed" : "rethrew")} {rec.ExceptionType} in {rec.OwnerType}.{rec.OwnerMethod} " +
                $"[{category}] culprit={rec.CulpritAssembly}: {rec.Message}");

            return swallow ? null : __exception;
        }
        catch (Exception fxEx)
        {
            DiagLog.LogCaught(Tag, "SaveShieldFinalizer/internal", fxEx);
            return __exception;  // re-throw original — internal failure
        }
    }

    private const string AttributedKey = "TAOM.SaveShield.Attributed";

    /// <summary>
    /// True when a previous (inner) SaveShield finalizer already attributed and recorded this
    /// exception. Best-effort: <c>Exception.Data</c> can be read-only or throw on exotic exception
    /// types, and a false negative only costs a duplicate catalog row.
    /// </summary>
    private static bool AlreadyAttributed(Exception ex)
    {
        try { return ex.Data.Contains(AttributedKey); }
        catch { return false; }
    }

    private static void MarkAttributed(Exception ex)
    {
        try { ex.Data[AttributedKey] = true; }
        catch { /* read-only Data — we just re-attribute, which is the pre-existing behaviour */ }
    }

    private static (string assembly, string frame) AttributeCulprit(Exception ex)
    {
        try
        {
            var stack = new StackTrace(ex, fNeedFileInfo: false);
            for (int i = 0; i < stack.FrameCount; i++)
            {
                var frame = stack.GetFrame(i);
                var method = frame?.GetMethod();
                if (method == null) continue;
                var asmName = method.DeclaringType?.Assembly.GetName().Name;
                if (string.IsNullOrEmpty(asmName)) continue;

                if (IsEngineAssembly(asmName!)) continue;

                var frameDesc = $"{method.DeclaringType?.FullName}.{method.Name}";
                return (asmName!, frameDesc);
            }
        }
        catch { }
        return ("(unknown)", "(no non-engine frame)");
    }

    private static string ResolveCategoryFor(MethodBase? method)
    {
        if (method == null) return "?";
        try
        {
            var declFull = method.DeclaringType?.FullName ?? string.Empty;
            var name = method.Name;
            foreach (var (typeFullName, _, methodName, category) in _targets)
            {
                if (declFull == typeFullName && name == methodName) return category;
            }
        }
        catch { }
        return "?";
    }
}
