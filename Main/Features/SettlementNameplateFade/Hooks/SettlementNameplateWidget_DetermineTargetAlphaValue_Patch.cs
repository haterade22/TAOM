using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Features.SettlementNameplateFade.Hooks;

/// <summary>
/// Postfix on <see cref="SettlementNameplateWidget"/>.<c>DetermineTargetAlphaValue</c>
/// (private float, called every frame from <c>UpdateNameplateTransparencyAndBrightness</c>
/// which itself is called from <c>OnParallelUpdate</c>). Two adjustments to the vanilla target,
/// in this order: the relation floor (enemy and allied plates lifted from vanilla's neutral 0.35
/// to the own-faction 0.5, #591) and then the distance fade multiplier, so a raised plate still
/// fades to nothing at the far edge.
///
/// Thin entry point per ADR-002: zero logic, delegates to <see cref="INameplateFadeService"/> and
/// <see cref="INameplateRelationAlphaService"/>. Both references are captured ONCE via
/// <see cref="Initialize"/> at module-load time and stored in static fields — Lazy&lt;T&gt;.Value
/// has a non-zero per-call cost that adds up at the hot-path frequency (~3000 calls/sec on a
/// populated map). Matches the project's standard pattern (<c>BannerColorPersistence</c> patches,
/// <c>SettlementGuards</c> patches).
///
/// Threading: <c>OnParallelUpdate</c> is a multi-threaded TaleWorlds engine hook. The services
/// read stateless values (the relation service holds none; the fade provider reads
/// <c>TaomSettings.Instance</c> through stateless getters) — safe for concurrent reads. The two
/// widget properties read here (<c>RelationType</c>, <c>IsTracked</c>) are plain backing fields.
/// </summary>
[HarmonyPatch(typeof(SettlementNameplateWidget), "DetermineTargetAlphaValue")]
[HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
public static class SettlementNameplateWidget_DetermineTargetAlphaValue_Patch
{
    private static INameplateFadeService? _service;
    private static INameplateRelationAlphaService? _relationAlpha;

    public static void Initialize(INameplateFadeService service, INameplateRelationAlphaService relationAlpha)
    {
        _service = service;
        _relationAlpha = relationAlpha;
    }

    [HarmonyPostfix]
    public static void Postfix(SettlementNameplateWidget __instance, ref float __result)
    {
        // Vanilla already returned 0 (off-screen, untracked) — nothing to raise or fade further.
        if (__result <= 0f) return;

        var relationAlpha = _relationAlpha;
        if (relationAlpha != null)
            __result = relationAlpha.Adjust(__result, __instance.RelationType, __instance.IsTracked);

        var service = _service;
        if (service == null) return;

        var multiplier = service.ComputeAlphaMultiplier(__instance.DistanceToCamera);
        __result *= multiplier;
    }
}
