using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Dynamic side enrollment (deviation from LOTRAOM's scripted war events, which TAOM's
/// phase machine replaced): while the phase machine is in FullWar, every kingdom with a
/// Free/Evil alignment is swept into its side; Neutral kingdoms never enroll. Bookkeeping
/// only — never declares wars or alliances (Diplomacy owns stances).
/// </summary>
public interface IMomentumEnrollmentService
{
    /// <summary>Returns true when anything changed (new enrollment / war started).</summary>
    bool SweepEnrollment(MomentumWarState state);

    /// <summary>Handles KingdomDestroyed: drops the kingdom from whichever side holds it.</summary>
    bool RemoveKingdom(MomentumWarState state, string kingdomId);
}
