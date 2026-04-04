namespace TAOM.Features.ArmyTargeting;

public interface IArmyTargetingService
{
    float GetTargetMultiplier(string candidateId, string committedTargetId, string factionId);
    float GetStrengthMultiplier(string factionId);
    float GetDistanceCompensation(string factionId, string targetId);
}
