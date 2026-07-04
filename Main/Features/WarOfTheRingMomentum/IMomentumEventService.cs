using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Features.WarOfTheRingMomentum.Snapshots;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Momentum scoring + decay. All methods take flat snapshots + the campaign clock in
/// hours (CampaignTime.Now.ToHours at the behavior boundary) — pure and fully testable.
/// </summary>
public interface IMomentumEventService
{
    void ProcessBattle(BattleOutcomeSnapshot battle, MomentumWarState state, double nowHours);
    void ProcessSiege(SiegeOutcomeSnapshot siege, MomentumWarState state, double nowHours);
    void ProcessRaid(RaidOutcomeSnapshot raid, MomentumWarState state, double nowHours);
    void ProcessArmyGathered(ArmyGatheredSnapshot army, MomentumWarState state, double nowHours);

    /// <summary>Decay pass on both sides, then the daily strength-differential award.</summary>
    void ProcessDailyTick(MomentumWarState state, double nowHours);
}
