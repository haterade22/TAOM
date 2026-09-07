using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;
using TAOM.Features.Execution.Hooks;

namespace TAOM.Features.Execution.Models;

public class TaomExecutionRelationModel : DefaultExecutionRelationModel
{
    private readonly IExecutionRelationService _service;
    private readonly IPlayerContextAdapter _playerContext;

    public TaomExecutionRelationModel(IExecutionRelationService service, IPlayerContextAdapter playerContext)
    {
        _service = service;
        _playerContext = playerContext;
    }

    public override int GetRelationChangeForExecutingHero(Hero victim, Hero hero, out bool showQuickNotification)
    {
        // Boundary: convert sealed TaleWorlds heroes to participants + compute the vanilla baseline.
        // Executor and victim prefer the snapshot taken before the kill mutated them; the evaluator
        // is untouched by the kill, so it is always read live.
        int baseDelta = base.GetRelationChangeForExecutingHero(victim, hero, out bool baseShowNotification);
        var executor = ExecutionContext.ResolveExecutor(
            _playerContext.GetPlayerKingdomId(),
            _playerContext.GetPlayerCultureId());
        var victimParticipant = ExecutionContext.ResolveVictim(
            victim?.Clan?.Kingdom?.StringId,
            victim?.Culture?.StringId);
        var evaluator = new ExecutionParticipant(hero?.Clan?.Kingdom?.StringId, hero?.Culture?.StringId);

        // Delegate: all decisions live in IExecutionRelationService.
        var result = _service.GetRelationModifier(
            executor,
            victimParticipant,
            evaluator,
            baseDelta,
            baseShowNotification);

        showQuickNotification = result.ShowNotification;
        return result.RelationDelta;
    }
}
