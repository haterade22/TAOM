using System.Collections.Generic;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public interface ILandmarkService
{
    IEnumerable<LandmarkDef> GetCapitals();
}
