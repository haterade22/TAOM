using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.TroopProgression.Models;

public class TaomVolunteerModel : DefaultVolunteerModel
{
    private readonly IVolunteerTierService _volunteerTierService;

    public TaomVolunteerModel(IVolunteerTierService volunteerTierService)
    {
        _volunteerTierService = volunteerTierService;
    }

    public override int MaxVolunteerTier => _volunteerTierService.MaxVolunteerTier;
}
