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
}
