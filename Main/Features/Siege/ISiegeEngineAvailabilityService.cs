using System.Collections.Generic;
using TAOM.Features.Siege.Models;

namespace TAOM.Features.Siege;

public interface ISiegeEngineAvailabilityService
{
    IEnumerable<DefenderSiegeEngineKind> GetDefenderEngines(bool hasFirePerks);
}
