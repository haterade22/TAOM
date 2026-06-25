using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;

namespace TAOM.Features.NavalTravel.Hooks;

/// <summary>
/// Patch57 — prevents a native AV CTD introduced by enabling naval travel. The vanilla
/// <c>AIMoveToNearestLandBehavior.AiHourlyTick</c> fires only for a party already
/// <c>IsCurrentlyAtSea</c> (inert in vanilla TAOM, since nothing ever reaches sea) and calls the
/// native cross-region land-pathfind <c>MapScene.GetNearestFaceCenterForPositionWithPath</c>. On
/// TAOM_Map the naval region-map navigation infrastructure that pathfind dereferences is never built
/// (#120), so the call AVs (<c>0xC0000005</c> reading <c>0x4</c>) and crashes to desktop on the hourly
/// AI tick — for ANY party at sea, including the player once they sail. The AV is in native code (an
/// <c>AccessViolationException</c> corrupted-state exception a managed Finalizer can't reliably catch,
/// unlike the managed-NRE Patch49/50 finalizers), so the only fix is to not make the call: this Prefix
/// skips the behavior whenever the feature is enabled (the player disembarks manually by clicking land;
/// AI parties are covered too). When the feature is off the behavior is inert anyway, so we leave it
/// running (vanilla). The skip decision is the pure
/// <see cref="INavalTravelService.ShouldSuppressAtSeaLandRescue"/>.
///
/// Targets the internal vanilla type by name (engine-drift-safe: if it can't bind, <see cref="TargetMethod"/>
/// logs and returns null and SubModule's try/catch leaves the guard off rather than failing module load).
/// Crash report 2026-06-25 (#296). See docs/features/naval-travel.md + the #296 crash RCA.
/// </summary>
[HarmonyPatchCategory("Patch57_NavalAtSeaLandRescueGuard")]
public static class Patch57_NavalAtSeaLandRescueGuard
{
    private const string BehaviorTypeName =
        "TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AIMoveToNearestLandBehavior";

    private static INavalTravelService? _service;
    private static IModLogger? _logger;

    public static void Initialize(INavalTravelService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName(BehaviorTypeName);
        if (type == null)
        {
            _logger?.LogWarning($"[NavalTravel] Patch57: {BehaviorTypeName} not found — at-sea rescue guard not applied (engine drift?).");
            return null;
        }

        var method = AccessTools.Method(type, "AiHourlyTick");
        if (method == null)
            _logger?.LogWarning("[NavalTravel] Patch57: AIMoveToNearestLandBehavior.AiHourlyTick not found — at-sea rescue guard not applied (engine drift?).");
        return method;
    }

    /// <summary>
    /// Returns false to SKIP the vanilla at-sea→nearest-land rescue tick (the crashing native pathfind)
    /// while naval travel is enabled; true to run vanilla otherwise.
    /// </summary>
    [HarmonyPrefix]
    public static bool Prefix() => !(_service?.ShouldSuppressAtSeaLandRescue ?? false);
}
