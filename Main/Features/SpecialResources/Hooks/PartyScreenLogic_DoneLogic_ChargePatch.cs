using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources.Hooks;

/// <summary>
/// Patch26 (#1516). <c>PartyScreenLogic.DoneLogic(bool)</c> can succeed while the party screen stays open: Talk with
/// pending changes (<c>PartyVM.ExecuteTalk</c>), a companion removed (<c>GauntletPartyScreen.OnCompanionRemoved</c>) and
/// TAOM's forced commit (<c>PartyScreenCommit</c>). On success vanilla applies the upgrades and empties
/// <c>UpgradedTroopsHistory</c>, so a later Cancel or Reset cannot undo them. The postfix charges the queued upgrade cost at that moment. On the normal Done the close then finds nothing pending.
/// A failed Done charges nothing. Faults are logged and swallowed: a charge must never keep a party screen open.
/// </summary>
[HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.DoneLogic))]
[HarmonyPatchCategory("Patch26_SpecialResources")]
public static class PartyScreenLogic_DoneLogic_ChargePatch
{
    private static IOnPartyUpgradeResourceCheck? _hook;
    private static IModLogger? _logger;

    public static void Initialize(IOnPartyUpgradeResourceCheck hook, IModLogger logger)
    {
        _hook = hook;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(bool __result)
    {
        if (!__result || _hook == null) return;

        try
        {
            var hero = Hero.MainHero;
            if (hero?.StringId == null) return;

            var kingdomId = hero.Clan?.Kingdom?.StringId;
            var cultureId = hero.Culture?.StringId;
            var spent = _hook.ChargeAppliedUpgrades(hero.StringId, kingdomId, cultureId);
            if (spent > 0f)
                SpecialResourceToasts.UpgradeSpend(_hook.GetResourceDisplayName(kingdomId, cultureId), spent,
                    _hook.GetAvailableAmount(hero.StringId, kingdomId, cultureId));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[SpecRes] Patch26-DoneLogic: charging applied upgrades failed: {ex.Message}");
        }
    }
}
