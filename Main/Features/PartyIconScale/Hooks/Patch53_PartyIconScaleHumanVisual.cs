using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.PartyIconScale.Hooks;

/// <summary>
/// Patch53, second half. v1.5.0 moved the campaign-map leader-figure scale out of
/// <c>MobilePartyVisual.AddCharacterToPartyIcon</c> and into the new nested helper
/// <c>SandBox.View.SandBoxViewHelpers+MobilePartyVisualHelper.GetHumanAgentPartyVisual</c>, where it
/// is a bare <c>.Scale(0.3f)</c>. Harmony never feeds one method's IL to another method's transpiler,
/// so the relocated site needs its own target: without this the mount honoured the MCM "Map Figure
/// Scale" slider while the rider stayed vanilla-size, and nothing turned red because the transpiler
/// fails safe.
/// </summary>
[HarmonyPatchCategory("Patch53_PartyIconScale")]
public static class Patch53_PartyIconScaleHumanVisual
{
    private static IModLogger? _logger;
    private static MethodInfo? _getScale;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
        _getScale = AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));
    }

    public static MethodBase? TargetMethod() =>
        AccessTools.Method(
            AccessTools.TypeByName("SandBox.View.SandBoxViewHelpers+MobilePartyVisualHelper"),
            "GetHumanAgentPartyVisual");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        PartyIconScaleTranspiler.RewriteHumanVisualSite(instructions, _getScale, _logger);
}
