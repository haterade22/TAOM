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
    // 0 when either parent race is immortal or the age is outside the race's fertility window.
    float GetDailyPregnancyBaseChance(
        int raceId, int spouseRaceId, float heroAge, int childCount, int clanTier,
        int aliveLords, bool playerOrSpouseInvolved);
    // Phase 9b #131 R1 — clear singleton cache on new campaign
    void ResetCache();
}
