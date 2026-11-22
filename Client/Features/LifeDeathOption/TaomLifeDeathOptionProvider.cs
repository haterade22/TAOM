using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.LifeDeathOption;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace TAOM.Client.Features.LifeDeathOption;

public sealed class TaomLifeDeathOptionProvider : ICampaignOptionProvider
{
    private readonly LifeDeathOptionService _service = new LifeDeathOptionService(new LoadedModuleIds());

    public IEnumerable<ICampaignOptionData> GetGameplayCampaignOptions() => Offer(CampaignOptionEnableState.Disabled);

    public IEnumerable<ICampaignOptionData> GetCharacterCreationCampaignOptions() => Offer(CampaignOptionEnableState.DisabledLater);

    private IEnumerable<ICampaignOptionData> Offer(CampaignOptionEnableState state)
    {
        if (!_service.ShouldOffer()) yield break;
        yield return new BooleanCampaignOptionData(
            LifeDeathOptionService.OptionId,
            LifeDeathOptionService.PriorityIndex,
            state,
            () => !CampaignOptions.IsLifeDeathCycleDisabled ? 1f : 0f,
            value => CampaignOptions.IsLifeDeathCycleDisabled = value == 0f);
    }
}
