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
        Kingdom kingdomDeclaresAlliance,
        Kingdom kingdomDeclaredAlliance,
        out TextObject explanation,
        bool includeDescription = false)
    {
        var result = base.GetScoreOfStartingAlliance(
            kingdomDeclaresAlliance, kingdomDeclaredAlliance,
            out explanation, includeDescription);

        float modifier = _diplomacyService.GetAllianceScoreModifier(
            kingdomDeclaresAlliance.StringId, kingdomDeclaredAlliance.StringId);

        if (modifier != 0f)
        {
            result.Add(modifier, new TextObject("{=taom_alliance_lore}Lore Alignment"));
        }

        return result;
    }

    public override bool CanMakeAlliance(
        Kingdom kingdom,
        Kingdom targetKingdom,
        IFaction evaluatingFaction,
        out TextObject reason,
        bool includeReason = false)
    {
        if (!_diplomacyService.IsAllianceAllowed(kingdom.StringId, targetKingdom.StringId))
        {
            reason = new TextObject("{=taom_alliance_blocked}These kingdoms can never be allied.");
            return false;
        }

        return base.CanMakeAlliance(kingdom, targetKingdom, evaluatingFaction, out reason, includeReason);
    }
}
