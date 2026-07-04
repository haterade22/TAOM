using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Victory evaluation + application. On a win it freezes the momentum state, ends the
/// phase-machine war (lifting all three peace-block layers), then peaces out every
/// cross-side at-war pair — strictly in that order, because MakePeaceAction is blocked
/// until the phase leaves FullWar.
/// </summary>
public interface IMomentumVictoryService
{
    /// <summary>Returns the outcome decided by THIS call; None when the war continues (or already ended).</summary>
    WarOutcome CheckAndApplyVictory(MomentumWarState state);
}
