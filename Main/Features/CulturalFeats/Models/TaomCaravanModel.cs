using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomCaravanModel : DefaultCaravanModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomCaravanModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override int GetCaravanFormingCost(bool largerCaravan, bool navalCaravan)
        => _feats.ApplyCaravanCost(
            CultureFeatAdapter.FromOrNull(CharacterObject.PlayerCharacter?.Culture),
            base.GetCaravanFormingCost(largerCaravan, navalCaravan));
}
