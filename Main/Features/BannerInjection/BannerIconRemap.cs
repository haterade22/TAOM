using System.Collections.Generic;

namespace TAOM.Features.BannerInjection;

/// <summary>
/// Swaps icon ids in a banner code (#1285). A code is 10 numbers per layer, separated by dots; the first number of a
/// layer is its mesh id (BannerData), so only that field is read.
/// </summary>
internal static class BannerIconRemap
{
    private const int FieldsPerLayer = 10;

    /// <summary>The code with each mapped mesh id replaced, or null when nothing changed or the code is not a banner code.</summary>
    public static string? Remap(string? code, IReadOnlyDictionary<int, int> map)
    {
        if (string.IsNullOrEmpty(code))
            return null;
        var fields = code!.Split('.');
        if (fields.Length % FieldsPerLayer != 0)
            return null;
        var changed = false;
        for (var i = 0; i < fields.Length; i += FieldsPerLayer)
        {
            if (!int.TryParse(fields[i], out var mesh))
                return null;
            if (!map.TryGetValue(mesh, out var replacement))
                continue;
            fields[i] = replacement.ToString(System.Globalization.CultureInfo.InvariantCulture);
            changed = true;
        }
        return changed ? string.Join(".", fields) : null;
    }
}
