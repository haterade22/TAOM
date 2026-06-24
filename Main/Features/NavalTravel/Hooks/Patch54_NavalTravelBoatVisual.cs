using System;
using System.Reflection;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Features.NavalTravel.Hooks;

/// <summary>
/// Patch54 — renders an at-sea party as a boat. The base game's <c>MobilePartyVisual</c> omits the
/// leader figure when <c>IsCurrentlyAtSea</c> but adds no ship (the campaign ship visual is otherwise
/// only provided by NavalDLC.View's <c>NavalMobilePartyVisual</c>), so a non-DLC sailing party would
/// render as a bare banner. Adds the configured boat mesh (base-game <c>boat_sail_on</c> by default,
/// scale 0.4 — NavalDLC's own no-ship recipe) to the party's <c>StrategicEntity</c> when at sea.
///
/// TWO hooks share <see cref="UpdateBoat"/>: the icon-rebuild chokepoint (<c>AddMobileIconComponents</c>)
/// re-adds the boat after a rebuild, and <c>OnTransitionEnded</c> catches the actual embark/disembark
/// completion (which does NOT trigger an icon rebuild, so the rebuild hook alone never saw the at-sea
/// change). Idempotent via a <c>taom_naval_boat</c> tag — the boat is found/created/removed by tag so it
/// never accumulates, follows the party as a child of <c>StrategicEntity</c>, and despawns with it.
/// Thin entry points: the show/hide decision + mesh + scale come from the service; only engine entity
/// work lives here (ADR-008, in-game-only). Whole body try/caught — never breaks the map render.
/// </summary>
[HarmonyPatchCategory("Patch54_NavalTravelBoatVisual")]
public static class Patch54_NavalTravelBoatVisual
{
    private const string BoatTag = "taom_naval_boat";

    private static INavalTravelService? _service;
    private static IModLogger? _logger;

    // Diagnostics (temporary — strip after in-game sign-off).
    private static bool _loggedActive;
    private static bool? _lastMainPartyShow;

    public static void Initialize(INavalTravelService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public static MethodBase? TargetMethod() =>
        AccessTools.Method(typeof(MobilePartyVisual), "AddMobileIconComponents");

    [HarmonyPostfix]
    public static void Postfix(MobilePartyVisual __instance, PartyBase party) =>
        UpdateBoat(__instance, party);

    /// <summary>Add/remove the boat mesh on the party's StrategicEntity based on its at-sea state.</summary>
    internal static void UpdateBoat(MobilePartyVisual visual, PartyBase party)
    {
        var service = _service;
        if (service == null || visual == null)
            return;
        var mobileParty = party?.MobileParty;
        if (mobileParty == null)
            return;
        var strategicEntity = visual.StrategicEntity;
        if (strategicEntity == null)
            return;

        if (!_loggedActive)
        {
            _loggedActive = true;
            _logger?.LogInfo("[NavalTravel][diag] Patch54 boat-visual is ACTIVE.");
        }

        try
        {
            bool atSea = mobileParty.IsCurrentlyAtSea;
            bool inTransition = mobileParty.IsTransitionInProgress;
            bool show = service.ShouldRenderBoat(atSea, inTransition);

            if (mobileParty.IsMainParty && _lastMainPartyShow != show)
            {
                _lastMainPartyShow = show;
                _logger?.LogInfo($"[NavalTravel][diag] main party atSea={atSea} inTransition={inTransition} renderBoat={show} " +
                    $"(mesh='{service.BoatMeshName}' scale={service.BoatScale}).");
            }

            var existing = strategicEntity.GetFirstChildEntityWithTag(BoatTag);

            if (show && existing == null)
            {
                var mesh = MetaMesh.GetCopy(service.BoatMeshName, true, false);
                if (mesh == null)
                {
                    _logger?.LogWarning($"[NavalTravel][diag] boat mesh '{service.BoatMeshName}' returned null for '{mobileParty.StringId}'.");
                    return;
                }

                var boat = GameEntity.CreateEmpty(strategicEntity.Scene, true, false, true);
                boat.AddMultiMesh(mesh, true);
                boat.AddTag(BoatTag);

                var frame = MatrixFrame.Identity;
                frame.rotation.ApplyScaleLocal(service.BoatScale);
                boat.SetFrame(ref frame, true);

                strategicEntity.AddChild(boat, false);

                if (mobileParty.IsMainParty)
                    _logger?.LogInfo($"[NavalTravel][diag] ADDED boat '{service.BoatMeshName}' (scale {service.BoatScale}) to main party.");
            }
            else if (!show && existing != null)
            {
                existing.Remove(0);
                if (mobileParty.IsMainParty)
                    _logger?.LogInfo("[NavalTravel][diag] removed boat from main party.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[NavalTravel][diag] boat-visual threw for '{mobileParty.StringId}': {ex.Message}");
        }
    }
}

/// <summary>
/// Second Patch54 hook — fires when an embark/disembark transition COMPLETES. The at-sea state change
/// does not trigger an icon rebuild, so <c>AddMobileIconComponents</c> alone never observes it; this is
/// the moment the boat must appear (embark) or disappear (disembark). Shares <see cref="Patch54_NavalTravelBoatVisual.UpdateBoat"/>.
/// </summary>
[HarmonyPatchCategory("Patch54_NavalTravelBoatVisual")]
public static class Patch54_NavalTravelBoatVisual_OnTransitionEnded
{
    public static MethodBase? TargetMethod() =>
        AccessTools.Method(typeof(MobilePartyVisual), "OnTransitionEnded");

    [HarmonyPostfix]
    public static void Postfix(MobilePartyVisual __instance) =>
        Patch54_NavalTravelBoatVisual.UpdateBoat(__instance, __instance?.MapEntity);
}
