using TAOM.Features.SettlementGuards.Domain;

namespace TAOM.Features.SettlementGuards;

public interface ISettlementGuardService
{
    string ResolveGuardTroopId(SettlementGuardContext context, string spawnPointTag);
    string ResolveSpearItemId(string cultureId);

    /// <summary>
    /// True when the race must never spawn as a visible settlement guard (#346 — cave troll).
    /// Affects only guard-post spawning; garrison membership and siege defense are untouched.
    /// </summary>
    bool IsRaceExcludedFromGuardDuty(int raceId);
}
