// Behavioral inspiration: Alliance mod's EntityUtils.ColorFromHex (GPL v3,
// github.com/Byak0/Alliance). Implementation written from a behavioral spec at
// docs/scene-scripts/specs/cs-road.md; no Alliance source was copied. See
// docs/scene-scripts/ATTRIBUTION.md for the clean-room procedure.
using System.Globalization;

namespace TAOM.SceneScripts.Roads;

public static class HexColorParser
{
    public const uint OpaqueWhitePackedArgb = 0xFFFFFFFFu;

    public static uint ToPackedArgb(string hexColor, uint fallback = OpaqueWhitePackedArgb)
    {
        return TryParse(hexColor, out uint result) ? result : fallback;
    }

    public static bool TryParse(string hexColor, out uint packedArgb)
    {
        packedArgb = 0u;

        if (string.IsNullOrEmpty(hexColor)) return false;
        if (hexColor[0] != '#') return false;

        int hexLength = hexColor.Length - 1;
        if (hexLength != 6 && hexLength != 8) return false;

        if (!TryParseByte(hexColor, 1, out byte r)) return false;
        if (!TryParseByte(hexColor, 3, out byte g)) return false;
        if (!TryParseByte(hexColor, 5, out byte b)) return false;

        byte a = 0xFF;
        if (hexLength == 8 && !TryParseByte(hexColor, 7, out a)) return false;

        packedArgb = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        return true;
    }

    private static bool TryParseByte(string source, int startIndex, out byte value)
    {
        return byte.TryParse(
            source.Substring(startIndex, 2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value);
    }
}
