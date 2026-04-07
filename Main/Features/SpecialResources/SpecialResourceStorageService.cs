using System;
using System.Collections.Generic;

namespace TAOM.Features.SpecialResources;

public class SpecialResourceStorageService : ISpecialResourceStorageService
{
    private Dictionary<string, float> _heroResources = new();

    public float Get(string heroId)
    {
        return _heroResources.TryGetValue(heroId, out var amount) ? amount : 0f;
    }

    public void Set(string heroId, float amount)
    {
        _heroResources[heroId] = Math.Max(0f, amount);
    }

    public void Add(string heroId, float delta)
    {
        var current = Get(heroId);
        Set(heroId, current + delta);
    }

    public Dictionary<string, float> GetAllData() => _heroResources;

    public void RestoreData(Dictionary<string, float> data)
    {
        _heroResources = data ?? new Dictionary<string, float>();
    }
}
