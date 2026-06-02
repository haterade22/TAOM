using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

// Boundary wrapper for the per-activation effect pipeline extracted from
// CareerPerkMissionBehavior.ExecuteAbilityEffect (Issue #102). Allocates the
// MissionAbilityExecutionContext (touches Mission.Current.MainAgent), runs the per-archetype
// executor, and emits the yellow "activated" toast. The behavior keeps ownership of the
// _activeContexts list (mission-scoped expiration) and passes a callback for the registered
// context.
public interface IAbilityEffectExecutor
{
    void Execute(string heroStringId, System.Action<MissionAbilityExecutionContext> registerContext);
}
