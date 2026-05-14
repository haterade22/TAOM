using System;
using System.Collections.Generic;

namespace TAOM.Features.SpecialResources;

public class SpecialResourceStorageService : ISpecialResourceStorageService
{
    private Dictionary<string, float> _data = new();

    public float Get(string heroId, string resourceId)
    {
        var key = MakeKey(heroId, resourceId);
        return _data.TryGetValue(key, out var amount) ? amount : 0f;
    }

    public void Set(string heroId, string resourceId, float amount)
    {
        _data[MakeKey(heroId, resourceId)] = Math.Max(0f, amount);
    }

    public void Add(string heroId, string resourceId, float delta)
    {
        var current = Get(heroId, resourceId);
        Set(heroId, resourceId, current + delta);
    }

    public bool Contains(string heroId, string resourceId)
    {
        return _data.ContainsKey(MakeKey(heroId, resourceId));
    }

    public Dictionary<string, float> GetAllData() => _data;

    public void RestoreData(Dictionary<string, float> data)
    {
        _data = data ?? new Dictionary<string, float>();
    }

    public void ClampAll(float cap)
    {
        foreach (var key in new List<string>(_data.Keys))
        {
            var val = _data[key];
            if (float.IsNaN(val) || float.IsInfinity(val))
                _data[key] = 0f;
            else
                _data[key] = Math.Max(0f, Math.Min(cap, val));
        }
    }

    private static string MakeKey(string heroId, string resourceId)
    {
        return string.Concat(heroId, ":", resourceId);
    }
}
