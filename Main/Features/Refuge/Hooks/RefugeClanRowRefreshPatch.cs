using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// Postfix on <see cref="ClanPartyItemVM"/>.<c>UpdateProperties</c> (#994): re-applies the refuge row
/// decoration after every property refresh. <see cref="RefugeClanScreenPatch"/> used to decorate rows once,
/// at list build, but <c>ClanPartiesVM.RefreshValues</c> runs each row's <c>RefreshValues</c> afterwards
/// (v1.4.8 ClanPartiesVM.cs:402-413), and <c>UpdateProperties</c> (ClanPartyItemVM.cs:1125) resets
/// <c>Name</c> to the plain party name (:1147) and <c>PartyWageSubTitleText</c> to the live wage (:1161).
/// The row constructor ends with <c>RefreshValues</c> too (:1116), so a freshly built row is decorated here
/// as well.
///
/// <para><c>UpdateProperties</c> has just rewritten <c>Name</c> when this runs, so appending the
/// "(being raised)" suffix once per call never stacks. The wage slider is hidden, because the Garrison-typed
/// row builds one whose figure is not charged for a refuge (DefaultClanFinanceModel processes neither
/// WarPartyComponents nor OwnedCaravans) and whose SetWagePaymentLimit is a silent no-op on the base
/// PartyComponent. <c>ShouldPartyHaveExpense = false</c> hides the slider panel and shows the static wage
/// line (v1.4.8 SandBox ClanPartiesRightPanel.xml:54, :343), which binds <c>PartyWageSubTitleText</c>
/// (:58), so that line is re-rendered to the honest figure: 0.</para>
///
/// <para>Whole body try/catch: a clan-screen refresh must never die on a refuge row; the failure mode is an
/// undecorated row, which is the pre-patch state. Disjoint from Patch17's TroopWeight postfix on the same
/// method, which restates the party-size fields.</para>
/// </summary>
[HarmonyPatch(typeof(ClanPartyItemVM), nameof(ClanPartyItemVM.UpdateProperties))]
[HarmonyPatchCategory("Patch75_Refuge")]
public static class RefugeClanRowRefreshPatch
{
    private static IRefugeBook? _refuges;

    /// <summary>Called once from RefugeIoC.InitializePatchStatics at container build time.</summary>
    public static void Initialize(IRefugeBook refuges)
    {
        _refuges = refuges;
    }

    [HarmonyPostfix]
    public static void Postfix(ClanPartyItemVM __instance)
    {
        try
        {
            var refugeService = _refuges;
            if (refugeService == null || __instance == null)
                return;

            var partyId = __instance.Party?.MobileParty?.StringId;
            var refuge = partyId == null ? null : refugeService.GetByPartyId(partyId);
            if (refuge == null)
                return;

            SuppressWagePanel(__instance);
            if (refuge.Building)
            {
                __instance.Name = new TextObject("{=taom_rf_clan_row_building}{NAME} (being raised)")
                    .SetTextVariable("NAME", __instance.Name).ToString();
            }
        }
        catch
        {
            // Decoration is cosmetic; an undecorated row is the safe degradation, and a throw here
            // would take the whole clan-screen refresh down.
        }
    }

    private static void SuppressWagePanel(ClanPartyItemVM row)
    {
        row.ShouldPartyHaveExpense = false;
        row.ExpenseItem = null;
        GameTexts.SetVariable("LEFT", GameTexts.FindText("str_party_wage"));
        GameTexts.SetVariable("RIGHT", 0);
        row.PartyWageSubTitleText = GameTexts.FindText("str_LEFT_colon_RIGHT").ToString();
    }
}
