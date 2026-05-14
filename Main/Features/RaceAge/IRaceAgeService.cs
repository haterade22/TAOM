namespace TAOM.Features.RaceAge;

public interface IRaceAgeService
{
    int GetMaxAge(int raceId);
    int GetBecomeOldAge(int raceId);
    int GetComesOfAge(int raceId);
    int GetMiddleAge(int raceId);
    int GetFertilityEndAge(int raceId);
    float GetFertilityModifier(int raceId);
    bool IsImmortal(int raceId);
    bool ShouldDieOfOldAge(int raceId, float currentAge);
    // Phase 9b #131 R1 — clear singleton cache on new campaign
    void ResetCache();
}
