using SandBox.GameComponents;
using TAOM.Features.CultureDoctrine.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Models;

/// <summary>
/// Campaign battles: the culture's morale doctrine over <c>SandboxBattleMoraleModel</c>. Two
/// seams, both thin (gamemodels.md rule 4): <c>CanPanicDueToMorale</c>, which
/// <c>CommonAIComponent.CanPanic</c> asks on the worker tick before raising the panic flag
/// (`CommonAIComponent.cs:174-176`), and <c>GetEffectiveInitialMorale</c>, which
/// <c>InitializeMorale</c> asks once per soldier (`:69-75`). Vanilla's own answer is kept as the
/// gate: a soldier vanilla would not let panic (the Loyalty and Honor perk) still cannot, and a
/// culture that never routs makes it false for everyone else. Registered last in
/// <c>SubModule.OnGameStart</c>, so <c>MissionGameModels</c> resolves this one.
/// </summary>
public class TaomBattleMoraleModel : SandboxBattleMoraleModel
{
    private readonly ICultureMoraleService _morale;

    public TaomBattleMoraleModel(ICultureMoraleService morale)
    {
        _morale = morale;
    }

    public override bool CanPanicDueToMorale(Agent agent)
        => base.CanPanicDueToMorale(agent) && _morale.CanPanic(AgentAggressionApplier.CultureOf(agent));

    public override float GetEffectiveInitialMorale(Agent agent, float baseMorale)
        => _morale.InitialMorale(AgentAggressionApplier.CultureOf(agent), base.GetEffectiveInitialMorale(agent, baseMorale));
}
