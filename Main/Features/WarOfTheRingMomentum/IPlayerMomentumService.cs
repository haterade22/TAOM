using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Player participation: records the player's momentum events (the victory gate),
/// supplies the participation multiplier, and decides whether the player sits on the
/// currently-stronger side (for the daily strength award). Pure — strengths are
/// computed by the caller via IKingdomStrengthAdapter and passed in.
/// </summary>
public interface IPlayerMomentumService
{
    void RecordPlayerEvent(MomentumActionType actionType);
    bool HasPlayerMetVictoryRequirement();
    float GetParticipationMultiplier(bool playerInvolved);
    bool IsPlayerOnStrongerSide(MomentumWarState state, float freeStrength, float evilStrength);
}
