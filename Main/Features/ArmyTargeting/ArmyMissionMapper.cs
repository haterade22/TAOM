using TaleWorlds.CampaignSystem;

namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// Boundary converter from the engine's <c>Army.ArmyTypes</c> to TAOM's
/// <see cref="ArmyTargetingMission"/>.
///
/// Lives outside <c>TaomTargetScoreModel</c> so the model body stays a boundary conversion plus a
/// direct delegate, with no branching of its own (gamemodels.md rule 4).
/// </summary>
public static class ArmyMissionMapper
{
    /// <summary>
    /// v1.4.8 <c>Army.ArmyTypes</c> is { Besieger, Raider, Defender, Patrolling,
    /// NumberOfArmyTypes }. Patrolling and the count sentinel both map to
    /// <see cref="ArmyTargetingMission.Other"/> and pass through unmodified, as does any value a
    /// future engine version adds.
    /// </summary>
    public static ArmyTargetingMission FromArmyType(Army.ArmyTypes missionType)
    {
        switch (missionType)
        {
            case Army.ArmyTypes.Besieger: return ArmyTargetingMission.Besieger;
            case Army.ArmyTypes.Raider: return ArmyTargetingMission.Raider;
            case Army.ArmyTypes.Defender: return ArmyTargetingMission.Defender;
            default: return ArmyTargetingMission.Other;
        }
    }
}
