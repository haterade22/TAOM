using System;
using System.Linq;
using System.Reflection;
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
        // Force GauntletUI assembly load before UIExtenderEx static ctor fires
        Assembly.Load("TaleWorlds.Engine.GauntletUI");

        // Force XML prefab parsing — without this, the game uses pre-generated prefabs
        // that don't contain TAOM's custom brushes/sprites. Must be set BEFORE any
        // prefab is loaded (i.e., before Native). The UIConfigPatch Harmony prefix
        // then blocks anything from setting it back to false.
        UIConfig.DoNotUseGeneratedPrefabs = true;
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        ApplyHarmonyGuards();
        CheckForDuplicateHarmony();

        // Touching the UIExtender type triggers its static constructor, which applies:
        // 1. UIConfigPatch — blocks DoNotUseGeneratedPrefabs setter (keeps it true)
        // 2. ViewModelPatch — patches ViewModel ctor + ExecuteCommand
        // 3. WidgetPrefabPatch — transpiles WidgetPrefab.LoadFrom for XML injection
        // 4. BrushFactoryManager — patches GetBrush/Brushes for custom brushes
        // 5. WidgetFactoryManager — patches CreateBuiltinWidget/GetCustomType for custom widgets
        _ = typeof(Bannerlord.UIExtenderEx.UIExtender);
    }

    private static void ApplyHarmonyGuards()
    {
        var harmony = new Harmony(GuardHarmonyId);
        var unpatchAll = AccessTools.Method(typeof(Harmony), nameof(Harmony.UnpatchAll));
        harmony.Patch(unpatchAll, prefix: new HarmonyMethod(typeof(SubModule), nameof(UnpatchAllGuard)));
    }

    /// <summary>
    /// Blocks UnpatchAll(null) which would wipe ALL patches across all mods in the AppDomain.
    /// </summary>
    private static bool UnpatchAllGuard(string harmonyID)
    {
        if (harmonyID is null)
        {
            FileLog.Log("[TAOM.Dependencies] Blocked UnpatchAll(null) — would wipe all Harmony patches globally.");
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
                FileLog.Log($"[TAOM.Dependencies] WARNING: Another 0Harmony.dll detected: {asm.FullName} at {asm.Location}. This may cause patching conflicts.");
            }
        }
    }
}
