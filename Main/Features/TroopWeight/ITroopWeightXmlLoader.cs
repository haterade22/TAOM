using System.Collections.Generic;

namespace TAOM.Features.TroopWeight;

public interface ITroopWeightXmlLoader
{
    Dictionary<string, float> GetTroopWeights();
    void ReloadWeights();
}
