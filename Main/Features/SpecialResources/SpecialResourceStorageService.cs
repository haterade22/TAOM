using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

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
        // The floor below is Math.Max, and Math.Max(0f, NaN) is NaN: a non-finite write used to be
        // stored, survive every later Add and the save round trip, and render as int.MinValue on the
        // map bar. This is the last gate before the save, so a non-finite write is refused and the
        // previous balance stands (#558 deep review; csharp-architecture.md "Engine-Float Decision Gates").
        if (!FiniteFloatValidator.IsFinite(amount)) return;
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

        // A save written before Set refused non-finite values may carry one. Repair to 0 rather than
        // drop the key: the entry stays tracked, so the legacy-save seed does not re-fire for it.
        List<string> poisoned = null;
        foreach (var pair in _data)
            if (!FiniteFloatValidator.IsFinite(pair.Value))
                (poisoned ??= new List<string>()).Add(pair.Key);
        if (poisoned != null)
            foreach (var key in poisoned)
                _data[key] = 0f;
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
