using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.Encyclopedia.Models;

public class TaomInformationRestrictionModel : DefaultInformationRestrictionModel
{
    private readonly Func<bool> _showAll;

    public TaomInformationRestrictionModel()
        : this(() => TaomSettings.Instance?.ShowAllEncyclopediaCharacters ?? false) { }

    internal TaomInformationRestrictionModel(Func<bool> showAll) => _showAll = showAll;

    public override bool DoesPlayerKnowDetailsOf(Hero hero)
    {
        if (_showAll())
            return true;
        return base.DoesPlayerKnowDetailsOf(hero);
    }
}
