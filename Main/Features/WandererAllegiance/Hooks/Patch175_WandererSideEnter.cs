using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.WandererAllegiance.Hooks;

/// <summary>
/// Patch175 (docs/features/wanderer-allegiance.md, "Where wanderers stand"), second class of the category: inside vanilla's two
/// wanderer-placing routines (<see cref="Patch175_WandererSideScope"/>), a wanderer who would enter a town of the opposite side
/// enters another town of its own side instead. Target: <c>private static void EnterSettlementAction.ApplyInternal(Hero hero,
/// MobileParty mobileParty, Settlement settlement, EnterSettlementDetail detail, ...)</c> (v1.5.7 EnterSettlementAction.cs:18), which
/// <c>ApplyForCharacterOnly</c> (:96-100) calls right after <c>hero.StayingInSettlement = settlement</c>. The public wrapper is 19 IL
/// bytes, small enough for the JIT to inline into its callers, so the patch sits on the method below it. A void prefix that
/// rewrites the <c>settlement</c> argument and the hero's staying settlement, so vanilla still runs and no bool prefix is added.
/// Outside the scope, with a party, or when the hero's staying settlement is not the argument, it returns at once.
/// Callers, thread, PatchShield and co-op notes: docs/reference/harmony-patch-registry.md "Patch175_WandererSideTowns".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(Category)]
public static class Patch175_WandererSideEnter
{
    internal const string Category = "Patch175_WandererSideTowns";

    /// <summary>How many of vanilla's placing routines are running on this thread (the campaign main thread); the scope class opens and closes it.</summary>
    [ThreadStatic]
    internal static int Depth;

    private static WandererTownAdapter? _adapter;

    internal static void Initialize(WandererTownAdapter adapter) => _adapter = adapter;

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod() => AccessTools.Method(typeof(EnterSettlementAction), "ApplyInternal");

    [HarmonyPrefix]
    public static void Prefix(Hero hero, MobileParty mobileParty, ref Settlement settlement)
    {
        if (Depth <= 0 || _adapter == null || mobileParty != null || hero == null || hero.StayingInSettlement != settlement)
            return;
        try
        {
            if (_adapter.Applies())
                _adapter.TryRedirect(hero, ref settlement);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("WandererAllegiance", "EnterSettlementAction.ApplyInternal", ex);
        }
    }
}
