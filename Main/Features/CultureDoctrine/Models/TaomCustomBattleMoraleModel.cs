using TAOM.Features.CultureDoctrine.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Models;

/// <summary>
/// Custom Battle (and the editor's test battle): the same two seams as
/// <see cref="TaomBattleMoraleModel"/> over <c>CustomBattleMoraleModel</c>, so the A/B in Custom
/// Battle measures the morale doctrine too. <c>CustomGame</c> adds its models before calling
/// the submodules' <c>OnGameStart</c> (the same shape as <c>EditorGame.cs:17-19</c>), where this
/// one is added after them.
/// </summary>
public class TaomCustomBattleMoraleModel : CustomBattleMoraleModel
{
    private readonly ICultureMoraleService _morale;

    public TaomCustomBattleMoraleModel(ICultureMoraleService morale)
    {
        _morale = morale;
    }

    public override bool CanPanicDueToMorale(Agent agent)
        => base.CanPanicDueToMorale(agent) && _morale.CanPanic(AgentAggressionApplier.CultureOf(agent));

    public override float GetEffectiveInitialMorale(Agent agent, float baseMorale)
        => _morale.InitialMorale(AgentAggressionApplier.CultureOf(agent), base.GetEffectiveInitialMorale(agent, baseMorale));
}
