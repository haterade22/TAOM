using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Decides, once per mission, whether the enlisted soldier gets the Order of Battle deployment
/// screen (#576). He never does: the answer is <c>false</c> exactly when
/// <see cref="BattleCommandPolicy.ShouldStripPlayerCommand"/> is true, so this seam and the role
/// strip cannot gate apart, and <c>null</c> (vanilla decides) everywhere else.
///
/// Both log lines are INFO and ungated on purpose. The engine caches the model's answer once per
/// mission (<c>BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle</c>), so each fires
/// at most once per battle, and this is the line a player report turns on. DEBUG would ride
/// FileLogger's async queue, which a native CTD discards.
/// </summary>
public sealed class EnlistmentDeploymentService : IEnlistmentDeploymentService
{
    private readonly IEnlistmentStateQuery _query;
    private readonly IEncounterAdapter _encounter;
    private readonly IModLogger _logger;

    public EnlistmentDeploymentService(IEnlistmentStateQuery query, IEncounterAdapter encounter, IModLogger logger)
    {
        _query = query;
        _encounter = encounter;
        _logger = logger;
    }

    public bool? CanPlayerSideDeployWithOrderOfBattle()
    {
        var state = _query.State;
        var leads = _encounter.IsMainPartyLeadingItsBattleSide;

        // No map event: nothing to decide about. The two mission behaviors take the same early
        // return; a mission with no player map event is not an enlisted battle.
        if (leads == null)
            return null;

        if (!BattleCommandPolicy.ShouldStripPlayerCommand(state, leads.Value))
        {
            if (state != EnlistmentState.NotEnlisted)
                _logger?.LogInfo(
                    $"[Enlistment] Order of Battle gate left to vanilla: state {state}, player leads the side: {leads.Value}");
            return null;
        }

        _logger?.LogInfo(
            "[Enlistment] Order of Battle deployment suppressed: enlisted soldier in the commander's battle, " +
            "he takes orders and assigns no formation (#576)");
        return false;
    }
}
