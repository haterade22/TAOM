using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.WarOfTheRingMomentum.Snapshots;

namespace TAOM.Adapters;

/// <summary>
/// Converts war-event engine args into flat snapshot DTOs at the behavior boundary
/// (WarOfTheRingMomentum). Every method is null-tolerant and returns null when the
/// event can't be snapshotted — callers skip null.
/// </summary>
public interface IWarEventSnapshotAdapter
{
    BattleOutcomeSnapshot FromMapEvent(MapEvent mapEvent);
    SiegeOutcomeSnapshot FromSiege(Settlement settlement, MobileParty capturerParty);
    RaidOutcomeSnapshot FromRaid(BattleSideEnum winnerSide, RaidEventComponent component);
    ArmyGatheredSnapshot FromArmyGathered(Army army);
}
