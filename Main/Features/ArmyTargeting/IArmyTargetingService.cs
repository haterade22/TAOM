namespace TAOM.Features.ArmyTargeting;

public interface IArmyTargetingService
{
    float GetTargetMultiplier(string candidateId, string committedTargetId, string cultureId);
}
