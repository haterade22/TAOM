using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using TAOM.Core.Logging;

namespace TAOM.Features.PartyIconScale.Hooks;

/// <summary>
/// Patch53 — Transpiler on private <c>MobilePartyVisual.AddCharacterToPartyIcon</c>. Replaces the
/// hardcoded vanilla <c>0.3f</c> campaign-map scale literals left in that method since v1.5.0 (the mount,
/// and the human frame's <c>ApplyScaleLocal</c>; the leader figure's own literal moved to
/// <c>MobilePartyVisualHelper.GetHumanAgentPartyVisual</c>, which <c>Patch53_PartyIconScaleHumanVisual</c>
/// rewrites) with a <c>call</c> to <see cref="PartyIconScaleConfig.GetScale"/>, so all honour the MCM "Map Figure Scale"
/// slider (default 0.15 = half vanilla). Thin entry point: all IL work lives in
/// <see cref="PartyIconScaleTranspiler"/>. Coexists with the BannerColorPersistence Postfix on the same
/// method (transpiler rewrites IL; postfix runs after).
/// </summary>
[HarmonyPatchCategory("Patch53_PartyIconScale")]
public static class Patch53_PartyIconScale
{
    private static IModLogger? _logger;
    private static MethodInfo? _getScale;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
        _getScale = AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));
    }

    public static MethodBase? TargetMethod() =>
        AccessTools.Method(typeof(MobilePartyVisual), "AddCharacterToPartyIcon");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        PartyIconScaleTranspiler.RewriteIconSites(instructions, _getScale, _logger);
}
