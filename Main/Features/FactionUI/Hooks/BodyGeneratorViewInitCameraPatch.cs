using System;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: moves the camera of a themed character-creation screen by Kysaro's tuning (#704,
/// his <c>FaceGenCamera_Patch</c>). Target: the static
/// <c>BodyGeneratorView.InitCamera(Camera camera, Vec3 cameraPosition)</c> (v1.5.3 :828), called by the
/// face generator (from <c>OpenScene</c>, :235) and by the backstory, review and options views to place
/// their camera.
/// </summary>
[HarmonyPatch(typeof(BodyGeneratorView), nameof(BodyGeneratorView.InitCamera))]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class BodyGeneratorViewInitCameraPatch
{
    private const float VanillaVerticalFov = (float)Math.PI / 4f;
    private static bool _reported;

    static void Prefix(ref Vec3 cameraPosition, out float __state)
    {
        __state = 0f;
        try
        {
            var offsets = FactionUIPatchContext.Camera?.CurrentOffsets;
            if (offsets is not { } o)
                return;
            __state = o.Fov;
            if (o.X != 0f || o.Distance != 0f)
                cameraPosition = new Vec3(cameraPosition.x + o.X, cameraPosition.y + o.Distance, cameraPosition.z);
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(BodyGeneratorViewInitCameraPatch), ex);
        }
    }

    static void Postfix(Camera camera, float __state)
    {
        if (__state == 0f || camera == null)
            return;
        try
        {
            camera.SetFovVertical(VanillaVerticalFov + __state, Screen.AspectRatio, 0.02f, 200f);
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(BodyGeneratorViewInitCameraPatch), ex);
        }
    }
}
