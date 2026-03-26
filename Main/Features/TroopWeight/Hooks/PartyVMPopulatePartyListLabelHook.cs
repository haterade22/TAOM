using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.TroopWeight.Hooks;

public class PartyVMPopulatePartyListLabelHook : IOnPartyVMPopulatePartyListLabel
{
    private readonly ITroopWeightService _troopWeightService;

    public PartyVMPopulatePartyListLabelHook(ITroopWeightService troopWeightService)
    {
        _troopWeightService = troopWeightService;
    }

    public bool OnPartyVMPopulatePartyListLabel(
        ref string __result,
        MBBindingList<PartyCharacterVM> partyList,
        int limit)
    {
        try
        {
            float weightedHealthy = 0f;
            float weightedWounded = 0f;

            foreach (var item in partyList)
            {
                if (item?.Character != null)
                {
                    var weight = _troopWeightService.GetTroopWeight(item.Character);
                    var healthy = Math.Max(0, item.Number - item.WoundedCount);
                    var wounded = item.WoundedCount;

                    weightedHealthy += healthy * weight;
                    weightedWounded += wounded * weight;
                }
            }

            int totalHealthy = (int)Math.Ceiling(weightedHealthy);
            int totalWounded = (int)Math.Ceiling(weightedWounded);

            MBTextManager.SetTextVariable("COUNT", totalHealthy);
            MBTextManager.SetTextVariable("WEAK_COUNT", totalWounded);

            if (limit != 0)
            {
                MBTextManager.SetTextVariable("MAX_COUNT", limit);
                if (totalWounded > 0)
                {
                    MBTextManager.SetTextVariable("PARTY_LIST_TAG", "");
                    MBTextManager.SetTextVariable("TOTAL_COUNT", totalHealthy + totalWounded);
                    __result = new TextObject("{=str_party_list_label_with_weak}{PARTY_LIST_TAG}{TOTAL_COUNT}/{MAX_COUNT} ({COUNT} + {WEAK_COUNT}w)").ToString();
                }
                else
                {
                    MBTextManager.SetTextVariable("PARTY_LIST_TAG", "");
                    __result = new TextObject("{=str_party_list_label}{PARTY_LIST_TAG}({COUNT} / {MAX_COUNT})").ToString();
                }
            }
            else
            {
                if (totalWounded > 0)
                {
                    MBTextManager.SetTextVariable("PARTY_LIST_TAG", "");
                    __result = new TextObject("{=str_party_list_label_with_weak_without_max}{PARTY_LIST_TAG}({COUNT} + {WEAK_COUNT}w)").ToString();
                }
                else
                {
                    MBTextManager.SetTextVariable("PARTY_LIST_TAG", "");
                    __result = new TextObject("{=str_party_list_label_without_max}Party").ToString();
                }
            }
            return false;
        }
        catch
        {
            return true;
        }
    }
}
