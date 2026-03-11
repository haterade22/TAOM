using System.Collections.Generic;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public interface ILandmarkService
{
    IReadOnlyList<LandmarkDef> GetAllLandmarks();
    IEnumerable<LandmarkDef> GetCapitals();
    IEnumerable<LandmarkDef> GetByFaction(int factionId);
}
