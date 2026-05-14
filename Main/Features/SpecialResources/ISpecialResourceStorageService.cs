using System.Collections.Generic;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceStorageService
{
    float Get(string heroId, string resourceId);
    void Set(string heroId, string resourceId, float amount);
    void Add(string heroId, string resourceId, float delta);

    /// <summary>
    /// Returns <c>true</c> iff the (hero, resource) pair has ever been written via
    /// <see cref="Set"/> or <see cref="Add"/>, even if the current value is 0.
    /// Phase 9b deferred #133 P2 — distinguishes "never owned" from "spent to zero"
    /// so legacy-save seeding only fires on first-load, not on every kingdom-change.
    /// </summary>
    bool Contains(string heroId, string resourceId);

    Dictionary<string, float> GetAllData();
    void RestoreData(Dictionary<string, float> data);
    void ClampAll(float cap);
}
