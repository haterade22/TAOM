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
}
