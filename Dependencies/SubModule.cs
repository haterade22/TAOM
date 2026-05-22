using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;

namespace TAOM.Dependencies;

/// <summary>
/// Pre-Native module that provides Harmony (forked) and UIExtenderEx patches.
/// Must load before Native so patches are in place before any prefabs/brushes are loaded.
/// </summary>
public class SubModule : MBSubModuleBase
{
    private const string GuardHarmonyId = "TAOM.HarmonyGuard";

    static SubModule()
    {
        EarlyLog.Info("[TAOM.Dependencies] Static init: loading TaleWorlds.Engine.GauntletUI");
        Assembly.Load("TaleWorlds.Engine.GauntletUI");

        UIConfig.DoNotUseGeneratedPrefabs = true;
        EarlyLog.Info("[TAOM.Dependencies] Static init: DoNotUseGeneratedPrefabs = true");
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        EarlyLog.Info($"[TAOM.Dependencies] Harmony forked v{typeof(Harmony).Assembly.GetName().Version} loaded from {typeof(Harmony).Assembly.GetName().Name}");

        try
        {
            ApplyHarmonyGuards();
            EarlyLog.Info("[TAOM.Dependencies] UnpatchAll guard applied");
        }
        catch (Exception ex)
        {
            EarlyLog.Error($"[TAOM.Dependencies] Failed to apply Harmony guards: {ex.Message}");
        }

        CheckForDuplicateHarmony();

        try
        {
            // Codex review 2026-05-22 (P1): `_ = typeof(UIExtender)` only fetches the Type
            // object — it does NOT execute the class's static constructor where
            // UIConfigPatch.Patch / ViewModelPatch.Patch / etc. are applied.
            // RunClassConstructor forces the static cctor to run (idempotent — JIT
            // marks the type as initialized after the first call).
            RuntimeHelpers.RunClassConstructor(typeof(Bannerlord.UIExtenderEx.UIExtender).TypeHandle);
            EarlyLog.Info("[TAOM.Dependencies] UIExtenderEx static cctor executed (system patches applied)");
        }
        catch (Exception ex)
        {
            EarlyLog.Error($"[TAOM.Dependencies] UIExtenderEx initialization failed: {ex.Message}");
        }

        EarlyLog.Info("[TAOM.Dependencies] OnSubModuleLoad complete");
    }

    private static void ApplyHarmonyGuards()
    {
        var harmony = new Harmony(GuardHarmonyId);
        var unpatchAll = AccessTools.Method(typeof(Harmony), nameof(Harmony.UnpatchAll));
        harmony.Patch(unpatchAll, prefix: new HarmonyMethod(typeof(SubModule), nameof(UnpatchAllGuard)));
    }

    private static bool UnpatchAllGuard(string harmonyID)
    {
        if (harmonyID is null)
        {
            EarlyLog.Error("[TAOM.Dependencies] Blocked UnpatchAll(null) -- would wipe all Harmony patches globally");
            return false;
        }
        return true;
    }

    private static void CheckForDuplicateHarmony()
    {
        var harmonyAssembly = typeof(Harmony).Assembly;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "0Harmony" && asm != harmonyAssembly)
            {
                EarlyLog.Error($"[TAOM.Dependencies] Another 0Harmony.dll detected: {asm.FullName} at {asm.Location}. May cause patching conflicts.");
                return;
            }
        }
        EarlyLog.Info("[TAOM.Dependencies] No duplicate Harmony assemblies detected");
    }
}
