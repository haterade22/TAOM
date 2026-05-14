using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.Siege.Models;

namespace TAOM.Features.Siege;

public interface ISiegeDefenseService
{
    void OnSiegeStarted(ISiegeEventAdapter siege);
    void OnHourlyTick();
    void OnSiegeEnded(string settlementId);
    bool IsWatchedSiege(ISiegeEventAdapter siege);
    IReadOnlyDictionary<string, ActiveSiegeDefenseEvent> ActiveEvents { get; }

    // Phase 9b #132 — R1 singleton reset for new-campaign-same-process safety
    void Reset();

    // Phase 9b #132 — flat-primitive serialization (mirrors CareerPersistenceBehavior pattern,
    // avoids SaveableTypeDefiner). Each event encoded as "defenderFactionId|deadlineTicks|accepted|rewardClaimed".
    Dictionary<string, string> SnapshotForSave();
    void RestoreFromSave(Dictionary<string, string> snapshot);
}
