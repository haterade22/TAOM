using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Features.Execution.Hooks;

namespace TAOM.Features.Execution.Models;

public class TaomExecutionRelationModel : DefaultExecutionRelationModel
{
    private readonly IOnExecutionAction _executionHook;

    public TaomExecutionRelationModel(IOnExecutionAction executionHook)
    {
        _executionHook = executionHook;
    }

    public override int GetRelationChangeForExecutingHero(Hero victim, Hero hero, out bool showQuickNotification)
    {
        int baseChange = base.GetRelationChangeForExecutingHero(victim, hero, out showQuickNotification);

        var executorKingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;
        var victimKingdomId = victim?.Clan?.Kingdom?.StringId;
        var evaluatorKingdomId = hero?.Clan?.Kingdom?.StringId;

        if (executorKingdomId == null || victimKingdomId == null || evaluatorKingdomId == null)
            return baseChange;

        int modified = _executionHook.GetRelationModifier(executorKingdomId, victimKingdomId, evaluatorKingdomId, baseChange);

        if (modified == 0)
            showQuickNotification = false;

        return modified;
    }
}
