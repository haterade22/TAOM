using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// The shape of <c>ModuleData/realm_borders/palette.json</c>. <c>RealmPaletteTests</c> holds the
/// shipped file to its own floors: every kingdom present, every pair of colours, reserve included, at
/// least <see cref="MinimumDeltaE"/> apart, none darker than <see cref="MinimumLightness"/>.
/// </summary>
public sealed class RealmPaletteConfig
{
    public double MinimumDeltaE { get; set; } = 15.0;

    public double MinimumLightness { get; set; } = 25.0;

    /// <summary>Kingdom id to "#RRGGBB".</summary>
    public Dictionary<string, string> Realms { get; set; } = new Dictionary<string, string>();

    /// <summary>Colours handed to kingdoms created in play, farthest first.</summary>
    public List<string> Reserve { get; set; } = new List<string>();
}
