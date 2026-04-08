using System.Collections.Generic;

namespace TAOM.Features.SettlementGuards.Domain;

public sealed class GuardEntry
{
    public string TroopId { get; }
    public int Weight { get; }
    public IReadOnlyList<string> SpawnPoints { get; }

    public GuardEntry(string troopId, int weight, IReadOnlyList<string> spawnPoints)
    {
        TroopId = troopId;
        Weight = weight;
        SpawnPoints = spawnPoints;
    }
}
