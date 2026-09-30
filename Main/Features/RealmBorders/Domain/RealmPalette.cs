using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Border colours by realm id, as opaque ARGB. Curated colours come from
/// <c>ModuleData/realm_borders/palette.json</c>; a realm created in play (rebels, the player's own)
/// takes the unused reserve colour farthest, by CIE76 colour distance, from every colour in use. It
/// keeps that colour for as long as this palette lives (one campaign, since the border service builds a
/// fresh palette at every session start) unless the player gives the realm a colour of their own, which
/// hands it back to the reserve. Banner colours are never used: in TAOM they leave 9 of 22 realms near
/// black and make some pairs indistinguishable.
/// </summary>
public sealed class RealmPalette
{
    private readonly Dictionary<string, uint> _curated;
    private readonly Dictionary<string, uint> _colours;
    private readonly List<uint> _reserve;
    private readonly Dictionary<string, uint> _fromReserve = new Dictionary<string, uint>(StringComparer.Ordinal);
    private string? _yourRealm;
    private uint? _yourColour;

    public RealmPalette(IReadOnlyDictionary<string, uint> curated, IReadOnlyList<uint> reserve)
    {
        if (curated == null)
            throw new ArgumentNullException(nameof(curated));
        if (reserve == null)
            throw new ArgumentNullException(nameof(reserve));
        _curated = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var pair in curated)
            _curated[pair.Key] = pair.Value;
        _colours = new Dictionary<string, uint>(_curated, StringComparer.Ordinal);
        _reserve = new List<uint>(reserve);
    }

    /// <summary>The realms the palette file names.</summary>
    public IReadOnlyCollection<string> CuratedRealms => _curated.Keys;

    /// <summary>
    /// The player's colour for a realm, or null to restore the palette's own: the file's for a curated realm,
    /// a free reserve colour for one created in play. Colours in use include it, so a reserve colour handed
    /// out afterwards keeps clear of it; a reserve colour the realm held goes back to the reserve.
    /// </summary>
    public void Override(string realmId, uint? colour)
    {
        if (realmId == null)
            throw new ArgumentNullException(nameof(realmId));
        if (_fromReserve.TryGetValue(realmId, out uint held))
        {
            _fromReserve.Remove(realmId);
            _reserve.Add(held);
        }
        if (colour.HasValue)
            _colours[realmId] = colour.Value;
        else if (_curated.TryGetValue(realmId, out uint original))
            _colours[realmId] = original;
        else
            _colours.Remove(realmId);
    }

    /// <summary>
    /// Gives the player's realm MCM's Your Realm colour, or a free colour when that is null. A realm the file
    /// names keeps its own. The colour follows the player: when their realm or the colour changes, the realm
    /// it went to last takes a free colour again. The same realm and colour a second time change nothing, so
    /// a free colour never moves.
    /// </summary>
    public void ApplyYourRealm(string? playerRealm, uint? colour)
    {
        if (playerRealm == null || _curated.ContainsKey(playerRealm))
            colour = null;
        if (playerRealm == _yourRealm && colour == _yourColour)
            return;
        if (_yourRealm != null && _yourColour.HasValue)
            Override(_yourRealm, null);
        if (playerRealm != null && colour.HasValue)
            Override(playerRealm, colour);
        (_yourRealm, _yourColour) = (playerRealm, colour);
    }

    public uint ColourOf(string realmId)
    {
        if (realmId == null)
            throw new ArgumentNullException(nameof(realmId));
        if (_colours.TryGetValue(realmId, out uint colour))
            return colour;

        if (_reserve.Count > 0)
            _fromReserve[realmId] = colour = TakeFarthestReserve();
        else
            colour = HashedColour(realmId);
        _colours[realmId] = colour;
        return colour;
    }

    /// <summary>CIE76 distance in CIELAB (D65): about 2.3 is a just-noticeable difference.</summary>
    public static double DeltaE(uint argbA, uint argbB)
    {
        var (l1, a1, b1) = ToLab(argbA);
        var (l2, a2, b2) = ToLab(argbB);
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    /// <summary>CIELAB lightness L*, 0 for black to 100 for white.</summary>
    public static double Lightness(uint argb) => ToLab(argb).L;

    private uint TakeFarthestReserve()
    {
        int best = 0;
        double bestDistance = double.MinValue;
        for (int i = 0; i < _reserve.Count; i++)
        {
            double nearest = double.MaxValue;
            foreach (uint used in _colours.Values)
                nearest = Math.Min(nearest, DeltaE(_reserve[i], used));
            if (nearest > bestDistance)
            {
                best = i;
                bestDistance = nearest;
            }
        }
        uint colour = _reserve[best];
        _reserve.RemoveAt(best);
        return colour;
    }

    /// <summary>A mid-light, saturated colour from a stable hash of the id (FNV-1a, not string.GetHashCode).</summary>
    private static uint HashedColour(string realmId)
    {
        uint hash = 2166136261u;
        foreach (char ch in realmId)
            hash = unchecked((hash ^ ch) * 16777619u);
        double hue = hash % 360u;
        return FromHsv(hue, 0.6, 0.85);
    }

    private static uint FromHsv(double hue, double saturation, double value)
    {
        double c = value * saturation, x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1)), m = value - c;
        double r, g, b;
        if (hue < 60) (r, g, b) = (c, x, 0.0);
        else if (hue < 120) (r, g, b) = (x, c, 0.0);
        else if (hue < 180) (r, g, b) = (0.0, c, x);
        else if (hue < 240) (r, g, b) = (0.0, x, c);
        else if (hue < 300) (r, g, b) = (x, 0.0, c);
        else (r, g, b) = (c, 0.0, x);
        uint Channel(double u) => (uint)Math.Round((u + m) * 255.0);
        return 0xFF000000u | (Channel(r) << 16) | (Channel(g) << 8) | Channel(b);
    }

    private static (double L, double A, double B) ToLab(uint argb)
    {
        double Linear(uint channel)
        {
            double u = channel / 255.0;
            return u <= 0.04045 ? u / 12.92 : Math.Pow((u + 0.055) / 1.055, 2.4);
        }

        double r = Linear((argb >> 16) & 0xFF), g = Linear((argb >> 8) & 0xFF), b = Linear(argb & 0xFF);
        double x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
        double y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        double z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
        double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;
        double fx = F(x), fy = F(y), fz = F(z);
        return (116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }
}
