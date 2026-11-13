using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TAOM.Features.KingsMuster;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms.Hooks;

/// <summary>
/// A liege calls the holders of the titles directly below their own to their army for less influence
/// (<c>army_discount</c> in &lt;Consequences&gt;). A postfix on the army model the campaign actually uses (TAOM's own,
/// when TAOM replaces it), patched once at session launch. The check is one set lookup per call.
/// It does not stack with the King's Muster: when that discount already lowered the call, only the part of the liege
/// discount that goes beyond it applies, so the larger of the two wins (<see cref="LiegeArmyDiscountRule"/>).
/// </summary>
internal static class LiegeArmyDiscount
{
    internal static RealmsCampaign? Behavior;
    private static readonly HashSet<MethodBase> Patched = new();
    private static KingsMusterService? _muster;

    public static void Patch()
    {
        try
        {
            var model = TaleWorlds.CampaignSystem.Campaign.Current?.Models?.ArmyManagementCalculationModel;
            if (model == null)
                return;
            var method = AccessTools.Method(model.GetType(), nameof(ArmyManagementCalculationModel.CalculatePartyInfluenceCost),
                new[] { typeof(MobileParty), typeof(MobileParty) });
            if (method == null || !Patched.Add(method))
                return;
            new Harmony("TAOM.Realms.LiegeArmyDiscount").Patch(method, postfix: new HarmonyMethod(typeof(LiegeArmyDiscount), nameof(Postfix)));
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] liege army discount is off: {ex.Message}");
        }
    }

    private static void Postfix(MobileParty __0, MobileParty __1, ref int __result)
    {
        var behavior = Behavior;
        if (behavior == null || __result <= 0 || !behavior.Settings.LiegeArmyDiscount)
            return;
        var liege = __0?.LeaderHero?.StringId;
        var vassal = __1?.LeaderHero?.StringId;
        if (liege == null || vassal == null)
            return;
        var discount = behavior.Config.Feudal.ArmyDiscount;
        if (discount > 0 && behavior.IsLiegeOf(liege, vassal))
            __result = Math.Max(0, (int)Math.Round(__result * LiegeArmyDiscountRule.RemainingFactor(MusterFactor(__0), discount)));
    }

    // The factor the King's Muster already applied to this call (1 when it applied none or cannot be read).
    private static float MusterFactor(MobileParty? leader)
    {
        try
        {
            // A singleton (KingsMusterModule), so one resolve serves every session.
            _muster ??= IoC.Resolve<KingsMusterService>();
            return _muster?.AppliedFactor(RulerFactsReader.IsKingdomRuler(leader), RulerFactsReader.IsLedByMainHero(leader)) ?? 1f;
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] liege army discount could not read the King's Muster: {ex.Message}");
            return 1f;
        }
    }
}
