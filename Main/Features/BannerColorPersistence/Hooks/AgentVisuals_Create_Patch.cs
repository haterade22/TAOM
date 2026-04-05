using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerColorPersistence.Hooks;

/// <summary>
/// Patches AgentVisuals.Create in TaleWorlds.MountAndBlade.View.dll to disable
/// color randomness when explicit clan colors are set. Without this, the engine's
/// AddColorRandomnessData flag generates HSB variations from BodyProperties hash,
/// overriding the clan colors we set on AgentBuildData.
/// Applied manually — type lives in View assembly.
/// </summary>
public static class AgentVisuals_Create_Patch
{
    private static IBannerColorService? _service;

    public static void Initialize(IBannerColorService service)
    {
        _service = service;
    }

    public static MethodBase? TargetMethod()
    {
        var viewType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.AgentVisuals");
        return viewType == null
            ? null
            : AccessTools.Method(viewType, "Create",
                new[] { typeof(AgentVisualsData), typeof(string), typeof(bool), typeof(bool), typeof(bool) });
    }

    public static void Prefix(AgentVisualsData data)
    {
        if (!(_service?.IsAgentVisualColorsEnabled() ?? false)) return;
        if (data == null) return;
        if (data.ClothColor1Data == uint.MaxValue) return;

        data.AddColorRandomness(false);
    }
}
