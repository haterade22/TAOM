using System.IO;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace TAOM.Features.BattleScenes.Hooks;

[HarmonyPatch(typeof(SandBox.MapScene), "Load")]
[HarmonyPatchCategory("Patch0_BattleScenes")]
public class MapScene_Load_DiagnosticPatch
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        Debug.Print("TAOM: === Map Scene Load Diagnostics ===", 0, Debug.DebugColor.Green, 17592186044416uL);

        ModuleInfo? selected = null;
        foreach (var mod in ModuleHelper.GetActiveModules())
        {
            var scenePath = Path.Combine(mod.FolderPath, "SceneObj", "Main_map", "scene.xscene");
            if (mod.IsActive && File.Exists(scenePath))
            {
                Debug.Print($"TAOM:   Module '{mod.Id}' has Main_map scene at: {scenePath}", 0, Debug.DebugColor.White, 17592186044416uL);
                selected = mod;
            }
        }

        if (selected != null)
            Debug.Print($"TAOM: >>> Selected map module: '{selected.Id}' (last wins)", 0, Debug.DebugColor.Green, 17592186044416uL);
        else
            Debug.Print("TAOM: WARNING - No module found with Main_map scene!", 0, Debug.DebugColor.Red, 17592186044416uL);
    }
}
