using System;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;

namespace TAOM.Features.TroopWeight.Hooks;

public class RecruitmentVMRefreshPartyPropertiesHook : IOnRecruitmentVMRefreshPartyProperties
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;

    public RecruitmentVMRefreshPartyPropertiesHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
    }

    public void OnRecruitmentVMRefreshPartyProperties(RecruitmentVM recruitmentVM)
    {
        try
        {
            if (recruitmentVM?.TroopsInCart == null)
                return;

            float cartWeightedCount = 0f;
            foreach (var troop in recruitmentVM.TroopsInCart)
            {
                if (troop?.Character != null)
                {
                    var weight = _troopWeightService.GetTroopWeight(troop.Character);
                    cartWeightedCount += weight;
                }
            }

            var basePartyWeightedCount = PartyBase.MainParty?.NumberOfAllMembers ?? 0;
            var totalWeightedSize = basePartyWeightedCount + (int)Math.Ceiling(cartWeightedCount);

            recruitmentVM.CurrentPartySize = totalWeightedSize;
            recruitmentVM.IsPartyCapacityWarningEnabled = totalWeightedSize > recruitmentVM.PartyCapacity;

            GameTexts.SetVariable("LEFT", totalWeightedSize.ToString());
            GameTexts.SetVariable("RIGHT", recruitmentVM.PartyCapacity.ToString());
            recruitmentVM.PartyCapacityText = GameTexts.FindText("str_LEFT_over_RIGHT").ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TroopWeight] RecruitmentVM hook error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
