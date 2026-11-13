using System.Collections.Generic;
using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.CareerSystem.Mutations;

public class MutationParams
{
    private readonly IReadOnlyDictionary<string, string> _params;

    public MutationParams(IReadOnlyDictionary<string, string> parameters)
    {
        _params = parameters ?? new Dictionary<string, string>();
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        if (!_params.TryGetValue(key, out var val)) return defaultValue;
        return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && FiniteFloatValidator.IsFinite(result)
            ? result : defaultValue;
    }

    public string GetString(string key, string defaultValue = "")
    {
        return _params.TryGetValue(key, out var val) ? val : defaultValue;
    }
}
