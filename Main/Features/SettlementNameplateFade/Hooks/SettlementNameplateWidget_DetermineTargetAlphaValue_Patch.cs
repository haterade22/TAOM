using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate;

namespace TAOM.Features.SettlementNameplateFade.Hooks;

/// <summary>
/// Postfix on <see cref="SettlementNameplateWidget"/>.<c>DetermineTargetAlphaValue</c>
/// (private float, called every frame from <c>UpdateNameplateTransparencyAndBrightness</c>
/// which itself is called from <c>OnParallelUpdate</c>). Multiplies the vanilla target alpha
/// by a fade factor computed from the widget's <c>DistanceToCamera</c>, so nameplates fade
/// out as the camera moves away from them.
///
/// Thin entry point per ADR-002: zero logic, delegates to <see cref="INameplateFadeService"/>.
/// Service reference is captured ONCE via <see cref="Initialize"/> at module-load time and
/// stored in a static field — Lazy&lt;T&gt;.Value has a non-zero per-call cost that adds up at
/// the hot-path frequency (~3000 calls/sec on a populated map). Matches the project's standard
/// pattern (<c>BannerColorPersistence</c> patches, <c>SettlementGuards</c> patches).
///
/// Threading: <c>OnParallelUpdate</c> is a multi-threaded TaleWorlds engine hook. The service
/// + provider read from <c>TaomSettings.Instance</c> through stateless getters — safe for
/// concurrent reads.
/// </summary>
[HarmonyPatch(typeof(SettlementNameplateWidget), "DetermineTargetAlphaValue")]
[HarmonyPatchCategory("Patch38_SettlementNameplateFade")]
public static class SettlementNameplateWidget_DetermineTargetAlphaValue_Patch
{
    private static INameplateFadeService _service;

    public static void Initialize(INameplateFadeService service)
    {
        _service = service;
    }

    [HarmonyPostfix]
    public static void Postfix(SettlementNameplateWidget __instance, ref float __result)
    {
        // Vanilla already returned 0 (off-screen, untracked) — nothing to fade further.
        if (__result <= 0f) return;

        var service = _service;
        if (service == null) return;

        var multiplier = service.ComputeAlphaMultiplier(__instance.DistanceToCamera);
        __result *= multiplier;
    }
}
