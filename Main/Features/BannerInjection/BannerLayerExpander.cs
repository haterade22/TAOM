using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TAOM.Features.BannerInjection;

public static class BannerLayerExpander
{
    private const int FieldsPerLayer = 10;
    private static readonly ConcurrentDictionary<string, List<int[]>> _cache = new();

    public static List<int[]> ParseAllLayers(string bannerCode)
    {
        if (string.IsNullOrEmpty(bannerCode))
            return new List<int[]>();

        return _cache.GetOrAdd(bannerCode, ParseAllLayersUncached);
    }

    private static List<int[]> ParseAllLayersUncached(string bannerCode)
    {
        var result = new List<int[]>();
        var parts = bannerCode.Split('.');

        for (var i = 0; i + FieldsPerLayer <= parts.Length; i += FieldsPerLayer)
        {
            var layer = new int[FieldsPerLayer];
            var valid = true;

            for (var j = 0; j < FieldsPerLayer; j++)
            {
                if (!int.TryParse(parts[i + j], out layer[j]))
                {
                    valid = false;
                    break;
                }
            }

            if (!valid)
                return new List<int[]>();

            result.Add(layer);
        }

        return result;
    }
}
