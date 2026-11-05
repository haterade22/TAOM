using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// Postfix on <see cref="ClanPartiesVM"/>.<c>RefreshPartiesList</c>: appends EVERY refuge (ready,
/// building, orphan-adopted) to the clan screen's Garrisons list, so refuges are visible and
/// inspectable where a player expects standing garrisons -- a mid-raise refuge holding a deposited
/// garrison must not vanish from clan management for the build window (source parity: the module
/// appended all refuges unfiltered). The row decoration (the "(being raised)" name suffix, the
/// hidden wage slider and the honest zero wage line) is not applied here:
/// <see cref="RefugeClanRowRefreshPatch"/> re-applies it on every <c>UpdateProperties</c>, including
/// the one the row constructor triggers, so it survives the refreshes that used to wipe it (#994).
///
/// <para>The row wiring reuses the VM's own private <c>OnPartySelection</c> (via a cached
/// reflection handle, per the hot-path rule; a UI refresh is not hot, but the lookup still runs
/// once, not per refresh) so selecting a refuge row behaves exactly like selecting a vanilla
/// garrison. Expense-change and change-leader callbacks are no-ops.</para>
///
/// <para>Whole body try/catch: a clan-screen refresh must never die on a refuge row; the failure
/// mode is the row silently missing, which is the pre-patch state.</para>
/// </summary>
[HarmonyPatch(typeof(ClanPartiesVM), "RefreshPartiesList")]
[HarmonyPatchCategory("Patch75_Refuge")]
public static class RefugeClanScreenPatch
{
    private static IRefugeService? _refuges;

    // Cached once at type init (harmony-patches.md: reflection handles never resolve per call).
    // Null when the engine renames the method; the postfix then no-ops and the binding test is
    // what fails loudly.
    private static readonly MethodInfo? OnPartySelectionMethod =
        AccessTools.Method(typeof(ClanPartiesVM), "OnPartySelection");

    /// <summary>Called once from RefugeIoC at container build time.</summary>
    public static void Initialize(IRefugeService refuges)
    {
        _refuges = refuges;
    }

    [HarmonyPostfix]
    public static void Postfix(ClanPartiesVM __instance)
    {
        try
        {
            var refugeService = _refuges;
            var method = OnPartySelectionMethod;
            if (refugeService == null || method == null || __instance?.Garrisons == null)
                return;

            var refuges = refugeService.AllRefuges;
            if (refuges.Count == 0)
                return;

            var onSelect = (Action<ClanPartyItemVM>)Delegate.CreateDelegate(
                typeof(Action<ClanPartyItemVM>), __instance, method);
            Action noop = () => { };
            var disbandBehavior = Campaign.Current?.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
            var teleportationBehavior = Campaign.Current?.GetCampaignBehavior<ITeleportationCampaignBehavior>();

            bool added = false;
            foreach (var refuge in refuges)
            {
                var party = ResolveParty(refuge.PartyId);
                if (party?.Party == null)
                    continue;
                if (__instance.Garrisons.Any(row => row.Party == party.Party))
                    continue;

                // The constructor ends with RefreshValues, so RefugeClanRowRefreshPatch has already
                // decorated the row (#994).
                var row = new ClanPartyItemVM(
                    party.Party, onSelect, noop, noop,
                    ClanPartyItemVM.ClanPartyType.Garrison,
                    disbandBehavior, teleportationBehavior);
                __instance.Garrisons.Add(row);
                added = true;
            }

            if (added)
            {
                // Vanilla set the "Garrisons (N)" header before this postfix ran; re-render it so
                // the count matches the rows (the source module left it desynced).
                GameTexts.SetVariable("CURRENT", __instance.Garrisons.Count);
                __instance.GarrisonsText = GameTexts.FindText("str_clan_garrisons").ToString();
            }
        }
        catch
        {
            // A refuge row is a convenience; the clan screen rendering vanilla-only is the safe
            // degradation, and the throw would otherwise take the whole screen refresh down.
        }
    }

    private static MobileParty? ResolveParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return null;
        foreach (var party in MobileParty.All)
        {
            if (party != null && string.Equals(party.StringId, partyId, StringComparison.Ordinal))
                return party;
        }
        return null;
    }
}
