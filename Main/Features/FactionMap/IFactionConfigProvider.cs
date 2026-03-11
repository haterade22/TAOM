using System.Collections.Generic;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public interface IFactionConfigProvider
{
    Dictionary<string, RegionData> LoadRegions();
    Dictionary<string, FactionData> LoadFactions();
}
