using System.Collections.Generic;

namespace TAOM.Features.BannerInjection;

public static class BannerLayerExpander
{
    private const int FieldsPerLayer = 10;

    public static List<int[]> ParseAllLayers(string bannerCode)
    {
        var result = new List<int[]>();

        if (string.IsNullOrEmpty(bannerCode))
            return result;

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
            {
                result.Clear();
                return result;
            }

            result.Add(layer);
        }

        return result;
    }
}
