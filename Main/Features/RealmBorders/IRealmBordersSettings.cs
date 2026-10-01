namespace TAOM.Features.RealmBorders;

/// <summary>The realm borders' player settings, validated. Faked in the service tests.</summary>
public interface IRealmBordersSettings
{
    bool Enabled { get; }

    /// <summary>Draw the mod-style heraldic bands instead of the Atlas look.</summary>
    bool HeraldicBands { get; }

    /// <summary>Put the gold cord on the player's own realm's borders.</summary>
    bool GildPlayerRealm { get; }

    /// <summary>Multiplies every border width, 0.5 to 3.</summary>
    float WidthScale { get; }

    /// <summary>Camera distance at which the borders start to appear.</summary>
    float FadeStartDistance { get; }

    /// <summary>Camera distance at which the borders are fully drawn; above the start.</summary>
    float FullOpacityDistance { get; }

    /// <summary>Draw over hills and map models (no depth test) rather than hidden behind ridges.</summary>
    bool DrawThroughTerrain { get; }

    bool RealmNames { get; }

    bool CrossingNotices { get; }

    /// <summary>Tint each realm's land in its colour between the borders.</summary>
    bool FillLands { get; }

    /// <summary>How strongly the land is tinted, 0.05 to 0.8.</summary>
    float FillStrength { get; }

    /// <summary>The engine blend mode to draw with, by name; null keeps the material's own.</summary>
    string? BlendMode { get; }

    /// <summary>The engine material to draw from; null picks the first that exists.</summary>
    string? MaterialName { get; }

    /// <summary>Lay the parchment map over the campaign map at full zoom-out, under the borders.</summary>
    bool ParchmentMap { get; }

    /// <summary>Changes whenever a realm's colour field changes, so a repaint can follow.</summary>
    int ColourVersion { get; }

    /// <summary>The player's colour for a realm, or null to keep the palette's.</summary>
    uint? ColourOverride(string realm);

    /// <summary>
    /// The player's colour for their own realm when the palette names none for it (their clan's land while it
    /// serves no kingdom, then a kingdom they found), or null for a free colour.
    /// </summary>
    uint? YourRealmColour { get; }
}
