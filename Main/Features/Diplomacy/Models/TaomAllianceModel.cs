using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace TAOM.Features.Diplomacy.Models;

public class TaomAllianceModel : DefaultAllianceModel
{
    private readonly IDiplomacyService _diplomacyService;

    public TaomAllianceModel(IDiplomacyService diplomacyService)
    {
        _diplomacyService = diplomacyService;
    }

    public override int MaxNumberOfAlliances => int.MaxValue;

    public override CampaignTime MaxDurationOfAlliance => CampaignTime.Years(100);

    public override ExplainedNumber GetScoreOfStartingAlliance(
        Kingdom querierKingdom,
        Kingdom queriedKingdom,
        out TextObject explanationText,
        bool includeDescription = false)
    {
        var result = base.GetScoreOfStartingAlliance(
            querierKingdom, queriedKingdom, out explanationText, includeDescription);

        float modifier = _diplomacyService.GetAllianceScoreModifier(
            querierKingdom.StringId, queriedKingdom.StringId);

        if (modifier != 0f)
        {
            result.Add(modifier, new TextObject("{=taom_alliance_lore}Lore Alignment"));
        }

        return result;
    }
}
