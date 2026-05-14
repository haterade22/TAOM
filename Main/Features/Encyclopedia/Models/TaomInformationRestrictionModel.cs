using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace TAOM.Features.Encyclopedia.Models;

public class TaomInformationRestrictionModel : DefaultInformationRestrictionModel
{
    private readonly IEncyclopediaSettingsProvider _settings;

    public TaomInformationRestrictionModel(IEncyclopediaSettingsProvider settings)
    {
        _settings = settings;
    }

    public override bool DoesPlayerKnowDetailsOf(Hero hero)
    {
        if (_settings.ShowAllEncyclopediaCharacters)
            return true;
        return base.DoesPlayerKnowDetailsOf(hero);
    }
}
