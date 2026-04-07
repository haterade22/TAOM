using System.Collections.Generic;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceStorageService
{
    float Get(string heroId);
    void Set(string heroId, float amount);
    void Add(string heroId, float delta);
    Dictionary<string, float> GetAllData();
    void RestoreData(Dictionary<string, float> data);
}
