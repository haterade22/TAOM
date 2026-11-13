using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.KingdomAllegiance.Hooks;

/// <summary>
/// Patch145, #1111: postfix on the private <c>CharacterRelationCampaignBehavior.UpdateFriendshipAndEnemies(CampaignGameStarter)</c>
/// (v1.5.6, unchanged in v1.5.7), vanilla's one-time relation roll for pairs of lords, a listener on <c>OnNewGameCreatedEvent</c>. A postfix and not
/// a TAOM listener on the same event, because listeners run newest first and a TAOM one would run before vanilla's roll. The
/// service turns each Free-vs-Evil friendship into enmity; a fault keeps vanilla's relations. Main thread, once per new game.
/// Notes: docs/reference/harmony-patch-registry.md "Patch145_KingdomAllegiance".
/// </summary>
[HarmonyPatchCategory(KingdomAllegianceModule.PatchCategory)]
public static class Patch145_FriendshipSeeding_Postfix
{
    private const string TargetName = "UpdateFriendshipAndEnemies";

    private static KingdomAllegianceService? _service;
    private static IAllegianceRelationAdapter? _lords;
    private static IModLogger? _logger;

    public static void Initialize(KingdomAllegianceService service, IAllegianceRelationAdapter lords, IModLogger logger)
    {
        _service = service;
        _lords = lords;
        _logger = logger;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(CharacterRelationCampaignBehavior), TargetName, new[] { typeof(CampaignGameStarter) });

    [HarmonyPostfix]
    public static void Postfix()
    {
        var service = _service;
        var lords = _lords;
        if (service == null || lords == null)
            return;

        try
        {
            service.FlipOpposedFriendships(lords);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[KingdomAllegiance] Patch145 postfix failed, keeping vanilla's relations: {ex}");
        }
    }
}
