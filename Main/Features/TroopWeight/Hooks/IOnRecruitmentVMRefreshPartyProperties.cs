using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;

namespace TAOM.Features.TroopWeight.Hooks;

/// Recruitment screen party-capacity readout (current size / capacity, incl. the pending cart).
public interface IOnRecruitmentVMRefreshPartyProperties
{
    void OnRecruitmentRefreshPartyProperties(RecruitmentVM vm);
}
