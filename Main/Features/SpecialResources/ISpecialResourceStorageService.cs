using System.Collections.Generic;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceStorageService
{
    float Get(string heroId, string resourceId);
    void Set(string heroId, string resourceId, float amount);
    void Add(string heroId, string resourceId, float delta);
    Dictionary<string, float> GetAllData();
    void RestoreData(Dictionary<string, float> data);
    void ClampAll(float cap);
}
